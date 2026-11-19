package com.winnigames2024.geodash;

import android.app.AlertDialog;
import android.graphics.Color;
import android.widget.LinearLayout;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.List;

/**
 * Список уровней. Режимы: saved, online, featured, epic, my, user:&lt;имя&gt;, search:&lt;строка&gt;.
 */
public class LevelListActivity extends GdActivity {

    String mode = "saved";
    LinearLayout box;

    @Override protected int bgA() {
        return mode.startsWith("saved") ? Color.rgb(16, 50, 40) : Color.rgb(16, 34, 76);
    }

    @Override protected String headTitle() {
        if (mode.startsWith("user:")) return "УРОВНИ " + mode.substring(5).toUpperCase();
        if (mode.startsWith("search:")) return "ПОИСК";
        switch (mode) {
            case "saved": return "SAVED";
            case "featured": return "FEATURED";
            case "epic": return "EPIC";
            case "my": return "MY LEVELS";
            default: return "ОНЛАЙН-УРОВНИ";
        }
    }

    @Override protected String headSub() {
        if (mode.equals("saved")) return "лежат в assets/saved — играются без интернета";
        return Api.online ? "сервер онлайн" : "офлайн — показан кэш";
    }

    @Override protected void build() {
        String m = getIntent().getStringExtra("mode");
        if (m != null) mode = m;

        box = scrollBox(0.04f, 0.20f, 0.92f, 0.64f);
        back(0.04f, 0.86f);
        btn("ОБНОВИТЬ", Ui.I_REFRESH, Color.rgb(160, 170, 210), Color.rgb(70, 80, 130),
                0.20f, 0.86f, 0.18f, 0.11f, this::load);
        load();
    }

    void load() {
        box.removeAllViews();
        if (mode.equals("saved") || mode.equals("my")) {
            List<Level> list = mode.equals("saved") ? Store.savedLevels() : Store.myLevels();
            if (list.isEmpty()) {
                card(box, "Пусто", mode.equals("saved")
                        ? "сохрани уровень из онлайна или CREATOR → SEARCH"
                        : "создай уровень в EDITOR", "", -1, null);
                return;
            }
            for (Level lv : list) {
                double[] pr = Store.progress(lv);
                card(box, lv.settings.name + (lv.settings.serverId > 0 ? "  #" + lv.settings.serverId : ""),
                        "автор: " + lv.settings.author + "   " + Gd.DIFFS[lv.settings.difficulty]
                                + (Music.isCached(lv.settings.songId) ? "   ♪ офлайн" : ""),
                        String.format("%.0f%%", pr[0]), lv.settings.difficulty,
                        () -> localMenu(lv));
            }
            return;
        }

        card(box, "Загружаю…", "", "", -1, null);
        Api.async(() -> {
            JSONArray arr;
            if (mode.startsWith("user:")) arr = Api.levelsOf(mode.substring(5));
            else if (mode.startsWith("search:")) arr = Api.levels(mode.substring(7), "recent", -1);
            else if (mode.equals("featured")) arr = Api.levels("", "featured", -1);
            else if (mode.equals("epic")) arr = Api.levels("", "epic", -1);
            else arr = Api.levels("", "recent", -1);

            final List<JSONObject> items = new ArrayList<>();
            for (int i = 0; i < arr.length(); i++) {
                JSONObject o = arr.optJSONObject(i);
                if (o != null) items.add(o);
            }
            ui.post(() -> {
                box.removeAllViews();
                if (items.isEmpty()) { card(box, "Ничего не найдено", "", "", -1, null); return; }
                for (JSONObject o : items) {
                    int id = o.optInt("id");
                    String flags = (o.optBoolean("daily") ? " [DAILY]" : "")
                            + (o.optBoolean("weekly") ? " [WEEKLY]" : "")
                            + (o.optBoolean("epic") ? " [EPIC]" : o.optBoolean("featured") ? " [FEATURED]" : "")
                            + (o.optBoolean("offline") ? " [OFFLINE]" : "")
                            + (Store.isSaved(id) ? "  ✔" : "");
                    card(box, "#" + id + "  " + o.optString("name") + flags,
                            "автор: " + o.optString("author") + "   ★" + o.optInt("stars")
                                    + "   ♥" + o.optInt("likes") + "   ↓" + o.optInt("downloads"),
                            Gd.DIFFS[Math.min(11, o.optInt("difficulty"))],
                            Math.min(11, o.optInt("difficulty")),
                            () -> onlineMenu(o));
                }
            });
        });
    }

