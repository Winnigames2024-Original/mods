package com.winnigames2024.nareserver;

import net.kyori.adventure.text.Component;
import org.bukkit.Bukkit;
import org.bukkit.NamespacedKey;
import org.bukkit.Registry;
import org.bukkit.attribute.Attribute;
import org.bukkit.attribute.AttributeInstance;
import org.bukkit.attribute.AttributeModifier;
import org.bukkit.block.Block;
import org.bukkit.event.EventHandler;
import org.bukkit.event.Listener;
import org.bukkit.event.block.BlockBreakEvent;
import org.bukkit.event.player.PlayerQuitEvent;
import org.bukkit.entity.Player;
import org.bukkit.inventory.EquipmentSlotGroup;
import org.bukkit.plugin.java.JavaPlugin;

import java.util.HashMap;
import java.util.Map;
import java.util.UUID;

/**
 * Серверная половина NareVisuals.
 *
 * Клиентский мод сам по себе не может ни узнать, у кого ещё он стоит, ни
 * ломать блоки дальше обычного, ни показать другим, что он сломал: всё это
 * проверяет и рассылает сервер. Этот плагин и есть та недостающая половина.
 *
 * Чего он намеренно не делает: ничего не рассылает игрокам без мода и
 * не трогает тех, кто не включил приём «шуток» у себя сам.
 */
public final class NareServer extends JavaPlugin implements Listener {

    /** Один игрок с модом. */
    public record Guest(String name, boolean funTrolls) { }

    private final Map<UUID, Guest> guests = new HashMap<>();

    private NareChannel channel;

    private double reachBlocks;
    private double reachEntities;
    private boolean marks;
    private boolean trolls;

    @Override
    public void onEnable() {
        saveDefaultConfig();
        readConfig();

        channel = new NareChannel(this);
        channel.register();

        Bukkit.getPluginManager().registerEvents(this, this);

        getLogger().info("NareVisuals: сервер готов. Дальность "
                + (reachBlocks > 0 ? reachBlocks + "/" + reachEntities : "не меняем")
                + ", метки " + (marks ? "включены" : "выключены")
                + ", шутки " + (trolls ? "разрешены" : "запрещены"));
    }

    @Override
    public void onDisable() {
        for (UUID id : guests.keySet()) {
            Player player = Bukkit.getPlayer(id);
            if (player != null) reach(player, 0, 0);
        }
        guests.clear();
    }

    private void readConfig() {
        reachBlocks = getConfig().getDouble("reach.blocks", 0);
        reachEntities = getConfig().getDouble("reach.entities", 0);
        marks = getConfig().getBoolean("marks", true);
        trolls = getConfig().getBoolean("trolls", true);
    }

    public boolean trollsAllowed() {
        return trolls;
    }

    public Map<UUID, Guest> guests() {
        return guests;
    }

    // ------------------------------------------------------------ рукопожатие

    /** Клиент сказал, что мод у него есть. */
    public void onHello(Player player, String version, boolean funTrolls) {
        guests.put(player.getUniqueId(), new Guest(player.getName(), funTrolls));

        double blocks = 0, entities = 0;
        if (player.hasPermission("narevisuals.reach")) {
            blocks = reachBlocks;
            entities = reachEntities;
        }
        reach(player, blocks, entities);

        channel.sendWelcome(player, blocks, entities, trolls);
        broadcastPlayers();

        getLogger().info("NareVisuals " + version + " у " + player.getName()
                + (funTrolls ? " (шутки разрешил)" : ""));
    }

    @EventHandler
    public void onQuit(PlayerQuitEvent event) {
        if (guests.remove(event.getPlayer().getUniqueId()) == null) return;
        reach(event.getPlayer(), 0, 0);
        broadcastPlayers();
    }

    /** Рассылает всем с модом список тех, у кого мод тоже есть. */
    public void broadcastPlayers() {
        for (UUID id : guests.keySet()) {
            Player player = Bukkit.getPlayer(id);
            if (player != null) channel.sendPlayers(player, guests.values());
        }
    }

    // ---------------------------------------------------------------- дальность

    private static final NamespacedKey REACH_BLOCKS =
            NamespacedKey.fromString("narevisuals:reach_blocks");
    private static final NamespacedKey REACH_ENTITIES =
            NamespacedKey.fromString("narevisuals:reach_entities");

    /**
     * Поднимает игроку дальность действий. Ноль — снять надбавку.
     * Атрибуты ищем по ключу, а не по имени константы: между версиями
     * игры константы переименовывали, ключи же остались теми же.
     */
    private void reach(Player player, double blocks, double entities) {
        apply(player, "block_interaction_range", REACH_BLOCKS, blocks);
        apply(player, "entity_interaction_range", REACH_ENTITIES, entities);
    }

    private void apply(Player player, String key, NamespacedKey marker, double want) {
        Attribute attribute = Registry.ATTRIBUTE.get(NamespacedKey.minecraft(key));
        if (attribute == null) return;

        AttributeInstance instance = player.getAttribute(attribute);
        if (instance == null) return;

        for (AttributeModifier old : instance.getModifiers())
            if (marker.equals(old.getKey())) instance.removeModifier(old);

        if (want <= 0) return;

        double bonus = want - instance.getBaseValue();
        if (bonus <= 0) return;

        instance.addModifier(new AttributeModifier(
                marker, bonus, AttributeModifier.Operation.ADD_NUMBER, EquipmentSlotGroup.ANY));
    }

    // ------------------------------------------------------------- метки блоков

    /**
     * Кто-то сломал блок — рассказываем об этом всем, у кого есть мод.
     * Сам блок ломает сервер обычным образом, мы только сообщаем координаты,
     * чтобы клиент нарисовал метку и можно было туда дойти.
     */
    @EventHandler(ignoreCancelled = true)
    public void onBreak(BlockBreakEvent event) {
        if (!marks || guests.isEmpty()) return;

        Block block = event.getBlock();
        String who = event.getPlayer().getName();

        for (UUID id : guests.keySet()) {
            Player player = Bukkit.getPlayer(id);
            if (player == null) continue;
            if (!player.getWorld().equals(block.getWorld())) continue;

            channel.sendMark(player, who,
                    block.getX(), block.getY(), block.getZ());
        }
    }

    // ---------------------------------------------------------------- шутки

    /**
     * Пересылает «шутку» — но только тому, кто сам её разрешил аргументом
     * запуска. Без этого согласия пакет просто выбрасывается: лезть к людям,
     * которые об этом не просили, плагин не будет.
     */
    public void onTroll(Player from, String targetName, byte kind) {
        if (!trolls) return;

        Player target = Bukkit.getPlayerExact(targetName);
        if (target == null || target.equals(from)) return;

        Guest guest = guests.get(target.getUniqueId());
        if (guest == null || !guest.funTrolls()) {
            ((net.kyori.adventure.audience.Audience) from).sendMessage(
                    Component.text("Этот игрок шутки не включал — ничего не отправил."));
            return;
        }

        channel.sendTroll(target, from.getName(), kind);
    }

    public NareChannel channel() {
        return channel;
    }
}
