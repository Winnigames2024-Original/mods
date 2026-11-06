package com.winnigames2024.forge_narevisuals.mixin;

import com.winnigames2024.forge_narevisuals.client.NareFps;
import net.minecraft.client.renderer.culling.Frustum;
import net.minecraft.client.renderer.entity.EntityRenderDispatcher;
import net.minecraft.world.entity.Entity;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

/**
 * FPS+: здесь игра решает, попадает ли сущность в кадр. Мы отвечаем «нет» за
 * тех, кого договорились не рисовать.
 *
 * Место выбрано нарочно: это не подмена отрисовки, а штатный вопрос «рисовать
 * ли вообще». Сущность остаётся жить своей жизнью, сервер о ней знает, просто
 * мы её не показываем.
 */
@Mixin(EntityRenderDispatcher.class)
public class EntityCullMixin {

    @Inject(method = "shouldRender", at = @At("HEAD"), cancellable = true, require = 0)
    private <E extends Entity> void narevisuals$cull(E entity, Frustum frustum,
                                                     double camX, double camY, double camZ,
                                                     CallbackInfoReturnable<Boolean> cir) {
        if (NareFps.skip(entity)) cir.setReturnValue(false);
    }
}
