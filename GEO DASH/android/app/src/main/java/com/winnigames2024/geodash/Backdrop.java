package com.winnigames2024.geodash;

import android.graphics.*;
import android.util.SparseArray;

/**
 * Фон и пол как в Geometry Dash: ровный цвет, поверх — повторяющийся узор,
 * который едет за камерой, снизу пол со своей текстурой и яркой линией.
 */
public final class Backdrop {

    public static final int BG_COUNT = 20;
    public static final int GROUND_COUNT = 10;
    static final int BG_TILE = 128;
    static final int GR_TILE = 64;

    /** Классический синий фон меню. */
    public static final int MENU_BG = Color.rgb(40, 125, 255);

    static final SparseArray<Bitmap> BG = new SparseArray<>();
    static final SparseArray<Bitmap> GR = new SparseArray<>();

    public static int mix(int a, int b, double k) {
        if (k < 0) k = 0; else if (k > 1) k = 1;
        return Color.argb(Color.alpha(a),
                (int) (Color.red(a) + (Color.red(b) - Color.red(a)) * k),
                (int) (Color.green(a) + (Color.green(b) - Color.green(a)) * k),
                (int) (Color.blue(a) + (Color.blue(b) - Color.blue(a)) * k));
    }

    /** Цвет пола по цвету фона. */
    public static int groundOf(int bg) { return mix(bg, Color.rgb(0, 0, 20), 0.42); }

    static int idx(int id, int count) { return ((id - 1) % count + count) % count; }

    // ------------------------------------------------------------ маски

    static Paint w(float width, Paint.Style st) {
        Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
        p.setColor(Color.WHITE);
        p.setStyle(st);
        p.setStrokeWidth(width);
        p.setStrokeJoin(Paint.Join.ROUND);
        return p;
    }

    static void dots(Canvas c, int s, int step, float r, boolean fill, float thick) {
        Paint p = w(thick, fill ? Paint.Style.FILL : Paint.Style.STROKE);
        for (int y = step / 2; y < s; y += step)
            for (int x = step / 2; x < s; x += step)
                c.drawCircle(x, y, r, p);
    }

