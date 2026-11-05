package com.winnigames2024.forge_narevisuals.client;

import com.winnigames2024.forge_narevisuals.Config;
import com.winnigames2024.forge_narevisuals.Forge_narevisuals;
import com.winnigames2024.forge_narevisuals.Launch;
import net.minecraft.SharedConstants;
import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.Font;
import net.minecraft.client.gui.GuiGraphics;
import net.minecraft.client.gui.components.AbstractButton;
import net.minecraft.client.gui.components.AbstractWidget;
import net.minecraft.client.gui.components.events.GuiEventListener;
import net.minecraft.client.gui.screens.Screen;
import net.minecraft.client.gui.screens.TitleScreen;
import net.minecraftforge.api.distmarker.Dist;
import net.minecraftforge.client.event.ScreenEvent;
import net.minecraftforge.eventbus.api.SubscribeEvent;
import net.minecraftforge.fml.ModList;
import net.minecraftforge.fml.common.Mod;
import net.minecraftforge.versions.forge.ForgeVersion;

import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;
import java.util.Random;

/**
 * Своё главное меню: плоский список, марка в углу, сведения о сборке справа
 * внизу. Ванильный экран не подменяется — мы прячем его кнопки, отменяем его
 * отрисовку и рисуем всё сами, а по клику дёргаем ту же кнопку. Поэтому пункты
 * сторонних модов (в том числе «Mods» от Forge) остаются на месте.
 *
 * Всё, что видно на экране, настраивается в разделе «Меню».
 */
@Mod.EventBusSubscriber(modid = Forge_narevisuals.MODID, value = Dist.CLIENT)
public final class NareTitleMenu {

    /** Раскладка считается от размера окна: в маленьком всё ужимается. */
    private static final class Layout {
        int markX, markY, markSize;
        float markScale;
        int wordY;
        float wordScale;
        int top, rowH;
        float entryScale;
        boolean mark;
        boolean centered;
        int left;
    }

    private static Layout layout(Screen screen) {
        Config cfg = Config.get();

        Layout l = new Layout();
        boolean small = screen.height < 300;

        l.mark = cfg.menuMark;
        l.centered = cfg.menuCentered;
        l.left = Math.max(6, Math.min(Math.max(6, screen.width - 60), cfg.menuLeft));

        l.markSize = small ? 30 : 52;
        l.markX = small ? 16 : l.left;
        l.markY = small ? 14 : 40;
        l.markScale = small ? 1.7f : 3.0f;

        l.wordScale = small ? 1.0f : 1.5f;
        l.wordY = l.markY + l.markSize + (small ? 5 : 10);

        l.top = l.mark
                ? l.wordY + (int) (9 * l.wordScale) + (small ? 8 : 14)
                : (small ? 18 : 40);

        int count = Math.max(1, ENTRIES.size());
        int reserve = small ? 26 : 34;
        int available = Math.max(30, screen.height - l.top - reserve);

        l.rowH = Math.max(11, Math.min(26, available / count));

        float user = Math.max(0.6f, Math.min(1.6f, cfg.menuScale / 100f));
        l.entryScale = Math.max(0.6f, Math.min(1.8f, (l.rowH / 16f) * user));
        return l;
    }

    /** Звёзды фона: доли экрана и яркость. Считаются один раз. */
    private static final float[][] STARS = new float[220][3];

    static {
        Random rng = new Random(20261105L);
        for (int i = 0; i < STARS.length; i++) {
            STARS[i][0] = rng.nextFloat();
            STARS[i][1] = rng.nextFloat() * 0.62f;
            STARS[i][2] = 0.25f + rng.nextFloat() * 0.75f;
        }
    }

    private static final List<AbstractButton> ENTRIES = new ArrayList<>();
    private static Screen owner;
    private static int hover = -1;
    private static float shift;

    private NareTitleMenu() { }

    private static boolean active(Screen screen) {
        return Launch.visuals()
                && Config.get().customMenu
                && screen instanceof TitleScreen
                && screen == owner;
    }

    // ---------------------------------------------------------------- сбор кнопок

    @SubscribeEvent
    public static void onInit(ScreenEvent.Init.Post event) {
        Screen screen = event.getScreen();
        if (!(screen instanceof TitleScreen)) return;

        ENTRIES.clear();
        owner = null;

        if (!Launch.visuals() || !Config.get().customMenu) return;

        var found = new ArrayList<AbstractButton>();

        for (GuiEventListener child : screen.children()) {
            if (!(child instanceof AbstractButton button)) continue;

            // крупные кнопки меню: мелкие значки и невидимая кнопка копирайта не в счёт
            if (button.getHeight() != 20 || button.getWidth() < 60) continue;
            if (button.getMessage().getString().isBlank()) continue;

            found.add(button);
        }

        found.sort(Comparator.<AbstractButton>comparingInt(AbstractWidget::getY)
                             .thenComparingInt(AbstractWidget::getX));

        for (AbstractButton button : found) button.visible = false;

        ENTRIES.addAll(found);
        owner = screen;
    }

