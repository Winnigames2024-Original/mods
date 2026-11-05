package com.winnigames2024.forge_narevisuals.client;

import com.winnigames2024.forge_narevisuals.Config;
import net.minecraft.client.gui.GuiGraphics;
import net.minecraft.client.gui.screens.Screen;
import net.minecraft.network.chat.Component;

import java.util.List;

/**
 * Панель: кто сейчас в сети с этим же модом, телепорт к ним и возврат
 * сломанного.
 *
 * Всё здесь решает сервер: и список, и телепорт, и восстановление. Без
 * плагина панель честно пишет, что ей не с кем разговаривать, а не делает
 * вид, что работает.
 */
public class NarePanelScreen extends Screen {

    private final Screen parent;

    private int px, py, pw, ph, headH, footH, rowH;
    private int scroll;
    private String said = "";

    public NarePanelScreen(Screen parent) {
        super(Component.literal("Панель"));
        this.parent = parent;
    }

    private void measure() {
        pw = Math.min(420, Math.max(240, this.width - 20));
        ph = Math.min(300, Math.max(150, this.height - 20));
        px = (this.width - pw) / 2;
        py = (this.height - ph) / 2;
        headH = ph < 200 ? 42 : 54;
        footH = ph < 200 ? 34 : 44;
        rowH = 16;
    }

    private int rows() {
        return Math.max(1, (ph - headH - footH) / rowH);
    }

    private static boolean inside(double mx, double my, int x, int y, int w, int h) {
        return mx >= x && mx <= x + w && my >= y && my <= y + h;
    }

    // ---------------------------------------------------------------- мышь

    @Override
    public boolean mouseScrolled(double mx, double my, double sx, double sy) {
        measure();
        if (sy == 0) return true;

        int max = Math.max(0, NareNet.modded().size() - rows());
        scroll = Math.max(0, Math.min(max, scroll - (int) Math.signum(sy)));
        return true;
    }

    @Override
    public boolean mouseClicked(double mx, double my, int button) {
        measure();

        List<String> players = NareNet.modded();
        int limit = Math.min(players.size() - scroll, rows());

        for (int i = 0; i < limit; i++) {
            int y = py + headH + i * rowH;
            if (!inside(mx, my, px + 12, y, pw - 24, rowH - 3)) continue;

            String name = players.get(i + scroll);
            if (name.equals(me())) { said = "Это вы."; return true; }

            NareNet.teleport(name);
            said = "Попросил сервер перенести к " + name;
            return true;
        }

        // вернуть сломанное
        int by = py + ph - 30;
        if (inside(mx, my, px + 12, by, 120, 20)) { ask(1); return true; }
        if (inside(mx, my, px + 138, by, 120, 20)) { ask(Config.get().restoreBatch); return true; }

        if (inside(mx, my, px + pw - 94, by, 84, 20)) { onClose(); return true; }

        return super.mouseClicked(mx, my, button);
    }

    private void ask(int count) {
        if (!NareNet.plugged()) { said = "Нужен плагин на сервере."; return; }

        NareNet.restore(count);
        said = "Попросил вернуть: " + count;
    }

    private static String me() {
        var mc = net.minecraft.client.Minecraft.getInstance();
        return mc.player == null ? "" : mc.player.getGameProfile().getName();
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

        NareTheme.bold(g, font, "Свои на сервере", px + 14, py + 11, NareTheme.TEXT);

        String hint = NareNet.plugged()
                ? "Строка — телепорт к игроку"
                : "Плагина на сервере нет — панель пустая";
        g.drawString(font, hint, px + 14, py + 23, NareTheme.DIM, false);

        list(g, mouseX, mouseY);
        foot(g, mouseX, mouseY);
    }

    private void list(GuiGraphics g, int mx, int my) {
        List<String> players = NareNet.modded();

        if (players.isEmpty()) {
            g.drawString(font, NareNet.plugged() ? "больше никого с модом" : "никого",
                    px + 18, py + headH + 3, NareTheme.DIM, false);
            return;
        }

        int limit = Math.min(players.size() - scroll, rows());
        String self = me();

        for (int i = 0; i < limit; i++) {
            String name = players.get(i + scroll);
            int y = py + headH + i * rowH;

            boolean hover = inside(mx, my, px + 12, y, pw - 24, rowH - 3);
            boolean own = name.equals(self);

            if (hover && !own)
                NareTheme.roundRect(g, px + 12, y, pw - 24, rowH - 3, 4, 0x12FFFFFF);

            // точка — тот же значок, что и в списке «Игроки рядом»
            NareTheme.roundRect(g, px + 18, y + 4, 6, 6, 2,
                    NareTheme.alpha(NareTheme.accent(), 0.9f));

            g.drawString(font, name, px + 30, y + 3,
                    own ? NareTheme.DIM : (hover ? NareTheme.TEXT : 0xFFD2DAE8), false);

            if (own) {
                String tag = "это вы";
                g.drawString(font, tag, px + pw - 24 - font.width(tag), y + 3,
                        NareTheme.DIM, false);
            } else if (NareNet.fun(name)) {
                String tag = "шутки";
                g.drawString(font, tag, px + pw - 24 - font.width(tag), y + 3,
                        NareTheme.MUTED, false);
            }
        }
    }

    private void foot(GuiGraphics g, int mx, int my) {
        int by = py + ph - 30;

        chip(g, px + 12, by, 120, "Вернуть блок", mx, my);
        chip(g, px + 138, by, 120, "Вернуть " + Config.get().restoreBatch, mx, my);

        if (!said.isEmpty())
            g.drawString(font, said, px + 12, by - 12, NareTheme.DIM, false);

        int dx = px + pw - 94;
        boolean done = inside(mx, my, dx, by, 84, 20);
        int accent = NareTheme.accent();
        NareTheme.roundRect(g, dx, by, 84, 20, 5, done ? accent : NareTheme.alpha(accent, 0.8f));
        NareTheme.bold(g, font, "Готово", dx + (84 - font.width("Готово")) / 2, by + 6, 0xFF0B0E16);
    }

    private void chip(GuiGraphics g, int x, int y, int w, String text, int mx, int my) {
        boolean hover = inside(mx, my, x, y, w, 20);

        NareTheme.roundRect(g, x, y, w, 20, 5, hover ? 0x1EFFFFFF : 0x12FFFFFF);
        NareTheme.outline(g, x, y, w, 20, 5, 0x22FFFFFF);

        String cut = font.width(text) > w - 10 ? text.substring(0, 12) : text;
        g.drawString(font, cut, x + (w - font.width(cut)) / 2, y + 6,
                hover ? NareTheme.TEXT : NareTheme.MUTED, false);
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
