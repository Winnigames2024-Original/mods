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

    private boolean requireMod;
    private int requireSeconds;
    private String requireMessage = "";

    private int tntMaxNearby;
    private int tntRadius;
    private boolean tntRescue;
    private String tntRescueCommand = "";

    private boolean ruleFreecam = true;
    private int ruleFreecamRange;
    private boolean ruleEspPlayers = true;
    private boolean ruleEspMobs = true;
    private boolean ruleBright = true;
    private boolean ruleZoom = true;
    private String voiceBind = "";

    /** Отдельный порт для голоса. null — звук идёт по игровому соединению. */
    private NareVoicePort voicePipe;

    private NareParkour parkour;
    private boolean parkourRespawn;
    private boolean parkourKeep;
    private NareFiles files;

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

        parkour = new NareParkour(this);
        parkour.load();
        parkour.recipes();

        files = new NareFiles(this);

        var nare = getCommand("nare");
        if (nare != null) nare.setExecutor(this);
        else getLogger().warning("NareVisuals: команда /nare не зарегистрировалась — "
                + "плитки выдаются только крафтом");

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

    /** Команда /nare: выдать плитки и посмотреть, сколько их стоит. */
    @Override
    public boolean onCommand(org.bukkit.command.CommandSender sender,
                             org.bukkit.command.Command command,
                             String label, String[] args) {
        if (!sender.hasPermission("narevisuals.parkour")) {
            sender.sendMessage(net.kyori.adventure.text.Component.text("Нужно право narevisuals.parkour"));
            return true;
        }

        if (args.length == 0 || args[0].equalsIgnoreCase("help")) {
            sender.sendMessage(net.kyori.adventure.text.Component.text(
                    "/nare give [ник] checkpoint|uncheckpoint|jumppad [сила] — порядок слов любой"));
            sender.sendMessage(net.kyori.adventure.text.Component.text("/nare count — сколько плиток стоит"));
            sender.sendMessage(net.kyori.adventure.text.Component.text("Плитки ещё и крафтятся: керамика в кольце из"));
            sender.sendMessage(net.kyori.adventure.text.Component.text("жемчуга Края, редстоуна или слизи."));
            return true;
        }

        if (args[0].equalsIgnoreCase("count")) {
            sender.sendMessage(net.kyori.adventure.text.Component.text("Плиток паркура: " + (parkour == null ? 0 : parkour.count())));
            return true;
        }

        if (!args[0].equalsIgnoreCase("give") || args.length < 2) {
            sender.sendMessage(net.kyori.adventure.text.Component.text("Так: /nare give checkpoint|uncheckpoint|jumppad [сила] [ник]"));
            return true;
        }

        // порядок слов не важен: и «give ник плитка», и «give плитка ник»
        String kind = null;
        String nick = null;
        int power = 12;

        for (int i = 1; i < args.length; i++) {
            String word = args[i];
            String low = word.toLowerCase(java.util.Locale.ROOT);

            if (low.equals("checkpoint") || low.equals("uncheckpoint") || low.equals("jumppad")) {
                kind = low;
                continue;
            }

            if (word.chars().allMatch(Character::isDigit)) {
                try { power = Integer.parseInt(word); } catch (Exception ignored) { }
                continue;
            }

            nick = word;
        }

        if (kind == null) {
            sender.sendMessage(net.kyori.adventure.text.Component.text(
                    "Плитки бывают: checkpoint, uncheckpoint, jumppad"));
            return true;
        }

        Player target = nick != null ? Bukkit.getPlayerExact(nick)
                : (sender instanceof Player self ? self : null);

        if (target == null) {
            sender.sendMessage(net.kyori.adventure.text.Component.text(
                    nick == null ? "Кому выдавать? Укажите ник."
                                 : "Игрока " + nick + " нет на сервере"));
            return true;
        }

        target.getInventory().addItem(parkour.item(kind, power));
        sender.sendMessage(net.kyori.adventure.text.Component.text("Выдал: " + title(kind) + " для " + target.getName()));
        return true;
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
        if (parkour != null) parkour.save();

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
                                         "limits.bright", "limits.zoom",
                                         "require-mod.enabled", "require-mod.seconds",
                                         "require-mod.message",
                                         "tnt.max-nearby", "tnt.radius",
                                         "tnt.rescue", "tnt.rescue-command",
                                         "parkour.respawn", "parkour.keep-on-quit" }) {
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

        requireMod = getConfig().getBoolean("require-mod.enabled", false);
        requireSeconds = Math.max(5, getConfig().getInt("require-mod.seconds", 20));
        requireMessage = getConfig().getString("require-mod.message",
                "Сюда пускают только с модом NareVisuals.");

        parkourRespawn = getConfig().getBoolean("parkour.respawn", true);
        parkourKeep = getConfig().getBoolean("parkour.keep-on-quit", false);

        tntMaxNearby = Math.max(0, getConfig().getInt("tnt.max-nearby", 24));
        tntRadius = Math.max(1, getConfig().getInt("tnt.radius", 8));
        tntRescue = getConfig().getBoolean("tnt.rescue", true);
        tntRescueCommand = getConfig().getString("tnt.rescue-command", "spawn");

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

    /**
     * Пускаем только с модом: ждём, пока клиент поздоровается, и если не
     * дождались — отправляем с объяснением. Проверяем отложенно, потому что
     * вход на плохой связи бывает долгим.
     */
    @EventHandler
    public void onJoinCheck(org.bukkit.event.player.PlayerJoinEvent event) {
        if (!requireMod) return;

        Player player = event.getPlayer();
        if (player.hasPermission("narevisuals.bypass")) return;

        Bukkit.getScheduler().runTaskLater(this, () -> {
            if (!player.isOnline()) return;
            if (guests.containsKey(player.getUniqueId())) return;

            player.kick(net.kyori.adventure.text.Component.text(requireMessage));
            getLogger().info("NareVisuals: " + player.getName() + " без мода — отправлен");
        }, requireSeconds * 20L);
    }

    /**
     * Зажечь ТНТ рядом с кучей такой же нельзя. Блок остаётся лежать: человек
     * видит, что не поджигается, и это понятнее, чем десять секунд тишины у
     * всех на сервере.
     */
    @EventHandler(ignoreCancelled = true)
    public void onTntSpawn(org.bukkit.event.entity.EntitySpawnEvent event) {
        if (tntMaxNearby <= 0) return;
        if (!(event.getEntity() instanceof org.bukkit.entity.TNTPrimed)) return;

        var at = event.getLocation();
        int near = 0;

        for (var other : at.getWorld().getNearbyEntities(at, tntRadius, tntRadius, tntRadius)) {
            if (!(other instanceof org.bukkit.entity.TNTPrimed)) continue;
            if (++near < tntMaxNearby) continue;

            event.setCancelled(true);
            return;
        }
    }

    /**
     * Клиент говорит, что уже не тянет. Уводим человека и убираем то, из-за
     * чего всё встало: ему всё равно не видно и не нажать.
     */
    public void onPanic(Player player, int tnt) {
        if (!tntRescue) return;
        if (!guests.containsKey(player.getUniqueId())) return;

        var at = player.getLocation();
        int removed = 0;

        for (var entity : at.getWorld().getNearbyEntities(at, 48, 48, 48)) {
            if (!(entity instanceof org.bukkit.entity.TNTPrimed)) continue;
            entity.remove();
            removed++;
        }

        boolean moved = false;
        if (!tntRescueCommand.isBlank())
            moved = Bukkit.dispatchCommand(player, tntRescueCommand);

        if (!moved) player.teleport(at.getWorld().getSpawnLocation());

        getLogger().info("NareVisuals: увёл " + player.getName()
                + " (говорит, рядом " + tnt + " ТНТ), убрал " + removed);
    }

    // ------------------------------------------------------------ паркур

    /**
     * Смерть на паркуре возвращает к последнему чекпоинту, а не на спавн.
     * Ради этого плитки и ставят, иначе от них никакого толку.
     */
    @EventHandler
    public void onRespawn(org.bukkit.event.player.PlayerRespawnEvent event) {
        if (parkour == null || !parkourRespawn) return;

        var where = parkour.checkpointOf(event.getPlayer());
        if (where == null) return;

        event.setRespawnLocation(where);
        hint(event.getPlayer(), "Вернулись к чекпоинту");
    }

    /** Поставили помеченную плитку — запоминаем её. */
    @EventHandler(ignoreCancelled = true)
    public void onPlaceTile(org.bukkit.event.block.BlockPlaceEvent event) {
        if (parkour == null) return;

        var tile = parkour.kindOf(event.getItemInHand());
        if (tile == null) return;

        parkour.placed(event.getBlockPlaced(), tile);
        hint(event.getPlayer(), "Плитка поставлена: " + title(tile.kind()));
    }

    /** Ударили по плитке: игра сыграет ноту, а нам важно, чтобы вид не съехал. */
    @EventHandler(ignoreCancelled = true)
    public void onHitTile(org.bukkit.event.block.BlockDamageEvent event) {
        if (parkour == null) return;
        if (parkour.tileAt(event.getBlock().getLocation()) == null) return;

        var block = event.getBlock();
        Bukkit.getScheduler().runTask(this, () -> parkour.repaint(block));
    }

    /** Редстоун тоже умеет трогать нотный блок. */
    @EventHandler(ignoreCancelled = true)
    public void onTilePower(org.bukkit.event.block.BlockRedstoneEvent event) {
        if (parkour == null) return;
        if (parkour.tileAt(event.getBlock().getLocation()) == null) return;

        var block = event.getBlock();
        Bukkit.getScheduler().runTask(this, () -> parkour.repaint(block));
    }

    /** Сломали — забываем, иначе на этом месте потом «призрак». */
    @EventHandler(ignoreCancelled = true)
    public void onBreakTile(org.bukkit.event.block.BlockBreakEvent event) {
        if (parkour == null) return;
        if (parkour.tileAt(event.getBlock().getLocation()) == null) return;

        parkour.remove(event.getBlock().getLocation());
        hint(event.getPlayer(), "Плитка убрана");
    }

    /** Наступили на плитку. Проверяем только смену блока, а не каждый шаг. */
    @EventHandler(ignoreCancelled = true)
    public void onStep(org.bukkit.event.player.PlayerMoveEvent event) {
        if (parkour == null || parkour.count() == 0) return;

        var to = event.getTo();
        var from = event.getFrom();
        if (to.getBlockX() == from.getBlockX()
                && to.getBlockY() == from.getBlockY()
                && to.getBlockZ() == from.getBlockZ()) return;

        var under = to.clone().add(0, -0.2, 0).getBlock();
        String said = parkour.stepped(event.getPlayer(), under);
        if (said != null) hint(event.getPlayer(), said);
    }

    /** Правый клик по прыжковой плите у кого есть право — открыть настройку. */
    @EventHandler(ignoreCancelled = true)
    public void onTouchTile(org.bukkit.event.player.PlayerInteractEvent event) {
        if (parkour == null) return;
        if (event.getAction() != org.bukkit.event.block.Action.RIGHT_CLICK_BLOCK) return;
        if (event.getClickedBlock() == null) return;
        if (event.getHand() != org.bukkit.inventory.EquipmentSlot.HAND) return;

        var tile = parkour.tileAt(event.getClickedBlock().getLocation());
        if (tile == null) return;

        // по нотному блоку щелчок меняет ноту, а нота — это наша картинка
        event.setCancelled(true);

        if (!tile.kind().equals("jumppad")) return;
        if (!event.getPlayer().hasPermission("narevisuals.parkour")) return;
        if (!guests.containsKey(event.getPlayer().getUniqueId())) {
            hint(event.getPlayer(), "Настроить плиту можно только с модом");
            return;
        }

        var at = event.getClickedBlock().getLocation();
        channel.sendJumpPad(event.getPlayer(), at.getWorld().getName(),
                at.getBlockX(), at.getBlockY(), at.getBlockZ(), tile.power());
    }

    /** Мод прислал новую силу плиты. */
    public void onJumpPadSet(Player player, String world, int x, int y, int z, int power) {
        if (parkour == null) return;
        if (!player.hasPermission("narevisuals.parkour")) return;

        var level = Bukkit.getWorld(world);
        if (level == null) return;

        var at = new org.bukkit.Location(level, x, y, z);
        var tile = parkour.tileAt(at);
        if (tile == null || !tile.kind().equals("jumppad")) return;

        parkour.put(at, "jumppad", power);
        hint(player, "Сила плиты: " + NareParkour.clamp(power));
    }

    // -------------------------------------------------------- файлы мода

    /** Мод просит записать, удалить или перечислить свой файл. */
    public void onModFile(Player player, byte action, String name, byte[] data) {
        if (files == null) return;
        if (!guests.containsKey(player.getUniqueId())) return;

        switch (action) {
            case 0 -> files.write(player, name, data);
            case 1 -> files.delete(player, name);
            default -> { }
        }

        channel.sendFiles(player, files.list(player));
    }

    private static String title(String kind) {
        return switch (kind) {
            case "checkpoint" -> "чекпоинт";
            case "uncheckpoint" -> "сброс чекпоинта";
            default -> "прыжковая плита";
        };
    }

    private static void hint(Player player, String text) {
        player.sendActionBar(net.kyori.adventure.text.Component.text(text));
    }

    @EventHandler
    public void onQuit(PlayerQuitEvent event) {
        where.remove(event.getPlayer().getUniqueId());
        worlds.remove(event.getPlayer().getUniqueId());

        if (voicePipe != null) voicePipe.forget(event.getPlayer().getUniqueId());
        if (parkour != null && !parkourKeep) parkour.forget(event.getPlayer().getUniqueId());

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
