package com.winnigames2024.forge_narevisuals.client;

import com.google.gson.JsonArray;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import com.winnigames2024.forge_narevisuals.Config;
import com.winnigames2024.forge_narevisuals.Forge_narevisuals;

import net.minecraft.SharedConstants;
import net.minecraft.client.Minecraft;
import net.minecraft.server.MinecraftServer;
import net.minecraft.world.level.GameType;
import net.minecraftforge.api.distmarker.Dist;
import net.minecraftforge.event.TickEvent;
import net.minecraftforge.eventbus.api.SubscribeEvent;
import net.minecraftforge.fml.ModList;
import net.minecraftforge.fml.common.Mod;

import java.io.File;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.InetSocketAddress;
import java.net.Socket;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.security.MessageDigest;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/**
 * «Вместе» — открыть свой одиночный мир так, чтобы к нему пришли друзья.
 *
 * Делает три вещи:
 *   1. открывает мир в multiplayer — обычным способом самой игры;
 *   2. объявляет его на сборном пункте NareNet: ник, порт, версия, моды;
 *   3. если снаружи до порта не достаётся, поднимает туннель через тот же
 *      сборный пункт, и гости идут через него.
 *
 * Виртуальной сети, как в Radmin VPN, тут нет: для неё нужен драйвер в
 * системе. Туннель решает ту же задачу другим путём.
 *
 * Всё, что может ждать сеть, живёт в отдельных потоках: игру тормозить
 * нельзя.
 */
@Mod.EventBusSubscriber(modid = Forge_narevisuals.MODID, value = Dist.CLIENT)
public final class NareTogether {

    private static final int BEAT = 20_000;        // как часто объявляемся
    private static final int WAIT = 8_000;

    private static volatile boolean open;
    private static volatile boolean stop;
    private static volatile String token = "";
    private static volatile int gamePort;
    private static volatile int shownPort;
    private static volatile String mode = "direct";
    private static volatile Boolean reachable;
    private static volatile String note = "мир закрыт";
    private static volatile String uploadNote = "";

    private static Thread beat;
    private static Thread tunnel;

    /**
     * Номер поколения туннеля. Поток, у которого номер устарел, сам
     * выходит: interrupt() не прерывает чтение из сокета, поэтому
     * старые потоки иначе живут вечно и стучатся с мёртвым токеном.
     */
    private static volatile int age;

    /** Управляющий сокет текущего туннеля — закрыть его можно только так. */
    private static volatile Socket wire;

    /** sha1 по пути файла: считать его каждый раз незачем. */
    private static final Map<String, String> hashes = new HashMap<>();

    private NareTogether() { }

    // ------------------------------------------------------------- состояние

    public static boolean running() { return open; }

    public static String state() { return note; }

    public static String uploads() { return uploadNote; }

    /** Адрес, который видят гости. */
    public static String address() {
        if (!open) return "";
        String host = host();
        return "tunnel".equals(mode) ? host + ":" + shownPort : "ваш адрес:" + shownPort;
    }

    private static String where() {
        String value = Config.get().togetherHost;
        return value == null || value.trim().isEmpty() ? "151.245.217.104:2445" : value.trim();
    }

    /** Все сборные пункты, которые знает мод. */
    public static List<String> hosts() {
        var list = new ArrayList<String>();

        String all = Config.get().togetherHosts;
        if (all != null)
            for (String part : all.split("[;,]")) {
                String one = part.trim();
                if (!one.isEmpty() && !list.contains(one)) list.add(one);
            }

        String active = where();
        if (!list.contains(active)) list.add(0, active);

        return list;
    }

