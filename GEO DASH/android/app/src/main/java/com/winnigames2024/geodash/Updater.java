package com.winnigames2024.geodash;

import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import android.os.Build;

import androidx.core.content.FileProvider;

import org.json.JSONObject;

import java.io.File;

/** Автообновление apk через update.json (тот же файл, что и у версии для Windows). */
public class Updater {

    public static final String UPDATE_JSON =
            "https://raw.githubusercontent.com/Winnigames2024-Original/mods/main/GEO%20DASH/update.json";

    public static String currentVersion() { return BuildConfig.VERSION_NAME; }

    /** Читает update.json: сначала с GitHub, потом с сервера игры. */
    public static JSONObject fetch() {
        byte[] b = Api.fetchBytes(UPDATE_JSON);
        if (b != null) {
            try { return new JSONObject(new String(b, java.nio.charset.StandardCharsets.UTF_8)); }
            catch (Exception ignored) { }
        }
        String s = Api.rawGet("/api/version");
        try { return s == null ? null : new JSONObject(s); } catch (Exception e) { return null; }
    }

    public static boolean isNewer(String remote, String local) {
        if (remote == null || remote.isEmpty()) return false;
        String[] a = remote.replace("v", "").split("\\.");
        String[] b = local.replace("v", "").split("\\.");
        for (int i = 0; i < Math.max(a.length, b.length); i++) {
            int x = i < a.length ? parse(a[i]) : 0;
            int y = i < b.length ? parse(b[i]) : 0;
            if (x != y) return x > y;
        }
        return false;
    }

    static int parse(String s) {
        try { return Integer.parseInt(s.trim()); } catch (Exception e) { return 0; }
    }

    /** Проверяет и, если есть версия новее, показывает свой диалог обновления. */
    public static void check(final Activity a) {
        Api.async(() -> {
            if (!Api.online && !Api.ping()) return;
            final JSONObject info = fetch();
            if (info == null) return;
            final String remote = info.optString("version", "");
            final String apk = info.optString("apk", "");
            if (!isNewer(remote, currentVersion()) || apk.isEmpty()) return;

            a.runOnUiThread(() -> GdDialog.message(a, "ЕСТЬ ОБНОВЛЕНИЕ " + remote,
                    currentVersion() + "  →  " + remote + "\n\n" + info.optString("notes", ""),
                    new String[]{"ОБНОВИТЬ", "ПОЗЖЕ"},
                    i -> { if (i == 0) download(a, apk); }));
        });
    }

    /** Качает apk в assets/updates и запускает установку. */
    public static void download(final Activity a, final String url) {
        GdDialog.info(a, "ОБНОВЛЕНИЕ", "Скачиваю новую версию…");
        Api.async(() -> {
            byte[] data = Api.fetchBytes(url);
            if (data == null || data.length < 20000) {
                a.runOnUiThread(() -> GdDialog.info(a, "ОБНОВЛЕНИЕ",
                        "Не вышло скачать. Попробуй позже или скачай с сайта."));
                return;
            }
            File dir = new File(Store.assets(), "updates");
            dir.mkdirs();
            final File f = new File(dir, "GEO_DASH.apk");
            if (!Store.writeBytes(f, data)) {
                a.runOnUiThread(() -> GdDialog.info(a, "ОБНОВЛЕНИЕ", "Не удалось сохранить файл."));
                return;
            }
            a.runOnUiThread(() -> install(a, f));
        });
    }

    static void install(Activity a, File apk) {
        try {
            Intent i = new Intent(Intent.ACTION_VIEW);
            i.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_NO_ANIMATION);
            Uri uri;
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.N) {
                uri = FileProvider.getUriForFile(a, a.getPackageName() + ".files", apk);
                i.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
            } else {
                uri = Uri.fromFile(apk);
            }
            i.setDataAndType(uri, "application/vnd.android.package-archive");
            a.startActivity(i);
        } catch (Exception e) {
            GdDialog.info(a, "ОБНОВЛЕНИЕ",
                    "Файл скачан: " + apk.getAbsolutePath() + "\nУстанови его вручную.");
        }
    }
}
