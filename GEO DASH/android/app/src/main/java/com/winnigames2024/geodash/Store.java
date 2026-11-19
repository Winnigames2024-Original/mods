package com.winnigames2024.geodash;

import android.content.Context;
import android.content.SharedPreferences;

import org.json.JSONObject;

import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.ByteArrayOutputStream;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;

/**
 * Папка assets создаётся сама рядом с данными приложения:
 *   assets/levels, assets/saved, assets/songs, assets/official, assets/cache
 * Песни (mp3/wav) складываются в assets/songs — из встроенных и с сайта.
 */
public class Store {

    static Context ctx;
    public static SharedPreferences prefs;

    public static void init(Context c) {
        ctx = c.getApplicationContext();
        prefs = ctx.getSharedPreferences("geodash", Context.MODE_PRIVATE);
        for (File f : new File[]{assets(), levels(), saved(), songs(), official(), cache()}) f.mkdirs();
    }

    public static File assets() { return new File(ctx.getExternalFilesDir(null), "assets"); }
    public static File levels() { return new File(assets(), "levels"); }
    public static File saved() { return new File(assets(), "saved"); }
    public static File songs() { return new File(assets(), "songs"); }
    public static File official() { return new File(assets(), "official"); }
    public static File cache() { return new File(assets(), "cache"); }

    // ------------------------------------------------------------ настройки

    public static String host() { return prefs.getString("host", "166.1.161.200"); }
    public static int port() { return prefs.getInt("port", 37534); }
    public static String site() { return prefs.getString("site", "https://geodash.winnigames2024.ru"); }
    public static String token() { return prefs.getString("token", ""); }
    public static String user() { return prefs.getString("user", ""); }
    public static boolean admin() { return user().equalsIgnoreCase("Winnigames2024") || prefs.getBoolean("admin", false); }
    public static boolean loggedIn() { return !token().isEmpty(); }

    public static void setAccount(String token, String user, boolean admin) {
        prefs.edit().putString("token", token).putString("user", user).putBoolean("admin", admin).apply();
    }

    public static void setServer(String host, int port, String site) {
        prefs.edit().putString("host", host).putInt("port", port).putString("site", site).apply();
    }

    public static float musicVolume() { return prefs.getFloat("music", 0.8f); }
    public static float sfxVolume() { return prefs.getFloat("sfx", 0.6f); }
    public static void setVolumes(float m, float s) { prefs.edit().putFloat("music", m).putFloat("sfx", s).apply(); }

    // ------------------------------------------------------------ файлы

    public static String readFile(File f) {
        if (f == null || !f.exists()) return null;
        try (InputStream in = new java.io.FileInputStream(f)) {
            ByteArrayOutputStream bo = new ByteArrayOutputStream();
            byte[] buf = new byte[8192];
            int n;
            while ((n = in.read(buf)) > 0) bo.write(buf, 0, n);
            return new String(bo.toByteArray(), StandardCharsets.UTF_8);
        } catch (IOException e) { return null; }
    }

    public static boolean writeFile(File f, String text) {
        try {
            f.getParentFile().mkdirs();
            try (FileOutputStream o = new FileOutputStream(f)) {
                o.write(text.getBytes(StandardCharsets.UTF_8));
            }
            return true;
        } catch (IOException e) { return false; }
    }

    public static boolean writeBytes(File f, byte[] data) {
        try {
            f.getParentFile().mkdirs();
            try (FileOutputStream o = new FileOutputStream(f)) { o.write(data); }
            return true;
        } catch (IOException e) { return false; }
    }

    // ------------------------------------------------------------ уровни

    public static File savedFile(int serverId) { return new File(saved(), "srv_" + serverId + ".gdl"); }
    public static boolean isSaved(int serverId) { return savedFile(serverId).exists(); }

    public static Level loadSaved(int serverId) {
        String s = readFile(savedFile(serverId));
        return s == null ? null : Level.parse(s);
    }

    public static void saveDownloaded(int serverId, String json) {
        writeFile(savedFile(serverId), json);
    }

    public static List<Level> savedLevels() {
        List<Level> res = new ArrayList<>();
        File[] fs = saved().listFiles();
        if (fs != null) for (File f : fs) {
            if (!f.getName().endsWith(".gdl")) continue;
            String s = readFile(f);
            if (s != null) res.add(Level.parse(s));
        }
        return res;
    }

    public static List<Level> myLevels() {
        List<Level> res = new ArrayList<>();
        File[] fs = levels().listFiles();
        if (fs != null) for (File f : fs) {
            if (!f.getName().endsWith(".gdl")) continue;
            String s = readFile(f);
            if (s != null) res.add(Level.parse(s));
        }
        return res;
    }