    /**
     * Переключиться на следующий сборный пункт. Токен привязан к серверу,
     * поэтому его и туннель сбрасываем — объявимся заново.
     */
    public static void nextHost() {
        var list = hosts();
        if (list.size() < 2) return;

        int at = list.indexOf(where());
        String next = list.get((Math.max(at, 0) + 1) % list.size());

        Config.get().togetherHost = next;
        Config.save();

        token = "";
        mode = "direct";
        reachable = null;

        age++;
        closeQuietly(wire);
        wire = null;

        if (tunnel != null) { tunnel.interrupt(); tunnel = null; }

        note = open ? "сборный пункт сменился, объявляюсь заново…" : "мир закрыт";
        Forge_narevisuals.LOGGER.info("Сборный пункт: {}", next);
    }

    private static String host() {
        String value = where();
        int colon = value.lastIndexOf(':');
        return colon > 0 ? value.substring(0, colon) : value;
    }

    // ---------------------------------------------------------------- запуск

    /** Открыть или закрыть мир. Зовётся из настроек мода. */
    public static void toggle() {
        if (open) { close("сами закрыли"); return; }

        Minecraft mc = Minecraft.getInstance();
        MinecraftServer server = mc.getSingleplayerServer();

        if (server == null) {
            note = "сначала войдите в свой одиночный мир";
            NareErrors.report("together-no-world", "Открыть мир не получилось",
                    "Открывать можно только свой одиночный мир. Зайдите в него и попробуйте снова.");
            return;
        }

        int want = Config.get().togetherPort;
        int port = want > 0 ? want : freePort();

        boolean ok;

        // Мир уже открыт в сеть — второй раз открывать нельзя: игра возьмёт
        // новый порт и выбросит всех, кто успел зайти.
        if (published(server)) {
            port = server.getPort();
            ok = port > 0;
            Forge_narevisuals.LOGGER.info("Мир уже был открыт на порту {} — оставляю как есть", port);
        } else {
            try {
                ok = server.publishServer(GameType.SURVIVAL, false, port);
            } catch (Throwable t) {
                ok = false;
                Forge_narevisuals.LOGGER.warn("publishServer не сработал: {}", t.toString());
            }
        }

        if (!ok) {
            note = "игра не смогла открыть порт " + port;
            NareErrors.report("together-publish", "Порт не открылся",
                    "Minecraft не смог открыть мир на порту " + port + ".\n"
                    + "Скорее всего порт занят другой программой. Поставьте в настройках 0 — "
                    + "тогда мод выберет свободный порт сам.");
            return;
        }

        gamePort = server.getPort() > 0 ? server.getPort() : port;
        shownPort = gamePort;
        mode = "direct";
        reachable = null;
        token = "";
        stop = false;
        open = true;
        note = "мир открыт на порту " + gamePort + ", объявляю…";

        Forge_narevisuals.LOGGER.info("Мир открыт для друзей на порту {}", gamePort);

        beat = new Thread(NareTogether::beatLoop, "nare-together");
        beat.setDaemon(true);
        beat.start();
    }

    public static void close(String why) {
        if (!open) return;

        open = false;
        stop = true;
        note = "мир закрыт" + (why == null ? "" : " — " + why);

        String key = token;
        token = "";

        age++;                                  // всем прежним туннелям — выход

        closeQuietly(wire);
        wire = null;

        if (tunnel != null) { tunnel.interrupt(); tunnel = null; }
        if (beat != null) { beat.interrupt(); beat = null; }

        if (!key.isEmpty()) {
            Thread bye = new Thread(() -> {
                JsonObject body = new JsonObject();
                body.addProperty("token", key);
                try { post("/bye", body.toString()); } catch (Exception ignored) { }
            }, "nare-together-bye");
            bye.setDaemon(true);
            bye.start();
        }

        Forge_narevisuals.LOGGER.info("Мир больше не объявляется: {}", why);
    }

    /** Мир закрылся сам — например, вышли в меню. Проверяем раз в секунду. */
    @SubscribeEvent
    public static void onTick(TickEvent.ClientTickEvent event) {
        if (event.phase != TickEvent.Phase.END) return;
        if (!open) return;

        if (++ticks < 20) return;
        ticks = 0;

        if (Minecraft.getInstance().getSingleplayerServer() == null) close("вышли из мира");
    }

