package com.winnigames2024.forge_narevisuals.client;

import com.winnigames2024.forge_narevisuals.Config;
import com.winnigames2024.forge_narevisuals.Forge_narevisuals;
import com.winnigames2024.forge_narevisuals.Launch;
import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.GuiGraphics;
import net.minecraft.resources.ResourceLocation;
import net.minecraftforge.api.distmarker.Dist;
import net.minecraftforge.client.event.AddGuiOverlayLayersEvent;
import net.minecraftforge.eventbus.api.SubscribeEvent;
import net.minecraftforge.fml.common.Mod;

import java.util.ArrayList;
import java.util.List;

/**
 * Собирает элементы HUD и рисует их поверх игры.
 *
 * Начиная с 1.20.5 ванильный HUD состоит из слоёв (LayeredDraw), а старые
 * события Forge RenderGuiEvent и RegisterGuiOverlaysEvent удалены. Свой слой
 * добавляется один раз через AddGuiOverlayLayersEvent на шине мода.
 */
@Mod.EventBusSubscriber(modid = Forge_narevisuals.MODID,
                        bus = Mod.EventBusSubscriber.Bus.MOD,
                        value = Dist.CLIENT)
public final class NareHud {

    public static final List<HudModule> MODULES = new ArrayList<>();

    /** Счётчик кадров: считаем сами, чтобы не зависеть от версии API. */
    public static int measuredFps;
    private static int frames;
    private static long lastSecond;

    static {
        MODULES.add(new HudModules.Fps());
        MODULES.add(new HudModules.Ping());
        MODULES.add(new HudModules.Coords());
        MODULES.add(new HudModules.Cps());
        MODULES.add(new HudModules.Keystrokes());
        MODULES.add(new HudModules.Armor());
        MODULES.add(new HudModules.Effects());
        MODULES.add(new HudModules.Near());
        MODULES.add(new HudModules.Pointer());
        MODULES.add(new HudModules.Broken());
        MODULES.add(new HudModules.HitMarker());
        MODULES.add(new HudModules.Voice());
    }

    private NareHud() { }

    public static HudModule byId(String id) {
        for (HudModule m : MODULES)
            if (m.id.equals(id)) return m;
        return null;
    }

    @SubscribeEvent
    public static void addLayers(AddGuiOverlayLayersEvent event) {
        // типы параметров лямбды выводятся из LayeredDraw.Layer
        event.getLayeredDraw().add(
                ResourceLocation.fromNamespaceAndPath(Forge_narevisuals.MODID, "hud"),
                (graphics, delta) -> renderLayer(graphics));

        Forge_narevisuals.LOGGER.info("{}: слой HUD добавлен", Forge_narevisuals.NAME);
    }

    private static void renderLayer(GuiGraphics g) {
        if (!Launch.visuals()) return;      // без --visuals мода на экране нет

        countFrame();

        Minecraft mc = Minecraft.getInstance();
        if (!Config.get().hudEnabled) return;
        if (mc.options.hideGui) return;
        if (mc.screen instanceof HudEditScreen) return;   // там HUD рисует сам редактор

        renderAll(g, mc, false);

        Freecam.drawHint(g, mc,
                mc.getWindow().getGuiScaledWidth(), mc.getWindow().getGuiScaledHeight());
    }

    private static void countFrame() {
        frames++;
        long now = System.currentTimeMillis();
        if (lastSecond == 0) lastSecond = now;
        if (now - lastSecond >= 1000L) {
            measuredFps = (int) Math.round(frames * 1000.0 / (now - lastSecond));
            frames = 0;
            lastSecond = now;
        }
    }

    public static void renderAll(GuiGraphics g, Minecraft mc, boolean editing) {
        int w = mc.getWindow().getGuiScaledWidth();
        int h = mc.getWindow().getGuiScaledHeight();

        for (HudModule module : MODULES) {
            if (!module.isEnabled()) continue;
            if (module.needsPlayer() && mc.player == null) continue;
            if (module instanceof HudModules.HitMarker && !Config.get().hitMarker) continue;

            try {
                module.draw(g, mc, w, h);
            } catch (Exception ex) {
                // один сломавшийся элемент не должен ронять весь HUD
                Forge_narevisuals.LOGGER.debug("Элемент {} не отрисован: {}", module.id, ex.toString());
            }
        }
    }
}
