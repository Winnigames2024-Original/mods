package com.winnigames2024.geodash;

import android.content.Intent;
import android.graphics.*;
import android.net.Uri;
import android.text.Editable;
import android.text.TextWatcher;
import android.view.Gravity;
import android.view.View;
import android.widget.EditText;

/**
 * Лицензия. Без ключа в игру не пускаем: ключ берётся в аккаунте на сайте,
 * у админа или приходит сразу после покупки. Один ключ — один аккаунт,
 * навсегда и на всех устройствах игрока.
 */
public class LicenceActivity extends GdActivity {

    EditText key;
    Ui.GdBtn bGo, bSite, bAgain;
    MsgView msg;
    String text = "Проверяю лицензию…";
    boolean busy = true, noSetup = false, offline = false;

    @Override protected int bgA() { return Gd.BG_BLUE; }
    @Override protected int bgId() { return 1; }
    @Override protected int groundId() { return 1; }
    @Override protected String headTitle() { return "ЛИЦЕНЗИОННЫЙ КЛЮЧ"; }
    @Override protected String headSub() { return "аккаунт " + Store.user(); }

    @Override protected void build() {
        msg = new MsgView(this);
        root.addView(msg, Ui.lp(W, H, 0.04f, 0.16f, 0.92f, 0.72f));

        key = input("GD-XXXX-XXXX-XXXX-XXXX", "", 0.28f, 0.37f, 0.44f, 0.11f);
        key.setGravity(Gravity.CENTER);
        key.setAllCaps(true);
        key.setTextSize(20f);
        // ключ из установщика — подставляем сами
        String fromSetup = getIntent() != null ? getIntent().getStringExtra("gd_key") : null;
        if (fromSetup != null && !fromSetup.isEmpty()) key.setText(Lic.pretty(fromSetup));

        key.addTextChangedListener(new TextWatcher() {
            boolean guard;
            @Override public void beforeTextChanged(CharSequence s, int a, int b, int c) { }
            @Override public void onTextChanged(CharSequence s, int a, int b, int c) { }
            @Override public void afterTextChanged(Editable e) {
                if (guard) return;
                String pretty = Lic.pretty(e.toString());
                if (!pretty.equals(e.toString())) {
                    guard = true;
                    key.setText(pretty);
                    key.setSelection(pretty.length());
                    guard = false;
                }
                if (msg != null) msg.invalidate();
            }
        });

        bGo = btn("АКТИВИРОВАТЬ", Ui.I_CHECK, Color.rgb(120, 255, 170), Color.rgb(20, 150, 100),
                0.28f, 0.51f, 0.21f, 0.11f, this::activate);
        bSite = btn("ПОЛУЧИТЬ КЛЮЧ", Ui.I_DOWNLOAD, Color.rgb(255, 205, 90), Color.rgb(175, 120, 10),
                0.51f, 0.51f, 0.21f, 0.11f, () -> open(Store.site() + "/account/"));
        bAgain = btn("ПРОВЕРИТЬ СНОВА", Ui.I_REFRESH, Color.rgb(170, 175, 215), Color.rgb(70, 76, 130),
                0.28f, 0.64f, 0.21f, 0.09f, this::check);
        btn("УСТАНОВЩИК", Ui.I_DOWNLOAD, Color.rgb(150, 190, 255), Color.rgb(45, 80, 170),
                0.51f, 0.64f, 0.21f, 0.09f, () -> open(Store.site() + "/download/"));

        btn("ВЫХОД", Ui.I_CROSS, Color.rgb(220, 120, 120), Color.rgb(140, 40, 50),
                0.03f, 0.86f, 0.14f, 0.1f, this::finishAffinity);
        btn("СМЕНИТЬ АККАУНТ", Ui.I_PROFILE, Color.rgb(170, 175, 215), Color.rgb(70, 76, 130),
                0.80f, 0.86f, 0.17f, 0.1f, () -> {
                    Lic.forget();
                    Store.setAccount("", Store.user(), false);
                    go(AuthActivity.class);
                    finish();
                });

        check();
    }

