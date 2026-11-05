package com.winnigames2024.forge_narevisuals.client;

import com.winnigames2024.forge_narevisuals.Config;
import com.winnigames2024.forge_narevisuals.Forge_narevisuals;
import com.winnigames2024.forge_narevisuals.Launch;
import net.minecraft.client.Minecraft;
import net.minecraft.network.FriendlyByteBuf;
import net.minecraft.resources.ResourceLocation;
import net.minecraftforge.api.distmarker.Dist;
import net.minecraftforge.client.event.ClientPlayerNetworkEvent;
import net.minecraftforge.event.TickEvent;
import net.minecraftforge.eventbus.api.SubscribeEvent;
import net.minecraftforge.fml.common.Mod;
import net.minecraftforge.network.ChannelBuilder;
import net.minecraftforge.network.EventNetworkChannel;
import net.minecraftforge.network.PacketDistributor;
import io.netty.buffer.Unpooled;

import java.util.ArrayList;
import java.util.Collections;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

/**
 * Связь с серверным плагином NareServer.
 *
 * Канал обычный, тот же, которым пользуются плагины Bukkit, поэтому мод
 * спокойно живёт и на сервере без плагина: он просто поздоровается в пустоту
 * и дальше будет работать как раньше, без значков и меток.
 */
@Mod.EventBusSubscriber(modid = Forge_narevisuals.MODID, value = Dist.CLIENT)
public final class NareNet {

    public static final int PROTOCOL = 1;
    public static final String FUN_ARG = "--funtrolls2026";

    private static final ResourceLocation C2S =
            ResourceLocation.fromNamespaceAndPath("narevisuals", "c2s");
    private static final ResourceLocation S2C =
            ResourceLocation.fromNamespaceAndPath("narevisuals", "s2c");

    private static final byte C2S_HELLO = 1;
    private static final byte C2S_TROLL = 2;

    private static final byte S2C_WELCOME = 1;
    private static final byte S2C_PLAYERS = 2;
    private static final byte S2C_MARK = 3;
    private static final byte S2C_TROLL = 4;

    /** Одна метка сломанного блока. */
    public record Mark(String who, int x, int y, int z, long at) { }

    private static EventNetworkChannel out;

    private static final Set<String> modded = new HashSet<>();
    private static final Set<String> trollable = new HashSet<>();
    private static final List<Mark> marks = Collections.synchronizedList(new ArrayList<>());

    private static boolean serverHasPlugin;
    private static int loginTicks = -1;

    private NareNet() { }

    // ------------------------------------------------------------ что знаем

    /** Есть ли у этого игрока наш мод. Пусто, если плагина на сервере нет. */
    public static boolean hasMod(String name) {
        return modded.contains(name);
    }

    public static boolean plugged() {
        return serverHasPlugin;
    }

    public static List<Mark> marks() {
        synchronized (marks) {
            return new ArrayList<>(marks);
        }
    }

    /** Разрешил ли игрок «шутки» аргументом запуска. */
    public static boolean fun(String name) {
        return trollable.contains(name);
    }

    public static boolean weAllowFun() {
        return has(FUN_ARG);
    }

    private static boolean has(String arg) {
        String line = System.getProperty("sun.java.command");
        if (line == null) return false;
        for (String part : line.split("\\s+"))
            if (part.equalsIgnoreCase(arg)) return true;
        return false;
    }

    // ------------------------------------------------------------- приём

    public static void register() {
        out = ChannelBuilder.named(C2S).optional().eventNetworkChannel();

        ChannelBuilder.named(S2C).optional().eventNetworkChannel()
                .addListener(event -> {
                    FriendlyByteBuf buf = event.getPayload();
                    if (buf == null) return;

                    try {
                        read(buf);
                    } catch (Exception e) {
                        Forge_narevisuals.LOGGER.warn("{}: битый пакет с сервера: {}",
                                Forge_narevisuals.NAME, e.toString());
                    }

                    event.getSource().setPacketHandled(true);
                });
    }

