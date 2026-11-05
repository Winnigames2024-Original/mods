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

    private static boolean hasArg(String line) {
        if (line == null || line.isEmpty()) return false;

        for (String part : line.split("\\s+")) {
            if (part.equalsIgnoreCase(ARG)) return true;
            if (part.equalsIgnoreCase("-visuals")) return true;
        }
        return false;
    }
}
