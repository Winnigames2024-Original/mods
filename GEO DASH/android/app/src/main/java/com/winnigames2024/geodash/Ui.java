package com.winnigames2024.geodash;

import android.content.Context;
import android.graphics.*;
import android.view.MotionEvent;
import android.view.View;
import android.widget.FrameLayout;

/** Общие элементы интерфейса: фон, кнопки, карточки, иконки. */
public class Ui {

    // иконки
    public static final int I_NONE = 0, I_PLAY = 1, I_EDIT = 2, I_ONLINE = 3, I_PROFILE = 4, I_FRIENDS = 5,
            I_SETTINGS = 6, I_ADMIN = 7, I_MUSIC = 8, I_SAVE = 9, I_BACK = 10, I_COIN = 11, I_STAR = 12,
            I_LOCK = 13, I_DOWNLOAD = 14, I_DAILY = 15, I_WEEKLY = 16, I_SEARCH = 17, I_PLUS = 18,
            I_TRASH = 19, I_COPY = 20, I_UPLOAD = 21, I_REFRESH = 22, I_CHECK = 23, I_CROSS = 24;

    /** Анимированный фон в стиле игры. */
    public static class BgView extends View {
        double t;
        int a = Gd.DEEP2, b = Gd.DEEP;

        public BgView(Context c) { this(c, Gd.DEEP2, Gd.DEEP); }

        public BgView(Context c, int colA, int colB) {
            super(c);
            a = colA; b = colB;
            postDelayed(new Runnable() {
                @Override public void run() { t += 0.033; invalidate(); postDelayed(this, 33); }
            }, 33);
        }

        @Override protected void onDraw(Canvas c) { Gd.background(c, getWidth(), getHeight(), t); }
    }

    /** Кнопка в стиле игры: плита с обводкой, иконка и подпись. */
    public static class GdBtn extends View {
        public String text = "", sub = "", caption = "";
        public int icon = I_NONE, col1 = Color.rgb(90, 160, 255), col2 = Color.rgb(25, 70, 170);
        float anim;
        boolean down;

        public GdBtn(Context c) {
            super(c);
            setClickable(true);
        }

        public GdBtn set(String t, int ic, int c1, int c2) {
            text = t; icon = ic; col1 = c1; col2 = c2;
            return this;
        }

        public GdBtn sub(String s) { sub = s; return this; }

        /** Подпись под картинкой — что это за кнопка. */
        public GdBtn caption(String s) { caption = s; return this; }

        @Override public boolean onTouchEvent(MotionEvent e) {
            switch (e.getActionMasked()) {
                case MotionEvent.ACTION_DOWN: down = true; anim = 1; invalidate(); break;
                case MotionEvent.ACTION_UP:
                    if (down) { down = false; anim = 0; invalidate(); performClick(); Music.sfx("click"); }
                    break;
                case MotionEvent.ACTION_CANCEL: down = false; anim = 0; invalidate(); break;
            }
            return true;
        }

        @Override public boolean performClick() { return super.performClick(); }

