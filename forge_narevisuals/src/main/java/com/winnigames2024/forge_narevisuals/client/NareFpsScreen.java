package com.winnigames2024.forge_narevisuals.client;

import com.winnigames2024.forge_narevisuals.Config;
import net.minecraft.client.gui.GuiGraphics;
import net.minecraft.client.gui.screens.Screen;
import net.minecraft.network.chat.Component;

import java.util.ArrayList;
import java.util.List;
import java.util.function.BooleanSupplier;
import java.util.function.IntConsumer;
import java.util.function.IntSupplier;
import java.util.function.IntFunction;

/**
 * Окно FPS+. Сверху — пороги: сколько чего должно набраться, прежде чем мод
 * начнёт прятать. Ниже — список видов, которые можно выключить насовсем.
 */
public class NareFpsScreen extends Screen {

    private static final int ROW_H = 20;

    private final Screen parent;

    private int scroll;
    private int px, py, pw, ph;

    private Integer dragging;

    private static final class Row {
        enum Kind { LABEL, TOGGLE, SLIDER, TYPE }

        final Kind kind;
        final String title;
        final String hint;

        BooleanSupplier state;
        Runnable act;

        IntSupplier value;
        IntConsumer set;
        IntFunction<String> text;
        int min, max;

        String typeId;

        Row(Kind kind, String title, String hint) {
            this.kind = kind;
            this.title = title;
            this.hint = hint;
        }
    }

    public NareFpsScreen(Screen parent) {
        super(Component.literal("FPS+"));
        this.parent = parent;
    }

    // ------------------------------------------------------------- строки

    private List<Row> rows() {
        Config cfg = Config.get();
        var list = new ArrayList<Row>();

        Row top = new Row(Row.Kind.TOGGLE, "FPS+", NareFps.state());
        top.state = () -> cfg.fpsPlus;
        top.act = () -> { cfg.fpsPlus = !cfg.fpsPlus; Config.save(); };
        list.add(top);

        list.add(new Row(Row.Kind.LABEL, "Прятать, когда их много", null));

        list.add(slider("Динамит", "Взрыв всё равно будет — просто не рисуем",
                0, 200, () -> cfg.fpsTntOver, v -> { cfg.fpsTntOver = v; Config.save(); }));

        list.add(slider("Мобы", "Ближние остаются, дальние уходят",
                0, 300, () -> cfg.fpsMobsOver, v -> { cfg.fpsMobsOver = v; Config.save(); }));

        list.add(slider("Выпавшие предметы", null,
                0, 400, () -> cfg.fpsItemsOver, v -> { cfg.fpsItemsOver = v; Config.save(); }));

        list.add(slider("Шарики опыта", null,
                0, 300, () -> cfg.fpsOrbsOver, v -> { cfg.fpsOrbsOver = v; Config.save(); }));

        list.add(new Row(Row.Kind.LABEL, "Взрывы", null));

        Row boom = new Row(Row.Kind.TOGGLE, "Беречь кадры на взрывах",
                "Предел частиц и одновременных «бумов»");
        boom.state = () -> cfg.fpsExplosions;
        boom.act = () -> { cfg.fpsExplosions = !cfg.fpsExplosions; Config.save(); };
        list.add(boom);

        Row budget = slider("Частиц за тик", "Взрыв всё равно виден",
                0, 3000, () -> cfg.fpsParticleBudget,
                v -> { cfg.fpsParticleBudget = v; Config.save(); });
        budget.text = NareFps::budgetText;
        list.add(budget);

        Row booms = slider("Звуков взрыва разом", "Лишние глушим",
                0, 16, () -> cfg.fpsBoomsAtOnce,
                v -> { cfg.fpsBoomsAtOnce = v; Config.save(); });
        booms.text = NareFps::boomText;
        list.add(booms);

        list.add(new Row(Row.Kind.LABEL, "Ещё", null));

        Row range = slider("Дальше этого не рисовать", "Для всего разом",
                0, 128, () -> cfg.fpsRange, v -> { cfg.fpsRange = v; Config.save(); });
        range.text = NareFps::rangeText;
        list.add(range);

        Row particles = new Row(Row.Kind.TOGGLE, "Меньше частиц",
                "Ванильная настройка на минимум, пока FPS+ включён");
        particles.state = () -> cfg.fpsLessParticles;
        particles.act = () -> { cfg.fpsLessParticles = !cfg.fpsLessParticles; Config.save(); };
        list.add(particles);

        Row players = new Row(Row.Kind.TOGGLE, "Прятать и игроков",
                "По умолчанию игроков не трогаем");
        players.state = () -> cfg.fpsHidePlayers;
        players.act = () -> { cfg.fpsHidePlayers = !cfg.fpsHidePlayers; Config.save(); };
        list.add(players);

        list.add(new Row(Row.Kind.LABEL, "Не рисовать совсем", null));

        for (int i = 0; i < NareFps.TYPES.length; i++) {
            Row row = new Row(Row.Kind.TYPE, NareFps.TYPE_TITLES[i], null);
            row.typeId = NareFps.TYPES[i];
            list.add(row);
        }

        list.add(new Row(Row.Kind.LABEL, "Сейчас вокруг: " + NareFps.counts(), null));

        String dropped = NareFps.droppedText();
        if (!dropped.isEmpty()) list.add(new Row(Row.Kind.LABEL, dropped, null));

        return list;
    }

