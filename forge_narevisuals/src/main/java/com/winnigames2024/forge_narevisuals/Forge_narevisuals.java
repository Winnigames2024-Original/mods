package com.winnigames2024.forge_narevisuals;

import com.mojang.logging.LogUtils;
import net.minecraftforge.api.distmarker.Dist;
import net.minecraftforge.eventbus.api.SubscribeEvent;
import net.minecraftforge.fml.common.Mod;
import net.minecraftforge.fml.event.lifecycle.FMLClientSetupEvent;
import net.minecraftforge.fml.javafmlmod.FMLJavaModLoadingContext;
import org.slf4j.Logger;

/**
 * NareVisuals — клиентский мод визуальных улучшений.
 * Ничего в механику игры не добавляет: только HUD и эффекты на экране.
 */
@Mod(Forge_narevisuals.MODID)
public class Forge_narevisuals {

    public static final String MODID = "forge_narevisuals";
    public static final String NAME = "NareVisuals";
    public static final String VERSION = "1.0";
    public static final Logger LOGGER = LogUtils.getLogger();

    public Forge_narevisuals(FMLJavaModLoadingContext context) {
        // Мод целиком клиентский: на выделенном сервере он просто ничего не делает.
        LOGGER.info("{} загружается", NAME);
    }

    @Mod.EventBusSubscriber(modid = MODID, bus = Mod.EventBusSubscriber.Bus.MOD, value = Dist.CLIENT)
    public static class ClientModEvents {

        @SubscribeEvent
        public static void onClientSetup(FMLClientSetupEvent event) {
            Config.load();
            LOGGER.info("{}: настройки загружены из {}", NAME, Config.file());

            // канал к серверному плагину; на сервере без него мод работает как раньше
            com.winnigames2024.forge_narevisuals.client.NareNet.register();
        }
    }
}
