package com.winnigames2024.forge_narevisuals.client;

import com.winnigames2024.forge_narevisuals.Config;
import com.winnigames2024.forge_narevisuals.Forge_narevisuals;
import net.minecraft.Util;
import net.minecraft.client.gui.GuiGraphics;
import net.minecraft.client.gui.screens.Screen;
import net.minecraft.network.chat.Component;
import org.lwjgl.glfw.GLFW;

import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;
import java.util.function.Consumer;

/**
 * Общий экран со списком: пресеты .nvc и картинки для прицела и неба.
 * Нарисован так же, как настройки — своими руками, без ванильных кнопок.
 */
public abstract class NarePickScreen extends Screen {

    protected static final int ROW_H = 22;

    private final Screen parent;
    protected final String subtitle;

    protected List<String> items = new ArrayList<>();
    protected int selected = -1;
    protected int scroll;

    /** Ввод имени нового пресета; null — не вводим. */
    protected StringBuilder typing;
    protected String typingTitle;
    protected Consumer<String> typingDone;

    protected String status = "";
    protected boolean statusBad;

    private int px, py, pw, ph, headH, footH;
    private final List<Action> actions = new ArrayList<>();

    protected record Action(String title, Runnable run, boolean accent, boolean needsSelection) { }

    protected NarePickScreen(Screen parent, String title, String subtitle) {
        super(Component.literal(title));
        this.parent = parent;
        this.subtitle = subtitle;
    }

    protected abstract void reload();

    protected abstract List<Action> buttons();

    protected void refresh() {
        reload();
        actions.clear();
        actions.addAll(buttons());
        if (selected >= items.size()) selected = items.size() - 1;
    }

    @Override
    protected void init() {
        refresh();
    }

    protected void say(String text, boolean bad) {
        status = text;
        statusBad = bad;
    }

    protected String current() {
        return selected >= 0 && selected < items.size() ? items.get(selected) : null;
    }

    protected void ask(String title, String initial, Consumer<String> done) {
        typingTitle = title;
        typing = new StringBuilder(initial == null ? "" : initial);
        typingDone = done;
    }

    protected void openFolder(Path path) {
        try {
            java.nio.file.Files.createDirectories(path);
            Util.getPlatform().openPath(path);
        } catch (Exception ex) {
            say("Папку не открыть", true);
        }
    }

    // ---------------------------------------------------------------- размеры

    private void measure() {
        pw = Math.min(430, Math.max(220, this.width - 20));
        ph = Math.min(300, Math.max(140, this.height - 20));
        px = (this.width - pw) / 2;
        py = (this.height - ph) / 2;
        headH = ph < 200 ? 38 : 48;
        footH = 58;
    }

    private int visibleRows() {
        return Math.max(1, (ph - headH - footH) / ROW_H);
    }

    private static boolean inside(double mx, double my, int x, int y, int w, int h) {
        return mx >= x && mx <= x + w && my >= y && my <= y + h;
    }

    // ---------------------------------------------------------------- ввод

    @Override
    public boolean mouseClicked(double mx, double my, int button) {
        measure();

        if (typing != null) {
            // клик мимо поля — отмена ввода
            if (!inside(mx, my, px + 14, py + ph - 52, pw - 28, 22)) {
                if (inside(mx, my, px + pw - 94, py + ph - 26, 84, 20)) {
                    String value = typing.toString().trim();
                    Consumer<String> done = typingDone;
                    typing = null;
                    if (!value.isEmpty() && done != null) done.accept(value);
                    refresh();
                    return true;
                }
                if (inside(mx, my, px + pw - 186, py + ph - 26, 84, 20)) {
                    typing = null;
                    return true;
                }
            }
            return true;
        }

        int listH = visibleRows() * ROW_H;
        if (inside(mx, my, px + 12, py + headH, pw - 24, listH)) {
            int index = (int) ((my - (py + headH)) / ROW_H) + scroll;
            if (index >= 0 && index < items.size()) {
                selected = index;
                status = "";
                refresh();
            }
            return true;
        }

        int bx = px + 12;
        int by = py + ph - 30;
        for (Action action : actions) {
            int w = font.width(action.title()) + 18;
            if (inside(mx, my, bx, by, w, 20)) {
                if (!action.needsSelection() || current() != null) action.run().run();
                return true;
            }
            bx += w + 6;
        }

        return super.mouseClicked(mx, my, button);
    }

    @Override
    public boolean mouseScrolled(double mx, double my, double sx, double sy) {
        if (sy != 0 && typing == null) {
            scroll -= (int) Math.signum(sy);
            int max = Math.max(0, items.size() - visibleRows());
            scroll = Math.max(0, Math.min(max, scroll));
        }
        return true;
    }

    @Override
    public boolean charTyped(char c, int modifiers) {
        if (typing == null) return super.charTyped(c, modifiers);
        if (typing.length() < 40 && c >= ' ') typing.append(c);
        return true;
    }

