package com.winnigames2024.forge_narevisuals.client;

import com.mojang.blaze3d.platform.InputConstants;
import net.minecraft.client.gui.GuiGraphics;
import net.minecraft.client.gui.screens.Screen;
import net.minecraft.network.chat.Component;
import net.minecraft.resources.ResourceLocation;

import java.util.List;

/**
 * Замена текстур прямо в игре: слева блоки или предметы с поиском,
 * справа картинки из папки images. Выбрал одно, нажал другое — готово,
 * ресурсы перезагружаются сами.
 */
public class NareTextureScreen extends Screen {

    private final Screen parent;

    private String kind = NareTextures.BLOCK;
    private String query = "";

    private List<ResourceLocation> found;
    private List<String> pictures;

    private ResourceLocation chosen;
    private String said = "";

    private int leftScroll, rightScroll;
    private int px, py, pw, ph, headH, footH, rowH, leftW;

    public NareTextureScreen(Screen parent) {
        super(Component.literal("Текстуры"));
        this.parent = parent;
    }

    @Override
    protected void init() {
        NareTextures.ensure();
        refresh();
    }

    private void refresh() {
        found = NareTextures.search(kind, query);
        pictures = NareTextures.pictures();
        leftScroll = 0;
        rightScroll = 0;
    }

    private void measure() {
        pw = Math.min(520, Math.max(260, this.width - 20));
        ph = Math.min(320, Math.max(150, this.height - 20));
        px = (this.width - pw) / 2;
        py = (this.height - ph) / 2;
        headH = ph < 200 ? 44 : 56;
        footH = ph < 200 ? 30 : 40;
        rowH = 14;
        leftW = (pw - 24) * 3 / 5;
    }

    private int rows() {
        return Math.max(1, (ph - headH - footH) / rowH);
    }

    private static boolean inside(double mx, double my, int x, int y, int w, int h) {
        return mx >= x && mx <= x + w && my >= y && my <= y + h;
    }

    // ---------------------------------------------------------------- ввод

    @Override
    public boolean charTyped(char c, int modifiers) {
        if (c >= ' ') {
            query = query + c;
            refresh();
            return true;
        }
        return super.charTyped(c, modifiers);
    }

    @Override
    public boolean keyPressed(int keyCode, int scanCode, int modifiers) {
        if (keyCode == InputConstants.KEY_BACKSPACE && !query.isEmpty()) {
            query = query.substring(0, query.length() - 1);
            refresh();
            return true;
        }
        return super.keyPressed(keyCode, scanCode, modifiers);
    }

    @Override
    public boolean mouseScrolled(double mx, double my, double sx, double sy) {
        measure();
        if (sy == 0) return true;

        int step = (int) -Math.signum(sy);
        if (mx < px + 12 + leftW) {
            leftScroll = clamp(leftScroll + step, found.size());
        } else {
            rightScroll = clamp(rightScroll + step, pictures.size());
        }
        return true;
    }

    private int clamp(int value, int total) {
        int max = Math.max(0, total - rows());
        return Math.max(0, Math.min(max, value));
    }

    @Override
    public boolean mouseClicked(double mx, double my, int button) {
        measure();

        // вкладки
        if (inside(mx, my, px + 12, py + headH - 18, 70, 14)) {
            kind = NareTextures.BLOCK; chosen = null; refresh(); return true;
        }
        if (inside(mx, my, px + 88, py + headH - 18, 84, 14)) {
            kind = NareTextures.ITEM; chosen = null; refresh(); return true;
        }

        // список блоков и предметов
        int limit = Math.min(found.size() - leftScroll, rows());
        for (int i = 0; i < limit; i++) {
            if (inside(mx, my, px + 12, py + headH + i * rowH, leftW, rowH - 2)) {
                chosen = found.get(i + leftScroll);
                said = "";
                return true;
            }
        }

        // список картинок
        int rx = px + 18 + leftW;
        int rw = pw - leftW - 30;
        int pics = Math.min(pictures.size() - rightScroll, rows());
        for (int i = 0; i < pics; i++) {
            if (inside(mx, my, rx, py + headH + i * rowH, rw, rowH - 2)) {
                if (chosen == null) { said = "Сначала выберите блок слева"; return true; }

                String picture = pictures.get(i + rightScroll);
                said = NareTextures.apply(kind, chosen, picture)
                        ? "Заменено: " + chosen.getPath()
                        : "Картинку не прочитать";
                return true;
            }
        }

        // вернуть ванильную
        if (inside(mx, my, px + 12, py + ph - 26, 126, 18)) {
            if (chosen == null) said = "Сначала выберите блок слева";
            else said = NareTextures.clear(kind, chosen)
                    ? "Вернули ванильную" : "Эта текстура и так своя не была";
            return true;
        }

        if (inside(mx, my, px + pw - 94, py + ph - 26, 84, 20)) {
            onClose();
            return true;
        }

        return super.mouseClicked(mx, my, button);
    }

    // ------------------------------------------------------------ рисование