    /** Свои уровни: созданные с нуля (copied = false) или скопированные. */
    public static List<Level> myLevels(boolean copied) {
        List<Level> res = new ArrayList<>();
        for (Level l : myLevels()) if (l.settings.isCopy == copied) res.add(l);
        return res;
    }

    // ------------------------------------------------------------ прогресс

    static JSONObject progressJson() {
        String s = readFile(new File(assets(), "progress.json"));
        try { return s == null ? new JSONObject() : new JSONObject(s); }
        catch (Exception e) { return new JSONObject(); }
    }

    static void saveProgressJson(JSONObject o) {
        writeFile(new File(assets(), "progress.json"), o.toString());
    }

    public static String progressKey(Level lv) {
        return lv.settings.serverId > 0 ? "srv:" + lv.settings.serverId : "local:" + lv.settings.localId;
    }

    /** {best, attempts, coins, completed} */
    public static double[] progress(Level lv) {
        JSONObject root = progressJson();
        JSONObject o = root.optJSONObject(progressKey(lv));
        if (o == null) return new double[]{0, 0, 0, 0};
        return new double[]{o.optDouble("best", 0), o.optInt("attempts", 0),
                o.optInt("coins", 0), o.optBoolean("completed") ? 1 : 0};
    }

    public static void recordRun(Level lv, double percent, int attempts, int coins, boolean completed) {
        JSONObject root = progressJson();
        try {
            String k = progressKey(lv);
            JSONObject o = root.optJSONObject(k);
            if (o == null) o = new JSONObject();
            o.put("best", Math.max(o.optDouble("best", 0), percent));
            o.put("attempts", o.optInt("attempts", 0) + attempts);
            o.put("coins", Math.max(o.optInt("coins", 0), coins));
            if (completed) o.put("completed", true);
            root.put(k, o);
            saveProgressJson(root);
        } catch (Exception ignored) { }

        if (lv.settings.serverId > 0) {
            final int id = lv.settings.serverId;
            Api.async(() -> Api.complete(id, percent, attempts, coins));
        }
    }

    /** Сколько демонов пройдено локально. */
    public static int demonsBeaten() {
        int n = 0;
        List<Level> all = new ArrayList<>(savedLevels());
        all.addAll(Official.all());
        for (Level lv : all) {
            double[] p = progress(lv);
            if (p[3] > 0.5 && Gd.isDemon(lv.settings.difficulty)) n++;
        }
        return n;
    }

    public static int coinsTotal() {
        JSONObject root = progressJson();
        int n = 0;
        java.util.Iterator<String> it = root.keys();
        while (it.hasNext()) {
            JSONObject o = root.optJSONObject(it.next());
            if (o != null) n += o.optInt("coins", 0);
        }
        return n;
    }

    // ------------------------------------------------------------ кэш и очередь

    public static void putCache(String key, String json) {
        writeFile(new File(cache(), safe(key) + ".json"), json);
    }

    public static String getCache(String key) {
        return readFile(new File(cache(), safe(key) + ".json"));
    }

    public static String safe(String s) {
        StringBuilder b = new StringBuilder();
        for (char c : s.toCharArray()) b.append(Character.isLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
        return b.toString();
    }

    public static void enqueue(String path, String bodyJson, String tag) {
        try {
            File f = new File(assets(), "queue.json");
            org.json.JSONArray arr;
            String s = readFile(f);
            arr = s == null ? new org.json.JSONArray() : new org.json.JSONArray(s);
            org.json.JSONArray out = new org.json.JSONArray();
            for (int i = 0; i < arr.length(); i++) {
                JSONObject o = arr.optJSONObject(i);
                if (o != null && !tag.equals(o.optString("tag"))) out.put(o);
            }
            JSONObject n = new JSONObject();
            n.put("path", path); n.put("body", bodyJson); n.put("tag", tag);
            out.put(n);
            writeFile(f, out.toString());
        } catch (Exception ignored) { }
    }

    public static int flushQueue() {
        File f = new File(assets(), "queue.json");
        String s = readFile(f);
        if (s == null) return 0;
        int done = 0;
        try {
            org.json.JSONArray arr = new org.json.JSONArray(s);
            org.json.JSONArray left = new org.json.JSONArray();
            for (int i = 0; i < arr.length(); i++) {
                JSONObject o = arr.optJSONObject(i);
                if (o == null) continue;
                String r = Api.rawPost(o.optString("path"), o.optString("body"));
                if (r != null) done++; else left.put(o);
            }
            writeFile(f, left.toString());
        } catch (Exception ignored) { }
        return done;
    }
}
