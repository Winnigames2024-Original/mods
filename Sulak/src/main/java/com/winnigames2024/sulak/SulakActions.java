package com.winnigames2024.sulak;

import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import net.minecraft.ChatFormatting;
import net.minecraft.commands.CommandSourceStack;
import net.minecraft.core.BlockPos;
import net.minecraft.network.chat.Component;
import net.minecraft.network.protocol.game.ClientboundSetTitleTextPacket;
import net.minecraft.network.protocol.game.ClientboundSetSubtitleTextPacket;
import net.minecraft.server.MinecraftServer;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.world.item.ItemStack;
import net.minecraft.world.level.Level;

import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

/** Исполняет действия, которые вернул ИИ. Всегда на главном потоке сервера. */
public final class SulakActions {

    private SulakActions() {}

    public static void runAll(ServerPlayer owner, JsonArray actions) {
        if (actions == null) return;
        int seen = 0;
        for (JsonElement el : actions) {
            if (!el.isJsonObject()) continue;
            if (++seen > 64) break; // предохранитель от бесконечных списков
            try {
                run(owner, el.getAsJsonObject());
            } catch (Exception e) {
                Sulak.LOGGER.warn("[Sulak] action failed: {}", el, e);
            }
        }
    }

    private static boolean run(ServerPlayer owner, JsonObject a) {
        MinecraftServer server = owner.level().getServer();
        String type = str(a, "type", "").toLowerCase(Locale.ROOT);
        List<ServerPlayer> targets = resolve(owner, str(a, "target", "@owner"));

        switch (type) {
            case "give": {
                String item = str(a, "item", "minecraft:stone");
                int count = num(a, "count", 1);
                for (ServerPlayer p : targets) {
                    int left = Math.max(1, Math.min(count, 2304));
                    while (left > 0) {
                        int part = Math.min(left, 64);
                        cmd(server, "give " + p.getScoreboardName() + " " + item + " " + part);
                        left -= part;
                    }
                }
                return true;
            }
            case "kill": {
                if (!Config.allowKill) {
                    tell(owner, "Сулак не может отнимать жизни на этом сервере.");
                    return false;
                }
                for (ServerPlayer p : targets) cmd(server, "kill " + p.getScoreboardName());
                return true;
            }
            case "damage": {
                if (!Config.allowKill) return false;
                float amount = (float) num(a, "amount", 4);
                for (ServerPlayer p : targets)
                    cmd(server, "damage " + p.getScoreboardName() + " " + amount + " minecraft:magic");
                return true;
            }
            case "heal": {
                for (ServerPlayer p : targets)
                    cmd(server, "effect give " + p.getScoreboardName() + " minecraft:instant_health 1 10 true");
                return true;
            }
            case "feed": {
                for (ServerPlayer p : targets)
                    cmd(server, "effect give " + p.getScoreboardName() + " minecraft:saturation 2 10 true");
                return true;
            }
            case "effect": {
                String effect = str(a, "effect", "minecraft:speed");
                int duration = Math.max(1, num(a, "duration", 30));
                int amplifier = Math.max(0, Math.min(num(a, "amplifier", 0), 10));
                for (ServerPlayer p : targets)
                    cmd(server, "effect give " + p.getScoreboardName() + " " + effect + " " + duration + " " + amplifier);
                return true;
            }
            case "clear_effects": {
                for (ServerPlayer p : targets) cmd(server, "effect clear " + p.getScoreboardName());
                return true;
            }
            case "replace_items": {
                String from = str(a, "from", "");
                String to = str(a, "to", "minecraft:dirt");
                for (ServerPlayer p : targets) replaceItems(p, from, to);
                return true;
            }
            case "clear_inventory": {
                String item = str(a, "item", null);
                for (ServerPlayer p : targets)
                    cmd(server, "clear " + p.getScoreboardName() + (item == null ? "" : " " + item));
                return true;
            }
            case "drop_rule": {
                String entity = str(a, "entity", "minecraft:ender_dragon");
                String item = str(a, "item", "minecraft:diamond");
                int count = Math.max(1, Math.min(num(a, "count", 1), 512));
                SulakState.get().addDropRule(entity, item, count);
                return true;
            }
            case "setblock": {
                String block = str(a, "block", "minecraft:stone");
                for (ServerPlayer p : targets) {
                    BlockPos pos = p.blockPosition().offset(num(a, "dx", 0), num(a, "dy", -1), num(a, "dz", 0));
                    cmd(server, "setblock " + pos.getX() + " " + pos.getY() + " " + pos.getZ() + " " + block);
                }
                return true;
            }
            case "fill": {
                String block = str(a, "block", "minecraft:glass");
                int r = Math.max(1, Math.min(num(a, "radius", 3), 16));
                boolean hollow = bool(a, "hollow", true);
                for (ServerPlayer p : targets) {
                    BlockPos c = p.blockPosition();
                    cmd(server, "fill "
                            + (c.getX() - r) + " " + (c.getY() - 1) + " " + (c.getZ() - r) + " "
                            + (c.getX() + r) + " " + (c.getY() + r) + " " + (c.getZ() + r) + " "
                            + block + (hollow ? " hollow" : " replace"));
                }
                return true;
            }
            case "summon": {
                String entity = str(a, "entity", "minecraft:zombie");
                int count = Math.max(1, Math.min(num(a, "count", 1), 32));
                for (ServerPlayer p : targets) {
                    BlockPos pos = p.blockPosition();
                    for (int i = 0; i < count; i++)
                        cmd(server, "summon " + entity + " "
                                + (pos.getX() + (i % 5) - 2) + " " + pos.getY() + " " + (pos.getZ() + (i / 5) - 2));
                }
                return true;
            }
            case "teleport": {
                List<ServerPlayer> to = resolve(owner, str(a, "to", "@owner"));
                if (to.isEmpty()) return false;
                String dest = to.get(0).getScoreboardName();
                for (ServerPlayer p : targets)
                    cmd(server, "tp " + p.getScoreboardName() + " " + dest);
                return true;
            }
            case "time": {
                cmd(server, "time set " + str(a, "value", "day"));
                return true;
            }
            case "weather": {
                cmd(server, "weather " + str(a, "value", "clear"));
                return true;
            }
            case "lightning": {
                for (ServerPlayer p : targets) {
                    BlockPos pos = p.blockPosition();
                    cmd(server, "summon minecraft:lightning_bolt " + pos.getX() + " " + pos.getY() + " " + pos.getZ());
                }
                return true;
            }
            case "explosion": {
                float power = (float) Math.max(1, Math.min(num(a, "power", 2), 6));
                for (ServerPlayer p : targets) {
                    if (p.level() instanceof ServerLevel sl)
                        sl.explode(null, p.getX(), p.getY(), p.getZ(), power, Level.ExplosionInteraction.MOB);
                }
                return true;
            }
            case "xp": {
                int levels = num(a, "levels", 1);
                for (ServerPlayer p : targets)
                    cmd(server, "xp add " + p.getScoreboardName() + " " + levels + " levels");
                return true;
            }
            case "gamemode": {
                String v = str(a, "value", "survival");
                for (ServerPlayer p : targets)
                    cmd(server, "gamemode " + v + " " + p.getScoreboardName());
                return true;
            }
            case "sound": {
                String sound = str(a, "sound", "minecraft:entity.enderman.scream");
                for (ServerPlayer p : targets)
                    cmd(server, "playsound " + sound + " master " + p.getScoreboardName());
                return true;
            }
            case "title": {
                String text = str(a, "text", "");
                for (ServerPlayer p : targets) {
                    p.connection.send(new ClientboundSetTitleTextPacket(
                            Component.literal(text).withStyle(ChatFormatting.DARK_PURPLE)));
                    p.connection.send(new ClientboundSetSubtitleTextPacket(
                            Component.literal("Сулак").withStyle(ChatFormatting.GRAY)));
                }
                return true;
            }
            case "message": {
                String text = str(a, "text", "");
                for (ServerPlayer p : targets) tell(p, text);
                return true;
            }
            case "command": {
                if (!Config.allowRawCommands) return false;
                cmd(server, str(a, "command", ""));
                return true;
            }
            default:
                Sulak.LOGGER.info("[Sulak] unknown action type '{}'", type);
                return false;
        }
    }

