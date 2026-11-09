package com.winnigames2024.forge_narevisuals;

import com.mojang.brigadier.arguments.IntegerArgumentType;
import com.mojang.brigadier.arguments.StringArgumentType;
import com.mojang.brigadier.suggestion.SuggestionProvider;
import net.minecraft.commands.CommandSourceStack;
import net.minecraft.commands.Commands;
import net.minecraft.commands.SharedSuggestionProvider;
import net.minecraft.core.BlockPos;
import net.minecraft.core.registries.Registries;
import net.minecraft.network.chat.Component;
import net.minecraft.resources.ResourceKey;
import net.minecraft.resources.ResourceLocation;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.world.level.Level;
import net.minecraft.world.level.levelgen.Heightmap;
import net.minecraftforge.event.RegisterCommandsEvent;
import net.minecraftforge.eventbus.api.SubscribeEvent;
import net.minecraftforge.fml.common.Mod;

import java.util.Set;
import java.util.concurrent.ThreadLocalRandom;

/**
 * Серверные команды: /rtp (случайный телепорт), /sethome, /home, /delhome.
 * Все переносы без урона от падения (через NareFall). Работают на интегрированном
 * сервере «Вместе» и на любом Forge-сервере с модом.
 */
@Mod.EventBusSubscriber(modid = Forge_narevisuals.MODID)
public final class NareCmd {

    private NareCmd() { }

    private static final SuggestionProvider<CommandSourceStack> HOME_NAMES = (ctx, builder) -> {
        try {
            ServerPlayer p = ctx.getSource().getPlayerOrException();
            return SharedSuggestionProvider.suggest(NareHomes.names(p.getStringUUID()), builder);
        } catch (Exception e) {
            return builder.buildFuture();
        }
    };

    @SubscribeEvent
    public static void onRegister(RegisterCommandsEvent e) {
        e.getDispatcher().register(Commands.literal("rtp").requires(s -> true)
                .executes(ctx -> rtp(ctx.getSource(), 5000))
                .then(Commands.argument("радиус", IntegerArgumentType.integer(100, 100000))
                        .executes(ctx -> rtp(ctx.getSource(), IntegerArgumentType.getInteger(ctx, "радиус")))));

        e.getDispatcher().register(Commands.literal("sethome").requires(s -> true)
                .then(Commands.argument("имя", StringArgumentType.word())
                        .executes(ctx -> setHome(ctx.getSource(), StringArgumentType.getString(ctx, "имя")))));

        e.getDispatcher().register(Commands.literal("home").requires(s -> true)
                .then(Commands.argument("имя", StringArgumentType.word()).suggests(HOME_NAMES)
                        .executes(ctx -> goHome(ctx.getSource(), StringArgumentType.getString(ctx, "имя")))));

        e.getDispatcher().register(Commands.literal("delhome").requires(s -> true)
                .then(Commands.argument("имя", StringArgumentType.word()).suggests(HOME_NAMES)
                        .executes(ctx -> delHome(ctx.getSource(), StringArgumentType.getString(ctx, "имя")))));

        e.getDispatcher().register(Commands.literal("virtualchest").requires(s -> true)
                .executes(ctx -> vchest(ctx.getSource())));
        e.getDispatcher().register(Commands.literal("vc").requires(s -> true)
                .executes(ctx -> vchest(ctx.getSource())));
    }

    private static int vchest(CommandSourceStack src) {
        ServerPlayer p;
        try { p = src.getPlayerOrException(); } catch (Exception ex) { return 0; }
        NareVchest.open(p);
        return 1;
    }

    /** Общий перенос: прогружает чанк, ставит игрока, гасит урон и защиту от кика. */
    static void place(ServerPlayer p, ServerLevel level, double x, double y, double z, float yaw, float pitch) {
        // прогрузить целевой чанк — иначе при переносе далеко (rtp) сервер кикает по таймауту
        try { level.getChunk(((int) Math.floor(x)) >> 4, ((int) Math.floor(z)) >> 4); } catch (Throwable ignore) { }
        p.teleportTo(level, x, y, z, Set.of(), yaw, pitch, false);
        p.fallDistance = 0f;
        NareFall.mark(p);
        resetFloating(p);
    }

