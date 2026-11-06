package com.winnigames2024.forge_narevisuals.client;

import com.winnigames2024.forge_narevisuals.Config;
import net.minecraft.client.gui.GuiGraphics;
import net.minecraft.client.gui.components.Button;
import net.minecraft.client.gui.screens.Screen;
import net.minecraft.network.chat.Component;

/** Перетаскивание элементов HUD мышью. */
public class HudEditScreen extends Screen {

    private final Screen parent;

    private HudModule dragging;
    private int grabX, grabY;

    public HudEditScreen(Screen parent) {
        super(Component.literal("Расположение элементов"));
        this.parent = parent;
    }

    @Override
    protected void init() {
        addRenderableWidget(Button.builder(Component.literal("Готово"), b -> onClose())
                .bounds(this.width / 2 - 104, this.height - 28, 100, 20).build());

        addRenderableWidget(Button.builder(Component.literal("Сбросить"), b -> {
            Config.get().elements.clear();
            Config.save();
        }).bounds(this.width / 2 + 4, this.height - 28, 100, 20).build());
    }

    @Override
    public void render(GuiGraphics g, int mouseX, int mouseY, float partialTick) {
        super.render(g, mouseX, mouseY, partialTick);

        // сам HUD — чтобы было видно, что двигаешь
        NareHud.renderAll(g, minecraft, true);

        for (HudModule module : NareHud.MODULES) {
            if (!module.isEnabled() || !module.movable()) continue;

            boolean hover = module.contains(mouseX, mouseY) || module == dragging;
            int color = hover ? NareTheme.accent() : NareTheme.alpha(0xFFFFFFFF, 0.25f);

            int x = module.lastX, y = module.lastY;
            int w = module.lastWidth, h = module.lastHeight;

            g.fill(x - 1, y - 1, x + w + 1, y, color);
            g.fill(x - 1, y + h, x + w + 1, y + h + 1, color);
            g.fill(x - 1, y, x, y + h, color);
            g.fill(x + w, y, x + w + 1, y + h, color);

            if (hover)
                g.drawString(font, module.title, x, y - 11, NareTheme.accent(), true);
        }

        g.drawCenteredString(font, "Перетащите элементы мышью", this.width / 2, 16, NareTheme.TEXT);
        g.drawCenteredString(font, "«Сбросить» вернёт стандартные места",
                this.width / 2, 28, NareTheme.MUTED);
    }

    @Override
    public boolean mouseClicked(double mouseX, double mouseY, int button) {
        if (super.mouseClicked(mouseX, mouseY, button)) return true;
        if (button != 0) return false;

        for (int i = NareHud.MODULES.size() - 1; i >= 0; i--) {
            HudModule module = NareHud.MODULES.get(i);
            if (!module.isEnabled() || !module.movable()) continue;
            if (!module.contains(mouseX, mouseY)) continue;

            dragging = module;
            grabX = (int) mouseX - module.lastX;
            grabY = (int) mouseY - module.lastY;
            return true;
        }
        return false;
    }

    @Override
    public boolean mouseDragged(double mouseX, double mouseY, int button, double dragX, double dragY) {
        if (dragging != null && button == 0) {
            int w = minecraft.getWindow().getGuiScaledWidth();
            int h = minecraft.getWindow().getGuiScaledHeight();
            dragging.moveTo((int) mouseX - grabX, (int) mouseY - grabY, w, h);
            return true;
        }
        return super.mouseDragged(mouseX, mouseY, button, dragX, dragY);
    }

    @Override
    public boolean mouseReleased(double mouseX, double mouseY, int button) {
        if (dragging != null) {
            dragging = null;
            Config.save();
            return true;
        }
        return super.mouseReleased(mouseX, mouseY, button);
    }

    @Override
    public void onClose() {
        Config.save();
        minecraft.setScreen(parent);
    }

    /**
     * Игру не затемняем и не размываем. Элементы двигают, чтобы посмотреть,
     * как они лягут поверх живой картинки, а не поверх серого прямоугольника.
     */
    @Override
    public void renderBackground(GuiGraphics g, int mouseX, int mouseY, float partialTick) {
        // ничего: пусть будет видно саму игру
    }

    @Override
    public boolean isPauseScreen() {
        return false;
    }
}