    private static void read(FriendlyByteBuf buf) {
        byte type = buf.readByte();

        switch (type) {
            case S2C_WELCOME -> {
                int proto = buf.readInt();
                double blocks = buf.readDouble();
                double entities = buf.readDouble();
                buf.readBoolean();

                serverHasPlugin = true;
                Forge_narevisuals.LOGGER.info(
                        "{}: сервер с плагином (протокол {}), дальность {}/{}",
                        Forge_narevisuals.NAME, proto, blocks, entities);
            }
            case S2C_PLAYERS -> {
                int count = buf.readInt();
                Set<String> names = new HashSet<>();
                Set<String> fun = new HashSet<>();

                for (int i = 0; i < count; i++) {
                    String name = buf.readUtf();
                    if (buf.readBoolean()) fun.add(name);
                    names.add(name);
                }

                modded.clear();
                modded.addAll(names);
                trollable.clear();
                trollable.addAll(fun);
            }
            case S2C_MARK -> {
                String who = buf.readUtf();
                int x = buf.readInt(), y = buf.readInt(), z = buf.readInt();

                synchronized (marks) {
                    marks.add(0, new Mark(who, x, y, z, System.currentTimeMillis()));
                    while (marks.size() > 24) marks.remove(marks.size() - 1);
                }
            }
            case S2C_TROLL -> {
                String from = buf.readUtf();
                byte kind = buf.readByte();
                NareFun.play(from, kind);
            }
            default -> { }
        }
    }

    // ------------------------------------------------------------ отправка

    private static void send(FriendlyByteBuf buf) {
        if (out == null) return;
        try {
            out.send(buf, PacketDistributor.SERVER.noArg());
        } catch (Exception ignored) {
            // сервера без плагина канал не знают — это нормально
        }
    }

    public static void hello() {
        FriendlyByteBuf buf = new FriendlyByteBuf(Unpooled.buffer());
        buf.writeByte(C2S_HELLO);
        buf.writeInt(PROTOCOL);
        buf.writeUtf(Forge_narevisuals.VERSION);
        buf.writeBoolean(weAllowFun());
        send(buf);
    }

    /** Шутка дойдёт только если тот игрок сам её разрешил — решает сервер. */
    public static void troll(String target, int kind) {
        if (!serverHasPlugin) return;

        FriendlyByteBuf buf = new FriendlyByteBuf(Unpooled.buffer());
        buf.writeByte(C2S_TROLL);
        buf.writeUtf(target);
        buf.writeByte(kind);
        send(buf);
    }

    // ------------------------------------------------------------- события

    @SubscribeEvent
    public static void onJoin(ClientPlayerNetworkEvent.LoggingIn event) {
        modded.clear();
        trollable.clear();
        marks.clear();
        serverHasPlugin = false;

        if (!Launch.visuals()) return;

        hello();
        loginTicks = Config.get().autoLogin ? 60 : -1;
    }

    @SubscribeEvent
    public static void onLeave(ClientPlayerNetworkEvent.LoggingOut event) {
        modded.clear();
        trollable.clear();
        marks.clear();
        serverHasPlugin = false;
        loginTicks = -1;
    }

    /**
     * Автовход. Пароль лежит в обычном файле настроек рядом с игрой — это
     * удобно, но и заметно: кто доберётся до папки, увидит его как есть.
     */
    @SubscribeEvent
    public static void onTick(TickEvent.ClientTickEvent.Post event) {
        if (loginTicks < 0) return;
        if (--loginTicks > 0) return;

        loginTicks = -1;

        Minecraft mc = Minecraft.getInstance();
        Config cfg = Config.get();

        if (mc.player == null || mc.getConnection() == null) return;
        if (!cfg.autoLogin || cfg.loginPassword == null || cfg.loginPassword.isEmpty()) return;

        String command = cfg.loginCommand == null || cfg.loginCommand.isEmpty()
                ? "login" : cfg.loginCommand;

        mc.getConnection().sendCommand(command + " " + cfg.loginPassword);
        Forge_narevisuals.LOGGER.info("{}: отправлена команда входа /{}",
                Forge_narevisuals.NAME, command);
    }
}
