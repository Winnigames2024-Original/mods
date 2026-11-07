package com.winnigames2024.forge_narevisuals.client;

import com.winnigames2024.forge_narevisuals.Config;
import com.winnigames2024.forge_narevisuals.Forge_narevisuals;
import com.winnigames2024.forge_narevisuals.Launch;
import net.minecraft.client.Minecraft;
import net.minecraft.world.entity.Entity;
import net.minecraft.world.entity.item.PrimedTnt;
import net.minecraftforge.api.distmarker.Dist;
import net.minecraftforge.event.TickEvent;
import net.minecraftforge.eventbus.api.SubscribeEvent;
import net.minecraftforge.fml.common.Mod;

/**
 * Спасение от взрыва, который кладёт игру.
 *
 * Клиент сам по себе тут бессилен: прятать ТНТ он умеет (это делает FPS+),
 * но пока сервер считает сотню зарядов, всё равно будет плохо. Поэтому мод
 * просит помощи у плагина: тот уводит игрока на спавн и убирает источник.
 *
 * Срабатывает только когда совпало два условия: рядом правда много зажжённой
 * ТНТ и кадры уже еле идут. Одного мало — мало ли кто фейерверк запустил.
 */
@Mod.EventBusSubscriber(modid = Forge_narevisuals.MODID, value = Dist.CLIENT)
public final class NarePanic {

    private static final int RADIUS = 48;
    private static final long COOLDOWN = 30_000L;

    private static long lastAsk;
    private static int slowTicks;

    private NarePanic() { }

    @SubscribeEvent
    public static void onTick(TickEvent.ClientTickEvent event) {
        if (event.phase != TickEvent.Phase.END) return;
        if (!Launch.visuals()) return;

        Config cfg = Config.get();
        if (!cfg.panicRescue) return;

        Minecraft mc = Minecraft.getInstance();
        if (mc.level == null || mc.player == null) return;
        if (!NareNet.plugged()) return;

        // кадры еле идут — считаем, сколько это длится
        if (NareHud.measuredFps > 0 && NareHud.measuredFps < cfg.panicFps) slowTicks++;
        else slowTicks = Math.max(0, slowTicks - 2);

        if (slowTicks < 40) return;                       // около двух секунд подряд

        int tnt = countTnt(mc);
        if (tnt < cfg.panicTnt) return;

        long now = System.currentTimeMillis();
        if (now - lastAsk < COOLDOWN) return;
        lastAsk = now;
        slowTicks = 0;

        Forge_narevisuals.LOGGER.warn("{}: рядом {} зажжённых ТНТ и {} кадров — прошу увести",
                Forge_narevisuals.NAME, tnt, NareHud.measuredFps);

        NareNet.sendPanic(tnt);

        if (mc.gui != null)
            mc.gui.setOverlayMessage(net.minecraft.network.chat.Component.literal(
                    "Слишком много взрывчатки рядом — ухожу на спавн"), false);
    }

    private static int countTnt(Minecraft mc) {
        int count = 0;

        var box = mc.player.getBoundingBox().inflate(RADIUS);
        for (Entity entity : mc.level.getEntities(mc.player, box)) {
            if (entity instanceof PrimedTnt) count++;
            if (count > 512) break;
        }

        return count;
    }
}
