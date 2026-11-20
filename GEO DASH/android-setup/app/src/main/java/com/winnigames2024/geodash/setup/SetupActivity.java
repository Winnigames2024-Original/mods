package com.winnigames2024.geodash.setup;

import android.app.Activity;
import android.app.PendingIntent;
import android.content.Intent;
import android.content.pm.PackageInfo;
import android.content.pm.PackageInstaller;
import android.content.pm.PackageManager;
import android.graphics.Color;
import android.graphics.Typeface;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.provider.Settings;
import android.text.Editable;
import android.text.InputType;
import android.text.TextWatcher;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.widget.*;

import org.json.JSONObject;

import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.util.concurrent.Executors;

/**
 * Установщик GEO DASH на телефон.
 *
 * Качает с сайта всегда самую свежую сборку игры и ставит её от своего имени —
 * поэтому игра видит, что поставлена установщиком, и не просит ставить её заново.
 * Введённый здесь ключ передаётся игре при первом запуске.
 */
public class SetupActivity extends Activity {

    public static final String GAME_PKG = "com.winnigames2024.geodash";
    static final String SITE = "https://geodash.winnigames2024.ru";
    static final String APK_URL = SITE + "/download/GEO_DASH.apk";
    static final String STATUS_URL = SITE + "/api.php?action=status";

    /** Чтобы приёмник установки мог сообщить окну результат. */
    public static SetupActivity live;

    final Handler ui = new Handler(Looper.getMainLooper());

    TextView title, sub, status;
    EditText key;
    Button install, open, site;
    ProgressBar bar;
    String latest = "";

    @Override protected void onCreate(Bundle b) {
        super.onCreate(b);
        live = this;

        ScrollView sv = new ScrollView(this);
        LinearLayout box = new LinearLayout(this);
        box.setOrientation(LinearLayout.VERTICAL);
        box.setPadding(pad(22), pad(30), pad(22), pad(30));
        box.setBackgroundColor(Color.rgb(11, 10, 30));
        sv.addView(box, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));
        setContentView(sv);

        title = new TextView(this);
        title.setText("GEO DASH");
        title.setTextColor(Color.rgb(240, 244, 255));
        title.setTextSize(34f);
        title.setTypeface(Typeface.DEFAULT_BOLD);
        box.addView(title);

        sub = new TextView(this);
        sub.setText("установщик · Winnigames2024");
        sub.setTextColor(Color.rgb(150, 190, 255));
        sub.setTextSize(14f);
        sub.setPadding(0, pad(2), 0, pad(22));
        box.addView(sub);

        TextView about = new TextView(this);
        about.setText("Установщик скачивает последнюю версию игры с сервера и ставит её. " +
                "Игра, поставленная иначе, попросит лицензионный ключ и не запустится.");
        about.setTextColor(Color.rgb(205, 215, 245));
        about.setTextSize(14f);
        about.setPadding(0, 0, 0, pad(20));
        box.addView(about);

        TextView klab = new TextView(this);
        klab.setText("Лицензионный ключ");
        klab.setTextColor(Color.rgb(240, 244, 255));
        klab.setTextSize(15f);
        klab.setTypeface(Typeface.DEFAULT_BOLD);
        box.addView(klab);

        TextView khint = new TextView(this);
        khint.setText("не обязательно сейчас — игра спросит его при первом запуске");
        khint.setTextColor(Color.rgb(150, 190, 255));
        khint.setTextSize(12.5f);
        khint.setPadding(0, 0, 0, pad(8));
        box.addView(khint);

