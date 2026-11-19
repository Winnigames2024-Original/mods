package com.winnigames2024.geodash;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.graphics.*;
import android.os.Bundle;
import android.view.*;
import android.widget.*;

import java.io.File;
import java.util.ArrayList;
import java.util.List;

/** Редактор уровней на телефоне: сетка, палитра объектов, тест и загрузка. */
public class EditorCanvasActivity extends Activity {

    static Level editing;

    public static void edit(Context ctx, Level lv) {
        editing = lv;
        Intent i = new Intent(ctx, EditorCanvasActivity.class);
        i.addFlags(Intent.FLAG_ACTIVITY_NO_ANIMATION);
        ctx.startActivity(i);
        if (ctx instanceof Activity) ((Activity) ctx).overridePendingTransition(0, 0);
    }

    Level lv;
    EditorView view;
    TextView info;
    int mode = 0;            // 0 — ставить, 1 — удалять, 2 — двигать вид
    Palette.Item brush = Palette.ITEMS.get(0);

    @Override protected void onCreate(Bundle b) {
        super.onCreate(b);
        requestWindowFeature(Window.FEATURE_NO_TITLE);
        getWindow().setFlags(WindowManager.LayoutParams.FLAG_FULLSCREEN, WindowManager.LayoutParams.FLAG_FULLSCREEN);
        Store.init(this);

        lv = editing;
        if (lv == null) { finish(); return; }

        FrameLayout root = new FrameLayout(this);
        setContentView(root);

        view = new EditorView(this);
        root.addView(view, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));

