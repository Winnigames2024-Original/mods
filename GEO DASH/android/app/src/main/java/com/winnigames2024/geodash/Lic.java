package com.winnigames2024.geodash;

import android.content.pm.PackageManager;
import android.os.Build;
import android.provider.Settings;

import org.json.JSONObject;

import java.security.MessageDigest;

/**
 * Лицензия игры. Ключ GD-XXXX-XXXX-XXXX-XXXX активируется один раз и навсегда
 * остаётся за аккаунтом — он работает и на компьютере, и на телефоне.
 *
 * Проверяем две вещи: игра поставлена нашим установщиком и на аккаунте есть ключ.
 * Активация запоминается, поэтому без интернета игра запускается как обычно.
 * У админа игра открыта всегда.
 */
public class Lic {

    /** Алфавит Крокфорда: без похожих друг на друга I, L, O, U. */
    private static final String ALPHA = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /** Пакет нашего установщика на телефон. */
    public static final String SETUP_PKG = "com.winnigames2024.geodash.setup";

    // ------------------------------------------------------------------ ключ

    public static String normalize(String s) {
        if (s == null) return "";
        StringBuilder sb = new StringBuilder(16);
        for (char ch : s.toUpperCase().toCharArray()) {
            char c = ch;
            if (c == 'O') c = '0';
            else if (c == 'I' || c == 'L') c = '1';
            else if (c == 'U') c = 'V';
            if (ALPHA.indexOf(c) >= 0) sb.append(c);
        }
        String t = sb.toString();
        // «GD» впереди — часть записи ключа, а не его тело
        if (t.startsWith("GD") && t.length() - 2 >= 16) t = t.substring(2);
        return t.length() > 16 ? t.substring(0, 16) : t;
    }

    private static char checkChar(String body) {
        int sum = 0;
        for (int i = 0; i < body.length(); i++) {
            int v = ALPHA.indexOf(body.charAt(i));
            sum += (v < 0 ? 0 : v) * (i + 2);
        }
        return ALPHA.charAt(sum % 32);
    }

    /** Контрольная сумма сходится — описку видно сразу, без сервера. */
    public static boolean wellFormed(String s) {
        String raw = normalize(s);
        return raw.length() == 16 && checkChar(raw.substring(0, 15)) == raw.charAt(15);
    }

    /** Привести к виду GD-XXXX-XXXX-XXXX-XXXX. */
    public static String pretty(String s) {
        String raw = normalize(s);
        if (raw.isEmpty()) return "";
        StringBuilder sb = new StringBuilder("GD");
        for (int i = 0; i < raw.length(); i++) {
            if (i % 4 == 0) sb.append('-');
            sb.append(raw.charAt(i));
        }
        return sb.toString();
    }

    // --------------------------------------------------------------- память

    /** Почему не пускает: none, off (ключ отключён), revoked (отозван). */
    public static String reason = "none";

    /** Что показать игроку, когда игра не открывается. */
    public static String reasonText() {
        if (reason.equals("off")) return "Ваш ключ отключён — включите его в аккаунте на сайте";
        if (reason.equals("revoked")) return "Ваш ключ отозван администратором";
        return "Вы не ввели лицензионный ключ";
    }

    public static String key() { return Store.prefs.getString("lic_key", ""); }
    public static String keyUser() { return Store.prefs.getString("lic_user", ""); }

    public static void remember(String key, String user) {
        Store.prefs.edit().putString("lic_key", key == null ? "" : key)
                .putString("lic_user", user == null ? "" : user).apply();
    }

    public static void forget() { remember("", ""); }

    /** Запомненная активация этого же аккаунта — на случай «без интернета». */
    public static boolean localOk() {
        return !key().isEmpty() && keyUser().equalsIgnoreCase(Store.user());
    }

    /** Номер этого телефона — чтобы в админке было видно устройства. */
    public static String device() {
        try {
            String id = Settings.Secure.getString(Store.ctx.getContentResolver(),
                    Settings.Secure.ANDROID_ID);
            MessageDigest md = MessageDigest.getInstance("SHA-256");
            byte[] h = md.digest(((id == null ? "?" : id) + "|" + Build.MODEL).getBytes("UTF-8"));
            StringBuilder sb = new StringBuilder("and-");
            for (int i = 0; i < 6; i++) sb.append(String.format("%02x", h[i]));
            return sb.toString();
        } catch (Exception e) { return "and-unknown"; }
    }

    // ------------------------------------------------- поставлено установщиком?

    /** Кто поставил игру: наш установщик, магазин или вообще никто (ручная установка). */
    public static String installerPackage() {
        try {
            PackageManager pm = Store.ctx.getPackageManager();
            String pkg = Store.ctx.getPackageName();
            if (Build.VERSION.SDK_INT >= 30) {
                String s = pm.getInstallSourceInfo(pkg).getInstallingPackageName();
                return s == null ? "" : s;
            }
            String s = pm.getInstallerPackageName(pkg);
            return s == null ? "" : s;
        } catch (Exception e) { return ""; }
    }

    /**
     * Игра поставлена нашим установщиком (или магазином приложений).
     *
     * Проверяем это один раз и запоминаем: при обновлении игры телефон
     * записывает установщиком уже саму игру, и без памяти проверка начала бы
     * ругаться на честно установленную копию.
     */
    public static boolean installedProperly() {
        if (Store.prefs.getBoolean("lic_setup_ok", false)) return true;
        String p = installerPackage();
        boolean ok = p.equals(SETUP_PKG)
                || p.equals(Store.ctx.getPackageName())   // обновление самой игрой
                || p.equals("com.android.vending")        // Google Play
                || p.startsWith("ru.vk.store")            // RuStore
                || p.startsWith("com.huawei.appmarket");
        if (ok) Store.prefs.edit().putBoolean("lic_setup_ok", true).apply();
        return ok;
    }

    // ------------------------------------------------------------- проверка

    /**
     * Спросить сервер про лицензию. Сети нет — верим запомненной активации.
     * Вызывать только из фонового потока.
     */
    public static boolean check() {
        JSONObject r = Api.licenceMine();
        if (r != null) {
            String why = r.optString("reason", "");
            reason = why.isEmpty() ? "none" : why;
            if (r.optBoolean("licensed")) {
                String k = r.optString("key", "");
                remember(k.isEmpty() ? key() : k, Store.user());
                return true;
            }
            if (localOk()) forget();     // сервер ответил «нет» — держать нечего
            return false;
        }
        return localOk();
    }

    /** Активировать ключ. Возвращает текст ошибки или null, если всё вышло. */
    public static String activate(String keyText) {
        if (!wellFormed(keyText)) return "Ключ набран неверно — проверьте символы";
        JSONObject r = Api.licenceActivate(pretty(keyText), device(), "android");
        if (r == null) return Api.online ? "Ключ не принят" : "Нет связи с сервером";
        if (!r.optBoolean("ok")) return r.optString("error", "Ключ не принят");
        String k = r.optString("key", "");
        remember(k.isEmpty() ? pretty(keyText) : k, Store.user());
        return null;
    }
}