    /** Вернуть ванильные кнопки, если своё меню выключили прямо на этом экране. */
    private static void release() {
        for (AbstractButton button : ENTRIES) button.visible = true;
        ENTRIES.clear();
        owner = null;
    }

    // ---------------------------------------------------------------- клики

    @SubscribeEvent
    public static void onClick(ScreenEvent.MouseButtonPressed.Pre event) {
        if (!active(event.getScreen())) return;
        if (event.getButton() != 0) return;

        int index = rowAt(event.getScreen(), event.getMouseX(), event.getMouseY());
        if (index < 0) return;

        Minecraft.getInstance().getSoundManager().play(
                net.minecraft.client.resources.sounds.SimpleSoundInstance.forUI(
                        net.minecraft.sounds.SoundEvents.UI_BUTTON_CLICK, 1.0F));

        ENTRIES.get(index).onPress();
        event.setCanceled(true);
    }

    /** Левый край строки: слева с отступом или по центру экрана. */
    private static int rowX(Screen screen, Layout l, Font font, String text) {
        if (!l.centered) return l.left;
        int w = (int) (font.width(text) * l.entryScale);
        return Math.max(4, (screen.width - w) / 2);
    }

    private static int rowAt(Screen screen, double mx, double my) {
        Font font = Minecraft.getInstance().font;
        Layout l = layout(screen);

        for (int i = 0; i < ENTRIES.size(); i++) {
            String text = title(ENTRIES.get(i));
            int w = (int) (font.width(text) * l.entryScale);
            int x = rowX(screen, l, font, text);
            int y = l.top + i * l.rowH;

            if (mx >= x - 10 && mx <= x + w + 10 && my >= y - 3 && my <= y + l.rowH - 3)
                return i;
        }
        return -1;
    }

    private static String title(AbstractButton button) {
        String text = button.getMessage().getString().trim();
        while (text.endsWith(".")) text = text.substring(0, text.length() - 1);
        return text;
    }

    // ---------------------------------------------------------------- отрисовка

    @SubscribeEvent
    public static void onRender(ScreenEvent.Render.Pre event) {
        Screen screen = event.getScreen();

        // меню выключили, пока оно открыто — отдаём экран игре как есть
        if (screen instanceof TitleScreen && screen == owner && !Config.get().customMenu) {
            release();
            return;
        }

        if (!active(screen)) return;

        GuiGraphics g = event.getGuiGraphics();
        Font font = Minecraft.getInstance().font;

        Layout l = layout(screen);

        background(g, screen.width, screen.height);
        if (l.mark) mark(g, font, l);
        entries(g, font, screen, l, event.getMouseX(), event.getMouseY(), event.getPartialTick());
        if (Config.get().menuCorners) corners(g, font, screen, l);

        // ванильный экран со своим логотипом и кнопками не рисуем
        event.setCanceled(true);
    }

    /** Ночное небо: градиент, свечение у горизонта и звёзды с медленным дрейфом. */
    private static void background(GuiGraphics g, int w, int h) {
        Config cfg = Config.get();
        int horizon = (int) (h * 0.62f);

        g.fillGradient(0, 0, w, horizon, 0xFF070910, 0xFF161F33);
        g.fillGradient(0, horizon, w, h, 0xFF161F33, 0xFF05070C);

        if (cfg.menuGlow) {
            int accent = NareTheme.accent();
            int glowTop = Math.max(0, horizon - h / 5);

            for (int i = 0; i < 6; i++) {
                float t = i / 6f;
                int y0 = glowTop + (int) ((horizon - glowTop) * t);
                int y1 = glowTop + (int) ((horizon - glowTop) * (t + 1f / 6f));
                g.fill(0, y0, w, y1, NareTheme.alpha(accent, 0.03f + t * 0.07f));
            }
        }

        if (cfg.menuStars) {
            int count = Math.max(0, Math.min(STARS.length, cfg.menuStarCount));
            float drift = (System.currentTimeMillis() % 240000L) / 240000f;

            for (int i = 0; i < count; i++) {
                float[] star = STARS[i];
                int x = (int) (((star[0] + drift) % 1f) * w);
                int y = (int) (star[1] * h);
                int bright = (int) (star[2] * 255);
                g.fill(x, y, x + 1, y + 1, (bright << 24) | 0x00FFFFFF);
                if (star[2] > 0.88f) g.fill(x, y + 1, x + 1, y + 2, 0x33FFFFFF);
            }
        }

        // лёгкое затемнение к низу, чтобы текст читался
        g.fillGradient(0, h - 80, w, h, 0x00000000, 0x80000000);
    }

