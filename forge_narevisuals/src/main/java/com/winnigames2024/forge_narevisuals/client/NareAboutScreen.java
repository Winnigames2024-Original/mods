package com.winnigames2024.forge_narevisuals.client;

import com.winnigames2024.forge_narevisuals.Forge_narevisuals;
import net.minecraft.client.gui.GuiGraphics;
import net.minecraft.client.gui.screens.Screen;
import net.minecraft.client.renderer.RenderType;
import net.minecraft.network.chat.Component;
import net.minecraft.resources.ResourceLocation;

import java.util.ArrayList;
import java.util.List;

/**
 * «Что умеет мод» — большое окно со списком возможностей, картинкой у
 * каждой и подробным текстом.
 *
 * Зачем отдельный экран: настройки отвечают на вопрос «как включить», а
 * этот — на вопрос «что это вообще такое и зачем». Человеку, который
 * открыл мод впервые, второе нужнее.
 */
public class NareAboutScreen extends Screen {

    private static final int ICON = 34;

    private record Part(String icon, String title, String what, String[] lines) { }

    private static final Part[] PARTS = {
            new Part("hud", "HUD — всё нужное на экране",
                    "Right Shift → «Элементы»",
                    new String[] {
                            "FPS, пинг, координаты, нажатия клавиш, CPS, броня,",
                            "эффекты, игроки рядом и стрелка на ближайшего.",
                            "Любой элемент можно выключить и перетащить мышью.",
                            "Клавиша ] двигает их прямо в игре, не выходя в меню."
                    }),

            new Part("voice", "Голосовой чат",
                    "V — меню, левый Alt — говорить",
                    new String[] {
                            "Слышно тех, кто рядом: громкость падает с расстоянием,",
                            "звук идёт с той стороны, где человек стоит.",
                            "Три режима: держать клавишу, нажал-говоришь, по голосу.",
                            "В меню — список «кто рядом»: каждому можно убавить",
                            "громкость или заглушить совсем, и это запоминается.",
                            "Нужен плагин на сервере, порт открывать не обязательно."
                    }),

            new Part("fps", "FPS+",
                    "Right Shift → «FPS+»",
                    new String[] {
                            "Когда чего-то становится слишком много, лишнее просто",
                            "не рисуется — и только у вас. ТНТ, мобы, предметы, опыт.",
                            "Есть список «никогда не рендерить» и бюджет частиц.",
                            "На сервере при этом не меняется ничего."
                    }),

            new Part("camera", "Камера и обзор",
                    "F — свободная камера, C — приближение",
                    new String[] {
                            "Свободная камера летает отдельно от тела, колесо меняет",
                            "скорость. Приближение — на клавишу, как бинокль.",
                            "Подсветка блока под прицелом своим цветом.",
                            "Сервер может это запрещать — тогда мод послушается."
                    }),

            new Part("totem", "Помощь в бою",
                    "Right Shift → «Инвентарь»",
                    new String[] {
                            "Авто-тотем достаёт тотем во вторую руку, когда",
                            "здоровья осталось меньше выбранного.",
                            "Авто-броня надевает лучшую из инвентаря.",
                            "Делается ровно так же, как руками, — просто быстрее."
                    }),

            new Part("shield", "Что не мешает смотреть",
                    "Right Shift → «Инвентарь» → «Обзор»",
                    new String[] {
                            "Огонь во весь экран убирается или рисуется полоской",
                            "внизу. Гореть вы при этом не перестаёте.",
                            "Щит в руке уменьшается ползунком.",
                            "Эффекты можно показывать не все, а только выбранные."
                    }),

            new Part("theme", "Темы и меню",
                    "Настройки игры → «Тема»",
                    new String[] {
                            "Семь готовых тем и свой акцентный цвет.",
                            "Два вида меню: обычное окно с разделами и панельное,",
                            "где окошки таскаешь по экрану.",
                            "Отдельной строкой — «Как в обычном Minecraft»:",
                            "мод перестаёт подменять меню, прицел и небо."
                    }),

            new Part("parkour", "Плитки для паркура",
                    "На сервере: /nare give",
                    new String[] {
                            "Чекпоинт становится временным спавном: умер —",
                            "возродился на нём. Сброс его забывает.",
                            "Прыжковая плита подкидывает туда, куда смотрите,",
                            "силу админ правит правым кликом по ней.",
                            "Плитки ещё и крафтятся."
                    })
    };

