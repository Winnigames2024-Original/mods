package com.winnigames2024.geodash;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/** Сеть: тот же API, что у игры на Windows. Без интернета отдаём кэш. */
public class Api {

    public static boolean online = false;
    public static String lastError = "";
    static final ExecutorService POOL = Executors.newFixedThreadPool(3);

    public static void async(Runnable r) { POOL.execute(r); }

    static String base() { return "http://" + Store.host() + ":" + Store.port(); }

    // ------------------------------------------------------------ низкий уровень

    static HttpURLConnection open(String path, String method) throws Exception {
        URL url = new URL(path.startsWith("http") ? path : base() + path);
        HttpURLConnection c = (HttpURLConnection) url.openConnection();
        c.setRequestMethod(method);
        c.setConnectTimeout(8000);
        c.setReadTimeout(20000);
        c.setRequestProperty("Accept", "application/json");
        c.setRequestProperty("X-Client", "geodash-android/1.0");
        String t = Store.token();
        if (!t.isEmpty()) c.setRequestProperty("Authorization", "Bearer " + t);
        return c;
    }

    /** Сервер закрыт на обслуживание: игра показывает это и выходит. */
    public static String maintenance = "";

    static String read(HttpURLConnection c) throws Exception {
        int code = c.getResponseCode();
        InputStream in = code >= 400 ? c.getErrorStream() : c.getInputStream();
        if (in == null) return null;
        ByteArrayOutputStream bo = new ByteArrayOutputStream();
        byte[] buf = new byte[8192];
        int n;
        while ((n = in.read(buf)) > 0) bo.write(buf, 0, n);
        in.close();
        String body = new String(bo.toByteArray(), StandardCharsets.UTF_8);
        if (code == 503) {
            try {
                org.json.JSONObject j = new org.json.JSONObject(body);
                if (j.optBoolean("maintenance"))
                    maintenance = j.optString("error", "Сервер обновляется");
            } catch (Exception ignored) { }
        }
        return body;
    }

    public static String rawGet(String path) {
        try {
            HttpURLConnection c = open(path, "GET");
            String s = read(c);
            online = c.getResponseCode() < 500;
            return c.getResponseCode() < 400 ? s : null;
        } catch (Exception e) { lastError = e.getMessage(); online = false; return null; }
    }

    public static String rawPost(String path, String body) {
        try {
            HttpURLConnection c = open(path, "POST");
            c.setDoOutput(true);
            c.setRequestProperty("Content-Type", "application/json; charset=utf-8");
            try (OutputStream o = c.getOutputStream()) { o.write(body.getBytes(StandardCharsets.UTF_8)); }
            String s = read(c);
            online = true;
            return c.getResponseCode() < 400 ? s : null;
        } catch (Exception e) { lastError = e.getMessage(); online = false; return null; }
    }

    /** POST, который отдаёт ответ и при ошибке — чтобы показать её текст игроку. */
    public static String rawPostAny(String path, String body) {
        try {
            HttpURLConnection c = open(path, "POST");
            c.setDoOutput(true);
            c.setRequestProperty("Content-Type", "application/json; charset=utf-8");
            try (OutputStream o = c.getOutputStream()) { o.write(body.getBytes(StandardCharsets.UTF_8)); }
            String s = read(c);
            online = true;
            return s;
        } catch (Exception e) { lastError = e.getMessage(); online = false; return null; }
    }

    /** GET с кэшем: офлайн вернёт последний скачанный ответ. */
    public static String cachedGet(String path, String cacheKey) {
        String s = rawGet(path);
        if (s != null) { Store.putCache(cacheKey, s); return s; }
        return Store.getCache(cacheKey);
    }

    static String enc(String s) {
        try { return URLEncoder.encode(s == null ? "" : s, "UTF-8"); } catch (Exception e) { return ""; }
    }

    // ------------------------------------------------------------ методы

    public static boolean ping() {
        String s = rawGet("/api/ping");
        online = s != null;
        return online;
    }

    public static JSONObject login(String name, String pass) {
        try {
            JSONObject b = new JSONObject().put("name", name).put("pass", pass);
            String s = rawPost("/api/login", b.toString());
            return s == null ? null : new JSONObject(s);
        } catch (Exception e) { return null; }
    }

    public static JSONObject register(String name, String pass) {
        try {
            JSONObject b = new JSONObject().put("name", name).put("pass", pass);
            String s = rawPost("/api/register", b.toString());
            return s == null ? null : new JSONObject(s);
        } catch (Exception e) { return null; }
    }

    public static JSONObject me() {
        String s = cachedGet("/api/me", "me");
        try { return s == null ? null : new JSONObject(s); } catch (Exception e) { return null; }
    }

    /** Открыта ли игра на этом аккаунте и какие ключи за ним. */
    public static JSONObject licenceMine() {
        String s = rawGet("/api/licence/mine");
        try { return s == null ? null : new JSONObject(s); } catch (Exception e) { return null; }
    }

