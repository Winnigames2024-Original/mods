package com.winnigames2024.forge_narevisuals.client;

import com.mojang.math.Axis;
import net.minecraft.client.KeyMapping;
import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.GuiGraphics;
import net.minecraft.client.multiplayer.PlayerInfo;
import net.minecraft.network.chat.Component;
import net.minecraft.world.effect.MobEffectInstance;
import net.minecraft.world.entity.EquipmentSlot;
import net.minecraft.world.item.ItemStack;

import java.util.ArrayList;
import java.util.List;

/** Все элементы HUD в одном месте. */
public final class HudModules {

    private static final int ROW = 18;

    private HudModules() { }

    // ------------------------------------------------------- Сломанные блоки

    /**
     * Что сломали другие игроки с модом. Координаты присылает серверный
     * плагин — без него список всегда пуст, и это честно написано внутри.
     */
    public static class Broken extends HudModule {

        private static final int MAX_ROWS = 4, HEAD = 17, ROW_H = 10;

        public Broken() { super("broken", "Сломанные блоки", 0.735f, 0.47f); }

        @Override public int width(Minecraft mc) { return 150; }

        @Override
        public int height(Minecraft mc) {
            int n = NareNet.marks().size();
            int rows = n == 0 ? 1 : Math.min(MAX_ROWS, n);
            return HEAD + rows * ROW_H + 5;
        }

        @Override
        public void render(GuiGraphics g, Minecraft mc, int x, int y) {
            List<NareNet.Mark> list = com.winnigames2024.forge_narevisuals.Config.get().showMarks
                    ? NareNet.marks() : java.util.Collections.emptyList();

            int w = width(mc), h = height(mc);
            NareTheme.card(g, x, y, w, h);

            NareTheme.text(g, mc.font, "СЛОМАНО", x + 9, y + 5, NareTheme.MUTED);
            NareTheme.rightBold(g, mc.font, String.valueOf(list.size()), x + w - 8, y + 5,
                    list.isEmpty() ? NareTheme.DIM : NareTheme.accent());

            g.fill(x + 9, y + HEAD - 2, x + w - 8, y + HEAD - 1, NareTheme.LINE_SOFT);

            int ry = y + HEAD + 2;

            if (list.isEmpty()) {
                NareTheme.text(g, mc.font,
                        NareNet.plugged() ? "пока ничего" : "нужен плагин на сервере",
                        x + 9, ry, NareTheme.DIM);
                return;
            }

            int shown = Math.min(MAX_ROWS, list.size());
            for (int i = 0; i < shown; i++) {
                NareNet.Mark mark = list.get(i);

                String who = mark.who();
                if (who.length() > 9) who = who.substring(0, 8) + "\u2026";
                NareTheme.text(g, mc.font, who, x + 9, ry, NareTheme.TEXT);

                String where = mark.x() + " " + mark.y() + " " + mark.z();
                NareTheme.rightBold(g, mc.font, where, x + w - 8, ry, NareTheme.MUTED);

                ry += ROW_H;
            }
        }
    }

    // --------------------------------------------------- Стрелка на игрока

    /**
     * Стрелка, которая всё время смотрит на ближайшего игрока. Угол тот же,
     * что считает /near: относительно вашего взгляда, так что «вверх» — это
     * «прямо перед вами».
     */
    public static class Pointer extends HudModule {

        public Pointer() { super("pointer", "Стрелка на игрока", 0.5f, 0.70f); }

        @Override public int width(Minecraft mc) { return 96; }
        @Override public int height(Minecraft mc) { return 56; }

