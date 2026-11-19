package com.winnigames2024.geodash;

import android.graphics.*;

/** Палитра, константы игры и рисовалки интерфейса. */
public final class Gd {

    // ------------------------------------------------------------ константы

    public static final int CUBE = 0, SHIP = 1, BALL = 2, UFO = 3, WAVE = 4, ROBOT = 5, SPIDER = 6, WORM = 7;
    public static final String[] MODE_NAMES = {"Куб","Корабль","Шар","НЛО","Волна","Робот","Паук","Червяк"};

    /** 0.5x, 1x, 2x, 3x, 4x, 5x, 6x */
    public static final double[] SPEED_MUL = {0.807, 1.0, 1.243, 1.502, 1.849, 2.25, 2.70};
    public static final String[] SPEED_NAMES = {"0.5x","1x","2x","3x","4x","5x (+5)","6x (+6)"};
    public static final double SPEED_BASE = 311.58;

    public static final String[] DIFFS = {"Без оценки","Auto","Easy","Normal","Hard","Harder","Insane",
            "Easy Demon","Medium Demon","Hard Demon","Insane Demon","Extreme Demon"};
    public static final int[] DIFF_STARS = {0,1,2,3,5,7,9,10,10,10,10,10};

    public static double speedUnits(int tier) {
        if (tier < 0) tier = 0;
        if (tier >= SPEED_MUL.length) tier = SPEED_MUL.length - 1;
        return SPEED_BASE * SPEED_MUL[tier];
    }

    public static boolean isDemon(int d) { return d >= 7; }

    public static int diffTint(int d) {
        if (d >= 7) return Color.rgb(200, 60, 230);
        switch (d) {
            case 1: return Color.rgb(120, 220, 255);
            case 2: return Color.rgb(90, 220, 130);
            case 3: return Color.rgb(120, 190, 255);
            case 4: return Color.rgb(255, 205, 90);
            case 5: return Color.rgb(255, 140, 70);
            case 6: return Color.rgb(255, 90, 120);
            default: return Color.rgb(160, 160, 160);
        }
    }

    // ------------------------------------------------------------ цвета ui

    public static final int BG_BLUE = Color.rgb(40, 125, 255);
    public static final int DEEP = Color.rgb(11, 10, 30);
    public static final int DEEP2 = Color.rgb(30, 18, 66);
    public static final int NEON = Color.rgb(0, 230, 255);
    public static final int MAG = Color.rgb(255, 60, 190);
    public static final int LIME = Color.rgb(134, 255, 120);
    public static final int GOLD = Color.rgb(255, 205, 60);
    public static final int RED = Color.rgb(255, 90, 106);
    public static final int INK = Color.rgb(7, 6, 26);
    public static final int PAPER = Color.rgb(238, 242, 255);
    public static final int DIM = Color.rgb(154, 163, 199);

    // ------------------------------------------------------------ рисовалки

    static final Paint P = new Paint(Paint.ANTI_ALIAS_FLAG);

    public static Paint fill(int color) {
        Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
        p.setStyle(Paint.Style.FILL);
        p.setColor(color);
        return p;
    }

    public static Paint stroke(int color, float w) {
        Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
        p.setStyle(Paint.Style.STROKE);
        p.setStrokeWidth(w);
        p.setStrokeCap(Paint.Cap.ROUND);
        p.setStrokeJoin(Paint.Join.ROUND);
        p.setColor(color);
        return p;
    }

    /** Текст с чёрной обводкой — как в игре. */
    public static void outlined(Canvas c, String text, float x, float y, float size, int color,
                                Paint.Align align) {
        if (text == null) return;
        Paint o = new Paint(Paint.ANTI_ALIAS_FLAG);
        o.setTextSize(size);
        o.setTypeface(Typeface.create(Typeface.DEFAULT, Typeface.BOLD));
        o.setTextAlign(align);
        o.setStyle(Paint.Style.STROKE);
        o.setStrokeWidth(Math.max(2.5f, size * 0.17f));
        o.setStrokeJoin(Paint.Join.ROUND);
        o.setColor(INK);
        c.drawText(text, x, y, o);
        o.setStyle(Paint.Style.FILL);
        o.setColor(color);
        c.drawText(text, x, y, o);
    }

    public static float textWidth(String s, float size) {
        Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
        p.setTextSize(size);
        p.setTypeface(Typeface.create(Typeface.DEFAULT, Typeface.BOLD));
        return p.measureText(s == null ? "" : s);
    }

