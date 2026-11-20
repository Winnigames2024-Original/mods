package com.winnigames2024.geodash;

import android.app.Activity;
import android.content.Context;
import android.graphics.*;
import android.text.InputType;
import android.view.*;
import android.widget.*;

/** Свои диалоги вместо системных AlertDialog: плита с обводкой и свои кнопки. */
public class GdDialog {

    public interface OnPick { void pick(int index); }
    public interface OnText { void text(String value); }

    /** Плита-подложка диалога. */
    static class Plate extends View {
        Plate(Context c) { super(c); }
        @Override protected void onDraw(Canvas c) {
            RectF r = new RectF(6, 6, getWidth() - 6, getHeight() - 6);
            c.drawRoundRect(new RectF(r.left + 3, r.top + 6, r.right + 3, r.bottom + 6), 22, 22,
                    Gd.fill(Color.argb(120, 0, 0, 0)));
            Gd.plate(c, r, Color.rgb(52, 54, 108), Color.rgb(24, 22, 58), 22f);
        }
    }

    static ViewGroup rootOf(Activity a) {
        return (ViewGroup) a.getWindow().getDecorView().findViewById(android.R.id.content);
    }

    /** Общий каркас: затемнение + плита + содержимое. */
    static FrameLayout open(Activity a, String title, String message) {
        ViewGroup root = rootOf(a);

        FrameLayout overlay = new FrameLayout(a);
        overlay.setBackgroundColor(Color.argb(190, 6, 5, 18));
        overlay.setClickable(true);
        root.addView(overlay, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));

        int w = root.getWidth() > 0 ? root.getWidth() : 1280;
        int dw = (int) (w * 0.56f);

