package com.winnigames2024.forge_narevisuals;

import java.lang.management.ManagementFactory;

/**
 * Включатель мода. NareVisuals показывает себя только если игра запущена
 * с аргументом --visuals. Без него мод загружен, но полностью молчит:
 * ни HUD, ни кнопок, ни перехвата клавиш.
 *
 * Аргумент ищем в командной строке процесса, в аргументах JVM и в системном
 * свойстве -Dnarevisuals=true — какой-нибудь из путей работает всегда.
 */
public final class Launch {

    public static final String ARG = "--visuals";
    public static final String PROPERTY = "narevisuals";

    private static Boolean enabled;
    private static String reason = "аргумент " + ARG + " не передан";

    private Launch() { }

    public static boolean visuals() {
        if (enabled == null) {
            enabled = detect();
            Forge_narevisuals.LOGGER.info("{}: визуалы {} ({})",
                    Forge_narevisuals.NAME, enabled ? "включены" : "выключены", reason);
        }
        return enabled;
    }

    public static String reason() {
        visuals();
        return reason;
    }

    private static boolean detect() {
        String property = System.getProperty(PROPERTY);
        if (property != null && !property.equalsIgnoreCase("false") && !property.equals("0")) {
            reason = "задано свойство -D" + PROPERTY;
            return true;
        }

        if (hasArg(System.getProperty("sun.java.command"))) {
            reason = "передан " + ARG;
            return true;
        }

        try {
            for (String argument : ManagementFactory.getRuntimeMXBean().getInputArguments()) {
                if (hasArg(argument)) {
                    reason = "передан " + ARG;
                    return true;
                }
            }
        } catch (Throwable ignored) {
            // на некоторых сборках JVM доступа к своим аргументам нет — это не беда
        }

        return false;
    }

    // ------------------------------------------------- прочие аргументы

    /**
     * Значение аргумента вида --onjoin=... или --onjoin "..." из командной
     * строки. Лаунчер кладёт сюда команды, которые надо выполнить при входе,
     * и признак «с этого сервера не выходить».
     */
    public static String value(String name) {
        String property = System.getProperty("narevisuals." + name.replace("--", ""));
        if (property != null && !property.isBlank()) return property;

        String line = System.getProperty("sun.java.command");
        if (line == null || line.isEmpty()) return "";

        // аргумент мог приехать в кавычках, поэтому режем по ним аккуратно
        int at = line.indexOf(name);
        if (at < 0) return "";

        String rest = line.substring(at + name.length()).trim();
        if (rest.startsWith("=")) rest = rest.substring(1).trim();
        if (rest.isEmpty()) return "";

        if (rest.charAt(0) == '"') {
            int end = rest.indexOf('"', 1);
            return end > 1 ? rest.substring(1, end) : "";
        }

        int space = rest.indexOf(' ');
        return space < 0 ? rest : rest.substring(0, space);
    }

    /** Есть ли такой аргумент вообще, без значения. */
    public static boolean flag(String name) {
        String property = System.getProperty("narevisuals." + name.replace("--", ""));
        if (property != null && !property.equalsIgnoreCase("false")) return true;

        return hasArg(System.getProperty("sun.java.command"), name);
    }

    private static boolean hasArg(String line, String want) {
        if (line == null || line.isEmpty()) return false;

        for (String part : line.split("\\s+"))
            if (part.equalsIgnoreCase(want)) return true;

        return false;
    }

    private static boolean hasArg(String line) {
        if (line == null || line.isEmpty()) return false;

        for (String part : line.split("\\s+")) {
            if (part.equalsIgnoreCase(ARG)) return true;
            if (part.equalsIgnoreCase("-visuals")) return true;
        }
        return false;
    }
}