    /** Сбросить счётчики «полёта», чтобы сервер не кикнул «Flying is not enabled». */
    private static void resetFloating(ServerPlayer p) {
        Object conn = p.connection;
        if (conn == null) return;
        String[] names = { "aboveGroundTickCount", "aboveGroundVehicleTickCount", "floatingTickCount" };
        for (String name : names) {
            Class<?> c = conn.getClass();
            while (c != null) {
                try {
                    java.lang.reflect.Field f = c.getDeclaredField(name);
                    f.setAccessible(true);
                    f.setInt(conn, 0);
                    break;
                } catch (NoSuchFieldException nsf) {
                    c = c.getSuperclass();
                } catch (Throwable ignore) {
                    break;
                }
            }
        }
    }

    private static int rtp(CommandSourceStack src, int radius) {
        ServerPlayer p;
        try { p = src.getPlayerOrException(); } catch (Exception ex) { return 0; }
        ServerLevel level = p.serverLevel();
        ThreadLocalRandom rnd = ThreadLocalRandom.current();
        for (int tries = 0; tries < 24; tries++) {
            int x = rnd.nextInt(-radius, radius + 1);
            int z = rnd.nextInt(-radius, radius + 1);
            int y = level.getHeight(Heightmap.Types.MOTION_BLOCKING_NO_LEAVES, x, z);
            if (y <= level.getMinY() + 1) continue;
            BlockPos pos = new BlockPos(x, y, z);
            if (level.getBlockState(pos).getFluidState().isEmpty()
                    && level.getBlockState(pos.below()).getFluidState().isEmpty()) {
                final int fx = x, fy = y, fz = z;
                place(p, level, x + 0.5, y, z + 0.5, p.getYRot(), p.getXRot());
                src.sendSuccess(() -> Component.literal("Случайный телепорт: " + fx + ", " + fy + ", " + fz), false);
                return 1;
            }
        }
        src.sendFailure(Component.literal("Не нашёл безопасное место, попробуйте ещё раз"));
        return 0;
    }

    private static int setHome(CommandSourceStack src, String name) {
        ServerPlayer p;
        try { p = src.getPlayerOrException(); } catch (Exception ex) { return 0; }
        NareHomes.Home h = new NareHomes.Home();
        h.dim = p.serverLevel().dimension().location().toString();
        h.x = p.getX(); h.y = p.getY(); h.z = p.getZ();
        h.yaw = p.getYRot(); h.pitch = p.getXRot();
        NareHomes.set(p.getStringUUID(), name, h);
        src.sendSuccess(() -> Component.literal("Точка дома «" + name + "» сохранена"), false);
        return 1;
    }

    private static int goHome(CommandSourceStack src, String name) {
        ServerPlayer p;
        try { p = src.getPlayerOrException(); } catch (Exception ex) { return 0; }
        NareHomes.Home h = NareHomes.get(p.getStringUUID(), name);
        if (h == null) { src.sendFailure(Component.literal("Нет точки «" + name + "»")); return 0; }
        ServerLevel level = p.server.getLevel(dimKey(h.dim));
        if (level == null) level = p.serverLevel();
        place(p, level, h.x, h.y, h.z, h.yaw, h.pitch);
        src.sendSuccess(() -> Component.literal("Телепорт домой: " + name), false);
        return 1;
    }

    private static int delHome(CommandSourceStack src, String name) {
        ServerPlayer p;
        try { p = src.getPlayerOrException(); } catch (Exception ex) { return 0; }
        boolean ok = NareHomes.delete(p.getStringUUID(), name);
        if (ok) src.sendSuccess(() -> Component.literal("Точка «" + name + "» удалена"), false);
        else src.sendFailure(Component.literal("Нет точки «" + name + "»"));
        return ok ? 1 : 0;
    }

    private static ResourceKey<Level> dimKey(String dim) {
        ResourceLocation id;
        try { id = ResourceLocation.parse(dim); } catch (Exception ex) { id = ResourceLocation.withDefaultNamespace("overworld"); }
        return ResourceKey.create(Registries.DIMENSION, id);
    }
}