    static Bitmap bgMask(int id) {
        int i = idx(id, BG_COUNT);
        Bitmap b = BG.get(i);
        if (b != null) return b;
        int s = BG_TILE;
        b = Bitmap.createBitmap(s, s, Bitmap.Config.ARGB_8888);
        Canvas c = new Canvas(b);
        Paint f = w(1, Paint.Style.FILL);
        Paint p = w(8, Paint.Style.STROKE);

        switch (i) {
            case 0: // широкие диагональные полосы
                p.setStrokeWidth(18);
                for (float x = -s; x <= s * 2; x += 36) c.drawLine(x - 4, -4, x + s + 4, s + 4, p);
                break;
            case 1: // крупная клетка
                p.setStrokeWidth(7);
                for (int k = 0; k <= s; k += 64) { c.drawLine(k, -2, k, s + 2, p); c.drawLine(-2, k, s + 2, k, p); }
                break;
            case 2:
                dots(c, s, 64, 21, true, 1);
                break;
            case 3: // шеврон
                p.setStrokeWidth(10);
                for (int y = 0; y <= s; y += 32)
                    for (int x = -64; x <= s; x += 64) {
                        c.drawLine(x, y + 16, x + 32, y - 8, p);
                        c.drawLine(x + 32, y - 8, x + 64, y + 16, p);
                    }
                break;
            case 4: // кирпич
                p.setStrokeWidth(6);
                for (int y = 0; y <= s; y += 32) {
                    c.drawLine(-2, y, s + 2, y, p);
                    int off = (y / 32) % 2 == 0 ? 0 : 32;
                    for (int x = off; x <= s; x += 64) c.drawLine(x, y, x, y + 32, p);
                }
                break;
            case 5: // треугольники
                for (int y = 0; y <= s; y += 64)
                    for (int x = -32; x <= s; x += 64) {
                        Path t = new Path();
                        t.moveTo(x, y + 48); t.lineTo(x + 32, y + 8); t.lineTo(x + 64, y + 48); t.close();
                        c.drawPath(t, f);
                    }
                break;
            case 6: // сетка по диагонали
                p.setStrokeWidth(4);
                for (float x = -s; x <= s * 2; x += 24) {
                    c.drawLine(x - 4, -4, x + s + 4, s + 4, p);
                    c.drawLine(x + s + 4, -4, x - 4, s + 4, p);
                }
                break;
            case 7:
                dots(c, s, 42, 17, false, 5);
                break;
            case 8: // кольца
                p.setStrokeWidth(7);
                for (int k = 0; k < 5; k++) {
                    float cx = k == 4 ? s / 2f : (k % 2 == 0 ? 0 : s);
                    float cy = k == 4 ? s / 2f : (k < 2 ? 0 : s);
                    for (float r = 18; r <= 56; r += 19) c.drawCircle(cx, cy, r, p);
                }
                break;
            case 9: // вертикальные полосы
                p.setStrokeWidth(22);
                for (int x = 0; x <= s; x += 64) c.drawLine(x, -2, x, s + 2, p);
                break;
            case 10: // шахматка
                for (int y = 0; y < s; y += 32)
                    for (int x = 0; x < s; x += 32)
                        if (((x / 32) + (y / 32)) % 2 == 0) c.drawRect(x, y, x + 32, y + 32, f);
                break;
            case 11: // звёздочки
                for (int y = 32; y < s; y += 64)
                    for (int x = 32; x < s; x += 64) {
                        Path st = new Path();
                        for (int k = 0; k < 8; k++) {
                            double a = -Math.PI / 2 + k * Math.PI / 4;
                            double rr = k % 2 == 0 ? 22 : 8;
                            float px = (float) (x + Math.cos(a) * rr), py = (float) (y + Math.sin(a) * rr);
                            if (k == 0) st.moveTo(px, py); else st.lineTo(px, py);
                        }
                        st.close();
                        c.drawPath(st, f);
                    }
                break;
            case 12: // волны
                p.setStrokeWidth(8);
                for (int y = 0; y <= s; y += 32) {
                    Path path = new Path();
                    for (int k = 0; k <= 32; k++) {
                        float px = k * (s / 32f), py = (float) (y + Math.sin(k / 32.0 * Math.PI * 2) * 11);
                        if (k == 0) path.moveTo(px, py); else path.lineTo(px, py);
                    }
                    c.drawPath(path, p);
                }
                break;
            case 13: // сетка плюс диагональ
                p.setStrokeWidth(4);
                for (int k = 0; k <= s; k += 32) { c.drawLine(k, -2, k, s + 2, p); c.drawLine(-2, k, s + 2, k, p); }
                p.setStrokeWidth(12);
                for (float x = -s; x <= s * 2; x += 64) c.drawLine(x - 4, -4, x + s + 4, s + 4, p);
                break;
            case 14: // чешуя
                p.setStrokeWidth(7);
                for (int y = 0; y <= s; y += 32)
                    for (int x = -32; x <= s; x += 32)
                        c.drawArc(new RectF(x, y - 16, x + 32, y + 16), 0, 180, false, p);
                break;
            case 15: // ромбы
                for (int y = 0; y <= s; y += 64)
                    for (int x = 0; x <= s; x += 64) {
                        Path d = new Path();
                        d.moveTo(x, y - 24); d.lineTo(x + 24, y); d.lineTo(x, y + 24); d.lineTo(x - 24, y); d.close();
                        c.drawPath(d, f);
                    }
                break;
            case 16: // плитка со скруглением
                for (int y = 0; y <= s; y += 64)
                    for (int x = 0; x <= s; x += 64)
                        c.drawRoundRect(new RectF(x - 22, y - 22, x + 22, y + 22), 10, 10, f);
                break;
            case 17: // тонкие обратные диагонали
                p.setStrokeWidth(5);
                for (float x = -s; x <= s * 2; x += 20) c.drawLine(x + s + 4, -4, x - 4, s + 4, p);
                break;
            case 18: // кресты
                p.setStrokeWidth(9);
                for (int y = 32; y < s; y += 64)
                    for (int x = 32; x < s; x += 64) {
                        c.drawLine(x - 18, y, x + 18, y, p);
                        c.drawLine(x, y - 18, x, y + 18, p);
                    }
                break;
            default: // «пиксели»
                java.util.Random rnd = new java.util.Random(7);
                for (int y = 0; y < s; y += 16)
                    for (int x = 0; x < s; x += 16)
                        if (rnd.nextInt(100) < 34) {
                            f.setColor(Color.argb(120 + rnd.nextInt(135), 255, 255, 255));
                            c.drawRect(x, y, x + 16, y + 16, f);
                        }
                break;
        }
        BG.put(i, b);
        return b;
    }

