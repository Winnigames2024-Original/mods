package com.winnigames2024.forge_narevisuals.client;

import com.winnigames2024.forge_narevisuals.Config;
import com.winnigames2024.forge_narevisuals.Forge_narevisuals;
import com.winnigames2024.forge_narevisuals.Launch;
import net.minecraft.client.Minecraft;
import net.minecraft.world.effect.MobEffectInstance;
import net.minecraft.world.effect.MobEffects;
import net.minecraftforge.api.distmarker.Dist;
import net.minecraftforge.event.TickEvent;
import net.minecraftforge.eventbus.api.SubscribeEvent;
import net.minecraftforge.fml.common.Mod;

/**
 * Яркость: поднятая гамма и ночное зрение.
 *
 * Эффект ночного зрения ставится только на клиенте — сервер о нём не знает и
 * ничего не получает. Гамма запоминается при включении и возвращается на место
 * при выключении, чтобы не портить настройки игрока.
 */
@Mod.EventBusSubscriber(modid = Forge_narevisuals.MODID, value = Dist.CLIENT)
public final class NareVision {

    private static Double savedGamma;
    private static boolean effectApplied;

    private NareVision() { }

    @SubscribeEvent
    public static void onTick(TickEvent.ClientTickEvent.Post event) {
        NareKeys.migrate();

        Minecraft mc = Minecraft.getInstance();
        Config cfg = Config.get();

        boolean bright = Launch.visuals() && cfg.fullbright;
        boolean vision = Launch.visuals() && cfg.nightVision;

        // --- гамма ---
        if (bright) {
            if (savedGamma == null) savedGamma = mc.options.gamma().get();
            if (mc.options.gamma().get() < 1.0) mc.options.gamma().set(1.0);
        } else if (savedGamma != null) {
            mc.options.gamma().set(savedGamma);
            savedGamma = null;
        }

        // --- ночное зрение ---
        if (mc.player == null) {
            effectApplied = false;
            return;
        }

        if (vision) {
            MobEffectInstance active = mc.player.getEffect(MobEffects.NIGHT_VISION);
            if (active == null || active.getDuration() < 220) {
                mc.player.addEffect(new MobEffectInstance(
                        MobEffects.NIGHT_VISION, 1200, 0, false, false, false));
            }
            effectApplied = true;
        } else if (effectApplied) {
            mc.player.removeEffect(MobEffects.NIGHT_VISION);
            effectApplied = false;
        }
    }

    /** Вернуть гамму при выходе из мира, чтобы меню не светилось. */
    public static void restore() {
        if (savedGamma != null) {
            Minecraft.getInstance().options.gamma().set(savedGamma);
            savedGamma = null;
        }
    }
}
