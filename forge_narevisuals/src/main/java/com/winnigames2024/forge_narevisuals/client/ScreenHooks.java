package com.winnigames2024.forge_narevisuals.client;

import com.mojang.blaze3d.platform.InputConstants;
import com.winnigames2024.forge_narevisuals.Config;
import com.winnigames2024.forge_narevisuals.Forge_narevisuals;
import com.winnigames2024.forge_narevisuals.Launch;
import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.components.AbstractWidget;
import net.minecraft.client.gui.components.Button;
import net.minecraft.client.gui.components.EditBox;
import net.minecraft.client.gui.components.events.GuiEventListener;
import net.minecraft.client.gui.screens.PauseScreen;
import net.minecraft.client.gui.screens.Screen;
import net.minecraft.client.gui.screens.TitleScreen;
import net.minecraft.network.chat.Component;
import net.minecraftforge.api.distmarker.Dist;
import net.minecraftforge.client.event.ScreenEvent;
import net.minecraftforge.eventbus.api.SubscribeEvent;
import net.minecraftforge.fml.common.Mod;

/**
 * Кнопка NareVisuals в главном меню и в паузе, плюс открытие настроек
 * правым Shift на любом экране — не только в игре.
 *
 * Место для кнопки подбирается так, чтобы она не налезала на ванильные:
 * в узком окне кнопка языка вставала ровно туда же, и они перекрывались.
 */
@Mod.EventBusSubscriber(modid = Forge_narevisuals.MODID, value = Dist.CLIENT)
public final class ScreenHooks {

    private static final int BW = 104;
    private static final int BH = 20;
    private static final int PAD = 6;

    private ScreenHooks() { }

    @SubscribeEvent
    public static void onScreenInit(ScreenEvent.Init.Post event) {
        if (!Launch.visuals()) return;

        Screen screen = event.getScreen();
        if (!(screen instanceof TitleScreen) && !(screen instanceof PauseScreen)) return;

        // в своём главном меню NareVisuals уже есть отдельной строкой
        if (screen instanceof TitleScreen && Config.get().customMenu) return;

        int[] spot = freeSpot(screen);

        Button button = Button.builder(Component.literal(Forge_narevisuals.NAME), b ->
                Minecraft.getInstance().setScreen(NareMenus.open(screen))
        ).bounds(spot[0], spot[1], BW, BH).build();

        event.addListener(button);
    }

    /** Первое место из списка, где кнопка никого не задевает. */
    private static int[] freeSpot(Screen screen) {
        int[][] candidates = {
                { PAD, PAD },
                { PAD, PAD + BH + 4 },
                { screen.width - BW - PAD, PAD },
                { PAD, screen.height - BH - PAD - 24 },
                { PAD, screen.height - BH - PAD - 48 }
        };

        for (int[] candidate : candidates)
            if (isFree(screen, candidate[0], candidate[1])) return candidate;

        return candidates[0];
    }

    private static boolean isFree(Screen screen, int x, int y) {
        for (GuiEventListener child : screen.children()) {
            if (!(child instanceof AbstractWidget widget)) continue;
            if (!widget.visible) continue;

            boolean apart =
                    x + BW + 2 <= widget.getX() ||
                    widget.getX() + widget.getWidth() + 2 <= x ||
                    y + BH + 2 <= widget.getY() ||
                    widget.getY() + widget.getHeight() + 2 <= y;

            if (!apart) return false;
        }
        return true;
    }

    @SubscribeEvent
    public static void onScreenKey(ScreenEvent.KeyPressed.Pre event) {
        if (!Launch.visuals()) return;
        if (event.getKeyCode() != InputConstants.KEY_RSHIFT) return;

        Screen screen = event.getScreen();

        // в своих экранах Shift не перехватываем — там он может назначаться как бинд
        if (screen instanceof NareSettingsScreen) return;
        if (screen instanceof NareClickScreen) return;
        if (screen instanceof NareFpsScreen) return;
        if (screen instanceof NareVoiceScreen) return;
        if (screen instanceof HudEditScreen) return;
        if (screen instanceof NareKeysScreen) return;

        // не мешаем вводу текста (чат, поиск, переименование)
        if (screen.getFocused() instanceof EditBox) return;

        Minecraft.getInstance().setScreen(NareMenus.open(screen));
        event.setCanceled(true);
    }
}
