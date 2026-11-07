package com.winnigames2024.forge_narevisuals.client;

import net.minecraft.client.gui.GuiGraphics;
import net.minecraft.client.gui.screens.Screen;
import net.minecraft.network.chat.Component;

import java.util.ArrayList;
import java.util.List;

/**
 * Окно ошибки — такое же по смыслу, как диалог в обычной программе: заголовок
 * с полосой, значок, текст и три кнопки.
 *
 * Три кнопки, а не одна, потому что ошибки бывают разные. «Продолжить» —
 * понял, идём дальше. «Игнорировать эту» — она повторяется и мешает.
 * «Закрыть все» — когда их насыпалось сразу несколько.
 */
public class NareErrorScreen extends Screen {

    private static final int BAR = 26;
    private static final int BTN_H = 20;

    private final Screen parent;
    private NareErrors.Trouble trouble;

    private int px, py, pw, ph;
    private final List<int[]> buttons = new ArrayList<>();   // x, y, w, h, какая

    public NareErrorScreen(Screen parent) {
        super(Component.literal("Ошибка"));
        this.parent = parent;
    }

    @Override
    protected void init() {
        trouble = NareErrors.peek();
        if (trouble == null) close();
    }

    @Override
    public boolean isPauseScreen() { return false; }

    // ---------------------------------------------------------- размеры

    private void measure() {
        pw = Math.min(360, Math.max(240, this.width - 40));

        int textW = pw - 60;
        int lines = trouble == null ? 1
                : font.split(Component.literal(trouble.text()), textW).size();

        ph = BAR + 16 + Math.max(34, lines * 10 + 12) + BTN_H + 18;

        px = (this.width - pw) / 2;
        py = (this.height - ph) / 2;
    }

    // --------------------------------------------------------- отрисовка

    @Override
    public void render(GuiGraphics g, int mouseX, int mouseY, float partialTick) {
        super.render(g, mouseX, mouseY, partialTick);

        if (trouble == null) {
            trouble = NareErrors.peek();
            if (trouble == null) { close(); return; }
        }

        measure();

        g.fill(0, 0, this.width, this.height, 0xB4000000);

        // тень под окном
        NareTheme.roundRect(g, px + 3, py + 4, pw, ph, 9, 0x40000000);

        NareTheme.roundRect(g, px, py, pw, ph, 8, 0xF60E1119);
        NareTheme.outline(g, px, py, pw, ph, 8, 0x3AFFFFFF);

        // полоса заголовка
        NareTheme.roundRect(g, px + 1, py + 1, pw - 2, BAR, 7, 0x24FF6B6B);
        g.fill(px + 1, py + BAR, px + pw - 1, py + BAR + 1, 0x33FFFFFF);

        NareTheme.bold(g, font, trouble.title(), px + 34, py + 9, NareTheme.TEXT);

        // значок: кружок с восклицательным знаком
        int ix = px + 13, iy = py + 8;
        NareTheme.roundRect(g, ix, iy, 11, 11, 5, NareTheme.BAD);
        g.fill(ix + 5, iy + 2, ix + 6, iy + 7, 0xFF0E1119);
        g.fill(ix + 5, iy + 8, ix + 6, iy + 9, 0xFF0E1119);

        // текст
        int textX = px + 16;
        int textW = pw - 32;
        int ty = py + BAR + 12;

        for (var line : font.split(Component.literal(trouble.text()), textW)) {
            g.drawString(font, line, textX, ty, 0xFFC9D3E4, false);
            ty += 10;
        }

        int more = NareErrors.left();
        if (more > 0)
            g.drawString(font, "и ещё " + more + " " + (more == 1 ? "ошибка" : "таких"),
                    textX, ty + 2, NareTheme.DIM, false);

        // кнопки
        buttons.clear();

        int by = py + ph - BTN_H - 10;
        int gap = 6;
        int free = pw - 20 - gap * 2;

        int w1 = Math.max(60, free * 30 / 100);
        int w2 = Math.max(74, free * 38 / 100);
        int w3 = free - w1 - w2;

        int bx = px + 10;
        button(g, bx, by, w1, "Продолжить", mouseX, mouseY, 0, true);
        bx += w1 + gap;
        button(g, bx, by, w2, "Игнорировать эту", mouseX, mouseY, 1, false);
        bx += w2 + gap;
        button(g, bx, by, w3, "Закрыть все", mouseX, mouseY, 2, false);
    }

    private void button(GuiGraphics g, int x, int y, int w, String text,
                        int mouseX, int mouseY, int which, boolean main) {
        boolean hover = mouseX >= x && mouseX <= x + w && mouseY >= y && mouseY <= y + BTN_H;

        int accent = NareTheme.accent();
        int back = main
                ? NareTheme.alpha(accent, hover ? 0.42f : 0.30f)
                : (hover ? 0x1EFFFFFF : 0x12FFFFFF);

        NareTheme.roundRect(g, x, y, w, BTN_H, 5, back);
        NareTheme.outline(g, x, y, w, BTN_H, 5,
                main ? NareTheme.alpha(accent, 0.75f) : 0x2AFFFFFF);

        String fit = text;
        if (font.width(fit) > w - 8) {
            while (fit.length() > 1 && font.width(fit + "…") > w - 8)
                fit = fit.substring(0, fit.length() - 1);
            fit = fit + "…";
        }

        g.drawString(font, fit, x + (w - font.width(fit)) / 2, y + (BTN_H - 8) / 2,
                main ? 0xFFFFFFFF : 0xFFD2DAE8, false);

        buttons.add(new int[] { x, y, w, BTN_H, which });
    }

    // --------------------------------------------------------------- мышь

    @Override
    public boolean mouseClicked(double mouseX, double mouseY, int button) {
        for (int[] b : buttons) {
            if (mouseX < b[0] || mouseX > b[0] + b[2]) continue;
            if (mouseY < b[1] || mouseY > b[1] + b[3]) continue;

            switch (b[4]) {
                case 0 -> NareErrors.drop();
                case 1 -> NareErrors.ignore(trouble.key());
                default -> NareErrors.dropAll();
            }

            trouble = NareErrors.peek();
            if (trouble == null) close();
            return true;
        }

        return super.mouseClicked(mouseX, mouseY, button);
    }

    @Override
    public boolean keyPressed(int key, int scan, int modifiers) {
        // Enter — «Продолжить», Esc — тоже: окно не должно держать человека
        if (key == 257 || key == 335 || key == 256) {
            NareErrors.drop();
            trouble = NareErrors.peek();
            if (trouble == null) close();
            return true;
        }

        return super.keyPressed(key, scan, modifiers);
    }

    @Override
    public void onClose() {
        if (minecraft != null) minecraft.setScreen(parent);
        else super.onClose();
    }

    private void close() {
        if (minecraft != null) minecraft.setScreen(parent);
    }
}
