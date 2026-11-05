package com.winnigames2024.forge_narevisuals.client;

import com.winnigames2024.forge_narevisuals.Config;
import com.winnigames2024.forge_narevisuals.Forge_narevisuals;
import com.winnigames2024.forge_narevisuals.Launch;
import net.minecraft.SharedConstants;
import net.minecraft.network.chat.Component;
import net.minecraft.server.packs.PackLocationInfo;
import net.minecraft.server.packs.PackSelectionConfig;
import net.minecraft.server.packs.PackType;
import net.minecraft.server.packs.PathPackResources;
import net.minecraft.server.packs.repository.Pack;
import net.minecraft.server.packs.repository.PackSource;
import net.minecraftforge.api.distmarker.Dist;
import net.minecraftforge.event.AddPackFindersEvent;
import net.minecraftforge.eventbus.api.SubscribeEvent;
import net.minecraftforge.fml.ModList;
import net.minecraftforge.fml.common.Mod;
import net.minecraftforge.forgespi.language.IModFileInfo;

import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Optional;

/**
 * Встроенный ресурспак NareVisuals: кнопки, поля, ползунки и фон главного меню.
 * Лежит прямо в jar мода и подключается сам — отдельно скачивать и включать
 * ничего не нужно. Без --visuals пак не добавляется вовсе, игра выглядит ванильно.
 *
 * У Forge 1.21.4 в AddPackFindersEvent только addRepositorySource, поэтому
 * собираем Pack руками — тем же способом, каким это делает сам Forge.
 */
@Mod.EventBusSubscriber(modid = Forge_narevisuals.MODID,
                        bus = Mod.EventBusSubscriber.Bus.MOD,
                        value = Dist.CLIENT)
public final class NarePack {

    /** Путь внутри jar. */
    public static final String ROOT = "resourcepacks/narevisuals";

    public static final String ID = "narevisuals";

    private NarePack() { }

    @SubscribeEvent
    public static void addPacks(AddPackFindersEvent event) {
        if (event.getPackType() != PackType.CLIENT_RESOURCES) return;

        if (!Launch.visuals()) {
            Forge_narevisuals.LOGGER.info("{}: ресурспак не подключаем — нет {}",
                    Forge_narevisuals.NAME, Launch.ARG);
            return;
        }

        boolean wanted = true;
        try {
            wanted = Config.get().resourcePack;
        } catch (Throwable ignored) {
            // конфиг ещё может быть не прочитан — тогда считаем, что пак нужен
        }

        if (!wanted) {
            Forge_narevisuals.LOGGER.info("{}: ресурспак выключен в настройках", Forge_narevisuals.NAME);
            return;
        }

        Path root = root();
        if (root == null || !Files.exists(root)) {
            Forge_narevisuals.LOGGER.warn("{}: ресурспак не найден в jar ({})",
                    Forge_narevisuals.NAME, ROOT);
            return;
        }

        event.addRepositorySource(consumer -> {
            var supplier = new PathPackResources.PathResourcesSupplier(root);

            var info = new PackLocationInfo(
                    ID,
                    Component.literal(Forge_narevisuals.NAME),
                    PackSource.BUILT_IN,
                    Optional.empty());

            // true — пак всегда включён, TOP — лежит поверх остальных
            var selection = new PackSelectionConfig(true, Pack.Position.TOP, false);

            Pack pack = Pack.readMetaAndCreate(info, supplier, PackType.CLIENT_RESOURCES, selection);

            if (pack == null) {
                Forge_narevisuals.LOGGER.warn("{}: не удалось прочитать pack.mcmeta ресурспака",
                        Forge_narevisuals.NAME);
                return;
            }

            consumer.accept(pack);
            Forge_narevisuals.LOGGER.info("{}: ресурспак подключён (формат игры {})",
                    Forge_narevisuals.NAME,
                    SharedConstants.getCurrentVersion().getPackVersion(PackType.CLIENT_RESOURCES));
        });

        addOwnTextures(event);
    }

    /** Папка со своими текстурами — обычный ресурспак рядом с конфигом. */
    private static void addOwnTextures(AddPackFindersEvent event) {
        NareTextures.ensure();

        Path folder = NareTextures.root();
        if (!Files.exists(folder.resolve("pack.mcmeta"))) return;

        event.addRepositorySource(consumer -> {
            var supplier = new PathPackResources.PathResourcesSupplier(folder);

            var info = new PackLocationInfo(
                    ID + "_textures",
                    Component.literal(Forge_narevisuals.NAME + " — свои текстуры"),
                    PackSource.BUILT_IN,
                    Optional.empty());

            var selection = new PackSelectionConfig(true, Pack.Position.TOP, false);
            Pack pack = Pack.readMetaAndCreate(info, supplier, PackType.CLIENT_RESOURCES, selection);

            if (pack != null) {
                consumer.accept(pack);
                Forge_narevisuals.LOGGER.info("{}: свои текстуры подключены ({})",
                        Forge_narevisuals.NAME, folder);
            }
        });
    }

    private static Path root() {
        try {
            IModFileInfo info = ModList.get().getModFileById(Forge_narevisuals.MODID);
            if (info == null) return null;
            return info.getFile().findResource(ROOT);
        } catch (Throwable ex) {
            Forge_narevisuals.LOGGER.warn("{}: не нашли папку ресурспака: {}",
                    Forge_narevisuals.NAME, ex.toString());
            return null;
        }
    }
}
