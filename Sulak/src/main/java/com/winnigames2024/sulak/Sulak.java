package com.winnigames2024.sulak;

import com.mojang.logging.LogUtils;
import com.winnigames2024.sulak.entity.SulakHelperEntity;
import net.minecraft.core.registries.Registries;
import net.minecraft.resources.ResourceKey;
import net.minecraft.resources.ResourceLocation;
import net.minecraft.world.entity.EntityType;
import net.minecraft.world.entity.MobCategory;
import net.minecraftforge.event.entity.EntityAttributeCreationEvent;
import net.minecraftforge.eventbus.api.bus.BusGroup;
import net.minecraftforge.fml.common.Mod;
import net.minecraftforge.fml.config.ModConfig;
import net.minecraftforge.fml.javafmlmod.FMLJavaModLoadingContext;
import net.minecraftforge.registries.DeferredRegister;
import net.minecraftforge.registries.ForgeRegistries;
import net.minecraftforge.registries.RegistryObject;
import org.slf4j.Logger;

/**
 * Sulak - AI helper NPC for Minecraft Forge 1.21.10.
 * Works on dedicated servers and in singleplayer.
 */
@Mod(Sulak.MODID)
public class Sulak {

    public static final String MODID = "sulak";
    public static final Logger LOGGER = LogUtils.getLogger();

    public static final DeferredRegister<EntityType<?>> ENTITY_TYPES =
            DeferredRegister.create(ForgeRegistries.ENTITY_TYPES, MODID);

    public static final RegistryObject<EntityType<SulakHelperEntity>> HELPER =
            ENTITY_TYPES.register("helper", () -> EntityType.Builder
                    .of(SulakHelperEntity::new, MobCategory.MISC)
                    .sized(0.6F, 1.95F)
                    .clientTrackingRange(32)
                    .updateInterval(2)
                    .fireImmune()
                    .build(ResourceKey.create(Registries.ENTITY_TYPE, id("helper"))));

    public Sulak(FMLJavaModLoadingContext context) {
        BusGroup modBus = context.getModBusGroup();
        ENTITY_TYPES.register(modBus);
        EntityAttributeCreationEvent.getBus(modBus).addListener(this::onAttributes);
        context.registerConfig(ModConfig.Type.COMMON, Config.SPEC);
        LOGGER.info("[Sulak] mod constructed");
    }

    private void onAttributes(EntityAttributeCreationEvent event) {
        event.put(HELPER.get(), SulakHelperEntity.createAttributes().build());
    }

    public static ResourceLocation id(String path) {
        return ResourceLocation.fromNamespaceAndPath(MODID, path);
    }
}