    private static int ticks;

    /** Открыт ли мир в сеть. Игра умеет это сказать, но метод есть не везде. */
    private static boolean published(MinecraftServer server) {
        try {
            return server.isPublished();
        } catch (Throwable t) {
            return server.getPort() > 0;
        }
    }

    private static int freePort() {
        try (java.net.ServerSocket probe = new java.net.ServerSocket(0)) {
            return probe.getLocalPort();
        } catch (IOException e) {
            return 25565;
        }
    }

    // ------------------------------------------------------------ объявление

    private static void beatLoop() {
        while (!stop && open) {
            try {
                announce();
            } catch (Exception e) {
                note = "сборный пункт не отвечает: " + e.getMessage();
                Forge_narevisuals.LOGGER.warn("NareNet не ответил: {}", e.toString());
            }

            try { Thread.sleep(BEAT); } catch (InterruptedException e) { return; }
        }
    }

    private static void announce() throws Exception {
        Minecraft mc = Minecraft.getInstance();
        MinecraftServer server = mc.getSingleplayerServer();

        if (server == null) { close("мира больше нет"); return; }

        JsonObject body = new JsonObject();
        if (!token.isEmpty()) body.addProperty("token", token);

        body.addProperty("nick", mc.getUser().getName());
        body.addProperty("port", gamePort);
        body.addProperty("version", SharedConstants.getCurrentVersion().getName());
        body.addProperty("loader", "forge");
        body.addProperty("world", level(server));
        body.addProperty("players", server.getPlayerCount());
        body.addProperty("max", Math.max(server.getMaxPlayers(), 8));
        body.addProperty("mode", "tunnel".equals(mode) ? "tunnel" : "direct");
        body.add("mods", mods());

        String answer;
        try {
            answer = post("/announce", body.toString());
        } catch (Exception e) {
            String why = e.getMessage() == null ? "" : e.getMessage();
            if (why.contains("together-banned")) {
                close("вы забанены — хостить нельзя");
                NareErrors.report("together-banned", "Вы забанены",
                        "Администратор забанил ваш аккаунт, поэтому открыть мир для друзей нельзя. "
                        + "Подать апелляцию можно в лаунчере.");
                return;
            }
            if (why.contains("together-blocked")) {
                close("администратор запретил «Вместе»");
                NareErrors.report("together-blocked", "«Вместе» запрещено",
                        "Администратор отключил вам функцию «Вместе».");
                return;
            }
            throw e;
        }

        JsonObject json = JsonParser.parseString(answer).getAsJsonObject();

        if (json.has("token")) token = json.get("token").getAsString();
        if (json.has("mode")) mode = json.get("mode").getAsString();
        if (json.has("port")) shownPort = json.get("port").getAsInt();

        reachable = json.has("reachable") && !json.get("reachable").isJsonNull()
                ? json.get("reachable").getAsBoolean() : null;

        // Туннель поднимаем сразу, не дожидаясь проверки порта: иначе комната
        // первые секунды висит «порт закрыт» и зайти в неё нельзя. Если потом
        // окажется, что снаружи и так достаёт, туннель закроем.
        if (!Boolean.TRUE.equals(reachable) && !"tunnel".equals(mode)) startTunnel();

        if (Boolean.TRUE.equals(reachable) && tunnel != null) {
            tunnel.interrupt();
            tunnel = null;
            mode = "direct";
            shownPort = gamePort;
            Forge_narevisuals.LOGGER.info("Порт открыт снаружи — туннель больше не нужен");
        }

        note = "tunnel".equals(mode)
                ? "гости идут через наш сервер: " + host() + ":" + shownPort
                : (Boolean.TRUE.equals(reachable)
                    ? "виден снаружи напрямую, порт " + shownPort
                    : "объявлен, проверяю порт " + shownPort);
    }