        key = new EditText(this);
        key.setHint("GD-XXXX-XXXX-XXXX-XXXX");
        key.setHintTextColor(Color.rgb(120, 130, 170));
        key.setTextColor(Color.rgb(240, 244, 255));
        key.setBackgroundColor(Color.rgb(22, 20, 52));
        key.setGravity(Gravity.CENTER);
        key.setSingleLine(true);
        key.setTextSize(19f);
        key.setPadding(pad(10), pad(14), pad(10), pad(14));
        key.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_FLAG_CAP_CHARACTERS);
        key.addTextChangedListener(new TextWatcher() {
            boolean guard;
            @Override public void beforeTextChanged(CharSequence s, int a, int b, int c) { }
            @Override public void onTextChanged(CharSequence s, int a, int b, int c) { }
            @Override public void afterTextChanged(Editable e) {
                if (guard) return;
                String p = Keys.pretty(e.toString());
                if (!p.equals(e.toString())) {
                    guard = true;
                    key.setText(p);
                    key.setSelection(p.length());
                    guard = false;
                }
                key.setTextColor(e.length() == 0 || Keys.wellFormed(e.toString())
                        ? Color.rgb(240, 244, 255) : Color.rgb(255, 150, 150));
            }
        });
        box.addView(key, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));

        bar = new ProgressBar(this, null, android.R.attr.progressBarStyleHorizontal);
        bar.setMax(100);
        LinearLayout.LayoutParams blp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, pad(14));
        blp.setMargins(0, pad(22), 0, pad(10));
        box.addView(bar, blp);

        status = new TextView(this);
        status.setTextColor(Color.rgb(200, 215, 255));
        status.setTextSize(14f);
        status.setText("Готов к установке");
        box.addView(status);

        install = big(box, "УСТАНОВИТЬ ИГРУ", Color.rgb(32, 150, 100));
        install.setOnClickListener(v -> start());

        open = big(box, "ОТКРЫТЬ ИГРУ", Color.rgb(40, 100, 200));
        open.setOnClickListener(v -> openGame());
        open.setVisibility(installedVersion() == null ? View.GONE : View.VISIBLE);

        site = big(box, "ПОЛУЧИТЬ КЛЮЧ НА САЙТЕ", Color.rgb(140, 100, 20));
        site.setOnClickListener(v -> {
            try { startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse(SITE + "/account/"))); }
            catch (Exception ignored) { }
        });

        check();
    }

    @Override protected void onDestroy() {
        if (live == this) live = null;
        super.onDestroy();
    }

    @Override protected void onResume() {
        super.onResume();
        if (installedVersion() != null) open.setVisibility(View.VISIBLE);
    }

    int pad(int dp) { return (int) (dp * getResources().getDisplayMetrics().density); }

    Button big(LinearLayout box, String text, int color) {
        Button b = new Button(this);
        b.setText(text);
        b.setAllCaps(true);
        b.setTextColor(Color.rgb(240, 244, 255));
        b.setBackgroundColor(color);
        b.setTextSize(16f);
        b.setTypeface(Typeface.DEFAULT_BOLD);
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, pad(56));
        lp.setMargins(0, pad(14), 0, 0);
        box.addView(b, lp);
        return b;
    }

    void say(String s) { ui.post(() -> status.setText(s)); }
    void pct(int p) { ui.post(() -> bar.setProgress(Math.max(0, Math.min(100, p)))); }

    /** Версия уже установленной игры или null. */
    String installedVersion() {
        try {
            PackageInfo i = getPackageManager().getPackageInfo(GAME_PKG, 0);
            return i.versionName;
        } catch (PackageManager.NameNotFoundException e) { return null; }
    }

    /** Какая версия лежит на сервере. */
    void check() {
        Executors.newSingleThreadExecutor().execute(() -> {
            String v = "";
            try {
                HttpURLConnection c = (HttpURLConnection) new URL(STATUS_URL).openConnection();
                c.setConnectTimeout(8000);
                c.setReadTimeout(15000);
                StringBuilder sb = new StringBuilder();
                try (InputStream in = c.getInputStream()) {
                    byte[] buf = new byte[8192];
                    int n;
                    while ((n = in.read(buf)) > 0) sb.append(new String(buf, 0, n, "UTF-8"));
                }
                v = new JSONObject(sb.toString()).optString("version", "");
            } catch (Exception ignored) { }

            latest = v;
            String have = installedVersion();
            if (v.isEmpty())
                say(have == null ? "Сервер не отвечает — попробуйте позже"
                                 : "Установлена версия " + have);
            else if (have == null) say("На сервере версия " + v + " — можно ставить");
            else if (have.equals(v)) say("Установлена последняя версия " + have);
            else say("У вас " + have + ", на сервере " + v + " — можно обновить");

            if (!v.isEmpty() && installedVersion() != null && !v.equals(installedVersion()))
                ui.post(() -> install.setText("ОБНОВИТЬ ИГРУ"));
        });
    }

    void start() {
        // на Android 8+ установку из сторонних источников разрешает сам игрок
        if (Build.VERSION.SDK_INT >= 26 && !getPackageManager().canRequestPackageInstalls()) {
            say("Разрешите установку из этого приложения и нажмите кнопку снова");
            try {
                startActivity(new Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES,
                        Uri.parse("package:" + getPackageName())));
            } catch (Exception ignored) { }
            return;
        }

        install.setEnabled(false);
        pct(0);
        say("Скачиваю игру…");

        Executors.newSingleThreadExecutor().execute(() -> {
            File apk = new File(getCacheDir(), "geodash.apk");
            if (!download(APK_URL, apk)) {
                say("Не получилось скачать игру. Проверьте интернет.");
                ui.post(() -> install.setEnabled(true));
                return;
            }
            say("Ставлю игру…");
            if (!installApk(apk)) {
                say("Установка не началась — попробуйте ещё раз");
                ui.post(() -> install.setEnabled(true));
            }
        });
    }

    boolean download(String url, File to) {
        HttpURLConnection c = null;
        try {
            c = (HttpURLConnection) new URL(url).openConnection();
            c.setConnectTimeout(10000);
            c.setReadTimeout(60000);
            c.setInstanceFollowRedirects(true);
            if (c.getResponseCode() >= 400) return false;
            long total = c.getContentLength(), done = 0;

            File part = new File(to.getParentFile(), to.getName() + ".part");
            try (InputStream in = c.getInputStream(); OutputStream out = new FileOutputStream(part)) {
                byte[] buf = new byte[256 * 1024];
                int n;
                while ((n = in.read(buf)) > 0) {
                    out.write(buf, 0, n);
                    done += n;
                    if (total > 0) pct((int) (done * 100 / total));
                }
            }
            if (part.length() < 300 * 1024) { part.delete(); return false; }
            if (to.exists()) to.delete();
            return part.renameTo(to);
        } catch (Exception e) {
            return false;
        } finally {
            if (c != null) c.disconnect();
        }
    }

    /**
     * Ставим через PackageInstaller: тогда система запоминает, что игру поставил
     * наш установщик, — именно это игра и проверяет.
     */
    boolean installApk(File apk) {
        try {
            PackageInstaller pi = getPackageManager().getPackageInstaller();
            PackageInstaller.SessionParams p =
                    new PackageInstaller.SessionParams(PackageInstaller.SessionParams.MODE_FULL_INSTALL);
            p.setAppPackageName(GAME_PKG);
            int id = pi.createSession(p);

            try (PackageInstaller.Session s = pi.openSession(id)) {
                try (OutputStream out = s.openWrite("geodash", 0, apk.length());
                     InputStream in = new FileInputStream(apk)) {
                    byte[] buf = new byte[256 * 1024];
                    int n;
                    while ((n = in.read(buf)) > 0) out.write(buf, 0, n);
                    s.fsync(out);
                }

                int flags = PendingIntent.FLAG_UPDATE_CURRENT;
                if (Build.VERSION.SDK_INT >= 31) flags |= 0x02000000;     // FLAG_MUTABLE
                Intent i = new Intent(InstallReceiver.ACTION).setPackage(getPackageName());
                PendingIntent pe = PendingIntent.getBroadcast(this, id, i, flags);
                s.commit(pe.getIntentSender());
            }
            return true;
        } catch (Exception e) {
            say("Ошибка установки: " + e.getMessage());
            return false;
        }
    }

    /** Результат установки — вызывается приёмником. */
    public void installDone(int status, String msg) {
        ui.post(() -> {
            install.setEnabled(true);
            if (status == PackageInstaller.STATUS_SUCCESS) {
                pct(100);
                setStatus(("Игра установлена" + (latest.isEmpty() ? "" : " — версия " + latest))
                        + ". Можно открывать.");
                open.setVisibility(View.VISIBLE);
                install.setText("ПЕРЕУСТАНОВИТЬ");
                openGame();
            } else if (status == PackageInstaller.STATUS_FAILURE_ABORTED) {
                setStatus("Установка отменена");
            } else {
                setStatus("Установить не вышло" + (msg == null || msg.isEmpty() ? "" : ": " + msg));
            }
        });
    }

    void setStatus(String s) { status.setText(s); }

    /** Запускаем игру и передаём ей ключ, если он введён. */
    void openGame() {
        Intent i = getPackageManager().getLaunchIntentForPackage(GAME_PKG);
        if (i == null) { say("Игра ещё не установлена"); return; }
        String k = key.getText().toString();
        if (Keys.wellFormed(k)) i.putExtra("gd_key", Keys.pretty(k));
        i.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
        try { startActivity(i); } catch (Exception ignored) { }
    }
}
