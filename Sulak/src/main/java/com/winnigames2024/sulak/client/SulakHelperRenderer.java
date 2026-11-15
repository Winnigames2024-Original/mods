package com.winnigames2024.sulak.client;

import com.winnigames2024.sulak.Sulak;
import com.winnigames2024.sulak.entity.SulakHelperEntity;
import net.minecraft.client.model.HumanoidModel;
import net.minecraft.client.model.geom.ModelLayers;
import net.minecraft.client.renderer.entity.EntityRendererProvider;
import net.minecraft.client.renderer.entity.HumanoidMobRenderer;
import net.minecraft.resources.ResourceLocation;

public class SulakHelperRenderer
        extends HumanoidMobRenderer<SulakHelperEntity, SulakHelperRenderState, HumanoidModel<SulakHelperRenderState>> {

    private static final ResourceLocation CALM = Sulak.id("textures/entity/sulak.png");
    private static final ResourceLocation ANGRY = Sulak.id("textures/entity/sulak_angry.png");

    public SulakHelperRenderer(EntityRendererProvider.Context ctx) {
        super(ctx, new HumanoidModel<>(ctx.bakeLayer(ModelLayers.PLAYER)), 0.5F);
    }

    @Override
    public SulakHelperRenderState createRenderState() {
        return new SulakHelperRenderState();
    }

    @Override
    public void extractRenderState(SulakHelperEntity entity, SulakHelperRenderState state, float partialTick) {
        super.extractRenderState(entity, state, partialTick);
        state.angry = entity.isAngry();
    }

    @Override
    public ResourceLocation getTextureLocation(SulakHelperRenderState state) {
        return state.angry ? ANGRY : CALM;
    }
}
