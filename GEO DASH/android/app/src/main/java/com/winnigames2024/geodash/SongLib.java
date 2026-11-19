package com.winnigames2024.geodash;

import org.json.JSONObject;

import java.io.File;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.List;

/** Что уже скачано в assets/songs: список файлов плюс запомненные названия треков. */
public final class SongLib {

    public static class Entry {
        public int id;
        public String name = "", artist = "";
        public long size;
        public boolean builtIn;
        public File file;

        public String sizeText() {
            return size >= 1024 * 1024
                    ? String.format("%.1f МБ", size / 1024.0 / 1024.0)
                    : String.format("%d КБ", size / 1024);
        }
    }

    static File metaFile() { return new File(Store.songs(), "songs.json"); }

    static JSONObject meta() {
        String s = Store.readFile(metaFile());
        if (s == null) return new JSONObject();
        try { return new JSONObject(s); } catch (Exception e) { return new JSONObject(); }
    }

    /** Запомнить название трека, чтобы оно было видно и офлайн. */
    public static void remember(int id, String name, String artist) {
        if (id == 0) return;
        try {
            JSONObject m = meta();
            JSONObject t = new JSONObject();
            t.put("name", name == null ? "" : name);
            t.put("artist", artist == null ? "" : artist);
            m.put(String.valueOf(id), t);
            Store.writeFile(metaFile(), m.toString());
        } catch (Exception ignored) { }
    }

    /** Все скачанные песни: встроенные (builtinN.wav) и по ID (N.mp3). */
    public static List<Entry> scan() {
        List<Entry> res = new ArrayList<>();
        File dir = Store.songs();
        File[] fs = dir.listFiles();
        if (fs == null) return res;
        JSONObject m = meta();

        for (File f : fs) {
            String n = f.getName().toLowerCase();
            if (!n.endsWith(".mp3") && !n.endsWith(".wav") && !n.endsWith(".ogg")) continue;
            String stem = f.getName().substring(0, f.getName().lastIndexOf('.'));
            Entry e = new Entry();
            e.file = f;
            e.size = f.length();
            try {
                if (stem.toLowerCase().startsWith("builtin")) {
                    int bid = Integer.parseInt(stem.substring(7));
                    e.builtIn = true;
                    e.id = -bid;
                    Soundtrack.Track t = Soundtrack.get(e.id);
                    e.name = t != null ? t.name : "Встроенный трек " + bid;
                    e.artist = t != null ? t.artist : "Winnigames2024";
                } else {
                    e.id = Integer.parseInt(stem);
                    JSONObject j = m.optJSONObject(stem);
                    e.name = j != null && !j.optString("name").isEmpty() ? j.optString("name") : "Трек #" + e.id;
                    e.artist = j != null ? j.optString("artist", "") : "";
                }
            } catch (Exception ex) { continue; }
            res.add(e);
        }
        Collections.sort(res, new Comparator<Entry>() {
            @Override public int compare(Entry a, Entry b) {
                if (a.builtIn != b.builtIn) return a.builtIn ? -1 : 1;
                return Integer.compare(Math.abs(a.id), Math.abs(b.id));
            }
        });
        return res;
    }

    private SongLib() {}
}
