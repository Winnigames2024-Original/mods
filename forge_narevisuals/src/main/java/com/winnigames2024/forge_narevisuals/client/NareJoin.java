package com.winnigames2024.forge_narevisuals.client;

import com.winnigames2024.forge_narevisuals.Config;
import com.winnigames2024.forge_narevisuals.Forge_narevisuals;
import com.winnigames2024.forge_narevisuals.Launch;
import net.minecraft.client.Minecraft;
import net.minecraftforge.api.distmarker.Dist;
import net.minecraftforge.client.event.ClientPlayerNetworkEvent;
import net.minecraftforge.event.TickEvent;
import net.minecraftforge.eventbus.api.SubscribeEvent;
import net.minecraftforge.fml.common.Mod;

import java.util.ArrayList;
import java.util.List;

/**
 * Команды, которые выполняются сами при входе в мир или на сервер.
 *
 * Берём их из двух мест: из настроек мода и из аргумента запуска
 * <c>--onjoin "команда; ещё одна"</c>, который подставляет лаунчер. Так одно
 * и то же можно задать и внутри игры, и снаружи.
 *
 * Шлём не сразу: сервер в первые мгновения после входа ещё занят, и команда
 * может потеряться. Ждём пару секунд, а между командами делаем паузу, иначе
 * антифлуд посчитает это спамом.
 */
@Mod.EventBusSubscriber(modid = Forge_narevisuals.MODID, value = Dist.CLIENT)
public final class NareJoin {

    private static final int DELAY = 50;        // тиков до первой команды
    private static final int GAP = 10;          // тиков между командами

    private static final List<String> queue = new ArrayList<>();
    private static int wait = -1;

    private NareJoin() { }

    @SubscribeEvent
    public static void onJoin(ClientPlayerNetworkEvent.LoggingIn event) {
        if (!Launch.visuals()) return;

        queue.clear();

        for (String line : Config.get().joinCommands) add(line);
        for (String line : Launch.value("--onjoin").split(";")) add(line);

        wait = queue.isEmpty() ? -1 : DELAY;

        if (!queue.isEmpty())
            Forge_narevisuals.LOGGER.info("{}: при входе выполню {} команд(ы)",
                    Forge_narevisuals.NAME, queue.size());
    }

    @SubscribeEvent
    public static void onLeave(ClientPlayerNetworkEvent.LoggingOut event) {
        queue.clear();
        wait = -1;
    }

    private static void add(String line) {
        if (line == null) return;

        String text = line.trim();
        if (text.isEmpty()) return;
        if (text.length() > 256) text = text.substring(0, 256);

        queue.add(text);
    }

    @SubscribeEvent
    public static void onTick(TickEvent.ClientTickEvent event) {
        if (event.phase != TickEvent.Phase.END) return;
        if (wait < 0 || queue.isEmpty()) return;

        if (wait-- > 0) return;

        Minecraft mc = Minecraft.getInstance();
        if (mc.player == null || mc.getConnection() == null) { wait = -1; queue.clear(); return; }

        String line = queue.remove(0);
        wait = queue.isEmpty() ? -1 : GAP;

        try {
            if (line.startsWith("/")) mc.getConnection().sendCommand(line.substring(1));
            else mc.getConnection().sendChat(line);

            Forge_narevisuals.LOGGER.info("{}: отправил при входе: {}",
                    Forge_narevisuals.NAME, line);
        }
        catch (Exception e) {
            Forge_narevisuals.LOGGER.warn("{}: команда при входе не ушла: {}",
                    Forge_narevisuals.NAME, e.getMessage());
        }
    }
}
