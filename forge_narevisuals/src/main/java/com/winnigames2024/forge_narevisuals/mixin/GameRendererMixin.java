package com.winnigames2024.forge_narevisuals.mixin;

import com.winnigames2024.forge_narevisuals.client.Freecam;
import net.minecraft.client.renderer.GameRenderer;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

/**
 * Игра выбирает цель под прицелом от «сущности камеры». Пока включена
 * свободная камера, этой сущностью становится наш невидимый призрак — и
 * ванильный расчёт начинает видеть тело самого игрока как обычную цель.
 * Удар по себе сервер считает ошибкой и рвёт соединение
 * («Cannot interact with self!»), а дальность всё равно обрезается
 * обычными 4.5 блока.
 *
 * Поэтому сразу после ванильного расчёта пересчитываем цель сами: от камеры,
 * на свою дальность и без тела игрока в списке целей.
 */
@Mixin(GameRenderer.class)
public class GameRendererMixin {

    @Inject(method = "pick(F)V", at = @At("RETURN"), require = 0)
    private void narevisuals$freecamPick(float partialTick, CallbackInfo ci) {
        Freecam.repick(partialTick);
    }
}