    private static String level(MinecraftServer server) {
        try { return server.getWorldData().getLevelName(); }
        catch (Throwable t) { return "мир"; }
    }

    /** Список своих модов. Встроенные в игру и загрузчик не считаются. */
    private static JsonArray mods() {
        var array = new JsonArray();

        for (var info : ModList.get().getMods()) {
            String id = info.getModId();
            if (id.equals("minecraft") || id.equals("forge") || id.equals("fml")
                    || id.equals("javafml") || id.equals("lowcodefml")) continue;

            File file = jarOf(id);
            if (file == null) continue;

            JsonObject one = new JsonObject();
            one.addProperty("id", id);
            one.addProperty("name", info.getDisplayName());
            one.addProperty("version", info.getVersion().toString());
            one.addProperty("file", file.getName());
            one.addProperty("size", file.length());

            String sha1 = sha1Of(file);
            if (sha1 != null) one.addProperty("sha1", sha1);

            array.add(one);
        }

        return array;
    }

    private static File jarOf(String modId) {
        try {
            var fileInfo = ModList.get().getModFileById(modId);
            if (fileInfo == null) return null;

            Path path = fileInfo.getFile().getFilePath();
            if (path == null) return null;

            File file = path.toFile();
            if (!file.isFile()) return null;                       // в разработке это папка
            if (!file.getName().toLowerCase().endsWith(".jar")) return null;

            return file;
        } catch (Throwable t) {
            return null;
        }
    }

    private static String sha1Of(File file) {
        String key = file.getAbsolutePath() + "|" + file.length() + "|" + file.lastModified();

        synchronized (hashes) {
            String had = hashes.get(key);
            if (had != null) return had;
        }

        try {
            MessageDigest digest = MessageDigest.getInstance("SHA-1");
            try (InputStream in = Files.newInputStream(file.toPath())) {
                byte[] buffer = new byte[64 * 1024];
                int read;
                while ((read = in.read(buffer)) > 0) digest.update(buffer, 0, read);
            }

            StringBuilder text = new StringBuilder();
            for (byte b : digest.digest()) text.append(String.format("%02x", b));

            String result = text.toString();
            synchronized (hashes) { hashes.put(key, result); }
            return result;
        } catch (Exception e) {
            Forge_narevisuals.LOGGER.warn("sha1 для {} не посчитался: {}", file.getName(), e.toString());
            return null;
        }
    }

    // ------------------------------------------------------- выкладка модов

    /**
     * Выкладывает свои jar-ы на сборный пункт, чтобы гости скачали ровно
     * такие же. Уже лежащие там файлы пропускаем.
     */
    public static void share() {
        Thread worker = new Thread(() -> {
            int sent = 0, had = 0, failed = 0;

            for (var info : ModList.get().getMods()) {
                String id = info.getModId();
                if (id.equals("minecraft") || id.equals("forge") || id.equals("fml")
                        || id.equals("javafml") || id.equals("lowcodefml")) continue;

                File file = jarOf(id);
                if (file == null) continue;

                String sha1 = sha1Of(file);
                if (sha1 == null) { failed++; continue; }

                try {
                    if (head("/file/" + sha1) == 200) { had++; continue; }

                    uploadNote = "выкладываю " + file.getName() + "…";
                    if (put("/file/" + sha1, file)) sent++;
                    else failed++;
                } catch (Exception e) {
                    failed++;
                    Forge_narevisuals.LOGGER.warn("{} не выложился: {}", file.getName(), e.toString());
                }
            }

            uploadNote = "выложено: " + sent + ", уже было: " + had
                    + (failed > 0 ? ", не вышло: " + failed : "");

            Forge_narevisuals.LOGGER.info("Выкладка модов: {}", uploadNote);
        }, "nare-together-share");

        worker.setDaemon(true);
        worker.start();

        uploadNote = "считаю хэши и выкладываю…";
    }

    // -------------------------------------------------------------- туннель

