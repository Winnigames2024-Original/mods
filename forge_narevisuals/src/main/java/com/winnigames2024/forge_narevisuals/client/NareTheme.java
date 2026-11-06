package com.winnigames2024.forge_narevisuals.client;

import com.winnigames2024.forge_narevisuals.Config;
import net.minecraft.client.gui.Font;
import net.minecraft.client.gui.GuiGraphics;

/**
 * Внешний вид HUD: скруглённые карточки, градиенты, мягкая тень и акцент.
 * Все элементы рисуются этими же примитивами, поэтому HUD выглядит цельно.
 */
public final class NareTheme {

    // Цвета темы. Не final: тему можно сменить прямо в игре, и все экраны
    // читают эти поля на каждом кадре, так что менять достаточно здесь.
    public static int BG_TOP = 0xE6121722;
    public static int BG_BOTTOM = 0xE60B0E16;
    public static int BG_SOFT = 0x900F131C;
    public static int LINE = 0x24FFFFFF;
    public static int LINE_SOFT = 0x14FFFFFF;
    public static int TEXT = 0xFFEAF0FA;
    public static int MUTED = 0xFF8C98AE;
    public static int DIM = 0xFF5E6880;
    public static final int GOOD = 0xFF46D08A;
    public static final int WARN = 0xFFF5BE54;
    public static final int BAD = 0xFFFF6B6B;

    public static final int RADIUS = 4;

    private NareTheme() { }

    public static int accent() {
        return Config.get().accent;
    }

    public static boolean rounded() {
        return Config.get().rounded;
    }

    public static int alpha(int color, float a) {
        int base = color & 0x00FFFFFF;
        int value = (int) Math.max(0, Math.min(255, a * 255));
        return (value << 24) | base;
    }

    /** Плавное дыхание 0.75…1.0 — для акцентов и маркера попадания. */
    public static float pulse() {
        return 0.78f + 0.22f * (float) Math.sin(System.currentTimeMillis() / 620.0);
    }


    // ------------------------------------------------------------------ темы

    /** Готовая тема: имя, акцент и весь набор цветов интерфейса. */
    public static final class Preset {
        public final String name;
        public final int accent, bgTop, bgBottom, bgSoft, line, lineSoft, text, muted, dim;

        Preset(String name, int accent, int bgTop, int bgBottom, int bgSoft,
               int line, int lineSoft, int text, int muted, int dim) {
            this.name = name;
            this.accent = accent;
            this.bgTop = bgTop;
            this.bgBottom = bgBottom;
            this.bgSoft = bgSoft;
            this.line = line;
            this.lineSoft = lineSoft;
            this.text = text;
            this.muted = muted;
            this.dim = dim;
        }
    }

    public static final Preset[] THEMES = {
            new Preset("Полночь", 0xFF4F8CFF,
                    0xE6121722, 0xE60B0E16, 0x900F131C,
                    0x24FFFFFF, 0x14FFFFFF, 0xFFEAF0FA, 0xFF8C98AE, 0xFF5E6880),

            new Preset("Уголь", 0xFFB9C2D0,
                    0xE6161616, 0xE60C0C0C, 0x900F0F0F,
                    0x22FFFFFF, 0x12FFFFFF, 0xFFF0F0F0, 0xFF9A9A9A, 0xFF666666),

            new Preset("Закат", 0xFFFF8A4C,
                    0xE61E1420, 0xE6130B14, 0x90170E18,
                    0x26FFFFFF, 0x15FFFFFF, 0xFFFBEDE4, 0xFFB79487, 0xFF7C6058),

            new Preset("Хвоя", 0xFF46D08A,
                    0xE60F1A16, 0xE6080F0C, 0x900B1310,
                    0x24FFFFFF, 0x14FFFFFF, 0xFFE6F6EE, 0xFF84A596, 0xFF567268),

            new Preset("Сирень", 0xFFB184FF,
                    0xE6181428, 0xE60E0B1A, 0x90120E20,
                    0x26FFFFFF, 0x15FFFFFF, 0xFFEFE9FB, 0xFF9B8FC0, 0xFF6B6190),

            new Preset("Молоко", 0xFF2F6BD8,
                    0xF2F3F5F9, 0xF2E7EBF2, 0xA0EDF0F6,
                    0x22000000, 0x12000000, 0xFF1A1F2B, 0xFF5B6374, 0xFF8A93A4),

            new Preset("Вишня", 0xFFFF5470,
                    0xE6200F16, 0xE614080D, 0x90190B11,
                    0x26FFFFFF, 0x15FFFFFF, 0xFFFBE8ED, 0xFFBC8795, 0xFF835A66)
    };

