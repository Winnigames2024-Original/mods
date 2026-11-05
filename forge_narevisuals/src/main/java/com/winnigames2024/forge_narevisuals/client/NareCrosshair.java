package com.winnigames2024.forge_narevisuals.client;

import com.mojang.blaze3d.platform.NativeImage;
import com.winnigames2024.forge_narevisuals.Config;
import com.winnigames2024.forge_narevisuals.Forge_narevisuals;
import com.winnigames2024.forge_narevisuals.Launch;
import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.GuiGraphics;
import net.minecraft.client.renderer.RenderType;
import net.minecraft.client.renderer.texture.DynamicTexture;
import net.minecraft.resources.ResourceLocation;
import net.minecraftforge.api.distmarker.Dist;
import net.minecraftforge.client.event.AddGuiOverlayLayersEvent;
import net.minecraftforge.client.gui.overlay.ForgeLayeredDraw;
import net.minecraftforge.eventbus.api.SubscribeEvent;
import net.minecraftforge.fml.common.Mod;

import java.io.InputStream;
import java.nio.file.Files;
import java.nio.file.Path;

/**
 * Свой прицел: либо картинка из config/narevisuals/images, либо нарисованный
 * вручную аккуратный крест.
 *
 * Ванильный прицел не убирается, а получает условие — он рисуется только пока
 * свой выключен. Так ничего не ломается, если мод отключить.
 */
@Mod.EventBusSubscriber(modid = Forge_narevisuals.MODID,
                        bus = Mod.EventBusSubscriber.Bus.MOD,
                        value = Dist.CLIENT)
public final class NareCrosshair {

    private static final ResourceLocation TEXTURE =
            ResourceLocation.fromNamespaceAndPath(Forge_narevisuals.MODID, "dynamic/crosshair");

    private static final ResourceLocation LAYER =
            ResourceLocation.fromNamespaceAndPath(Forge_narevisuals.MODID, "crosshair");

    private static String loadedFile = null;
    private static DynamicTexture texture;
    private static int texW, texH;

    private NareCrosshair() { }

    public static boolean active() {
        return Launch.visuals() && Config.get().customCrosshair;
    }

    @SubscribeEvent
    public static void addLayers(AddGuiOverlayLayersEvent event) {
        ForgeLayeredDraw root = event.getLayeredDraw();

        root.addConditionTo(ForgeLayeredDraw.PRE_SLEEP_STACK,
                ForgeLayeredDraw.CROSSHAIR, () -> !active());

        root.addAbove(ForgeLayeredDraw.PRE_SLEEP_STACK, LAYER,
                ForgeLayeredDraw.CROSSHAIR, (g, delta) -> render(g));

        Forge_narevisuals.LOGGER.info("{}: слой прицела добавлен", Forge_narevisuals.NAME);
    }

    private static void render(GuiGraphics g) {
        if (!active()) return;

        Minecraft mc = Minecraft.getInstance();
        if (mc.options.hideGui || mc.screen != null) return;
        if (mc.player == null) return;
        if (!mc.options.getCameraType().isFirstPerson() && !Freecam.active()) return;

        Config cfg = Config.get();
        int w = mc.getWindow().getGuiScaledWidth();
        int h = mc.getWindow().getGuiScaledHeight();

        ensureTexture(cfg.crosshairImage);

        int size = Math.max(4, Math.min(64, cfg.crosshairSize));
        int x = (w - size) / 2;
        int y = (h - size) / 2;

        if (texture != null) {
            g.blit(RenderType::guiTextured, TEXTURE, x, y, 0f, 0f,
                    size, size, texW, texH, texW, texH, cfg.crosshairColor);
        } else {
            drawCross(g, w / 2, h / 2, size, cfg.crosshairColor);
        }
    }

    /** Запасной прицел, когда картинка не выбрана: тонкий крест с просветом. */
    private static void drawCross(GuiGraphics g, int cx, int cy, int size, int color) {
        int arm = Math.max(2, size / 2 - 2);
        int gap = Math.max(1, size / 8);

        g.fill(cx - arm, cy, cx - gap, cy + 1, color);
        g.fill(cx + gap + 1, cy, cx + arm + 1, cy + 1, color);
        g.fill(cx, cy - arm, cx + 1, cy - gap, color);
        g.fill(cx, cy + gap + 1, cx + 1, cy + arm + 1, color);
        g.fill(cx, cy, cx + 1, cy + 1, color);
    }

    // ------------------------------------------------------------ картинка

    private static void ensureTexture(String file) {
        if (file == null) file = "";

        if (file.equals(loadedFile)) return;
        loadedFile = file;

        release();

        if (file.isEmpty()) return;

        Path path = Config.images().resolve(file);
        if (!Files.isRegularFile(path)) {
            Forge_narevisuals.LOGGER.warn("{}: картинка прицела не найдена: {}",
                    Forge_narevisuals.NAME, path);
            return;
        }

        try (InputStream in = Files.newInputStream(path)) {
            NativeImage image = NativeImage.read(in);
            texW = image.getWidth();
            texH = image.getHeight();
            texture = new DynamicTexture(image);
            Minecraft.getInstance().getTextureManager().register(TEXTURE, texture);
        } catch (Exception ex) {
            Forge_narevisuals.LOGGER.warn("{}: картинку прицела не прочитать: {}",
                    Forge_narevisuals.NAME, ex.toString());
            texture = null;
        }
    }

    /** Сбросить текстуру, чтобы следующий кадр перечитал файл. */
    public static void reload() {
        loadedFile = null;
    }

    private static void release() {
        if (texture != null) {
            Minecraft.getInstance().getTextureManager().release(TEXTURE);
            texture.close();
            texture = null;
        }
    }
}