    /** Плита интерфейса: градиент, чёрная обводка, блик сверху и тёмная кромка снизу. */
    public static void plate(Canvas c, RectF r, int top, int bottom, float rad) {
        if (r.width() <= 2 || r.height() <= 2) return;
        Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
        p.setShader(new LinearGradient(r.left, r.top, r.left, r.bottom, top, bottom, Shader.TileMode.CLAMP));
        c.drawRoundRect(r, rad, rad, p);

        int save = c.save();
        Path clip = new Path();
        clip.addRoundRect(r, rad, rad, Path.Direction.CW);
        c.clipPath(clip);
        Paint low = new Paint();
        float ly = r.bottom - r.height() * 0.26f;
        low.setShader(new LinearGradient(0, ly, 0, r.bottom,
                Color.argb(0, 0, 0, 0), Color.argb(90, 0, 0, 0), Shader.TileMode.CLAMP));
        c.drawRect(r.left, ly, r.right, r.bottom, low);
        c.restoreToCount(save);

        RectF hi = new RectF(r.left + 4, r.top + 3, r.right - 4, r.top + r.height() * 0.40f);
        if (hi.width() > 2 && hi.height() > 2) {
            Paint hp = new Paint(Paint.ANTI_ALIAS_FLAG);
            hp.setShader(new LinearGradient(0, hi.top, 0, hi.bottom,
                    Color.argb(70, 255, 255, 255), Color.argb(12, 255, 255, 255), Shader.TileMode.CLAMP));
            c.drawRoundRect(hi, rad * 0.75f, rad * 0.75f, hp);
        }

        c.drawRoundRect(r, rad, rad, stroke(INK, Math.max(3f, Math.min(4.5f, r.height() * 0.06f))));
    }

    /** Кнопка: та же плита, при нажатии проваливается и темнеет. */
    public static void buttonPlate(Canvas c, RectF r, int c1, int c2, float rad, boolean pressed) {
        if (r.width() <= 2 || r.height() <= 2) return;
        if (!pressed) {
            RectF sh = new RectF(r);
            sh.offset(0, Math.max(3f, r.height() * 0.07f));
            c.drawRoundRect(sh, rad, rad, fill(Color.argb(110, 0, 0, 0)));
        }
        plate(c, r, lighten(c1, pressed ? -0.22f : 0.10f), pressed ? lighten(c2, -0.22f) : c2, rad);
    }

    public static void glass(Canvas c, RectF r, float rad) {
        if (r.width() <= 2 || r.height() <= 2) return;
        Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
        p.setShader(new LinearGradient(0, r.top, 0, r.bottom,
                Color.argb(205, 36, 26, 74), Color.argb(215, 18, 13, 44), Shader.TileMode.CLAMP));
        c.drawRoundRect(r, rad, rad, p);
        RectF inner = new RectF(r.left + 3, r.top + 3, r.right - 3, r.bottom - 3);
        c.drawRoundRect(inner, Math.max(2f, rad - 3), Math.max(2f, rad - 3),
                stroke(Color.argb(70, 255, 255, 255), 2f));
        c.drawRoundRect(r, rad, rad, stroke(INK, 3.2f));
    }

    /** Фон меню как в игре: ровный цвет с едущим узором и пол. */
    public static void background(Canvas c, int w, int h, double t) {
        background(c, w, h, t, 1, 1, BG_BLUE, Backdrop.groundOf(BG_BLUE));
    }

    public static void background(Canvas c, int w, int h, double t,
                                  int bgId, int groundId, int bg, int ground) {
        if (w <= 0 || h <= 0) return;
        float scale = Math.max(0.65f, Math.min(1.9f, h / 620f));
        Backdrop.background(c, new RectF(0, 0, w, h), bgId, bg, t * 42, Math.sin(t * 0.25) * 10, scale);
        float gh = Math.max(56f, h * 0.155f);
        Backdrop.ground(c, new RectF(0, h - gh, w, h), groundId, ground, Color.WHITE, t * 170, scale, false);
    }

    /** «Лицо» сложности. */
    public static void face(Canvas c, RectF r, int diff) {
        int col = diffTint(diff);
        Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
        p.setShader(new LinearGradient(r.left, r.top, r.left, r.bottom, lighten(col, 0.35f), col, Shader.TileMode.CLAMP));
        c.drawOval(r, p);
        c.drawOval(r, stroke(INK, Math.max(2f, r.width() * 0.08f)));

        float cx = r.centerX(), cy = r.centerY(), s = r.width();
        Paint ink = fill(INK);
        Paint pen = stroke(INK, Math.max(2f, s * 0.07f));
        float ey = cy - s * 0.08f;

        if (isDemon(diff)) {
            Path h1 = new Path();
            h1.moveTo(r.left + s * 0.12f, r.top + s * 0.18f);
            h1.lineTo(r.left + s * 0.30f, r.top + s * 0.02f);
            h1.lineTo(r.left + s * 0.34f, r.top + s * 0.22f);
            h1.close(); c.drawPath(h1, ink);
            Path h2 = new Path();
            h2.moveTo(r.right - s * 0.12f, r.top + s * 0.18f);
            h2.lineTo(r.right - s * 0.30f, r.top + s * 0.02f);
            h2.lineTo(r.right - s * 0.34f, r.top + s * 0.22f);
            h2.close(); c.drawPath(h2, ink);
            Path e1 = new Path();
            e1.moveTo(cx - s * 0.26f, ey - s * 0.04f); e1.lineTo(cx - s * 0.06f, ey + s * 0.04f);
            e1.lineTo(cx - s * 0.26f, ey + s * 0.10f); e1.close(); c.drawPath(e1, ink);
            Path e2 = new Path();
            e2.moveTo(cx + s * 0.26f, ey - s * 0.04f); e2.lineTo(cx + s * 0.06f, ey + s * 0.04f);
            e2.lineTo(cx + s * 0.26f, ey + s * 0.10f); e2.close(); c.drawPath(e2, ink);
            c.drawArc(new RectF(cx - s * 0.2f, cy + s * 0.14f, cx + s * 0.2f, cy + s * 0.36f), 0, 180, false, pen);
        } else {
            c.drawOval(new RectF(cx - s * 0.22f, ey - s * 0.05f, cx - s * 0.11f, ey + s * 0.08f), ink);
            c.drawOval(new RectF(cx + s * 0.11f, ey - s * 0.05f, cx + s * 0.22f, ey + s * 0.08f), ink);
            if (diff <= 3) c.drawArc(new RectF(cx - s * 0.2f, cy, cx + s * 0.2f, cy + s * 0.26f), 0, 180, false, pen);
            else if (diff <= 5) c.drawLine(cx - s * 0.18f, cy + s * 0.18f, cx + s * 0.18f, cy + s * 0.18f, pen);
            else c.drawArc(new RectF(cx - s * 0.2f, cy + s * 0.12f, cx + s * 0.2f, cy + s * 0.38f), 180, 180, false, pen);
        }
    }