    /** Тема по имени; незнакомое имя — первая в списке. */
    public static Preset themeByName(String name) {
        for (Preset preset : THEMES)
            if (preset.name.equalsIgnoreCase(name)) return preset;
        return THEMES[0];
    }

    /**
     * Применяет тему к интерфейсу. Акцент тоже меняется, но только если игрок
     * не выбрал свой цвет руками — свой выбор уважаем и не затираем.
     */
    public static void applyTheme(String name, boolean takeAccent) {
        Preset preset = themeByName(name);

        BG_TOP = preset.bgTop;
        BG_BOTTOM = preset.bgBottom;
        BG_SOFT = preset.bgSoft;
        LINE = preset.line;
        LINE_SOFT = preset.lineSoft;
        TEXT = preset.text;
        MUTED = preset.muted;
        DIM = preset.dim;

        Config cfg = Config.get();
        cfg.theme = preset.name;
        if (takeAccent) {
            cfg.accent = preset.accent;
            cfg.ownAccent = false;
        }
    }

    /** Ставит тему, сохранённую в настройках. Зовётся при запуске. */
    public static void applySaved() {
        Config cfg = Config.get();
        applyTheme(cfg.theme, !cfg.ownAccent);
    }

    /** Следующая тема по кругу. */
    public static void nextTheme() {
        Config cfg = Config.get();
        int index = 0;
        for (int i = 0; i < THEMES.length; i++)
            if (THEMES[i].name.equalsIgnoreCase(cfg.theme)) { index = i; break; }

        applyTheme(THEMES[(index + 1) % THEMES.length].name, true);
        Config.save();
    }

    // ------------------------------------------------------------ примитивы

    /** Прямоугольник со скруглёнными углами, собранный из полосок. */
    public static void roundRect(GuiGraphics g, int x, int y, int w, int h, int r, int color) {
        if (w <= 0 || h <= 0) return;

        if (r <= 0 || w < r * 2 || h < r * 2) {
            g.fill(x, y, x + w, y + h, color);
            return;
        }

        g.fill(x + r, y, x + w - r, y + h, color);
        g.fill(x, y + r, x + r, y + h - r, color);
        g.fill(x + w - r, y + r, x + w, y + h - r, color);

        for (int i = 0; i < r; i++) {
            int dy = r - i - 1;
            int inset = r - (int) Math.round(Math.sqrt((double) r * r - (double) dy * dy));

            g.fill(x + inset, y + i, x + r, y + i + 1, color);
            g.fill(x + w - r, y + i, x + w - inset, y + i + 1, color);
            g.fill(x + inset, y + h - i - 1, x + r, y + h - i, color);
            g.fill(x + w - r, y + h - i - 1, x + w - inset, y + h - i, color);
        }
    }

    /** Тонкая рамка по тому же контуру. */
    public static void outline(GuiGraphics g, int x, int y, int w, int h, int r, int color) {
        if (w <= 0 || h <= 0) return;

        g.fill(x + r, y, x + w - r, y + 1, color);
        g.fill(x + r, y + h - 1, x + w - r, y + h, color);
        g.fill(x, y + r, x + 1, y + h - r, color);
        g.fill(x + w - 1, y + r, x + w, y + h - r, color);

        for (int i = 0; i < r; i++) {
            int dy = r - i - 1;
            int inset = r - (int) Math.round(Math.sqrt((double) r * r - (double) dy * dy));

            g.fill(x + inset, y + i, x + inset + 1, y + i + 1, color);
            g.fill(x + w - inset - 1, y + i, x + w - inset, y + i + 1, color);
            g.fill(x + inset, y + h - i - 1, x + inset + 1, y + h - i, color);
            g.fill(x + w - inset - 1, y + h - i - 1, x + w - inset, y + h - i, color);
        }
    }