    static Bitmap groundMask(int id) {
        int i = idx(id, GROUND_COUNT);
        Bitmap b = GR.get(i);
        if (b != null) return b;
        int s = GR_TILE;
        b = Bitmap.createBitmap(s, s, Bitmap.Config.ARGB_8888);
        Canvas c = new Canvas(b);
        Paint f = w(1, Paint.Style.FILL);
        Paint p = w(5, Paint.Style.STROKE);

        switch (i) {
            case 0: // клетка с внутренним квадратом
                for (int k = 0; k <= s; k += 32) { c.drawLine(k, -2, k, s + 2, p); c.drawLine(-2, k, s + 2, k, p); }
                p.setStrokeWidth(3);
                for (int y = 0; y < s; y += 32)
                    for (int x = 0; x < s; x += 32)
                        c.drawRect(x + 9, y + 9, x + 23, y + 23, p);
                break;
            case 1:
                p.setStrokeWidth(8);
                for (float x = -s; x <= s * 2; x += 24) c.drawLine(x - 2, -2, x + s + 2, s + 2, p);
                break;
            case 2:
                for (int x = 0; x <= s; x += 32) c.drawRect(x - 6, -2, x + 6, s + 2, f);
                break;
            case 3:
                for (int y = 0; y <= s; y += 32) {
                    c.drawLine(-2, y, s + 2, y, p);
                    int off = (y / 32) % 2 == 0 ? 0 : 16;
                    for (int x = off; x <= s; x += 32) c.drawLine(x, y, x, y + 32, p);
                }
                break;
            case 4:
                for (int y = 0; y < s; y += 16)
                    for (int x = 0; x < s; x += 16)
                        if (((x / 16) + (y / 16)) % 2 == 0) c.drawRect(x, y, x + 16, y + 16, f);
                break;
            case 5:
                dots(c, s, 32, 9, true, 1);
                break;
            case 6:
                p.setStrokeWidth(7);
                for (int y = 0; y <= s; y += 32)
                    for (int x = -32; x <= s; x += 32) {
                        c.drawLine(x, y + 16, x + 16, y, p);
                        c.drawLine(x + 16, y, x + 32, y + 16, p);
                    }
                break;
            case 7:
                p.setStrokeWidth(7);
                for (int k = 0; k <= s; k += 64) { c.drawLine(k, -2, k, s + 2, p); c.drawLine(-2, k, s + 2, k, p); }
                break;
            case 8:
                p.setStrokeWidth(3);
                for (int k = 0; k <= s; k += 16) { c.drawLine(k, -2, k, s + 2, p); c.drawLine(-2, k, s + 2, k, p); }
                break;
            default:
                p.setStrokeWidth(6);
                for (int y = 0; y <= s; y += 32)
                    for (int x = 0; x <= s; x += 32) {
                        c.drawLine(x, y, x + 32, y + 32, p);
                        c.drawLine(x + 32, y, x, y + 32, p);
                    }
                break;
        }
        GR.put(i, b);
        return b;
    }

    // ------------------------------------------------------------ тиражирование

    static float wrap(double v, float period) {
        if (period <= 0.01f) return 0;
        float r = (float) (v % period);
        return r > 0 ? r - period : r;
    }