    void open(String url) {
        try { startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse(url))); } catch (Exception ignored) { }
    }

    void say(String s, boolean working) {
        text = s;
        busy = working;
        ui.post(() -> {
            if (msg != null) msg.invalidate();
            if (bGo != null) bGo.setEnabled(!working);
            if (bAgain != null) bAgain.setEnabled(!working);
        });
    }

    void enter() {
        ui.post(() -> {
            go(MainActivity.class);
            finish();
        });
    }

    void check() {
        say("Проверяю лицензию…", true);
        noSetup = !Lic.installedProperly() && !Store.admin();
        Api.async(() -> {
            boolean ok = Lic.check();
            if (!ok && Lic.online) {
                // ключ, переданный установщиком, пробуем молча
                String k = getIntent() != null ? getIntent().getStringExtra("gd_key") : null;
                if (k != null && Lic.wellFormed(k) && Lic.activate(k) == null) ok = true;
            }
            // ключ проверить нечем — зовём включить интернет
            if (!ok && !Lic.online) {
                offline = true;
                say(Lic.reasonText(), false);
                return;
            }
            offline = false;
            if (ok && !noSetup) { enter(); return; }
            if (noSetup) {
                say(ok ? "Лицензия есть, но игра поставлена не установщиком"
                       : Lic.reasonText(), false);
                return;
            }
            say(Lic.reasonText(), false);
        });
    }

    void activate() {
        if (offline) { say("Пока нет интернета, ключ проверить нечем — включите сеть", false); return; }
        if (noSetup) { say("Сначала поставьте игру установщиком — кнопка «УСТАНОВЩИК»", false); return; }
        final String k = key.getText().toString();
        if (!Lic.wellFormed(k)) { say("Ключ набран не полностью: GD- и 16 знаков", false); return; }

        say("Активирую ключ…", true);
        Api.async(() -> {
            String err = Lic.activate(k);
            if (err == null) {
                say("Готово! Ключ закреплён за аккаунтом " + Store.user(), true);
                try { Thread.sleep(600); } catch (InterruptedException ignored) { }
                enter();
                return;
            }
            say(err, false);
        });
    }

    @Override public void onBackPressed() { finishAffinity(); }

    class MsgView extends View {
        MsgView(android.content.Context c) { super(c); }

        @Override protected void onDraw(Canvas c) {
            RectF r = new RectF(getWidth() * 0.24f, getHeight() * 0.18f,
                    getWidth() * 0.76f, getHeight() * 0.74f);
            Gd.glass(c, r, 22f);
            float fs = Math.min(getWidth() * 0.016f, 22f);

            Gd.outlined(c, "один ключ — один аккаунт, навсегда и на всех ваших устройствах",
                    r.centerX(), r.top + fs * 2.0f, fs, Gd.GOLD, Paint.Align.CENTER);
            Gd.outlined(c, "ключ есть в аккаунте на сайте, его может выдать Winnigames2024,",
                    r.centerX(), r.top + fs * 3.6f, fs * 0.9f, Gd.PAPER, Paint.Align.CENTER);
            Gd.outlined(c, "и он приходит сразу после покупки",
                    r.centerX(), r.top + fs * 4.9f, fs * 0.9f, Gd.PAPER, Paint.Align.CENTER);

            if (key != null && Lic.wellFormed(key.getText().toString()))
                Gd.outlined(c, "ключ набран верно", r.centerX(), getHeight() * 0.66f, fs * 0.9f,
                        Gd.LIME, Paint.Align.CENTER);

            if (offline) {
                RectF w = new RectF(getWidth() * 0.12f, getHeight() * 0.78f,
                        getWidth() * 0.88f, getHeight() * 0.95f);
                Gd.plate(c, w, Color.rgb(40, 70, 120), Color.rgb(18, 34, 70), 14f);
                Gd.outlined(c, "Включите интернет и нажмите «ПРОВЕРИТЬ СНОВА»",
                        w.centerX(), w.centerY() + fs * 0.4f, fs * 0.95f, Gd.GOLD, Paint.Align.CENTER);
            } else if (noSetup) {
                RectF w = new RectF(getWidth() * 0.12f, getHeight() * 0.78f,
                        getWidth() * 0.88f, getHeight() * 0.95f);
                Gd.plate(c, w, Color.rgb(120, 40, 50), Color.rgb(70, 18, 28), 14f);
                Gd.outlined(c, "Игра поставлена не установщиком — скачайте его с сайта",
                        w.centerX(), w.centerY() + fs * 0.4f, fs * 0.95f, Gd.GOLD, Paint.Align.CENTER);
            }

            if (text != null && !text.isEmpty())
                Gd.outlined(c, text, getWidth() / 2f, getHeight() * 0.99f, fs,
                        busy ? Gd.PAPER : Gd.GOLD, Paint.Align.CENTER);
        }
    }
}