    void localMenu(Level lv) {
        String[] items = mode.equals("my")
                ? new String[]{"Играть", "Тренировка", "Редактировать", "Залить на сервер", "Удалить"}
                : new String[]{"Играть", "Тренировка", "Копировать в мои", "Удалить из SAVED"};
        new AlertDialog.Builder(this).setTitle(lv.settings.name)
                .setItems(items, (d, i) -> {
                    if (i == 0) GameActivity.play(this, lv, false);
                    else if (i == 1) GameActivity.play(this, lv, true);
                    else if (i == 2) {
                        if (mode.equals("my")) EditorCanvasActivity.edit(this, lv);
                        else copyToMine(lv);
                    } else if (i == 3) {
                        if (mode.equals("my")) upload(lv);
                        else confirm("Удалить из SAVED?", () -> {
                            Store.savedFile(lv.settings.serverId).delete();
                            load();
                        });
                    } else if (i == 4) {
                        confirm("Удалить уровень?", () -> {
                            new java.io.File(Store.levels(), Store.safe(lv.settings.localId) + ".gdl").delete();
                            load();
                        });
                    }
                }).show();
    }

    void upload(Level lv) {
        if (!Store.loggedIn()) { toast("Сначала войди в аккаунт"); return; }
        toast("Заливаю…");
        Api.async(() -> {
            int id = Api.uploadLevel(lv);
            ui.post(() -> {
                if (id > 0) {
                    lv.settings.serverId = id;
                    Store.writeFile(new java.io.File(Store.levels(),
                            Store.safe(lv.settings.localId) + ".gdl"), lv.toJson());
                    toast("Готово, ID #" + id);
                } else toast("Нет сети — уедет позже");
                load();
            });
        });
    }

    void copyToMine(Level lv) {
        if (lv.settings.copyMode == 0) { toast("Автор закрыл копирование"); return; }
        if (lv.settings.copyMode == 1) {
            askPassword("Пароль автора для копирования:", pass -> {
                if (!pass.equals(lv.settings.copyPassword)) { toast("Неверный пароль"); return; }
                doCopy(lv);
            });
        } else doCopy(lv);
    }

    void doCopy(Level lv) {
        Level c = Level.parse(lv.toJson());
        c.settings.localId = java.util.UUID.randomUUID().toString().replace("-", "");
        c.settings.serverId = 0;
        c.settings.name = lv.settings.name + " (копия)";
        c.settings.author = Store.user();
        c.settings.copyMode = 0;
        c.settings.copyPassword = "";
        Store.writeFile(new java.io.File(Store.levels(), Store.safe(c.settings.localId) + ".gdl"), c.toJson());
        toast("Скопировано в MY LEVELS");
    }

    void onlineMenu(JSONObject o) {
        final int id = o.optInt("id");
        new AlertDialog.Builder(this).setTitle("#" + id + "  " + o.optString("name"))
                .setItems(new String[]{"Играть", "Сохранить офлайн", "Копировать в мои", "Лайк", "Профиль автора"},
                        (d, i) -> {
                            if (i == 0) fetchThen(id, lv -> GameActivity.play(this, lv, false));
                            else if (i == 1) fetchThen(id, lv -> toast("Сохранено в SAVED"));
                            else if (i == 2) fetchThen(id, this::copyToMine);
                            else if (i == 3) Api.async(() -> { Api.like(id); ui.post(() -> toast("Лайк отправлен")); });
                            else go(ProfileActivity.class, "user", o.optString("author"));
                        }).show();
    }

    void fetchThen(int id, java.util.function.Consumer<Level> cb) {
        toast("Скачиваю…");
        Api.async(() -> {
            Level lv = Api.downloadLevel(id);
            if (lv == null && Store.isSaved(id)) lv = Store.loadSaved(id);
            final Level f = lv;
            ui.post(() -> {
                if (f == null) toast("Нет сети и уровень не сохранён");
                else cb.accept(f);
            });
        });
    }
}
