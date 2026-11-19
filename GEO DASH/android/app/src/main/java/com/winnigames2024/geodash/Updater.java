package com.winnigames2024.geodash;

import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import android.os.Build;
import android.provider.Settings;

import java.io.InputStream;
import java.io.FileOutputStream;
import java.net.HttpURLConnection;
import java.net.URL;

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
        check(a, false);
    }

    /**
     * @param manual true — проверка по кнопке: скажем и когда версия последняя.
     */
    public static void check(final Activity a, final boolean manual) {
        Api.async(() -> {
            if (!Api.online && !Api.ping()) {
                if (manual) a.runOnUiThread(() -> GdDialog.info(a, "ОБНОВЛЕНИЕ", "Нет связи с интернетом."));
                return;
            }
            final JSONObject info = fetch();
            if (info == null) {
                if (manual) a.runOnUiThread(() -> GdDialog.info(a, "ОБНОВЛЕНИЕ", "Не вышло проверить обновление."));
                return;
            }
            final String remote = info.optString("version", "");
            final String apk = info.optString("apk", "");
            if (!isNewer(remote, currentVersion()) || apk.isEmpty()) {
                if (manual) a.runOnUiThread(() -> GdDialog.info(a, "ОБНОВЛЕНИЕ",
                        "У тебя последняя версия (" + currentVersion() + ")."));
                return;
            }

            a.runOnUiThread(() -> GdDialog.message(a, "ЕСТЬ ОБНОВЛЕНИЕ " + remote,
                    currentVersion() + "  →  " + remote + "\n\n" + info.optString("notes", ""),
                    new String[]{"ОБНОВИТЬ", "ПОЗЖЕ"},
                    i -> { if (i == 0) download(a, apk); }));
        });
    }

    /** Качает apk в assets/updates с показом процентов и запускает установку. */
    public static void download(final Activity a, final String url) {
        final GdDialog.Progress bar = GdDialog.progress(a, "ОБНОВЛЕНИЕ", "Скачиваю новую версию…");
        Api.async(() -> {
            File dir = new File(Store.assets(), "updates");
            dir.mkdirs();
            final File f = new File(dir, "GEO_DASH.apk");
            boolean ok = fetchTo(url, f, bar);
            a.runOnUiThread(() -> {
                bar.close();
                if (!ok || f.length() < 200000) {
                    GdDialog.info(a, "ОБНОВЛЕНИЕ", "Не вышло скачать. Попробуй позже или скачай с сайта.");
                    return;
                }
                askInstall(a, f);
            });
        });
    }

    /** Скачивание файла с процентами. */
    static boolean fetchTo(String url, File out, GdDialog.Progress bar) {
        HttpURLConnection c = null;
        try {
            c = (HttpURLConnection) new URL(url).openConnection();
            c.setConnectTimeout(10000);
            c.setReadTimeout(120000);
            c.setInstanceFollowRedirects(true);
            if (c.getResponseCode() >= 400) return false;
            int total = c.getContentLength();
            InputStream in = c.getInputStream();
            FileOutputStream os = new FileOutputStream(out);
            byte[] buf = new byte[65536];
            long done = 0;
            long last = 0;
            int n;
            while ((n = in.read(buf)) > 0) {
                os.write(buf, 0, n);
                done += n;
                long now = System.currentTimeMillis();
                if (bar != null && now - last > 120) {
                    last = now;
                    final double k = total > 0 ? done / (double) total : 0;
                    final String txt = total > 0
                            ? String.format("Скачиваю… %d%%  (%.1f из %.1f МБ)",
                                (int) (k * 100), done / 1048576.0, total / 1048576.0)
                            : String.format("Скачиваю… %.1f МБ", done / 1048576.0);
                    bar.update(k, txt);
                }
            }
            os.close();
            in.close();
            if (bar != null) bar.update(1, "Готово, запускаю установку…");
            return true;
        } catch (Exception e) {
            return false;
        } finally {
            if (c != null) c.disconnect();
        }
    }

    /**
     * Перед установкой просим разрешение «ставить приложения из этого источника»,
     * иначе система молча откажет.
     */
    static void askInstall(final Activity a, final File apk) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O
                && !a.getPackageManager().canRequestPackageInstalls()) {
            GdDialog.message(a, "РАЗРЕШЕНИЕ",
                    "Чтобы поставить обновление, разреши установку приложений из GEO DASH.\n"
                            + "Сейчас откроются настройки — включи переключатель и вернись назад.",
                    new String[]{"ОТКРЫТЬ НАСТРОЙКИ", "ОТМЕНА"}, i -> {
                        if (i != 0) return;
                        try {
                            Intent s = new Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES,
                                    Uri.parse("package:" + a.getPackageName()));
                            s.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                            a.startActivity(s);
                            pending = apk;
                        } catch (Exception e) {
                            install(a, apk);
                        }
                    });
            return;
        }
        install(a, apk);
    }

    /** Скачанный apk, который ждёт разрешения на установку. */
    static File pending;

    /** Вызывается при возврате в игру: если разрешение дали — ставим. */
    public static void resumeInstall(Activity a) {
        if (pending == null) return;
        File f = pending;
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O
                && !a.getPackageManager().canRequestPackageInstalls()) return;
        pending = null;
        install(a, f);
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
