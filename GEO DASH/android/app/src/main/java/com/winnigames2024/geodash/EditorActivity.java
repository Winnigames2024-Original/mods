package com.winnigames2024.geodash;

import android.content.Context;
import android.graphics.*;
import android.view.View;

import java.io.File;
import java.util.UUID;

/** EDITOR — меню редактора: создать, свои уровни, копии, скачанное и песни. */
public class EditorActivity extends GdActivity {

    int mine, copies, published, saved, songs;
    Stats stats;

    @Override protected int bgA() { return Color.rgb(235, 130, 20); }
    @Override protected int bgId() { return 5; }
    @Override protected int groundId() { return 4; }
    @Override protected String headTitle() { return "EDITOR"; }
    @Override protected String headSub() { return "создать, изменить и опубликовать уровень"; }

    @Override protected void build() {
        stats = new Stats(this);
        root.addView(stats, Ui.lp(W, H, 0.14f, 0.175f, 0.72f, 0.085f));

        float bw = 0.28f, bh = 0.25f, gx = 0.025f, gy = 0.03f;
        float x0 = (1 - (bw * 3 + gx * 2)) / 2f, y0 = 0.29f;

        big("СОЗДАТЬ", "новый уровень", Ui.I_PLUS, Color.rgb(120, 255, 170), Color.rgb(20, 150, 100),
                x0, y0, bw, bh, this::create);
        big("МОИ УРОВНИ", "править и публиковать", Ui.I_EDIT, Color.rgb(255, 180, 70), Color.rgb(175, 95, 10),
                x0 + bw + gx, y0, bw, bh, () -> MyLevelsActivity.open(this, false));
        big("КОПИИ", "скопированные уровни", Ui.I_COPY, Color.rgb(200, 160, 255), Color.rgb(100, 55, 185),
                x0 + (bw + gx) * 2, y0, bw, bh, () -> MyLevelsActivity.open(this, true));

        float y1 = y0 + bh + gy;
        big("СОХРАНЁННЫЕ", "скачанные уровни", Ui.I_DOWNLOAD, Color.rgb(120, 230, 160), Color.rgb(30, 130, 80),
                x0, y1, bw, bh, () -> go(LevelListActivity.class, "mode", "saved"));
        big("МОИ ПЕСНИ", "что уже скачано", Ui.I_MUSIC, Color.rgb(255, 150, 215), Color.rgb(160, 40, 120),
                x0 + bw + gx, y1, bw, bh, () -> go(SongsActivity.class));
        big("ВСЕ УРОВНИ", "одним списком", Ui.I_SEARCH, Color.rgb(140, 200, 255), Color.rgb(35, 95, 190),
                x0 + (bw + gx) * 2, y1, bw, bh, () -> go(LevelListActivity.class, "mode", "my"));

        back(0.03f, 0.86f);
        count();
    }

    void big(String caption, String sub, int icon, int c1, int c2,
             float x, float y, float w, float h, Runnable click) {
        Ui.GdBtn b = btn("", icon, c1, c2, x, y, w, h, click);
        b.caption(caption).sub(sub);
    }

    @Override protected void onResume() {
        super.onResume();
        count();
    }

    void count() {
        try {
            mine = Store.myLevels(false).size();
            copies = Store.myLevels(true).size();
            published = 0;
            for (Level l : Store.myLevels()) if (l.settings.serverId > 0) published++;
            saved = Store.savedLevels().size();
            songs = SongLib.scan().size();
        } catch (Exception ignored) { }
        if (stats != null) stats.invalidate();
    }

    void create() {
        Level lv = new Level();
        lv.settings.name = "Уровень " + (Store.myLevels(false).size() + 1);
        lv.settings.author = Store.user();
        lv.settings.localId = UUID.randomUUID().toString().replace("-", "");
        Store.writeFile(new File(Store.levels(), Store.safe(lv.settings.localId) + ".gdl"), lv.toJson());
        EditorCanvasActivity.edit(this, lv);
    }

    class Stats extends View {
        Stats(Context c) { super(c); }

        @Override protected void onDraw(Canvas c) {
            RectF r = new RectF(0, 0, getWidth(), getHeight());
            Gd.glass(c, r, r.height() * 0.3f);
            String[] s = {
                    "своих: " + mine, "копий: " + copies, "опубликовано: " + published,
                    "скачано: " + saved, "песен: " + songs
            };
            float w = r.width() / s.length;
            float fs = Math.min(r.height() * 0.42f, w * 0.14f);
            for (int i = 0; i < s.length; i++)
                Gd.outlined(c, s[i], w * (i + 0.5f), r.centerY() + fs * 0.36f, fs,
                        i == 2 ? Gd.LIME : Gd.PAPER, Paint.Align.CENTER);
        }
    }
}