    private static Row slider(String title, String hint, int min, int max,
                              IntSupplier value, IntConsumer set) {
        Row row = new Row(Row.Kind.SLIDER, title, hint);
        row.min = min;
        row.max = max;
        row.value = value;
        row.set = set;
        row.text = NareFps::limitText;
        return row;
    }

    // ---------------------------------------------------------- отрисовка

    private void measure() {
        pw = Math.min(460, Math.max(220, this.width - 20));
        ph = Math.min(400, Math.max(140, this.height - 20));
        px = (this.width - pw) / 2;
        py = (this.height - ph) / 2;
    }

    private int visibleRows() {
        return Math.max(1, (ph - 46 - 30) / ROW_H);
    }

    @Override
    public void render(GuiGraphics g, int mouseX, int mouseY, float partialTick) {
        super.render(g, mouseX, mouseY, partialTick);
        measure();

        g.fill(0, 0, this.width, this.height, 0x99000000);

        NareTheme.roundRect(g, px, py, pw, ph, 8, 0xF40D1018);
        NareTheme.outline(g, px, py, pw, ph, 8, 0x2EFFFFFF);

        NareTheme.bold(g, font, "FPS+", px + 14, py + 13, NareTheme.TEXT);
        g.drawString(font, "рисуем меньше — идёт быстрее", px + 54, py + 14,
                NareTheme.DIM, false);

        var list = rows();
        int shown = visibleRows();
        if (scroll > Math.max(0, list.size() - shown)) scroll = Math.max(0, list.size() - shown);
        if (scroll < 0) scroll = 0;

        int cx = px + 10;
        int cw = pw - 20;
        int limit = Math.min(list.size() - scroll, shown);

        for (int i = 0; i < limit; i++) {
            Row row = list.get(i + scroll);
            int y = py + 46 + i * ROW_H;
            boolean hover = mouseX >= cx && mouseX <= cx + cw && mouseY >= y && mouseY < y + ROW_H - 2;

            if (row.kind == Row.Kind.LABEL) {
                NareTheme.bold(g, font, row.title.toUpperCase(), cx + 4, y + 6,
                        NareTheme.alpha(NareTheme.accent(), 0.85f));
                continue;
            }

            if (hover) NareTheme.roundRect(g, cx, y, cw, ROW_H - 2, 4, 0x12FFFFFF);

            int ty = row.hint == null ? y + 6 : y + 2;
            g.drawString(font, row.title, cx + 6, ty, 0xFFD2DAE8, false);
            if (row.hint != null)
                g.drawString(font, row.hint, cx + 6, y + 12, NareTheme.DIM, false);

            switch (row.kind) {
                case TOGGLE -> lamp(g, cx + cw - 20, y + 6, row.state.getAsBoolean());
                case TYPE -> lamp(g, cx + cw - 20, y + 6, !NareFps.typeOff(row.typeId));
                case SLIDER -> slider(g, row, cx, cw, y);
                default -> { }
            }
        }

        if (list.size() > shown) {
            int trackY = py + 46;
            int trackH = shown * ROW_H - 4;
            int barH = Math.max(14, trackH * shown / list.size());
            int barY = trackY + (trackH - barH) * scroll / Math.max(1, list.size() - shown);

            g.fill(px + pw - 7, trackY, px + pw - 5, trackY + trackH, 0x18FFFFFF);
            g.fill(px + pw - 7, barY, px + pw - 5, barY + barH, 0x55FFFFFF);
        }

        g.drawString(font, "Esc — назад · колесо листает", px + 14, py + ph - 16,
                NareTheme.DIM, false);
    }

