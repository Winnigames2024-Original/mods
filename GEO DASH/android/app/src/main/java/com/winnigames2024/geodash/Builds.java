package com.winnigames2024.geodash;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.File;
import java.util.ArrayList;
import java.util.List;

/**
 * Мои конструкции — выделил свайпом, сохранил, ставишь целиком.
 * Лежат в assets/builds.json.
 */
public final class Builds {

    public static class Piece {
        public String name = "";
        public final List<Level.Obj> objects = new ArrayList<>();
    }

    static File file() { return new File(Store.assets(), "builds.json"); }

    public static List<Piece> all() {
        List<Piece> res = new ArrayList<>();
        String s = Store.readFile(file());
        if (s == null) return res;
        try {
            JSONArray arr = new JSONArray(s);
            for (int i = 0; i < arr.length(); i++) {
                JSONObject o = arr.optJSONObject(i);
                if (o == null) continue;
                Piece p = new Piece();
                p.name = o.optString("Name", "Конструкция");
                JSONArray objs = o.optJSONArray("Objects");
                if (objs != null)
                    for (int k = 0; k < objs.length(); k++) {
                        JSONObject j = objs.optJSONObject(k);
                        if (j != null) p.objects.add(Level.parseObj(j));
                    }
                if (!p.objects.isEmpty()) res.add(p);
            }
        } catch (Exception ignored) { }
        return res;
    }

    static void write(List<Piece> list) {
        try {
            JSONArray arr = new JSONArray();
            for (Piece p : list) {
                JSONObject o = new JSONObject();
                o.put("Name", p.name);
                JSONArray objs = new JSONArray();
                for (Level.Obj ob : p.objects) objs.put(Level.objJson(ob, 0));
                o.put("Objects", objs);
                arr.put(o);
            }
            Store.writeFile(file(), arr.toString());
        } catch (Exception ignored) { }
    }

    /** Сохранить выделенное: координаты запоминаются от левого нижнего угла. */
    public static Piece save(String name, List<Level.Obj> objects) {
        if (objects == null || objects.isEmpty()) return null;
        double minX = Double.MAX_VALUE, minY = Double.MAX_VALUE;
        for (Level.Obj o : objects) { minX = Math.min(minX, o.x); minY = Math.min(minY, o.y); }

        Piece p = new Piece();
        p.name = name;
        for (Level.Obj o : objects) {
            Level.Obj c = Level.parseObj(Level.objJson(o, 0));
            c.id = 0;
            c.x -= minX; c.y -= minY;
            p.objects.add(c);
        }
        List<Piece> all = all();
        for (int i = all.size() - 1; i >= 0; i--)
            if (all.get(i).name.equalsIgnoreCase(name)) all.remove(i);
        all.add(p);
        write(all);
        return p;
    }

    public static void delete(String name) {
        List<Piece> all = all();
        for (int i = all.size() - 1; i >= 0; i--)
            if (all.get(i).name.equalsIgnoreCase(name)) all.remove(i);
        write(all);
    }

    /** Объекты конструкции, сдвинутые в нужную точку. */
    public static List<Level.Obj> stamp(Piece p, double x, double y) {
        List<Level.Obj> res = new ArrayList<>();
        if (p == null) return res;
        for (Level.Obj o : p.objects) {
            Level.Obj c = Level.parseObj(Level.objJson(o, 0));
            c.id = 0;
            c.x += x; c.y += y;
            res.add(c);
        }
        return res;
    }

    private Builds() {}
}
