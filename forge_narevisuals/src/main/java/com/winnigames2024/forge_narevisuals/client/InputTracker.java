package com.winnigames2024.forge_narevisuals.client;

import com.winnigames2024.forge_narevisuals.Config;
import com.winnigames2024.forge_narevisuals.Forge_narevisuals;
import com.winnigames2024.forge_narevisuals.Launch;
import net.minecraft.client.Minecraft;
import net.minecraftforge.api.distmarker.Dist;
import net.minecraftforge.client.event.InputEvent;
import net.minecraftforge.event.entity.player.AttackEntityEvent;
import net.minecraftforge.eventbus.api.SubscribeEvent;
import net.minecraftforge.fml.common.Mod;

import java.util.ArrayDeque;
import java.util.Deque;

/** Считает клики в секунду, ловит удары и горячие клавиши мода. */
@Mod.EventBusSubscriber(modid = Forge_narevisuals.MODID, value = Dist.CLIENT)
public final class InputTracker {

    private static final Deque<Long> LEFT = new ArrayDeque<>();
    private static final Deque<Long> RIGHT = new ArrayDeque<>();

    /** Момент последнего попадания по существу — для маркера попадания. */
    public static long lastHitAt;

    private InputTracker() { }

    @SubscribeEvent
    public static void onMouse(InputEvent.MouseButton.Pre event) {
        if (event.getAction() != 1) return;          // 1 = нажатие
        if (Minecraft.getInstance().screen != null) return;

        long now = System.currentTimeMillis();
        if (event.getButton() == 0) LEFT.addLast(now);
        else if (event.getButton() == 1) RIGHT.addLast(now);
    }

    /** Колесо меняет скорость свободной камеры вместо выбора предмета. */
    @SubscribeEvent
    public static void onScroll(InputEvent.MouseScrollingEvent event) {
        if (!Launch.visuals()) return;
        if (Minecraft.getInstance().screen != null) return;
        if (Freecam.scrolled(event.getDeltaY())) event.setCanceled(true);
    }

    /**
     * Голос слушаем отдельно: нужны и нажатие, и отпускание, а общий
     * обработчик ниже реагирует только на нажатие.
     */
    @SubscribeEvent
    public static void onVoiceKey(InputEvent.Key event) {
        if (event.getAction() == 2) return;          // авто-повтор не нужен
        if (!Launch.visuals()) return;

        Minecraft mc = Minecraft.getInstance();
        if (mc.screen != null) return;

        if (event.getKey() != NareKeys.VOICE.getKey().getValue()) return;

        NareVoice.hold(event.getAction() == 1);
    }

    @SubscribeEvent
    public static void onKey(InputEvent.Key event) {
        if (event.getAction() != 1) return;
        Minecraft mc = Minecraft.getInstance();
        if (mc.screen != null) return;
        if (!Launch.visuals()) return;

        Config cfg = Config.get();

        if (NareKeys.OPEN_SETTINGS.isDown()) {
            mc.setScreen(NareMenus.open(null));
        } else if (NareKeys.TOGGLE_HUD.isDown()) {
            cfg.hudEnabled = !cfg.hudEnabled;
            Config.save();
        } else if (NareKeys.TOGGLE_ESP.isDown()) {
            cfg.playerEsp = !cfg.playerEsp;
            Config.save();
            note(mc, "Подсветка игроков: " + onOff(cfg.playerEsp));
        } else if (NareKeys.TOGGLE_BRIGHT.isDown()) {
            boolean on = !(cfg.fullbright && cfg.nightVision);
            cfg.fullbright = on;
            cfg.nightVision = on;
            Config.save();
            note(mc, "Яркость: " + onOff(on));
        } else if (NareKeys.TOGGLE_FREECAM.isDown()) {
            Freecam.toggle();
        } else if (NareKeys.TOGGLE_SKY.isDown()) {
            cfg.customSky = !cfg.customSky;
            Config.save();
            note(mc, "Своё небо: " + onOff(cfg.customSky));
        } else if (NareKeys.MOVE_HUD.isDown()) {
            mc.setScreen(new HudEditScreen(null));
        } else if (NareKeys.TOGGLE_NEAR.isDown()) {
            HudModule near = NareHud.byId("players");
            if (near != null) {
                near.setEnabled(!near.isEnabled());
                Config.save();
                note(mc, "Список «кто рядом»: " + onOff(near.isEnabled()));
            }
        }
    }

    private static String onOff(boolean on) {
        return on ? "включена" : "выключена";
    }

    private static void note(Minecraft mc, String text) {
        if (mc.gui != null)
            mc.gui.setOverlayMessage(net.minecraft.network.chat.Component.literal(text), false);
    }

    @SubscribeEvent
    public static void onAttack(AttackEntityEvent event) {
        if (event.getEntity() == Minecraft.getInstance().player)
            lastHitAt = System.currentTimeMillis();
    }

    public static int leftCps() {
        return count(LEFT);
    }

    public static int rightCps() {
        return count(RIGHT);
    }

    private static int count(Deque<Long> clicks) {
        long edge = System.currentTimeMillis() - 1000L;
        while (!clicks.isEmpty() && clicks.peekFirst() < edge) clicks.removeFirst();
        return clicks.size();
    }
}
