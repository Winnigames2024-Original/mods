package com.winnigames2024.geodash;

import java.io.File;
import java.io.FileOutputStream;
import java.util.Random;

/** Встроенный саундтрек: генерируется кодом в assets/songs при первом запуске. */
public class Soundtrack {

    public static final int RATE = 44100;

    public static class Track {
        public int id;                 // -1, -2, -3 …
        public String name, artist = "Winnigames2024";
        public double bpm;
        public int bars, root;
        public int[] scale, progression, motif;
        public double density, drive, bright = 1.0;
        public boolean doubleTime;

        Track(int id, String name, double bpm, int bars, int root,
              int[] scale, int[] prog, int[] motif, double density,
              boolean dbl, double drive, double bright) {
            this.id = id; this.name = name; this.bpm = bpm; this.bars = bars; this.root = root;
            this.scale = scale; this.progression = prog; this.motif = motif;
            this.density = density; this.doubleTime = dbl; this.drive = drive; this.bright = bright;
        }
    }

    public static final Track[] TRACKS = {
            new Track(-1, "Neon Start", 128, 64, 45,
                    new int[]{0,2,4,5,7,9,11}, new int[]{0,5,3,4}, new int[]{0,2,4,2,5,4,2,1},
                    0.55, false, 0.22, 0.95),
            new Track(-2, "Static Rush", 150, 72, 43,
                    new int[]{0,2,3,5,7,8,10}, new int[]{0,3,5,4}, new int[]{0,4,3,4,6,4,3,2},
                    0.75, true, 0.40, 1.10),
            new Track(-3, "Hyperdrive", 174, 80, 41,
                    new int[]{0,1,3,5,7,8,10}, new int[]{0,6,3,5}, new int[]{0,3,5,3,7,5,3,2},
                    0.92, true, 0.55, 1.20),
            new Track(-4, "Violet Sky", 118, 56, 47,
                    new int[]{0,2,3,5,7,8,10}, new int[]{0,5,2,4}, new int[]{4,2,0,2,4,5,4,2},
                    0.45, false, 0.15, 0.80),
            new Track(-5, "Afterburner", 162, 76, 40,
                    new int[]{0,2,3,5,7,9,10}, new int[]{0,4,6,5}, new int[]{0,2,5,4,2,0,4,2},
                    0.85, true, 0.50, 1.05)
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

    // ------------------------------------------------------------ аранжировка

    /** Часть трека: 0 интро, 1 разгон, 2 дроп, 3 брейк, 4 дроп-2, 5 финал. */
    static int sectionOf(int bar, int bars) {
        double p = (double) bar / Math.max(1, bars);
        if (p < 0.12) return 0;
        if (p < 0.25) return 1;
        if (p < 0.50) return 2;
        if (p < 0.62) return 3;
        if (p < 0.88) return 4;
        return 5;
    }

    static double freq(int midi) { return 440.0 * Math.pow(2, (midi - 69) / 12.0); }

    public static float[] render(Track d) {
        double spb = 60.0 / d.bpm, stepLen = spb / 4.0;
        int steps = d.bars * 16;
        int total = (int) (steps * stepLen * RATE) + RATE * 2;

        float[] drums = new float[total];
        float[] music = new float[total];
        float[] lead = new float[total];
        float[] duck = new float[total];
        for (int i = 0; i < total; i++) duck[i] = 1f;

        int[] chordTones = {0, 2, 4, 6};

        for (int s = 0; s < steps; s++) {
            int bar = s / 16, six = s % 16;
            int sec = sectionOf(bar, d.bars);
            boolean full = sec == 2 || sec == 4;
            boolean quiet = sec == 0 || sec == 3 || sec == 5;
            boolean build = sec == 1;
            double t0 = s * stepLen;

            int deg = d.progression[bar % d.progression.length];
            int chordRoot = d.root + d.scale[deg % d.scale.length];
            double energy = full ? 1.0 : build ? 0.75 : 0.5;

            boolean kick = (quiet && sec != 3) ? six % 8 == 0 : six % 4 == 0;
            if (sec == 3) kick = six == 0;
            if (build && bar % 4 == 3 && six >= 8) kick = six % 2 == 0;
            if (kick) {
                addKick(drums, t0, full ? 0.95 : 0.7);
                duck(duck, t0, full ? 0.72 : 0.5, spb * 0.55);
            }

            if (!quiet && six % 8 == 4) addClap(drums, t0, full ? 0.55 : 0.4);

            if (!quiet || sec == 5) {
                if (six % 2 == 1) addNoise(drums, t0, 0.035, full ? 0.17 : 0.11, 0.92);
                if (full && six % 4 == 2) addNoise(drums, t0, 0.09, 0.1, 0.85);
            }

            if (six == 0 && bar > 0 && sectionOf(bar - 1, d.bars) != sec)
                addNoise(drums, t0, 1.1, 0.33, 0.6);

            if (!quiet || sec == 5) {
                boolean note = d.doubleTime ? six % 2 == 0 : (six % 2 == 0 && six % 8 != 6);
                if (note) {
                    boolean up = six % 8 == 4;
                    double len = stepLen * (d.doubleTime ? 1.05 : 1.5);
                    voice(music, t0, len, freq(chordRoot - 24 + (up ? 12 : 0)),
                            0.40 * energy, 1, 0.003, 0.07,
                            420 * d.bright, 160 * d.bright, d.drive);
                }
            } else if (six == 0) {
                voice(music, t0, spb * 2.0, freq(chordRoot - 24), 0.22, 0, 0.02, 0.5, 300, 160, 0);
            }

            if (six % 8 == 0) {
                double amp = full ? 0.115 : quiet ? 0.085 : 0.10;
                double dur = spb * (full ? 1.9 : 3.6);
                int n = full ? 4 : 3;
                for (int k = 0; k < n; k++) {
                    int tone = chordTones[k % chordTones.length];
                    int note = chordRoot + d.scale[(deg + tone) % d.scale.length] + (tone >= 4 ? 12 : 0);
                    voice(music, t0, dur, freq(note), amp, full ? 3 : 0,
                            full ? 0.01 : 0.12, 0.55, 2400 * d.bright, 900 * d.bright, 0);
                }
            }

            boolean playLead;
            switch (sec) {
                case 0: playLead = six % 8 == 0; break;
                case 1: playLead = six % 4 == 0; break;
                case 3: playLead = six % 8 == 0; break;
                case 5: playLead = six % 4 == 0; break;
                default: playLead = six % 2 == 0 || (six % 4 == 1 && d.density > 0.8);
            }
            if (playLead) {
                int mi = (s / (d.doubleTime ? 1 : 2)) % d.motif.length;
                int step2 = d.motif[mi] + deg;
                int note = chordRoot + d.scale[step2 % d.scale.length]
                        + 12 * (1 + (step2 / d.scale.length)) + (full ? 12 : 0);
                double amp = full ? 0.21 : quiet ? 0.12 : 0.16;
                voice(lead, t0, stepLen * (d.doubleTime ? 1.1 : 1.8), freq(note), amp,
                        full ? 3 : 2, 0.004, 0.09, 3200 * d.bright, 1400 * d.bright, d.drive * 0.7);
            }

            if (build && bar % 8 == 7) {
                double p = six / 16.0;
                addNoise(drums, t0, stepLen * 1.2, 0.05 + 0.18 * p, 0.5 + 0.45 * p);
            }
        }

        float[] out = new float[total];
        int delay = (int) (stepLen * 3 * RATE);
        float[] echo = new float[total];
        for (int i = 0; i < total; i++) {
            float v = lead[i];
            if (i >= delay) v += echo[i - delay] * 0.42f;
            echo[i] = v;
        }
        for (int i = 0; i < total; i++)
            out[i] = drums[i] + music[i] * duck[i] + lead[i] + echo[i] * 0.3f * duck[i];

        float peak = 0.0001f;
        for (int i = 0; i < total; i++) peak = Math.max(peak, Math.abs(out[i]));
        float norm = 0.82f / peak;
        float env = 0;
        for (int i = 0; i < total; i++) {
            float v = out[i] * norm;
            float a = Math.abs(v);
            env += (a - env) * (a > env ? 0.02f : 0.0012f);
            float gain = env > 0.55f ? 0.55f / env : 1f;
            v *= 0.6f + 0.4f * gain;
            out[i] = (float) Math.tanh(v * 1.35) * 0.95f;
        }

        int fade = RATE / 2;
        for (int i = 0; i < fade && i < total; i++) {
            out[i] *= i / (float) fade;
            out[total - 1 - i] *= i / (float) fade;
        }
        return out;
    }

    // ------------------------------------------------------------ голоса

    static void duck(float[] duck, double start, double depth, double len) {
        int i0 = (int) (start * RATE), n = (int) (len * RATE);
        for (int i = 0; i < n; i++) {
            int idx = i0 + i;
            if (idx < 0 || idx >= duck.length) break;
            double t = (double) i / n;
            float v = (float) (1 - depth * (1 - t) * (1 - t));
            if (v < duck[idx]) duck[idx] = v;
        }
    }

    /** kind: 0 синус, 1 пила, 2 квадрат, 3 супер-пила. */
    static void voice(float[] buf, double start, double dur, double freq, double amp,
                      int kind, double atk, double rel, double cutStart, double cutEnd, double drive) {
        int i0 = (int) (start * RATE), n = (int) (dur * RATE);
        if (i0 < 0 || n <= 0 || freq <= 1) return;

        int used = kind == 3 ? 5 : 1;
        double[] ph = new double[5], inc = new double[5];
        for (int v = 0; v < used; v++) {
            double det = kind == 3 ? 1 + (v - 2) * 0.0035 : 1;
            inc[v] = 2 * Math.PI * freq * det / RATE;
            ph[v] = v * 0.37;
        }

        double lp = 0, lp2 = 0;
        for (int i = 0; i < n; i++) {
            int idx = i0 + i;
            if (idx >= buf.length) break;
            double t = (double) i / RATE;

            double envv;
            if (t < atk) envv = t / Math.max(1e-5, atk);
            else {
                double k = (t - atk) / Math.max(1e-5, dur - atk);
                envv = Math.max(0, 1 - k) * Math.exp(-(t - atk) / Math.max(1e-4, rel * 3));
            }
            if (envv <= 0.0001) continue;

            double raw = 0;
            for (int v = 0; v < used; v++) {
                double p = (ph[v] / (2 * Math.PI)) % 1.0;
                if (kind == 0) raw += Math.sin(ph[v]);
                else if (kind == 2) raw += (p < 0.5 ? 1 : -1) * 0.62;
                else raw += 2 * p - 1;
                ph[v] += inc[v];
            }
            raw /= used;
            if (drive > 0) raw = Math.tanh(raw * (1 + drive * 5)) * 0.82;

            double cut = cutStart + (cutEnd - cutStart) * Math.min(1, t / Math.max(0.02, dur));
            double a = 1 - Math.exp(-2 * Math.PI * Math.max(60, Math.min(16000, cut)) / RATE);
            lp += (raw - lp) * a;
            lp2 += (lp - lp2) * a;

            buf[idx] += (float) (lp2 * amp * envv);
        }
    }

    static void addKick(float[] buf, double start, double amp) {
        int i0 = (int) (start * RATE), n = (int) (0.3 * RATE);
        double ph = 0;
        Random rnd = new Random(i0);
        for (int i = 0; i < n; i++) {
            int idx = i0 + i;
            if (idx < 0 || idx >= buf.length) break;
            double t = (double) i / RATE;
            double f = 165 * Math.exp(-t * 34) + 46;
            ph += 2 * Math.PI * f / RATE;
            double body = Math.sin(ph) * Math.exp(-t * 9.5);
            double click = (rnd.nextDouble() * 2 - 1) * Math.exp(-t * 300) * 0.35;
            buf[idx] += (float) (Math.tanh((body + click) * 1.6) * amp);
        }
    }

    static void addClap(float[] buf, double start, double amp) {
        for (int k = 0; k < 3; k++)
            addNoise(buf, start + k * 0.011, 0.055, amp * (k == 2 ? 1 : 0.55), 0.78);
        addNoise(buf, start + 0.03, 0.16, amp * 0.5, 0.6);
    }

    static void addNoise(float[] buf, double start, double dur, double amp, double bright) {
        int i0 = (int) (start * RATE), n = (int) (dur * RATE);
        Random rnd = new Random(i0 + 17);
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
