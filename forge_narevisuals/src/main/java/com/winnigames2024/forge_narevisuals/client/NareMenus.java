package com.winnigames2024.forge_narevisuals.client;

import com.winnigames2024.forge_narevisuals.Config;
import net.minecraft.client.gui.screens.Screen;

/**
 * Какое меню открывать. Выбор живёт в настройках, а спрашивают про него из
 * трёх мест, поэтому решение собрано здесь, а не размазано по коду.
 */
public final class NareMenus {

    /** Названия — они же то, что видит игрок. */
    public static final String[] STYLES = { "Обычный", "Панели" };

    public static final String[] HINTS = {
            "Окно с разделами",
            "Окошки по категориям поверх игры"
    };

    private NareMenus() { }

    public static String style() {
        String saved = Config.get().menuStyle;
        for (String known : STYLES)
            if (known.equalsIgnoreCase(saved)) return known;
        return STYLES[0];
    }

    public static boolean panels() {
        return STYLES[1].equalsIgnoreCase(style());
    }

    /** Следующий тип по кругу. */
    public static void next() {
        String now = style();
        int index = 0;
        for (int i = 0; i < STYLES.length; i++)
            if (STYLES[i].equals(now)) { index = i; break; }

        Config.get().menuStyle = STYLES[(index + 1) % STYLES.length];
        Config.save();
    }

    public static String hint() {
        String now = style();
        for (int i = 0; i < STYLES.length; i++)
            if (STYLES[i].equals(now)) return HINTS[i];
        return HINTS[0];
    }

    /** Экран настроек того типа, который выбран. */
    public static Screen open(Screen parent) {
        return panels() ? new NareClickScreen(parent) : new NareSettingsScreen(parent);
    }
}
