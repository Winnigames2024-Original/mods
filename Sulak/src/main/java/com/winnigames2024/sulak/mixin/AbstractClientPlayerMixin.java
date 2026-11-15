package com.winnigames2024.sulak.mixin;

import com.winnigames2024.sulak.Config;
import com.winnigames2024.sulak.client.CurseClient;
import net.minecraft.client.player.AbstractClientPlayer;
import net.minecraft.core.ClientAsset;
import net.minecraft.world.entity.player.PlayerSkin;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

import java.util.Optional;

/** Подменяет скин игрока, у которого кончились желания. */
@Mixin(AbstractClientPlayer.class)
public abstract class AbstractClientPlayerMixin {

    @Inject(method = "getSkin", at = @At("RETURN"), cancellable = true)
    private void sulak$cursedSkin(CallbackInfoReturnable<PlayerSkin> cir) {
        if (!Config.curseChangesSkin) return;
        AbstractClientPlayer self = (AbstractClientPlayer) (Object) this;
        if (!CurseClient.isCursed(self)) return;

        PlayerSkin original = cir.getReturnValue();
        if (original == null) return;

        PlayerSkin.Patch patch = PlayerSkin.Patch.create(
                Optional.of(new ClientAsset.ResourceTexture(CurseClient.CURSED_SKIN)),
                Optional.empty(),
                Optional.empty(),
                Optional.empty());
        cir.setReturnValue(original.with(patch));
    }
}