        @Override
        public void render(GuiGraphics g, Minecraft mc, int x, int y) {
            int w = width(mc), h = height(mc);
            NareTheme.card(g, x, y, w, h);

            List<NareNear.Spot> list = NareNear.cached();
            int cx = x + w / 2;
            int cy = y + 21;

            if (list.isEmpty()) {
                arrow(g, cx, cy, 0f, NareTheme.LINE_SOFT);
                String none = "никого рядом";
                g.drawString(mc.font, none, cx - mc.font.width(none) / 2, y + h - 14,
                        NareTheme.MUTED, false);
                return;
            }

            NareNear.Spot spot = list.get(0);
            int color = NareTheme.gradeHigh((int) Math.round(spot.distance()), 17, 49);

            arrow(g, cx, cy, spot.relative(), color);

            String name = spot.name();
            if (name.length() > 12) name = name.substring(0, 11) + "…";
            g.drawString(mc.font, name, cx - mc.font.width(name) / 2, y + h - 22,
                    NareTheme.TEXT, false);

            String far = Math.round(spot.distance()) + "м"
                    + (Math.abs(spot.dy()) >= 2 ? (spot.dy() > 0 ? "  выше" : "  ниже") : "");
            g.drawString(mc.font, far, cx - mc.font.width(far) / 2, y + h - 12, color, false);
        }

        /** Треугольник с хвостом, повёрнутый на нужный угол. */
        private static void arrow(GuiGraphics g, int cx, int cy, float angle, int color) {
            g.pose().pushPose();
            g.pose().translate(cx, cy, 0);
            g.pose().mulPose(Axis.ZP.rotationDegrees(angle));

            for (int i = 0; i < 8; i++)
                g.fill(-i - 1, -10 + i, i + 1, -9 + i, color);

            g.fill(-2, -2, 2, 9, color);

            g.pose().popPose();
        }
    }

    // ------------------------------------------------------------- FPS

    public static class Fps extends HudModule {
        public Fps() { super("fps", "FPS", 0.006f, 0.008f); }

        @Override public boolean needsPlayer() { return false; }

        @Override public int width(Minecraft mc) { return 70; }
        @Override public int height(Minecraft mc) { return ROW; }

        @Override
        public void render(GuiGraphics g, Minecraft mc, int x, int y) {
            int fps = NareHud.measuredFps;
            NareTheme.stat(g, mc.font, x, y, width(mc), height(mc),
                    "FPS", String.valueOf(fps), NareTheme.gradeLow(fps, 100, 45));
        }
    }

    // ------------------------------------------------------------- Пинг

    public static class Ping extends HudModule {
        public Ping() { super("ping", "Пинг", 0.006f, 0.046f); }

        @Override public int width(Minecraft mc) { return 70; }
        @Override public int height(Minecraft mc) { return ROW; }

        @Override
        public void render(GuiGraphics g, Minecraft mc, int x, int y) {
            int ping = ping(mc);
            NareTheme.stat(g, mc.font, x, y, width(mc), height(mc),
                    "MS", ping < 0 ? "—" : String.valueOf(ping),
                    ping < 0 ? NareTheme.MUTED : NareTheme.gradeHigh(ping, 80, 160));
        }

        private static int ping(Minecraft mc) {
            if (mc.player == null || mc.getConnection() == null) return -1;
            PlayerInfo info = mc.getConnection().getPlayerInfo(mc.player.getUUID());
            return info == null ? -1 : info.getLatency();
        }
    }

    // ------------------------------------------------------------- Координаты

    public static class Coords extends HudModule {
        public Coords() { super("coords", "Координаты", 0.006f, 0.084f); }

        @Override public int width(Minecraft mc) { return 146; }
        @Override public int height(Minecraft mc) { return ROW; }

        @Override
        public void render(GuiGraphics g, Minecraft mc, int x, int y) {
            if (mc.player == null) return;

            int w = width(mc), h = height(mc);
            NareTheme.card(g, x, y, w, h);

            int ty = y + (h - 8) / 2;
            int cx = x + 9;

            cx = axis(g, mc, cx, ty, "X", mc.player.getX());
            cx = axis(g, mc, cx, ty, "Y", mc.player.getY());
            axis(g, mc, cx, ty, "Z", mc.player.getZ());

            String facing = mc.player.getDirection().getName().toUpperCase();
            NareTheme.rightBold(g, mc.font, facing, x + w - 8, ty, NareTheme.accent());
        }

