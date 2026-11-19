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

/**
 * Редактор уровней по образцу игры: режимы BUILD / EDIT / DELETE,
 * палитра по вкладкам, панель правки со стрелками.
 * Объекты ставятся по одному касанию — постановки свайпом нет.
 */
public class EditorCanvasActivity extends Activity {

    static final int BUILD = 0, EDIT = 1, DELETE = 2;

    static Level editing;

    public static void edit(Context ctx, Level lv) {
        editing = lv;
        Intent i = new Intent(ctx, EditorCanvasActivity.class);
        i.addFlags(Intent.FLAG_ACTIVITY_NO_ANIMATION);
        ctx.startActivity(i);
        if (ctx instanceof Activity) ((Activity) ctx).overridePendingTransition(0, 0);
    }

    Level lv;
    FrameLayout root;
    EditorView view;
    LinearLayout panel;
    LinearLayout modes;
    TextView info;

    int mode = BUILD;
    Palette.Item brush;
    String category = "Блоки";
    /** Свайп-выделение: ведёшь пальцем — выделяется всё, по чему провёл. */
    boolean swipe = true;
    Builds.Piece piece;
    double step = 30;
    final List<Integer> sel = new ArrayList<>();
    final List<Level.Obj> clipboard = new ArrayList<>();
    final List<String> undo = new ArrayList<>();
    final List<String> redo = new ArrayList<>();
    int W, H;

    @Override protected void onCreate(Bundle b) {
        super.onCreate(b);
        requestWindowFeature(Window.FEATURE_NO_TITLE);
        getWindow().setFlags(WindowManager.LayoutParams.FLAG_FULLSCREEN, WindowManager.LayoutParams.FLAG_FULLSCREEN);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        Store.init(this);

        lv = editing;
        if (lv == null) { finish(); return; }
        brush = Palette.ITEMS.get(0);

        root = new FrameLayout(this);
        setContentView(root);
        root.post(this::buildUi);
    }

    void buildUi() {
        W = root.getWidth();
        H = root.getHeight();
        if (W == 0 || H == 0) { root.postDelayed(this::buildUi, 60); return; }

        root.removeAllViews();

        view = new EditorView(this);
        root.addView(view, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));

