package com.winnigames2024.forge_narevisuals.mixin;

import com.mojang.blaze3d.vertex.PoseStack;
import com.winnigames2024.forge_narevisuals.Config;
import com.winnigames2024.forge_narevisuals.client.NareSky;
import net.minecraft.client.renderer.FogParameters;
import net.minecraft.client.renderer.MultiBufferSource;
import net.minecraft.client.renderer.SkyRenderer;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

/**
 * Своё небо вместо ванильного купола.
 *
 * В Forge 1.21.4 событий отрисовки мира больше нет — кадр собирается графом
 * проходов, и подцепиться к небу можно только миксином. Инъекции не
 * обязательные: если игра поменяет сигнатуру, мод просто не нарисует небо
 * и ничего не уронит.
 */
@Mixin(SkyRenderer.class)
public class SkyRendererMixin {

    /** Ванильный цветной купол не нужен, когда сверху своя картинка. */
    @Inject(method = "renderSkyDisc", at = @At("HEAD"), cancellable = true, require = 0)
    private void narevisuals$skipDisc(float red, float green, float blue, CallbackInfo ci) {
        if (NareSky.enabled()) ci.cancel();
    }

    @Inject(method = "renderSunMoonAndStars", at = @At("HEAD"), cancellable = true, require = 0)
    private void narevisuals$customSky(PoseStack pose,
                                       MultiBufferSource.BufferSource buffers,
                                       float timeOfDay,
                                       int moonPhase,
                                       float rainBrightness,
                                       float starBrightness,
                                       FogParameters fog,
                                       CallbackInfo ci) {
        if (!NareSky.render(pose, buffers)) return;

        buffers.endBatch();
        if (!Config.get().skyKeepSun) ci.cancel();
    }
}
