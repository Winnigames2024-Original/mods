package com.winnigames2024.nareserver;

import org.bukkit.Bukkit;
import org.bukkit.plugin.java.JavaPlugin;
import org.bukkit.scheduler.BukkitRunnable;

import java.io.InputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;

/**
 * Забирает команды, поставленные в админ-панели лаунчера, и выполняет их на
 * сервере от консоли.
 *
 * Как это работает: админ в лаунчере пишет команду, она ложится в очередь на
 * сборном пункте NareNet (на VPS). Плагин раз в несколько секунд спрашивает
 * очередь своим ключом, забирает накопившееся и выполняет. Ключ — общий
 * секрет между этим плагином и VPS, он в config.yml.
 *
 * Сеть в отдельном потоке, выполнение команд — на главном: Bukkit этого
 * требует.
 */
public final class NareCommands {

    private final JavaPlugin plugin;
    private final String host;      // адрес VPS: 151.245.217.104:2445
    private final String key;       // общий ключ
    private final int seconds;      // как часто спрашивать

    private BukkitRunnable task;

    public NareCommands(JavaPlugin plugin, String host, String key, int seconds) {
        this.plugin = plugin;
        this.host = host == null ? "" : host.trim();
        this.key = key == null ? "" : key.trim();
        this.seconds = Math.max(2, seconds);
    }

    public boolean ready() {
        return !host.isEmpty() && !key.isEmpty();
    }

    public void start() {
        if (!ready()) {
            plugin.getLogger().info("NareVisuals: команды из лаунчера выключены "
                    + "(не задан narenet.host или narenet.key в config.yml)");
            return;
        }

        task = new BukkitRunnable() {
            @Override
            public void run() {
                poll();
            }
        };
        // первый опрос чуть погодя, дальше каждые seconds секунд
        task.runTaskTimerAsynchronously(plugin, 100L, seconds * 20L);

        plugin.getLogger().info("NareVisuals: команды из лаунчера включены, опрос каждые "
                + seconds + " с через " + host);
    }

    public void stop() {
        if (task != null) {
            try { task.cancel(); } catch (Throwable ignored) { }
            task = null;
        }
    }

    private void poll() {
        String body;
        try {
            body = get("/commands?key=" + URLEncoder.encode(key, StandardCharsets.UTF_8));
        } catch (Exception e) {
            // VPS недоступен — молчим, попробуем в следующий раз
            return;
        }

        List<String> list = parse(body);
        if (list.isEmpty()) return;

        for (String raw : list) {
            final String command = raw.trim();
            if (command.isEmpty()) continue;

            // выполнять команды можно только на главном потоке
            Bukkit.getScheduler().runTask(plugin, () -> {
                try {
                    plugin.getLogger().info("Команда из лаунчера: " + command);
                    Bukkit.dispatchCommand(Bukkit.getConsoleSender(), command);
                } catch (Throwable t) {
                    plugin.getLogger().warning("Команда не выполнилась: " + command + " — " + t);
                }
            });
        }
    }

    /**
     * Достаёт строки из ответа вида {"commands":["a","b"]}. Формат наш, его
     * пишет сам сервер, поэтому хватает аккуратного разбора кавычек без
     * сторонних библиотек — gson на сервере Paper на компиляции не гарантирован.
     */
    static List<String> parse(String body) {
        var out = new ArrayList<String>();
        if (body == null) return out;

        int open = body.indexOf('[');
        int close = body.lastIndexOf(']');
        if (open < 0 || close <= open) return out;

        String inside = body.substring(open + 1, close);

        int i = 0;
        int n = inside.length();
        while (i < n) {
            // ищем начало строки
            while (i < n && inside.charAt(i) != '"') i++;
            if (i >= n) break;
            i++;

            var sb = new StringBuilder();
            while (i < n) {
                char c = inside.charAt(i++);
                if (c == '\\' && i < n) {
                    char e = inside.charAt(i++);
                    switch (e) {
                        case 'n': sb.append('\n'); break;
                        case 't': sb.append('\t'); break;
                        case 'r': sb.append('\r'); break;
                        case '"': sb.append('"'); break;
                        case '\\': sb.append('\\'); break;
                        case '/': sb.append('/'); break;
                        case 'u':
                            if (i + 4 <= n) {
                                try { sb.append((char) Integer.parseInt(inside.substring(i, i + 4), 16)); }
                                catch (Exception ignored) { }
                                i += 4;
                            }
                            break;
                        default: sb.append(e);
                    }
                } else if (c == '"') {
                    break;
                } else {
                    sb.append(c);
                }
            }
            out.add(sb.toString());
        }

        return out;
    }

    private String get(String route) throws Exception {
        URL url = new URL("http://" + host + route);
        HttpURLConnection connection = (HttpURLConnection) url.openConnection();
        connection.setRequestMethod("GET");
        connection.setConnectTimeout(6000);
        connection.setReadTimeout(6000);
        connection.setRequestProperty("User-Agent", "NareServer");

        int code = connection.getResponseCode();
        try (InputStream in = code < 400 ? connection.getInputStream() : connection.getErrorStream()) {
            String text = in == null ? "" : new String(in.readAllBytes(), StandardCharsets.UTF_8);
            if (code >= 400) throw new Exception("ответ " + code);
            return text;
        }
    }
}
