package com.winnigames2024.geodash;

import android.graphics.Color;
import android.widget.LinearLayout;

import org.json.JSONArray;
import org.json.JSONObject;

/** Друзья: запросы, приём, отклонение, удаление. */
public class FriendsActivity extends GdActivity {

    LinearLayout box;

    @Override protected int bgA() { return Color.rgb(215, 60, 150); }
    @Override protected int bgId() { return 18; }
    @Override protected int groundId() { return 6; }
    @Override protected String headTitle() { return "ДРУЗЬЯ"; }
    @Override protected String headSub() { return "запросы принимаются и здесь, и на сайте"; }

    @Override protected void build() {
        box = scrollBox(0.06f, 0.19f, 0.88f, 0.62f);

        btn("ДОБАВИТЬ", Ui.I_PLUS, Color.rgb(120, 255, 170), Color.rgb(20, 150, 100),
                0.06f, 0.84f, 0.20f, 0.12f,
                () -> ask("Имя игрока", "", name -> act("request", name)));
        btn("ОБНОВИТЬ", Ui.I_REFRESH, Color.rgb(160, 170, 210), Color.rgb(70, 80, 130),
                0.28f, 0.84f, 0.20f, 0.12f, this::load);
        back(0.80f, 0.84f);

        load();
    }

    void load() {
        box.removeAllViews();
        if (!Store.loggedIn()) {
            card(box, "Нужен аккаунт", "войди в профиле, чтобы видеть друзей", "", -1,
                    () -> go(ProfileActivity.class));
            return;
        }
        card(box, "Загружаю…", "", "", -1, null);

        Api.async(() -> {
            JSONArray fr = Api.friends(), rq = Api.friendRequests();
            ui.post(() -> {
                box.removeAllViews();
                int reqs = 0;
                for (int i = 0; i < rq.length(); i++) {
                    JSONObject o = rq.optJSONObject(i);
                    if (o == null) continue;
                    boolean in = o.optString("to").equalsIgnoreCase(Store.user());
                    final String name = in ? o.optString("from") : o.optString("to");
                    reqs++;
                    card(box, (in ? "Запрос от " : "Отправлен → ") + name,
                            in ? "нажми, чтобы принять или отклонить" : "ждём ответа",
                            in ? "ВХОДЯЩИЙ" : "ИСХОДЯЩИЙ", -1,
                            in ? () -> answer(name) : null);
                }
                for (int i = 0; i < fr.length(); i++) {
                    JSONObject o = fr.optJSONObject(i);
                    if (o == null) continue;
                    final String name = o.optString("name");
                    card(box, name, "★ " + o.optInt("stars"), "ДРУГ", -1, () -> friendMenu(name));
                }
                if (reqs == 0 && fr.length() == 0)
                    card(box, "Пока никого", "добавь друга по имени", "", -1, null);
            });
        });
    }

    void answer(String name) {
        menu(name, new String[]{"Принять", "Отклонить", "Профиль"}, i -> {
                    if (i == 0) act("accept", name);
                    else if (i == 1) act("decline", name);
                    else go(ProfileActivity.class, "user", name);
                });
    }

    void friendMenu(String name) {
        menu(name, new String[]{"Профиль", "Его уровни", "Удалить из друзей"}, i -> {
                    if (i == 0) go(ProfileActivity.class, "user", name);
                    else if (i == 1) go(LevelListActivity.class, "mode", "user:" + name);
                    else confirm("Удалить " + name + " из друзей?", () -> act("remove", name));
                });
    }

    void act(String action, String name) {
        if (name == null || name.trim().isEmpty()) return;
        Api.async(() -> {
            boolean ok = Api.friendAction(action, name.trim());
            ui.post(() -> { toast(ok ? "Готово" : "Нет сети — встало в очередь"); load(); });
        });
    }
}
