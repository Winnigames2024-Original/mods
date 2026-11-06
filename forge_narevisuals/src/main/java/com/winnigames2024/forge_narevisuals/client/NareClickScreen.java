package com.winnigames2024.forge_narevisuals.client;

import com.mojang.blaze3d.platform.InputConstants;
import com.winnigames2024.forge_narevisuals.Config;
import net.minecraft.client.gui.GuiGraphics;
import net.minecraft.client.gui.screens.Screen;
import net.minecraft.network.chat.Component;

import java.util.ArrayList;
import java.util.List;
import java.util.function.BooleanSupplier;

/**
 * Панельное меню: несколько окошек по категориям, каждое можно таскать мышью
 * и сворачивать щелчком по заголовку. Привычная раскладка для тех, кто
 * пришёл из клиентов, где всё включается прямо поверх игры.
 *
 * Игра при этом видна: ни затемнения, ни размытия — сразу понятно, что
 * меняет каждый выключатель.
 *
 * Тонкие настройки — размеры, цвета, радиусы — живут в обычном меню, сюда
 * вынесено только «включить и выключить». Кнопка внизу открывает обычное.
 */
public class NareClickScreen extends Screen {

    private static final int ROW_H = 13;
    private static final int HEAD_H = 15;
    private static final int PANEL_W = 104;

    private final Screen parent;

    // ------------------------------------------------------------- строки

    private static final class Row {
        final String title;
        final BooleanSupplier state;    // null — это кнопка, а не выключатель
        final Runnable act;

        Row(String title, BooleanSupplier state, Runnable act) {
            this.title = title;
            this.state = state;
            this.act = act;
        }

        static Row toggle(String title, BooleanSupplier state, Runnable act) {
            return new Row(title, state, act);
        }

        static Row action(String title, Runnable act) {
            return new Row(title, null, act);
        }
    }

    private static final class Panel {
        final String id;
        final String title;
        final List<Row> rows = new ArrayList<>();
        int x, y;
        boolean open = true;

        Panel(String id, String title, int x, int y) {
            this.id = id;
            this.title = title;
            this.x = x;
            this.y = y;
        }

        int height() {
            return HEAD_H + (open ? rows.size() * ROW_H + 3 : 0);
        }
    }

    private final List<Panel> panels = new ArrayList<>();

    private Panel dragging;
    private int grabX, grabY;

    public NareClickScreen(Screen parent) {
        super(Component.literal("NareVisuals"));
        this.parent = parent;
    }

    // ------------------------------------------------------------- сборка

