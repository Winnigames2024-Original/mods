package com.winnigames2024.nareserver;

import org.bukkit.entity.Player;

import java.io.File;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;

/**
 * Файлы мода, которые хранятся на сервере: plugins/NareServer/mod/<ник>/…
 *
 * Зачем: человек заходит с другого компьютера — и настройки, пресеты и
 * расположение HUD уже на месте. Мод кладёт их сюда сам и сам же забирает.
 *
 * Правила простые и строгие: только внутри своей папки, только разумные
 * имена, ограничение на размер. Имя файла приходит от клиента, а всё, что
 * приходит снаружи, считается недоверенным, пока не проверено.
 */
public final class NareFiles {

    private static final int MAX_BYTES = 512 * 1024;

    private final NareServer plugin;
    private final File root;

    public NareFiles(NareServer plugin) {
        this.plugin = plugin;
        this.root = new File(plugin.getDataFolder(), "mod");
        //noinspection ResultOfMethodCallIgnored
        root.mkdirs();
    }

    /** Папка игрока. */
    public File folderOf(Player player) {
        File folder = new File(root, safeName(player.getName()));
        //noinspection ResultOfMethodCallIgnored
        folder.mkdirs();
        return folder;
    }

    /**
     * Файл внутри папки игрока или null, если имя не годится.
     * Проверяем по-настоящему: сравниваем итоговый путь с папкой, чтобы
     * никакие «..» не вывели наружу.
     */
    public File fileOf(Player player, String name) {
        if (name == null || name.isBlank() || name.length() > 64) return null;

        String cleaned = safeName(name);
        if (cleaned.isEmpty()) return null;

        File folder = folderOf(player);
        File target = new File(folder, cleaned);

        try {
            if (!target.getCanonicalPath().startsWith(folder.getCanonicalPath() + File.separator))
                return null;
        }
        catch (Exception e) { return null; }

        return target;
    }

    public boolean write(Player player, String name, byte[] data) {
        if (data == null || data.length > MAX_BYTES) return false;

        File target = fileOf(player, name);
        if (target == null) return false;

        try {
            Files.write(target.toPath(), data);
            return true;
        }
        catch (Exception e) {
            plugin.getLogger().warning("Файл мода не записался (" + player.getName()
                    + "/" + name + "): " + e.getMessage());
            return false;
        }
    }

    public byte[] read(Player player, String name) {
        File target = fileOf(player, name);
        if (target == null || !target.isFile()) return null;
        if (target.length() > MAX_BYTES) return null;

        try { return Files.readAllBytes(target.toPath()); }
        catch (Exception e) { return null; }
    }

    public boolean delete(Player player, String name) {
        File target = fileOf(player, name);
        if (target == null || !target.isFile()) return false;

        try { return target.delete(); }
        catch (Exception e) { return false; }
    }

    /** Что лежит у игрока — через запятую, для ответа моду. */
    public String list(Player player) {
        File[] files = folderOf(player).listFiles();
        if (files == null || files.length == 0) return "";

        var names = new StringBuilder();
        for (File f : files) {
            if (!f.isFile()) continue;
            if (names.length() > 0) names.append(',');
            names.append(f.getName());
        }

        return names.toString();
    }

    /** Оставляем только то, из чего бывают имена файлов. */
    private static String safeName(String name) {
        var kept = new StringBuilder();

        for (char c : name.toCharArray()) {
            boolean ok = Character.isLetterOrDigit(c) || c == '.' || c == '-' || c == '_';
            if (ok && !(c == '.' && kept.length() == 0)) kept.append(c);
        }

        String text = kept.toString();
        return text.contains("..") ? text.replace("..", "_") : text;
    }

    public static String utf8(byte[] data) {
        return new String(data, StandardCharsets.UTF_8);
    }
}
