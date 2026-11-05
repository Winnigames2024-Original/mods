package com.winnigames2024.nareserver;

import net.kyori.adventure.text.Component;
import org.bukkit.Bukkit;
import org.bukkit.entity.Player;
import org.bukkit.plugin.messaging.PluginMessageListener;
import org.jetbrains.annotations.NotNull;

import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.io.DataInputStream;
import java.io.DataOutputStream;
import java.io.IOException;
import java.util.Collection;

/**
 * Обмен с клиентским модом обычными plugin-сообщениями: клиент шлёт в
 * narevisuals:c2s, сервер отвечает в narevisuals:s2c. Формат простой —
 * первый байт тип, дальше поля.
 */
public final class NareChannel implements PluginMessageListener {

    public static final String C2S = "narevisuals:c2s";
    public static final String S2C = "narevisuals:s2c";

    public static final int PROTOCOL = 1;

    // от клиента
    public static final byte C2S_HELLO = 1;
    public static final byte C2S_TROLL = 2;
    public static final byte C2S_BREAK = 3;

    // к клиенту
    public static final byte S2C_WELCOME = 1;
    public static final byte S2C_PLAYERS = 2;
    public static final byte S2C_MARK = 3;
    public static final byte S2C_TROLL = 4;

    private final NareServer plugin;

    public NareChannel(NareServer plugin) {
        this.plugin = plugin;
    }

    public void register() {
        Bukkit.getMessenger().registerOutgoingPluginChannel(plugin, S2C);
        Bukkit.getMessenger().registerIncomingPluginChannel(plugin, C2S, this);
    }

    @Override
    public void onPluginMessageReceived(@NotNull String channel, @NotNull Player player, byte[] message) {
        if (!C2S.equals(channel)) return;

        try (DataInputStream in = new DataInputStream(new ByteArrayInputStream(message))) {
            byte type = in.readByte();

            switch (type) {
                case C2S_HELLO -> {
                    int proto = in.readInt();
                    String version = in.readUTF();
                    boolean funTrolls = in.readBoolean();

                    if (proto != PROTOCOL) {
                        ((net.kyori.adventure.audience.Audience) player).sendMessage(Component.text(
                                "NareVisuals: версия мода и плагина разные ("
                                + proto + " против " + PROTOCOL + ")."));
                        return;
                    }

                    Bukkit.getScheduler().runTask(plugin,
                            () -> plugin.onHello(player, version, funTrolls));
                }
                case C2S_BREAK -> {
                    int count = in.readInt();
                    if (count < 0 || count > 1024) return;

                    int[][] blocks = new int[count][3];
                    for (int i = 0; i < count; i++) {
                        blocks[i][0] = in.readInt();
                        blocks[i][1] = in.readInt();
                        blocks[i][2] = in.readInt();
                    }

                    Bukkit.getScheduler().runTask(plugin,
                            () -> plugin.onBreakArea(player, blocks));
                }
                case C2S_TROLL -> {
                    String target = in.readUTF();
                    byte kind = in.readByte();
                    Bukkit.getScheduler().runTask(plugin,
                            () -> plugin.onTroll(player, target, kind));
                }
                default -> { }
            }
        } catch (IOException e) {
            plugin.getLogger().warning("Битый пакет от " + player.getName() + ": " + e.getMessage());
        }
    }

    // ------------------------------------------------------------- отправка

    public void sendWelcome(Player player, double blocks, double entities, boolean trolls) {
        send(player, out -> {
            out.writeByte(S2C_WELCOME);
            out.writeInt(PROTOCOL);
            out.writeDouble(blocks);
            out.writeDouble(entities);
            out.writeBoolean(trolls);
        });
    }

    public void sendPlayers(Player player, Collection<NareServer.Guest> guests) {
        send(player, out -> {
            out.writeByte(S2C_PLAYERS);
            out.writeInt(guests.size());
            for (NareServer.Guest guest : guests) {
                out.writeUTF(guest.name());
                out.writeBoolean(guest.funTrolls());
            }
        });
    }

    public void sendMark(Player player, String who, int x, int y, int z) {
        send(player, out -> {
            out.writeByte(S2C_MARK);
            out.writeUTF(who);
            out.writeInt(x);
            out.writeInt(y);
            out.writeInt(z);
        });
    }

    public void sendTroll(Player player, String from, byte kind) {
        send(player, out -> {
            out.writeByte(S2C_TROLL);
            out.writeUTF(from);
            out.writeByte(kind);
        });
    }

    private interface Body {
        void write(DataOutputStream out) throws IOException;
    }

    private void send(Player player, Body body) {
        try (ByteArrayOutputStream bytes = new ByteArrayOutputStream();
             DataOutputStream out = new DataOutputStream(bytes)) {

            body.write(out);
            out.flush();
            player.sendPluginMessage(plugin, S2C, bytes.toByteArray());

        } catch (IOException e) {
            plugin.getLogger().warning("Пакет не отправлен: " + e.getMessage());
        }
    }
}
