package com.winnigames2024.nareserver;

import org.bukkit.Bukkit;
import org.bukkit.entity.Player;

import java.net.DatagramPacket;
import java.net.DatagramSocket;
import java.net.InetAddress;
import java.net.InetSocketAddress;
import java.nio.charset.StandardCharsets;
import java.security.SecureRandom;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;

/**
 * Отдельный UDP-порт для голоса.
 *
 * Зачем он нужен, если звук и так доходит по игровому соединению: игровое
 * соединение — это TCP. Потерялся один пакет — всё, что едет следом, ждёт,
 * пока его перешлют. Для файлов это правильно, для голоса — нет: лучше
 * потерять кусочек звука, чем услышать его с опозданием на полсекунды.
 * Поэтому UDP, где потерянный кадр просто пропадает.
 *
 * Кто есть кто, разбираем по токену: при входе игрок получает по обычному
 * каналу случайное 64-битное число и кладёт его в каждый свой пакет. Адрес
 * запоминаем с первого пакета — так работает и за NAT, и за роутером.
 *
 * Если у игрока UDP не прошёл (закрыт у провайдера, режет файрвол), он
 * продолжает слать звук по игровому соединению, и мы отвечаем ему так же.
 */
public final class NareVoicePort {

    /** Первый байт пакета — чтобы случайный мусор на порту не разбирать. */
    private static final byte MAGIC = (byte) 0x4E;      // 'N'

    private static final byte C2S_AUDIO = 1;
    private static final byte C2S_PING = 2;
    private static final byte S2C_AUDIO = 1;

    private static final int MAX_PACKET = 2048;

    private final NareServer plugin;
    private final int port;

    private DatagramSocket socket;
    private Thread reader;
    private volatile boolean running;

    private final SecureRandom random = new SecureRandom();

    private final Map<Long, UUID> byToken = new ConcurrentHashMap<>();
    private final Map<UUID, Long> tokens = new ConcurrentHashMap<>();
    private final Map<UUID, InetSocketAddress> where = new ConcurrentHashMap<>();
    private final Map<UUID, Long> seen = new ConcurrentHashMap<>();

    public NareVoicePort(NareServer plugin, int port) {
        this.plugin = plugin;
        this.port = port;
    }

    // ------------------------------------------------------------- запуск

    /** Открывает порт. false — не вышло, работаем по-старому. */
    public boolean open(String bind) {
        try {
            socket = (bind == null || bind.isBlank())
                    ? new DatagramSocket(port)
                    : new DatagramSocket(port, InetAddress.getByName(bind));

            socket.setReceiveBufferSize(MAX_PACKET * 64);
            running = true;

            reader = new Thread(this::loop, "NareVoice-udp");
            reader.setDaemon(true);
            reader.start();

            plugin.getLogger().info("NareVisuals: голосовой порт " + port + "/udp открыт");
            return true;
        }
        catch (Exception e) {
            plugin.getLogger().warning("NareVisuals: порт " + port
                    + "/udp не открылся (" + e.getMessage()
                    + "). Голос пойдёт по игровому соединению.");
            close();
            return false;
        }
    }

    public void close() {
        running = false;

        if (socket != null) { try { socket.close(); } catch (Exception ignored) { } }
        socket = null;
        reader = null;

        byToken.clear();
        tokens.clear();
        where.clear();
        seen.clear();
    }

    public boolean working() { return running && socket != null; }

    public int port() { return port; }

    // ------------------------------------------------------------- токены

    /** Выдаёт игроку его число. Зовётся при входе, уходит в WELCOME. */
    public long tokenFor(UUID id) {
        Long already = tokens.get(id);
        if (already != null) return already;

        long token;
        do { token = random.nextLong(); }
        while (token == 0L || byToken.containsKey(token));

        tokens.put(id, token);
        byToken.put(token, id);
        return token;
    }

    public void forget(UUID id) {
        Long token = tokens.remove(id);
        if (token != null) byToken.remove(token);

        where.remove(id);
        seen.remove(id);
    }

    /** Дошёл ли до нас хоть один пакет от этого игрока за последние 10 секунд. */
    public boolean reachable(UUID id) {
        Long at = seen.get(id);
        return at != null && System.currentTimeMillis() - at < 10_000L;
    }

    // -------------------------------------------------------------- приём

    private void loop() {
        byte[] buffer = new byte[MAX_PACKET];

        while (running) {
            var packet = new DatagramPacket(buffer, buffer.length);

            try { socket.receive(packet); }
            catch (Exception e) {
                if (running) plugin.getLogger().warning("NareVisuals: голосовой порт: " + e.getMessage());
                continue;
            }

            try { take(packet); }
            catch (Exception ignored) { /* кривой пакет — просто выбрасываем */ }
        }
    }

    private void take(DatagramPacket packet) {
        byte[] data = packet.getData();
        int length = packet.getLength();

        if (length < 10) return;
        if (data[0] != MAGIC) return;

        byte type = data[1];

        long token = 0;
        for (int i = 0; i < 8; i++) token = (token << 8) | (data[2 + i] & 0xFFL);

        UUID id = byToken.get(token);
        if (id == null) return;

        // адрес запоминаем с любого пакета — и с пинга тоже
        where.put(id, new InetSocketAddress(packet.getAddress(), packet.getPort()));
        seen.put(id, System.currentTimeMillis());

        if (type == C2S_PING) return;
        if (type != C2S_AUDIO) return;
        if (length < 12) return;

        int size = ((data[10] & 0xFF) << 8) | (data[11] & 0xFF);
        if (size <= 0 || size > length - 12) return;

        byte[] audio = new byte[size];
        System.arraycopy(data, 12, audio, 0, size);

        Player from = Bukkit.getPlayer(id);
        if (from == null) return;

        plugin.onVoice(from, audio);
    }

    // ------------------------------------------------------------ отправка

    /**
     * Шлёт кадр слушателю по UDP. false — адреса ещё нет, пусть уходит
     * обычным путём.
     */
    public boolean send(UUID to, String who, double x, double y, double z, byte[] audio) {
        if (!working()) return false;

        InetSocketAddress address = where.get(to);
        if (address == null) return false;

        try {
            byte[] name = who.getBytes(StandardCharsets.UTF_8);
            if (name.length > 255) return false;

            int size = 2 + 1 + name.length + 24 + 2 + audio.length;
            byte[] out = new byte[size];

            int at = 0;
            out[at++] = MAGIC;
            out[at++] = S2C_AUDIO;
            out[at++] = (byte) name.length;

            System.arraycopy(name, 0, out, at, name.length);
            at += name.length;

            at = putDouble(out, at, x);
            at = putDouble(out, at, y);
            at = putDouble(out, at, z);

            out[at++] = (byte) ((audio.length >> 8) & 0xFF);
            out[at++] = (byte) (audio.length & 0xFF);

            System.arraycopy(audio, 0, out, at, audio.length);

            socket.send(new DatagramPacket(out, out.length, address));
            return true;
        }
        catch (Exception e) {
            where.remove(to);          // адрес протух — вернёмся к обычному пути
            return false;
        }
    }

    private static int putDouble(byte[] out, int at, double value) {
        long bits = Double.doubleToLongBits(value);
        for (int i = 7; i >= 0; i--) out[at++] = (byte) ((bits >> (i * 8)) & 0xFF);
        return at;
    }
}
