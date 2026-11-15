package com.winnigames2024.sulak;

import com.winnigames2024.sulak.entity.SulakHelperEntity;
import net.minecraft.ChatFormatting;
import net.minecraft.network.chat.Component;
import net.minecraft.server.MinecraftServer;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.world.entity.Entity;
import net.minecraft.world.entity.EntitySpawnReason;
import net.minecraft.world.phys.Vec3;

import java.util.Map;
import java.util.Set;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;

/** Вся серверная логика Сулака: кто его хозяин, кто ждёт ввода, лимиты, запросы к ИИ. */
public final class SulakManager {

    private SulakManager() {}

    /** Игроки, которые нажали на Сулака и чья следующая фраза в чате — это желание. */
    private static final Set<UUID> WAITING = ConcurrentHashMap.newKeySet();
    /** Игроки, чей запрос сейчас летит к ИИ. */
    private static final Set<UUID> BUSY = ConcurrentHashMap.newKeySet();
    /** UUID живого Сулака в каждом мире. */
    private static final Map<String, UUID> HELPERS = new ConcurrentHashMap<>();

    // ---- появление ----------------------------------------------------

    /** Перевыбирает случайного игрока и ставит Сулака рядом с ним. */
    public static void rollOwner(MinecraftServer server) {
        var players = server.getPlayerList().getPlayers();
        if (players.isEmpty()) return;
        ServerPlayer target = players.get(server.overworld().getRandom().nextInt(players.size()));
        spawnFor(target);
    }

    public static void spawnFor(ServerPlayer player) {
        ServerLevel level = player.level();
        removeExisting(server(player));

        SulakHelperEntity helper = Sulak.HELPER.get().create(level, EntitySpawnReason.MOB_SUMMONED);
        if (helper == null) return;

        Vec3 pos = player.position().add(
                (player.getRandom().nextDouble() - 0.5D) * 4.0D, 0.0D,
                (player.getRandom().nextDouble() - 0.5D) * 4.0D);
        helper.snapTo(pos.x, pos.y, pos.z, player.getYRot() + 180.0F, 0.0F);
        helper.setOwner(player.getUUID());
        level.addFreshEntity(helper);
        HELPERS.put(level.dimension().location().toString(), helper.getUUID());

        SulakState.get().ownerUuid = player.getUUID().toString();
        SulakState.save();

        player.sendSystemMessage(Component.literal("Рядом с тобой появился ")
                .withStyle(ChatFormatting.GRAY)
                .append(Component.literal("Сулак").withStyle(ChatFormatting.DARK_PURPLE))
                .append(Component.literal(". Нажми на него, чтобы загадать желание.")
                        .withStyle(ChatFormatting.GRAY)));
    }

    private static MinecraftServer server(ServerPlayer p) {
        return p.level().getServer();
    }

    /** Убирает прежнего Сулака со всех миров. */
    public static void removeExisting(MinecraftServer server) {
        for (ServerLevel level : server.getAllLevels()) {
            UUID id = HELPERS.get(level.dimension().location().toString());
            if (id == null) continue;
            Entity e = level.getEntity(id);
            if (e instanceof SulakHelperEntity) e.discard();
            HELPERS.remove(level.dimension().location().toString());
        }
    }

    // ---- взаимодействие ------------------------------------------------

    public static void onInteract(ServerPlayer player, SulakHelperEntity helper) {
        UUID id = player.getUUID();
        SulakState state = SulakState.get();

        if (BUSY.contains(id)) {
            SulakActions.tell(player, "Подожди, я ещё думаю над прошлым желанием.");
            return;
        }

        if (state.remaining(id) <= 0) {
            helper.setAngry(true);
            SulakCurse.afflict(player);
            SulakActions.tell(player, "Твои желания кончились. Теперь моя очередь просить.");
            return;
        }

        if (WAITING.add(id)) {
            SulakActions.tell(player, "Говори. Напиши желание в чат — одно сообщение. Осталось желаний: "
                    + state.remaining(id) + ".");
        } else {
            SulakActions.tell(player, "Я слушаю. Пиши в чат.");
        }
    }

    public static boolean isWaiting(ServerPlayer player) {
        return WAITING.contains(player.getUUID());
    }

    /** Возвращает true, если сообщение было перехвачено как желание. */
    public static boolean consumeChat(ServerPlayer player, String message) {
        UUID id = player.getUUID();
        if (!WAITING.remove(id)) return false;
        if (message == null || message.isBlank()) return false;

        SulakState state = SulakState.get();
        if (state.remaining(id) <= 0) {
            SulakCurse.afflict(player);
            return true;
        }

        state.spend(id);
        BUSY.add(id);

        SulakActions.tell(player, "Слышу тебя...");
        MinecraftServer server = player.level().getServer();
        String system = SulakPrompt.build(player);
        String key = state.keyFor(id);

        AIWinnigames2024RU.ask(id, system, message, key).whenComplete((result, error) -> server.execute(() -> {
            BUSY.remove(id);
            ServerPlayer p = server.getPlayerList().getPlayer(id);
            if (p == null) return;

            if (error != null || result == null || !result.ok) {
                String why = error != null ? error.getMessage() : (result == null ? "нет ответа" : result.error);
                SulakActions.tell(p, "Моя связь с той стороной оборвалась (" + why + "). Желание тебе возвращено.");
                state.setUsed(id, Math.max(0, state.usedBy(id) - 1));
                return;
            }

            if (result.say != null && !result.say.isBlank()) SulakActions.tell(p, result.say);
            SulakActions.runAll(p, result.actions);

            int left = state.remaining(id);
            if (left > 0) {
                SulakActions.tell(p, "Осталось желаний: " + left + ".");
            } else {
                SulakActions.tell(p, "Это было последнее.");
                SulakCurse.afflict(p);
            }
        }));
        return true;
    }

    public static void forget(ServerPlayer player) {
        WAITING.remove(player.getUUID());
        BUSY.remove(player.getUUID());
    }

    public static void clearAll() {
        WAITING.clear();
        BUSY.clear();
        HELPERS.clear();
    }
}
