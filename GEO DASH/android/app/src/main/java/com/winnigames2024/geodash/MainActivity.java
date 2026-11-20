package com.winnigames2024.geodash;

import android.content.Context;
import android.graphics.*;
import android.os.Bundle;
import android.view.View;
import android.view.ViewGroup;
import android.widget.FrameLayout;

import org.json.JSONObject;

/** Главное меню: PLAY, EDITOR, CREATOR и нижний ряд кнопок. */
public class MainActivity extends GdActivity {

    static boolean started;
    String status = "Запуск…";
    JSONObject account;
    StatusView statusView;

    @Override protected String headTitle() { return "GEO DASH"; }
    @Override protected String headSub() { return "by Winnigames2024"; }

    @Override protected void onCreate(Bundle b) {
        super.onCreate(b);
        if (!started) {
            started = true;
            Api.async(() -> {
                setStatus("Готовлю встроенный саундтрек…");
                Soundtrack.ensureAll();
                ui.post(() -> Music.play(-1, 0, 0.6f, true));
                setStatus("Синхронизация…");
                Api.sync(null);
                account = Api.me();
                setStatus(Api.online ? "Готово" : "Офлайн — доступны локальные и сохранённые уровни");
                checkMaintenance();
                ui.post(() -> LicenceGate.check(MainActivity.this));
                Updater.check(MainActivity.this);
            });
        } else {
            Api.async(() -> {
                account = Api.me();
                ui.post(() -> {
                    if (statusView != null) statusView.invalidate();
                    LicenceGate.check(MainActivity.this);
                });
            });
        }
    }

    @Override protected void onResume() {
        super.onResume();
        Updater.resumeInstall(this);   // вернулись из настроек — ставим скачанное обновление
        if (Music.currentId != -1) Music.play(-1, 0, 0.6f, true);
        Api.async(() -> { Api.me(); checkMaintenance(); });
    }

    private boolean closingForMaintenance = false;

    /**
     * Сервер закрыли на обслуживание — говорим об этом и выходим: в меню всё
     * равно ничего не работает. Админа не трогаем, ему доделывать дела.
     */
    void checkMaintenance() {
        if (closingForMaintenance || Api.maintenance.isEmpty() || Store.admin()) return;
        closingForMaintenance = true;
        ui.post(() -> {
            Music.stop();
            GdDialog.message(this, "СЕРВЕР ОБНОВЛЯЕТСЯ",
                    Api.maintenance + "\n\nИгра закроется. Зайдите через пару минут.",
                    new String[]{"ПОНЯТНО"}, i -> finishAffinity());
        });
    }

    void setStatus(String s) {
        status = s;
        ui.post(() -> { if (statusView != null) statusView.invalidate(); });
    }