    private static void startTunnel() {
        if (tunnel != null && tunnel.isAlive()) return;
        if (token.isEmpty()) return;

        age++;                                  // прежние потоки, если есть, выйдут сами

        tunnel = new Thread(NareTogether::tunnelLoop, "nare-tunnel");
        tunnel.setDaemon(true);
        tunnel.start();
    }

    private static void tunnelLoop() {
        final int mine = age;                   // чьё это поколение
        int refused = 0;

        while (!stop && open && mine == age) {
            String key = token;                 // токен берём живой, не снимок

            if (key.isEmpty()) {                // объявления ещё не было
                sleep(1000);
                continue;
            }

            Socket control = null;
            try {
                control = dial();
                wire = control;
                control.setKeepAlive(true);
                control.getOutputStream().write(("NARE1 HOST " + key + "\n").getBytes(StandardCharsets.UTF_8));
                control.getOutputStream().flush();

                Forge_narevisuals.LOGGER.info("Туннель: подключился к сборному пункту");

                final Socket keeper = control;
                Thread ping = new Thread(() -> {
                    while (!stop && open && mine == age && !keeper.isClosed()) {
                        try { Thread.sleep(BEAT); } catch (InterruptedException e) { return; }
                        try {
                            keeper.getOutputStream().write("{\"ping\":1}\n".getBytes(StandardCharsets.UTF_8));
                            keeper.getOutputStream().flush();
                        } catch (IOException e) { return; }
                    }
                }, "nare-tunnel-ping");
                ping.setDaemon(true);
                ping.start();

                var reader = new java.io.BufferedReader(
                        new java.io.InputStreamReader(control.getInputStream(), StandardCharsets.UTF_8));

                String line;
                while (!stop && open && mine == age && (line = reader.readLine()) != null) {
                    if (line.trim().isEmpty()) continue;

                    JsonObject json;
                    try { json = JsonParser.parseString(line).getAsJsonObject(); }
                    catch (Exception e) { continue; }

                    if (json.has("error")) {
                        String why = json.get("error").getAsString();
                        Forge_narevisuals.LOGGER.warn("Туннель отказал: {}", why);

                        refused++;
                        note = "сборный пункт не признал комнату: " + why;

                        // ждём, пока объявление даст свежий токен: со старым
                        // стучаться бесполезно
                        for (int i = 0; i < 30 && token.equals(key) && open && mine == age; i++)
                            sleep(1000);

                        break;
                    }

                    if (json.has("port")) {
                        shownPort = json.get("port").getAsInt();
                        mode = "tunnel";
                        note = "гости идут через наш сервер: " + host() + ":" + shownPort;
                        Forge_narevisuals.LOGGER.info("Туннель открыт: {}:{}", host(), shownPort);
                    }

                    if (json.has("open")) pair(json.get("open").getAsString());
                }
            } catch (Exception e) {
                Forge_narevisuals.LOGGER.warn("Туннель оборвался: {}", e.toString());
            } finally {
                closeQuietly(control);
            }

            if (stop || !open || mine != age) return;

            if (refused >= 5) {
                mode = "direct";
                note = "туннель не принимают — закройте и снова откройте мир";
                Forge_narevisuals.LOGGER.warn("Туннель сдался после {} отказов", refused);
                return;
            }

            mode = "direct";
            note = "туннель переподключается…";
            sleep(5000);
        }
    }

    private static void sleep(long ms) {
        try { Thread.sleep(ms); } catch (InterruptedException e) { Thread.currentThread().interrupt(); }
    }