    private static void mark(GuiGraphics g, Font font, Layout l) {
        int accent = NareTheme.accent();
        int radius = Math.max(4, l.markSize / 5);

        NareTheme.roundRect(g, l.markX, l.markY, l.markSize, l.markSize, radius, accent);
        g.fillGradient(l.markX, l.markY, l.markX + l.markSize, l.markY + l.markSize,
                0x33FFFFFF, 0x00000000);
        NareTheme.outline(g, l.markX, l.markY, l.markSize, l.markSize, radius, 0x44FFFFFF);

        int nx = l.markX + Math.round((l.markSize - font.width("N") * l.markScale) / 2f);
        int ny = l.markY + Math.round((l.markSize - 8 * l.markScale) / 2f);
        scaled(g, font, "N", nx, ny, l.markScale, 0xFF0B0E16, false);

        scaled(g, font, "NARE", l.markX, l.wordY, l.wordScale, NareTheme.TEXT, true);
        scaled(g, font, "VISUALS",
                l.markX + (int) (font.width("NARE") * l.wordScale) + 5, l.wordY,
                l.wordScale, accent, true);
    }

    private static void entries(GuiGraphics g, Font font, Screen screen, Layout l,
                                int mouseX, int mouseY, float partialTick) {
        hover = rowAt(screen, mouseX, mouseY);

        for (int i = 0; i < ENTRIES.size(); i++) {
            String text = title(ENTRIES.get(i));
            int x = rowX(screen, l, font, text);
            int y = l.top + i * l.rowH;
            boolean on = i == hover;

            float target = on ? 8f : 0f;
            if (on) shift += (target - shift) * Math.min(1f, partialTick * 0.4f);

            int offset = on ? (int) shift : 0;
            int color = on ? NareTheme.accent() : 0xFFD6DEEC;

            if (on) {
                int barH = Math.max(8, (int) (10 * l.entryScale));
                g.fill(x - 12, y, x - 9, y + barH, NareTheme.accent());
            }

            scaled(g, font, text, x + offset, y, l.entryScale, color, true);
        }
    }

    private static void corners(GuiGraphics g, Font font, Screen screen, Layout l) {
        int w = screen.width, h = screen.height;

        // надпись справа сверху
        float es = l.wordScale >= 1.5f ? 1.2f : 1.0f;
        String edition = "Forge Edition";
        int ew = (int) (font.width(edition) * es);
        scaled(g, font, edition, w - 14 - ew, 14, es, NareTheme.accent(), true);

        // сведения справа снизу
        String[] lines = {
                Forge_narevisuals.NAME + " " + modVersion(),
                "Minecraft " + SharedConstants.getCurrentVersion().getName(),
                "Forge " + ForgeVersion.getVersion(),
                "Copyright Mojang AB. Do not distribute!"
        };

        int y = h - 10 - lines.length * 10;
        for (String line : lines) {
            g.drawString(font, line, w - 8 - font.width(line), y,
                    line.startsWith("Copyright") ? NareTheme.DIM : NareTheme.MUTED, true);
            y += 10;
        }

        // слева снизу — чья сборка
        g.drawString(font, "NareLauncher · Winnigames2024", 8, h - 10, NareTheme.DIM, true);
    }

    private static String modVersion() {
        try {
            var container = ModList.get().getModContainerById(Forge_narevisuals.MODID);
            if (container.isPresent())
                return container.get().getModInfo().getVersion().toString();
        } catch (Throwable ignored) { }

        // до списка модов не достучались — показываем то, что знаем сами,
        // а не выдуманное число: иначе версия в меню живёт своей жизнью
        return Forge_narevisuals.VERSION;
    }

    /** Текст крупнее обычного: масштабируем матрицу, а не шрифт. */
    private static void scaled(GuiGraphics g, Font font, String text,
                               int x, int y, float scale, int color, boolean shadow) {
        var pose = g.pose();
        pose.pushPose();
        pose.translate((float) x, (float) y, 0f);
        pose.scale(scale, scale, 1f);
        g.drawString(font, text, 0, 0, color, shadow);
        pose.popPose();
    }
}
