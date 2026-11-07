package com.winnigames2024.forge_narevisuals.client;

/**
 * Что разрешено на этом сервере.
 *
 * Правила присылает плагин сразу после входа. Пока плагина нет — одиночная
 * игра, чужой сервер без нашей половины — не ограничиваем ничего: запрещать
 * что-то «на всякий случай» там, где никто не просил, было бы странно.
 *
 * Важно: настройки игрока правила НЕ стирают. Человек мог годами играть с
 * яркостью, зайти на строгий сервер и выйти — вернуться всё должно таким,
 * каким было. Поэтому в Config лежит то, чего хочет игрок, а здесь — что из
 * этого сейчас можно, и код спрашивает именно отсюда.
 */
public final class NareRules {

    private static volatile boolean known;      // сервер прислал правила

    private static volatile boolean freecam = true;
    private static volatile int freecamRange;   // блоков, 0 — без предела
    private static volatile boolean espPlayers = true;
    private static volatile boolean espMobs = true;
    private static volatile boolean bright = true;
    private static volatile boolean zoom = true;

    private NareRules() { }

    /** Запоминает правила с сервера. */
    public static void set(boolean freecamOn, int range, boolean players,
                           boolean mobs, boolean brightOn, boolean zoomOn) {
        freecam = freecamOn;
        freecamRange = Math.max(0, range);
        espPlayers = players;
        espMobs = mobs;
        bright = brightOn;
        zoom = zoomOn;
        known = true;
    }

    /** Забываем правила: вышли с сервера. */
    public static void forget() {
        known = false;
        freecam = true;
        freecamRange = 0;
        espPlayers = true;
        espMobs = true;
        bright = true;
        zoom = true;
    }

    /** Сервер вообще что-то говорил про правила. */
    public static boolean known() { return known; }

    public static boolean freecam() { return !known || freecam; }
    public static boolean espPlayers() { return !known || espPlayers; }
    public static boolean espMobs() { return !known || espMobs; }
    public static boolean bright() { return !known || bright; }
    public static boolean zoom() { return !known || zoom; }

    /** На сколько блоков можно улететь свободной камерой. 0 — без предела. */
    public static int freecamRange() { return known ? freecamRange : 0; }

    /** Подпись для строки настройки, которую сервер запретил. */
    public static String why() { return "запрещено на этом сервере"; }

    /** Коротко, что нам тут можно — для экрана настроек. */
    public static String text() {
        if (!known) return "сервер правил не задаёт";

        var off = new java.util.ArrayList<String>();
        if (!freecam) off.add("свободная камера");
        if (!espPlayers) off.add("подсветка игроков");
        if (!espMobs) off.add("подсветка существ");
        if (!bright) off.add("яркость");
        if (!zoom) off.add("приближение");

        if (off.isEmpty())
            return freecamRange > 0
                    ? "можно всё, камера — не дальше " + freecamRange + " бл."
                    : "сервер разрешает всё";

        String text = "нельзя: " + String.join(", ", off);
        return freecamRange > 0 ? text + " · камера не дальше " + freecamRange + " бл." : text;
    }
}
