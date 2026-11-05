package com.winnigames2024.forge_narevisuals.client;

import com.winnigames2024.forge_narevisuals.Config;
import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.GuiGraphics;

/** Базовый элемент HUD: своё положение, свой размер, своя отрисовка. */
public abstract class HudModule {

    public final String id;
    public final String title;
    private final float defaultX;
    private final float defaultY;

    /** Размеры последней отрисовки — нужны для перетаскивания в редакторе. */
    public int lastX, lastY, lastWidth = 60, lastHeight = 16;

    protected HudModule(String id, String title, float defaultX, float defaultY) {
        this.id = id;
        this.title = title;
        this.defaultX = defaultX;
        this.defaultY = defaultY;
    }

    public Config.Element cfg() {
        return Config.get().element(id, true, defaultX, defaultY);
    }

    public boolean isEnabled() {
        return cfg().enabled;
    }

    public void setEnabled(boolean value) {
        cfg().enabled = value;
    }

    /** Элемент можно двигать мышью в редакторе. */
    public boolean movable() {
        return true;
    }

    /** Элемент всегда рисуется по центру экрана (прицел, маркер попадания). */
    public boolean centered() {
        return false;
    }

    /** Нужен ли игрок в мире, чтобы элемент имел смысл. */
    public boolean needsPlayer() {
        return true;
    }

    /** Ширина и высота до отрисовки — редактору нужно знать зону захвата. */
    public abstract int width(Minecraft mc);

    public abstract int height(Minecraft mc);

    public abstract void render(GuiGraphics g, Minecraft mc, int x, int y);

    /** Переводит доли экрана в пиксели и запоминает прямоугольник элемента. */
    public final void draw(GuiGraphics g, Minecraft mc, int screenW, int screenH) {
        int w = Math.max(8, width(mc));
        int h = Math.max(8, height(mc));

        int x, y;
        if (centered()) {
            x = (screenW - w) / 2;
            y = (screenH - h) / 2;
        } else {
            x = Math.round(cfg().x * screenW);
            y = Math.round(cfg().y * screenH);
            x = Math.max(0, Math.min(screenW - w, x));
            y = Math.max(0, Math.min(screenH - h, y));
        }

        lastX = x;
        lastY = y;
        lastWidth = w;
        lastHeight = h;

        render(g, mc, x, y);
    }

    public final void moveTo(int x, int y, int screenW, int screenH) {
        if (screenW <= 0 || screenH <= 0) return;
        cfg().x = Math.max(0f, Math.min(1f, x / (float) screenW));
        cfg().y = Math.max(0f, Math.min(1f, y / (float) screenH));
    }

    public final boolean contains(double mx, double my) {
        return mx >= lastX && mx <= lastX + lastWidth && my >= lastY && my <= lastY + lastHeight;
    }
}