    @Override
    public void render(GuiGraphics g, int mouseX, int mouseY, float partialTick) {
        super.render(g, mouseX, mouseY, partialTick);
        measure();

        g.fill(0, 0, this.width, this.height, 0x99000000);

        NareTheme.roundRect(g, px, py, pw, ph, 8, 0xF40D1018);
        g.fillGradient(px + 1, py + 1, px + pw - 1, py + ph - 1, 0x10FFFFFF, 0x00000000);
        NareTheme.outline(g, px, py, pw, ph, 8, 0x2EFFFFFF);

        NareTheme.bold(g, font, "Свои текстуры", px + 14, py + 11, NareTheme.TEXT);

        String hint = query.isEmpty()
                ? "Просто печатайте — это поиск · подменено: " + NareTextures.count()
                : "Поиск: " + query;
        g.drawString(font, hint, px + 14, py + 23, NareTheme.DIM, false);

        tab(g, px + 12, py + headH - 18, 70, "Блоки", NareTextures.BLOCK.equals(kind), mouseX, mouseY);
        tab(g, px + 88, py + headH - 18, 84, "Предметы", NareTextures.ITEM.equals(kind), mouseX, mouseY);

        left(g, mouseX, mouseY);
        right(g, mouseX, mouseY);
        foot(g, mouseX, mouseY);
    }

    private void tab(GuiGraphics g, int x, int y, int w, String text, boolean on, int mx, int my) {
        boolean hover = inside(mx, my, x, y, w, 14);
        int accent = NareTheme.accent();

        NareTheme.roundRect(g, x, y, w, 14, 4,
                on ? NareTheme.alpha(accent, 0.22f) : (hover ? 0x1AFFFFFF : 0x12FFFFFF));
        g.drawString(font, text, x + (w - font.width(text)) / 2, y + 3,
                on ? accent : NareTheme.MUTED, false);
    }

    private void left(GuiGraphics g, int mx, int my) {
        int limit = Math.min(found.size() - leftScroll, rows());

        for (int i = 0; i < limit; i++) {
            ResourceLocation key = found.get(i + leftScroll);
            int y = py + headH + i * rowH;

            boolean hover = inside(mx, my, px + 12, y, leftW, rowH - 2);
            boolean pick = key.equals(chosen);

            if (pick) NareTheme.roundRect(g, px + 12, y, leftW, rowH - 2, 4,
                    NareTheme.alpha(NareTheme.accent(), 0.22f));
            else if (hover) NareTheme.roundRect(g, px + 12, y, leftW, rowH - 2, 4, 0x12FFFFFF);

            int color = pick ? NareTheme.accent() : (hover ? NareTheme.TEXT : 0xFFC8D2E4);
            g.drawString(font, key.getPath(), px + 18, y + 2, color, false);

            if (NareTextures.replaced(kind, key))
                g.drawString(font, "своя", px + 12 + leftW - font.width("своя") - 6, y + 2,
                        0xFF3FBF7F, false);
        }

        if (found.isEmpty())
            g.drawString(font, "ничего не нашлось", px + 18, py + headH + 2, NareTheme.DIM, false);
    }

    private void right(GuiGraphics g, int mx, int my) {
        int rx = px + 18 + leftW;
        int rw = pw - leftW - 30;

        if (pictures.isEmpty()) {
            g.drawString(font, "положите .png", rx, py + headH + 2, NareTheme.DIM, false);
            g.drawString(font, "в config/narevisuals/images", rx, py + headH + 14, NareTheme.DIM, false);
            return;
        }

        int limit = Math.min(pictures.size() - rightScroll, rows());
        for (int i = 0; i < limit; i++) {
            String picture = pictures.get(i + rightScroll);
            int y = py + headH + i * rowH;

            boolean hover = inside(mx, my, rx, y, rw, rowH - 2);
            if (hover) NareTheme.roundRect(g, rx, y, rw, rowH - 2, 4, 0x12FFFFFF);

            String text = picture.length() > 22 ? picture.substring(0, 21) + "…" : picture;
            g.drawString(font, text, rx + 6, y + 2,
                    hover ? NareTheme.TEXT : NareTheme.MUTED, false);
        }
    }

    private void foot(GuiGraphics g, int mx, int my) {
        boolean hover = inside(mx, my, px + 12, py + ph - 26, 126, 18);
        NareTheme.roundRect(g, px + 12, py + ph - 26, 126, 18, 5, hover ? 0x1EFFFFFF : 0x12FFFFFF);
        NareTheme.outline(g, px + 12, py + ph - 26, 126, 18, 5, 0x22FFFFFF);
        g.drawString(font, "Вернуть ванильную", px + 20, py + ph - 21,
                hover ? NareTheme.TEXT : NareTheme.MUTED, false);

        if (!said.isEmpty())
            g.drawString(font, said, px + 146, py + ph - 21, NareTheme.DIM, false);

        int dx = px + pw - 94, dy = py + ph - 26;
        boolean done = inside(mx, my, dx, dy, 84, 20);
        int accent = NareTheme.accent();
        NareTheme.roundRect(g, dx, dy, 84, 20, 5, done ? accent : NareTheme.alpha(accent, 0.8f));
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