        LinearLayout top = new LinearLayout(this);
        top.setOrientation(LinearLayout.HORIZONTAL);
        top.setBackgroundColor(Color.argb(190, 10, 8, 26));
        top.addView(tool("ОБЪЕКТ", v -> palette()));
        top.addView(tool("СТАВИТЬ", v -> setMode(0)));
        top.addView(tool("УДАЛЯТЬ", v -> setMode(1)));
        top.addView(tool("ВИД", v -> setMode(2)));
        top.addView(tool("ТЕСТ", v -> test()));
        top.addView(tool("НАСТР.", v -> LevelSettingsDialog.show(this, lv, this::save)));
        top.addView(tool("СОХР.", v -> { save(); toast("Сохранено"); }));
        top.addView(tool("СЕРВЕР", v -> upload()));
        top.addView(tool("ВЫХОД", v -> { save(); finish(); }));
        root.addView(top, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));

        info = new TextView(this);
        info.setTextColor(Gd.PAPER);
        info.setBackgroundColor(Color.argb(160, 10, 8, 26));
        info.setPadding(16, 6, 16, 6);
        FrameLayout.LayoutParams lp = new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT, Gravity.BOTTOM);
        root.addView(info, lp);
        updateInfo();
    }

    Button tool(String t, View.OnClickListener l) {
        Button b = new Button(this);
        b.setText(t);
        b.setTextSize(12);
        b.setTextColor(Color.WHITE);
        b.setBackgroundColor(Color.argb(220, 44, 48, 96));
        b.setPadding(4, 4, 4, 4);
        b.setOnClickListener(l);
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(0,
                ViewGroup.LayoutParams.WRAP_CONTENT, 1f);
        lp.setMargins(3, 4, 3, 4);
        b.setLayoutParams(lp);
        return b;
    }

    void setMode(int m) { mode = m; updateInfo(); }

    void updateInfo() {
        String m = mode == 0 ? "ставить" : mode == 1 ? "удалять" : "двигать вид";
        info.setText("режим: " + m + "   объект: " + brush.label
                + "   объектов: " + lv.objects.size()
                + "   длина: " + (int) (lv.rightEdge() / 30) + " клеток"
                + "   (два пальца — зум и прокрутка)");
    }

    void palette() {
        final List<String> names = new ArrayList<>();
        for (Palette.Item it : Palette.ITEMS) names.add(it.label);
        GdDialog.items(this, "ОБЪЕКТ", names.toArray(new String[0]), i -> {
            brush = Palette.ITEMS.get(i);
            mode = 0;
            updateInfo();
        });
    }

    void save() {
        lv.settings.lengthUnits = lv.rightEdge();
        Store.writeFile(new File(Store.levels(), Store.safe(lv.settings.localId) + ".gdl"), lv.toJson());
    }

    void test() {
        save();
        GameActivity.test(this, Level.parse(lv.toJson()));
    }

    void upload() {
        save();
        if (!Store.loggedIn()) { toast("Сначала войди в аккаунт"); return; }
        toast("Заливаю…");
        Api.async(() -> {
            int id = Api.uploadLevel(lv);
            runOnUiThread(() -> {
                if (id > 0) { lv.settings.serverId = id; save(); toast("Готово, ID #" + id); }
                else toast("Нет сети — уедет позже");
            });
        });
    }

    void toast(String s) { Toast.makeText(this, s, Toast.LENGTH_SHORT).show(); }

    @Override public void onBackPressed() { save(); finish(); }

    @Override public void finish() {
        super.finish();
        overridePendingTransition(0, 0);
    }

    // ------------------------------------------------------------ канва

    class EditorView extends View {
        float camX = 300, camY = 240, zoom = 1;
        float lastX, lastY, startDist;
        boolean pinching;

        EditorView(Context c) { super(c); }

        float scale() { return (float) (getHeight() / (480.0 / zoom)); }
        float sx(double wx) { return (float) ((wx - camX) * scale() + getWidth() / 2.0); }
        float sy(double wy) { return (float) (getHeight() / 2.0 - (wy - camY) * scale()); }
        float wx(float x) { return (x - getWidth() / 2f) / scale() + camX; }
        float wy(float y) { return (getHeight() / 2f - y) / scale() + camY; }

        @Override protected void onDraw(Canvas c) {
            c.drawColor(Color.rgb(16, 14, 40));

            // земля
            float g0 = sy(0);
            if (g0 < getHeight())
                c.drawRect(0, g0, getWidth(), getHeight(), Gd.fill(Color.rgb(24, 20, 56)));
            c.drawLine(0, g0, getWidth(), g0, Gd.stroke(Gd.NEON, 3f));

            // сетка
            float step = 30 * scale();
            if (step > 5) {
                Paint p1 = Gd.stroke(Color.argb(40, 255, 255, 255), 1f);
                Paint p2 = Gd.stroke(Color.argb(90, 255, 255, 255), 1f);
                float left = wx(0), right = wx(getWidth());
                for (double x = Math.floor(left / 30) * 30; x < right; x += 30) {
                    float X = sx(x);
                    c.drawLine(X, 0, X, getHeight(), (((int) x) % 240 == 0) ? p2 : p1);
                }
                float bot = wy(getHeight()), top = wy(0);
                for (double y = Math.floor(bot / 30) * 30; y < top; y += 30) {
                    float Y = sy(y);
                    c.drawLine(0, Y, getWidth(), Y, (((int) y) % 240 == 0) ? p2 : p1);
                }
            }

            // объекты
            float visL = wx(0) - 60, visR = wx(getWidth()) + 60;
            for (Level.Obj o : lv.objects) {
                Catalog.Def d = Catalog.get(o.typeId);
                if (o.x + d.w < visL || o.x > visR) continue;
                RectF r = new RectF(sx(o.x), sy(o.y + d.h * o.scaleY),
                        sx(o.x + d.w * o.scaleX), sy(o.y));
                drawObj(c, o, d, r);
            }

            // старт игрока
            float ps = 30 * scale();
            RectF pr = new RectF(sx(0) - ps / 2, sy(30), sx(0) + ps / 2, sy(0));
            c.drawRect(pr, Gd.stroke(Gd.LIME, 2f));
            Gd.outlined(c, "СТАРТ", pr.centerX(), pr.top - 6, Math.max(10f, ps * 0.3f), Gd.LIME, Paint.Align.CENTER);
        }

        void drawObj(Canvas c, Level.Obj o, Catalog.Def d, RectF r) {
            int col;
            switch (d.kind) {
                case Catalog.HAZARD: col = Color.rgb(255, 110, 110); break;
                case Catalog.PORTAL: col = GameView.portalLook(o.portal)[0]; break;
                case Catalog.ORB: col = GameView.orbColor(o.orb); break;
                case Catalog.PAD: col = GameView.padColor(o.pad); break;
                case Catalog.TRIGGER: col = Color.rgb(90, 150, 255); break;
                case Catalog.COIN: col = Gd.GOLD; break;
                case Catalog.FINISH: col = Color.WHITE; break;
                default: col = Color.rgb(210, 215, 240);
            }
            switch (d.style) {
                case Catalog.ST_SPIKE:
                case Catalog.ST_SPIKE_SMALL:
                case Catalog.ST_SPIKE_HALF: {
                    Path p = new Path();
                    if (o.typeId == 22) { p.moveTo(r.left, r.top); p.lineTo(r.right, r.top); p.lineTo(r.centerX(), r.bottom); }
                    else { p.moveTo(r.left, r.bottom); p.lineTo(r.right, r.bottom); p.lineTo(r.centerX(), r.top); }
                    p.close();
                    c.drawPath(p, Gd.fill(col));
                    break;
                }
                case Catalog.ST_ORB:
                    c.drawOval(r, Gd.stroke(col, Math.max(2f, r.width() * 0.16f)));
                    break;
                case Catalog.ST_PAD:
                    c.drawRect(r, Gd.fill(col));
                    break;
                case Catalog.ST_PORTAL:
                    c.drawOval(r, Gd.fill(col));
                    Gd.outlined(c, GameView.portalLabel(o.portal), r.centerX(), r.centerY(),
                            Math.max(8f, r.width() * 0.4f), Color.WHITE, Paint.Align.CENTER);
                    break;
                case Catalog.ST_COIN:
                    Gd.coin(c, r, true);
                    break;
                case Catalog.ST_TRIGGER:
                    c.drawRect(r, Gd.fill(Color.argb(170, 70, 130, 255)));
                    Gd.outlined(c, "T", r.centerX(), r.centerY() + r.height() * 0.2f,
                            Math.max(8f, r.width() * 0.5f), Color.WHITE, Paint.Align.CENTER);
                    break;
                case Catalog.ST_TEXT:
                    Gd.outlined(c, o.text, r.left, r.bottom, Math.max(10f, r.height() * 0.8f), col, Paint.Align.LEFT);
                    break;
                case Catalog.ST_FINISH:
                    c.drawRect(r, Gd.fill(Color.argb(200, 240, 240, 250)));
                    break;
                default:
                    c.drawRect(r, Gd.fill(col));
                    c.drawRect(r, Gd.stroke(Color.argb(140, 255, 255, 255), 1.5f));
            }
        }

        @Override public boolean onTouchEvent(MotionEvent e) {
            int act = e.getActionMasked();

            if (e.getPointerCount() >= 2) {
                float d = dist(e);
                if (act == MotionEvent.ACTION_POINTER_DOWN) { startDist = d; pinching = true; }
                else if (act == MotionEvent.ACTION_MOVE && pinching && startDist > 10) {
                    zoom = Math.max(0.25f, Math.min(3f, zoom * (d / startDist)));
                    startDist = d;
                    float cx = (e.getX(0) + e.getX(1)) / 2, cy = (e.getY(0) + e.getY(1)) / 2;
                    camX -= (cx - lastX) / scale();
                    camY += (cy - lastY) / scale();
                    lastX = cx; lastY = cy;
                    invalidate();
                }
                if (act == MotionEvent.ACTION_POINTER_DOWN) {
                    lastX = (e.getX(0) + e.getX(1)) / 2;
                    lastY = (e.getY(0) + e.getY(1)) / 2;
                }
                return true;
            }

            switch (act) {
                case MotionEvent.ACTION_DOWN:
                    pinching = false;
                    lastX = e.getX(); lastY = e.getY();
                    if (mode == 0) place(e.getX(), e.getY());
                    else if (mode == 1) erase(e.getX(), e.getY());
                    return true;

                case MotionEvent.ACTION_MOVE:
                    if (mode == 2) {
                        camX -= (e.getX() - lastX) / scale();
                        camY += (e.getY() - lastY) / scale();
                        lastX = e.getX(); lastY = e.getY();
                        invalidate();
                    } else if (mode == 0) place(e.getX(), e.getY());
                    else if (mode == 1) erase(e.getX(), e.getY());
                    return true;
            }
            return true;
        }

        float dist(MotionEvent e) {
            float dx = e.getX(0) - e.getX(1), dy = e.getY(0) - e.getY(1);
            return (float) Math.sqrt(dx * dx + dy * dy);
        }

        void place(float px, float py) {
            double gx = Math.floor(wx(px) / 30) * 30, gy = Math.floor(wy(py) / 30) * 30;
            for (Level.Obj o : lv.objects)
                if (Math.abs(o.x - gx) < 1 && Math.abs(o.y - gy) < 1 && o.typeId == brush.typeId) return;

            Level.Obj o = new Level.Obj();
            o.typeId = brush.typeId;
            o.x = gx; o.y = gy;
            o.portal = brush.portal;
            o.orb = brush.orb;
            o.pad = brush.pad;
            if (brush.typeId == 75) o.text = "Текст";
            if (brush.typeId == 100) {
                o.trig = new Level.Trig();
                o.trig.type = brush.trigType;
            }
            lv.objects.add(o);
            invalidate();
            updateInfo();
        }

        void erase(float px, float py) {
            float wxp = wx(px), wyp = wy(py);
            for (int i = lv.objects.size() - 1; i >= 0; i--) {
                Level.Obj o = lv.objects.get(i);
                Catalog.Def d = Catalog.get(o.typeId);
                if (wxp >= o.x && wxp <= o.x + d.w * o.scaleX && wyp >= o.y && wyp <= o.y + d.h * o.scaleY) {
                    lv.objects.remove(i);
                    invalidate();
                    updateInfo();
                    return;
                }
            }
        }
    }

    /** Палитра объектов редактора. */
    static class Palette {
        static class Item {
            int typeId, portal, orb, pad, trigType;
            String label;
            Item(int t, String l) { typeId = t; label = l; }
            Item portal(int p) { portal = p; return this; }
            Item orb(int o) { orb = o; return this; }
            Item pad(int p) { pad = p; return this; }
            Item trig(int t) { trigType = t; return this; }
        }

        static final List<Item> ITEMS = new ArrayList<>();

        static {
            ITEMS.add(new Item(1, "Блок"));
            ITEMS.add(new Item(8, "Кирпич"));
            ITEMS.add(new Item(9, "Стекло"));
            ITEMS.add(new Item(2, "Плита (низ)"));
            ITEMS.add(new Item(3, "Плита (верх)"));
            ITEMS.add(new Item(7, "Платформа"));
            ITEMS.add(new Item(5, "Склон вверх"));
            ITEMS.add(new Item(6, "Склон вниз"));

            ITEMS.add(new Item(20, "Шип"));
            ITEMS.add(new Item(21, "Малый шип"));
            ITEMS.add(new Item(22, "Шип с потолка"));
            ITEMS.add(new Item(27, "Половина шипа"));
            ITEMS.add(new Item(23, "Пила"));
            ITEMS.add(new Item(24, "Большая пила"));
            ITEMS.add(new Item(25, "Три шипа"));

            String[] orbNames = {"жёлтый", "розовый", "синий", "зелёный", "красный", "чёрный",
                    "рывок", "грави-рывок", "паук", "переключатель"};
            for (int i = 0; i < orbNames.length; i++)
                ITEMS.add(new Item(40, "Орб: " + orbNames[i]).orb(i));

            String[] padNames = {"жёлтый", "розовый", "синий", "красный", "паук"};
            for (int i = 0; i < padNames.length; i++)
                ITEMS.add(new Item(50, "Пад: " + padNames[i]).pad(i));

            String[] portalNames = {"", "куб", "корабль", "шар", "НЛО", "волна", "робот", "паук", "червяк",
                    "гравитация ↑", "гравитация ↓", "гравитация ⇅", "мини", "обычный",
                    "дуал", "одиночный", "зеркало", "обычный вид",
                    "0.5x", "1x", "2x", "3x", "4x", "5x", "6x", "телепорт"};
            for (int i = 1; i < portalNames.length; i++)
                ITEMS.add(new Item(60, "Портал: " + portalNames[i]).portal(i));

            ITEMS.add(new Item(80, "Монета"));
            ITEMS.add(new Item(82, "Пикап"));
            ITEMS.add(new Item(70, "Декор: квадрат"));
            ITEMS.add(new Item(71, "Декор: круг"));
            ITEMS.add(new Item(72, "Декор: треугольник"));
            ITEMS.add(new Item(75, "Текст"));
            ITEMS.add(new Item(92, "Чекпоинт"));
            ITEMS.add(new Item(90, "ФИНИШ"));

            int[] trigs = {Level.T_MOVE, Level.T_TOGGLE, Level.T_ALPHA, Level.T_PULSE, Level.T_COLOR,
                    Level.T_BG, Level.T_SHAKE, Level.T_ZOOM, Level.T_SONG, Level.T_SPEED, Level.T_END};
            String[] trigNames = {"Move", "Toggle", "Alpha", "Pulse", "Color", "BG", "Shake",
                    "Zoom", "Song", "Speed", "End"};
            for (int i = 0; i < trigs.length; i++)
                ITEMS.add(new Item(100, "Триггер: " + trigNames[i]).trig(trigs[i]));
        }
    }
}
