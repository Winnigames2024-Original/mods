package com.winnigames2024.forge_narevisuals.client;

import com.winnigames2024.forge_narevisuals.Config;
import net.minecraft.client.gui.GuiGraphics;
import net.minecraft.client.gui.screens.Screen;
import net.minecraft.network.chat.Component;

/**
 * Выбор оформления. Открывается кнопкой «Тема» прямо из настроек игры —
 * там его и ищут.
 *
 * Первой строкой — «Как в обычном Minecraft»: мод перестаёт подменять меню,
 * прицел и небо, и игра выглядит ровно так, как без него. Всё остальное
 * (HUD, голос, FPS+) продолжает работать: человек просил не трогать вид, а
 * не выключить мод.
 */
public class NareThemeScreen extends Screen {

    private static final int ROW_H = 22;

    private final Screen parent;

    private int px, py, pw, ph;

    public NareThemeScreen(Screen parent) {
        super(Component.literal("Тема"));
        this.parent = parent;
    }

    private void measure() {
        pw = Math.min(300, Math.max(200, this.width - 40));
        ph = 56 + (NareTheme.THEMES.length + 1) * ROW_H + 16;
        px = (this.width - pw) / 2;
        py = Math.max(10, (this.height - ph) / 2);
    }

    @Override
    public void render(GuiGraphics g, int mouseX, int mouseY, float partialTick) {
        super.render(g, mouseX, mouseY, partialTick);
        measure();

        g.fill(0, 0, this.width, this.height, 0x99000000);

        NareTheme.roundRect(g, px, py, pw, ph, 8, 0xF40D1018);
        NareTheme.outline(g, px, py, pw, ph, 8, 0x2EFFFFFF);

        NareTheme.bold(g, font, "Оформление", px + 14, py + 13, NareTheme.TEXT);
        g.drawString(font, "мод рисует своё меню, прицел и небо",
                px + 14, py + 26, NareTheme.DIM, false);

        Config cfg = Config.get();

        // ванильный вид — отдельной строкой сверху
        row(g, 0, "Как в обычном Minecraft", "мод ничего не подменяет",
                cfg.vanillaLook, mouseX, mouseY);

        for (int i = 0; i < NareTheme.THEMES.length; i++) {
            var preset = NareTheme.THEMES[i];
            boolean on = !cfg.vanillaLook && preset.name.equals(cfg.theme);
            row(g, i + 1, preset.name, null, on, mouseX, mouseY);
        }
    }

    private void row(GuiGraphics g, int index, String title, String hint,
                     boolean on, int mouseX, int mouseY) {
        int y = py + 44 + index * ROW_H;
        int cx = px + 10;
        int cw = pw - 20;

        boolean hover = mouseX >= cx && mouseX <= cx + cw && mouseY >= y && mouseY < y + ROW_H - 2;
        if (hover) NareTheme.roundRect(g, cx, y, cw, ROW_H - 2, 4, 0x12FFFFFF);

        int ty = hint == null ? y + 6 : y + 2;
        g.drawString(font, title, cx + 8, ty, 0xFFD2DAE8, false);
        if (hint != null) g.drawString(font, hint, cx + 8, y + 12, NareTheme.DIM, false);

        int accent = index == 0 ? 0xFF8C98AE : NareTheme.THEMES[index - 1].accent;

        if (on) {
            NareTheme.roundRect(g, cx + cw - 22, y + 6, 9, 9, 3, accent);
            NareTheme.roundRect(g, cx + cw - 23, y + 5, 11, 11, 4, NareTheme.alpha(accent, 0.25f));
        }
        else NareTheme.outline(g, cx + cw - 22, y + 6, 9, 9, 3, NareTheme.alpha(accent, 0.55f));
    }

    @Override
    public boolean mouseClicked(double mouseX, double mouseY, int button) {
        measure();

        int cx = px + 10;
        int cw = pw - 20;
        Config cfg = Config.get();

        for (int index = 0; index <= NareTheme.THEMES.length; index++) {
            int y = py + 44 + index * ROW_H;
            if (mouseX < cx || mouseX > cx + cw) continue;
            if (mouseY < y || mouseY >= y + ROW_H - 2) continue;

            if (index == 0) vanilla(cfg);
            else {
                cfg.vanillaLook = false;
                cfg.customMenu = true;
                cfg.customCrosshair = true;
                NareTheme.applyTheme(NareTheme.THEMES[index - 1].name, true);
            }

            Config.save();
            return true;
        }

        return super.mouseClicked(mouseX, mouseY, button);
    }

    /** Возвращает игре её собственный вид, не трогая остальное в моде. */
    private static void vanilla(Config cfg) {
        cfg.vanillaLook = true;
        cfg.customMenu = false;
        cfg.customCrosshair = false;
        cfg.customSky = false;
    }

    @Override
    public void onClose() {
        if (minecraft != null) minecraft.setScreen(parent);
        else super.onClose();
    }
}