    /** Активировать ключ на этом аккаунте — один раз и навсегда. */
    public static JSONObject licenceActivate(String key, String device, String platform) {
        try {
            JSONObject b = new JSONObject().put("key", key).put("device", device).put("platform", platform);
            String s = rawPostAny("/api/licence/activate", b.toString());
            if (s == null) return null;
            return new JSONObject(s);
        } catch (Exception e) { return null; }
    }

    public static JSONArray levels(String q, String sort, int diff) {
        String path = "/api/levels?q=" + enc(q) + "&sort=" + enc(sort) + "&diff=" + diff;
        String s = cachedGet(path, "levels_" + sort + "_" + diff + "_" + q);
        try { return s == null ? new JSONArray() : new JSONArray(s); } catch (Exception e) { return new JSONArray(); }
    }

    public static JSONObject level(int id) {
        String s = cachedGet("/api/levels/" + id, "level_" + id);
        try { return s == null ? null : new JSONObject(s); } catch (Exception e) { return null; }
    }

    public static JSONObject daily() { return obj(cachedGet("/api/daily", "daily")); }
    public static JSONObject weekly() { return obj(cachedGet("/api/weekly", "weekly")); }
    public static JSONArray offlinePacks() { return arr(cachedGet("/api/offline", "offlinepacks")); }
    public static JSONArray friends() { return arr(cachedGet("/api/friends", "friends")); }
    public static JSONArray friendRequests() { return arr(cachedGet("/api/friends/requests", "friendreq")); }
    public static JSONArray top() { return arr(cachedGet("/api/top", "top")); }

    public static boolean friendAction(String action, String name) {
        try {
            String body = new JSONObject().put("name", name).toString();
            String path = "/api/friends/" + action;
            String r = rawPost(path, body);
            if (r == null) { Store.enqueue(path, body, action + ":" + name); return false; }
            return true;
        } catch (Exception e) { return false; }
    }

    public static void complete(int levelId, double percent, int attempts, int coins) {
        try {
            String body = new JSONObject()
                    .put("percent", percent).put("attempts", attempts).put("coins", coins).toString();
            String path = "/api/levels/" + levelId + "/complete";
            if (rawPost(path, body) == null) Store.enqueue(path, body, "complete:" + levelId);
        } catch (Exception ignored) { }
    }

    /** Залить уровень на сервер (из редактора на телефоне). Возвращает id или 0. */
    public static int uploadLevel(Level lv) {
        try {
            JSONObject b = new JSONObject();
            b.put("id", lv.settings.serverId);
            b.put("password", lv.settings.password);
            b.put("name", lv.settings.name);
            b.put("desc", lv.settings.desc);
            b.put("songId", lv.settings.songId);
            b.put("songName", lv.settings.songName);
            b.put("difficulty", lv.settings.difficulty);
            b.put("length", lv.rightEdge());
            b.put("localId", lv.settings.localId);
            b.put("data", lv.toJson());
            String r = rawPost("/api/levels", b.toString());
            if (r == null) {
                Store.enqueue("/api/levels", b.toString(), "upload:" + lv.settings.localId);
                return 0;
            }
            return new JSONObject(r).optInt("id", 0);
        } catch (Exception e) { return 0; }
    }

    public static JSONArray searchUsers(String q) {
        return arr(cachedGet("/api/users?q=" + enc(q), "usersearch"));
    }

    public static JSONArray levelsOf(String author) {
        return arr(cachedGet("/api/levels?author=" + enc(author) + "&sort=recent", "levels_of_" + author));
    }

    public static JSONObject user(String name) {
        return obj(cachedGet("/api/users/" + enc(name), "user_" + name));
    }

    public static void like(int levelId) {
        try {
            String body = new JSONObject().put("like", true).toString();
            String path = "/api/levels/" + levelId + "/like";
            if (rawPost(path, body) == null) Store.enqueue(path, body, "like:" + levelId);
        } catch (Exception ignored) { }
    }

    /** Список картинок toolbox. */
    public static JSONArray images() { return arr(cachedGet("/api/images", "images")); }

    /** Список треков, залитых на сервер. */
    public static JSONArray songs() { return arr(cachedGet("/api/music", "songs")); }

    /** Комментарии к уровню. */
    public static JSONArray comments(int levelId) {
        return arr(rawGet("/api/levels/" + levelId + "/comments"));
    }

    public static boolean addComment(int levelId, String text) {
        try {
            JSONObject b = new JSONObject().put("text", text);
            return rawPost("/api/levels/" + levelId + "/comments", b.toString()) != null;
        } catch (Exception e) { return false; }
    }

    // ---------------------------------------------------------- мультиплеер