    @Override
    public boolean keyPressed(int key, int scan, int modifiers) {
        if (typing != null) {
            if (key == GLFW.GLFW_KEY_BACKSPACE) {
                if (typing.length() > 0) typing.deleteCharAt(typing.length() - 1);
                return true;
            }
            if (key == GLFW.GLFW_KEY_ENTER || key == GLFW.GLFW_KEY_KP_ENTER) {
                String value = typing.toString().trim();
                Consumer<String> done = typingDone;
                typing = null;
                if (!value.isEmpty() && done != null) done.accept(value);
                refresh();
                return true;
            }
            if (key == GLFW.GLFW_KEY_ESCAPE) {
                typing = null;
                return true;
            }
            return true;
        }
        return super.keyPressed(key, scan, modifiers);
    }

    // ---------------------------------------------------------------- отрисовка

    @Override
    public void render(GuiGraphics g, int mouseX, int mouseY, float partialTick) {
        super.render(g, mouseX, mouseY, partialTick);
        measure();

        g.fill(0, 0, this.width, this.height, 0x99000000);

        NareTheme.roundRect(g, px, py, pw, ph, 8, 0xF40D1018);
        g.fillGradient(px + 1, py + 1, px + pw - 1, py + ph - 1, 0x10FFFFFF, 0x00000000);
        NareTheme.outline(g, px, py, pw, ph, 8, 0x2EFFFFFF);

        NareTheme.bold(g, font, getTitle().getString(), px + 14, py + 12, NareTheme.TEXT);
        if (headH >= 48)
            g.drawString(font, subtitle, px + 14, py + 24, NareTheme.DIM, false);

        g.fill(px + 1, py + headH - 6, px + pw - 1, py + headH - 5, 0x1AFFFFFF);

        list(g, mouseX, mouseY);

        if (typing != null) input(g, mouseX, mouseY);
        else buttonsRow(g, mouseX, mouseY);
    }

    private void list(GuiGraphics g, int mouseX, int mouseY) {
        int shown = visibleRows();
        int limit = Math.min(items.size() - scroll, shown);

        if (items.isEmpty()) {
            g.drawString(font, "Пусто", px + 20, py + headH + 6, NareTheme.DIM, false);
            return;
        }

        for (int i = 0; i < limit; i++) {
            int index = i + scroll;
            int y = py + headH + i * ROW_H;
            boolean hover = inside(mouseX, mouseY, px + 12, y, pw - 24, ROW_H - 2);
            boolean active = index == selected;

            if (active) {
                NareTheme.roundRect(g, px + 12, y, pw - 24, ROW_H - 2, 5,
                        NareTheme.alpha(NareTheme.accent(), 0.22f));
                g.fill(px + 12, y + 4, px + 14, y + ROW_H - 6, NareTheme.accent());
            } else if (hover) {
                NareTheme.roundRect(g, px + 12, y, pw - 24, ROW_H - 2, 5, 0x12FFFFFF);
            }

            String text = items.get(index);
            int color = active ? NareTheme.TEXT : hover ? 0xFFD2DAE8 : NareTheme.MUTED;
            if (active) NareTheme.bold(g, font, text, px + 22, y + 6, color);
            else g.drawString(font, text, px + 22, y + 6, color, false);
        }

        if (items.size() > shown) {
            int trackY = py + headH;
            int trackH = shown * ROW_H - 4;
            int barH = Math.max(14, trackH * shown / items.size());
            int barY = trackY + (trackH - barH) * scroll / Math.max(1, items.size() - shown);
            g.fill(px + pw - 9, trackY, px + pw - 7, trackY + trackH, 0x18FFFFFF);
            g.fill(px + pw - 9, barY, px + pw - 7, barY + barH, 0x55FFFFFF);
        }
    }

    private void buttonsRow(GuiGraphics g, int mouseX, int mouseY) {
        if (!status.isEmpty())
            g.drawString(font, status, px + 14, py + ph - 46,
                    statusBad ? NareTheme.BAD : NareTheme.GOOD, false);

        int bx = px + 12;
        int by = py + ph - 30;

        for (Action action : actions) {
            int w = font.width(action.title()) + 18;
            boolean usable = !action.needsSelection() || current() != null;
            boolean hover = usable && inside(mouseX, mouseY, bx, by, w, 20);

            if (action.accent()) {
                int accent = NareTheme.accent();
                NareTheme.roundRect(g, bx, by, w, 20, 5,
                        usable ? (hover ? accent : NareTheme.alpha(accent, 0.8f)) : 0x33FFFFFF);
                NareTheme.bold(g, font, action.title(), bx + 9, by + 6,
                        usable ? 0xFF0B0E16 : NareTheme.DIM);
            } else {
                NareTheme.roundRect(g, bx, by, w, 20, 5, hover ? 0x1EFFFFFF : 0x12FFFFFF);
                NareTheme.outline(g, bx, by, w, 20, 5, 0x22FFFFFF);
                g.drawString(font, action.title(), bx + 9, by + 6,
                        usable ? (hover ? NareTheme.TEXT : NareTheme.MUTED) : NareTheme.DIM, false);
            }

            bx += w + 6;
        }
    }

