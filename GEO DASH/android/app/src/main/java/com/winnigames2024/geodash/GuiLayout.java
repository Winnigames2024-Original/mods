package com.winnigames2024.geodash;

import android.graphics.RectF;

import org.json.JSONObject;

/**
 * Расположение кнопок в игре: «=» слева сверху, полоса прогресса,
 * кнопки платформера. Всё двигается прямо в игре и запоминается.
 */
public final class GuiLayout {

    public static class Item {
        public String name;
        public double x, y, size;
        public Item(String n, double x, double y, double s) { name = n; this.x = x; this.y = y; size = s; }
    }

    public final Item menu = new Item("Кнопка =", 0.035, 0.085, 0.11);
    public final Item progress = new Item("Полоса прогресса", 0.5, 0.06, 0.46);
    public final Item left = new Item("Влево", 0.09, 0.82, 0.19);
    public final Item right = new Item("Вправо", 0.25, 0.82, 0.19);
    public final Item jump = new Item("Прыжок", 0.90, 0.82, 0.21);

    public Item[] items() { return new Item[]{menu, progress, left, right, jump}; }

    public void reset() {
        set(menu, 0.035, 0.085, 0.11);
        set(progress, 0.5, 0.06, 0.46);
        set(left, 0.09, 0.82, 0.19);
        set(right, 0.25, 0.82, 0.19);
        set(jump, 0.90, 0.82, 0.21);
    }

    static void set(Item i, double x, double y, double s) { i.x = x; i.y = y; i.size = s; }

    /** Квадрат элемента на экране. */
    public static RectF rect(Item i, int w, int h) {
        float s = (float) (i.size * h);
        float cx = (float) (i.x * w), cy = (float) (i.y * h);
        return new RectF(cx - s / 2, cy - s / 2, cx + s / 2, cy + s / 2);
    }

    // ------------------------------------------------------------ хранение

    public static GuiLayout load() {
        GuiLayout g = new GuiLayout();
        try {
            String s = Store.prefs.getString("gui", "");
            if (s == null || s.isEmpty()) return g;
            JSONObject o = new JSONObject(s);
            for (Item i : g.items()) {
                JSONObject j = o.optJSONObject(i.name);
                if (j == null) continue;
                i.x = j.optDouble("x", i.x);
                i.y = j.optDouble("y", i.y);
                i.size = j.optDouble("s", i.size);
            }
        } catch (Exception ignored) { }
        return g;
    }

    public void save() {
        try {
            JSONObject o = new JSONObject();
            for (Item i : items()) {
                JSONObject j = new JSONObject();
                j.put("x", i.x); j.put("y", i.y); j.put("s", i.size);
                o.put(i.name, j);
            }
            Store.prefs.edit().putString("gui", o.toString()).apply();
        } catch (Exception ignored) { }
    }
}
