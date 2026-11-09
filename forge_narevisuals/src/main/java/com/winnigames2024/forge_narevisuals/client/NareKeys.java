package com.winnigames2024.forge_narevisuals.client;

import com.mojang.blaze3d.platform.InputConstants;
import com.winnigames2024.forge_narevisuals.Config;
import com.winnigames2024.forge_narevisuals.Forge_narevisuals;
import net.minecraft.client.Minecraft;
import net.minecraft.client.KeyMapping;
import net.minecraftforge.api.distmarker.Dist;
import net.minecraftforge.client.event.RegisterKeyMappingsEvent;
import net.minecraftforge.client.settings.KeyModifier;
import net.minecraftforge.eventbus.api.SubscribeEvent;
import net.minecraftforge.fml.common.Mod;

@Mod.EventBusSubscriber(modid = Forge_narevisuals.MODID,
                        bus = Mod.EventBusSubscriber.Bus.MOD,
                        value = Dist.CLIENT)
public final class NareKeys {

    public static final String CATEGORY = "key.categories.narevisuals";

    public static final KeyMapping OPEN_SETTINGS = new KeyMapping(
            "key.narevisuals.settings", InputConstants.KEY_RSHIFT, CATEGORY);

    public static final KeyMapping TOGGLE_HUD = new KeyMapping(
            "key.narevisuals.toggle", InputConstants.KEY_H, CATEGORY);

    public static final KeyMapping TOGGLE_ESP = new KeyMapping(
            "key.narevisuals.esp", InputConstants.KEY_J, CATEGORY);

    public static final KeyMapping TOGGLE_BRIGHT = new KeyMapping(
            "key.narevisuals.bright", InputConstants.KEY_K, CATEGORY);

    /** Телепорт к игроку: открыть список и перенестись. По умолчанию G. */
    public static final KeyMapping TELEPORT = new KeyMapping(
            "key.narevisuals.teleport", InputConstants.KEY_G, CATEGORY);

    public static final KeyMapping TOGGLE_FREECAM = new KeyMapping(
            "key.narevisuals.freecam", InputConstants.KEY_F, CATEGORY);

    public static final KeyMapping TOGGLE_SKY = new KeyMapping(
            "key.narevisuals.sky", InputConstants.UNKNOWN.getValue(), CATEGORY);

    public static final KeyMapping ZOOM = new KeyMapping(
            "key.narevisuals.zoom", InputConstants.KEY_C, CATEGORY);

    public static final KeyMapping TOGGLE_NEAR = new KeyMapping(
            "key.narevisuals.near", InputConstants.KEY_B, CATEGORY);

    /** Двигать элементы HUD прямо в игре, не заходя в настройки. */
    public static final KeyMapping MOVE_HUD = new KeyMapping(
            "key.narevisuals.movehud", InputConstants.KEY_RBRACKET, CATEGORY);

    /** Открыть меню голоса: микрофон, громкости, кто говорит. */
    public static final KeyMapping VOICE = new KeyMapping(
            "key.narevisuals.voice", InputConstants.KEY_V, CATEGORY);

    /**
     * Говорить. Отдельно от меню: V открывает окно, а эта клавиша держится
     * во время разговора. По умолчанию левый Alt — игра его ничем не занимает.
     */
    public static final KeyMapping TALK = new KeyMapping(
            "key.narevisuals.talk", org.lwjgl.glfw.GLFW.GLFW_KEY_LEFT_ALT, CATEGORY);

    /** Для экрана назначения клавиш: сами привязки и их подписи. */
    public static final KeyMapping[] ALL = {
            OPEN_SETTINGS, TOGGLE_HUD, TOGGLE_ESP, TOGGLE_BRIGHT, TELEPORT,
            TOGGLE_FREECAM, TOGGLE_SKY, ZOOM, TOGGLE_NEAR, MOVE_HUD, VOICE, TALK
    };

    public static final String[] TITLES = {
            "Открыть настройки", "Спрятать HUD", "Подсветка игроков",
            "Яркость", "Телепорт к игроку", "Свободная камера", "Своё небо",
            "Приближение (держать)", "Игроки рядом", "Передвинуть элементы",
            "Меню голоса", "Говорить (держать)"
    };

    private static boolean migrated;

    private NareKeys() { }

    /**
     * Свободная камера раньше жила на N. Один раз переносим её на F —
     * но только если игрок сам ничего не менял и на ней всё ещё N.
     */
    public static void migrate() {
        if (migrated) return;
        migrated = true;

        Config cfg = Config.get();
        boolean changed = false;

        // свободная камера раньше была на N — один раз переносим на F
        if (!cfg.freecamKeyMoved) {
            cfg.freecamKeyMoved = true;
            if (TOGGLE_FREECAM.getKey().getValue() == InputConstants.KEY_N) {
                TOGGLE_FREECAM.setKeyModifierAndCode(KeyModifier.NONE,
                        InputConstants.getKey(InputConstants.KEY_F, 0));
                changed = true;
            }
        }

        // яркость переезжает с G на K, потому что G теперь занят телепортом —
        // но только если игрок сам не менял её с дефолтной G
        if (!cfg.brightKeyMoved) {
            cfg.brightKeyMoved = true;
            if (TOGGLE_BRIGHT.getKey().getValue() == InputConstants.KEY_G) {
                TOGGLE_BRIGHT.setKeyModifierAndCode(KeyModifier.NONE,
                        InputConstants.getKey(InputConstants.KEY_K, 0));
                changed = true;
            }
        }

        Config.save();

        if (changed) {
            Minecraft mc = Minecraft.getInstance();
            if (mc != null) {
                mc.options.save();
                KeyMapping.resetMapping();
            }
        }
    }

    @SubscribeEvent
    public static void register(RegisterKeyMappingsEvent event) {
        for (KeyMapping key : ALL) event.register(key);
    }
}