    /** Зайти в комнату уровня: вернётся её id или null. */
    public static String mpJoin(int levelId, int maxPlayers) {
        try {
            JSONObject b = new JSONObject().put("levelId", levelId).put("maxPlayers", maxPlayers);
            JSONObject r = obj(rawPost("/api/mp/join", b.toString()));
            return r != null && r.optBoolean("ok") ? r.optString("room") : null;
        } catch (Exception e) { return null; }
    }

    /** Отправить свои координаты, получить остальных. */
    public static JSONObject mpState(String room, double x, double y, int mode, int dir,
                                     boolean mini, boolean dead, double percent, int color) {
        try {
            JSONObject b = new JSONObject()
                    .put("room", room).put("x", x).put("y", y).put("mode", mode).put("dir", dir)
                    .put("mini", mini).put("dead", dead).put("percent", percent).put("color", color);
            return obj(rawPost("/api/mp/state", b.toString()));
        } catch (Exception e) { return null; }
    }

    public static void mpLeave(String room) {
        try { rawPost("/api/mp/leave", new JSONObject().put("room", room).toString()); }
        catch (Exception ignored) { }
    }

    /** Название и исполнитель трека: {name, artist} или null. */
    public static String[] songInfo(int id) {
        try {
            JSONObject o = obj(cachedGet("/api/music/" + id, "song_" + id));
            if (o == null) return null;
            return new String[]{o.optString("name", ""), o.optString("artist", "")};
        } catch (Exception e) { return null; }
    }

    /** Качает песню по ID: сначала через API, потом прямо с сайта /songs/<id>.mp3 */
    public static byte[] downloadSong(int id) {
        byte[] b = fetchBytes(base() + "/api/music/" + id + "/file");
        if (b != null && b.length > 8192) return b;
        b = fetchBytes(Store.site().replaceAll("/+$", "") + "/songs/" + id + ".mp3");
        return (b != null && b.length > 8192) ? b : null;
    }

    public static byte[] fetchBytes(String url) {
        return fetchBytes(url, 0);
    }

    static byte[] fetchBytes(String url, int hop) {
        try {
            HttpURLConnection c = (HttpURLConnection) new URL(url).openConnection();
            c.setConnectTimeout(8000);
            c.setReadTimeout(60000);
            int code = c.getResponseCode();
            // http -> https сам java не переходит, идём руками
            if (code == 301 || code == 302 || code == 303 || code == 307 || code == 308) {
                String to = c.getHeaderField("Location");
                c.disconnect();
                if (to == null || hop >= 4) return null;
                return fetchBytes(new URL(new URL(url), to).toString(), hop + 1);
            }
            if (code >= 400) return null;
            InputStream in = c.getInputStream();
            ByteArrayOutputStream bo = new ByteArrayOutputStream();
            byte[] buf = new byte[16384];
            int n;
            while ((n = in.read(buf)) > 0) bo.write(buf, 0, n);
            in.close();
            online = true;
            return bo.toByteArray();
        } catch (Exception e) { lastError = e.getMessage(); return null; }
    }

    static JSONObject obj(String s) {
        try { return s == null ? null : new JSONObject(s); } catch (Exception e) { return null; }
    }

    static JSONArray arr(String s) {
        try { return s == null ? new JSONArray() : new JSONArray(s); } catch (Exception e) { return new JSONArray(); }
    }

    /** Полная синхронизация: очередь → daily/weekly → офлайн-паки → музыка. */
    public static void sync(Runnable onStep) {
        if (!ping()) return;
        Store.flushQueue();
        me();
        JSONObject d = daily(), w = weekly();
        levels("", "recent", -1);
        levels("", "top", -1);
        top();
        friends();
        friendRequests();

        downloadLevelOffline(d);
        downloadLevelOffline(w);

        JSONArray packs = offlinePacks();
        for (int i = 0; i < packs.length(); i++) {
            JSONObject l = packs.optJSONObject(i);
            if (l == null) continue;
            int id = l.optInt("id");
            if (!Store.isSaved(id)) downloadLevel(id);
            else Music.ensureSong(l.optInt("songId"));
            if (onStep != null) onStep.run();
        }

        for (Level lv : Store.savedLevels()) Music.ensureSong(lv.settings.songId);
    }

    static void downloadLevelOffline(JSONObject daily) {
        if (daily == null) return;
        JSONObject l = daily.optJSONObject("level");
        if (l != null) downloadLevel(l.optInt("id"));
    }

    /** Скачать уровень вместе с музыкой в assets/saved. */
    public static Level downloadLevel(int id) {
        JSONObject o = level(id);
        if (o == null) return Store.isSaved(id) ? Store.loadSaved(id) : null;
        String data = o.optString("data", "");
        if (data.length() < 20) return Store.isSaved(id) ? Store.loadSaved(id) : null;
        Store.saveDownloaded(id, data);
        Level lv = Level.parse(data);
        lv.settings.serverId = id;
        if (lv.settings.songId != 0) Music.ensureSong(lv.settings.songId);
        return lv;
    }
}
