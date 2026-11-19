package com.winnigames2024.geodash;

import android.graphics.Color;
import android.text.InputType;
import android.view.ViewGroup;
import android.widget.EditText;
import android.widget.LinearLayout;

import org.json.JSONObject;

/** Профиль игрока и вход в аккаунт. */
public class ProfileActivity extends GdActivity {

    String who;
    JSONObject acc;
    LinearLayout box;

    @Override protected int bgA() { return Color.rgb(45, 115, 210); }
    @Override protected int bgId() { return 8; }
    @Override protected int groundId() { return 3; }
    @Override protected String headTitle() { return (who == null || who.isEmpty() ? "АККАУНТ" : who.toUpperCase()); }
    @Override protected String headSub() { return Api.online ? "" : "офлайн — показаны последние данные"; }

    @Override protected void build() {
        who = getIntent().getStringExtra("user");
        if (who == null || who.isEmpty()) who = Store.user();

        box = scrollBox(0.06f, 0.19f, 0.88f, 0.60f);

        boolean self = who.isEmpty() || who.equalsIgnoreCase(Store.user());
        if (self) {
            btn(Store.loggedIn() ? "СМЕНИТЬ АККАУНТ" : "ВОЙТИ", Ui.I_PROFILE,
                    Color.rgb(120, 190, 255), Color.rgb(30, 90, 190),
                    0.06f, 0.82f, 0.22f, 0.12f, this::login);
            if (Store.loggedIn())
                btn("ВЫЙТИ", Ui.I_CROSS, Color.rgb(230, 110, 110), Color.rgb(140, 40, 50),
                        0.30f, 0.82f, 0.18f, 0.12f, () -> {
                            Store.setAccount("", "", false);
                            toast("Вышли из аккаунта");
                            rebuild();
                        });
        } else {
            btn("В ДРУЗЬЯ", Ui.I_FRIENDS, Color.rgb(255, 150, 200), Color.rgb(160, 50, 110),
                    0.06f, 0.82f, 0.22f, 0.12f, () -> Api.async(() -> {
                        boolean ok = Api.friendAction("request", who);
                        toast(ok ? "Запрос отправлен" : "Нет сети — встало в очередь");
                    }));
            btn("ЕГО УРОВНИ", Ui.I_ONLINE, Color.rgb(120, 220, 255), Color.rgb(20, 110, 180),
                    0.30f, 0.82f, 0.22f, 0.12f, () -> go(LevelListActivity.class, "mode", "user:" + who));
        }
        back(0.80f, 0.82f);
        load();
    }

    void load() {
        box.removeAllViews();
        card(box, "Загружаю…", "", "", -1, null);
        final String target = who;
        Api.async(() -> {
            JSONObject a = (target.isEmpty() || target.equalsIgnoreCase(Store.user()))
                    ? Api.me() : Api.user(target);
            acc = a;
            ui.post(() -> {
                box.removeAllViews();
                if (a != null) {
                    card(box, a.optString("name"),
                            "★ звёзды: " + a.optInt("stars") + "   монеты: " + a.optInt("coins"),
                            "", -1, null);
                    card(box, "Демоны: " + a.optInt("demons"),
                            "уровней: " + a.optInt("levels") + "   друзей: " + a.optInt("friends")
                                    + "   попыток: " + a.optInt("attempts"),
                            a.optBoolean("admin") ? "админ" : "", -1, null);
                } else {
                    card(box, Store.loggedIn() ? "Нет связи с сервером" : "Ты не вошёл в аккаунт",
                            "локальная статистика ниже работает всегда", "", -1, null);
                }
                card(box, "Локально", "монет: " + Store.coinsTotal()
                        + "   демонов пройдено: " + Store.demonsBeaten()
                        + "   своих уровней: " + Store.myLevels().size(), "", -1, null);
                card(box, "Папка данных", Store.assets().getAbsolutePath(), "", -1, null);
                card(box, "Сервер", Store.SERVER_LABEL
                        + (Api.online ? "   (онлайн)" : "   (офлайн)"), "", -1, null);
            });
        });
    }

    void login() {
        final EditText[] fields = new EditText[2];
        final boolean[] register = {false};

        LinearLayout content = GdDialog.custom(this, "ВХОД В GEO DASH", "ВОЙТИ",
                () -> auth(fields[0].getText().toString(), fields[1].getText().toString(), register[0]));

        fields[0] = field(content, "имя игрока", Store.user(), false);
        fields[1] = field(content, "пароль", "", true);

        Ui.GdBtn reg = new Ui.GdBtn(this).set("РЕГИСТРАЦИЯ", Ui.I_PLUS,
                Color.rgb(120, 190, 255), Color.rgb(30, 90, 190));
        reg.setOnClickListener(v -> {
            register[0] = true;
            auth(fields[0].getText().toString(), fields[1].getText().toString(), true);
        });
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, (int) (H * 0.13f));
        lp.setMargins(0, 16, 0, 0);
        content.addView(reg, lp);
    }

    EditText field(LinearLayout box, String hint, String value, boolean password) {
        EditText e = new EditText(this);
        e.setHint(hint);
        e.setText(value);
        e.setTextColor(Gd.PAPER);
        e.setHintTextColor(Gd.DIM);
        e.setBackgroundColor(Color.rgb(20, 18, 48));
        e.setPadding(20, 14, 20, 14);
        e.setSingleLine(true);
        if (password) e.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_VARIATION_PASSWORD);
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        lp.setMargins(0, 12, 0, 0);
        box.addView(e, lp);
        return e;
    }

    void auth(String n, String p, boolean reg) {
        Api.async(() -> {
            JSONObject r = reg ? Api.register(n, p) : Api.login(n, p);
            ui.post(() -> {
                if (r != null && r.optBoolean("ok")) {
                    JSONObject a = r.optJSONObject("account");
                    Store.setAccount(r.optString("token"), a != null ? a.optString("name") : n,
                            a != null && a.optBoolean("admin"));
                    toast("Привет, " + Store.user() + "!");
                    who = Store.user();
                    rebuild();
                } else toast(r != null ? r.optString("error", "Не вышло")
                        : (Api.online ? "Ошибка" : "Нет сети — но играть офлайн можно"));
            });
        });
    }
}
