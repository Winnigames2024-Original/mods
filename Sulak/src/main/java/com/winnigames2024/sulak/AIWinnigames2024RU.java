package com.winnigames2024.sulak;

import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

import java.net.URI;
import java.net.URLEncoder;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.charset.StandardCharsets;
import java.time.Duration;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.Executors;
import java.util.concurrent.ExecutorService;

/**
 * Единственная точка общения мода с сайтом ai.winnigames2024.ru.
 *
 * Запрос:
 *   {apiUrl}?dbtoken=<uuid игрока>&system_prompt=<...>&user_promps=<...>&api_key=<...>
 * Отправляется POST-ом (form-urlencoded), api.php читает $_REQUEST,
 * поэтому обычная GET-ссылка из документации тоже работает.
 *
 * Ответ (JSON):
 *   { "ok": true, "say": "текст", "actions": [ {...}, ... ], "memory": 12 }
 */
public final class AIWinnigames2024RU {

    private AIWinnigames2024RU() {}

    private static final ExecutorService POOL = Executors.newFixedThreadPool(2, r -> {
        Thread t = new Thread(r, "sulak-ai");
        t.setDaemon(true);
        return t;
    });

    private static volatile HttpClient CLIENT;

    private static HttpClient client() {
        HttpClient c = CLIENT;
        if (c == null) {
            synchronized (AIWinnigames2024RU.class) {
                c = CLIENT;
                if (c == null) {
                    c = HttpClient.newBuilder()
                            .connectTimeout(Duration.ofSeconds(10))
                            .followRedirects(HttpClient.Redirect.NORMAL)
                            .build();
                    CLIENT = c;
                }
            }
        }
        return c;
    }

    /** Результат разбора ответа сайта. */
    public static final class Result {
        public final boolean ok;
        public final String say;
        public final JsonArray actions;
        public final String error;

        private Result(boolean ok, String say, JsonArray actions, String error) {
            this.ok = ok;
            this.say = say;
            this.actions = actions == null ? new JsonArray() : actions;
            this.error = error;
        }

        public static Result ok(String say, JsonArray actions) {
            return new Result(true, say, actions, null);
        }

        public static Result fail(String error) {
            return new Result(false, null, null, error);
        }
    }

    /**
     * Асинхронно спрашивает ИИ. dbtoken — UUID игрока, он же ключ памяти на сервере.
     */
    public static CompletableFuture<Result> ask(UUID dbtoken, String systemPrompt, String userPrompt, String apiKey) {
        return CompletableFuture.supplyAsync(() -> request(dbtoken, systemPrompt, userPrompt, apiKey), POOL);
    }

    private static Result request(UUID dbtoken, String systemPrompt, String userPrompt, String apiKey) {
        try {
            String body = "dbtoken=" + enc(dbtoken.toString())
                    + "&system_prompt=" + enc(systemPrompt)
                    + "&user_promps=" + enc(userPrompt)
                    + "&api_key=" + enc(apiKey == null ? "" : apiKey);

            HttpRequest req = HttpRequest.newBuilder()
                    .uri(URI.create(Config.apiUrl))
                    .timeout(Duration.ofSeconds(Config.timeoutSeconds))
                    .header("Content-Type", "application/x-www-form-urlencoded; charset=UTF-8")
                    .header("Accept", "application/json")
                    .header("User-Agent", "SulakMod/1.0 (Minecraft Forge 1.21.10)")
                    .POST(HttpRequest.BodyPublishers.ofString(body, StandardCharsets.UTF_8))
                    .build();

            HttpResponse<String> res = client().send(req, HttpResponse.BodyHandlers.ofString(StandardCharsets.UTF_8));

            if (res.statusCode() / 100 != 2) {
                return Result.fail("HTTP " + res.statusCode());
            }
            return parse(res.body());
        } catch (Exception e) {
            Sulak.LOGGER.warn("[Sulak] AI request failed", e);
            return Result.fail(e.getClass().getSimpleName() + ": " + e.getMessage());
        }
    }

    /** Разбирает ответ. Терпим к тому, что модель вернула JSON внутри текста. */
    public static Result parse(String raw) {
        if (raw == null || raw.isBlank()) return Result.fail("пустой ответ");
        try {
            JsonElement root = JsonParser.parseString(raw.trim());
            if (!root.isJsonObject()) return Result.ok(raw.trim(), null);
            JsonObject o = root.getAsJsonObject();

            if (o.has("ok") && !o.get("ok").getAsBoolean()) {
                String err = o.has("error") ? o.get("error").getAsString() : "ошибка сервера";
                return Result.fail(err);
            }

            // api.php уже разобрал ответ модели
            if (o.has("actions") || o.has("say")) {
                String say = str(o, "say");
                JsonArray actions = o.has("actions") && o.get("actions").isJsonArray()
                        ? o.getAsJsonArray("actions") : new JsonArray();
                return Result.ok(say, actions);
            }

            // api.php вернул сырой текст модели в reply/text/result
            String inner = str(o, "reply");
            if (inner == null) inner = str(o, "text");
            if (inner == null) inner = str(o, "result");
            if (inner != null) return fromModelText(inner);

            return Result.ok(raw.trim(), null);
        } catch (Exception e) {
            return fromModelText(raw);
        }
    }

    /** Достаёт JSON-объект из произвольного текста модели. */
    private static Result fromModelText(String text) {
        String t = text == null ? "" : text.trim();
        if (t.startsWith("```")) {
            int nl = t.indexOf('\n');
            if (nl > 0) t = t.substring(nl + 1);
            if (t.endsWith("```")) t = t.substring(0, t.length() - 3);
            t = t.trim();
        }
        int a = t.indexOf('{');
        int b = t.lastIndexOf('}');
        if (a >= 0 && b > a) {
            try {
                JsonObject o = JsonParser.parseString(t.substring(a, b + 1)).getAsJsonObject();
                String say = str(o, "say");
                JsonArray actions = o.has("actions") && o.get("actions").isJsonArray()
                        ? o.getAsJsonArray("actions") : new JsonArray();
                if (say != null || actions.size() > 0) return Result.ok(say, actions);
            } catch (Exception ignored) {
            }
        }
        return Result.ok(text, null);
    }

    private static String str(JsonObject o, String key) {
        if (o.has(key) && o.get(key).isJsonPrimitive()) return o.get(key).getAsString();
        return null;
    }

    private static String enc(String s) {
        return URLEncoder.encode(s == null ? "" : s, StandardCharsets.UTF_8);
    }
}