    public static void coin(Canvas c, RectF r, boolean got) {
        int col = got ? GOLD : Color.rgb(90, 90, 110);
        Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
        p.setShader(new LinearGradient(r.left, r.top, r.left, r.bottom, lighten(col, 0.4f), col, Shader.TileMode.CLAMP));
        c.drawOval(r, p);
        c.drawOval(r, stroke(INK, Math.max(1.5f, r.width() * 0.12f)));
    }

    public static void star(Canvas c, RectF r, int col) {
        Path path = new Path();
        float cx = r.centerX(), cy = r.centerY(), R = Math.min(r.width(), r.height()) / 2f;
        for (int i = 0; i < 10; i++) {
            double a = -Math.PI / 2 + i * Math.PI / 5;
            double rr = (i % 2 == 0) ? R : R * 0.46;
            float x = (float) (cx + Math.cos(a) * rr), y = (float) (cy + Math.sin(a) * rr);
            if (i == 0) path.moveTo(x, y); else path.lineTo(x, y);
        }
        path.close();
        c.drawPath(path, fill(col));
        c.drawPath(path, stroke(INK, Math.max(1.4f, R * 0.16f)));
    }

    /** Треугольник PLAY. */
    public static void playButton(Canvas c, RectF r, double t) {
        float size = Math.min(r.width(), r.height());
        float pulse = (float) (1 + Math.sin(t * 2.4) * 0.025);
        float s2 = size * 0.9f * pulse;
        RectF rr = new RectF(r.centerX() - s2 / 2, r.centerY() - s2 / 2, r.centerX() + s2 / 2, r.centerY() + s2 / 2);

        for (int i = 3; i >= 1; i--) {
            RectF g = new RectF(rr); g.inset(-i * 7f, -i * 7f);
            c.drawOval(g, stroke(Color.argb(30 + i * 10, 0, 230, 255), 3f));
        }
        Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
        p.setShader(new LinearGradient(rr.left, rr.top, rr.left, rr.bottom,
                Color.rgb(120, 255, 170), Color.rgb(20, 160, 110), Shader.TileMode.CLAMP));
        c.drawOval(rr, p);
        c.drawOval(rr, stroke(INK, 5f));

        float s = s2 * 0.34f;
        float cx = rr.centerX() + s * 0.08f, cy = rr.centerY();
        Path tri = new Path();
        tri.moveTo(cx - s * 0.55f, cy - s);
        tri.lineTo(cx + s * 0.85f, cy);
        tri.lineTo(cx - s * 0.55f, cy + s);
        tri.close();
        c.drawPath(tri, fill(PAPER));
        c.drawPath(tri, stroke(INK, 4f));
    }

    public static int lighten(int c, float k) {
        int r = Color.red(c), g = Color.green(c), b = Color.blue(c);
        if (k > 0) {
            r += (255 - r) * k; g += (255 - g) * k; b += (255 - b) * k;
        } else {
            r *= (1 + k); g *= (1 + k); b *= (1 + k);
        }
        return Color.argb(Color.alpha(c), clamp(r), clamp(g), clamp(b));
    }

    public static int blend(int a, int b, double k) {
        k = Math.max(0, Math.min(1, k));
        return Color.argb(
                (int) (Color.alpha(a) + (Color.alpha(b) - Color.alpha(a)) * k),
                (int) (Color.red(a) + (Color.red(b) - Color.red(a)) * k),
                (int) (Color.green(a) + (Color.green(b) - Color.green(a)) * k),
                (int) (Color.blue(a) + (Color.blue(b) - Color.blue(a)) * k));
    }

    static int clamp(float v) { return (int) Math.max(0, Math.min(255, v)); }

    private Gd() {}
}
