package com.winnigames2024.forge_narevisuals.client;

import com.winnigames2024.forge_narevisuals.Config;
import com.winnigames2024.forge_narevisuals.Forge_narevisuals;
import net.minecraft.client.Minecraft;
import net.minecraft.core.registries.BuiltInRegistries;
import net.minecraft.resources.ResourceLocation;
import net.minecraft.world.item.Item;
import net.minecraft.world.level.block.Block;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardCopyOption;
import java.util.ArrayList;
import java.util.List;

/**
 * Свои текстуры блоков и предметов прямо из игры.
 *
 * Работает честным способом: рядом с конфигом лежит обычный ресурспак
 * (config/narevisuals/textures), игра подключает его поверх остальных, а мы
 * кладём туда картинку под нужным именем и перезагружаем ресурсы. Ничего
 * внутри jar не правится, всё можно унести на другой компьютер папкой.
 *
 * Имя текстуры у большинства ванильных блоков совпадает с их идентификатором
 * (stone → block/stone.png). Для блоков, у которых стороны разные, игра ждёт
 * несколько файлов — такие придётся дописать руками в ту же папку.
 */
public final class NareTextures {

    public static final String BLOCK = "block";
    public static final String ITEM = "item";

    private NareTextures() { }

    public static Path root() {
        return Config.folder().resolve("textures");
    }

    private static Path assets() {
        return root().resolve("assets");
    }

    /** Папка, куда ложится картинка для такого-то предмета или блока. */
    private static Path target(String kind, ResourceLocation what) {
        return assets().resolve(what.getNamespace())
                       .resolve("textures").resolve(kind)
                       .resolve(what.getPath() + ".png");
    }

    // ------------------------------------------------------------- сам пак

    /** Создаёт пак, если его ещё нет. Без pack.mcmeta игра папку не возьмёт. */
    public static void ensure() {
        try {
            Path meta = root().resolve("pack.mcmeta");
            Files.createDirectories(assets());
            if (Files.exists(meta)) return;

            String text = "{\n"
                    + "  \"pack\": {\n"
                    + "    \"pack_format\": 46,\n"
                    + "    \"description\": \"Свои текстуры NareVisuals\"\n"
                    + "  }\n"
                    + "}\n";
            Files.write(meta, text.getBytes(StandardCharsets.UTF_8));

            Forge_narevisuals.LOGGER.info("{}: папка своих текстур готова — {}",
                    Forge_narevisuals.NAME, root());
        } catch (IOException e) {
            Forge_narevisuals.LOGGER.warn("{}: папку текстур не создать: {}",
                    Forge_narevisuals.NAME, e.toString());
        }
    }

    // ------------------------------------------------------------ действия

    /** Кладёт картинку вместо текстуры и сразу перезагружает ресурсы. */
    public static boolean apply(String kind, ResourceLocation what, String image) {
        Path source = Config.images().resolve(image);
        if (!Files.exists(source)) return false;

        try {
            ensure();
            Path to = target(kind, what);
            Files.createDirectories(to.getParent());
            Files.copy(source, to, StandardCopyOption.REPLACE_EXISTING);
        } catch (IOException e) {
            Forge_narevisuals.LOGGER.warn("{}: текстуру не заменить: {}",
                    Forge_narevisuals.NAME, e.toString());
            return false;
        }

        reload();
        return true;
    }

    /** Возвращает ванильную текстуру на место. */
    public static boolean clear(String kind, ResourceLocation what) {
        try {
            Path to = target(kind, what);
            if (!Files.deleteIfExists(to)) return false;
        } catch (IOException e) {
            return false;
        }

        reload();
        return true;
    }

    public static boolean replaced(String kind, ResourceLocation what) {
        return Files.exists(target(kind, what));
    }

    /** Сколько текстур сейчас подменено. */
    public static int count() {
        if (!Files.exists(assets())) return 0;
        try (var walk = Files.walk(assets())) {
            return (int) walk.filter(p -> p.toString().endsWith(".png")).count();
        } catch (IOException e) {
            return 0;
        }
    }

    public static void reload() {
        Minecraft mc = Minecraft.getInstance();
        if (mc != null) mc.reloadResourcePacks();
    }

    // ------------------------------------------------------------- списки

    /** Блоки и предметы с поиском по названию — для окна выбора. */
    public static List<ResourceLocation> search(String kind, String query) {
        String needle = query == null ? "" : query.trim().toLowerCase();
        List<ResourceLocation> out = new ArrayList<>();

        if (BLOCK.equals(kind)) {
            for (Block block : BuiltInRegistries.BLOCK) {
                ResourceLocation key = BuiltInRegistries.BLOCK.getKey(block);
                if (matches(key, needle)) out.add(key);
                if (out.size() >= 600) break;
            }
        } else {
            for (Item item : BuiltInRegistries.ITEM) {
                ResourceLocation key = BuiltInRegistries.ITEM.getKey(item);
                if (matches(key, needle)) out.add(key);
                if (out.size() >= 600) break;
            }
        }

        return out;
    }

    private static boolean matches(ResourceLocation key, String needle) {
        if (key == null) return false;
        return needle.isEmpty() || key.getPath().contains(needle);
    }

    /** Какие картинки лежат в папке images — из них и выбираем. */
    public static List<String> pictures() {
        List<String> out = new ArrayList<>();
        try {
            Path dir = Config.images();
            if (!Files.exists(dir)) return out;

            try (var list = Files.list(dir)) {
                list.filter(p -> p.getFileName().toString().toLowerCase().endsWith(".png"))
                    .forEach(p -> out.add(p.getFileName().toString()));
            }
        } catch (IOException ignored) {
        }

        out.sort(String::compareToIgnoreCase);
        return out;
    }
}
