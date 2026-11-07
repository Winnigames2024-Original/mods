package com.winnigames2024.forge_narevisuals.client;

import com.winnigames2024.forge_narevisuals.Config;
import com.winnigames2024.forge_narevisuals.Forge_narevisuals;
import com.winnigames2024.forge_narevisuals.Launch;
import net.minecraftforge.api.distmarker.Dist;
import net.minecraftforge.client.event.ClientPlayerNetworkEvent;
import net.minecraftforge.event.TickEvent;
import net.minecraftforge.eventbus.api.SubscribeEvent;
import net.minecraftforge.fml.common.Mod;

import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.ArrayList;
import java.util.List;

/**
 * Настройки мода, которые живут на сервере.
 *
 * Плагин держит их в своей папке, по игроку:
 * plugins/NareServer/mod/<ник>/config.json и так далее. Человек заходит с
 * другого компьютера — и всё уже на месте.
 *
 * Отправляем не каждое нажатие: после изменения ждём несколько секунд,
 * иначе ползунок громкости отправил бы сорок файлов подряд.
 */
@Mod.EventBusSubscriber(modid = Forge_narevisuals.MODID, value = Dist.CLIENT)
public final class NareCloud {

    private static final int DELAY = 100;       // тиков тишины перед отправкой

    private static final List<String> onServer = new ArrayList<>();

    private static int wait = -1;
    private static long lastSize;

    private NareCloud() { }

    /** Что лежит у нас на сервере — присылает плагин. */
    public static void listed(String names) {
        synchronized (onServer) {
            onServer.clear();
            if (names != null && !names.isBlank())
                for (String name : names.split(","))
                    if (!name.isBlank()) onServer.add(name.trim());
        }

        Forge_narevisuals.LOGGER.info("{}: на сервере наших файлов: {}",
                Forge_narevisuals.NAME, onServer.size());
    }

    public static List<String> onServer() {
        synchronized (onServer) { return new ArrayList<>(onServer); }
    }

    /** Попросить плагин прислать список. */
    public static void refresh() {
        NareNet.sendFile(2, "config.json", null);
    }

    /** Отправить настройки прямо сейчас. */
    public static void push() {
        try {
            var path = Config.file();
            if (path == null || !Files.isRegularFile(path)) return;

            byte[] data = Files.readAllBytes(path);
            if (data.length == 0 || data.length > 512 * 1024) return;

            NareNet.sendFile(0, "config.json", data);
            lastSize = data.length;

            Forge_narevisuals.LOGGER.info("{}: настройки отправлены на сервер ({} байт)",
                    Forge_narevisuals.NAME, data.length);
        }
        catch (Exception e) {
            Forge_narevisuals.LOGGER.warn("{}: настройки не отправились: {}",
                    Forge_narevisuals.NAME, e.getMessage());
        }
    }

    /** Убрать свои файлы с сервера. */
    public static void drop(String name) {
        NareNet.sendFile(1, name == null || name.isBlank() ? "config.json" : name, null);
    }

    /** Настройки поменялись — отправим, когда человек перестанет их крутить. */
    public static void touched() {
        if (!Config.get().syncToServer) return;
        wait = DELAY;
    }

    @SubscribeEvent
    public static void onJoin(ClientPlayerNetworkEvent.LoggingIn event) {
        if (!Launch.visuals()) return;
        wait = DELAY;                           // поздороваемся и отправим
    }

    @SubscribeEvent
    public static void onTick(TickEvent.ClientTickEvent event) {
        if (event.phase != TickEvent.Phase.END) return;
        if (!Launch.visuals()) return;
        if (wait < 0) return;
        if (wait-- > 0) return;

        wait = -1;

        if (!NareNet.plugged()) return;
        if (!Config.get().syncToServer) return;

        refresh();
        push();
    }

    public static String state() {
        if (!Config.get().syncToServer) return "выключено";
        if (!NareNet.plugged()) return "нужен плагин на сервере";

        int count = onServer().size();
        return count == 0 ? "пока ничего не отправлено"
                : "на сервере " + count + " файл(ов), " + lastSize + " байт";
    }

    public static String utf8(byte[] data) {
        return new String(data, StandardCharsets.UTF_8);
    }
}
