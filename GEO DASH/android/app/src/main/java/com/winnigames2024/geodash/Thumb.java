package com.winnigames2024.geodash;

import android.graphics.*;

/** Мини-картинка уровня: начало уровня в маленьком окошке — для карточек списков. */
public final class Thumb {

    public static void draw(Canvas c, RectF r, Level lv) {
        if (lv == null || r.width() < 8 || r.height() < 8) return;
        Level.Settings st = lv.settings;

        int save = c.save();
        Path clip = new Path();
        clip.addRoundRect(r, 10, 10, Path.Direction.CW);
        c.clipPath(clip);

        float scale = Math.max(0.25f, r.height() / 330f);
        Backdrop.background(c, r, st.bgId, st.bgColor, 0, 0, scale);

        float groundH = r.height() * 0.18f;
        RectF gr = new RectF(r.left, r.bottom - groundH, r.right, r.bottom);
        Backdrop.ground(c, gr, st.groundId, st.groundColor, Color.WHITE, 0, scale, false);

        float baseY = gr.top;
        for (Level.Obj o : lv.objects) {
            Catalog.Def d = Catalog.get(o.typeId);
            float x = r.left + (float) (o.x * scale);
            float w = Math.max(2f, (float) (d.w * o.scaleX * scale));
            if (x > r.right + 20 || x + w < r.left - 20) continue;
            float h = Math.max(2f, (float) (d.h * o.scaleY * scale));
            float y = baseY - (float) ((o.y + d.h * o.scaleY) * scale);
            if (y > r.bottom + 10 || y + h < r.top - 10) continue;
            EditorCanvasActivity.drawPreview(c, o, d, new RectF(x, y, x + w, y + h));
        }

        c.restoreToCount(save);
        c.drawRoundRect(r, 10, 10, Gd.stroke(Gd.INK, 3f));
    }

    private Thumb() {}
}
