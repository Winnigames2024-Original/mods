package com.winnigames2024.geodash;

import android.content.Context;
import android.graphics.*;
import android.view.View;
import android.view.ViewGroup;
import android.widget.FrameLayout;

import java.util.List;

/** Основные уровни: карточка с листанием, как в игре. */
public class PlayActivity extends GdActivity {

    List<Level> levels;
    int index;
    CardBig card;

    @Override protected int bgA() { return Color.rgb(120, 60, 230); }
    @Override protected int bgId() { return 11; }
    @Override protected int groundId() { return 2; }
    @Override protected String headTitle() { return "ОСНОВНЫЕ УРОВНИ"; }
    @Override protected String headSub() { return "музыка встроена — играется без интернета"; }

    @Override protected void build() {
        levels = Official.all();

        card = new CardBig(this);
        root.addView(card, Ui.lp(W, H, 0.12f, 0.20f, 0.76f, 0.46f));

        btn("", Ui.I_BACK, Color.rgb(150, 150, 185), Color.rgb(60, 62, 100),
                0.02f, 0.36f, 0.07f, 0.13f, () -> move(-1));
        btn("", Ui.I_PLAY, Color.rgb(150, 150, 185), Color.rgb(60, 62, 100),
                0.91f, 0.36f, 0.07f, 0.13f, () -> move(1));

        btn("ИГРАТЬ", Ui.I_PLAY, Color.rgb(120, 255, 170), Color.rgb(20, 150, 100),
                0.28f, 0.70f, 0.20f, 0.13f, () -> GameActivity.play(this, cur(), false));
        btn("ТРЕНИРОВКА", Ui.I_CHECK, Color.rgb(120, 190, 255), Color.rgb(30, 90, 190),
                0.52f, 0.70f, 0.20f, 0.13f, () -> GameActivity.play(this, cur(), true));

        back(0.02f, 0.86f);
        preview();
    }

    @Override protected void onResume() {
        super.onResume();
        if (card != null) card.invalidate();
    }

    Level cur() { return levels.get(index); }

    void move(int d) {
        index = (index + d + levels.size()) % levels.size();
        card.invalidate();
        preview();
    }

    void preview() {
        Music.play(cur().settings.songId, 0, 0.55f, true);
    }

    class CardBig extends View {
        CardBig(Context c) { super(c); }

        @Override protected void onDraw(Canvas c) {
            RectF r = new RectF(0, 0, getWidth(), getHeight());
            Gd.plate(c, r, Color.rgb(44, 38, 92), Color.rgb(22, 18, 54), 24f);

            Level lv = cur();
            Level.Settings s = lv.settings;
            double[] pr = Store.progress(lv);

            float fs = r.height() * 0.4f;
            RectF face = new RectF(r.left + r.width() * 0.04f, r.top + r.height() * 0.18f,
                    r.left + r.width() * 0.04f + fs, r.top + r.height() * 0.18f + fs);
            Gd.face(c, face, s.difficulty);
            Gd.outlined(c, Gd.DIFFS[s.difficulty], face.centerX(), face.bottom + r.height() * 0.11f,
                    r.height() * 0.07f, Gd.diffTint(s.difficulty), Paint.Align.CENTER);

            float x = face.right + r.width() * 0.05f;
            Gd.outlined(c, s.name, x, r.top + r.height() * 0.2f, r.height() * 0.14f, Gd.PAPER, Paint.Align.LEFT);
            Gd.outlined(c, "автор: " + s.author, x, r.top + r.height() * 0.33f,
                    r.height() * 0.075f, Gd.NEON, Paint.Align.LEFT);
            Gd.outlined(c, "музыка: " + s.songName + " (встроенная)", x, r.top + r.height() * 0.43f,
                    r.height() * 0.07f, Color.argb(230, 210, 225, 255), Paint.Align.LEFT);
            Gd.outlined(c, "объектов: " + lv.objects.size() + "   длина: " + (int) (s.lengthUnits / 30) + " клеток",
                    x, r.top + r.height() * 0.53f, r.height() * 0.07f,
                    Color.argb(220, 200, 215, 250), Paint.Align.LEFT);

            RectF bar = new RectF(x, r.top + r.height() * 0.60f, r.right - r.width() * 0.05f, r.top + r.height() * 0.70f);
            c.drawRect(bar, Gd.fill(Color.argb(160, 10, 8, 26)));
            Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
            p.setShader(new LinearGradient(bar.left, 0, bar.right, 0, Gd.NEON, Gd.LIME, Shader.TileMode.CLAMP));
            c.drawRect(new RectF(bar.left, bar.top, bar.left + (float) (bar.width() * Math.min(1, pr[0] / 100.0)), bar.bottom), p);
            c.drawRect(bar, Gd.stroke(Gd.INK, 3f));
            Gd.outlined(c, (int) pr[0] + "%" + (pr[3] > 0.5 ? "  ПРОЙДЕН" : ""), bar.centerX(),
                    bar.bottom - bar.height() * 0.22f, bar.height() * 0.7f, Gd.PAPER, Paint.Align.CENTER);

            float cx = x;
            for (int i = 1; i <= 3; i++) {
                float cs = r.height() * 0.1f;
                Gd.coin(c, new RectF(cx, r.top + r.height() * 0.76f, cx + cs, r.top + r.height() * 0.76f + cs), pr[2] >= i);
                cx += cs * 1.3f;
            }
            Gd.outlined(c, "попыток: " + (int) pr[1], cx + r.width() * 0.02f, r.top + r.height() * 0.83f,
                    r.height() * 0.07f, Color.argb(220, 210, 225, 255), Paint.Align.LEFT);

            // точки
            float dx = r.centerX() - (levels.size() * r.width() * 0.012f);
            for (int i = 0; i < levels.size(); i++) {
                float rad = r.width() * 0.008f;
                c.drawCircle(dx + i * r.width() * 0.024f, r.bottom - r.height() * 0.06f, rad,
                        Gd.fill(i == index ? Gd.NEON : Color.argb(90, 255, 255, 255)));
            }
        }
    }
}