    @Override
    protected void init() {
        panels.clear();
        Config cfg = Config.get();

        Panel view = panel("view", "Вид", 10, 10);
        view.rows.add(Row.toggle("HUD", () -> cfg.hudEnabled,
                () -> { cfg.hudEnabled = !cfg.hudEnabled; Config.save(); }));
        view.rows.add(Row.toggle("Своё меню", () -> cfg.customMenu,
                () -> { cfg.customMenu = !cfg.customMenu; Config.save(); }));
        view.rows.add(Row.toggle("Свой прицел", () -> cfg.customCrosshair,
                () -> { cfg.customCrosshair = !cfg.customCrosshair; Config.save(); }));
        view.rows.add(Row.toggle("Своё небо", () -> cfg.customSky,
                () -> { cfg.customSky = !cfg.customSky; Config.save(); }));
        view.rows.add(Row.toggle("Ресурспак", () -> cfg.resourcePack,
                () -> { cfg.resourcePack = !cfg.resourcePack; Config.save(); }));

        Panel world = panel("world", "Мир", 10 + PANEL_W + 6, 10);
        world.rows.add(Row.toggle("Игроки", () -> cfg.playerEsp,
                () -> { cfg.playerEsp = !cfg.playerEsp; Config.save(); }));
        world.rows.add(Row.toggle("Существа", () -> cfg.espMobs,
                () -> { cfg.espMobs = !cfg.espMobs; Config.save(); }));
        world.rows.add(Row.toggle("Блок под прицелом", () -> cfg.blockHighlight,
                () -> { cfg.blockHighlight = !cfg.blockHighlight; Config.save(); }));
        world.rows.add(Row.toggle("Яркость", () -> cfg.fullbright || cfg.nightVision,
                () -> {
                    boolean on = !(cfg.fullbright || cfg.nightVision);
                    cfg.fullbright = on;
                    cfg.nightVision = on;
                    Config.save();
                }));
        world.rows.add(Row.toggle("Метки сломанного", () -> cfg.showMarks,
                () -> { cfg.showMarks = !cfg.showMarks; Config.save(); }));
        world.rows.add(Row.toggle("FPS+", () -> cfg.fpsPlus,
                () -> { cfg.fpsPlus = !cfg.fpsPlus; Config.save(); }));
        world.rows.add(Row.action("Настроить FPS+",
                () -> minecraft.setScreen(new NareFpsScreen(this))));

        Panel camera = panel("camera", "Камера", 10 + (PANEL_W + 6) * 2, 10);
        camera.rows.add(Row.toggle("Свободная камера", Freecam::active, Freecam::toggle));
        camera.rows.add(Row.toggle("Приближение", () -> cfg.zoomEnabled,
                () -> { cfg.zoomEnabled = !cfg.zoomEnabled; Config.save(); }));
        camera.rows.add(Row.toggle("Бить по области", () -> cfg.areaAttack,
                () -> { cfg.areaAttack = !cfg.areaAttack; Config.save(); }));
        camera.rows.add(Row.toggle("Дальность всегда", () -> cfg.reachAlways,
                () -> { cfg.reachAlways = !cfg.reachAlways; Config.save(); }));

        Panel elements = panel("elements", "Элементы", 10, 10 + 100);
        for (HudModule module : NareHud.MODULES) {
            final HudModule m = module;
            elements.rows.add(Row.toggle(m.title, m::isEnabled,
                    () -> { m.setEnabled(!m.isEnabled()); Config.save(); }));
        }

        Panel more = panel("more", "Ещё", 10 + PANEL_W + 6, 10 + 100);
        more.rows.add(Row.toggle("Автовход", () -> cfg.autoLogin,
                () -> { cfg.autoLogin = !cfg.autoLogin; Config.save(); }));
        more.rows.add(Row.action("Передвинуть элементы",
                () -> minecraft.setScreen(new HudEditScreen(this))));
        more.rows.add(Row.action("Свои и телепорт",
                () -> minecraft.setScreen(new NarePanelScreen(this))));
        more.rows.add(Row.action("Обычное меню",
                () -> minecraft.setScreen(new NareSettingsScreen(parent))));
        more.rows.add(Row.action("Расставить окна заново", this::reset));
    }

    private Panel panel(String id, String title, int x, int y) {
        Config.Element saved = Config.get().element("clickgui." + id, true, -1f, -1f);

        Panel p = new Panel(id, title, x, y);

        if (saved.x >= 0f && saved.y >= 0f)
        {
            p.x = (int) saved.x;
            p.y = (int) saved.y;
        }
        p.open = saved.enabled;

        clampInside(p);
        panels.add(p);
        return p;
    }

    private void remember(Panel p) {
        Config.Element saved = Config.get().element("clickgui." + p.id, p.open, p.x, p.y);
        saved.x = p.x;
        saved.y = p.y;
        saved.enabled = p.open;
        Config.save();
    }

    private void clampInside(Panel p) {
        p.x = Math.max(0, Math.min(p.x, Math.max(0, this.width - PANEL_W)));
        p.y = Math.max(0, Math.min(p.y, Math.max(0, this.height - HEAD_H)));
    }

    private void reset() {
        for (Panel p : panels) Config.get().elements.remove("clickgui." + p.id);
        Config.save();
        init();
    }

    // ---------------------------------------------------------- отрисовка

    @Override
    public void render(GuiGraphics g, int mouseX, int mouseY, float partialTick) {
        super.render(g, mouseX, mouseY, partialTick);

        for (Panel p : panels) draw(g, p, mouseX, mouseY);

        String hint = "Заголовок — перетащить, правая кнопка — свернуть · Esc — закрыть";
        g.drawString(font, hint, 6, this.height - 11, NareTheme.DIM, false);
    }

