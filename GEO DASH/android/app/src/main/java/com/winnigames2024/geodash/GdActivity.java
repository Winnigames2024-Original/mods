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

    protected int bgA() { return Gd.DEEP2; }
    protected int bgB() { return Gd.DEEP; }
    protected String headTitle() { return ""; }
    protected String headSub() { return ""; }

    /** Здесь экран собирается из View внутри FrameLayout. */
    protected abstract void build();

    @Override protected void onCreate(Bundle b) {
        super.onCreate(b);
        requestWindowFeature(Window.FEATURE_NO_TITLE);
        getWindow().setFlags(WindowManager.LayoutParams.FLAG_FULLSCREEN, WindowManager.LayoutParams.FLAG_FULLSCREEN);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        Store.init(this);

        root = new FrameLayout(this);
        root.setBackgroundColor(Gd.DEEP);
        setContentView(root);
        rebuild();
    }

    protected void rebuild() {
        root.removeAllViews();
        bg = new Ui.BgView(this, bgA(), bgB());
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