    // ---- вспомогательное ---------------------------------------------

    /** Замена предметов в инвентаре (например, алмазы -> земля). */
    private static void replaceItems(ServerPlayer player, String fromId, String toId) {
        if (fromId == null || fromId.isBlank()) return;
        String from = normalize(fromId);
        var inv = player.getInventory();
        for (int i = 0; i < inv.getContainerSize(); i++) {
            ItemStack stack = inv.getItem(i);
            if (stack.isEmpty()) continue;
            String id = net.minecraftforge.registries.ForgeRegistries.ITEMS.getKey(stack.getItem()) == null
                    ? "" : net.minecraftforge.registries.ForgeRegistries.ITEMS.getKey(stack.getItem()).toString();
            if (!id.equals(from)) continue;
            int count = stack.getCount();
            inv.setItem(i, ItemStack.EMPTY);
            cmd(player.level().getServer(), "give " + player.getScoreboardName() + " " + normalize(toId) + " " + count);
        }
        inv.setChanged();
    }

    private static String normalize(String id) {
        if (id == null) return "minecraft:air";
        id = id.trim();
        return id.contains(":") ? id : "minecraft:" + id;
    }

    public static void cmd(MinecraftServer server, String command) {
        if (command == null || command.isBlank()) return;
        CommandSourceStack src = server.createCommandSourceStack().withPermission(4).withSuppressedOutput();
        server.getCommands().performPrefixedCommand(src, command);
    }

