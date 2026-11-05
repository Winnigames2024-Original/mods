package com.winnigames2024.forge_narevisuals.client;

import com.winnigames2024.forge_narevisuals.Config;
import com.winnigames2024.forge_narevisuals.Forge_narevisuals;

import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.nio.charset.StandardCharsets;
import java.nio.file.DirectoryStream;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardCopyOption;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.zip.ZipEntry;
import java.util.zip.ZipInputStream;
import java.util.zip.ZipOutputStream;

/**
 * Пресеты настроек в архиве .nvc.
 *
 * Внутри обычный zip: narevisuals.json со всеми настройками и папка images
 * с картинками, на которые настройки ссылаются (прицел, небо). Так пресет
 * переносится одним файлом и не теряет картинки.
 */
public final class NarePresets {

    public static final String EXT = ".nvc";
    private static final String CONFIG_ENTRY = "narevisuals.json";
    private static final String IMAGE_DIR = "images/";

    private NarePresets() { }

    public static Path folder() {
        return Config.folder().resolve("presets");
    }

    private static void prepare() throws IOException {
        Files.createDirectories(folder());
        Files.createDirectories(Config.images());
    }

    /** Имена пресетов без расширения, по алфавиту. */
    public static List<String> list() {
        List<String> names = new ArrayList<>();
        try {
            prepare();
            try (DirectoryStream<Path> files = Files.newDirectoryStream(folder(), "*" + EXT)) {
                for (Path file : files) {
                    String name = file.getFileName().toString();
                    names.add(name.substring(0, name.length() - EXT.length()));
                }
            }
        } catch (IOException ignored) {
        }
        Collections.sort(names, String.CASE_INSENSITIVE_ORDER);
        return names;
    }

    public static Path file(String name) {
        return folder().resolve(safe(name) + EXT);
    }

    public static boolean exists(String name) {
        return Files.isRegularFile(file(name));
    }

    // ------------------------------------------------------------ сохранение

    /** Кладёт в архив текущие настройки и все картинки, на которые они ссылаются. */
    public static void save(String name) throws IOException {
        prepare();

        Config cfg = Config.get();
        List<String> wanted = new ArrayList<>();
        if (cfg.crosshairImage != null && !cfg.crosshairImage.isEmpty()) wanted.add(cfg.crosshairImage);
        if (cfg.skyImage != null && !cfg.skyImage.isEmpty()) wanted.add(cfg.skyImage);

        Path target = file(name);
        try (OutputStream out = Files.newOutputStream(target);
             ZipOutputStream zip = new ZipOutputStream(out, StandardCharsets.UTF_8)) {

            zip.putNextEntry(new ZipEntry(CONFIG_ENTRY));
            zip.write(Config.toJson().getBytes(StandardCharsets.UTF_8));
            zip.closeEntry();

            for (String image : wanted) {
                Path source = Config.images().resolve(image);
                if (!Files.isRegularFile(source)) continue;
                zip.putNextEntry(new ZipEntry(IMAGE_DIR + image));
                Files.copy(source, zip);
                zip.closeEntry();
            }
        }

        Forge_narevisuals.LOGGER.info("{}: пресет сохранён — {}", Forge_narevisuals.NAME, target);
    }

    // ------------------------------------------------------------ загрузка

    /** Разбирает архив: картинки — в images, настройки — в текущие. */
    public static void load(String name) throws IOException {
        prepare();

        Path source = file(name);
        if (!Files.isRegularFile(source)) throw new IOException("Пресет не найден: " + name);

        String json = null;

        try (InputStream in = Files.newInputStream(source);
             ZipInputStream zip = new ZipInputStream(in, StandardCharsets.UTF_8)) {

            ZipEntry entry;
            while ((entry = zip.getNextEntry()) != null) {
                if (entry.isDirectory()) continue;
                String path = entry.getName();

                if (CONFIG_ENTRY.equals(path)) {
                    json = new String(zip.readAllBytes(), StandardCharsets.UTF_8);
                } else if (path.startsWith(IMAGE_DIR)) {
                    String file = safe(path.substring(IMAGE_DIR.length()));
                    if (file.isEmpty()) continue;
                    Files.copy(zip, Config.images().resolve(file),
                            StandardCopyOption.REPLACE_EXISTING);
                }
            }
        }

        if (json == null) throw new IOException("В архиве нет настроек");

        Config loaded = Config.fromJson(json);
        if (loaded == null) throw new IOException("Настройки в архиве не читаются");

        Config.replace(loaded);
        Config.save();
        NareCrosshair.reload();

        Forge_narevisuals.LOGGER.info("{}: пресет загружен — {}", Forge_narevisuals.NAME, name);
    }

    public static void delete(String name) throws IOException {
        Files.deleteIfExists(file(name));
    }

    // ------------------------------------------------------------ картинки

    /** Картинки, доступные для прицела и неба. */
    public static List<String> images() {
        List<String> names = new ArrayList<>();
        try {
            prepare();
            try (DirectoryStream<Path> files = Files.newDirectoryStream(Config.images())) {
                for (Path file : files) {
                    if (!Files.isRegularFile(file)) continue;
                    String n = file.getFileName().toString();
                    String lower = n.toLowerCase();
                    if (lower.endsWith(".png") || lower.endsWith(".jpg") || lower.endsWith(".jpeg"))
                        names.add(n);
                }
            }
        } catch (IOException ignored) {
        }
        Collections.sort(names, String.CASE_INSENSITIVE_ORDER);
        return names;
    }

    /** Убираем из имени всё, чем можно уйти из своей папки. */
    public static String safe(String name) {
        if (name == null) return "";
        String out = name.replace('\\', '/');
        int slash = out.lastIndexOf('/');
        if (slash >= 0) out = out.substring(slash + 1);
        out = out.replaceAll("[^A-Za-zА-Яа-яЁё0-9 ._()\\-]", "").trim();
        return out;
    }
}
