package com.winnigames2024.nareserver;

import org.bukkit.Bukkit;
import org.bukkit.Location;
import org.bukkit.Material;
import org.bukkit.NamespacedKey;
import org.bukkit.block.Block;
import org.bukkit.block.data.Directional;
import org.bukkit.configuration.file.YamlConfiguration;
import org.bukkit.entity.Player;
import org.bukkit.inventory.ItemStack;
import org.bukkit.inventory.ShapedRecipe;
import org.bukkit.inventory.meta.ItemMeta;
import org.bukkit.persistence.PersistentDataType;
import org.bukkit.util.Vector;

import java.io.File;
import java.util.HashMap;
import java.util.Map;

/**
 * Плитки для паркура: чекпоинт, сброс чекпоинта и прыжковая плита.
 *
 * Настоящих новых блоков тут нет и быть не может: мод стоит у клиента, а
 * сервер на Paper, и про выдуманный блок он ничего не знает. Поэтому
 * плитка — это обычный блок игры, помеченный у нас в списке. Выглядит она
 * как надо за счёт текстур из мода, а работает за счёт этого файла.
 *
 * Такой блок переживает перезагрузку, ломается как обычный и не ломает
 * мир тем, у кого мода нет: они увидят глазурованную керамику.
 */
public final class NareParkour {

    /** Из чего делаем плитки. Керамика — чтобы не портить обычные стройблоки. */
    public static final Material CHECKPOINT = Material.LIME_GLAZED_TERRACOTTA;
    public static final Material UNCHECKPOINT = Material.RED_GLAZED_TERRACOTTA;
    public static final Material JUMPPAD = Material.MAGENTA_GLAZED_TERRACOTTA;

    private static final int DEFAULT_POWER = 12;

    private final NareServer plugin;
    private final File file;

    /** Где какая плитка: "мир;x;y;z" -> описание. */
    private final Map<String, Tile> tiles = new HashMap<>();

    /** Последний чекпоинт каждого игрока. */
    private final Map<java.util.UUID, Location> saved = new HashMap<>();

    private NamespacedKey markKey;

    public record Tile(String kind, int power) { }

    public NareParkour(NareServer plugin) {
        this.plugin = plugin;
        this.file = new File(plugin.getDataFolder(), "parkour.yml");
        this.markKey = new NamespacedKey(plugin, "parkour");
    }

    public NamespacedKey markKey() { return markKey; }

    // ----------------------------------------------------------- хранение

    public void load() {
        tiles.clear();
        if (!file.exists()) return;

        var yaml = YamlConfiguration.loadConfiguration(file);
        for (String key : yaml.getKeys(false)) {
            String kind = yaml.getString(key + ".kind", "");
            int power = yaml.getInt(key + ".power", DEFAULT_POWER);
            if (kind.isEmpty()) continue;

            tiles.put(key, new Tile(kind, power));
        }

        plugin.getLogger().info("NareVisuals: плиток паркура загружено " + tiles.size());
    }

    public void save() {
        var yaml = new YamlConfiguration();

        for (var entry : tiles.entrySet()) {
            yaml.set(entry.getKey() + ".kind", entry.getValue().kind());
            yaml.set(entry.getKey() + ".power", entry.getValue().power());
        }

        try { yaml.save(file); }
        catch (Exception e) { plugin.getLogger().warning("parkour.yml не записался: " + e.getMessage()); }
    }

    private static String at(Location where) {
        return where.getWorld().getName() + ";" + where.getBlockX()
                + ";" + where.getBlockY() + ";" + where.getBlockZ();
    }

    public Tile tileAt(Location where) {
        return where == null || where.getWorld() == null ? null : tiles.get(at(where));
    }

    public void put(Location where, String kind, int power) {
        tiles.put(at(where), new Tile(kind, clamp(power)));
        save();
    }

    public void remove(Location where) {
        if (tiles.remove(at(where)) != null) save();
    }

    public int count() { return tiles.size(); }

    public static int clamp(int power) {
        return Math.max(1, Math.min(60, power));
    }

    // -------------------------------------------------------------- вещи

