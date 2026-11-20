package com.winnigames2024.geodash;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.content.res.Configuration;
import android.graphics.*;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.text.InputType;
import android.view.*;
import android.widget.*;

/** База всех экранов: FrameLayout + анимированный фон + заголовок + GD-кнопки. */
public abstract class GdActivity extends Activity {

    protected FrameLayout root;
    protected Ui.BgView bg;
    protected HeaderView header;
    protected final Handler ui = new Handler(Looper.getMainLooper());
    protected int W, H;

    /** Цвет фона экрана. */
    protected int bgA() { return Gd.BG_BLUE; }
    /** Цвет пола; 0 — считается по цвету фона. */
    protected int bgB() { return 0; }
    /** Номер узора фона (1..20) и пола (1..10). */
    protected int bgId() { return 1; }
    protected int groundId() { return 1; }
    protected String headTitle() { return ""; }
    protected String headSub() { return ""; }

    /** Здесь экран собирается из View внутри FrameLayout. */
    protected abstract void build();

    /**
     * Просим у телефона самую высокую герцовку с текущим разрешением.
     * Без этого многие экраны держат 60 Гц, даже когда умеют 120, и игра
     * выглядит дёрганой при том же FPS.
     */
    private void unlockHighRefresh() {
        try {
            if (android.os.Build.VERSION.SDK_INT < 23) return;
            android.view.Display d = getWindowManager().getDefaultDisplay();
            android.view.Display.Mode cur = d.getMode();
            android.view.Display.Mode best = cur;
            for (android.view.Display.Mode m : d.getSupportedModes()) {
                if (m.getPhysicalWidth() != cur.getPhysicalWidth()) continue;
                if (m.getPhysicalHeight() != cur.getPhysicalHeight()) continue;
                if (m.getRefreshRate() > best.getRefreshRate()) best = m;
            }
            if (best.getModeId() != cur.getModeId()) {
                WindowManager.LayoutParams lp = getWindow().getAttributes();
                lp.preferredDisplayModeId = best.getModeId();
                getWindow().setAttributes(lp);
            }
        } catch (Exception ignored) { }
    }

    /**
     * Картинка во весь экран: заходим под вырез камеры и прячем системные
     * шторки так, чтобы случайный свайп от края не дёргал размер окна —
     * именно от этого в игре случались рывки.
     */
    private void edgeToEdge() {
        try {
            if (android.os.Build.VERSION.SDK_INT >= 28) {
                WindowManager.LayoutParams lp = getWindow().getAttributes();
                lp.layoutInDisplayCutoutMode =
                        WindowManager.LayoutParams.LAYOUT_IN_DISPLAY_CUTOUT_MODE_SHORT_EDGES;
                getWindow().setAttributes(lp);
            }
            getWindow().addFlags(WindowManager.LayoutParams.FLAG_HARDWARE_ACCELERATED);

            if (android.os.Build.VERSION.SDK_INT >= 30) {
                android.view.WindowInsetsController ic = getWindow().getInsetsController();
                if (ic != null) {
                    ic.hide(android.view.WindowInsets.Type.systemBars());
                    ic.setSystemBarsBehavior(
                            android.view.WindowInsetsController.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE);
                }
            } else {
                getWindow().getDecorView().setSystemUiVisibility(
                        View.SYSTEM_UI_FLAG_FULLSCREEN
                                | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
                                | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY
                                | View.SYSTEM_UI_FLAG_LAYOUT_STABLE
                                | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
                                | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION);
            }
        } catch (Exception ignored) { }
    }

    /**
     * Падение на телефоне: отчёт уходит админу (виден в админке на сайте),
     * после чего приложение закрывается как обычно.
     */
    private static boolean crashHooked = false;

    private void catchCrashes() {
        if (crashHooked) return;
        crashHooked = true;
        final Thread.UncaughtExceptionHandler prev = Thread.getDefaultUncaughtExceptionHandler();
        Thread.setDefaultUncaughtExceptionHandler((t, e) -> {
            try {
                java.io.StringWriter w = new java.io.StringWriter();
                e.printStackTrace(new java.io.PrintWriter(w));
                final String text = w.toString();
                try {
                    Store.prefs.edit()
                            .putString("crash_text", text.length() > 4000 ? text.substring(0, 4000) : text)
                            .putString("crash_stage", stage)
                            .commit();      // именно commit: процесс уже умирает
                } catch (Throwable ignored) { }
                Thread send = new Thread(() -> Api.report("вылет", text, stage));
                send.start();
                send.join(2500);           // ждём недолго: отчёт важен, но не ценой зависания
            } catch (Throwable ignored) { }
            if (prev != null) prev.uncaughtException(t, e);
        });
    }

    /** Что приложение делает прямо сейчас — попадает в отчёт. */
    public static volatile String stage = "запуск";

    @Override protected void onCreate(Bundle b) {
        super.onCreate(b);
        requestWindowFeature(Window.FEATURE_NO_TITLE);
        getWindow().setFlags(WindowManager.LayoutParams.FLAG_FULLSCREEN, WindowManager.LayoutParams.FLAG_FULLSCREEN);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        unlockHighRefresh();
        edgeToEdge();
        Store.init(this);
        catchCrashes();

        root = new FrameLayout(this);
        root.setBackgroundColor(Gd.BG_BLUE);
        setContentView(root);
        rebuild();
    }

    protected void rebuild() {
        root.removeAllViews();
        bg = new Ui.BgView(this, bgA(), bgB()).ids(bgId(), groundId());
        root.addView(bg, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
        header = new HeaderView(this);
        root.addView(header, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));

        root.post(() -> {
            W = root.getWidth();
            H = root.getHeight();
            if (W == 0 || H == 0) { root.postDelayed(this::rebuild, 60); return; }
            build();
        });
    }

