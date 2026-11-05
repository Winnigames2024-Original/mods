package com.winnigames2024.forge_narevisuals.client;

import com.mojang.blaze3d.platform.NativeImage;
import com.mojang.blaze3d.vertex.PoseStack;
import com.mojang.blaze3d.vertex.VertexConsumer;
import com.mojang.math.Axis;
import com.winnigames2024.forge_narevisuals.Config;
import com.winnigames2024.forge_narevisuals.Forge_narevisuals;
import com.winnigames2024.forge_narevisuals.Launch;
import net.minecraft.client.Minecraft;
import net.minecraft.client.renderer.MultiBufferSource;
import net.minecraft.client.renderer.RenderType;
import net.minecraft.client.renderer.texture.DynamicTexture;
import net.minecraft.resources.ResourceLocation;
import org.joml.Matrix4f;

import java.io.InputStream;
import java.nio.file.Files;
import java.nio.file.Path;

/**
 * Своё небо: куб вокруг игрока, обтянутый одной картинкой.
 *
 * Картинка — либо встроенный пресет из ресурсов мода, либо файл из
 * config/narevisuals/images. Рисуется тем же типом отрисовки, которым игра
 * рисует солнце и луну, поэтому ложится поверх неба и не трогает глубину.
 */
public final class NareSky {

    private static final String[] PRESETS = { "nebula", "sunset", "aurora", "dawn", "void" };

    private static final ResourceLocation OWN =
            ResourceLocation.fromNamespaceAndPath(Forge_narevisuals.MODID, "dynamic/sky");

    private static String loadedFile;
    private static DynamicTexture texture;

    private NareSky() { }

    public static boolean enabled() {
        return Launch.visuals() && Config.get().customSky;
    }

    /** @return true, если небо нарисовано — значит ванильный диск можно не рисовать. */
    public static boolean render(PoseStack ps, MultiBufferSource buffers) {
        if (!enabled()) return false;

        Config cfg = Config.get();
        ResourceLocation tex = resolve(cfg);
        if (tex == null) return false;

        ps.pushPose();

        if (cfg.skySpin) {
            // полный оборот примерно за двадцать минут — заметно, но не мельтешит
            float angle = (System.currentTimeMillis() % 1200000L) / 1200000f * 360f;
            ps.mulPose(Axis.YP.rotationDegrees(angle));
        }

        VertexConsumer vc = buffers.getBuffer(RenderType.celestial(tex));
        cube(ps, vc, 100f);

        ps.popPose();
        return true;
    }

    private static void cube(PoseStack ps, VertexConsumer vc, float r) {
        for (int face = 0; face < 6; face++) {
            ps.pushPose();

            switch (face) {
                case 1 -> ps.mulPose(Axis.XP.rotationDegrees(90f));
                case 2 -> ps.mulPose(Axis.XP.rotationDegrees(-90f));
                case 3 -> ps.mulPose(Axis.XP.rotationDegrees(180f));
                case 4 -> ps.mulPose(Axis.ZP.rotationDegrees(90f));
                case 5 -> ps.mulPose(Axis.ZP.rotationDegrees(-90f));
                default -> { }
            }

            Matrix4f m = ps.last().pose();
            vc.addVertex(m, -r, -r, -r).setUv(0f, 0f).setColor(-1);
            vc.addVertex(m, -r, -r,  r).setUv(0f, 1f).setColor(-1);
            vc.addVertex(m,  r, -r,  r).setUv(1f, 1f).setColor(-1);
            vc.addVertex(m,  r, -r, -r).setUv(1f, 0f).setColor(-1);

            ps.popPose();
        }
    }

    // ------------------------------------------------------------ картинка

    private static ResourceLocation resolve(Config cfg) {
        String own = cfg.skyImage;
        if (own != null && !own.isEmpty()) {
            ensureOwn(own);
            return texture != null ? OWN : null;
        }

        release();

        String preset = cfg.skyPreset;
        if (preset == null || preset.isEmpty()) preset = PRESETS[0];

        boolean known = false;
        for (String p : PRESETS) if (p.equals(preset)) { known = true; break; }
        if (!known) preset = PRESETS[0];

        return ResourceLocation.fromNamespaceAndPath(
                Forge_narevisuals.MODID, "textures/environment/sky_" + preset + ".png");
    }

    private static void ensureOwn(String file) {
        if (file.equals(loadedFile) && texture != null) return;
        loadedFile = file;
        release();

        Path path = Config.images().resolve(file);
        if (!Files.isRegularFile(path)) {
            Forge_narevisuals.LOGGER.warn("{}: картинка неба не найдена: {}",
                    Forge_narevisuals.NAME, path);
            return;
        }

        try (InputStream in = Files.newInputStream(path)) {
            NativeImage image = NativeImage.read(in);
            texture = new DynamicTexture(image);
            Minecraft.getInstance().getTextureManager().register(OWN, texture);
        } catch (Exception ex) {
            Forge_narevisuals.LOGGER.warn("{}: картинку неба не прочитать: {}",
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
            Minecraft.getInstance().getTextureManager().release(OWN);
            texture.close();
            texture = null;
            loadedFile = null;
        }
    }
}
