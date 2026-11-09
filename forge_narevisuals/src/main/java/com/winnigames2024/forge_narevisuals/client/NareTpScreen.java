package com.winnigames2024.forge_narevisuals.client;

import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.GuiGraphics;
import net.minecraft.client.gui.screens.Screen;
import net.minecraft.client.multiplayer.PlayerInfo;
import net.minecraft.network.chat.Component;

import java.util.ArrayList;
import java.util.List;

/**
 * Экран телепорта (кнопка G): список игроков в сети, клик — перенестись к выбранному.
 * Сам перенос делает сервер командой /ntp, поэтому прав игроку не нужно.
 */
public class NareTpScreen extends Screen {

    private static final int BAR = 26;
    private static final int ROW = 22;

    private final Screen parent;
    private final List<String> names = new ArrayList<>();
    private final List<int[]> rows = new ArrayList<>();   // x, y, w, h, индекс имени
    private int scroll;
    private int px, py, pw, ph, listH;

    public NareTpScreen() { this(null); }

    public NareTpScreen(Screen parent) {
        super(Component.literal("Телепорт"));
        this.parent = parent;
    }

    @Override
    protected void init() { reload(); }

    @Override
    public boolean isPauseScreen() { return false; }

    private void reload() {
        names.clear();
        Minecraft mc = Minecraft.getInstance();
        if (mc.getConnection() == null || mc.player == null) return;
        String me = mc.player.getGameProfile().getName();
        for (PlayerInfo p : mc.getConnection().getOnlinePlayers()) {
            String n = p.getProfile().getName();
            if (n != null && !n.equalsIgnoreCase(me)) names.add(n);
        }
        names.sort(String.CASE_INSENSITIVE_ORDER);
    }

    private void measure() {
        pw = Math.min(320, Math.max(240, this.width - 60));
        int maxList = Math.max(ROW, this.height - 150);
        listH = Math.max(ROW, Math.min(Math.max(1, names.size()) * ROW, maxList));
        ph = BAR + 10 + listH + 14;
        px = (this.width - pw) / 2;
        py = (this.height - ph) / 2;
    }

    @Override
    public void render(GuiGraphics g, int mouseX, int mouseY, float pt) {
        super.render(g, mouseX, mouseY, pt);
        measure();

        g.fill(0, 0, this.width, this.height, 0xB4000000);
        NareTheme.roundRect(g, px + 3, py + 4, pw, ph, 9, 0x40000000);
        NareTheme.roundRect(g, px, py, pw, ph, 8, 0xF60E1119);
        NareTheme.outline(g, px, py, pw, ph, 8, 0x3AFFFFFF);
        NareTheme.roundRect(g, px + 1, py + 1, pw - 2, BAR, 7, NareTheme.alpha(NareTheme.accent(), 0.20f));
        g.fill(px + 1, py + BAR, px + pw - 1, py + BAR + 1, 0x33FFFFFF);
        NareTheme.bold(g, font, "Телепорт к игроку", px + 12, py + 9, NareTheme.TEXT);

        rows.clear();
        int listTop = py + BAR + 8;
        int listBot = listTop + listH;

        g.enableScissor(px + 1, listTop, px + pw - 1, listBot);
        if (names.isEmpty()) {
            g.drawString(font, "Нет других игроков в сети", px + 14, listTop + 6, NareTheme.DIM, false);
        } else {
            int top = listTop - scroll;
            for (int i = 0; i < names.size(); i++) {
                int ry = top + i * ROW;
                if (ry + ROW - 4 < listTop || ry > listBot) continue;
                boolean hover = mouseX >= px + 8 && mouseX <= px + pw - 8
                        && mouseY >= Math.max(ry, listTop) && mouseY <= Math.min(ry + ROW - 4, listBot);
                NareTheme.roundRect(g, px + 8, ry, pw - 16, ROW - 4, 5,
                        hover ? NareTheme.alpha(NareTheme.accent(), 0.30f) : 0x12FFFFFF);
                g.drawString(font, names.get(i), px + 16, ry + (ROW - 4 - 8) / 2,
                        hover ? 0xFFFFFFFF : 0xFFD2DAE8, false);
                rows.add(new int[] { px + 8, ry, pw - 16, ROW - 4, i });
            }
        }
        g.disableScissor();

        g.drawString(font, "клик — перенестись, Esc — закрыть", px + 12, listBot + 3, NareTheme.DIM, false);
    }

    @Override
    public boolean mouseClicked(double mx, double my, int button) {
        int listTop = py + BAR + 8;
        int listBot = listTop + listH;
        if (my >= listTop && my <= listBot) {
            for (int[] r : rows) {
                if (mx >= r[0] && mx <= r[0] + r[2] && my >= r[1] && my <= r[1] + r[3]) {
                    teleport(names.get(r[4]));
                    return true;
                }
            }
        }
        return super.mouseClicked(mx, my, button);
    }

    @Override
    public boolean mouseScrolled(double mx, double my, double dx, double dy) {
        int max = Math.max(0, names.size() * ROW - listH);
        scroll -= (int) (dy * ROW);
        if (scroll < 0) scroll = 0;
        if (scroll > max) scroll = max;
        return true;
    }

    private void teleport(String name) {
        Minecraft mc = Minecraft.getInstance();
        if (mc.getConnection() != null) mc.getConnection().sendCommand("ntp " + name);
        if (mc.gui != null) mc.gui.setOverlayMessage(Component.literal("Телепорт к " + name), false);
        onClose();
    }

    @Override
    public void onClose() {
        if (minecraft != null) minecraft.setScreen(parent);
        else super.onClose();
    }
}