    @Override public void onConfigurationChanged(Configuration c) {
        super.onConfigurationChanged(c);
        rebuild();
    }

    /** Заголовок экрана. */
    public class HeaderView extends View {
        public HeaderView(Context c) { super(c); }

        @Override protected void onDraw(Canvas c) {
            String t = headTitle(), s = headSub();
            if (!t.isEmpty())
                Gd.outlined(c, t, getWidth() / 2f, getHeight() * 0.09f,
                        Math.min(46f, getWidth() * 0.045f), Gd.PAPER, Paint.Align.CENTER);
            if (!s.isEmpty())
                Gd.outlined(c, s, getWidth() / 2f, getHeight() * 0.145f,
                        Math.min(20f, getWidth() * 0.019f), Color.argb(230, 190, 210, 255), Paint.Align.CENTER);
        }
    }

    // ------------------------------------------------------------ элементы

    protected Ui.GdBtn btn(String text, int icon, int c1, int c2,
                           float x, float y, float w, float h, Runnable onClick) {
        Ui.GdBtn b = new Ui.GdBtn(this).set(text, icon, c1, c2);
        b.setOnClickListener(v -> { if (onClick != null) onClick.run(); });
        root.addView(b, Ui.lp(W, H, x, y, w, h));
        return b;
    }

    protected Ui.GdBtn btn(String text, String sub, int icon, int c1, int c2,
                           float x, float y, float w, float h, Runnable onClick) {
        Ui.GdBtn b = btn(text, icon, c1, c2, x, y, w, h, onClick);
        b.sub(sub);
        return b;
    }

    protected void back(float x, float y) {
        btn("НАЗАД", Ui.I_BACK, Color.rgb(160, 160, 190), Color.rgb(70, 70, 110),
                x, y, 0.14f, 0.11f, this::finish);
    }

    /** Прокручиваемый список карточек внутри FrameLayout. */
    protected LinearLayout scrollBox(float x, float y, float w, float h) {
        ScrollView sv = new ScrollView(this);
        LinearLayout box = new LinearLayout(this);
        box.setOrientation(LinearLayout.VERTICAL);
        sv.addView(box, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));
        root.addView(sv, Ui.lp(W, H, x, y, w, h));
        return box;
    }

    /** Карточка с мини-картинкой уровня. */
    protected Ui.CardView levelCard(LinearLayout box, Level lv, String title, String line1, String line2,
                                    int diff, Runnable onClick) {
        Ui.CardView c = card(box, title, line1, line2, diff, onClick);
        c.level = lv;
        c.invalidate();
        return c;
    }

    protected Ui.CardView card(LinearLayout box, String title, String line1, String line2,
                               int diff, Runnable onClick) {
        Ui.CardView c = new Ui.CardView(this);
        c.title = title; c.line1 = line1 == null ? "" : line1; c.line2 = line2 == null ? "" : line2;
        c.diff = diff;
        c.setOnClickListener(v -> { Music.sfx("click"); if (onClick != null) onClick.run(); });
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, (int) (H * 0.17f));
        lp.setMargins(0, (int) (H * 0.012f), 0, 0);
        box.addView(c, lp);
        return c;
    }

    protected EditText input(String hint, String value, float x, float y, float w, float h) {
        EditText e = new EditText(this);
        e.setHint(hint);
        e.setText(value == null ? "" : value);
        e.setTextColor(Gd.PAPER);
        e.setHintTextColor(Gd.DIM);
        e.setBackgroundColor(Color.rgb(20, 18, 48));
        e.setPadding(20, 10, 20, 10);
        e.setSingleLine(true);
        root.addView(e, Ui.lp(W, H, x, y, w, h));
        return e;
    }

    protected TextView text(String s, float x, float y, float w, float h, int color, float sizeSp) {
        TextView t = new TextView(this);
        t.setText(s);
        t.setTextColor(color);
        t.setTextSize(sizeSp);
        root.addView(t, Ui.lp(W, H, x, y, w, h));
        return t;
    }

    // ------------------------------------------------------------ утилиты

    protected void toast(String s) { ui.post(() -> Toast.makeText(this, s, Toast.LENGTH_SHORT).show()); }

    /** Переход без анимации. */
    protected void go(Class<?> cls) { go(cls, null, null); }

    protected void go(Class<?> cls, String key, String value) {
        Intent i = new Intent(this, cls);
        i.addFlags(Intent.FLAG_ACTIVITY_NO_ANIMATION);
        if (key != null) i.putExtra(key, value);
        startActivity(i);
        overridePendingTransition(0, 0);
    }

    @Override public void finish() {
        super.finish();
        overridePendingTransition(0, 0);
    }

    @Override protected void onPause() {
        super.onPause();
        Music.pause();          // музыка не играет, когда приложение свёрнуто
    }

    @Override protected void onResume() {
        unlockHighRefresh();
        edgeToEdge();
        super.onResume();
        Music.resume();
    }

    protected void ask(String title, String def, java.util.function.Consumer<String> cb) {
        GdDialog.input(this, title, "", def, false, cb::accept);
    }

    protected void askPassword(String title, java.util.function.Consumer<String> cb) {
        GdDialog.input(this, title, "пароль", "", true, cb::accept);
    }

    protected void confirm(String msg, Runnable yes) {
        GdDialog.confirm(this, "GEO DASH", msg, yes);
    }

    protected void menu(String title, String[] items, GdDialog.OnPick cb) {
        GdDialog.items(this, title, items, cb);
    }
}
