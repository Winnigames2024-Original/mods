package com.winnigames2024.geodash;

import android.graphics.*;
import android.text.InputType;
import android.view.View;
import android.widget.EditText;

import org.json.JSONObject;

/**
 * Вход в игру: авто-вход по сохранённому входу, иначе вход или регистрация.
 * Без аккаунта дальше не пускаем — кроме случая, когда сети нет, а аккаунт уже был.
 */
public class AuthActivity extends GdActivity {

    EditText name, pass;
    Ui.GdBtn bLogin, bReg, bRetry, bOffline;
    MsgView msg;
    String text = "Проверяю вход…";
    boolean busy = true;

    @Override protected int bgA() { return Gd.BG_BLUE; }
    @Override protected int bgId() { return 1; }
    @Override protected int groundId() { return 1; }
    @Override protected String headTitle() { return "GEO DASH"; }
    @Override protected String headSub() { return "вход в аккаунт"; }

    @Override protected void build() {
        msg = new MsgView(this);
        root.addView(msg, Ui.lp(W, H, 0.05f, 0.17f, 0.90f, 0.70f));

        name = input("имя", Store.user(), 0.34f, 0.33f, 0.32f, 0.10f);
        pass = input("пароль", "", 0.34f, 0.45f, 0.32f, 0.10f);
        pass.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_VARIATION_PASSWORD);

        bLogin = btn("ВОЙТИ", Ui.I_CHECK, Color.rgb(120, 255, 170), Color.rgb(20, 150, 100),
                0.34f, 0.58f, 0.155f, 0.11f, () -> go(false));
        bReg = btn("РЕГИСТРАЦИЯ", Ui.I_PLUS, Color.rgb(120, 200, 255), Color.rgb(25, 95, 190),
                0.505f, 0.58f, 0.155f, 0.11f, () -> go(true));
        bRetry = btn("ПОВТОРИТЬ", Ui.I_REFRESH, Color.rgb(170, 175, 215), Color.rgb(70, 76, 130),
                0.34f, 0.71f, 0.155f, 0.09f, this::auto);
        bOffline = btn("ИГРАТЬ ОФЛАЙН", Ui.I_DOWNLOAD, Color.rgb(255, 200, 90), Color.rgb(170, 115, 10),
                0.505f, 0.71f, 0.155f, 0.09f, this::enter);
        bOffline.setVisibility(View.GONE);

        auto();
    }

    void say(String s, boolean working) {
        text = s;
        busy = working;
        ui.post(() -> {
            if (msg != null) msg.invalidate();
            if (bLogin != null) bLogin.setEnabled(!working);
            if (bReg != null) bReg.setEnabled(!working);
        });
    }

    void enter() {
        ui.post(() -> {
            // лицензию спросит диалогом сам главный экран
            String k = getIntent() != null ? getIntent().getStringExtra("gd_key") : null;
            go(MainActivity.class, k == null ? null : "gd_key", k);
            finish();
        });
    }

    /** Авто-вход: сохранённый вход проверяем на сервере, без сети — пускаем по кэшу. */
    void auto() {
        if (bOffline != null) bOffline.setVisibility(View.GONE);
        if (!Store.loggedIn()) {
            say("Войди в аккаунт или создай новый — без него в игру не пустим", false);
            return;
        }
        say("Захожу как " + Store.user() + "…", true);
        Api.async(() -> {
            JSONObject me = Api.me();
            if (me != null && !me.optString("name").isEmpty()) {
                Store.setAccount(Store.token(), me.optString("name"), me.optBoolean("admin", false));
                enter();
                return;
            }
            if (!Api.online) { enter(); return; }
            Store.setAccount("", Store.user(), false);
            say("Сохранённый вход больше не подходит — войди заново", false);
        });
    }

    void go(final boolean register) {
        final String n = name.getText().toString().trim(), p = pass.getText().toString();
        if (n.length() < 3 || p.length() < 3) { say("Имя и пароль — от 3 символов", false); return; }
        say(register ? "Регистрирую…" : "Вхожу…", true);

        Api.async(() -> {
            JSONObject r = register ? Api.register(n, p) : Api.login(n, p);
            if (r != null && r.optBoolean("ok")) {
                JSONObject acc = r.optJSONObject("account");
                Store.setAccount(r.optString("token"),
                        acc != null ? acc.optString("name", n) : n,
                        acc != null && acc.optBoolean("admin"));
                enter();
                return;
            }
            String err = r != null ? r.optString("error", "Ошибка") : "Сервер недоступен";
            if (!Api.online) {
                err = "Сервер недоступен. Проверь интернет и нажми «ПОВТОРИТЬ».";
                if (Store.loggedIn()) ui.post(() -> bOffline.setVisibility(View.VISIBLE));
            }
            say(err, false);
        });
    }

    @Override public void onBackPressed() { finishAffinity(); }

    class MsgView extends View {
        MsgView(android.content.Context c) { super(c); }

        @Override protected void onDraw(Canvas c) {
            RectF r = new RectF(getWidth() * 0.27f, getHeight() * 0.14f,
                    getWidth() * 0.73f, getHeight() * 0.82f);
            Gd.glass(c, r, 22f);
            float fs = Math.min(getWidth() * 0.016f, 22f);
            Gd.outlined(c, "вход запоминается — дальше игра заходит сама",
                    r.centerX(), r.top + fs * 2.1f, fs, Gd.GOLD, Paint.Align.CENTER);
            Gd.outlined(c, "Имя", r.left + 18, getHeight() * 0.245f, fs, Gd.PAPER, Paint.Align.LEFT);
            Gd.outlined(c, "Пароль", r.left + 18, getHeight() * 0.415f, fs, Gd.PAPER, Paint.Align.LEFT);
            if (text != null && !text.isEmpty())
                Gd.outlined(c, text, getWidth() / 2f, getHeight() * 0.95f, fs,
                        busy ? Gd.PAPER : Gd.GOLD, Paint.Align.CENTER);
        }
    }
}
