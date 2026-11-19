package com.winnigames2024.geodash;

import java.io.File;
import java.io.FileOutputStream;
import java.util.Random;

/** Встроенный саундтрек: генерируется кодом в assets/songs при первом запуске. */
public class Soundtrack {

    public static final int RATE = 44100;

    public static class Track {
        public int id;                 // -1, -2, -3
        public String name, artist = "Winnigames2024";
        public double bpm;
        public int bars, root;
        public int[] scale, progression;
        public double density, drive;
        public boolean doubleTime;

        Track(int id, String name, double bpm, int bars, int root,
              int[] scale, int[] prog, double density, boolean dbl, double drive) {
            this.id = id; this.name = name; this.bpm = bpm; this.bars = bars; this.root = root;
            this.scale = scale; this.progression = prog; this.density = density;
            this.doubleTime = dbl; this.drive = drive;
        }
    }

    public static final Track[] TRACKS = {
            new Track(-1, "Neon Start", 128, 48, 45,
                    new int[]{0,2,4,5,7,9,11}, new int[]{0,5,3,4}, 0.55, false, 0.25),
            new Track(-2, "Static Rush", 152, 56, 43,
                    new int[]{0,2,3,5,7,8,10}, new int[]{0,3,5,4}, 0.75, true, 0.45),
            new Track(-3, "Hyperdrive", 178, 64, 41,
                    new int[]{0,1,3,5,7,8,10}, new int[]{0,6,3,5}, 0.95, true, 0.65)
    };

    public static Track get(int id) {
        for (Track t : TRACKS) if (t.id == id) return t;
        return null;
    }

    public static boolean isBuiltIn(int id) { return id < 0 && get(id) != null; }

    public static File path(int id) { return new File(Store.songs(), "builtin" + Math.abs(id) + ".wav"); }

    /** Генерирует трек, если его ещё нет. */
    public static File ensure(int id) {
        Track t = get(id);
        if (t == null) return null;
        File f = path(id);
        if (f.exists() && f.length() > 200000) return f;
        float[] pcm = render(t);
        writeWav(f, pcm);
        return f;
    }

    public static void ensureAll() { for (Track t : TRACKS) ensure(t.id); }

    // ------------------------------------------------------------ синтез

    static double freq(int midi) { return 440.0 * Math.pow(2, (midi - 69) / 12.0); }

    public static float[] render(Track d) {
        double spb = 60.0 / d.bpm, stepLen = spb / 4.0;
        int steps = d.bars * 16;
        int total = (int) (steps * stepLen * RATE) + RATE;
        float[] buf = new float[total];
        Random rnd = new Random(d.id * 7919L);
        int[] chordTones = {0, 2, 4};

        for (int s = 0; s < steps; s++) {
            int bar = s / 16, sixteenth = s % 16;
            int deg = d.progression[bar % d.progression.length];
            int chordRoot = d.root + d.scale[deg % d.scale.length];
            double t0 = s * stepLen;
            boolean drop = bar % 16 >= 8;

            if (sixteenth % 2 == 0)
                voice(buf, t0, stepLen * (d.doubleTime ? 1.0 : 1.6), freq(chordRoot - 12), 0.34, 2, 0.004, 0.09, d.drive);
            if (sixteenth % 4 == 0) kick(buf, t0, 0.42);
            if (sixteenth % 8 == 4) noise(buf, t0, 0.13, 0.3, 0.55);
            if (sixteenth % 2 == 1 && (drop || sixteenth % 4 == 1)) noise(buf, t0, 0.04, 0.11, 0.9);

            if (rnd.nextDouble() < (drop ? d.density : d.density * 0.6)) {
                int tone = chordTones[(s + bar) % chordTones.length];
                int oct = drop ? 12 : 0;
                int note = chordRoot + d.scale[(deg + tone) % d.scale.length] + 12 + oct;
                voice(buf, t0, stepLen * (d.doubleTime ? 0.9 : 1.4), freq(note),
                        drop ? 0.19 : 0.13, 1, 0.003, 0.05, d.drive * 0.6);
                voice(buf, t0 + stepLen * 3, stepLen * 0.7, freq(note), 0.06, 1, 0.003, 0.04, 0);
            }

            if (sixteenth == 0)
                for (int ct : chordTones) {
                    int note = chordRoot + d.scale[(deg + ct) % d.scale.length];
                    voice(buf, t0, spb * 3.6, freq(note), 0.055, 0, 0.08, 0.6, 0);
                }
        }

        float peak = 0.0001f;
        for (float v : buf) peak = Math.max(peak, Math.abs(v));
        float norm = 0.89f / peak;
        for (int i = 0; i < buf.length; i++) buf[i] = (float) (Math.tanh(buf[i] * norm * 1.25) * 0.92);
        return buf;
    }

