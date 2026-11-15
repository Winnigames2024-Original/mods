package com.winnigames2024.sulak.event;

import com.winnigames2024.sulak.Config;
import com.winnigames2024.sulak.Sulak;
import com.winnigames2024.sulak.SulakActions;
import com.winnigames2024.sulak.SulakCommands;
import com.winnigames2024.sulak.SulakCurse;
import com.winnigames2024.sulak.SulakManager;
import com.winnigames2024.sulak.SulakState;
import net.minecraft.server.MinecraftServer;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.world.entity.LivingEntity;
import net.minecraftforge.event.RegisterCommandsEvent;
import net.minecraftforge.event.ServerChatEvent;
import net.minecraftforge.event.entity.living.LivingDeathEvent;
import net.minecraftforge.event.entity.player.PlayerEvent;
import net.minecraftforge.event.server.ServerStartedEvent;
import net.minecraftforge.event.server.ServerStoppingEvent;
import net.minecraftforge.event.TickEvent;
import net.minecraftforge.eventbus.api.listener.SubscribeEvent;
import net.minecraftforge.fml.common.Mod;
import net.minecraftforge.registries.ForgeRegistries;

import java.util.List;

@Mod.EventBusSubscriber(modid = Sulak.MODID, bus = Mod.EventBusSubscriber.Bus.FORGE)
public class SulakEvents {

    @SubscribeEvent
    public static void onServerStarted(ServerStartedEvent event) {
        SulakState.load(event.getServer());
        SulakManager.clearAll();
    }

    @SubscribeEvent
    public static void onServerStopping(ServerStoppingEvent event) {
        SulakState.unload();
        SulakManager.clearAll();
    }

    @SubscribeEvent
    public static void onCommands(RegisterCommandsEvent event) {
        SulakCommands.register(event.getDispatcher());
    }

    @SubscribeEvent
    public static void onLogin(PlayerEvent.PlayerLoggedInEvent event) {
        if (!(event.getEntity() instanceof ServerPlayer player)) return;
        MinecraftServer server = player.level().getServer();

        if (SulakState.get().isCursed(player.getUUID())) {
            SulakCurse.joinTeam(player);
        }

        if (!Config.spawnOnJoin) return;
        if (server.overworld().getRandom().nextDouble() > Config.spawnChance) return;

        // немного позже входа, чтобы игрок успел прогрузиться
        server.execute(() -> server.execute(() -> SulakManager.rollOwner(server)));
    }

    @SubscribeEvent
    public static void onLogout(PlayerEvent.PlayerLoggedOutEvent event) {
        if (event.getEntity() instanceof ServerPlayer player) SulakManager.forget(player);
    }

    @SubscribeEvent
    public static boolean onChat(ServerChatEvent event) {
        ServerPlayer player = event.getPlayer();
        if (player == null) return false;
        if (!SulakManager.isWaiting(player)) return false;
        // true = сообщение в общий чат не уходит, это желание
        return SulakManager.consumeChat(player, event.getRawText());
    }

    @SubscribeEvent
    public static void onServerTick(TickEvent.ServerTickEvent.Post event) {
        SulakCurse.tick(event.server());
    }

    /** Правила дропа, выданные ИИ: "из эндердракона падают алмазы". */
    @SubscribeEvent
    public static void onDeath(LivingDeathEvent event) {
        LivingEntity entity = event.getEntity();
        if (entity.level().isClientSide()) return;
        var key = ForgeRegistries.ENTITY_TYPES.getKey(entity.getType());
        if (key == null) return;

        List<SulakState.DropRule> rules = SulakState.get().rulesFor(key.toString());
        if (rules == null || rules.isEmpty()) return;

        MinecraftServer server = entity.level().getServer();
        if (server == null) return;

        int x = (int) Math.floor(entity.getX());
        int y = (int) Math.floor(entity.getY()) + 1;
        int z = (int) Math.floor(entity.getZ());

        for (SulakState.DropRule rule : rules) {
            int left = Math.max(1, rule.count);
            while (left > 0) {
                int part = Math.min(left, 64);
                SulakActions.cmd(server, "summon minecraft:item " + x + " " + y + " " + z
                        + " {Item:{id:\"" + rule.item + "\",count:" + part + "}}");
                left -= part;
            }
        }
    }
}