    @Override protected void build() {
        // PLAY — большая круглая кнопка с треугольником
        PlayView play = new PlayView(this);
        play.setOnClickListener(v -> { Music.sfx("click"); go(PlayActivity.class); });
        float ps = 0.30f;
        root.addView(play, Ui.lp(W, H, 0.5f - ps * H / W / 2, 0.34f, ps * H / W, ps));

        btn("EDITOR", "мои уровни", Ui.I_EDIT, Color.rgb(255, 180, 70), Color.rgb(175, 95, 10),
                0.05f, 0.36f, 0.22f, 0.13f, () -> go(EditorActivity.class));
        btn("CREATOR", "поиск и daily", Ui.I_ONLINE, Color.rgb(90, 170, 255), Color.rgb(25, 80, 180),
                0.05f, 0.52f, 0.22f, 0.13f, () -> go(CreatorActivity.class));
        btn("SAVED", "офлайн-уровни", Ui.I_DOWNLOAD, Color.rgb(120, 230, 160), Color.rgb(30, 130, 80),
                0.73f, 0.36f, 0.22f, 0.13f, () -> go(LevelListActivity.class, "mode", "saved"));
        btn("DAILY", "день и неделя", Ui.I_DAILY, Color.rgb(210, 140, 255), Color.rgb(110, 40, 180),
                0.73f, 0.52f, 0.22f, 0.13f, () -> go(DailyActivity.class));

        btn("", Ui.I_PROFILE, Color.rgb(120, 190, 255), Color.rgb(30, 90, 190),
                0.30f, 0.78f, 0.09f, 0.16f, () -> go(ProfileActivity.class)).caption("ПРОФИЛЬ");
        btn("", Ui.I_FRIENDS, Color.rgb(255, 150, 200), Color.rgb(160, 50, 110),
                0.40f, 0.78f, 0.09f, 0.16f, () -> go(FriendsActivity.class)).caption("ДРУЗЬЯ");
        btn("", Ui.I_SETTINGS, Color.rgb(160, 170, 200), Color.rgb(70, 80, 120),
                0.50f, 0.78f, 0.09f, 0.16f, () -> go(SettingsActivity.class)).caption("НАСТРОЙКИ");
        btn("", Ui.I_REFRESH, Color.rgb(120, 220, 180), Color.rgb(30, 120, 100),
                0.60f, 0.78f, 0.09f, 0.16f, () -> Api.async(() -> {
                    setStatus("Синхронизация…");
                    Api.sync(null);
                    account = Api.me();
                    setStatus(Api.online ? "Синхронизировано" : "Нет сети");
                })).caption("СИНХРО");
        if (Store.admin())
            btn("", Ui.I_ADMIN, Color.rgb(255, 110, 110), Color.rgb(150, 30, 40),
                    0.70f, 0.78f, 0.09f, 0.16f, () -> go(AdminActivity.class)).caption("АДМИНКА");

        statusView = new StatusView(this);
        root.addView(statusView, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
    }

    class PlayView extends View {
        double t;
        PlayView(Context c) {
            super(c);
            setClickable(true);
            postDelayed(new Runnable() {
                @Override public void run() { t += 0.033; invalidate(); postDelayed(this, 33); }
            }, 33);
        }
        @Override protected void onDraw(Canvas c) {
            Gd.playButton(c, new RectF(0, 0, getWidth(), getHeight()), t);
        }
    }

    /** Карточка аккаунта и строка статуса. */
    class StatusView extends View {
        StatusView(Context c) { super(c); }

        @Override protected void onDraw(Canvas c) {
            int w = getWidth(), h = getHeight();
            RectF card = new RectF(w - w * 0.27f, h * 0.03f, w - w * 0.02f, h * 0.03f + h * 0.17f);
            Gd.glass(c, card, 16f);
            String name = Store.loggedIn() ? Store.user() : "Гость";
            Gd.outlined(c, name, card.left + 16, card.top + card.height() * 0.4f,
                    Math.min(26f, w * 0.022f), Gd.PAPER, Paint.Align.LEFT);
            int stars = account != null ? account.optInt("stars") : 0;
            int coins = Math.max(account != null ? account.optInt("coins") : 0, Store.coinsTotal());
            int demons = Math.max(account != null ? account.optInt("demons") : 0, Store.demonsBeaten());
            Gd.outlined(c, "★ " + stars + "   ◉ " + coins + "   ☠ " + demons,
                    card.left + 16, card.top + card.height() * 0.78f,
                    Math.min(20f, w * 0.017f), Gd.GOLD, Paint.Align.LEFT);

            c.drawRect(0, h - h * 0.06f, w, h, Gd.fill(Color.argb(170, 8, 6, 22)));
            // версия игры — мелким текстом в самом углу слева снизу
            float vs = Math.min(15f, w * 0.013f);
            String ver = "v" + BuildConfig.VERSION_NAME;
            Gd.outlined(c, ver, 12, h - h * 0.018f, vs, Color.argb(225, 200, 215, 250), Paint.Align.LEFT);
            float off = Gd.textWidth(ver, vs) + 22;
            Gd.outlined(c, status, off, h - h * 0.018f, Math.min(18f, w * 0.016f), Gd.PAPER, Paint.Align.LEFT);
            Gd.outlined(c, Api.online ? "● ОНЛАЙН" : "● ОФЛАЙН", w - 14, h - h * 0.018f,
                    Math.min(18f, w * 0.016f), Api.online ? Gd.LIME : Gd.GOLD, Paint.Align.RIGHT);
        }
    }
}
