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
import java.util.concurrent.ConcurrentHashMap;

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

    private boolean voice;
    private int voiceDistance;
    private int voicePort;

    private boolean ruleFreecam = true;
    private int ruleFreecamRange;
    private boolean ruleEspPlayers = true;
    private boolean ruleEspMobs = true;
    private boolean ruleBright = true;
    private boolean ruleZoom = true;
    private String voiceBind = "";

    /** Отдельный порт для голоса. null — звук идёт по игровому соединению. */
    private NareVoicePort voicePipe;

    /**
     * Где кто стоит — снимок, обновляемый каждый тик.
     *
     * Голос приходит сетевым потоком, а спрашивать у Bukkit координаты не из
     * главного потока нельзя. Поэтому читаем их заранее и раздаём по снимку:
     * опоздание на один тик для звука незаметно.
     */
    private final Map<UUID, double[]> where = new ConcurrentHashMap<>();
    private final Map<UUID, String> worlds = new ConcurrentHashMap<>();

    @Override
    public void onEnable() {
        saveDefaultConfig();
        mergeConfig();
        readConfig();

        channel = new NareChannel(this);
        channel.register();

        if (voice && voicePort > 0) {
            var pipe = new NareVoicePort(this, voicePort);
            if (pipe.open(voiceBind)) voicePipe = pipe;
        }

        Bukkit.getPluginManager().registerEvents(this, this);

        // снимок координат для голоса
        Bukkit.getScheduler().runTaskTimer(this, () -> {
            where.clear();
            worlds.clear();

            for (UUID id : guests.keySet()) {
                Player player = Bukkit.getPlayer(id);
                if (player == null) continue;

                var at = player.getEyeLocation();
                where.put(id, new double[] { at.getX(), at.getY(), at.getZ() });
                worlds.put(id, at.getWorld().getName());
            }
        }, 1L, 1L);

        getLogger().info("NareVisuals: сервер готов. Дальность "
                + (reachBlocks > 0 ? reachBlocks + "/" + reachEntities : "не меняем")
                + ", метки " + (marks ? "включены" : "выключены")
                + ", шутки " + (trolls ? "разрешены" : "запрещены")
                + ", голос " + (voice
                        ? "включён, слышно " + (voiceDistance <= 0 ? "везде" : voiceDistance + " бл.")
                          + (voicePipe != null ? ", порт " + voicePipe.port() + "/udp"
                                               : ", по игровому соединению")
                        : "выключен"));
    }

    public boolean ruleFreecam() { return ruleFreecam; }
    public int ruleFreecamRange() { return ruleFreecamRange; }
    public boolean ruleEspPlayers() { return ruleEspPlayers; }
    public boolean ruleEspMobs() { return ruleEspMobs; }
    public boolean ruleBright() { return ruleBright; }
    public boolean ruleZoom() { return ruleZoom; }

    /** Порт голоса, 0 — отдельного порта нет. */
    public int voicePort() {
        return voicePipe == null ? 0 : voicePipe.port();
    }

    /** Число, по которому игрок представляется на голосовом порту. */
    public long voiceToken(java.util.UUID id) {
        return voicePipe == null ? 0L : voicePipe.tokenFor(id);
    }

    @Override
    public void onDisable() {
        if (voicePipe != null) { voicePipe.close(); voicePipe = null; }

        for (UUID id : guests.keySet()) {
            Player player = Bukkit.getPlayer(id);
            if (player != null) reach(player, 0, 0);
        }
        guests.clear();
    }

    /**
     * Дописывает в уже существующий config.yml то, что появилось в новых
     * версиях плагина, и убирает то, чего больше нет. Без этого сервер,
     * обновлённый с прошлой версии, живёт со старым файлом: новых настроек в
     * нём просто нет, и человек их не видит.
     */
    private void mergeConfig() {
        boolean changed = false;

        // раздела area больше нет: ломание области из плагина убрано
        if (getConfig().contains("area")) {
            getConfig().set("area", null);
            changed = true;
            getLogger().info("NareVisuals: убрал из config.yml раздел area — такой возможности больше нет");
        }

        // что из новых настроек в файле ещё не записано — смотрим до слияния,
        // потому что после copyDefaults всё выглядит записанным
        for (String key : new String[] { "voice.enabled", "voice.distance",
                                         "voice.port", "voice.bind",
                                         "limits.freecam", "limits.freecam-distance",
                                         "limits.esp-players", "limits.esp-mobs",
                                         "limits.bright", "limits.zoom" }) {
            if (getConfig().isSet(key)) continue;
            changed = true;
            getLogger().info("NareVisuals: дописал в config.yml настройку " + key);
        }

        // добавляем всё, что есть в зашитом config.yml, но нет в файле на диске
        getConfig().options().copyDefaults(true);

        if (changed) saveConfig();
    }

    private void readConfig() {
        reachBlocks = getConfig().getDouble("reach.blocks", 0);
        reachEntities = getConfig().getDouble("reach.entities", 0);
        marks = getConfig().getBoolean("marks", true);
        trolls = getConfig().getBoolean("trolls", true);
        voice = getConfig().getBoolean("voice.enabled", true);
        voiceDistance = getConfig().getInt("voice.distance", 48);
        voicePort = getConfig().getInt("voice.port", 0);

        ruleFreecam = getConfig().getBoolean("limits.freecam", true);
        ruleFreecamRange = Math.max(0, getConfig().getInt("limits.freecam-distance", 0));
        ruleEspPlayers = getConfig().getBoolean("limits.esp-players", true);
        ruleEspMobs = getConfig().getBoolean("limits.esp-mobs", true);
        ruleBright = getConfig().getBoolean("limits.bright", true);
        ruleZoom = getConfig().getBoolean("limits.zoom", true);
        voiceBind = getConfig().getString("voice.bind", "");
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
        channel.sendRules(player);
        broadcastPlayers();

        getLogger().info("NareVisuals " + version + " у " + player.getName()
                + (funTrolls ? " (шутки разрешил)" : ""));
    }

    /**
     * Кадр голоса от игрока с модом — раздаём тем, кто рядом и тоже с модом.
     *
     * Зовётся из сетевого потока, поэтому работаем только со снимком координат
     * и ничего в состоянии плагина не меняем.
     */
    /** Дальность голоса, как её задали в config.yml. 0 и меньше — слышно везде. */
    public int voiceDistance() {
        return voiceDistance;
    }

    public void onVoice(Player from, byte[] data) {
        if (!voice) return;
        if (!guests.containsKey(from.getUniqueId())) return;
        if (!from.hasPermission("narevisuals.voice")) return;

        double[] at = where.get(from.getUniqueId());
        String world = worlds.get(from.getUniqueId());
        if (at == null || world == null) return;

        String who = from.getName();

        // 0 и меньше — слышно всем и в любом мире, расстояние не считаем
        boolean everywhere = voiceDistance <= 0;
        long limit = everywhere ? 0L : (long) voiceDistance * voiceDistance;

        for (UUID id : guests.keySet()) {
            if (id.equals(from.getUniqueId())) continue;

            double[] theirs = where.get(id);
            if (theirs == null) continue;

            if (!everywhere) {
                String theirWorld = worlds.get(id);
                if (theirWorld == null || !theirWorld.equals(world)) continue;

                double dx = theirs[0] - at[0];
                double dy = theirs[1] - at[1];
                double dz = theirs[2] - at[2];
                if (dx * dx + dy * dy + dz * dz > limit) continue;
            }

            Player to = Bukkit.getPlayer(id);
            if (to == null) continue;

            // через голосовой порт, если он открыт и слушатель до нас достучался
            if (voicePipe != null && voicePipe.send(id, who, at[0], at[1], at[2], data))
                continue;

            channel.sendVoice(to, who, at[0], at[1], at[2], data);
        }
    }

    @EventHandler
    public void onQuit(PlayerQuitEvent event) {
        where.remove(event.getPlayer().getUniqueId());
        worlds.remove(event.getPlayer().getUniqueId());

        if (voicePipe != null) voicePipe.forget(event.getPlayer().getUniqueId());

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