        private static int axis(GuiGraphics g, Minecraft mc, int x, int y, String name, double value) {
            NareTheme.text(g, mc.font, name, x, y, NareTheme.DIM);
            x += mc.font.width(name) + 2;

            String s = String.valueOf(Math.round(value));
            NareTheme.bold(g, mc.font, s, x, y, NareTheme.TEXT);
            return x + mc.font.width(s) + 7;
        }
    }

    // ------------------------------------------------------------- CPS

    public static class Cps extends HudModule {
        public Cps() { super("cps", "Клики в секунду", 0.006f, 0.122f); }

        @Override public int width(Minecraft mc) { return 92; }
        @Override public int height(Minecraft mc) { return ROW; }

        @Override
        public void render(GuiGraphics g, Minecraft mc, int x, int y) {
            int w = width(mc), h = height(mc);
            NareTheme.card(g, x, y, w, h);

            int ty = y + (h - 8) / 2;
            NareTheme.text(g, mc.font, "CPS", x + 9, ty, NareTheme.MUTED);

            String right = String.valueOf(InputTracker.rightCps());
            String left = String.valueOf(InputTracker.leftCps());

            int rx = x + w - 8;
            NareTheme.rightBold(g, mc.font, right, rx, ty, NareTheme.MUTED);
            rx -= mc.font.width(right) + 6;

            NareTheme.right(g, mc.font, "|", rx, ty, NareTheme.DIM);
            rx -= mc.font.width("|") + 6;

            NareTheme.rightBold(g, mc.font, left, rx, ty, NareTheme.TEXT);
        }
    }

    // ------------------------------------------------------------- Клавиши

    public static class Keystrokes extends HudModule {
        private static final int CELL = 22;
        private static final int GAP = 3;

        public Keystrokes() { super("keystrokes", "Клавиши", 0.02f, 0.52f); }

        @Override public int width(Minecraft mc) { return CELL * 3 + GAP * 2; }
        @Override public int height(Minecraft mc) { return CELL * 4 + GAP * 3; }

        @Override
        public void render(GuiGraphics g, Minecraft mc, int x, int y) {
            int step = CELL + GAP;
            int full = width(mc);

            key(g, mc, x + step, y, CELL, CELL, "W", mc.options.keyUp);
            key(g, mc, x, y + step, CELL, CELL, "A", mc.options.keyLeft);
            key(g, mc, x + step, y + step, CELL, CELL, "S", mc.options.keyDown);
            key(g, mc, x + step * 2, y + step, CELL, CELL, "D", mc.options.keyRight);

            key(g, mc, x, y + step * 2, full, CELL, "␣", mc.options.keyJump);

            int leftW = (full - GAP) / 2;
            cell(g, mc, x, y + step * 3, leftW, CELL,
                    "L " + InputTracker.leftCps(), mc.options.keyAttack.isDown());
            cell(g, mc, x + leftW + GAP, y + step * 3, full - leftW - GAP, CELL,
                    "R " + InputTracker.rightCps(), mc.options.keyUse.isDown());
        }

        private static void key(GuiGraphics g, Minecraft mc, int x, int y, int w, int h,
                                String label, KeyMapping mapping) {
            cell(g, mc, x, y, w, h, label, mapping.isDown());
        }

        private static void cell(GuiGraphics g, Minecraft mc, int x, int y, int w, int h,
                                 String label, boolean pressed) {
            int r = NareTheme.rounded() ? 3 : 0;

            if (pressed) {
                NareTheme.shadow(g, x, y, w, h);
                NareTheme.roundRect(g, x, y, w, h, r, NareTheme.alpha(NareTheme.accent(), 0.92f));
                g.fillGradient(x + 1, y + 1, x + w - 1, y + h - 1, 0x22FFFFFF, 0x00000000);
                NareTheme.outline(g, x, y, w, h, r, 0x40FFFFFF);
            } else {
                NareTheme.roundRect(g, x, y, w, h, r, NareTheme.BG_SOFT);
                g.fillGradient(x + 1, y + 1, x + w - 1, y + h - 1, 0x00000000, 0x28000000);
                NareTheme.outline(g, x, y, w, h, r, NareTheme.LINE_SOFT);
            }

            int color = pressed ? 0xFF0B0E16 : NareTheme.TEXT;
            int tx = x + (w - mc.font.width(label)) / 2;
            int ty = y + (h - 8) / 2;

            if (pressed) NareTheme.bold(g, mc.font, label, tx, ty, color);
            else NareTheme.text(g, mc.font, label, tx, ty, color);
        }
    }