    /** Помеченная вещь: при установке блок сам станет плиткой. */
    public ItemStack item(String kind, int power) {
        Material material = switch (kind) {
            case "checkpoint" -> CHECKPOINT;
            case "uncheckpoint" -> UNCHECKPOINT;
            default -> JUMPPAD;
        };

        ItemStack stack = new ItemStack(material);
        ItemMeta meta = stack.getItemMeta();

        meta.displayName(net.kyori.adventure.text.Component.text(switch (kind) {
            case "checkpoint" -> "Чекпоинт";
            case "uncheckpoint" -> "Сброс чекпоинта";
            default -> "Прыжковая плита · сила " + clamp(power);
        }));

        meta.getPersistentDataContainer().set(markKey, PersistentDataType.STRING,
                kind + ":" + clamp(power));

        stack.setItemMeta(meta);
        return stack;
    }

    /** Что это за вещь, или null. */
    public Tile kindOf(ItemStack stack) {
        if (stack == null || !stack.hasItemMeta()) return null;

        String value = stack.getItemMeta().getPersistentDataContainer()
                .get(markKey, PersistentDataType.STRING);
        if (value == null) return null;

        String[] parts = value.split(":");
        int power = DEFAULT_POWER;
        if (parts.length > 1) try { power = Integer.parseInt(parts[1]); } catch (Exception ignored) { }

        return new Tile(parts[0], clamp(power));
    }

    // ------------------------------------------------------------ рецепты

    /**
     * Рецепты, чтобы плитки можно было делать руками, а не только командой.
     * Обычную керамику они не трогают: на выходе помеченная вещь.
     */
    public void recipes() {
        add("checkpoint", CHECKPOINT, Material.ENDER_PEARL, "Чекпоинт");
        add("uncheckpoint", UNCHECKPOINT, Material.REDSTONE, "Сброс чекпоинта");
        add("jumppad", JUMPPAD, Material.SLIME_BALL, "Прыжковая плита");
    }

    private void add(String kind, Material base, Material extra, String title) {
        var key = new NamespacedKey(plugin, "nare_" + kind);

        try { Bukkit.removeRecipe(key); } catch (Exception ignored) { }

        var recipe = new ShapedRecipe(key, item(kind, DEFAULT_POWER));
        recipe.shape(" e ", "ebe", " e ");
        recipe.setIngredient('b', base);
        recipe.setIngredient('e', extra);

        try { Bukkit.addRecipe(recipe); }
        catch (Exception e) {
            plugin.getLogger().warning("Рецепт " + title + " не добавился: " + e.getMessage());
        }
    }

    // ------------------------------------------------------------ работа

    /** Поставили помеченный блок — запоминаем и разворачиваем, чтобы текст читался. */
    public void placed(Block block, Tile tile) {
        put(block.getLocation(), tile.kind(), tile.power());

        // у глазурованной керамики картинка поворачивается за установкой,
        // а надпись должна читаться всегда одинаково
        try {
            var data = block.getBlockData();
            if (data instanceof Directional directional) {
                directional.setFacing(org.bukkit.block.BlockFace.NORTH);
                block.setBlockData(directional, false);
            }
        }
        catch (Exception ignored) { }
    }

    /**
     * Игрок наступил на плитку. Возвращает текст для подсказки или null.
     */
    public String stepped(Player player, Block block) {
        Tile tile = tileAt(block.getLocation());
        if (tile == null) return null;

        switch (tile.kind()) {
            case "checkpoint" -> {
                Location was = saved.get(player.getUniqueId());
                Location now = block.getLocation().add(0.5, 1.0, 0.5);
                now.setYaw(player.getLocation().getYaw());
                now.setPitch(player.getLocation().getPitch());

                if (was != null && was.getBlockX() == now.getBlockX()
                        && was.getBlockY() == now.getBlockY()
                        && was.getBlockZ() == now.getBlockZ()) return null;

                saved.put(player.getUniqueId(), now);
                return "Чекпоинт сохранён";
            }

            case "uncheckpoint" -> {
                if (saved.remove(player.getUniqueId()) == null) return null;
                return "Чекпоинт сброшен";
            }

            default -> {
                var look = player.getLocation().getDirection().normalize();
                double power = tile.power() / 10.0;

                player.setVelocity(new Vector(look.getX() * power * 0.6,
                        power, look.getZ() * power * 0.6));

                player.getWorld().playSound(block.getLocation(),
                        org.bukkit.Sound.ENTITY_SLIME_JUMP, 0.7f, 1.4f);
                return null;
            }
        }
    }

    /** Куда вернуть игрока: его чекпоинт или null. */
    public Location checkpointOf(Player player) {
        return saved.get(player.getUniqueId());
    }

    public void forget(java.util.UUID id) {
        saved.remove(id);
    }
}
