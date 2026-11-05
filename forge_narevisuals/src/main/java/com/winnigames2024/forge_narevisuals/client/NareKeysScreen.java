package com.winnigames2024.forge_narevisuals.client;

import com.mojang.blaze3d.platform.InputConstants;
import net.minecraft.client.KeyMapping;
import net.minecraft.client.gui.GuiGraphics;
import net.minecraft.client.gui.screens.Screen;
import net.minecraft.network.chat.Component;
import net.minecraftforge.client.settings.KeyModifier;

/**
 * Назначение клавиш мода. Нарисовано своими руками, как и остальные окна,
 * и подстраивается под высоту окна: привязок стало шесть, в маленьком окне
 * ванильные кнопки уже не помещались.
 */
public class NareKeysScreen extends Screen {

    private final Screen parent;
    private KeyMapping awaiting;

    private int px, py, pw, ph, headH, footH, rowH;

    public NareKeysScreen(Screen parent) {
        super(Component.literal("Клавиши NareVisuals"));
        this.parent = parent;
    }

    private void measure() {
        pw = Math.min(360, Math.max(220, this.width - 20));
        ph = Math.min(300, Math.max(120, this.height - 20));
        px = (this.width - pw) / 2;
        py = (this.height - ph) / 2;
        headH = ph < 190 ? 30 : 42;
        footH = 32;
        rowH = Math.max(14, Math.min(24, (ph - headH - footH) / (NareKeys.ALL.length + 1)));
    }

    private static boolean inside(double mx, double my, int x, int y, int w, int h) {
        return mx >= x && mx <= x + w && my >= y && my <= y + h;
    }

    @Override
    public boolean mouseClicked(double mx, double my, int button) {
        measure();

        for (int i = 0; i < NareKeys.ALL.length; i++) {
            int y = py + headH + i * rowH;
            if (inside(mx, my, px + 12, y, pw - 24, rowH - 3)) {
                awaiting = NareKeys.ALL[i];
                return true;
            }
        }

        int resetY = py + headH + NareKeys.ALL.length * rowH + 2;
        if (inside(mx, my, px + 12, resetY, 110, 18)) {
            reset();
            return true;
        }

        if (inside(mx, my, px + pw - 94, py + ph - 26, 84, 20)) {
            onClose();
            return true;
        }

        return super.mouseClicked(mx, my, button);
    }

    @Override
    public boolean keyPressed(int keyCode, int scanCode, int modifiers) {
        if (awaiting != null) {
            if (keyCode == InputConstants.KEY_ESCAPE)
                bind(awaiting, InputConstants.UNKNOWN);
            else
                bind(awaiting, InputConstants.getKey(keyCode, scanCode));

            awaiting = null;
            apply();
            return true;
        }
        return super.keyPressed(keyCode, scanCode, modifiers);
    }

    private void reset() {
        bind(NareKeys.OPEN_SETTINGS, InputConstants.getKey(InputConstants.KEY_RSHIFT, 0));
        bind(NareKeys.TOGGLE_HUD, InputConstants.getKey(InputConstants.KEY_H, 0));
        bind(NareKeys.TOGGLE_ESP, InputConstants.getKey(InputConstants.KEY_J, 0));
        bind(NareKeys.TOGGLE_BRIGHT, InputConstants.getKey(InputConstants.KEY_G, 0));
        bind(NareKeys.TOGGLE_FREECAM, InputConstants.getKey(InputConstants.KEY_F, 0));
        bind(NareKeys.TOGGLE_SKY, InputConstants.UNKNOWN);
        bind(NareKeys.ZOOM, InputConstants.getKey(InputConstants.KEY_C, 0));
        bind(NareKeys.TOGGLE_NEAR, InputConstants.getKey(InputConstants.KEY_B, 0));
        apply();
    }

    private void bind(KeyMapping mapping, InputConstants.Key key) {
        mapping.setKeyModifierAndCode(KeyModifier.NONE, key);
    }

    private void apply() {
        if (minecraft != null) {
            minecraft.options.save();
            KeyMapping.resetMapping();
        }
    }

    @Override
    public void render(GuiGraphics g, int mouseX, int mouseY, float partialTick) {
        super.render(g, mouseX, mouseY, partialTick);
        measure();

        g.fill(0, 0, this.width, this.height, 0x99000000);

        NareTheme.roundRect(g, px, py, pw, ph, 8, 0xF40D1018);
        g.fillGradient(px + 1, py + 1, px + pw - 1, py + ph - 1, 0x10FFFFFF, 0x00000000);
        NareTheme.outline(g, px, py, pw, ph, 8, 0x2EFFFFFF);

        NareTheme.bold(g, font, "Клавиши", px + 14, py + 11, NareTheme.TEXT);
        if (headH >= 42)
            g.drawString(font, "Нажми строку, потом клавишу · Esc — снять", px + 14, py + 23,
                    NareTheme.DIM, false);

        for (int i = 0; i < NareKeys.ALL.length; i++) {
            KeyMapping mapping = NareKeys.ALL[i];
            int y = py + headH + i * rowH;
            boolean hover = inside(mouseX, mouseY, px + 12, y, pw - 24, rowH - 3);
            boolean waiting = awaiting == mapping;

            if (waiting)
                NareTheme.roundRect(g, px + 12, y, pw - 24, rowH - 3, 5,
                        NareTheme.alpha(NareTheme.accent(), 0.22f));
            else if (hover)
                NareTheme.roundRect(g, px + 12, y, pw - 24, rowH - 3, 5, 0x12FFFFFF);

            int ty = y + (rowH - 3 - 8) / 2;
            g.drawString(font, NareKeys.TITLES[i], px + 22, ty,
                    hover || waiting ? NareTheme.TEXT : 0xFFD2DAE8, false);

            String key = waiting ? "> жду клавишу <" : mapping.getTranslatedKeyMessage().getString();
            int kw = font.width(key);
            g.drawString(font, key, px + pw - 24 - kw, ty,
                    waiting ? NareTheme.accent() : NareTheme.MUTED, false);
        }

        int resetY = py + headH + NareKeys.ALL.length * rowH + 2;
        boolean resetHover = inside(mouseX, mouseY, px + 12, resetY, 110, 18);
        NareTheme.roundRect(g, px + 12, resetY, 110, 18, 5, resetHover ? 0x1EFFFFFF : 0x12FFFFFF);
        NareTheme.outline(g, px + 12, resetY, 110, 18, 5, 0x22FFFFFF);
        g.drawString(font, "Сбросить клавиши", px + 20, resetY + 5,
                resetHover ? NareTheme.TEXT : NareTheme.MUTED, false);

        int dx = px + pw - 94, dy = py + ph - 26;
        boolean doneHover = inside(mouseX, mouseY, dx, dy, 84, 20);
        int accent = NareTheme.accent();
        NareTheme.roundRect(g, dx, dy, 84, 20, 5, doneHover ? accent : NareTheme.alpha(accent, 0.8f));
        NareTheme.bold(g, font, "Готово", dx + (84 - font.width("Готово")) / 2, dy + 6, 0xFF0B0E16);
    }

    @Override
    public void onClose() {
        minecraft.setScreen(parent);
    }

    @Override
    public boolean isPauseScreen() {
        return false;
    }
}