    // ------------------------------------------------------------- Броня

    public static class Armor extends HudModule {
        private static final EquipmentSlot[] SLOTS = {
                EquipmentSlot.HEAD, EquipmentSlot.CHEST, EquipmentSlot.LEGS,
                EquipmentSlot.FEET, EquipmentSlot.MAINHAND
        };

        public Armor() { super("armor", "Броня", 0.9f, 0.34f); }

        @Override public int width(Minecraft mc) { return 30; }
        @Override public int height(Minecraft mc) { return SLOTS.length * 26 + 6; }

        @Override
        public void render(GuiGraphics g, Minecraft mc, int x, int y) {
            if (mc.player == null) return;

            int w = width(mc);
            NareTheme.card(g, x, y, w, height(mc));

            int row = y + 3;
            for (EquipmentSlot slot : SLOTS) {
                ItemStack stack = mc.player.getItemBySlot(slot);

                if (stack.isEmpty()) {
                    NareTheme.roundRect(g, x + 7, row + 3, 16, 16, 2, 0x18FFFFFF);
                } else {
                    g.renderItem(stack, x + 7, row + 3);
                    g.renderItemDecorations(mc.font, stack, x + 7, row + 3);

                    if (stack.isDamageableItem() && stack.getMaxDamage() > 0) {
                        float left = 1f - stack.getDamageValue() / (float) stack.getMaxDamage();
                        NareTheme.bar(g, x + 7, row + 21, 16, 2, left,
                                left > 0.5f ? NareTheme.GOOD : left > 0.2f ? NareTheme.WARN : NareTheme.BAD);
                    }
                }
                row += 26;
            }
        }
    }

    // ------------------------------------------------------------- Эффекты

    public static class Effects extends HudModule {
        public Effects() { super("effects", "Эффекты", 0.78f, 0.04f); }

        @Override public int width(Minecraft mc) { return 126; }

        @Override
        public int height(Minecraft mc) {
            int rows = 0;

            if (mc.player != null)
                for (MobEffectInstance effect : mc.player.getActiveEffects())
                    if (NareLook.show(effect)) rows++;

            return Math.max(ROW, rows * 13 + 8);
        }

        @Override
        public void render(GuiGraphics g, Minecraft mc, int x, int y) {
            if (mc.player == null) return;

            List<MobEffectInstance> effects = new ArrayList<>();
            for (MobEffectInstance effect : mc.player.getActiveEffects())
                if (NareLook.show(effect)) effects.add(effect);

            if (effects.isEmpty()) return;

            int w = width(mc);
            NareTheme.card(g, x, y, w, height(mc));

            int row = y + 5;
            for (MobEffectInstance effect : effects) {
                String name = Component.translatable(effect.getDescriptionId()).getString();
                String level = NareTheme.roman(effect.getAmplifier());
                if (!level.isEmpty()) name = name + " " + level;

                String time = NareTheme.duration(effect.getDuration());
                int timeWidth = mc.font.width(time);

                String trimmed = name;
                int room = w - 16 - timeWidth - 6;
                while (mc.font.width(trimmed) > room && trimmed.length() > 2)
                    trimmed = trimmed.substring(0, trimmed.length() - 1);

                boolean ending = effect.getDuration() >= 0 && effect.getDuration() < 200;

                NareTheme.text(g, mc.font, trimmed, x + 9, row, NareTheme.TEXT);
                NareTheme.rightBold(g, mc.font, time, x + w - 8, row,
                        ending ? NareTheme.BAD : NareTheme.MUTED);
                row += 13;
            }
        }
    }