    /** На каждого гостя открываем встречный канал и сводим его с игрой. */
    private static void pair(String id) {
        Thread worker = new Thread(() -> {
            Socket up = null, down = null;
            try {
                up = dial();
                up.getOutputStream().write(("NARE1 DATA " + id + "\n").getBytes(StandardCharsets.UTF_8));
                up.getOutputStream().flush();

                down = new Socket();
                down.connect(new InetSocketAddress("127.0.0.1", gamePort), WAIT);

                pipe(up, down, "гость->игра");
                pipe(down, up, "игра->гость");
                return;                                 // закроют сами потоки
            } catch (Exception e) {
                Forge_narevisuals.LOGGER.warn("Канал гостя не свёлся: {}", e.toString());
                closeQuietly(up);
                closeQuietly(down);
            }
        }, "nare-tunnel-guest");

        worker.setDaemon(true);
        worker.start();
    }

    private static void pipe(Socket from, Socket to, String title) {
        Thread worker = new Thread(() -> {
            byte[] buffer = new byte[32 * 1024];
            try {
                InputStream in = from.getInputStream();
                OutputStream out = to.getOutputStream();

                int read;
                while ((read = in.read(buffer)) > 0) {
                    out.write(buffer, 0, read);
                    out.flush();
                }
            } catch (IOException ignored) {
            } finally {
                closeQuietly(from);
                closeQuietly(to);
            }
        }, "nare-pipe " + title);

        worker.setDaemon(true);
        worker.start();
    }

    private static Socket dial() throws IOException {
        String value = where();
        int colon = value.lastIndexOf(':');

        String host = colon > 0 ? value.substring(0, colon) : value;
        int port = colon > 0 ? Integer.parseInt(value.substring(colon + 1)) : 2445;

        Socket socket = new Socket();
        socket.connect(new InetSocketAddress(host, port), WAIT);
        socket.setTcpNoDelay(true);
        return socket;
    }

    private static void closeQuietly(Socket socket) {
        if (socket == null) return;
        try { socket.close(); } catch (IOException ignored) { }
    }

    // ----------------------------------------------------------------- HTTP

    private static HttpURLConnection connect(String route, String method) throws IOException {
        var connection = (HttpURLConnection) new URL("http://" + where() + route).openConnection();
        connection.setRequestMethod(method);
        connection.setConnectTimeout(WAIT);
        connection.setReadTimeout(WAIT);
        connection.setRequestProperty("User-Agent", "NareVisuals");
        return connection;
    }

    private static String post(String route, String body) throws IOException {
        HttpURLConnection connection = connect(route, "POST");
        connection.setDoOutput(true);
        connection.setRequestProperty("Content-Type", "application/json; charset=utf-8");

        byte[] data = body.getBytes(StandardCharsets.UTF_8);
        connection.setFixedLengthStreamingMode(data.length);

        try (OutputStream out = connection.getOutputStream()) { out.write(data); }

        int code = connection.getResponseCode();
        try (InputStream in = code < 400 ? connection.getInputStream() : connection.getErrorStream()) {
            String answer = in == null ? "" : new String(in.readAllBytes(), StandardCharsets.UTF_8);
            if (code >= 400) throw new IOException("ответ " + code + ": " + answer);
            return answer;
        }
    }

    private static int head(String route) throws IOException {
        HttpURLConnection connection = connect(route, "HEAD");
        return connection.getResponseCode();
    }

    private static boolean put(String route, File file) throws IOException {
        HttpURLConnection connection = connect(route, "PUT");
        connection.setDoOutput(true);
        connection.setRequestProperty("Content-Type", "application/java-archive");
        connection.setFixedLengthStreamingMode(file.length());

        try (OutputStream out = connection.getOutputStream();
             InputStream in = Files.newInputStream(file.toPath())) {

            byte[] buffer = new byte[64 * 1024];
            int read;
            while ((read = in.read(buffer)) > 0) out.write(buffer, 0, read);
        }

        return connection.getResponseCode() < 400;
    }

    /** Для окна настроек: список того, что увидят гости. */
    public static List<String> summary() {
        var lines = new ArrayList<String>();

        lines.add(open ? note : "мир не объявлен");
        if (open && gamePort > 0) lines.add("порт игры: " + gamePort);
        if (!uploadNote.isEmpty()) lines.add(uploadNote);

        return lines;
    }
}