    private void draw(GuiGraphics g, Panel p, int mouseX, int mouseY) {
        int h = p.height();
        int accent = NareTheme.accent();

        NareTheme.roundRect(g, p.x, p.y, PANEL_W, h, 4, 0xEE0D1018);
        NareTheme.outline(g, p.x, p.y, PANEL_W, h, 4, 0x2EFFFFFF);

        // заголовок
        g.fill(p.x + 1, p.y + 1, p.x + PANEL_W - 1, p.y + HEAD_H - 1,
                NareTheme.alpha(accent, 0.22f));
        g.fill(p.x + 1, p.y + 1, p.x + 3, p.y + HEAD_H - 1, accent);

        NareTheme.bold(g, font, p.title, p.x + 7, p.y + 4, NareTheme.TEXT);
        g.drawString(font, p.open ? "–" : "+", p.x + PANEL_W - 11, p.y + 4,
                NareTheme.MUTED, false);

        if (!p.open) return;

        for (int i = 0; i < p.rows.size(); i++) {
            Row row = p.rows.get(i);
            int y = p.y + HEAD_H + i * ROW_H;

            boolean hover = mouseX >= p.x && mouseX <= p.x + PANEL_W
                         && mouseY >= y && mouseY < y + ROW_H;

            if (hover) g.fill(p.x + 1, y, p.x + PANEL_W - 1, y + ROW_H, 0x14FFFFFF);

            if (row.state == null) {
                g.drawString(font, row.title, p.x + 7, y + 3,
                        hover ? accent : NareTheme.MUTED, false);
                g.drawString(font, "›", p.x + PANEL_W - 11, y + 3,
                        hover ? accent : NareTheme.DIM, false);
                continue;
            }

            boolean on = row.state.getAsBoolean();

            g.drawString(font, row.title, p.x + 7, y + 3,
                    on ? NareTheme.TEXT : NareTheme.DIM, false);

            // лампочка справа
            int lx = p.x + PANEL_W - 13;
            int ly = y + 4;
            if (on) {
                NareTheme.roundRect(g, lx, ly, 6, 6, 2, accent);
                NareTheme.roundRect(g, lx - 1, ly - 1, 8, 8, 3, NareTheme.alpha(accent, 0.25f));
            } else {
                NareTheme.outline(g, lx, ly, 6, 6, 2, 0x55FFFFFF);
            }
        }
    }

    // --------------------------------------------------------------- мышь

    @Override
    public boolean mouseClicked(double mouseX, double mouseY, int button) {
        // сверху вниз: окна рисуются по порядку, а щёлкаем по верхнему
        for (int i = panels.size() - 1; i >= 0; i--) {
            Panel p = panels.get(i);
            int h = p.height();

            if (mouseX < p.x || mouseX > p.x + PANEL_W) continue;
            if (mouseY < p.y || mouseY > p.y + h) continue;

            // заголовок
            if (mouseY < p.y + HEAD_H) {
                if (button == 1) {
                    p.open = !p.open;
                    remember(p);
                    return true;
                }

                if (button == 0) {
                    dragging = p;
                    grabX = (int) mouseX - p.x;
                    grabY = (int) mouseY - p.y;

                    // перетаскиваемое окно поднимаем наверх
                    panels.remove(p);
                    panels.add(p);
                    return true;
                }

                return true;
            }

            if (!p.open || button != 0) return true;

            int index = (int) ((mouseY - p.y - HEAD_H) / ROW_H);
            if (index < 0 || index >= p.rows.size()) return true;

            Row row = p.rows.get(index);
            if (row.act != null) row.act.run();
            return true;
        }

        return super.mouseClicked(mouseX, mouseY, button);
    }

    @Override
    public boolean mouseDragged(double mouseX, double mouseY, int button, double dx, double dy) {
        if (dragging != null && button == 0) {
            dragging.x = (int) mouseX - grabX;
            dragging.y = (int) mouseY - grabY;
            clampInside(dragging);
            return true;
        }
        return super.mouseDragged(mouseX, mouseY, button, dx, dy);
    }

    @Override
    public boolean mouseReleased(double mouseX, double mouseY, int button) {
        if (dragging != null) {
            remember(dragging);
            dragging = null;
            return true;
        }
        return super.mouseReleased(mouseX, mouseY, button);
    }

    @Override
    public boolean keyPressed(int keyCode, int scanCode, int modifiers) {
        if (keyCode == InputConstants.KEY_ESCAPE) {
            onClose();
            return true;
        }
        return super.keyPressed(keyCode, scanCode, modifiers);
    }

    @Override
    public void onClose() {
        Config.save();
        minecraft.setScreen(parent);
    }

    /**
     * Игру не затемняем: панельное меню для того и нужно, чтобы видеть, что
     * меняется, прямо по ходу дела.
     */
    @Override
    public void renderBackground(GuiGraphics g, int mouseX, int mouseY, float partialTick) {
        // пусто — пусть будет видно игру
    }

    @Override
    public boolean isPauseScreen() {
        return false;
    }
}
