package com.winnigames2024.forge_narevisuals.client;

import net.minecraft.client.gui.GuiGraphics;
import net.minecraft.client.gui.screens.Screen;
import net.minecraft.network.chat.Component;

/**
 * Настройка прыжковой плиты. Открывается, когда админ с правом щёлкает по
 * ней правой кнопкой: сервер присылает координаты и нынешнюю силу.
 *
 * Сила уходит обратно сразу, как её меняют: лишнего «сохранить» тут не
 * надо, а плиту обычно настраивают, прыгая на ней же.
 */
public class NareJumpPadScreen extends Screen {

    private final Screen parent;
    private final String world;
    private final int bx, by, bz;

    private int power;
    private boolean dragging;

    private int px, py, pw, ph;

    public NareJumpPadScreen(Screen parent, String world, int x, int y, int z, int power) {
        super(Component.literal("Прыжковая плита"));

        this.parent = parent;
        this.world = world;
        this.bx = x;
        this.by = y;
        this.bz = z;
        this.power = clamp(power);
    }

    private static int clamp(int value) {
        return Math.max(1, Math.min(60, value));
    }

    private void measure() {
        pw = Math.min(300, Math.max(220, this.width - 40));
        ph = 136;
        px = (this.width - pw) / 2;
        py = (this.height - ph) / 2;
    }

    @Override
    public boolean isPauseScreen() { return false; }

    @Override
    public void render(GuiGraphics g, int mouseX, int mouseY, float partialTick) {
        super.render(g, mouseX, mouseY, partialTick);
        measure();

        g.fill(0, 0, this.width, this.height, 0x88000000);

        NareTheme.roundRect(g, px, py, pw, ph, 8, 0xF40D1018);
        NareTheme.outline(g, px, py, pw, ph, 8, 0x2EFFFFFF);

        NareTheme.bold(g, font, "Прыжковая плита", px + 14, py + 13, NareTheme.TEXT);
        g.drawString(font, bx + " " + by + " " + bz + " · " + world,
                px + 14, py + 26, NareTheme.DIM, false);

        g.drawString(font, "Сила", px + 14, py + 50, 0xFFD2DAE8, false);

        String value = String.valueOf(power);
        NareTheme.bold(g, font, value, px + pw - 14 - font.width(value), py + 50, NareTheme.accent());

        // ползунок
        int x = px + 14, w = pw - 28, y = py + 68;
        float k = (power - 1) / 59f;

        g.fill(x, y, x + w, y + 3, 0x33FFFFFF);
        g.fill(x, y, x + (int) (w * k), y + 3, NareTheme.accent());

        int knob = x + (int) (w * k);
        NareTheme.roundRect(g, knob - 3, y - 4, 6, 11, 3, NareTheme.accent());

        g.drawString(font, "слабее", x, y + 12, NareTheme.DIM, false);
        String far = "выше";
        g.drawString(font, far, x + w - font.width(far), y + 12, NareTheme.DIM, false);

        g.drawString(font, "Встаньте на плиту, чтобы проверить · Esc — закрыть",
                px + 14, py + ph - 16, NareTheme.DIM, false);
    }

    // --------------------------------------------------------------- мышь

    @Override
    public boolean mouseClicked(double mouseX, double mouseY, int button) {
        measure();

        int x = px + 14, w = pw - 28, y = py + 68;
        if (mouseY >= y - 8 && mouseY <= y + 11 && mouseX >= x - 4 && mouseX <= x + w + 4) {
            dragging = true;
            apply(mouseX, x, w);
            return true;
        }

        return super.mouseClicked(mouseX, mouseY, button);
    }

    @Override
    public boolean mouseDragged(double mouseX, double mouseY, int button, double dx, double dy) {
        if (dragging) { apply(mouseX, px + 14, pw - 28); return true; }
        return super.mouseDragged(mouseX, mouseY, button, dx, dy);
    }

    @Override
    public boolean mouseReleased(double mouseX, double mouseY, int button) {
        if (dragging) {
            dragging = false;
            NareNet.sendJumpPad(world, bx, by, bz, power);
        }
        return super.mouseReleased(mouseX, mouseY, button);
    }

    private void apply(double mouseX, int x, int w) {
        float k = (float) ((mouseX - x) / Math.max(1, w));
        power = clamp(1 + Math.round(k * 59f));
    }

    @Override
    public boolean keyPressed(int key, int scan, int modifiers) {
        if (key == 263 || key == 262) {                  // стрелки влево-вправо
            power = clamp(power + (key == 262 ? 1 : -1));
            NareNet.sendJumpPad(world, bx, by, bz, power);
            return true;
        }
        return super.keyPressed(key, scan, modifiers);
    }

    @Override
    public void onClose() {
        NareNet.sendJumpPad(world, bx, by, bz, power);
        if (minecraft != null) minecraft.setScreen(parent);
        else super.onClose();
    }
}