    public static void tell(ServerPlayer p, String text) {
        p.sendSystemMessage(Component.literal("[Сулак] ").withStyle(ChatFormatting.DARK_PURPLE)
                .append(Component.literal(text).withStyle(ChatFormatting.LIGHT_PURPLE)));
    }

    private static List<ServerPlayer> resolve(ServerPlayer owner, String target) {
        List<ServerPlayer> out = new ArrayList<>();
        MinecraftServer server = owner.level().getServer();
        String t = target == null ? "@owner" : target.trim();

        if (t.isEmpty() || t.equalsIgnoreCase("@owner") || t.equalsIgnoreCase("@s") || t.equalsIgnoreCase("@p")) {
            out.add(owner);
        } else if (t.equalsIgnoreCase("@all") || t.equalsIgnoreCase("@a") || t.equalsIgnoreCase("all")) {
            out.addAll(server.getPlayerList().getPlayers());
        } else if (t.equalsIgnoreCase("@random") || t.equalsIgnoreCase("@r")) {
            List<ServerPlayer> all = server.getPlayerList().getPlayers();
            if (!all.isEmpty()) out.add(all.get(owner.getRandom().nextInt(all.size())));
        } else {
            ServerPlayer p = server.getPlayerList().getPlayerByName(t);
            if (p != null) out.add(p);
        }
        return out;
    }

    private static String str(JsonObject o, String key, String def) {
        if (o.has(key) && o.get(key).isJsonPrimitive()) return o.get(key).getAsString();
        return def;
    }

    private static int num(JsonObject o, String key, int def) {
        try {
            if (o.has(key) && o.get(key).isJsonPrimitive()) return o.get(key).getAsInt();
        } catch (Exception ignored) {
        }
        return def;
    }

    private static boolean bool(JsonObject o, String key, boolean def) {
        try {
            if (o.has(key) && o.get(key).isJsonPrimitive()) return o.get(key).getAsBoolean();
        } catch (Exception ignored) {
        }
        return def;
    }
}
