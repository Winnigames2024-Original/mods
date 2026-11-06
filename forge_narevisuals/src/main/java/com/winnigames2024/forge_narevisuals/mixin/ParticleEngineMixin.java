package com.winnigames2024.forge_narevisuals.mixin;

import com.winnigames2024.forge_narevisuals.client.NareFps;
import net.minecraft.client.particle.Particle;
import net.minecraft.client.particle.ParticleEngine;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

/**
 * FPS+ и взрывы. Полсотни динамита разом — это десятки тысяч частиц за один
 * тик, и кадр встаёт колом именно на них, а не на самом взрыве.
 *
 * Здесь стоит счётчик на тик: пока бюджет не выбран, частицы добавляются как
 * обычно, дальше новые просто не берутся. Взрыв всё равно виден — первые
 * сотни частиц рисуют его целиком, остальные легли бы поверх.
 */
@Mixin(ParticleEngine.class)
public class ParticleEngineMixin {

    @Inject(method = "add", at = @At("HEAD"), cancellable = true, require = 0)
    private void narevisuals$budget(Particle particle, CallbackInfo ci) {
        if (!NareFps.allowParticle()) ci.cancel();
    }
}