    static void tiled(Canvas c, RectF area, Bitmap mask, int tint, float offX, float offY, float scale) {
        if (area.width() <= 0 || area.height() <= 0 || Color.alpha(tint) <= 2) return;
        BitmapShader sh = new BitmapShader(mask, Shader.TileMode.REPEAT, Shader.TileMode.REPEAT);
        Matrix m = new Matrix();
        m.setScale(scale, scale);
        m.postTranslate(offX, offY);
        sh.setLocalMatrix(m);
        Paint p = new Paint(Paint.ANTI_ALIAS_FLAG | Paint.FILTER_BITMAP_FLAG);
        p.setShader(sh);
        p.setColorFilter(new PorterDuffColorFilter(tint, PorterDuff.Mode.SRC_IN));
        c.drawRect(area, p);
    }

    // ------------------------------------------------------------ фон

    public static void background(Canvas c, RectF area, int bgId, int bg,
                                 double scrollX, double scrollY, float scale) {
        if (area.width() <= 0 || area.height() <= 0) return;

        Paint p = new Paint();
        p.setShader(new LinearGradient(0, area.top, 0, area.bottom,
                mix(bg, Color.WHITE, 0.10), mix(bg, Color.BLACK, 0.18), Shader.TileMode.CLAMP));
        c.drawRect(area, p);

        int i = idx(bgId, BG_COUNT);
        boolean light = i % 3 == 2;
        int tint = light ? Color.argb(46, Color.red(mix(bg, Color.WHITE, 0.75)),
                Color.green(mix(bg, Color.WHITE, 0.75)), Color.blue(mix(bg, Color.WHITE, 0.75)))
                : Color.argb(70, Color.red(mix(bg, Color.BLACK, 0.55)),
                Color.green(mix(bg, Color.BLACK, 0.55)), Color.blue(mix(bg, Color.BLACK, 0.55)));

        float period = BG_TILE * scale;
        tiled(c, area, bgMask(bgId), tint, wrap(-scrollX, period), wrap(scrollY, period), scale);

        Paint d = new Paint();
        float y0 = area.top + area.height() * 0.45f;
        d.setShader(new LinearGradient(0, y0, 0, area.bottom,
                Color.argb(0, 0, 0, 0), Color.argb(70, 0, 0, 20), Shader.TileMode.CLAMP));
        c.drawRect(area.left, y0, area.right, area.bottom, d);
    }

    /** Пол: текстура, светлая кромка, яркая линия и тень под ней. flip — потолок. */
    public static void ground(Canvas c, RectF area, int groundId, int col, int line,
                              double scrollX, float scale, boolean flip) {
        if (area.width() <= 0 || area.height() <= 0) return;

        float edge = Math.max(2f, 4f * scale);
        float lineW = Math.max(2f, 3.2f * scale);

        Paint p = new Paint();
        p.setShader(new LinearGradient(0, area.top, 0, area.bottom,
                flip ? mix(col, Color.BLACK, 0.55) : col,
                flip ? col : mix(col, Color.BLACK, 0.55), Shader.TileMode.CLAMP));
        c.drawRect(area, p);

        float period = GR_TILE * scale;
        int tint = Color.argb(70, Color.red(mix(col, Color.BLACK, 0.6)),
                Color.green(mix(col, Color.BLACK, 0.6)), Color.blue(mix(col, Color.BLACK, 0.6)));
        tiled(c, area, groundMask(groundId), tint, wrap(-scrollX, period), 0, scale);

        float top = flip ? area.bottom - edge : area.top;
        c.drawRect(area.left, top, area.right, top + edge, Gd.fill(mix(col, Color.WHITE, 0.45)));

        float ly = flip ? area.bottom - edge - lineW / 2 : area.top + lineW / 2;
        Paint lp = Gd.stroke(line, lineW);
        c.drawLine(area.left, ly, area.right, ly, lp);

        float sh = Math.max(3f, 10f * scale);
        float sy = flip ? area.bottom - edge - sh : area.top + edge;
        Paint sp = new Paint();
        sp.setShader(new LinearGradient(0, sy, 0, sy + sh,
                flip ? Color.argb(0, 0, 0, 0) : Color.argb(110, 0, 0, 0),
                flip ? Color.argb(110, 0, 0, 0) : Color.argb(0, 0, 0, 0), Shader.TileMode.CLAMP));
        c.drawRect(area.left, sy, area.right, sy + sh, sp);
    }

    private Backdrop() {}
}