    /** wave: 0 sine, 1 square, 2 saw */
    static void voice(float[] buf, double start, double dur, double f, double amp,
                      int wave, double atk, double rel, double drive) {
        int i0 = (int) (start * RATE), n = (int) (dur * RATE);
        if (i0 < 0 || n <= 0) return;
        double ph = 0, inc = 2 * Math.PI * f / RATE, lp = 0;
        for (int i = 0; i < n; i++) {
            int idx = i0 + i;
            if (idx >= buf.length) break;
            double t = (double) i / RATE;
            double env = t < atk ? t / Math.max(1e-5, atk)
                    : Math.max(0, 1 - (t - atk) / Math.max(1e-5, dur - atk)) * Math.exp(-(t - atk) / Math.max(1e-4, rel));
            double v;
            if (wave == 0) v = Math.sin(ph);
            else if (wave == 1) v = Math.signum(Math.sin(ph)) * 0.7;
            else v = 2 * ((ph / (2 * Math.PI)) % 1.0) - 1;
            if (drive > 0) v = Math.tanh(v * (1 + drive * 4)) * 0.8;
            lp += (v - lp) * 0.45;
            buf[idx] += (float) (lp * amp * env);
            ph += inc;
        }
    }

    static void kick(float[] buf, double start, double amp) {
        int i0 = (int) (start * RATE), n = (int) (0.17 * RATE);
        double ph = 0;
        for (int i = 0; i < n; i++) {
            int idx = i0 + i;
            if (idx < 0 || idx >= buf.length) break;
            double t = (double) i / RATE;
            double f = 130 * Math.exp(-t * 26) + 44;
            ph += 2 * Math.PI * f / RATE;
            buf[idx] += (float) (Math.sin(ph) * amp * Math.exp(-t * 15));
        }
    }

    static void noise(float[] buf, double start, double dur, double amp, double bright) {
        int i0 = (int) (start * RATE), n = (int) (dur * RATE);
        Random rnd = new Random(i0);
        double hp = 0, prev = 0;
        for (int i = 0; i < n; i++) {
            int idx = i0 + i;
            if (idx < 0 || idx >= buf.length) break;
            double t = (double) i / RATE;
            double white = rnd.nextDouble() * 2 - 1;
            hp = bright * (hp + white - prev);
            prev = white;
            buf[idx] += (float) (hp * amp * Math.exp(-t / (dur * 0.35)));
        }
    }

    public static void writeWav(File f, float[] data) {
        try {
            f.getParentFile().mkdirs();
            FileOutputStream o = new FileOutputStream(f);
            int bytes = data.length * 2;
            o.write(hdr(bytes));
            byte[] buf = new byte[bytes];
            for (int i = 0; i < data.length; i++) {
                int v = (int) (Math.max(-1, Math.min(1, data[i])) * 32000);
                buf[i * 2] = (byte) (v & 0xFF);
                buf[i * 2 + 1] = (byte) ((v >> 8) & 0xFF);
            }
            o.write(buf);
            o.close();
        } catch (Exception ignored) { }
    }

    static byte[] hdr(int dataBytes) {
        byte[] h = new byte[44];
        put(h, 0, "RIFF"); le32(h, 4, 36 + dataBytes); put(h, 8, "WAVE");
        put(h, 12, "fmt "); le32(h, 16, 16); le16(h, 20, 1); le16(h, 22, 1);
        le32(h, 24, RATE); le32(h, 28, RATE * 2); le16(h, 32, 2); le16(h, 34, 16);
        put(h, 36, "data"); le32(h, 40, dataBytes);
        return h;
    }

    static void put(byte[] b, int o, String s) { for (int i = 0; i < s.length(); i++) b[o + i] = (byte) s.charAt(i); }
    static void le32(byte[] b, int o, int v) { for (int i = 0; i < 4; i++) b[o + i] = (byte) ((v >> (8 * i)) & 0xFF); }
    static void le16(byte[] b, int o, int v) { for (int i = 0; i < 2; i++) b[o + i] = (byte) ((v >> (8 * i)) & 0xFF); }
}
