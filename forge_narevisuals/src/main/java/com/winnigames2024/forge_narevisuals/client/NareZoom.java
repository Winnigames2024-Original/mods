package com.winnigames2024.forge_narevisuals.client;

import com.winnigames2024.forge_narevisuals.Config;
import com.winnigames2024.forge_narevisuals.Forge_narevisuals;
import com.winnigames2024.forge_narevisuals.Launch;
import net.minecraft.client.Minecraft;
import net.minecraftforge.api.distmarker.Dist;
import net.minecraftforge.client.event.ViewportEvent;
import net.minecraftforge.eventbus.api.SubscribeEvent;
import net.minecraftforge.fml.common.Mod;

/**
 * Приближение, пока держишь клавишу. Меняется только угол обзора —
 * ни дальность прорисовки, ни что-либо ещё игра не трогает.
 */
@Mod.EventBusSubscriber(modid = Forge_narevisuals.MODID, value = Dist.CLIENT)
public final class NareZoom {

    private static boolean zooming;

    private NareZoom() { }

    public static boolean zooming() {
        return zooming;
    }

    @SubscribeEvent
    public static void onFov(ViewportEvent.ComputeFov event) {
        Minecraft mc = Minecraft.getInstance();

        zooming = Launch.visuals()
                && Config.get().zoomEnabled
                && NareRules.zoom()
                && mc.screen == null
                && NareKeys.ZOOM.isDown();

        if (!zooming) return;

        int fov = Math.max(5, Math.min(110, Config.get().zoomFov));
        event.setFOV(fov);
    }
}