    // ------------------------------------------------------------- Маркер попадания

    public static class HitMarker extends HudModule {
        private static final long LIFETIME = 360L;

        public HitMarker() { super("hitmarker", "Маркер попадания", 0.5f, 0.5f); }

        @Override public boolean movable() { return false; }
        @Override public boolean centered() { return true; }

        @Override public int width(Minecraft mc) { return 20; }
        @Override public int height(Minecraft mc) { return 20; }

        @Override
        public void render(GuiGraphics g, Minecraft mc, int x, int y) {
            long passed = System.currentTimeMillis() - InputTracker.lastHitAt;
            if (InputTracker.lastHitAt == 0 || passed > LIFETIME) return;

            float t = passed / (float) LIFETIME;
            float fade = 1f - t * t;              // гаснет мягко, а не линейно
            int spread = Math.round(2f + t * 3f); // и чуть разлетается в стороны

            int cx = x + 10;
            int cy = y + 10;

            int outer = NareTheme.alpha(0xFF000000, fade * 0.55f);
            int inner = NareTheme.alpha(0xFFFFFFFF, fade);

            for (int i = 2 + spread; i <= 6 + spread; i++) {
                tick(g, cx, cy, i, 1, outer);
            }
            for (int i = 2 + spread; i <= 6 + spread; i++) {
                tick(g, cx, cy, i, 0, inner);
            }
        }

        /** Четыре диагональных уголка маркера. */
        private static void tick(GuiGraphics g, int cx, int cy, int i, int pad, int color) {
            g.fill(cx - i - pad, cy - i - pad, cx - i + 1 + pad, cy - i + 1 + pad, color);
            g.fill(cx + i - 1 - pad, cy - i - pad, cx + i + pad, cy - i + 1 + pad, color);
            g.fill(cx - i - pad, cy + i - 1 - pad, cx - i + 1 + pad, cy + i + pad, color);
            g.fill(cx + i - 1 - pad, cy + i - 1 - pad, cx + i + pad, cy + i + pad, color);
        }
    }

    // ------------------------------------------------------------- Игроки рядом

    /**
     * Тот же список, что печатает /near, но всегда на экране.
     * Нужен там, где команду набрать некогда или чат занят.
     *
     * Ритм карточки тот же, что у FPS и координат: подпись слева приглушённо,
     * значение справа жирным, отступы 9 и 8. Стоит справа по центру — сверху
     * справа вылезают ванильные уведомления и накрывали заголовок.
     */
    public static class Near extends HudModule {

        private static final int MAX_ROWS = 5;
        private static final int HEAD = 17;
        private static final int ROW_H = 10;

        public Near() { super("players", "Игроки рядом", 0.735f, 0.30f); }

        @Override public int width(Minecraft mc) { return 134; }

        @Override
        public int height(Minecraft mc) {
            int n = NareNear.cached().size();
            if (n == 0) return HEAD + ROW_H + 5;

            int rows = Math.min(MAX_ROWS, n) + (n > MAX_ROWS ? 1 : 0);
            return HEAD + rows * ROW_H + 5;
        }