        @Override protected void onDraw(Canvas c) {
            float pop = anim * 3f;
            RectF r = new RectF(3 + pop, 4 + pop, getWidth() - 3 - pop, getHeight() - 6 - pop);
            if (r.width() < 4 || r.height() < 4) return;

            c.drawRoundRect(new RectF(r.left + 2, r.top + 5, r.right + 2, r.bottom + 5),
                    r.height() * 0.22f, r.height() * 0.22f, Gd.fill(Color.argb(90, 0, 0, 0)));
            Gd.plate(c, r, Gd.lighten(col1, down ? -0.1f : 0.08f), col2, Math.min(r.height() * 0.26f, 26f));

            boolean iconOnly = text == null || text.isEmpty();
            boolean hasCaption = iconOnly && caption != null && !caption.isEmpty();
            float size = Math.min(r.height() * (hasCaption ? 0.48f : 0.56f), r.width() * 0.6f);
            if (icon != I_NONE) {
                RectF ir;
                if (hasCaption) {
                    float top = r.top + r.height() * 0.10f;
                    ir = new RectF(r.centerX() - size / 2, top, r.centerX() + size / 2, top + size);
                } else if (iconOnly) {
                    ir = new RectF(r.centerX() - size / 2, r.centerY() - size / 2,
                            r.centerX() + size / 2, r.centerY() + size / 2);
                } else {
                    ir = new RectF(r.left + 14, r.centerY() - size / 2, r.left + 14 + size, r.centerY() + size / 2);
                }
                icon(c, icon, ir, Gd.PAPER);
            }
            if (hasCaption)
                Gd.outlined(c, caption, r.centerX(), r.bottom - r.height() * 0.12f,
                        Math.min(r.height() * 0.18f, r.width() * 0.17f), Gd.PAPER, Paint.Align.CENTER);
            if (!iconOnly) {
                float tx = icon == I_NONE ? r.centerX() : r.left + (icon == I_NONE ? 0 : size + 24);
                Paint.Align al = icon == I_NONE ? Paint.Align.CENTER : Paint.Align.LEFT;
                float fs = Math.min(r.height() * (sub.isEmpty() ? 0.38f : 0.30f), r.width() * 0.13f);
                float baseY = sub.isEmpty() ? r.centerY() + fs * 0.36f : r.centerY();
                Gd.outlined(c, text, tx, baseY, fs, Gd.PAPER, al);
                if (!sub.isEmpty())
                    Gd.outlined(c, sub, tx, r.centerY() + fs * 1.05f, fs * 0.66f,
                            Color.argb(230, 220, 232, 255), al);
            }
        }
    }

    /** Карточка-плита (для списков). */
    public static class CardView extends View {
        public String title = "", line1 = "", line2 = "";
        public int tint = Gd.PAPER, diff = -1;
        public boolean selected;

        public CardView(Context c) { super(c); setClickable(true); }

        @Override protected void onDraw(Canvas c) {
            RectF r = new RectF(4, 4, getWidth() - 4, getHeight() - 4);
            Gd.plate(c, r, selected ? Color.rgb(60, 58, 120) : Color.rgb(40, 36, 86),
                    Color.rgb(22, 18, 54), 16f);
            float x = 18;
            if (diff >= 0) {
                float s = r.height() * 0.66f;
                RectF fr = new RectF(x, r.centerY() - s / 2, x + s, r.centerY() + s / 2);
                Gd.face(c, fr, diff);
                x += s + 16;
            }
            Gd.outlined(c, title, x, r.top + r.height() * 0.42f, r.height() * 0.30f, tint, Paint.Align.LEFT);
            if (!line1.isEmpty())
                Gd.outlined(c, line1, x, r.top + r.height() * 0.74f, r.height() * 0.21f, Gd.DIM, Paint.Align.LEFT);
            if (!line2.isEmpty())
                Gd.outlined(c, line2, r.right - 18, r.top + r.height() * 0.74f, r.height() * 0.21f,
                        Gd.NEON, Paint.Align.RIGHT);
        }
    }

    // ------------------------------------------------------------ иконки

