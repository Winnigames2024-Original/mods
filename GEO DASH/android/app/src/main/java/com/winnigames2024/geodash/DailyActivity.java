package com.winnigames2024.geodash;

import android.content.Context;
import android.graphics.*;
import android.view.View;

import org.json.JSONObject;

/** DAILY / WEEKLY. */
public class DailyActivity extends GdActivity {

    JSONObject daily, weekly;
    boolean weeklyTab;
    CardView card;

    @Override protected int bgA() { return Color.rgb(160, 60, 220); }
    @Override protected int bgId() { return 16; }
    @Override protected int groundId() { return 7; }
    @Override protected String headTitle() { return weeklyTab ? "УРОВЕНЬ НЕДЕЛИ" : "УРОВЕНЬ ДНЯ"; }
    @Override protected String headSub() { return "качается автоматически и играется без интернета"; }

    @Override protected void build() {
        weeklyTab = "weekly".equals(getIntent().getStringExtra("tab"));

        btn("DAILY", Ui.I_DAILY, Color.rgb(120, 200, 255), Color.rgb(30, 90, 190),
                0.08f, 0.20f, 0.18f, 0.12f, () -> { weeklyTab = false; refresh(); });
        btn("WEEKLY", Ui.I_WEEKLY, Color.rgb(220, 150, 255), Color.rgb(120, 50, 190),
                0.28f, 0.20f, 0.18f, 0.12f, () -> { weeklyTab = true; refresh(); });

        card = new CardView(this);
        root.addView(card, Ui.lp(W, H, 0.08f, 0.35f, 0.84f, 0.34f));

        btn("ИГРАТЬ", Ui.I_PLAY, Color.rgb(120, 255, 170), Color.rgb(20, 150, 100),
                0.08f, 0.72f, 0.22f, 0.13f, this::play);
        btn("СОХРАНИТЬ", Ui.I_DOWNLOAD, Color.rgb(120, 220, 255), Color.rgb(20, 110, 180),
                0.33f, 0.72f, 0.22f, 0.13f, this::save);
        back(0.80f, 0.72f);

        Api.async(() -> {
            daily = Api.daily();
            weekly = Api.weekly();
            ui.post(this::refresh);
        });
    }

    void refresh() {
        if (card != null) card.invalidate();
        if (header != null) header.invalidate();
    }

    JSONObject cur() { return weeklyTab ? weekly : daily; }

    JSONObject curLevel() {
        JSONObject d = cur();
        return d == null ? null : d.optJSONObject("level");
    }

    void play() {
        JSONObject l = curLevel();
        if (l == null) { toast("Уровень не назначен"); return; }
        final int id = l.optInt("id");
        toast("Скачиваю…");
        Api.async(() -> {
            Level lv = Api.downloadLevel(id);
            if (lv == null && Store.isSaved(id)) lv = Store.loadSaved(id);
            final Level f = lv;
            ui.post(() -> {
                if (f == null) toast("Нет сети и уровень не сохранён");
                else GameActivity.play(this, f, false);
            });
        });
    }

    void save() {
        JSONObject l = curLevel();
        if (l == null) return;
        final int id = l.optInt("id");
        Api.async(() -> {
            Api.downloadLevel(id);
            ui.post(() -> { toast("Сохранено офлайн"); refresh(); });
        });
    }

    class CardView extends View {
        CardView(Context c) { super(c); }

        @Override protected void onDraw(Canvas c) {
            RectF r = new RectF(0, 0, getWidth(), getHeight());
            Gd.plate(c, r, Color.rgb(46, 34, 92), Color.rgb(22, 16, 52), 22f);

            JSONObject d = cur();
            JSONObject l = curLevel();
            if (l == null) {
                Gd.outlined(c, "Уровень не назначен (или нет связи)", r.centerX(), r.centerY(),
                        r.height() * 0.11f, Gd.GOLD, Paint.Align.CENTER);
                return;
            }

            float fs = r.height() * 0.42f;
            RectF face = new RectF(r.left + r.width() * 0.03f, r.top + r.height() * 0.2f,
                    r.left + r.width() * 0.03f + fs, r.top + r.height() * 0.2f + fs);
            Gd.face(c, face, Math.min(11, l.optInt("difficulty")));

            float x = face.right + r.width() * 0.04f;
            Gd.outlined(c, l.optString("name"), x, r.top + r.height() * 0.22f,
                    r.height() * 0.15f, Gd.PAPER, Paint.Align.LEFT);
            Gd.outlined(c, "автор: " + l.optString("author"), x, r.top + r.height() * 0.38f,
                    r.height() * 0.09f, Gd.NEON, Paint.Align.LEFT);
            Gd.outlined(c, Gd.DIFFS[Math.min(11, l.optInt("difficulty"))]
                            + "   ★ " + l.optInt("stars") + "   ♥ " + l.optInt("likes")
                            + "   ↓ " + l.optInt("downloads"),
                    x, r.top + r.height() * 0.52f, r.height() * 0.085f,
                    Color.argb(230, 210, 225, 255), Paint.Align.LEFT);

            long left = d == null ? 0 : d.optLong("secondsLeft");
            Gd.outlined(c, "сменится через " + (left / 86400) + "д " + (left % 86400 / 3600) + "ч "
                            + (left % 3600 / 60) + "м",
                    x, r.top + r.height() * 0.66f, r.height() * 0.085f, Gd.GOLD, Paint.Align.LEFT);
            boolean saved = Store.isSaved(l.optInt("id"));
            Gd.outlined(c, saved ? "✔ сохранён офлайн" : "не сохранён офлайн",
                    x, r.top + r.height() * 0.80f, r.height() * 0.085f,
                    saved ? Gd.LIME : Color.argb(220, 190, 190, 210), Paint.Align.LEFT);
        }
    }
}