    /** Мягкая тень под карточкой — две полупрозрачные полоски со сдвигом. */
    public static void shadow(GuiGraphics g, int x, int y, int w, int h) {
        if (!Config.get().panelShadow) return;
        g.fill(x + 2, y + h, x + w - 2, y + h + 1, 0x33000000);
        g.fill(x + 4, y + h + 1, x + w - 4, y + h + 2, 0x1A000000);
    }

    /** Карточка под элемент HUD. */
    public static void card(GuiGraphics g, int x, int y, int w, int h) {
        if (!Config.get().background) return;

        int r = rounded() ? RADIUS : 0;

        shadow(g, x, y, w, h);
        roundRect(g, x, y, w, h, r, BG_TOP);
        g.fillGradient(x + 1, y + 1, x + w - 1, y + h - 1, 0x00000000, 0x3A000000);

        // блик по верхней кромке
        g.fill(x + r, y, x + w - r, y + 1, LINE);

        accentBar(g, x, y, h);
        outline(g, x, y, w, h, r, LINE_SOFT);
    }

    /** Акцентная полоска слева с затуханием книзу. */
    public static void accentBar(GuiGraphics g, int x, int y, int h) {
        int a = accent();
        int top = alpha(a, 0.95f * pulse());
        int bottom = alpha(a, 0.28f);

        g.fillGradient(x + 1, y + 2, x + 3, y + h - 2, top, bottom);
    }

    // ------------------------------------------------------------ текст

    public static void text(GuiGraphics g, Font font, String s, int x, int y, int color) {
        g.drawString(font, s, x, y, color, Config.get().shadow);
    }

    /** Псевдожирный: та же строка со сдвигом в один пиксель. */
    public static void bold(GuiGraphics g, Font font, String s, int x, int y, int color) {
        g.drawString(font, s, x, y, color, Config.get().shadow);
        g.drawString(font, s, x + 1, y, color, false);
    }

    public static void right(GuiGraphics g, Font font, String s, int right, int y, int color) {
        text(g, font, s, right - font.width(s), y, color);
    }

    public static void rightBold(GuiGraphics g, Font font, String s, int right, int y, int color) {
        bold(g, font, s, right - font.width(s) - 1, y, color);
    }

    /**
     * Готовая строка-показатель: подпись слева приглушённо, значение справа жирным.
     * Так выглядят FPS, пинг, CPS и координаты — отсюда общий ритм HUD.
     */
    public static void stat(GuiGraphics g, Font font, int x, int y, int w, int h,
                            String label, String value, int valueColor) {
        card(g, x, y, w, h);

        int ty = y + (h - 8) / 2;
        text(g, font, label, x + 9, ty, MUTED);
        rightBold(g, font, value, x + w - 8, ty, valueColor);
    }

    /** Полоска заполнения от 0 до 1 — прочность брони и подобное. */
    public static void bar(GuiGraphics g, int x, int y, int w, int h, float t, int color) {
        if (t < 0) t = 0;
        if (t > 1) t = 1;

        roundRect(g, x, y, w, h, 0, 0x66000000);
        int fill = Math.round(w * t);
        if (fill > 0) g.fillGradient(x, y, x + fill, y + h, color, alpha(color, 0.55f));
    }

    // ------------------------------------------------------------ оценки

    public static int gradeHigh(int value, int goodBelow, int warnBelow) {
        if (value < goodBelow) return GOOD;
        if (value < warnBelow) return WARN;
        return BAD;
    }

    public static int gradeLow(int value, int goodAbove, int warnAbove) {
        if (value > goodAbove) return GOOD;
        if (value > warnAbove) return WARN;
        return BAD;
    }

    public static String roman(int number) {
        switch (number) {
            case 0: return "";
            case 1: return "II";
            case 2: return "III";
            case 3: return "IV";
            case 4: return "V";
            case 5: return "VI";
            default: return String.valueOf(number + 1);
        }
    }

    /** Тики в формат мм:сс. Отрицательная длительность означает бесконечный эффект. */
    public static String duration(int ticks) {
        if (ticks < 0) return "∞";
        int seconds = ticks / 20;
        return String.format("%d:%02d", seconds / 60, seconds % 60);
    }
}
