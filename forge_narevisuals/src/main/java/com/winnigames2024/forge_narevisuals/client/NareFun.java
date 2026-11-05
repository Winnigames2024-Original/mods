package com.winnigames2024.forge_narevisuals.client;

import com.winnigames2024.forge_narevisuals.Forge_narevisuals;
import net.minecraft.client.Minecraft;
import net.minecraft.network.chat.Component;
import net.minecraft.sounds.SoundEvents;
import net.minecraft.sounds.SoundSource;
import net.minecraftforge.api.distmarker.Dist;
import net.minecraftforge.client.event.ViewportEvent;
import net.minecraftforge.eventbus.api.SubscribeEvent;
import net.minecraftforge.fml.common.Mod;

/**
 * Безобидные шутки между своими.
 *
 * Работают только в одну сторону и только по согласию: дойти до вас они
 * могут, лишь если вы сами запустили игру с аргументом --funtrolls2026,
 * и проверяет это сервер, а не отправитель. Ничего, кроме звука, надписи
 * и лёгкого покачивания камеры, они не делают — ни управление не отнимают,
 * ни игру не ломают.
 */
@Mod.EventBusSubscriber(modid = Forge_narevisuals.MODID, value = Dist.CLIENT)
public final class NareFun {

    public static final int SOUND = 0;
    public static final int SHAKE = 1;
    public static final int FLASH = 2;

    public static final String[] TITLES = { "звук", "покачать камеру", "вспышка" };

    private static long shakeUntil;
    private static long flashUntil;

    private NareFun() { }

    public static boolean flashing() {
        return System.currentTimeMillis() < flashUntil;
    }

    /** Пришла шутка от другого игрока. */
    public static void play(String from, int kind) {
        Minecraft mc = Minecraft.getInstance();
        if (mc.player == null) return;

        // сервер и так проверяет согласие, но пусть и у нас будет свой замок
        if (!NareNet.weAllowFun()) return;

        mc.execute(() -> {
            long now = System.currentTimeMillis();

            switch (kind) {
                case SHAKE -> shakeUntil = now + 1500;
                case FLASH -> flashUntil = now + 400;
                default -> mc.player.playSound(SoundEvents.NOTE_BLOCK_PLING.value(), 1f, 1.4f);
            }

            mc.gui.setOverlayMessage(
                    Component.literal(from + " шутит: " + name(kind)), false);
        });
    }

    private static String name(int kind) {
        return kind >= 0 && kind < TITLES.length ? TITLES[kind] : "что-то";
    }

    /** Покачивание камеры — короткое и слабое, просто чтобы заметили. */
    @SubscribeEvent
    public static void onCamera(ViewportEvent.ComputeCameraAngles event) {
        long left = shakeUntil - System.currentTimeMillis();
        if (left <= 0) return;

        double t = System.currentTimeMillis() / 90.0;
        float power = (float) Math.min(1.0, left / 1500.0);

        event.setRoll(event.getRoll() + (float) Math.sin(t) * 2.2f * power);
        event.setYaw(event.getYaw() + (float) Math.cos(t * 1.3) * 0.6f * power);
    }

    /** Белая вспышка поверх экрана. */
    public static void draw(net.minecraft.client.gui.GuiGraphics g, int w, int h) {
        long left = flashUntil - System.currentTimeMillis();
        if (left <= 0) return;

        int alpha = (int) Math.min(170, 170 * left / 400);
        g.fill(0, 0, w, h, (alpha << 24) | 0xFFFFFF);
    }

    public static void send(String target, int kind) {
        NareNet.troll(target, kind);
    }
}