        FrameLayout card = new FrameLayout(a);
        card.addView(new Plate(a), new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));

        LinearLayout box = new LinearLayout(a);
        box.setOrientation(LinearLayout.VERTICAL);
        box.setPadding(36, 28, 36, 26);
        card.addView(box, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));

        TextView t = new TextView(a);
        t.setText(title);
        t.setTextColor(Gd.PAPER);
        t.setTypeface(Typeface.DEFAULT_BOLD);
        t.setTextSize(20);
        t.setShadowLayer(4, 2, 2, Gd.INK);
        t.setGravity(Gravity.CENTER);
        box.addView(t);

        if (message != null && !message.isEmpty()) {
            TextView m = new TextView(a);
            m.setText(message);
            m.setTextColor(Color.argb(235, 220, 230, 255));
            m.setTextSize(15);
            m.setGravity(Gravity.CENTER);
            m.setPadding(0, 16, 0, 6);
            box.addView(m);
        }

        FrameLayout.LayoutParams lp = new FrameLayout.LayoutParams(dw,
                ViewGroup.LayoutParams.WRAP_CONTENT, Gravity.CENTER);
        overlay.addView(card, lp);
        overlay.setTag(box);
        return overlay;
    }

    static LinearLayout boxOf(FrameLayout overlay) { return (LinearLayout) overlay.getTag(); }

    static void close(Activity a, FrameLayout overlay) {
        ViewGroup root = rootOf(a);
        root.removeView(overlay);
    }

    static Ui.GdBtn button(Activity a, LinearLayout parent, String text, int icon, int c1, int c2,
                           int height, Runnable onClick) {
        Ui.GdBtn b = new Ui.GdBtn(a).set(text, icon, c1, c2);
        b.setOnClickListener(v -> onClick.run());
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, height);
        lp.setMargins(0, 10, 0, 0);
        parent.addView(b, lp);
        return b;
    }

    // ------------------------------------------------------------ окна

    /** Окно с полосой загрузки — для обновления. */
    public static class Progress {
        final Activity a;
        final FrameLayout overlay;
        final TextView text;
        final BarView bar;
        boolean closed;

        Progress(Activity a, FrameLayout overlay, TextView text, BarView bar) {
            this.a = a; this.overlay = overlay; this.text = text; this.bar = bar;
        }

        /** k — доля от 0 до 1. */
        public void update(final double k, final String msg) {
            a.runOnUiThread(() -> {
                if (closed) return;
                bar.value = (float) Math.max(0, Math.min(1, k));
                bar.invalidate();
                if (msg != null) text.setText(msg);
            });
        }

        public void close() {
            a.runOnUiThread(() -> {
                if (closed) return;
                closed = true;
                GdDialog.close(a, overlay);
            });
        }
    }

    static class BarView extends View {
        float value;
        BarView(Context c) { super(c); }

        @Override protected void onDraw(Canvas c) {
            RectF r = new RectF(0, 0, getWidth(), getHeight());
            float rad = r.height() / 2;
            c.drawRoundRect(r, rad, rad, Gd.fill(Color.argb(190, 10, 12, 26)));
            float w = (r.width() - 6) * value;
            if (w > 2) {
                RectF f = new RectF(r.left + 3, r.top + 3, r.left + 3 + w, r.bottom - 3);
                Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
                p.setShader(new LinearGradient(0, f.top, 0, f.bottom,
                        Color.rgb(140, 255, 170), Color.rgb(20, 170, 110), Shader.TileMode.CLAMP));
                c.drawRoundRect(f, (r.height() - 6) / 2, (r.height() - 6) / 2, p);
            }
            c.drawRoundRect(r, rad, rad, Gd.stroke(Gd.INK, 3f));
            Gd.outlined(c, (int) (value * 100) + "%", r.centerX(), r.centerY() + r.height() * 0.22f,
                    r.height() * 0.6f, Gd.PAPER, Paint.Align.CENTER);
        }
    }

    /** Показать окно с полосой загрузки. */
    public static Progress progress(Activity a, String title, String message) {
        FrameLayout overlay = open(a, title, "");
        LinearLayout box = boxOf(overlay);

        TextView t = new TextView(a);
        t.setText(message);
        t.setTextColor(Color.argb(235, 220, 230, 255));
        t.setTextSize(15);
        t.setGravity(Gravity.CENTER);
        t.setPadding(0, 14, 0, 10);
        box.addView(t);

        BarView bar = new BarView(a);
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, 44);
        lp.setMargins(0, 8, 0, 6);
        box.addView(bar, lp);

        return new Progress(a, overlay, t, bar);
    }

    /** Сообщение с кнопками. */
    public static void message(Activity a, String title, String msg, String[] buttons, OnPick cb) {
        FrameLayout o = open(a, title, msg);
        LinearLayout box = boxOf(o);
        LinearLayout row = new LinearLayout(a);
        row.setOrientation(LinearLayout.HORIZONTAL);
        box.addView(row, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));

        int h = (int) (rootOf(a).getHeight() * 0.13f);
        for (int i = 0; i < buttons.length; i++) {
            final int idx = i;
            boolean first = i == 0;
            Ui.GdBtn b = new Ui.GdBtn(a).set(buttons[i], Ui.I_NONE,
                    first ? Color.rgb(120, 255, 170) : Color.rgb(170, 170, 200),
                    first ? Color.rgb(20, 150, 100) : Color.rgb(70, 70, 110));
            b.setOnClickListener(v -> { close(a, o); if (cb != null) cb.pick(idx); });
            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(0, h, 1f);
            lp.setMargins(6, 16, 6, 0);
            row.addView(b, lp);
        }
    }

    public static void info(Activity a, String title, String msg) {
        message(a, title, msg, new String[]{"OK"}, null);
    }

    public static void confirm(Activity a, String title, String msg, Runnable yes) {
        message(a, title, msg, new String[]{"ДА", "НЕТ"}, i -> { if (i == 0) yes.run(); });
    }

    /** Ввод текста. */
    public static void input(Activity a, String title, String hint, String def,
                             boolean password, OnText cb) {
        FrameLayout o = open(a, title, null);
        LinearLayout box = boxOf(o);

        EditText e = new EditText(a);
        e.setHint(hint);
        e.setText(def == null ? "" : def);
        e.setTextColor(Gd.PAPER);
        e.setHintTextColor(Gd.DIM);
        e.setBackgroundColor(Color.rgb(20, 18, 48));
        e.setPadding(20, 14, 20, 14);
        e.setSingleLine(true);
        if (password) e.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_VARIATION_PASSWORD);
        LinearLayout.LayoutParams elp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        elp.setMargins(0, 18, 0, 0);
        box.addView(e, elp);

        LinearLayout row = new LinearLayout(a);
        row.setOrientation(LinearLayout.HORIZONTAL);
        box.addView(row);
        int h = (int) (rootOf(a).getHeight() * 0.13f);

        Ui.GdBtn ok = new Ui.GdBtn(a).set("OK", Ui.I_CHECK, Color.rgb(120, 255, 170), Color.rgb(20, 150, 100));
        ok.setOnClickListener(v -> { close(a, o); if (cb != null) cb.text(e.getText().toString().trim()); });
        Ui.GdBtn no = new Ui.GdBtn(a).set("ОТМЕНА", Ui.I_CROSS, Color.rgb(170, 170, 200), Color.rgb(70, 70, 110));
        no.setOnClickListener(v -> close(a, o));

        LinearLayout.LayoutParams lp1 = new LinearLayout.LayoutParams(0, h, 1f);
        lp1.setMargins(6, 16, 6, 0);
        row.addView(ok, lp1);
        LinearLayout.LayoutParams lp2 = new LinearLayout.LayoutParams(0, h, 1f);
        lp2.setMargins(6, 16, 6, 0);
        row.addView(no, lp2);
    }

    /**
     * Ввод текста со своими подписями кнопок и действием на отмену.
     * Нужен там, где отказ что-то значит — например в запросе лицензионного ключа.
     */
    public static void inputOr(Activity a, String title, String message, String hint, String def,
                               String okText, String cancelText, OnText cb, Runnable onCancel) {
        inputOr(a, title, message, hint, def, okText, null, cancelText, cb, null, onCancel);
    }

    /** То же самое, но с третьей кнопкой посередине (например «войти в аккаунт»). */
    public static void inputOr(Activity a, String title, String message, String hint, String def,
                               String okText, String altText, String cancelText,
                               OnText cb, Runnable onAlt, Runnable onCancel) {
        FrameLayout o = open(a, title, message);
        LinearLayout box = boxOf(o);

        EditText e = new EditText(a);
        e.setHint(hint);
        e.setText(def == null ? "" : def);
        e.setTextColor(Gd.PAPER);
        e.setHintTextColor(Gd.DIM);
        e.setBackgroundColor(Color.rgb(20, 18, 48));
        e.setPadding(20, 14, 20, 14);
        e.setSingleLine(true);
        e.setAllCaps(true);
        e.setGravity(Gravity.CENTER);
        e.setTextSize(19);
        LinearLayout.LayoutParams elp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        elp.setMargins(0, 18, 0, 0);
        box.addView(e, elp);

        LinearLayout row = new LinearLayout(a);
        row.setOrientation(LinearLayout.HORIZONTAL);
        box.addView(row);
        int h = (int) (rootOf(a).getHeight() * 0.13f);

        Ui.GdBtn ok = new Ui.GdBtn(a).set(okText, Ui.I_CHECK, Color.rgb(120, 255, 170), Color.rgb(20, 150, 100));
        ok.setOnClickListener(v -> { close(a, o); if (cb != null) cb.text(e.getText().toString().trim()); });
        Ui.GdBtn no = new Ui.GdBtn(a).set(cancelText, Ui.I_CROSS, Color.rgb(220, 120, 120), Color.rgb(140, 40, 50));
        no.setOnClickListener(v -> { close(a, o); if (onCancel != null) onCancel.run(); });

        LinearLayout.LayoutParams lp1 = new LinearLayout.LayoutParams(0, h, 1f);
        lp1.setMargins(6, 16, 6, 0);
        row.addView(ok, lp1);

        if (altText != null && !altText.isEmpty()) {
            Ui.GdBtn alt = new Ui.GdBtn(a).set(altText, Ui.I_PROFILE,
                    Color.rgb(150, 200, 255), Color.rgb(40, 90, 170));
            alt.setOnClickListener(v -> { close(a, o); if (onAlt != null) onAlt.run(); });
            LinearLayout.LayoutParams lpA = new LinearLayout.LayoutParams(0, h, 1f);
            lpA.setMargins(6, 16, 6, 0);
            row.addView(alt, lpA);
        }

        LinearLayout.LayoutParams lp2 = new LinearLayout.LayoutParams(0, h, 1f);
        lp2.setMargins(6, 16, 6, 0);
        row.addView(no, lp2);
    }

    /** Что ввели в диалоге входа. */
    public interface OnLogin { void login(String name, String pass); }

    /**
     * Вход в аккаунт: логин и пароль в одном окне. Нужен там, где ключ
     * проще подобрать по аккаунту, чем набирать руками.
     */
    public static void account(Activity a, String title, String message,
                               String okText, String cancelText,
                               OnLogin cb, Runnable onCancel) {
        FrameLayout o = open(a, title, message);
        LinearLayout box = boxOf(o);

        EditText name = field(a, "логин на сайте", false);
        box.addView(name, fieldLp());
        EditText pass = field(a, "пароль", true);
        box.addView(pass, fieldLp());

        LinearLayout row = new LinearLayout(a);
        row.setOrientation(LinearLayout.HORIZONTAL);
        box.addView(row);
        int h = (int) (rootOf(a).getHeight() * 0.13f);

        Ui.GdBtn ok = new Ui.GdBtn(a).set(okText, Ui.I_CHECK, Color.rgb(120, 255, 170), Color.rgb(20, 150, 100));
        ok.setOnClickListener(v -> {
            close(a, o);
            if (cb != null) cb.login(name.getText().toString().trim(), pass.getText().toString());
        });
        Ui.GdBtn no = new Ui.GdBtn(a).set(cancelText, Ui.I_CROSS, Color.rgb(220, 120, 120), Color.rgb(140, 40, 50));
        no.setOnClickListener(v -> { close(a, o); if (onCancel != null) onCancel.run(); });

        LinearLayout.LayoutParams lp1 = new LinearLayout.LayoutParams(0, h, 1f);
        lp1.setMargins(6, 16, 6, 0);
        row.addView(ok, lp1);
        LinearLayout.LayoutParams lp2 = new LinearLayout.LayoutParams(0, h, 1f);
        lp2.setMargins(6, 16, 6, 0);
        row.addView(no, lp2);
    }

    static EditText field(Activity a, String hint, boolean secret) {
        EditText e = new EditText(a);
        e.setHint(hint);
        e.setTextColor(Gd.PAPER);
        e.setHintTextColor(Gd.DIM);
        e.setBackgroundColor(Color.rgb(20, 18, 48));
        e.setPadding(20, 14, 20, 14);
        e.setSingleLine(true);
        e.setTextSize(17);
        if (secret) e.setInputType(android.text.InputType.TYPE_CLASS_TEXT
                | android.text.InputType.TYPE_TEXT_VARIATION_PASSWORD);
        return e;
    }

    static LinearLayout.LayoutParams fieldLp() {
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        lp.setMargins(0, 14, 0, 0);
        return lp;
    }

    /** Список вариантов (вместо setItems у AlertDialog). */
    public static void items(Activity a, String title, String[] items, OnPick cb) {
        FrameLayout o = open(a, title, null);
        LinearLayout box = boxOf(o);

        int rootH = rootOf(a).getHeight();
        int h = (int) (rootH * 0.12f);
        int maxH = (int) (rootH * 0.52f);

        ScrollView sv = new ScrollView(a);
        LinearLayout list = new LinearLayout(a);
        list.setOrientation(LinearLayout.VERTICAL);
        sv.addView(list);
        LinearLayout.LayoutParams slp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, Math.min(maxH, h * Math.max(1, items.length) + 30));
        slp.setMargins(0, 14, 0, 0);
        box.addView(sv, slp);

        for (int i = 0; i < items.length; i++) {
            final int idx = i;
            button(a, list, items[i], Ui.I_NONE, Color.rgb(96, 150, 240), Color.rgb(32, 62, 150), h,
                    () -> { close(a, o); if (cb != null) cb.pick(idx); });
        }

        button(a, box, "ЗАКРЫТЬ", Ui.I_CROSS, Color.rgb(170, 170, 200), Color.rgb(70, 70, 110), h,
                () -> close(a, o));
    }

    /** Диалог с произвольным содержимым (настройки уровня и т.п.). */
    public static LinearLayout custom(Activity a, String title, String okText, Runnable onOk) {
        FrameLayout o = open(a, title, null);
        LinearLayout box = boxOf(o);

        ScrollView sv = new ScrollView(a);
        LinearLayout content = new LinearLayout(a);
        content.setOrientation(LinearLayout.VERTICAL);
        sv.addView(content);
        LinearLayout.LayoutParams slp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, (int) (rootOf(a).getHeight() * 0.5f));
        slp.setMargins(0, 12, 0, 0);
        box.addView(sv, slp);

        LinearLayout row = new LinearLayout(a);
        row.setOrientation(LinearLayout.HORIZONTAL);
        box.addView(row);
        int h = (int) (rootOf(a).getHeight() * 0.13f);

        Ui.GdBtn ok = new Ui.GdBtn(a).set(okText, Ui.I_CHECK, Color.rgb(120, 255, 170), Color.rgb(20, 150, 100));
        ok.setOnClickListener(v -> { close(a, o); if (onOk != null) onOk.run(); });
        Ui.GdBtn no = new Ui.GdBtn(a).set("ОТМЕНА", Ui.I_CROSS, Color.rgb(170, 170, 200), Color.rgb(70, 70, 110));
        no.setOnClickListener(v -> close(a, o));

        LinearLayout.LayoutParams lp1 = new LinearLayout.LayoutParams(0, h, 1f);
        lp1.setMargins(6, 14, 6, 0);
        row.addView(ok, lp1);
        LinearLayout.LayoutParams lp2 = new LinearLayout.LayoutParams(0, h, 1f);
        lp2.setMargins(6, 14, 6, 0);
        row.addView(no, lp2);

        return content;
    }
}
