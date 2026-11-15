package com.winnigames2024.sulak;

import net.minecraft.ChatFormatting;
import net.minecraft.network.chat.Component;
import net.minecraft.server.MinecraftServer;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.world.item.ItemStack;
import net.minecraft.world.scores.PlayerTeam;
import net.minecraft.world.scores.Scoreboard;
import net.minecraft.world.scores.Team;

import java.util.List;
import java.util.Random;

/**
 * Что происходит, когда лимит желаний закончился.
 * Проклятые игроки заносятся в команду "sulak_cursed" — она синхронизируется
 * с клиентами, и мод на клиенте подменяет им скин.
 */
public final class SulakCurse {

    private SulakCurse() {}

    public static final String TEAM = "sulak_cursed";
    private static final Random RNG = new Random();

    private static final String[] WHISPERS = {
            "Ты всё ещё думаешь, что это была сделка?",
            "Я помню каждое твоё желание.",
            "Обернись.",
            "Твоё имя мне теперь принадлежит.",
            "Ещё немного.",
            "Не закрывай глаза."
    };

    public static void afflict(ServerPlayer player) {
        if (!Config.curseEnabled) return;
        SulakState state = SulakState.get();
        if (state.isCursed(player.getUUID())) return;
        state.setCursed(player.getUUID(), true);
        joinTeam(player);

        player.sendSystemMessage(Component.literal("Что-то изменилось.")
                .withStyle(ChatFormatting.DARK_RED, ChatFormatting.ITALIC));
        SulakActions.cmd(player.level().getServer(), "playsound minecraft:entity.wither.spawn master "
                + player.getScoreboardName());
    }

    public static void cure(ServerPlayer player) {
        SulakState.get().setCursed(player.getUUID(), false);
        Scoreboard sb = player.level().getServer().getScoreboard();
        PlayerTeam team = sb.getPlayerTeam(TEAM);
        if (team != null) sb.removePlayerFromTeam(player.getScoreboardName(), team);
        SulakActions.cmd(player.level().getServer(), "effect clear " + player.getScoreboardName());
    }

    /** Заводит команду, если её нет, и добавляет игрока. */
    public static void joinTeam(ServerPlayer player) {
        if (!Config.curseChangesSkin) return;
        Scoreboard sb = player.level().getServer().getScoreboard();
        PlayerTeam team = sb.getPlayerTeam(TEAM);
        if (team == null) {
            team = sb.addPlayerTeam(TEAM);
            team.setColor(ChatFormatting.DARK_RED);
            team.setDisplayName(Component.literal("Отмеченные Сулаком"));
            team.setNameTagVisibility(Team.Visibility.ALWAYS);
        }
        sb.addPlayerToTeam(player.getScoreboardName(), team);
    }

    /** Вызывается с тика сервера. */
    public static void tick(MinecraftServer server) {
        if (!Config.curseEnabled) return;
        if (server.getTickCount() % 20 != 0) return;

        SulakState state = SulakState.get();
        List<ServerPlayer> players = server.getPlayerList().getPlayers();
        for (ServerPlayer p : players) {
            if (!state.isCursed(p.getUUID())) continue;
            joinTeam(p);
            // шанс события примерно раз в curseIntervalTicks тиков
            if (RNG.nextInt(Math.max(1, Config.curseIntervalTicks / 20)) != 0) continue;
            strangeThing(p);
        }
    }

    private static void strangeThing(ServerPlayer p) {
        String name = p.getScoreboardName();
        MinecraftServer server = p.level().getServer();

        switch (RNG.nextInt(12)) {
            case 0 -> SulakActions.cmd(server, "effect give " + name + " minecraft:blindness 3 0 true");
            case 1 -> SulakActions.cmd(server, "effect give " + name + " minecraft:nausea 8 0 true");
            case 2 -> SulakActions.cmd(server, "playsound minecraft:entity.enderman.stare master " + name);
            case 3 -> SulakActions.cmd(server, "playsound minecraft:ambient.cave master " + name);
            case 4 -> {
                // лёгкий рывок в сторону
                double dx = (RNG.nextDouble() - 0.5D) * 6.0D;
                double dz = (RNG.nextDouble() - 0.5D) * 6.0D;
                p.teleportTo(p.getX() + dx, p.getY(), p.getZ() + dz);
            }
            case 5 -> {
                // предметы в инвентаре меняются местами
                var inv = p.getInventory();
                int a = RNG.nextInt(inv.getContainerSize());
                int b = RNG.nextInt(inv.getContainerSize());
                ItemStack sa = inv.getItem(a);
                ItemStack sb = inv.getItem(b);
                inv.setItem(a, sb);
                inv.setItem(b, sa);
                inv.setChanged();
            }
            case 6 -> p.sendSystemMessage(Component.literal(WHISPERS[RNG.nextInt(WHISPERS.length)])
                    .withStyle(ChatFormatting.DARK_GRAY, ChatFormatting.ITALIC));
            case 7 -> {
                if (p.level() instanceof ServerLevel sl)
                    SulakActions.cmd(server, "summon minecraft:lightning_bolt "
                            + (int) p.getX() + " " + (int) p.getY() + " " + (int) (p.getZ() + 4));
            }
            case 8 -> SulakActions.cmd(server, "effect give " + name + " minecraft:darkness 6 0 true");
            case 9 -> SulakActions.cmd(server, "playsound minecraft:entity.ghast.scream master " + name);
            case 10 -> SulakActions.cmd(server, "effect give " + name + " minecraft:slowness 5 2 true");
            default -> SulakActions.cmd(server, "playsound minecraft:block.portal.trigger master " + name);
        }
    }
}
