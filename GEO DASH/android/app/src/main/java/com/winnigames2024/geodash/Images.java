package com.winnigames2024.geodash;

import android.graphics.Bitmap;
import android.graphics.BitmapFactory;

import java.io.File;
import java.util.HashMap;
import java.util.HashSet;
import java.util.Map;
import java.util.Set;

/** Картинки из toolbox сайта: качаются в assets/images и дальше работают офлайн. */
public final class Images {

    static final Map<Integer, Bitmap> CACHE = new HashMap<>();
    static final Set<Integer> LOADING = new HashSet<>();

    public static File dir() {
        File d = new File(Store.assets(), "images");
        d.mkdirs();
        return d;
    }

    public static File fileOf(int id) {
        for (String ext : new String[]{".png", ".jpg", ".gif"}) {
            File f = new File(dir(), id + ext);
            if (f.exists()) return f;
        }
        return new File(dir(), id + ".png");
    }

    public static boolean isCached(int id) {
        File f = fileOf(id);
        return f.exists() && f.length() > 64;
    }

    /** Готовая картинка или null — тогда она качается в фоне. */
    public static Bitmap get(int id) {
        if (id <= 0) return null;
        Bitmap b = CACHE.get(id);
        if (b != null) return b;

        if (isCached(id)) {
            try {
                b = BitmapFactory.decodeFile(fileOf(id).getAbsolutePath());
                if (b != null) { CACHE.put(id, b); return b; }
            } catch (Exception ignored) { }
        }
        download(id);
        return null;
    }

    /** Скачать картинку в фоне. */
    public static void download(final int id) {
        synchronized (LOADING) {
            if (!LOADING.add(id)) return;
        }
        Api.async(() -> {
            try {
                byte[] data = Api.fetchBytes(Api.base() + "/api/images/" + id + "/file");
                if (data == null || data.length < 64)
                    data = Api.fetchBytes(Store.site().replaceAll("/+$", "") + "/api/images/" + id + "/file");
                if (data != null && data.length > 64) {
                    String ext = (data[0] == (byte) 0xFF && data[1] == (byte) 0xD8) ? ".jpg"
                            : (data[0] == 'G' && data[1] == 'I') ? ".gif" : ".png";
                    Store.writeBytes(new File(dir(), id + ext), data);
                    CACHE.remove(id);
                }
            } catch (Exception ignored) {
            } finally {
                synchronized (LOADING) { LOADING.remove(id); }
            }
        });
    }

    private Images() {}
}