    private final Screen parent;

    private int px, py, pw, ph;
    private int scroll;

    public NareAboutScreen(Screen parent) {
        super(Component.literal("Что умеет мод"));
        this.parent = parent;
    }

    private static ResourceLocation icon(String name) {
        return ResourceLocation.fromNamespaceAndPath(Forge_narevisuals.MODID, "textures/about/" + name + ".png");
    }

    private void measure() {
        pw = Math.min(460, Math.max(280, this.width - 40));
        ph = Math.min(400, Math.max(180, this.height - 40));
        px = (this.width - pw) / 2;
        py = (this.height - ph) / 2;
    }

    /** Сколько места занимает один раздел. */
    private int heightOf(Part part) {
        return 24 + part.lines().length * 10 + 12;
    }

    private int total() {
        int sum = 0;
        for (Part part : PARTS) sum += heightOf(part);
        return sum;
    }

    @Override
    public void render(GuiGraphics g, int mouseX, int mouseY, float partialTick) {
        super.render(g, mouseX, mouseY, partialTick);
        measure();

        g.fill(0, 0, this.width, this.height, 0x99000000);

        NareTheme.roundRect(g, px, py, pw, ph, 8, 0xF40D1018);
        NareTheme.outline(g, px, py, pw, ph, 8, 0x2EFFFFFF);

        NareTheme.bold(g, font, "Что умеет NareVisuals", px + 14, py + 13, NareTheme.TEXT);
        g.drawString(font, "версия " + Forge_narevisuals.VERSION
                        + " · колесо — листать · Esc — назад",
                px + 14, py + 26, NareTheme.DIM, false);

        int top = py + 42;
        int bottom = py + ph - 10;

        int limit = Math.max(0, total() - (bottom - top));
        if (scroll > limit) scroll = limit;
        if (scroll < 0) scroll = 0;

        g.enableScissor(px + 1, top, px + pw - 1, bottom);

        int y = top - scroll;

        for (Part part : PARTS) {
            int tall = heightOf(part);

            if (y + tall >= top && y <= bottom) draw(g, part, y);
            y += tall;
        }

        g.disableScissor();

        // полоска прокрутки, чтобы было видно, что текст не кончился
        if (limit > 0) {
            int trackH = bottom - top;
            int barH = Math.max(18, trackH * trackH / Math.max(1, total()));
            int barY = top + (trackH - barH) * scroll / limit;

            NareTheme.roundRect(g, px + pw - 7, barY, 3, barH, 1,
                    NareTheme.alpha(NareTheme.accent(), 0.75f));
        }
    }

    private void draw(GuiGraphics g, Part part, int y) {
        int x = px + 14;

        g.blit(RenderType::guiTextured, icon(part.icon()), x, y, 0f, 0f,
                ICON, ICON, 64, 64, 64, 64, 0xFFFFFFFF);

        int tx = x + ICON + 12;
        int tw = pw - (tx - px) - 18;

        NareTheme.bold(g, font, trim(part.title(), tw), tx, y + 1, NareTheme.TEXT);
        g.drawString(font, trim(part.what(), tw), tx, y + 12,
                NareTheme.alpha(NareTheme.accent(), 0.95f), false);

        int ly = y + 26;
        for (String line : part.lines()) {
            g.drawString(font, trim(line, tw), tx, ly, NareTheme.MUTED, false);
            ly += 10;
        }
    }

    private String trim(String text, int room) {
        if (font.width(text) <= room) return text;

        String cut = text;
        while (cut.length() > 1 && font.width(cut + "…") > room)
            cut = cut.substring(0, cut.length() - 1);

        return cut + "…";
    }

    @Override
    public boolean mouseScrolled(double mouseX, double mouseY, double dx, double dy) {
        scroll -= (int) (dy * 24);
        return true;
    }

    @Override
    public boolean keyPressed(int key, int scan, int modifiers) {
        if (key == 265) { scroll -= 24; return true; }      // вверх
        if (key == 264) { scroll += 24; return true; }      // вниз
        if (key == 266) { scroll -= 160; return true; }     // page up
        if (key == 267) { scroll += 160; return true; }     // page down

        return super.keyPressed(key, scan, modifiers);
    }

    @Override
    public void onClose() {
        if (minecraft != null) minecraft.setScreen(parent);
        else super.onClose();
    }
}
