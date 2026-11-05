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
            "key.narevisuals.bright", InputConstants.KEY_G, CATEGORY);

    public static final KeyMapping TOGGLE_FREECAM = new KeyMapping(
            "key.narevisuals.freecam", InputConstants.KEY_F, CATEGORY);

    public static final KeyMapping TOGGLE_SKY = new KeyMapping(
            "key.narevisuals.sky", InputConstants.UNKNOWN.getValue(), CATEGORY);

    public static final KeyMapping ZOOM = new KeyMapping(
            "key.narevisuals.zoom", InputConstants.KEY_C, CATEGORY);

    public static final KeyMapping TOGGLE_NEAR = new KeyMapping(
            "key.narevisuals.near", InputConstants.KEY_B, CATEGORY);

    /** Для экрана назначения клавиш: сами привязки и их подписи. */
    public static final KeyMapping[] ALL = {
            OPEN_SETTINGS, TOGGLE_HUD, TOGGLE_ESP, TOGGLE_BRIGHT,
            TOGGLE_FREECAM, TOGGLE_SKY, ZOOM, TOGGLE_NEAR
    };

    public static final String[] TITLES = {
            "Открыть настройки", "Спрятать HUD", "Подсветка игроков",
            "Яркость", "Свободная камера", "Своё небо",
            "Приближение (держать)", "Игроки рядом"
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
        if (cfg.freecamKeyMoved) return;

        cfg.freecamKeyMoved = true;
        Config.save();

        if (TOGGLE_FREECAM.getKey().getValue() != InputConstants.KEY_N) return;

        TOGGLE_FREECAM.setKeyModifierAndCode(KeyModifier.NONE,
                InputConstants.getKey(InputConstants.KEY_F, 0));

        Minecraft mc = Minecraft.getInstance();
        if (mc != null) {
            mc.options.save();
            KeyMapping.resetMapping();
        }
    }

    @SubscribeEvent
    public static void register(RegisterKeyMappingsEvent event) {
        for (KeyMapping key : ALL) event.register(key);
    }
}