    private static void lamp(GuiGraphics g, int x, int y, boolean on) {
        int accent = NareTheme.accent();
        if (on) {
            NareTheme.roundRect(g, x, y, 8, 8, 2, accent);
            NareTheme.roundRect(g, x - 1, y - 1, 10, 10, 3, NareTheme.alpha(accent, 0.25f));
        }
        else NareTheme.outline(g, x, y, 8, 8, 2, 0x55FFFFFF);
    }

    private void slider(GuiGraphics g, Row row, int cx, int cw, int y) {
        int barW = 92;
        int x = cx + cw - barW - 6;
        int barY = y + 8;

        int value = row.value.getAsInt();
        float k = row.max == row.min ? 0f : (float) (value - row.min) / (row.max - row.min);

        g.fill(x, barY, x + barW, barY + 2, 0x33FFFFFF);
        g.fill(x, barY, x + (int) (barW * k), barY + 2, NareTheme.accent());

        int knob = x + (int) (barW * k);
        NareTheme.roundRect(g, knob - 2, barY - 3, 4, 8, 2, NareTheme.accent());

        String text = row.text == null ? String.valueOf(value) : row.text.apply(value);
        g.drawString(font, text, x - font.width(text) - 8, y + 6, NareTheme.MUTED, false);
    }

    // --------------------------------------------------------------- мышь

    @Override
    public boolean mouseClicked(double mouseX, double mouseY, int button) {
        measure();

        var list = rows();
        int shown = visibleRows();
        int limit = Math.min(list.size() - scroll, shown);

        int cx = px + 10;
        int cw = pw - 20;

        for (int i = 0; i < limit; i++) {
            int y = py + 46 + i * ROW_H;
            if (mouseX < cx || mouseX > cx + cw) continue;
            if (mouseY < y || mouseY >= y + ROW_H - 2) continue;

            Row row = list.get(i + scroll);

            switch (row.kind) {
                case TOGGLE -> { if (row.act != null) row.act.run(); }
                case TYPE -> NareFps.flipType(row.typeId);
                case SLIDER -> {
                    dragging = i + scroll;
                    apply(row, mouseX, cx, cw);
                }
                default -> { }
            }
            return true;
        }

        return super.mouseClicked(mouseX, mouseY, button);
    }

    @Override
    public boolean mouseDragged(double mouseX, double mouseY, int button, double dx, double dy) {
        if (dragging != null) {
            var list = rows();
            if (dragging >= 0 && dragging < list.size())
                apply(list.get(dragging), mouseX, px + 10, pw - 20);
            return true;
        }
        return super.mouseDragged(mouseX, mouseY, button, dx, dy);
    }

    @Override
    public boolean mouseReleased(double mouseX, double mouseY, int button) {
        dragging = null;
        return super.mouseReleased(mouseX, mouseY, button);
    }

    private void apply(Row row, double mouseX, int cx, int cw) {
        if (row.kind != Row.Kind.SLIDER) return;

        int barW = 92;
        int x = cx + cw - barW - 6;

        float k = (float) ((mouseX - x) / barW);
        if (k < 0f) k = 0f;
        if (k > 1f) k = 1f;

        row.set.accept(row.min + Math.round(k * (row.max - row.min)));
    }

    @Override
    public boolean mouseScrolled(double mouseX, double mouseY, double sx, double sy) {
        if (sy != 0) scroll -= (int) Math.signum(sy);
        return true;
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