        @Override
        public void render(GuiGraphics g, Minecraft mc, int x, int y) {
            List<NareNear.Spot> list = NareNear.cached();

            int w = width(mc), h = height(mc);
            NareTheme.card(g, x, y, w, h);

            NareTheme.text(g, mc.font, "РЯДОМ", x + 9, y + 5, NareTheme.MUTED);
            NareTheme.rightBold(g, mc.font, list.size() + " / " + NareNear.online(),
                    x + w - 8, y + 5, list.isEmpty() ? NareTheme.DIM : NareTheme.accent());

            g.fill(x + 9, y + HEAD - 2, x + w - 8, y + HEAD - 1, NareTheme.LINE_SOFT);

            int ry = y + HEAD + 2;

            if (list.isEmpty()) {
                NareTheme.text(g, mc.font, "никого рядом", x + 9, ry, NareTheme.DIM);
                return;
            }

            int shown = Math.min(MAX_ROWS, list.size());
            for (int i = 0; i < shown; i++) {
                NareNear.Spot spot = list.get(i);

                NareTheme.bold(g, mc.font, NareNear.arrow(spot.relative()),
                        x + 9, ry, NareTheme.accent());

                boolean ours = NareNet.hasMod(spot.name());

                String name = spot.name();
                int limit = ours ? 9 : 10;
                if (name.length() > limit) name = name.substring(0, limit - 1) + "\u2026";
                NareTheme.text(g, mc.font, name, x + 18, ry, NareTheme.TEXT);

                // значок: у этого игрока тоже стоит мод
                if (ours) {
                    int bx = x + 18 + mc.font.width(name) + 3;
                    NareTheme.roundRect(g, bx, ry - 1, 7, 7, 2,
                            NareTheme.alpha(NareTheme.accent(), 0.9f));
                }

                int distance = (int) Math.round(spot.distance());
                NareTheme.rightBold(g, mc.font, distance + " \u0431\u043b", x + w - 8, ry,
                        NareTheme.gradeHigh(distance, 17, 49));

                ry += ROW_H;
            }

            if (list.size() > shown)
                NareTheme.text(g, mc.font, "\u0438 \u0435\u0449\u0451 " + (list.size() - shown),
                        x + 18, ry, NareTheme.DIM);
        }
    }
    // ------------------------------------------------------------- Голос

    /**
     * Кто говорит рядом. Сверху — своя полоска микрофона, чтобы было видно,
     * что вас слышно, ещё до того как кто-то ответит.
     */
    public static class Voice extends HudModule {

        private static final int MAX_ROWS = 4, HEAD = 17, ROW_H = 10;

        public Voice() { super("voice", "Голосовой чат", 0.006f, 0.47f); }

        @Override public int width(Minecraft mc) { return 132; }

        @Override
        public int height(Minecraft mc) {
            int rows = Math.max(1, Math.min(MAX_ROWS, NareVoice.talkers().size()));
            return HEAD + rows * ROW_H + 5;
        }

        @Override
        public void render(GuiGraphics g, Minecraft mc, int x, int y) {
            List<String> list = NareVoice.talkers();
            boolean me = NareVoice.sending();

            int w = width(mc), h = height(mc);
            NareTheme.card(g, x, y, w, h);

            NareTheme.text(g, mc.font, "ГОЛОС", x + 9, y + 5, NareTheme.MUTED);

            // своя полоска: заполняется громкостью, пока держите клавишу
            int bx = x + w - 8 - 34, by = y + 7;
            NareTheme.roundRect(g, bx, by, 34, 4, 2, NareTheme.LINE_SOFT);
            if (me) {
                int fill = Math.max(2, Math.round(34 * Math.min(1f, NareVoice.level())));
                NareTheme.roundRect(g, bx, by, fill, 4, 2, NareTheme.accent());
            }

            g.fill(x + 9, y + HEAD - 2, x + w - 8, y + HEAD - 1, NareTheme.LINE_SOFT);

            int ry = y + HEAD + 2;

            if (list.isEmpty()) {
                String why = !NareVoice.running()
                        ? (NareVoice.trouble().isEmpty() ? "выключен" : NareVoice.trouble())
                        : (me ? "вас слышно" : "тихо");
                NareTheme.text(g, mc.font, why, x + 9, ry, NareTheme.DIM);
                return;
            }

            int shown = Math.min(MAX_ROWS, list.size());
            for (int i = 0; i < shown; i++) {
                String who = list.get(i);
                if (who.length() > 14) who = who.substring(0, 13) + "\u2026";

                NareTheme.roundRect(g, x + 9, ry + 1, 5, 5, 2, NareTheme.accent());
                NareTheme.text(g, mc.font, who, x + 19, ry, NareTheme.TEXT);

                ry += ROW_H;
            }

            if (list.size() > shown)
                NareTheme.text(g, mc.font, "\u0438 \u0435\u0449\u0451 " + (list.size() - shown),
                        x + 19, ry, NareTheme.DIM);
        }
    }
}
