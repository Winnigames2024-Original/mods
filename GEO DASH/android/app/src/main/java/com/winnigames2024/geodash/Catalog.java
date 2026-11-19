package com.winnigames2024.geodash;

import android.util.SparseArray;

/** Каталог объектов — тот же, что в игре на Windows. */
public final class Catalog {

    public static final int BLOCK = 0, HAZARD = 1, PORTAL = 2, ORB = 3, PAD = 4,
            TRIGGER = 5, DECO = 6, COIN = 7, SLOPE = 8, TEXT = 9, FINISH = 10, LIQUID = 11;

    public static final int ST_BLOCK = 0, ST_SLAB = 1, ST_PLATFORM = 2, ST_BRICK = 3, ST_GLASS = 4,
            ST_SLOPE_UP = 5, ST_SLOPE_DOWN = 6, ST_SPIKE = 7, ST_SPIKE_SMALL = 8, ST_SPIKE_HALF = 9,
            ST_SAW = 10, ST_SAW_BIG = 11, ST_SPIKE_ROW = 12, ST_HAZARD_BLOCK = 13, ST_ORB = 14,
            ST_PAD = 15, ST_PORTAL = 16, ST_DECO = 17, ST_CIRCLE = 18, ST_TRI = 19, ST_GLOW = 20,
            ST_TEXT = 21, ST_COIN = 22, ST_PICKUP = 23, ST_FINISH = 24, ST_CHECKPOINT = 25, ST_TRIGGER = 26,
            ST_WATER = 27, ST_LAVA = 28;

    public static class Def {
        public int id, kind, style;
        public double w = 30, h = 30, hx = 0, hy = 0, hw = 30, hh = 30;
        public boolean solid, deadly, wormPhase, topOnly, invisible;

        Def(int id, int kind, int style) { this.id = id; this.kind = kind; this.style = style; }
        Def size(double w, double h) { this.w = w; this.h = h; return this; }
        Def hit(double x, double y, double w, double h) { hx = x; hy = y; hw = w; hh = h; return this; }
        Def solid() { solid = true; wormPhase = true; return this; }
        Def deadly() { deadly = true; return this; }
        Def top() { topOnly = true; return this; }
        Def inv() { invisible = true; return this; }
    }

    static final SparseArray<Def> ALL = new SparseArray<>();
    static void add(Def d) { ALL.put(d.id, d); }

    static {
        add(new Def(1, BLOCK, ST_BLOCK).solid());
        add(new Def(2, BLOCK, ST_SLAB).size(30, 15).hit(0, 0, 30, 15).solid());
        add(new Def(3, BLOCK, ST_SLAB).size(30, 15).hit(0, 15, 30, 15).solid());
        add(new Def(4, BLOCK, ST_SLAB).size(30, 8).hit(0, 0, 30, 8).solid());
        add(new Def(5, SLOPE, ST_SLOPE_UP).solid());
        add(new Def(6, SLOPE, ST_SLOPE_DOWN).solid());
        add(new Def(7, BLOCK, ST_PLATFORM).size(30, 10).hit(0, 0, 30, 10).solid().top());
        add(new Def(8, BLOCK, ST_BRICK).solid());
        add(new Def(9, BLOCK, ST_GLASS).solid());

        add(new Def(20, HAZARD, ST_SPIKE).hit(11, 2, 8, 16).deadly());
        add(new Def(21, HAZARD, ST_SPIKE_SMALL).size(30, 15).hit(12, 2, 6, 8).deadly());
        add(new Def(22, HAZARD, ST_SPIKE).hit(11, 12, 8, 16).deadly());
        add(new Def(23, HAZARD, ST_SAW).hit(5, 5, 20, 20).deadly());
        add(new Def(24, HAZARD, ST_SAW_BIG).size(60, 60).hit(10, 10, 40, 40).deadly());
        add(new Def(25, HAZARD, ST_SPIKE_ROW).size(90, 30).hit(6, 2, 78, 14).deadly());
        add(new Def(26, HAZARD, ST_HAZARD_BLOCK).deadly());
        add(new Def(27, HAZARD, ST_SPIKE_HALF).size(30, 15).hit(10, 1, 10, 9).deadly());

        add(new Def(40, ORB, ST_ORB).hit(-3, -3, 36, 36));
        add(new Def(50, PAD, ST_PAD).size(30, 10).hit(0, 0, 30, 10));
        add(new Def(60, PORTAL, ST_PORTAL).size(30, 90).hit(0, 0, 30, 90));
        add(new Def(61, PORTAL, ST_PORTAL).size(30, 90).hit(0, 0, 30, 90));

        add(new Def(70, DECO, ST_DECO));
        add(new Def(71, DECO, ST_CIRCLE));
        add(new Def(72, DECO, ST_TRI));
        add(new Def(73, DECO, ST_GLOW).size(60, 60));
        add(new Def(75, TEXT, ST_TEXT).size(120, 30));

        add(new Def(80, COIN, ST_COIN).hit(-2, -2, 34, 34));
        add(new Def(82, COIN, ST_PICKUP).hit(-2, -2, 34, 34));

        // вода и лава: в них плавают, в лаве горят
        add(new Def(94, LIQUID, ST_WATER));
        add(new Def(95, LIQUID, ST_WATER).size(300, 120).hit(0, 0, 300, 120));
        add(new Def(96, LIQUID, ST_LAVA));
        add(new Def(97, LIQUID, ST_LAVA).size(300, 120).hit(0, 0, 300, 120));

        add(new Def(90, FINISH, ST_FINISH).size(30, 300).hit(0, 0, 30, 300));
        add(new Def(92, DECO, ST_CHECKPOINT).size(30, 120).hit(0, 0, 30, 120));

        add(new Def(100, TRIGGER, ST_TRIGGER).inv());
    }

    public static Def get(int id) {
        Def d = ALL.get(id);
        return d != null ? d : ALL.get(1);
    }

    private Catalog() {}
}