        LinearLayout top = new LinearLayout(this);
        top.setOrientation(LinearLayout.HORIZONTAL);
        top.setBackgroundColor(Color.argb(210, 14, 12, 36));
        root.addView(top, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, (int) (H * 0.12f)));

        topBtn(top, "ВЫХОД", Ui.I_BACK, Color.rgb(160, 160, 190), Color.rgb(70, 70, 110),
                () -> { save(); finish(); });
        topBtn(top, "СОХР.", Ui.I_SAVE, Color.rgb(120, 255, 170), Color.rgb(20, 150, 100),
                () -> { save(); toast("Сохранено"); });
        topBtn(top, "ОТМЕНА", Ui.I_BACK, Color.rgb(170, 175, 210), Color.rgb(70, 76, 125), this::doUndo);
        topBtn(top, "ВЕРНУТЬ", Ui.I_REFRESH, Color.rgb(170, 175, 210), Color.rgb(70, 76, 125), this::doRedo);
        topBtn(top, "ТЕСТ", Ui.I_PLAY, Color.rgb(120, 220, 255), Color.rgb(20, 110, 180), this::test);
        topBtn(top, "НАСТР.", Ui.I_SETTINGS, Color.rgb(200, 170, 255), Color.rgb(100, 60, 180),
                () -> LevelSettingsDialog.show(this, lv, this::save));
        topBtn(top, "СЕРВЕР", Ui.I_UPLOAD, Color.rgb(255, 210, 90), Color.rgb(170, 120, 10), this::upload);
        topBtn(top, "+", Ui.I_NONE, Color.rgb(140, 150, 190), Color.rgb(60, 66, 105),
                () -> { view.zoom = Math.min(3f, view.zoom * 1.25f); view.invalidate(); });
        topBtn(top, "−", Ui.I_NONE, Color.rgb(140, 150, 190), Color.rgb(60, 66, 105),
                () -> { view.zoom = Math.max(0.25f, view.zoom / 1.25f); view.invalidate(); });

        FrameLayout bottom = new FrameLayout(this);
        bottom.setBackgroundColor(Color.argb(235, 18, 16, 44));
        root.addView(bottom, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, (int) (H * 0.40f), Gravity.BOTTOM));

        modes = new LinearLayout(this);
        modes.setOrientation(LinearLayout.VERTICAL);
        modes.setBackgroundColor(Color.rgb(22, 20, 52));
        bottom.addView(modes, new FrameLayout.LayoutParams((int) (W * 0.11f),
                ViewGroup.LayoutParams.MATCH_PARENT, Gravity.START));

        panel = new LinearLayout(this);
        panel.setOrientation(LinearLayout.VERTICAL);
        FrameLayout.LayoutParams plp = new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT);
        plp.leftMargin = (int) (W * 0.11f);
        bottom.addView(panel, plp);

        info = new TextView(this);
        info.setTextColor(Gd.DIM);
        info.setTextSize(11);
        info.setBackgroundColor(Color.argb(170, 10, 8, 26));
        info.setPadding(14, 4, 14, 4);
        FrameLayout.LayoutParams ilp = new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT, Gravity.BOTTOM);
        ilp.bottomMargin = (int) (H * 0.40f);
        root.addView(info, ilp);

        setMode(BUILD);
        pushUndo();
    }

    void topBtn(LinearLayout bar, String text, int icon, int c1, int c2, Runnable r) {
        Ui.GdBtn b = new Ui.GdBtn(this).set(text, icon, c1, c2);
        b.setOnClickListener(v -> r.run());
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(0,
                ViewGroup.LayoutParams.MATCH_PARENT, 1f);
        lp.setMargins(4, 6, 4, 6);
        bar.addView(b, lp);
    }

    void modeButtons() {
        modes.removeAllViews();
        modeBtn("BUILD", Ui.I_PLUS, BUILD, Color.rgb(120, 255, 170), Color.rgb(20, 150, 100));
        modeBtn("EDIT", Ui.I_EDIT, EDIT, Color.rgb(255, 190, 80), Color.rgb(170, 100, 10));
        modeBtn("DELETE", Ui.I_TRASH, DELETE, Color.rgb(240, 120, 120), Color.rgb(150, 40, 50));
    }

    void modeBtn(String text, int icon, int m, int c1, int c2) {
        Ui.GdBtn b = new Ui.GdBtn(this).set(text, icon,
                mode == m ? c1 : Color.rgb(90, 95, 135), mode == m ? c2 : Color.rgb(44, 48, 82));
        b.setOnClickListener(v -> setMode(m));
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, 0, 1f);
        lp.setMargins(6, 6, 6, 6);
        modes.addView(b, lp);
    }

    void setMode(int m) {
        mode = m;
        Music.sfx("click");
        if (panel == null) return;
        panel.removeAllViews();
        if (m == BUILD) buildPalette();
        else if (m == EDIT) buildEditPanel();
        else buildDeletePanel();
        modeButtons();
        updateInfo();
        if (view != null) view.invalidate();
    }

    // ---------------------------------------------------- палитра

    void buildPalette() {
        HorizontalScrollView tabsScroll = new HorizontalScrollView(this);
        LinearLayout tabs = new LinearLayout(this);
        tabs.setOrientation(LinearLayout.HORIZONTAL);
        tabsScroll.addView(tabs);
        panel.addView(tabsScroll, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, (int) (H * 0.10f)));

        String[] cats = {"Блоки", "Шипы", "Порталы", "Шары и пады", "Декор", "Жидкости", "Сбор", "Триггеры", "Служебное", "Конструкции"};
        for (String c : cats) {
            final String cc = c;
            Ui.GdBtn b = new Ui.GdBtn(this).set(c, Ui.I_NONE,
                    c.equals(category) ? Color.rgb(120, 220, 255) : Color.rgb(70, 76, 125),
                    c.equals(category) ? Color.rgb(20, 110, 180) : Color.rgb(34, 38, 70));
            b.setOnClickListener(v -> { category = cc; setMode(BUILD); });
            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                    (int) (W * 0.14f), ViewGroup.LayoutParams.MATCH_PARENT);
            lp.setMargins(5, 6, 5, 4);
            tabs.addView(b, lp);
        }

        HorizontalScrollView itemsScroll = new HorizontalScrollView(this);
        LinearLayout items = new LinearLayout(this);
        items.setOrientation(LinearLayout.HORIZONTAL);
        itemsScroll.addView(items);
        panel.addView(itemsScroll, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));

        int size = (int) (H * 0.17f);
        if (category.equals("Конструкции")) {
            for (final Builds.Piece p : Builds.all()) {
                PieceBtn b = new PieceBtn(this, p);
                b.setOnClickListener(v -> { piece = p; toast("Конструкция «" + p.name + "» — коснись поля"); setMode(BUILD); });
                b.setOnLongClickListener(v -> {
                    GdDialog.confirm(this, "Конструкции", "Удалить «" + p.name + "»?", () -> {
                        Builds.delete(p.name);
                        if (piece != null && piece.name.equals(p.name)) piece = null;
                        setMode(BUILD);
                    });
                    return true;
                });
                LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams((int) (size * 1.3f), size);
                lp.setMargins(6, 8, 6, 8);
                items.addView(b, lp);
            }
            return;
        }
        for (final Palette.Item it : Palette.byCategory(category)) {
            ObjBtn b = new ObjBtn(this, it);
            b.setOnClickListener(v -> { brush = it; piece = null; setMode(BUILD); });
            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(size, size);
            lp.setMargins(6, 8, 6, 8);
            items.addView(b, lp);
        }
    }

    /** Кнопка палитры с превью целой конструкции. */
    class PieceBtn extends View {
        final Builds.Piece p;
        PieceBtn(Context c, Builds.Piece it) { super(c); p = it; setClickable(true); }

        @Override protected void onDraw(Canvas c) {
            RectF r = new RectF(3, 3, getWidth() - 3, getHeight() - 3);
            boolean on = piece != null && piece.name.equals(p.name);
            Gd.plate(c, r, on ? Color.rgb(70, 110, 220) : Color.rgb(40, 42, 86),
                    on ? Color.rgb(30, 60, 150) : Color.rgb(22, 22, 52), 14f);

            double minX = Double.MAX_VALUE, minY = Double.MAX_VALUE, maxX = -Double.MAX_VALUE, maxY = -Double.MAX_VALUE;
            for (Level.Obj o : p.objects) {
                Catalog.Def d = Catalog.get(o.typeId);
                minX = Math.min(minX, o.x); minY = Math.min(minY, o.y);
                maxX = Math.max(maxX, o.x + d.w); maxY = Math.max(maxY, o.y + d.h);
            }
            double w = Math.max(1, maxX - minX), h = Math.max(1, maxY - minY);
            RectF area = new RectF(r.left + 8, r.top + r.height() * 0.10f, r.right - 8, r.bottom - r.height() * 0.26f);
            float k = (float) Math.min(area.width() / w, area.height() / h);
            float ox = area.left + (area.width() - (float) (w * k)) / 2f;
            float oy = area.top + (area.height() - (float) (h * k)) / 2f;
            for (Level.Obj o : p.objects) {
                Catalog.Def d = Catalog.get(o.typeId);
                RectF box = new RectF(
                        ox + (float) ((o.x - minX) * k),
                        oy + (float) ((maxY - (o.y + d.h)) * k),
                        ox + (float) ((o.x - minX + d.w) * k),
                        oy + (float) ((maxY - o.y) * k));
                drawPreview(c, o, d, box);
            }
            Gd.outlined(c, p.name, r.centerX(), r.bottom - r.height() * 0.07f,
                    Math.min(r.height() * 0.14f, r.width() * 0.13f), Gd.PAPER, Paint.Align.CENTER);
        }
    }

    class ObjBtn extends View {
        final Palette.Item item;
        ObjBtn(Context c, Palette.Item it) { super(c); item = it; setClickable(true); }

        @Override protected void onDraw(Canvas c) {
            RectF r = new RectF(3, 3, getWidth() - 3, getHeight() - 3);
            boolean on = item == brush;
            Gd.plate(c, r, on ? Color.rgb(70, 110, 220) : Color.rgb(40, 42, 86),
                    on ? Color.rgb(30, 60, 150) : Color.rgb(22, 22, 52), 14f);

            Level.Obj o = item.make();
            Catalog.Def d = Catalog.get(o.typeId);
            float side = Math.min(r.width(), r.height()) * 0.5f;
            RectF box = new RectF(r.centerX() - side / 2, r.top + r.height() * 0.14f,
                    r.centerX() + side / 2, r.top + r.height() * 0.14f + side);
            drawPreview(c, o, d, box);

            Gd.outlined(c, item.label, r.centerX(), r.bottom - r.height() * 0.08f,
                    Math.min(r.height() * 0.15f, r.width() * 0.17f), Gd.PAPER, Paint.Align.CENTER);
        }
    }

    static void drawPreview(Canvas c, Level.Obj o, Catalog.Def d, RectF box) {
        switch (d.style) {
            case Catalog.ST_SPIKE: case Catalog.ST_SPIKE_SMALL: case Catalog.ST_SPIKE_HALF: {
                Path p = new Path();
                if (o.typeId == 22) { p.moveTo(box.left, box.top); p.lineTo(box.right, box.top); p.lineTo(box.centerX(), box.bottom); }
                else { p.moveTo(box.left, box.bottom); p.lineTo(box.right, box.bottom); p.lineTo(box.centerX(), box.top); }
                p.close();
                c.drawPath(p, Gd.fill(Color.rgb(255, 120, 120)));
                break;
            }
            case Catalog.ST_SPIKE_ROW: {
                float w3 = box.width() / 3f;
                for (int i = 0; i < 3; i++) {
                    Path p = new Path();
                    p.moveTo(box.left + i * w3, box.bottom);
                    p.lineTo(box.left + (i + 1) * w3, box.bottom);
                    p.lineTo(box.left + (i + 0.5f) * w3, box.top);
                    p.close();
                    c.drawPath(p, Gd.fill(Color.rgb(255, 120, 120)));
                }
                break;
            }
            case Catalog.ST_ORB:
                c.drawOval(box, Gd.stroke(GameView.orbColor(o.orb), Math.max(3f, box.width() * 0.16f)));
                break;
            case Catalog.ST_PAD:
                c.drawRect(new RectF(box.left, box.bottom - box.height() * 0.3f, box.right, box.bottom),
                        Gd.fill(GameView.padColor(o.pad)));
                break;
            case Catalog.ST_PORTAL: {
                int col = GameView.portalLook(o.portal)[0];
                RectF pr = new RectF(box.left + box.width() * 0.2f, box.top, box.right - box.width() * 0.2f, box.bottom);
                c.drawOval(pr, Gd.fill(col));
                Gd.outlined(c, GameView.portalLabel(o.portal), pr.centerX(), pr.centerY() + pr.height() * 0.1f,
                        Math.max(8f, pr.width() * 0.5f), Color.WHITE, Paint.Align.CENTER);
                break;
            }
            case Catalog.ST_COIN: Gd.coin(c, box, true); break;
            case Catalog.ST_PICKUP: {
                Path p = new Path();
                p.moveTo(box.centerX(), box.top); p.lineTo(box.right, box.centerY());
                p.lineTo(box.centerX(), box.bottom); p.lineTo(box.left, box.centerY());
                p.close();
                c.drawPath(p, Gd.fill(Color.rgb(120, 255, 180)));
                break;
            }
            case Catalog.ST_TRIGGER:
                c.drawRect(box, Gd.fill(Color.rgb(70, 130, 255)));
                Gd.outlined(c, o.trig == null ? "T" : shortTrig(o.trig.type), box.centerX(),
                        box.centerY() + box.height() * 0.15f, Math.max(8f, box.width() * 0.42f),
                        Color.WHITE, Paint.Align.CENTER);
                break;
            case Catalog.ST_SAW: case Catalog.ST_SAW_BIG:
                c.drawOval(box, Gd.fill(Color.rgb(200, 205, 225)));
                c.drawCircle(box.centerX(), box.centerY(), box.width() * 0.14f, Gd.fill(Color.rgb(50, 52, 74)));
                break;
            case Catalog.ST_HAZARD_BLOCK:
                c.drawRect(box, Gd.fill(Color.rgb(200, 60, 70)));
                break;
            case Catalog.ST_FINISH:
                c.drawRect(box, Gd.fill(Color.WHITE));
                c.drawRect(new RectF(box.left, box.centerY(), box.centerX(), box.bottom),
                        Gd.fill(Color.rgb(30, 30, 45)));
                break;
            case Catalog.ST_CHECKPOINT:
                c.drawRect(box, Gd.stroke(Color.rgb(120, 255, 160), 2f));
                break;
            case Catalog.ST_TEXT:
                Gd.outlined(c, "Aa", box.centerX(), box.centerY() + box.height() * 0.2f,
                        box.height() * 0.7f, Gd.PAPER, Paint.Align.CENTER);
                break;
            case Catalog.ST_CIRCLE:
                c.drawOval(box, Gd.fill(Color.rgb(150, 160, 230)));
                break;
            case Catalog.ST_TRI: {
                Path p = new Path();
                p.moveTo(box.left, box.bottom); p.lineTo(box.right, box.bottom); p.lineTo(box.centerX(), box.top);
                p.close();
                c.drawPath(p, Gd.fill(Color.rgb(150, 160, 230)));
                break;
            }
            case Catalog.ST_WATER:
            case Catalog.ST_LAVA: {
                boolean lava = d.style == Catalog.ST_LAVA;
                Paint lp = new Paint(Paint.ANTI_ALIAS_FLAG);
                lp.setShader(new LinearGradient(0, box.top, 0, box.bottom,
                        lava ? Color.rgb(255, 150, 40) : Color.rgb(90, 190, 255),
                        lava ? Color.rgb(170, 30, 10) : Color.rgb(20, 70, 180), Shader.TileMode.CLAMP));
                c.drawRect(box, lp);
                c.drawLine(box.left, box.top + 3, box.right, box.top + 3,
                        Gd.stroke(lava ? Color.rgb(255, 220, 120) : Color.rgb(200, 240, 255), 2.4f));
                break;
            }
            case Catalog.ST_GLOW:
                c.drawOval(box, Gd.fill(Color.argb(120, 160, 200, 255)));
                break;
            case Catalog.ST_SLAB: case Catalog.ST_PLATFORM:
                c.drawRect(new RectF(box.left, box.centerY(), box.right, box.bottom),
                        Gd.fill(Color.rgb(225, 228, 248)));
                break;
            case Catalog.ST_SLOPE_UP: case Catalog.ST_SLOPE_DOWN: {
                Path p = new Path();
                if (d.style == Catalog.ST_SLOPE_UP) {
                    p.moveTo(box.left, box.bottom); p.lineTo(box.right, box.bottom); p.lineTo(box.right, box.top);
                } else {
                    p.moveTo(box.left, box.bottom); p.lineTo(box.right, box.bottom); p.lineTo(box.left, box.top);
                }
                p.close();
                c.drawPath(p, Gd.fill(Color.rgb(225, 228, 248)));
                break;
            }
            default:
                c.drawRect(box, Gd.fill(Color.rgb(225, 228, 248)));
                c.drawRect(box, Gd.stroke(Color.rgb(120, 125, 170), 2f));
        }
    }

    static String shortTrig(int t) {
        switch (t) {
            case Level.T_MOVE: return "MOV";
            case Level.T_ROTATE: return "ROT";
            case Level.T_SCALE: return "SCL";
            case Level.T_ALPHA: return "ALP";
            case Level.T_TOGGLE: return "TGL";
            case Level.T_SPAWN: return "SPW";
            case Level.T_STOP: return "STP";
            case Level.T_PULSE: return "PLS";
            case Level.T_COLOR: return "COL";
            case Level.T_BG: return "BG";
            case Level.T_GROUND: return "GRD";
            case Level.T_SHAKE: return "SHK";
            case Level.T_FOLLOW: return "FLW";
            case Level.T_COUNT: return "CNT";
            case Level.T_PICKUP: return "PCK";
            case Level.T_RANDOM: return "RND";
            case Level.T_TIMER: return "TMR";
            case Level.T_CAMERA: return "CAM";
            case Level.T_ZOOM: return "ZOM";
            case Level.T_SONG: return "SNG";
            case Level.T_GRAVITY: return "GRV";
            case Level.T_TELEPORT: return "TPT";
            case Level.T_SPEED: return "SPD";
            case Level.T_END: return "END";
            default: return "TRG";
        }
    }

    // ---------------------------------------------------- панели режимов

    void buildEditPanel() {
        LinearLayout row1 = row();
        LinearLayout row2 = row();

        pBtn(row1, "", Ui.I_BACK, () -> moveSel(-step, 0));
        pBtn(row1, "", Ui.I_UPLOAD, () -> moveSel(0, step));
        pBtn(row1, "", Ui.I_DOWNLOAD, () -> moveSel(0, -step));
        pBtn(row1, "", Ui.I_PLAY, () -> moveSel(step, 0));
        pBtn(row1, "↺", Ui.I_NONE, () -> rotateSel(-90));
        pBtn(row1, "↻", Ui.I_NONE, () -> rotateSel(90));
        pBtn(row1, "⇄", Ui.I_NONE, () -> flipSel(true));
        pBtn(row1, "⇅", Ui.I_NONE, () -> flipSel(false));

        for (final double v : new double[]{15, 30, 150}) {
            boolean on = Math.abs(step - v) < 0.01;
            Ui.GdBtn b = new Ui.GdBtn(this).set(v == 15 ? "0.5" : v == 30 ? "1" : "5", Ui.I_NONE,
                    on ? Color.rgb(120, 220, 255) : Color.rgb(150, 158, 200),
                    on ? Color.rgb(20, 110, 180) : Color.rgb(60, 66, 110));
            b.setOnClickListener(x -> { step = v; setMode(EDIT); });
            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.MATCH_PARENT, 1f);
            lp.setMargins(5, 6, 5, 6);
            row2.addView(b, lp);
        }

        pBtn(row2, "КОПИЯ", Ui.I_COPY, this::copySel);
        pBtn(row2, "ВСТАВИТЬ", Ui.I_PLUS, this::pasteSel);
        pBtn(row2, "УДАЛИТЬ", Ui.I_TRASH, this::deleteSel);
        pBtn(row2, "ОБЪЕКТ", Ui.I_SETTINGS, this::editObject);
        pBtn(row2, "СНЯТЬ", Ui.I_CROSS, sel::clear);
        pBtn(row2, "СОХР. КОНСТР.", Ui.I_SAVE, this::savePiece);
        pBtn(row2, swipe ? "СВАЙП: ВКЛ" : "СВАЙП: ВЫКЛ", Ui.I_EDIT, () -> {
            swipe = !swipe;
            toast(swipe ? "Свайп: веди пальцем — выделяется всё под ним" : "Свайп выключен");
            setMode(EDIT);
        });
    }

    void buildDeletePanel() {
        LinearLayout row1 = row();
        pBtn(row1, "УДАЛИТЬ ВЫДЕЛЕННОЕ", Ui.I_TRASH, this::deleteSel);
        pBtn(row1, "УДАЛИТЬ ТАКИЕ ЖЕ", Ui.I_TRASH, () -> {
            if (sel.isEmpty()) return;
            int type = lv.objects.get(sel.get(0)).typeId;
            pushUndo();
            for (int i = lv.objects.size() - 1; i >= 0; i--)
                if (lv.objects.get(i).typeId == type) lv.objects.remove(i);
            sel.clear();
        });
        pBtn(row1, "ОЧИСТИТЬ", Ui.I_CROSS, () ->
                GdDialog.confirm(this, "Очистка", "Удалить все объекты уровня?", () -> {
                    pushUndo();
                    lv.objects.clear();
                    sel.clear();
                    view.invalidate();
                    updateInfo();
                }));

        TextView t = new TextView(this);
        t.setText("В режиме DELETE касание по объекту удаляет его.\n"
                + "Стирания свайпом нет — объекты удаляются по одному.");
        t.setTextColor(Gd.DIM);
        t.setPadding(20, 10, 20, 10);
        panel.addView(t);
    }

    LinearLayout row() {
        LinearLayout r = new LinearLayout(this);
        r.setOrientation(LinearLayout.HORIZONTAL);
        panel.addView(r, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, 0, 1f));
        return r;
    }

    void pBtn(LinearLayout row, String text, int icon, Runnable r) {
        Ui.GdBtn b = new Ui.GdBtn(this).set(text, icon, Color.rgb(150, 158, 200), Color.rgb(60, 66, 110));
        b.setOnClickListener(v -> { r.run(); if (view != null) view.invalidate(); updateInfo(); });
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.MATCH_PARENT, 1f);
        lp.setMargins(5, 6, 5, 6);
        row.addView(b, lp);
    }

    // ---------------------------------------------------- операции

    void moveSel(double dx, double dy) {
        if (sel.isEmpty()) return;
        pushUndo();
        for (int i : sel) { lv.objects.get(i).x += dx; lv.objects.get(i).y += dy; }
    }

    void rotateSel(double deg) {
        if (sel.isEmpty()) return;
        pushUndo();
        for (int i : sel) lv.objects.get(i).rotation = (lv.objects.get(i).rotation + deg) % 360;
    }

    void flipSel(boolean horizontal) {
        if (sel.isEmpty()) return;
        pushUndo();
        for (int i : sel) {
            Level.Obj o = lv.objects.get(i);
            if (horizontal) { o.flipX = !o.flipX; o.scaleX = -o.scaleX; }
            else { o.flipY = !o.flipY; o.scaleY = -o.scaleY; }
        }
    }

    /** Сохранить выделенное как свою конструкцию. */
    void savePiece() {
        if (sel.isEmpty()) { toast("Сначала выдели объекты — свайпом"); return; }
        final java.util.List<Level.Obj> objs = new ArrayList<>();
        for (int i : sel) if (i >= 0 && i < lv.objects.size()) objs.add(lv.objects.get(i));
        GdDialog.input(this, "Название конструкции", "", "Конструкция " + (Builds.all().size() + 1), false, name -> {
            if (name == null || name.trim().isEmpty()) return;
            Builds.Piece p = Builds.save(name.trim(), objs);
            if (p == null) { toast("Не вышло сохранить"); return; }
            piece = p;
            toast("Сохранено — вкладка «Конструкции» в BUILD");
        });
    }

    void copySel() {
        clipboard.clear();
        for (int i : sel) clipboard.add(copyObj(lv.objects.get(i)));
        toast("Скопировано: " + clipboard.size());
    }

    void pasteSel() {
        if (clipboard.isEmpty()) return;
        pushUndo();
        sel.clear();
        for (Level.Obj o : clipboard) {
            Level.Obj c = copyObj(o);
            c.x += 30;
            lv.objects.add(c);
            sel.add(lv.objects.size() - 1);
        }
    }

    static Level.Obj copyObj(Level.Obj o) {
        Level.Obj c = new Level.Obj();
        c.typeId = o.typeId; c.x = o.x; c.y = o.y; c.rotation = o.rotation;
        c.scaleX = o.scaleX; c.scaleY = o.scaleY; c.flipX = o.flipX; c.flipY = o.flipY;
        c.zOrder = o.zOrder; c.baseColor = o.baseColor; c.detailColor = o.detailColor;
        c.opacity = o.opacity; c.portal = o.portal; c.orb = o.orb; c.pad = o.pad;
        c.coinIndex = o.coinIndex; c.text = o.text; c.groups = o.groups.clone();
        c.trig = o.trig;
        return c;
    }

    void deleteSel() {
        if (sel.isEmpty()) return;
        pushUndo();
        List<Integer> sorted = new ArrayList<>(sel);
        java.util.Collections.sort(sorted, java.util.Collections.reverseOrder());
        for (int i : sorted) if (i >= 0 && i < lv.objects.size()) lv.objects.remove(i);
        sel.clear();
    }

    void editObject() {
        if (sel.isEmpty()) { toast("Сначала выдели объект"); return; }
        final Level.Obj o = lv.objects.get(sel.get(0));
        final EditText[] f = new EditText[2];

        LinearLayout box = GdDialog.custom(this, "ПРАВКА ОБЪЕКТА", "ПРИМЕНИТЬ", () -> {
            pushUndo();
            List<Integer> groups = new ArrayList<>();
            for (String part : f[0].getText().toString().split(",")) {
                try { int v = Integer.parseInt(part.trim()); if (v > 0) groups.add(v); } catch (Exception ignored) { }
            }
            int[] arr = new int[groups.size()];
            for (int i = 0; i < arr.length; i++) arr[i] = groups.get(i);
            double rot = 0;
            try { rot = Double.parseDouble(f[1].getText().toString().trim()); } catch (Exception ignored) { }
            for (int i : sel) {
                Level.Obj t = lv.objects.get(i);
                t.groups = arr.clone();
                t.rotation = rot;
            }
            view.invalidate();
            updateInfo();
        });

        f[0] = LevelSettingsDialog.field(this, box, "группы (через запятую)", join(o.groups), false);
        f[1] = LevelSettingsDialog.field(this, box, "поворот, градусы", String.valueOf((int) o.rotation), true);

        if (o.trig != null) {
            Ui.GdBtn t = new Ui.GdBtn(this).set("ТИП ТРИГГЕРА", Ui.I_ADMIN,
                    Color.rgb(120, 190, 255), Color.rgb(30, 90, 190));
            t.setOnClickListener(v -> GdDialog.items(this, "ТРИГГЕР", Level.TRIGS, i -> {
                pushUndo();
                for (int k : sel) {
                    Level.Obj x = lv.objects.get(k);
                    if (x.trig != null) x.trig.type = i;
                }
                view.invalidate();
            }));
            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT, 110);
            lp.setMargins(0, 14, 0, 0);
            box.addView(t, lp);
        }
    }

    static String join(int[] a) {
        StringBuilder b = new StringBuilder();
        for (int i = 0; i < a.length; i++) { if (i > 0) b.append(','); b.append(a[i]); }
        return b.toString();
    }

    // ---------------------------------------------------- прочее

    void pushUndo() {
        undo.add(lv.toJson());
        if (undo.size() > 60) undo.remove(0);
        redo.clear();
    }

    void doUndo() {
        if (undo.size() < 2) return;
        redo.add(lv.toJson());
        undo.remove(undo.size() - 1);
        lv = Level.parse(undo.get(undo.size() - 1));
        editing = lv;
        sel.clear();
        view.invalidate();
        updateInfo();
    }

    void doRedo() {
        if (redo.isEmpty()) return;
        undo.add(lv.toJson());
        lv = Level.parse(redo.remove(redo.size() - 1));
        editing = lv;
        sel.clear();
        view.invalidate();
        updateInfo();
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

    void updateInfo() {
        if (info == null) return;
        String m = mode == BUILD ? "ставить" : mode == EDIT ? "правка" : "удалять";
        info.setText("режим: " + m + "   объект: " + brush.label
                + "   выделено: " + sel.size()
                + "   объектов: " + lv.objects.size()
                + "   длина: " + (int) (lv.rightEdge() / 30) + " клеток"
                + "   шаг: " + (step / 30) + "   (два пальца — зум и прокрутка)");
    }

    void toast(String s) { Toast.makeText(this, s, Toast.LENGTH_SHORT).show(); }

    @Override public void onBackPressed() { save(); finish(); }

    @Override public void finish() {
        super.finish();
        overridePendingTransition(0, 0);
    }

    // ---------------------------------------------------- канва

    class EditorView extends View {
        float camX = 300, camY = 240, zoom = 1;
        float lastX, lastY, startDist;
        boolean pinching, movingSel;
        double dragX, dragY;

        EditorView(Context c) { super(c); }

        float scale() { return (float) (getHeight() / (480.0 / zoom)); }
        float sx(double wx) { return (float) ((wx - camX) * scale() + getWidth() / 2.0); }
        float sy(double wy) { return (float) (getHeight() / 2.0 - (wy - camY) * scale()); }
        float wx(float x) { return (x - getWidth() / 2f) / scale() + camX; }
        float wy(float y) { return (getHeight() / 2f - y) / scale() + camY; }

        @Override protected void onDraw(Canvas c) {
            c.drawColor(Color.rgb(16, 14, 40));

            float g0 = sy(0);
            if (g0 < getHeight()) c.drawRect(0, g0, getWidth(), getHeight(), Gd.fill(Color.rgb(24, 20, 56)));
            c.drawLine(0, g0, getWidth(), g0, Gd.stroke(Gd.NEON, 3f));

            float step30 = 30 * scale();
            if (step30 > 5) {
                Paint p1 = Gd.stroke(Color.argb(38, 255, 255, 255), 1f);
                Paint p2 = Gd.stroke(Color.argb(85, 255, 255, 255), 1f);
                for (double x = Math.floor(wx(0) / 30) * 30; x < wx(getWidth()); x += 30) {
                    float X = sx(x);
                    c.drawLine(X, 0, X, getHeight(), (((int) x) % 240 == 0) ? p2 : p1);
                }
                for (double y = Math.floor(wy(getHeight()) / 30) * 30; y < wy(0); y += 30) {
                    float Y = sy(y);
                    c.drawLine(0, Y, getWidth(), Y, (((int) y) % 240 == 0) ? p2 : p1);
                }
            }

            float visL = wx(0) - 60, visR = wx(getWidth()) + 60;
            for (int i = 0; i < lv.objects.size(); i++) {
                Level.Obj o = lv.objects.get(i);
                Catalog.Def d = Catalog.get(o.typeId);
                if (o.x + d.w < visL || o.x > visR) continue;
                RectF r = new RectF(sx(o.x), sy(o.y + d.h * Math.abs(o.scaleY)),
                        sx(o.x + d.w * Math.abs(o.scaleX)), sy(o.y));
                drawPreview(c, o, d, r);
                if (sel.contains(i)) c.drawRect(r, Gd.stroke(Gd.GOLD, 3f));
            }

            float ps = 30 * scale();
            RectF pr = new RectF(sx(0), sy(30), sx(0) + ps, sy(0));
            c.drawRect(pr, Gd.stroke(Gd.LIME, 2f));
            Gd.outlined(c, "СТАРТ", pr.centerX(), pr.top - 8, Math.max(10f, ps * 0.3f), Gd.LIME, Paint.Align.CENTER);
        }

        @Override public boolean onTouchEvent(MotionEvent e) {
            int act = e.getActionMasked();

            if (e.getPointerCount() >= 2) {
                float d = dist(e);
                float cx = (e.getX(0) + e.getX(1)) / 2, cy = (e.getY(0) + e.getY(1)) / 2;
                if (act == MotionEvent.ACTION_POINTER_DOWN) { startDist = d; pinching = true; lastX = cx; lastY = cy; }
                else if (act == MotionEvent.ACTION_MOVE && pinching && startDist > 10) {
                    zoom = Math.max(0.25f, Math.min(3f, zoom * (d / startDist)));
                    startDist = d;
                    camX -= (cx - lastX) / scale();
                    camY += (cy - lastY) / scale();
                    lastX = cx; lastY = cy;
                    invalidate();
                }
                return true;
            }

            float wxp = wx(e.getX()), wyp = wy(e.getY());

            switch (act) {
                case MotionEvent.ACTION_DOWN:
                    pinching = false;
                    movingSel = false;
                    lastX = e.getX(); lastY = e.getY();
                    dragX = wxp; dragY = wyp;

                    if (mode == BUILD) place(wxp, wyp);          // одно касание — один объект
                    else if (mode == DELETE) erase(wxp, wyp);
                    else {
                        int hit = hitTest(wxp, wyp);
                        if (hit >= 0) {
                            if (!sel.contains(hit)) { sel.clear(); sel.add(hit); }
                            movingSel = true;
                        } else {
                            sel.clear();
                            swiping = swipe;       // ведём пальцем по пустому — выделяем росчерком
                        }
                        invalidate();
                        updateInfo();
                    }
                    return true;

                case MotionEvent.ACTION_MOVE:
                    if (mode == EDIT && swiping) {
                        int under = hitTest(wxp, wyp);
                        if (under >= 0 && !sel.contains(under)) { sel.add(under); updateInfo(); }
                        invalidate();
                        return true;
                    }
                    if (mode == EDIT && movingSel && !sel.isEmpty()) {
                        double dx = Math.round((wxp - dragX) / 30) * 30.0;
                        double dy = Math.round((wyp - dragY) / 30) * 30.0;
                        if (dx != 0 || dy != 0) {
                            for (int i : sel) { lv.objects.get(i).x += dx; lv.objects.get(i).y += dy; }
                            dragX += dx; dragY += dy;
                            invalidate();
                        }
                    } else {
                        // перетаскивание двигает вид — объекты свайпом не ставятся и не стираются
                        camX -= (e.getX() - lastX) / scale();
                        camY += (e.getY() - lastY) / scale();
                        lastX = e.getX(); lastY = e.getY();
                        invalidate();
                    }
                    return true;

                case MotionEvent.ACTION_UP:
                    if (movingSel) pushUndo();
                    movingSel = false;
                    swiping = false;
                    return true;
            }
            return true;
        }

        float dist(MotionEvent e) {
            float dx = e.getX(0) - e.getX(1), dy = e.getY(0) - e.getY(1);
            return (float) Math.sqrt(dx * dx + dy * dy);
        }

        boolean swiping;

        int hitTest(float wxp, float wyp) {
            for (int i = lv.objects.size() - 1; i >= 0; i--) {
                Level.Obj o = lv.objects.get(i);
                Catalog.Def d = Catalog.get(o.typeId);
                if (wxp >= o.x && wxp <= o.x + d.w * Math.abs(o.scaleX)
                        && wyp >= o.y && wyp <= o.y + d.h * Math.abs(o.scaleY)) return i;
            }
            return -1;
        }

        void place(float px, float py) {
            double gx = Math.floor(px / 30) * 30, gy = Math.floor(py / 30) * 30;
            if (category.equals("Конструкции") && piece != null) {
                pushUndo();
                sel.clear();
                for (Level.Obj o : Builds.stamp(piece, gx, gy)) {
                    lv.objects.add(o);
                    sel.add(lv.objects.size() - 1);
                }
                Music.sfx("click");
                invalidate();
                updateInfo();
                return;
            }
            for (Level.Obj o : lv.objects)
                if (Math.abs(o.x - gx) < 1 && Math.abs(o.y - gy) < 1 && o.typeId == brush.typeId) return;
            pushUndo();
            Level.Obj o = brush.make();
            o.x = gx; o.y = gy;
            lv.objects.add(o);
            Music.sfx("click");
            invalidate();
            updateInfo();
        }

        void erase(float px, float py) {
            int hit = hitTest(px, py);
            if (hit >= 0) {
                pushUndo();
                lv.objects.remove(hit);
                sel.clear();
                invalidate();
                updateInfo();
            }
        }
    }

    // ---------------------------------------------------- палитра объектов

    static class Palette {
        static class Item {
            int typeId, portal, orb, pad, trigType = -1;
            String label, category;

            Item(int t, String l, String cat) { typeId = t; label = l; category = cat; }
            Item portal(int p) { portal = p; return this; }
            Item orb(int o) { orb = o; return this; }
            Item pad(int p) { pad = p; return this; }
            Item trig(int t) { trigType = t; return this; }

            Level.Obj make() {
                Level.Obj o = new Level.Obj();
                o.typeId = typeId; o.portal = portal; o.orb = orb; o.pad = pad;
                if (typeId == 75) o.text = "Текст";
                if (trigType >= 0) { o.trig = new Level.Trig(); o.trig.type = trigType; }
                return o;
            }
        }

        static final List<Item> ITEMS = new ArrayList<>();

        static List<Item> byCategory(String cat) {
            List<Item> r = new ArrayList<>();
            for (Item i : ITEMS) if (i.category.equals(cat)) r.add(i);
            return r;
        }

        static {
            ITEMS.add(new Item(1, "блок", "Блоки"));
            ITEMS.add(new Item(8, "кирпич", "Блоки"));
            ITEMS.add(new Item(9, "стекло", "Блоки"));
            ITEMS.add(new Item(2, "плита н", "Блоки"));
            ITEMS.add(new Item(3, "плита в", "Блоки"));
            ITEMS.add(new Item(4, "тонкая", "Блоки"));
            ITEMS.add(new Item(7, "платформа", "Блоки"));
            ITEMS.add(new Item(5, "склон ↗", "Блоки"));
            ITEMS.add(new Item(6, "склон ↘", "Блоки"));

            ITEMS.add(new Item(94, "вода", "Жидкости"));
            ITEMS.add(new Item(95, "вода XL", "Жидкости"));
            ITEMS.add(new Item(96, "лава", "Жидкости"));
            ITEMS.add(new Item(97, "лава XL", "Жидкости"));

            ITEMS.add(new Item(20, "шип", "Шипы"));
            ITEMS.add(new Item(21, "малый", "Шипы"));
            ITEMS.add(new Item(22, "потолок", "Шипы"));
            ITEMS.add(new Item(27, "половина", "Шипы"));
            ITEMS.add(new Item(23, "пила", "Шипы"));
            ITEMS.add(new Item(24, "пила XL", "Шипы"));
            ITEMS.add(new Item(25, "три шипа", "Шипы"));
            ITEMS.add(new Item(26, "опасный", "Шипы"));

            String[] orbNames = {"жёлтый", "розовый", "синий", "зелёный", "красный", "чёрный",
                    "рывок", "гр-рывок", "паук", "перекл."};
            for (int i = 0; i < orbNames.length; i++)
                ITEMS.add(new Item(40, orbNames[i], "Шары и пады").orb(i));
            String[] padNames = {"пад жёлт", "пад роз", "пад син", "пад красн", "пад паук"};
            for (int i = 0; i < padNames.length; i++)
                ITEMS.add(new Item(50, padNames[i], "Шары и пады").pad(i));

            String[] portalNames = {"", "куб", "корабль", "шар", "НЛО", "волна", "робот", "паук", "червяк",
                    "грав ↑", "грав ↓", "грав ⇅", "мини", "обычный", "дуал", "один", "зеркало", "норм",
                    "0.5x", "1x", "2x", "3x", "4x", "5x", "6x", "телепорт"};
            for (int i = 1; i < portalNames.length; i++)
                ITEMS.add(new Item(60, portalNames[i], "Порталы").portal(i));

            ITEMS.add(new Item(70, "квадрат", "Декор"));
            ITEMS.add(new Item(71, "круг", "Декор"));
            ITEMS.add(new Item(72, "треуг.", "Декор"));
            ITEMS.add(new Item(73, "свечение", "Декор"));
            ITEMS.add(new Item(75, "текст", "Декор"));

            ITEMS.add(new Item(80, "монета", "Сбор"));
            ITEMS.add(new Item(82, "пикап", "Сбор"));

            ITEMS.add(new Item(90, "ФИНИШ", "Служебное"));
            ITEMS.add(new Item(92, "чекпоинт", "Служебное"));

            int[] trigs = {Level.T_MOVE, Level.T_ROTATE, Level.T_SCALE, Level.T_ALPHA, Level.T_TOGGLE,
                    Level.T_SPAWN, Level.T_STOP, Level.T_PULSE, Level.T_COLOR, Level.T_BG, Level.T_GROUND,
                    Level.T_SHAKE, Level.T_FOLLOW, Level.T_COUNT, Level.T_PICKUP, Level.T_RANDOM,
                    Level.T_TIMER, Level.T_CAMERA, Level.T_ZOOM, Level.T_SONG, Level.T_GRAVITY,
                    Level.T_TELEPORT, Level.T_SPEED, Level.T_END};
            for (int t : trigs)
                ITEMS.add(new Item(100, shortTrig(t), "Триггеры").trig(t));
        }
    }
}
