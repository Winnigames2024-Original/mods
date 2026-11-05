package com.winnigames2024.forge_narevisuals.client;

import com.mojang.blaze3d.platform.InputConstants;
import com.winnigames2024.forge_narevisuals.Config;
import net.minecraft.client.gui.GuiGraphics;
import net.minecraft.client.gui.screens.Screen;
import net.minecraft.network.chat.Component;

/**
 * Пароль для автовхода. Пароль хранится в обычном файле настроек рядом с
 * игрой — в открытом виде, так что на чужом компьютере его лучше не
 * оставлять. Об этом окно и предупреждает прямо на себе.
 */
public class NareLoginScreen extends Screen {

    private final Screen parent;

    /** Что сейчас печатаем: 0 — команда, 1 — пароль. */
    private int field = 1;
    private boolean show;

    private int px, py, pw, ph;

    public NareLoginScreen(Screen parent) {
        super(Component.literal("Автовход"));
        this.parent = parent;
    }

    private void measure() {
        pw = Math.min(360, Math.max(240, this.width - 20));
        ph = 168;
        px = (this.width - pw) / 2;
        py = (this.height - ph) / 2;
    }

    private static boolean inside(double mx, double my, int x, int y, int w, int h) {
        return mx >= x && mx <= x + w && my >= y && my <= y + h;
    }

    @Override
    public boolean charTyped(char c, int modifiers) {
        if (c < ' ') return super.charTyped(c, modifiers);

        Config cfg = Config.get();
        if (field == 0) cfg.loginCommand = text(cfg.loginCommand) + c;
        else cfg.loginPassword = text(cfg.loginPassword) + c;

        Config.save();
        return true;
    }

    @Override
    public boolean keyPressed(int keyCode, int scanCode, int modifiers) {
        if (keyCode == InputConstants.KEY_BACKSPACE) {
            Config cfg = Config.get();
            if (field == 0) cfg.loginCommand = drop(cfg.loginCommand);
            else cfg.loginPassword = drop(cfg.loginPassword);
            Config.save();
            return true;
        }

        if (keyCode == InputConstants.KEY_TAB) {
            field = field == 0 ? 1 : 0;
            return true;
        }

        return super.keyPressed(keyCode, scanCode, modifiers);
    }

    private static String text(String s) { return s == null ? "" : s; }

    private static String drop(String s) {
        String t = text(s);
        return t.isEmpty() ? t : t.substring(0, t.length() - 1);
    }

    @Override
    public boolean mouseClicked(double mx, double my, int button) {
        measure();
        Config cfg = Config.get();

        if (inside(mx, my, px + 14, py + 46, pw - 28, 18)) { field = 0; return true; }
        if (inside(mx, my, px + 14, py + 86, pw - 28, 18)) { field = 1; return true; }

        if (inside(mx, my, px + 14, py + 112, 90, 16)) {
            cfg.autoLogin = !cfg.autoLogin;
            Config.save();
            return true;
        }

        if (inside(mx, my, px + 112, py + 112, 74, 16)) { show = !show; return true; }

        if (inside(mx, my, px + pw - 94, py + ph - 28, 84, 20)) { onClose(); return true; }

        return super.mouseClicked(mx, my, button);
    }

    @Override
    public void render(GuiGraphics g, int mouseX, int mouseY, float partialTick) {
        super.render(g, mouseX, mouseY, partialTick);
        measure();

        Config cfg = Config.get();

        g.fill(0, 0, this.width, this.height, 0x99000000);
        NareTheme.roundRect(g, px, py, pw, ph, 8, 0xF40D1018);
        NareTheme.outline(g, px, py, pw, ph, 8, 0x2EFFFFFF);

        NareTheme.bold(g, font, "Автовход на сервер", px + 14, py + 11, NareTheme.TEXT);
        g.drawString(font, "Tab — другое поле · пароль лежит в файле настроек открыто",
                px + 14, py + 23, NareTheme.DIM, false);

        box(g, px + 14, py + 46, pw - 28, "Команда", "/" + text(cfg.loginCommand), field == 0);
        box(g, px + 14, py + 86, pw - 28, "Пароль",
                show ? text(cfg.loginPassword) : stars(text(cfg.loginPassword)), field == 1);

        chip(g, px + 14, py + 112, 90, cfg.autoLogin ? "Включён" : "Выключен",
                cfg.autoLogin ? NareTheme.accent() : NareTheme.MUTED, mouseX, mouseY);
        chip(g, px + 112, py + 112, 74, show ? "Скрыть" : "Показать",
                NareTheme.MUTED, mouseX, mouseY);

        int dx = px + pw - 94, dy = py + ph - 28;
        boolean done = inside(mouseX, mouseY, dx, dy, 84, 20);
        int accent = NareTheme.accent();
        NareTheme.roundRect(g, dx, dy, 84, 20, 5, done ? accent : NareTheme.alpha(accent, 0.8f));
        NareTheme.bold(g, font, "Готово", dx + (84 - font.width("Готово")) / 2, dy + 6, 0xFF0B0E16);
    }

    private void box(GuiGraphics g, int x, int y, int w, String title, String value, boolean active) {
        g.drawString(font, title, x, y - 10, NareTheme.MUTED, false);

        NareTheme.roundRect(g, x, y, w, 18, 4, active ? 0x1EFFFFFF : 0x12FFFFFF);
        NareTheme.outline(g, x, y, w, 18, 4,
                active ? NareTheme.alpha(NareTheme.accent(), 0.8f) : 0x22FFFFFF);

        String text = value.isEmpty() ? "— пусто —" : value;
        g.drawString(font, text, x + 6, y + 5,
                value.isEmpty() ? NareTheme.DIM : NareTheme.TEXT, false);
    }

    private void chip(GuiGraphics g, int x, int y, int w, String text, int color, int mx, int my) {
        boolean hover = inside(mx, my, x, y, w, 16);
        NareTheme.roundRect(g, x, y, w, 16, 4, hover ? 0x1EFFFFFF : 0x12FFFFFF);
        g.drawString(font, text, x + (w - font.width(text)) / 2, y + 4, color, false);
    }

    private static String stars(String s) {
        StringBuilder sb = new StringBuilder(s.length());
        for (int i = 0; i < s.length(); i++) sb.append('*');
        return sb.toString();
    }

    @Override
    public void onClose() {
        Config.save();
        minecraft.setScreen(parent);
    }

    @Override
    public boolean isPauseScreen() {
        return false;
    }
}
