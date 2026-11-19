package com.winnigames2024.geodash;

import android.media.AudioManager;
import android.media.AudioTrack;
import android.media.MediaPlayer;
import android.media.AudioFormat;

import java.io.File;
import java.util.HashMap;
import java.util.Map;

/** Музыка (assets/songs) и звуки. Встроенные треки — отрицательные ID. */
public class Music {

    static MediaPlayer player;
    public static int currentId = 0;
    public static String lastError = "";

    public static File fileOf(int id) {
        if (Soundtrack.isBuiltIn(id)) return Soundtrack.path(id);
        return new File(Store.songs(), id + ".mp3");
    }

    public static boolean isCached(int id) {
        File f = fileOf(id);
        return f.exists() && f.length() > 8192;
    }

    /** Скачивает (или генерирует) песню в assets/songs. */
    public static File ensureSong(int id) {
        if (id == 0) return null;
        if (Soundtrack.isBuiltIn(id)) return Soundtrack.ensure(id);
        File f = fileOf(id);
        if (f.exists() && f.length() > 8192) return f;
        byte[] data = Api.downloadSong(id);
        if (data == null) { lastError = "не скачалось"; return null; }
        Store.writeBytes(f, data);
        return f;
    }

    public static void play(final int id, final double startSec, final float volume, final boolean loop) {
        Api.async(() -> {
            File f = ensureSong(id);
            if (f == null) return;
            playFile(f, startSec, volume, loop, id);
        });
    }

    /** Проиграть уже скачанный файл, не блокируя интерфейс. */
    public static void playFileAsync(final File f, final int id) {
        Api.async(() -> playFile(f, 0, 1f, true, id));
    }

    public static synchronized void playFile(File f, double startSec, float volume, boolean loop, int id) {
        stop();
        try {
            MediaPlayer mp = new MediaPlayer();
            mp.setDataSource(f.getAbsolutePath());
            mp.setAudioStreamType(AudioManager.STREAM_MUSIC);
            mp.setLooping(loop);
            mp.prepare();
            float v = Math.max(0f, Math.min(1f, volume * Store.musicVolume()));
            mp.setVolume(v, v);
            if (startSec > 0) mp.seekTo((int) (startSec * 1000));
            mp.start();
            player = mp;
            currentId = id;
        } catch (Exception e) { lastError = String.valueOf(e.getMessage()); }
    }

    public static synchronized void stop() {
        try { if (player != null) { player.stop(); player.release(); } } catch (Exception ignored) { }
        player = null;
        currentId = 0;
    }

    public static synchronized void pause() { try { if (player != null) player.pause(); } catch (Exception ignored) { } }
    public static synchronized void resume() { try { if (player != null) player.start(); } catch (Exception ignored) { } }

    public static synchronized void seek(double sec) {
        try { if (player != null) player.seekTo((int) (sec * 1000)); } catch (Exception ignored) { }
    }

    // ------------------------------------------------------------ звуки

    static final Map<String, short[]> SFX = new HashMap<>();
    static final int SR = 22050;

    static short[] bank(String name) {
        short[] s = SFX.get(name);
        if (s != null) return s;
        switch (name) {
            case "jump": s = tone(new double[]{520, 720}, new double[]{0.05, 0.05}, 0.25, 1); break;
            case "orb": s = tone(new double[]{660, 990, 1320}, new double[]{0.04, 0.06, 0.05}, 0.22, 0); break;
            case "portal": s = tone(new double[]{300, 600, 900}, new double[]{0.06, 0.06, 0.08}, 0.2, 2); break;
            case "coin": s = tone(new double[]{1046, 1318, 1568}, new double[]{0.05, 0.05, 0.1}, 0.2, 0); break;
            case "win": s = tone(new double[]{523, 659, 784, 1046}, new double[]{0.12, 0.12, 0.12, 0.26}, 0.22, 0); break;
            case "death": s = noiseBurst(0.3, 0.32); break;
            default: s = tone(new double[]{900}, new double[]{0.02}, 0.12, 1);
        }
        SFX.put(name, s);
        return s;
    }

    public static void sfx(final String name) {
        if (Store.sfxVolume() <= 0.01f) return;
        Api.async(() -> {
            try {
                short[] data = bank(name);
                AudioTrack t = new AudioTrack(AudioManager.STREAM_MUSIC, SR,
                        AudioFormat.CHANNEL_OUT_MONO, AudioFormat.ENCODING_PCM_16BIT,
                        Math.max(data.length * 2, 2048), AudioTrack.MODE_STATIC);
                t.write(data, 0, data.length);
                float v = Store.sfxVolume();
                t.setStereoVolume(v, v);
                t.play();
                Thread.sleep((long) (data.length * 1000L / SR) + 120);
                t.release();
            } catch (Exception ignored) { }
        });
    }

    static short[] tone(double[] freqs, double[] durs, double amp, int wave) {
        int total = 0;
        for (double d : durs) total += (int) (d * SR);
        short[] out = new short[total];
        int idx = 0;
        for (int p = 0; p < freqs.length; p++) {
            int n = (int) (durs[p] * SR);
            for (int i = 0; i < n; i++) {
                double t = (double) i / SR;
                double ph = 2 * Math.PI * freqs[p] * t;
                double v = wave == 0 ? Math.sin(ph) : wave == 1 ? Math.signum(Math.sin(ph))
                        : 2 * ((freqs[p] * t) % 1.0) - 1;
                double env = Math.min(1, i / 60.0) * (1 - (double) i / n);
                if (idx + i < out.length) out[idx + i] = (short) (v * amp * env * 26000);
            }
            idx += n;
        }
        return out;
    }

    static short[] noiseBurst(double dur, double amp) {
        int n = (int) (dur * SR);
        short[] out = new short[n];
        java.util.Random r = new java.util.Random(7);
        double low = 0;
        for (int i = 0; i < n; i++) {
            double white = r.nextDouble() * 2 - 1;
            low = low * 0.86 + white * 0.14;
            double env = 1 - (double) i / n;
            out[i] = (short) (low * amp * env * env * 26000);
        }
        return out;
    }
}
