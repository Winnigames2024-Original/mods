package com.winnigames2024.geodash;

import android.graphics.Color;
import android.widget.LinearLayout;

import org.json.JSONArray;
import org.json.JSONObject;

/** Админка: игроки, уровни, daily/weekly, объявления — только для админа. */
public class AdminActivity extends GdActivity {

    LinearLayout box;
    String tab = "users";       // users | levels
    JSONArray data;
    String stats = "";

    @Override protected int bgA() { return Color.rgb(210, 45, 60); }
    @Override protected int bgId() { return 19; }
    @Override protected int groundId() { return 10; }
    @Override protected String headTitle() { return "АДМИНКА"; }
    @Override protected String headSub() { return stats.isEmpty() ? "только для Winnigames2024" : stats; }

    @Override protected void build() {
        if (!Store.admin()) {
            card(scrollBox(0.1f, 0.3f, 0.8f, 0.3f), "Доступ только для админа", "", "", -1, null);
            back(0.03f, 0.86f);
            return;
        }

        btn("ИГРОКИ", Ui.I_PROFILE, tab.equals("users") ? Color.rgb(120, 220, 255) : Color.rgb(70, 76, 125),
                tab.equals("users") ? Color.rgb(20, 110, 180) : Color.rgb(34, 38, 70),
                0.06f, 0.17f, 0.20f, 0.10f, () -> { tab = "users"; rebuild(); });
        btn("УРОВНИ", Ui.I_EDIT, tab.equals("levels") ? Color.rgb(120, 220, 255) : Color.rgb(70, 76, 125),
                tab.equals("levels") ? Color.rgb(20, 110, 180) : Color.rgb(34, 38, 70),
                0.28f, 0.17f, 0.20f, 0.10f, () -> { tab = "levels"; rebuild(); });
        btn("DAILY → WEEKLY", Ui.I_UPLOAD, Color.rgb(255, 180, 120), Color.rgb(170, 90, 30),
                0.50f, 0.17f, 0.22f, 0.10f, this::promote);
        btn("ОБЪЯВЛЕНИЕ", Ui.I_ADMIN, Color.rgb(160, 180, 230), Color.rgb(60, 80, 150),
                0.74f, 0.17f, 0.20f, 0.10f, this::broadcast);

        box = scrollBox(0.06f, 0.30f, 0.88f, 0.52f);
        back(0.03f, 0.86f);
        btn("ОБНОВИТЬ", Ui.I_REFRESH, Color.rgb(150, 170, 220), Color.rgb(60, 70, 140),
                0.20f, 0.86f, 0.20f, 0.10f, this::reload);

        reload();
    }

    void reload() {
        if (box == null) return;
        box.removeAllViews();
        card(box, "Загружаю…", "", "", -1, null);
        Api.async(() -> {
            JSONObject st = Api.obj(Api.rawGet("/api/admin/stats"));
            if (st != null)
                stats = "игроков: " + st.optInt("users") + "   уровней: " + st.optInt("levels")
                        + "   песен: " + st.optInt("songs");
            final JSONArray list = tab.equals("users")
                    ? Api.arr(Api.rawGet("/api/admin/users"))
                    : Api.arr(Api.rawGet("/api/levels?sort=recent&limit=50"));
            data = list;
            ui.post(this::show);
        });
    }

    void show() {
        box.removeAllViews();
        if (header != null) header.invalidate();
        if (data == null || data.length() == 0) {
            card(box, "Пусто или нет связи", "", "", -1, null);
            return;
        }
        for (int i = 0; i < data.length(); i++) {
            final JSONObject o = data.optJSONObject(i);
            if (o == null) continue;
            if (tab.equals("users")) {
                final String name = o.optString("name");
                card(box, name + (o.optBoolean("admin") ? "   ★ админ" : "") + (o.optBoolean("banned") ? "   ⛔ бан" : ""),
                        "звёзд: " + o.optInt("stars") + "   монет: " + o.optInt("coins")
                                + "   демонов: " + o.optInt("demons"),
                        "", -1, () -> userMenu(name, o.optBoolean("banned"), o.optBoolean("admin")));
            } else {
                final int id = o.optInt("id");
                card(box, "#" + id + "  " + o.optString("name"),
                        "автор: " + o.optString("author") + "   " + Gd.DIFFS[Math.max(0, Math.min(11, o.optInt("difficulty")))],
                        (o.optBoolean("featured") ? "FEATURED  " : "") + (o.optBoolean("epic") ? "EPIC" : ""),
                        o.optInt("difficulty"), () -> levelMenu(id, o.optString("name")));
            }
        }
    }

