package com.winnigames2024.forge_narevisuals.client;

import com.winnigames2024.forge_narevisuals.Forge_narevisuals;
import net.minecraft.client.Minecraft;

import java.net.DatagramPacket;
import java.net.DatagramSocket;
import java.net.InetSocketAddress;
import java.nio.charset.StandardCharsets;

/**
 * Голосовой порт со стороны клиента.
 *
 * Игровое соединение — TCP: потерялся пакет, и всё, что едет следом, ждёт
 * его повтора. Для голоса это хуже, чем просто потерять кусочек звука,
 * поэтому сервер может открыть отдельный UDP-порт, и тогда звук ходит через
 * него. Если порт не отвечает, мы молча возвращаемся на игровое соединение —
 * человек вообще ничего не замечает.
 *
 * Представляемся числом, которое сервер прислал при входе: в UDP нет
 * соединения, и иначе сервер не понял бы, кто говорит.
 */
public final class NareUdp {

    private static final byte MAGIC = (byte) 0x4E;      // 'N'

    private static final byte C2S_AUDIO = 1;
    private static final byte C2S_PING = 2;
    private static final byte S2C_AUDIO = 1;

    private static final int MAX_PACKET = 2048;

    private static DatagramSocket socket;
    private static Thread reader;
    private static Thread pinger;

    private static volatile boolean running;
    private static volatile boolean heard;          // сервер нам хоть раз ответил
    private static volatile long token;

    private static InetSocketAddress server;

    private NareUdp() { }

    // ------------------------------------------------------------- запуск

    /**
     * Поднимает голосовой порт. host берём тот, к которому подключена игра:
     * адрес сервера в списке может быть доменным, и это нормально.
     */
    public static synchronized void start(int port, long serverToken) {
        stop();

        if (port <= 0 || serverToken == 0L) return;

        String host = host();
        if (host == null) return;

        try {
            server = new InetSocketAddress(host, port);
            if (server.isUnresolved()) {
                Forge_narevisuals.LOGGER.warn("{}: голосовой порт: имя {} не разрешилось",
                        Forge_narevisuals.NAME, host);
                server = null;
                return;
            }

            socket = new DatagramSocket();
            socket.setSoTimeout(1000);

            token = serverToken;
            running = true;
            heard = false;

            reader = new Thread(NareUdp::loop, "NareVoice-udp-in");
            reader.setDaemon(true);
            reader.start();

            pinger = new Thread(NareUdp::keepAlive, "NareVoice-udp-ping");
            pinger.setDaemon(true);
            pinger.start();

            Forge_narevisuals.LOGGER.info("{}: голос пойдёт на {}:{}",
                    Forge_narevisuals.NAME, host, port);
        }
        catch (Exception e) {
            Forge_narevisuals.LOGGER.warn("{}: голосовой порт не открылся: {}",
                    Forge_narevisuals.NAME, e.getMessage());
            stop();
        }
    }

    public static synchronized void stop() {
        running = false;
        heard = false;
        token = 0L;
        server = null;

        if (socket != null) { try { socket.close(); } catch (Exception ignored) { } }
        socket = null;
        reader = null;
        pinger = null;
    }

    /** Готов ли порт принимать звук. */
    public static boolean ready() {
        return running && socket != null && server != null;
    }

    /** Долетело ли до нас хоть что-то с голосового порта. */
    public static boolean alive() { return heard; }

    public static int port() {
        return server == null ? 0 : server.getPort();
    }

    /** Адрес, к которому подключена игра, без порта. */
    private static String host() {
        try {
            Minecraft mc = Minecraft.getInstance();
            var data = mc.getCurrentServer();
            if (data == null || data.ip == null || data.ip.isBlank()) return null;

            String ip = data.ip.trim();

            // [::1]:25565
            if (ip.startsWith("[")) {
                int end = ip.indexOf(']');
                return end > 0 ? ip.substring(1, end) : null;
            }

            int colon = ip.lastIndexOf(':');
            return colon > 0 ? ip.substring(0, colon) : ip;
        }
        catch (Exception e) { return null; }
    }

    // ------------------------------------------------------------ отправка

    /** Шлёт кадр. false — не вышло, пусть уходит по игровому соединению. */
    public static boolean sendVoice(byte[] audio) {
        if (!ready() || audio == null || audio.length == 0) return false;

        try {
            byte[] out = new byte[12 + audio.length];

            out[0] = MAGIC;
            out[1] = C2S_AUDIO;
            putToken(out);

            out[10] = (byte) ((audio.length >> 8) & 0xFF);
            out[11] = (byte) (audio.length & 0xFF);
            System.arraycopy(audio, 0, out, 12, audio.length);

            socket.send(new DatagramPacket(out, out.length, server));
            return true;
        }
        catch (Exception e) { return false; }
    }

    private static void putToken(byte[] out) {
        long value = token;
        for (int i = 0; i < 8; i++) out[2 + i] = (byte) ((value >> ((7 - i) * 8)) & 0xFF);
    }

    /**
     * Пинг раз в две секунды: сервер запоминает наш адрес, а роутер не
     * закрывает дырку, пока мы молчим.
     */
    private static void keepAlive() {
        byte[] ping = new byte[10];
        ping[0] = MAGIC;
        ping[1] = C2S_PING;

        while (running) {
            try {
                putToken(ping);
                socket.send(new DatagramPacket(ping, ping.length, server));
            }
            catch (Exception ignored) { }

            try { Thread.sleep(2000L); }
            catch (InterruptedException stop) { return; }
        }
    }

    // -------------------------------------------------------------- приём

    private static void loop() {
        byte[] buffer = new byte[MAX_PACKET];

        while (running) {
            var packet = new DatagramPacket(buffer, buffer.length);

            try { socket.receive(packet); }
            catch (java.net.SocketTimeoutException quiet) { continue; }
            catch (Exception e) {
                if (running) Forge_narevisuals.LOGGER.warn("{}: голосовой порт: {}",
                        Forge_narevisuals.NAME, e.getMessage());
                continue;
            }

            try { take(packet); }
            catch (Exception ignored) { }
        }
    }

    private static void take(DatagramPacket packet) {
        byte[] data = packet.getData();
        int length = packet.getLength();

        if (length < 3) return;
        if (data[0] != MAGIC) return;
        if (data[1] != S2C_AUDIO) return;

        heard = true;

        int nameLength = data[2] & 0xFF;
        int at = 3;
        if (length < at + nameLength + 26) return;

        String who = new String(data, at, nameLength, StandardCharsets.UTF_8);
        at += nameLength;

        double x = getDouble(data, at); at += 8;
        double y = getDouble(data, at); at += 8;
        double z = getDouble(data, at); at += 8;

        int size = ((data[at] & 0xFF) << 8) | (data[at + 1] & 0xFF);
        at += 2;

        if (size <= 0 || at + size > length) return;

        byte[] audio = new byte[size];
        System.arraycopy(data, at, audio, 0, size);

        NareVoice.incoming(who, x, y, z, audio);
    }

    private static double getDouble(byte[] data, int at) {
        long bits = 0;
        for (int i = 0; i < 8; i++) bits = (bits << 8) | (data[at + i] & 0xFFL);
        return Double.longBitsToDouble(bits);
    }
}