    public static void icon(Canvas c, int ic, RectF r, int col) {
        Paint br = Gd.fill(col);
        float w = Math.max(2f, r.width() * 0.12f);
        Paint pen = Gd.stroke(col, w);
        float cx = r.centerX(), cy = r.centerY(), s = Math.min(r.width(), r.height());

        switch (ic) {
            case I_PLAY: {
                Path p = new Path();
                p.moveTo(cx - s * 0.26f, cy - s * 0.34f);
                p.lineTo(cx + s * 0.34f, cy);
                p.lineTo(cx - s * 0.26f, cy + s * 0.34f);
                p.close();
                c.drawPath(p, br);
                break;
            }
            case I_EDIT:
                c.drawLine(cx - s * 0.28f, cy + s * 0.28f, cx + s * 0.22f, cy - s * 0.22f, pen);
                c.drawCircle(cx - s * 0.3f, cy + s * 0.3f, s * 0.08f, br);
                break;
            case I_ONLINE:
                c.drawCircle(cx, cy, s * 0.3f, pen);
                c.drawLine(cx - s * 0.3f, cy, cx + s * 0.3f, cy, pen);
                c.drawOval(new RectF(cx - s * 0.15f, cy - s * 0.3f, cx + s * 0.15f, cy + s * 0.3f), pen);
                break;
            case I_PROFILE:
                c.drawCircle(cx, cy - s * 0.14f, s * 0.17f, br);
                c.drawArc(new RectF(cx - s * 0.3f, cy, cx + s * 0.3f, cy + s * 0.55f), 180, 180, true, br);
                break;
            case I_FRIENDS:
                c.drawCircle(cx - s * 0.18f, cy - s * 0.14f, s * 0.14f, br);
                c.drawCircle(cx + s * 0.18f, cy - s * 0.14f, s * 0.14f, br);
                c.drawArc(new RectF(cx - s * 0.42f, cy, cx + s * 0.02f, cy + s * 0.45f), 180, 180, true, br);
                c.drawArc(new RectF(cx - s * 0.02f, cy, cx + s * 0.42f, cy + s * 0.45f), 180, 180, true, br);
                break;
            case I_SETTINGS:
                for (int i = 0; i < 8; i++) {
                    double a = i * Math.PI / 4;
                    c.drawLine((float) (cx + Math.cos(a) * s * 0.18f), (float) (cy + Math.sin(a) * s * 0.18f),
                            (float) (cx + Math.cos(a) * s * 0.34f), (float) (cy + Math.sin(a) * s * 0.34f), pen);
                }
                c.drawCircle(cx, cy, s * 0.16f, pen);
                break;
            case I_ADMIN: {
                Path p = new Path();
                p.moveTo(cx, cy - s * 0.34f);
                p.lineTo(cx + s * 0.32f, cy - s * 0.12f);
                p.lineTo(cx + s * 0.22f, cy + s * 0.32f);
                p.lineTo(cx - s * 0.22f, cy + s * 0.32f);
                p.lineTo(cx - s * 0.32f, cy - s * 0.12f);
                p.close();
                c.drawPath(p, br);
                break;
            }
            case I_MUSIC:
                c.drawCircle(cx - s * 0.18f, cy + s * 0.18f, s * 0.12f, br);
                c.drawCircle(cx + s * 0.18f, cy + s * 0.08f, s * 0.12f, br);
                c.drawLine(cx - s * 0.06f, cy + s * 0.18f, cx - s * 0.06f, cy - s * 0.28f, pen);
                c.drawLine(cx + s * 0.3f, cy + s * 0.08f, cx + s * 0.3f, cy - s * 0.34f, pen);
                c.drawLine(cx - s * 0.06f, cy - s * 0.28f, cx + s * 0.3f, cy - s * 0.34f, pen);
                break;
            case I_SAVE: case I_DOWNLOAD:
                c.drawLine(cx, cy - s * 0.3f, cx, cy + s * 0.16f, pen);
                c.drawLine(cx - s * 0.18f, cy - s * 0.02f, cx, cy + s * 0.18f, pen);
                c.drawLine(cx + s * 0.18f, cy - s * 0.02f, cx, cy + s * 0.18f, pen);
                c.drawLine(cx - s * 0.3f, cy + s * 0.32f, cx + s * 0.3f, cy + s * 0.32f, pen);
                break;
            case I_UPLOAD:
                c.drawLine(cx, cy + s * 0.3f, cx, cy - s * 0.16f, pen);
                c.drawLine(cx - s * 0.18f, cy + s * 0.02f, cx, cy - s * 0.18f, pen);
                c.drawLine(cx + s * 0.18f, cy + s * 0.02f, cx, cy - s * 0.18f, pen);
                break;
            case I_BACK:
                c.drawLine(cx + s * 0.16f, cy - s * 0.26f, cx - s * 0.16f, cy, pen);
                c.drawLine(cx - s * 0.16f, cy, cx + s * 0.16f, cy + s * 0.26f, pen);
                break;
            case I_COIN:
                Gd.coin(c, new RectF(cx - s * 0.32f, cy - s * 0.32f, cx + s * 0.32f, cy + s * 0.32f), true);
                break;
            case I_STAR:
                Gd.star(c, new RectF(cx - s * 0.36f, cy - s * 0.36f, cx + s * 0.36f, cy + s * 0.36f), Gd.GOLD);
                break;
            case I_LOCK:
                c.drawArc(new RectF(cx - s * 0.18f, cy - s * 0.34f, cx + s * 0.18f, cy + s * 0.02f), 180, 180, false, pen);
                c.drawRoundRect(new RectF(cx - s * 0.26f, cy - s * 0.06f, cx + s * 0.26f, cy + s * 0.34f), 6, 6, br);
                break;
            case I_DAILY: case I_WEEKLY:
                c.drawRoundRect(new RectF(cx - s * 0.3f, cy - s * 0.28f, cx + s * 0.3f, cy + s * 0.3f), 6, 6, pen);
                c.drawLine(cx - s * 0.3f, cy - s * 0.1f, cx + s * 0.3f, cy - s * 0.1f, pen);
                if (ic == I_WEEKLY) c.drawLine(cx, cy - s * 0.1f, cx, cy + s * 0.3f, pen);
                break;
            case I_SEARCH:
                c.drawCircle(cx - s * 0.08f, cy - s * 0.08f, s * 0.22f, pen);
                c.drawLine(cx + s * 0.1f, cy + s * 0.1f, cx + s * 0.32f, cy + s * 0.32f, pen);
                break;
            case I_PLUS:
                c.drawLine(cx - s * 0.26f, cy, cx + s * 0.26f, cy, pen);
                c.drawLine(cx, cy - s * 0.26f, cx, cy + s * 0.26f, pen);
                break;
            case I_TRASH:
                c.drawRect(new RectF(cx - s * 0.2f, cy - s * 0.18f, cx + s * 0.2f, cy + s * 0.3f), pen);
                c.drawLine(cx - s * 0.3f, cy - s * 0.24f, cx + s * 0.3f, cy - s * 0.24f, pen);
                break;
            case I_COPY:
                c.drawRect(new RectF(cx - s * 0.28f, cy - s * 0.3f, cx + s * 0.1f, cy + s * 0.14f), pen);
                c.drawRect(new RectF(cx - s * 0.1f, cy - s * 0.14f, cx + s * 0.28f, cy + s * 0.3f), pen);
                break;
            case I_REFRESH:
                c.drawArc(new RectF(cx - s * 0.28f, cy - s * 0.28f, cx + s * 0.28f, cy + s * 0.28f), 30, 280, false, pen);
                break;
            case I_CHECK:
                c.drawLine(cx - s * 0.26f, cy, cx - s * 0.06f, cy + s * 0.22f, pen);
                c.drawLine(cx - s * 0.06f, cy + s * 0.22f, cx + s * 0.28f, cy - s * 0.24f, pen);
                break;
            case I_CROSS:
                c.drawLine(cx - s * 0.22f, cy - s * 0.22f, cx + s * 0.22f, cy + s * 0.22f, pen);
                c.drawLine(cx + s * 0.22f, cy - s * 0.22f, cx - s * 0.22f, cy + s * 0.22f, pen);
                break;
        }
    }

    /** FrameLayout-параметры в долях экрана. */
    public static FrameLayout.LayoutParams lp(int parentW, int parentH,
                                              float x, float y, float w, float h) {
        FrameLayout.LayoutParams p = new FrameLayout.LayoutParams(
                (int) (parentW * w), (int) (parentH * h));
        p.leftMargin = (int) (parentW * x);
        p.topMargin = (int) (parentH * y);
        p.gravity = Gravity.NO_GRAVITY;
        return p;
    }

    static final class Gravity {
        static final int NO_GRAVITY = android.view.Gravity.TOP | android.view.Gravity.START;
    }
}