    // ------------------------------------------------------------ игроки

    void userMenu(final String name, final boolean banned, final boolean admin) {
        GdDialog.items(this, name, new String[]{
                banned ? "Разбанить" : "Забанить",
                admin ? "Снять админку" : "Сделать админом",
                "Выдать награды",
                "Сбросить пароль",
                "Удалить игрока",
                "Отмена"
        }, i -> {
            switch (i) {
                case 0: post("/api/admin/ban", json("name", name, "ban", !banned)); break;
                case 1: post("/api/admin/grant", json("name", name, "admin", !admin)); break;
                case 2: give(name); break;
                case 3: GdDialog.input(this, "Новый пароль", "", "", true, p -> {
                            if (p != null && p.trim().length() >= 3)
                                post("/api/admin/reset-password", json("name", name, "password", p.trim()));
                        }); break;
                case 4: confirm("Удалить игрока " + name + " и его уровни?",
                            () -> post("/api/admin/delete-user", json("name", name))); break;
            }
        });
    }

    void give(final String name) {
        GdDialog.input(this, "Звёзды (можно со знаком −)", "", "10", false, s1 ->
                GdDialog.input(this, "Монеты", "", "0", false, s2 ->
                        GdDialog.input(this, "Демоны", "", "0", false, s3 -> {
                            try {
                                JSONObject b = new JSONObject();
                                b.put("name", name);
                                b.put("stars", Integer.parseInt(s1.trim()));
                                b.put("coins", Integer.parseInt(s2.trim()));
                                b.put("demons", Integer.parseInt(s3.trim()));
                                post("/api/admin/give", b.toString());
                            } catch (Exception e) { toast("Нужны числа"); }
                        })));
    }

    // ------------------------------------------------------------ уровни

    void levelMenu(final int id, final String name) {
        GdDialog.items(this, "#" + id + "  " + name, new String[]{
                "Оценить сложность", "FEATURED", "EPIC",
                "Сделать DAILY", "Сделать WEEKLY", "Офлайн-пак вкл/выкл",
                "Удалить уровень", "Отмена"
        }, i -> {
            switch (i) {
                case 0:
                    GdDialog.items(this, "Сложность", Gd.DIFFS, d ->
                            post("/api/admin/rate", json2("id", id, "difficulty", d)));
                    break;
                case 1: post("/api/admin/rate", "{\"id\":" + id + ",\"featured\":true}"); break;
                case 2: post("/api/admin/rate", "{\"id\":" + id + ",\"featured\":true,\"epic\":true}"); break;
                case 3: post("/api/admin/daily", "{\"id\":" + id + "}"); break;
                case 4: post("/api/admin/weekly", "{\"id\":" + id + "}"); break;
                case 5: post("/api/admin/offline", "{\"id\":" + id + ",\"offline\":true}"); break;
                case 6: confirm("Удалить уровень #" + id + "?",
                            () -> post("/api/levels/" + id + "/delete", "{}")); break;
            }
        });
    }

    void promote() {
        confirm("Сделать текущий daily уровнем недели?", () -> post("/api/admin/daily-to-weekly", "{}"));
    }

    void broadcast() {
        GdDialog.input(this, "Объявление всем игрокам", "", "", false, t -> {
            if (t != null && !t.trim().isEmpty())
                post("/api/admin/broadcast", json("text", t.trim()));
        });
    }

    // ------------------------------------------------------------ мелочи

    String json(String k, String v) {
        try { return new JSONObject().put(k, v).toString(); } catch (Exception e) { return "{}"; }
    }

    String json(String k, String v, String k2, boolean v2) {
        try { return new JSONObject().put(k, v).put(k2, v2).toString(); } catch (Exception e) { return "{}"; }
    }

    String json(String k, String v, String k2, String v2) {
        try { return new JSONObject().put(k, v).put(k2, v2).toString(); } catch (Exception e) { return "{}"; }
    }

    String json2(String k, int v, String k2, int v2) {
        try { return new JSONObject().put(k, v).put(k2, v2).toString(); } catch (Exception e) { return "{}"; }
    }

    void post(final String path, final String body) {
        toast("Отправляю…");
        Api.async(() -> {
            final String r = Api.rawPost(path, body);
            ui.post(() -> {
                toast(r != null ? "Готово" : "Не вышло — нет связи или прав");
                reload();
            });
        });
    }
}