    private void input(GuiGraphics g, int mouseX, int mouseY) {
        int x = px + 14;
        int y = py + ph - 52;
        int w = pw - 28;

        g.drawString(font, typingTitle, x, y - 11, NareTheme.DIM, false);

        NareTheme.roundRect(g, x, y, w, 22, 5, 0x22000000);
        NareTheme.outline(g, x, y, w, 22, 5, NareTheme.alpha(NareTheme.accent(), 0.7f));

        String text = typing.toString();
        g.drawString(font, text, x + 8, y + 7, NareTheme.TEXT, false);

        if ((System.currentTimeMillis() / 500) % 2 == 0) {
            int cx = x + 8 + font.width(text);
            g.fill(cx, y + 5, cx + 1, y + 17, NareTheme.accent());
        }

        int okW = 84, cancelW = 84;
        int okX = px + pw - 94, cancelX = px + pw - 186, by = py + ph - 26;

        boolean okHover = inside(mouseX, mouseY, okX, by, okW, 20);
        int accent = NareTheme.accent();
        NareTheme.roundRect(g, okX, by, okW, 20, 5, okHover ? accent : NareTheme.alpha(accent, 0.8f));
        NareTheme.bold(g, font, "Готово", okX + (okW - font.width("Готово")) / 2, by + 6, 0xFF0B0E16);

        boolean cancelHover = inside(mouseX, mouseY, cancelX, by, cancelW, 20);
        NareTheme.roundRect(g, cancelX, by, cancelW, 20, 5, cancelHover ? 0x1EFFFFFF : 0x12FFFFFF);
        NareTheme.outline(g, cancelX, by, cancelW, 20, 5, 0x22FFFFFF);
        g.drawString(font, "Отмена", cancelX + (cancelW - font.width("Отмена")) / 2, by + 6,
                cancelHover ? NareTheme.TEXT : NareTheme.MUTED, false);
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

    // ================================================================ пресеты

    public static class Presets extends NarePickScreen {

        public Presets(Screen parent) {
            super(parent, "Пресеты", "Настройки и картинки одним файлом .nvc");
        }

        @Override
        protected void reload() {
            items = NarePresets.list();
        }

        @Override
        protected List<Action> buttons() {
            List<Action> list = new ArrayList<>();

            list.add(new Action("Сохранить как…", () -> ask("Название пресета", "", name -> {
                try {
                    NarePresets.save(name);
                    say("Сохранено: " + NarePresets.safe(name) + NarePresets.EXT, false);
                } catch (Exception ex) {
                    say("Не сохранить: " + ex.getMessage(), true);
                }
            }), true, false));

            list.add(new Action("Загрузить", () -> {
                try {
                    NarePresets.load(current());
                    say("Загружено: " + current(), false);
                } catch (Exception ex) {
                    say("Не загрузить: " + ex.getMessage(), true);
                }
            }, false, true));

            list.add(new Action("Удалить", () -> {
                try {
                    NarePresets.delete(current());
                    selected = -1;
                    say("Удалено", false);
                    refresh();
                } catch (Exception ex) {
                    say("Не удалить: " + ex.getMessage(), true);
                }
            }, false, true));

            list.add(new Action("Папка", () -> openFolder(NarePresets.folder()), false, false));

            return list;
        }
    }

    // ================================================================ картинки

    public static class Images extends NarePickScreen {

        /** true — выбираем прицел, false — небо. */
        private final boolean crosshair;

        public Images(Screen parent, boolean crosshair) {
            super(parent,
                  crosshair ? "Картинка прицела" : "Картинка неба",
                  "Файлы из config/narevisuals/images");
            this.crosshair = crosshair;
        }

        @Override
        protected void reload() {
            items = NarePresets.images();
            String chosen = crosshair ? Config.get().crosshairImage : Config.get().skyImage;
            if (selected < 0 && chosen != null && !chosen.isEmpty()) {
                int index = items.indexOf(chosen);
                if (index >= 0) selected = index;
            }
        }

        @Override
        protected List<Action> buttons() {
            List<Action> list = new ArrayList<>();

            list.add(new Action("Выбрать", () -> {
                Config cfg = Config.get();
                if (crosshair) {
                    cfg.crosshairImage = current();
                    cfg.customCrosshair = true;
                    NareCrosshair.reload();
                } else {
                    cfg.skyImage = current();
                    cfg.skyPreset = "";
                    cfg.customSky = true;
                    NareSky.reload();
                }
                Config.save();
                say("Выбрано: " + current(), false);
            }, true, true));

            list.add(new Action("Без картинки", () -> {
                Config cfg = Config.get();
                if (crosshair) {
                    cfg.crosshairImage = "";
                    NareCrosshair.reload();
                } else {
                    cfg.skyImage = "";
                    NareSky.reload();
                }
                Config.save();
                selected = -1;
                say("Картинка убрана", false);
            }, false, false));

            list.add(new Action("Папка", () -> openFolder(Config.images()), false, false));
            list.add(new Action("Обновить", this::refresh, false, false));

            return list;
        }
    }
}
