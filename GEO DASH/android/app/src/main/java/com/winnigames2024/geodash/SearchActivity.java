package com.winnigames2024.geodash;

import android.graphics.Color;
import android.widget.EditText;
import android.widget.LinearLayout;

import org.json.JSONArray;
import org.json.JSONObject;

/** Поиск уровней (по названию и по ID) и поиск игроков. */
public class SearchActivity extends GdActivity {

    String mode = "levels";     // levels | users | top
    EditText query;
    LinearLayout box;

    @Override protected int bgA() {
        return mode.equals("levels") ? Color.rgb(18, 40, 90) : Color.rgb(60, 24, 70);
    }

    @Override protected String headTitle() {
        switch (mode) {
            case "users": return "ПОИСК ИГРОКОВ";
            case "top": return "ТОП ИГРОКОВ";
            default: return "ПОИСК УРОВНЕЙ";
        }
    }

    @Override protected String headSub() {
        switch (mode) {
            case "users": return "введи имя игрока или часть имени";
            case "top": return "лучшие по звёздам";
            default: return "введи название уровня или его ID (например 12 или #12)";
        }
    }

    @Override protected void build() {
        String m = getIntent().getStringExtra("mode");
        if (m != null) mode = m;

        if (!mode.equals("top")) {
            query = input(mode.equals("users") ? "имя игрока" : "название или #ID", "",
                    0.06f, 0.19f, 0.52f, 0.10f);
            btn("ИСКАТЬ", Ui.I_SEARCH, Color.rgb(120, 220, 255), Color.rgb(20, 110, 180),
                    0.60f, 0.19f, 0.16f, 0.10f, this::run);
        }

        btn(mode.equals("levels") ? "ИГРОКИ" : "УРОВНИ", Ui.I_REFRESH,
                Color.rgb(200, 170, 255), Color.rgb(100, 60, 180),
                0.78f, 0.19f, 0.16f, 0.10f, () -> {
                    mode = mode.equals("levels") ? "users" : "levels";
                    rebuild();
                });

        box = scrollBox(0.04f, 0.32f, 0.92f, 0.52f);
        back(0.04f, 0.86f);
        run();
    }

    void run() {
        final String q = query == null ? "" : query.getText().toString().trim().replace("#", "");
        box.removeAllViews();
        card(box, "Ищу…", "", "", -1, null);

        Api.async(() -> {
            if (mode.equals("users") || mode.equals("top")) {
                JSONArray arr = mode.equals("top") ? Api.top() : Api.searchUsers(q);
                ui.post(() -> showUsers(arr));
                return;
            }

            // поиск по ID: если ввели число — сначала точный уровень
            JSONArray arr = null;
            try {
                int id = Integer.parseInt(q);
                JSONObject one = Api.level(id);
                if (one != null && one.optInt("id") > 0) {
                    arr = new JSONArray();
                    arr.put(one);
                }
            } catch (Exception ignored) { }
            if (arr == null) arr = Api.levels(q, "recent", -1);
            final JSONArray res = arr;
            ui.post(() -> showLevels(res));
        });
    }

    void showLevels(JSONArray arr) {
        box.removeAllViews();
        if (arr.length() == 0) { card(box, "Ничего не найдено", "", "", -1, null); return; }
        for (int i = 0; i < arr.length(); i++) {
            final JSONObject o = arr.optJSONObject(i);
            if (o == null) continue;
            int id = o.optInt("id");
            card(box, "#" + id + "  " + o.optString("name") + (Store.isSaved(id) ? "  ✔" : ""),
                    "автор: " + o.optString("author") + "   ★" + o.optInt("stars")
                            + "   ♥" + o.optInt("likes") + "   ↓" + o.optInt("downloads"),
                    Gd.DIFFS[Math.min(11, o.optInt("difficulty"))],
                    Math.min(11, o.optInt("difficulty")),
                    () -> levelMenu(o));
        }
    }

    void showUsers(JSONArray arr) {
        box.removeAllViews();
        if (arr.length() == 0) { card(box, "Игроки не найдены", "", "", -1, null); return; }
        for (int i = 0; i < arr.length(); i++) {
            final JSONObject u = arr.optJSONObject(i);
            if (u == null) continue;
            final String name = u.optString("name");
            card(box, name + (u.optBoolean("admin") ? "  [админ]" : "") + (u.optBoolean("banned") ? "  [бан]" : ""),
                    "★ " + u.optInt("stars") + "   монет: " + u.optInt("coins")
                            + "   демонов: " + u.optInt("demons") + "   уровней: " + u.optInt("levels"),
                    "", -1, () -> userMenu(name));
        }
    }

    void levelMenu(JSONObject o) {
        final int id = o.optInt("id");
        menu("#" + id + "  " + o.optString("name"),
                new String[]{"Играть", "Сохранить офлайн", "Лайк", "Профиль автора"},
                        i -> {
                            if (i == 0 || i == 1) {
                                toast("Скачиваю…");
                                Api.async(() -> {
                                    Level lv = Api.downloadLevel(id);
                                    final Level f = lv != null ? lv : (Store.isSaved(id) ? Store.loadSaved(id) : null);
                                    ui.post(() -> {
                                        if (f == null) { toast("Нет сети"); return; }
                                        if (i == 0) GameActivity.play(this, f, false);
                                        else toast("Сохранено в SAVED");
                                    });
                                });
                            } else if (i == 2) Api.async(() -> { Api.like(id); ui.post(() -> toast("Лайк отправлен")); });
                            else go(ProfileActivity.class, "user", o.optString("author"));
                        });
    }

    void userMenu(String name) {
        menu(name, new String[]{"Профиль", "В друзья", "Его уровни"}, i -> {
                    if (i == 0) go(ProfileActivity.class, "user", name);
                    else if (i == 1) Api.async(() -> {
                        boolean ok = Api.friendAction("request", name);
                        ui.post(() -> toast(ok ? "Запрос отправлен" : "Нет сети — встало в очередь"));
                    });
                    else go(LevelListActivity.class, "mode", "user:" + name);
                });
    }
}
