package com.winnigames2024.forge_narevisuals.client;

import com.mojang.blaze3d.platform.InputConstants;
import com.winnigames2024.forge_narevisuals.Config;
import com.winnigames2024.forge_narevisuals.Forge_narevisuals;
import net.minecraft.client.KeyMapping;
import net.minecraft.client.gui.GuiGraphics;
import net.minecraft.client.gui.screens.Screen;
import net.minecraft.network.chat.Component;
import net.minecraftforge.client.settings.KeyModifier;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.function.BooleanSupplier;
import java.util.function.IntConsumer;
import java.util.function.IntSupplier;
import java.util.function.IntFunction;

/**
 * Настройки мода. Нарисованы целиком своими руками: панель со скруглением,
 * слева разделы, справа строки с переключателями, ползунками и действиями.
 * Ванильных кнопок тут нет — иначе окно выглядело бы как сетка серых
 * прямоугольников.
 */
public class NareSettingsScreen extends Screen {

    private static final int MAX_W = 520;
    private static final int MAX_H = 320;
    private static final int ROW_H = 24;

    private static final int[] ACCENTS = {
            0xFF5E86FF, 0xFF9B5CFF, 0xFF3FBF7F,
            0xFFF0B446, 0xFFFF5C5C, 0xFF36C8D8, 0xFFE8EEF8
    };

    private static final String[] TABS = { "Быстро", "Функции", "Элементы", "Вид", "Меню", "Мир", "Камера", "Бинды", "Ещё" };

    private static final String[] SKY_PRESETS = { "nebula", "sunset", "aurora", "dawn", "void" };
    private static final String[] SKY_TITLES = { "Туманность", "Закат", "Сияние", "Рассвет", "Пустота" };

    /** Строка настройки. */
    private static final class Row {
        enum Kind { TOGGLE, ACTION, ACCENT, SLIDER, LABEL, KEY }

        final Kind kind;
        final String title;
        final String hint;
        final BooleanSupplier value;
        final Runnable action;

        IntSupplier get;
        IntConsumer set;
        int min, max;
        IntFunction<String> format;
        KeyMapping mapping;

        Row(Kind kind, String title, String hint, BooleanSupplier value, Runnable action) {
            this.kind = kind;
            this.title = title;
            this.hint = hint;
            this.value = value;
            this.action = action;
        }

        static Row toggle(String title, String hint, BooleanSupplier value, Runnable action) {
            return new Row(Kind.TOGGLE, title, hint, value, action);
        }

        static Row action(String title, String hint, Runnable action) {
            return new Row(Kind.ACTION, title, hint, null, action);
        }

        static Row accent(String title, String hint) {
            return new Row(Kind.ACCENT, title, hint, null, null);
        }

        static Row key(String title, KeyMapping mapping) {
            Row row = new Row(Kind.KEY, title, null, null, null);
            row.mapping = mapping;
            return row;
        }

        static Row label(String title) {
            return new Row(Kind.LABEL, title, null, null, null);
        }

        static Row slider(String title, int min, int max, IntSupplier get, IntConsumer set,
                          IntFunction<String> format) {
            Row row = new Row(Kind.SLIDER, title, null, null, null);
            row.min = min;
            row.max = max;
            row.get = get;
            row.set = set;
            row.format = format;
            return row;
        }
    }

    private final Screen parent;
    private final Map<String, Float> knobs = new HashMap<>();

    private int tab;
    private int scroll;
    private int dragging = -1;

    /** Выбранный, но ещё не применённый масштаб интерфейса. -1 — ничего не меняли. */
    private int pendingScale = -1;

    /** Привязка, для которой ждём нажатие. null — никого не ждём. */
    private KeyMapping awaiting;

    /** Поиск по всем разделам. Пусто — показываем обычный раздел. */
    private String query = "";

    /** Панель подстраивается под окно: в маленьком она просто ужимается. */
    private int px, py, pw, ph, sideW, headH, footH, tabH;
    private boolean hoverDone;

    public NareSettingsScreen(Screen parent) {
        super(Component.literal(Forge_narevisuals.NAME));
        this.parent = parent;
    }

    // ---------------------------------------------------------------- строки

    private List<Row> rows() {
        var list = new ArrayList<Row>();
        Config cfg = Config.get();

        switch (tab) {
            case 0 -> {
                list.add(Row.label("Включить прямо сейчас"));
                list.add(Row.toggle("Свободная камера",
                        NareKeys.TOGGLE_FREECAM.getTranslatedKeyMessage().getString()
                        + " · WASD, пробел, Shift, колесо",
                        Freecam::active, Freecam::toggle));
                list.add(Row.toggle("Подсветка игроков",
                        NareKeys.TOGGLE_ESP.getTranslatedKeyMessage().getString(),
                        () -> cfg.playerEsp, () -> { cfg.playerEsp = !cfg.playerEsp; Config.save(); }));
                list.add(Row.toggle("Яркость",
                        NareKeys.TOGGLE_BRIGHT.getTranslatedKeyMessage().getString(),
                        () -> cfg.fullbright && cfg.nightVision,
                        () -> {
                            boolean on = !(cfg.fullbright && cfg.nightVision);
                            cfg.fullbright = on;
                            cfg.nightVision = on;
                            Config.save();
                        }));
                list.add(Row.toggle("Своё небо",
                        NareKeys.TOGGLE_SKY.getTranslatedKeyMessage().getString(),
                        () -> cfg.customSky, () -> { cfg.customSky = !cfg.customSky; Config.save(); }));
                list.add(Row.toggle("Свой прицел", null,
                        () -> cfg.customCrosshair,
                        () -> { cfg.customCrosshair = !cfg.customCrosshair; Config.save(); }));
                list.add(Row.toggle("Приближение по клавише",
                        NareKeys.ZOOM.getTranslatedKeyMessage().getString() + " — держать",
                        () -> cfg.zoomEnabled, () -> { cfg.zoomEnabled = !cfg.zoomEnabled; Config.save(); }));
                list.add(Row.toggle("Подсветка блока", null,
                        () -> cfg.blockHighlight,
                        () -> { cfg.blockHighlight = !cfg.blockHighlight; Config.save(); }));
                list.add(Row.toggle("Показывать HUD",
                        NareKeys.TOGGLE_HUD.getTranslatedKeyMessage().getString(),
                        () -> cfg.hudEnabled, () -> { cfg.hudEnabled = !cfg.hudEnabled; Config.save(); }));

                HudModule near = NareHud.byId("players");
                if (near != null)
                    list.add(Row.toggle("Список «кто рядом»",
                            "Команда /near печатает то же самое",
                            near::isEnabled, () -> { near.setEnabled(!near.isEnabled()); Config.save(); }));
            }
            case 1 -> {
                list.add(Row.label("Что рисуем"));
                list.add(Row.toggle("HUD", "Все элементы разом",
                        () -> cfg.hudEnabled, () -> { cfg.hudEnabled = !cfg.hudEnabled; Config.save(); }));
                list.add(Row.toggle("Своё главное меню", null,
                        () -> cfg.customMenu, () -> { cfg.customMenu = !cfg.customMenu; Config.save(); }));
                list.add(Row.toggle("Свой ресурспак", "Кнопки, поля, ползунки",
                        () -> cfg.resourcePack, () -> { cfg.resourcePack = !cfg.resourcePack; Config.save(); }));
                list.add(Row.toggle("Свой прицел", null,
                        () -> cfg.customCrosshair, () -> { cfg.customCrosshair = !cfg.customCrosshair; Config.save(); }));
                list.add(Row.toggle("Своё небо", null,
                        () -> cfg.customSky, () -> { cfg.customSky = !cfg.customSky; Config.save(); }));

                list.add(Row.label("Что видим в мире"));
                list.add(Row.toggle("Подсветка игроков", null,
                        () -> cfg.playerEsp, () -> { cfg.playerEsp = !cfg.playerEsp; Config.save(); }));
                list.add(Row.toggle("Подсветка существ", null,
                        () -> cfg.espMobs, () -> { cfg.espMobs = !cfg.espMobs; Config.save(); }));
                list.add(Row.toggle("Подсветка блока", null,
                        () -> cfg.blockHighlight, () -> { cfg.blockHighlight = !cfg.blockHighlight; Config.save(); }));
                list.add(Row.toggle("Яркость", "Гамма и ночное зрение",
                        () -> cfg.fullbright || cfg.nightVision,
                        () -> {
                            boolean on = !(cfg.fullbright || cfg.nightVision);
                            cfg.fullbright = on;
                            cfg.nightVision = on;
                            Config.save();
                        }));
                list.add(Row.toggle("Метки сломанного", "Что ломают другие с модом",
                        () -> cfg.showMarks, () -> { cfg.showMarks = !cfg.showMarks; Config.save(); }));

                list.add(Row.label("Камера и действия"));
                list.add(Row.toggle("Свободная камера", "Прямо сейчас",
                        Freecam::active, Freecam::toggle));
                list.add(Row.toggle("Приближение", null,
                        () -> cfg.zoomEnabled, () -> { cfg.zoomEnabled = !cfg.zoomEnabled; Config.save(); }));
                list.add(Row.toggle("Ломать область", null,
                        () -> cfg.areaBreak, () -> { cfg.areaBreak = !cfg.areaBreak; Config.save(); }));
                list.add(Row.toggle("Бить по области", null,
                        () -> cfg.areaAttack, () -> { cfg.areaAttack = !cfg.areaAttack; Config.save(); }));
                list.add(Row.toggle("Дальность всегда", "А не только в свободной камере",
                        () -> cfg.reachAlways, () -> { cfg.reachAlways = !cfg.reachAlways; Config.save(); }));

                list.add(Row.label("Сервер"));
                list.add(Row.toggle("Автовход", "Команда и пароль — в «Ещё»",
                        () -> cfg.autoLogin, () -> { cfg.autoLogin = !cfg.autoLogin; Config.save(); }));
                list.add(Row.action("Панель: свои и телепорт", NareNet.plugged()
                                ? "Плагин на сервере есть" : "Нужен плагин на сервере",
                        () -> minecraft.setScreen(new NarePanelScreen(this))));
                list.add(Row.label(NareNet.weAllowFun()
                        ? "Шутки разрешены аргументом запуска"
                        : "Шутки выключены: нет аргумента --funtrolls2026"));

                list.add(Row.label("Подробности — в соседних разделах"));
            }
            case 2 -> {
                for (HudModule module : NareHud.MODULES) {
                    final HudModule m = module;
                    list.add(Row.toggle(m.title, null, m::isEnabled, () -> {
                        m.setEnabled(!m.isEnabled());
                        Config.save();
                    }));
                }
            }
            case 3 -> {
                int maxScale = Math.max(1, minecraft.getWindow().calculateScale(0, false));
                int liveScale = minecraft.options.guiScale().get();

                // ползунок только выбирает — перерисовывать интерфейс на каждое
                // движение мышью неудобно, поэтому применяется отдельной кнопкой
                list.add(Row.slider("Размер интерфейса", 0, maxScale,
                        this::chosenScale, v -> pendingScale = v, NareSettingsScreen::scaleText));

                boolean same = chosenScale() == liveScale;
                list.add(Row.action("Применить размер",
                        same ? "сейчас " + scaleText(liveScale)
                             : scaleText(liveScale) + " → " + scaleText(chosenScale()),
                        this::applyScale));
                list.add(Row.toggle("Подложка карточек", "Тёмный фон под элементами",
                        () -> cfg.background, () -> { cfg.background = !cfg.background; Config.save(); }));
                list.add(Row.toggle("Скруглённые углы", null,
                        () -> cfg.rounded, () -> { cfg.rounded = !cfg.rounded; Config.save(); }));
                list.add(Row.toggle("Тень под карточками", null,
                        () -> cfg.panelShadow, () -> { cfg.panelShadow = !cfg.panelShadow; Config.save(); }));
                list.add(Row.toggle("Тень у текста", null,
                        () -> cfg.shadow, () -> { cfg.shadow = !cfg.shadow; Config.save(); }));
                list.add(Row.toggle("Маркер попадания", null,
                        () -> cfg.hitMarker, () -> { cfg.hitMarker = !cfg.hitMarker; Config.save(); }));
                list.add(Row.toggle("Ресурспак интерфейса", "Применится после перезапуска",
                        () -> cfg.resourcePack, () -> { cfg.resourcePack = !cfg.resourcePack; Config.save(); }));
                list.add(Row.accent("Цвет акцента", null));
            }
            case 4 -> {
                list.add(Row.label("Главное меню игры"));
                list.add(Row.toggle("Своё главное меню", "Плоский список вместо ванильных кнопок",
                        () -> cfg.customMenu, () -> { cfg.customMenu = !cfg.customMenu; Config.save(); }));
                list.add(Row.toggle("Значок и название", "Квадрат с буквой N и надпись NareVisuals",
                        () -> cfg.menuMark, () -> { cfg.menuMark = !cfg.menuMark; Config.save(); }));
                list.add(Row.toggle("Подписи по углам", "Издание, версии и кто собрал",
                        () -> cfg.menuCorners, () -> { cfg.menuCorners = !cfg.menuCorners; Config.save(); }));

                list.add(Row.label("Список пунктов"));
                list.add(Row.toggle("По центру экрана", "Иначе — слева с отступом",
                        () -> cfg.menuCentered, () -> { cfg.menuCentered = !cfg.menuCentered; Config.save(); }));
                list.add(Row.slider("Отступ слева", 6, 200,
                        () -> cfg.menuLeft, v -> { cfg.menuLeft = v; Config.save(); },
                        v -> v + " px"));
                list.add(Row.slider("Размер пунктов", 60, 160,
                        () -> cfg.menuScale, v -> { cfg.menuScale = v; Config.save(); },
                        v -> v + "%"));

                list.add(Row.label("Фон"));
                list.add(Row.toggle("Звёзды", null,
                        () -> cfg.menuStars, () -> { cfg.menuStars = !cfg.menuStars; Config.save(); }));
                list.add(Row.slider("Сколько звёзд", 0, 220,
                        () -> cfg.menuStarCount, v -> { cfg.menuStarCount = v; Config.save(); },
                        String::valueOf));
                list.add(Row.toggle("Свечение у горизонта", "Подсвечивается цветом акцента",
                        () -> cfg.menuGlow, () -> { cfg.menuGlow = !cfg.menuGlow; Config.save(); }));
            }
            case 5 -> {
                list.add(Row.label("Игроки"));
                list.add(Row.toggle("Подсветка игроков",
                        NareKeys.TOGGLE_ESP.getTranslatedKeyMessage().getString() + " — быстрый выключатель",
                        () -> cfg.playerEsp, () -> { cfg.playerEsp = !cfg.playerEsp; Config.save(); }));
                list.add(Row.toggle("Цвет по здоровью", null,
                        () -> cfg.espHealthColor, () -> { cfg.espHealthColor = !cfg.espHealthColor; Config.save(); }));
                list.add(Row.toggle("Имя", null,
                        () -> cfg.espName, () -> { cfg.espName = !cfg.espName; Config.save(); }));
                list.add(Row.toggle("Здоровье", null,
                        () -> cfg.espHealth, () -> { cfg.espHealth = !cfg.espHealth; Config.save(); }));
                list.add(Row.toggle("Полоска здоровья", "Над головой, рядом с цифрами",
                        () -> cfg.espBar, () -> { cfg.espBar = !cfg.espBar; Config.save(); }));
                list.add(Row.toggle("Броня", null,
                        () -> cfg.espArmor, () -> { cfg.espArmor = !cfg.espArmor; Config.save(); }));
                list.add(Row.toggle("Опыт и сытость", "Сервер шлёт их только про вас",
                        () -> cfg.espExtra, () -> { cfg.espExtra = !cfg.espExtra; Config.save(); }));
                list.add(Row.toggle("Расстояние", null,
                        () -> cfg.espDistance, () -> { cfg.espDistance = !cfg.espDistance; Config.save(); }));
                list.add(Row.slider("Дальность подсветки", 16, 128,
                        () -> cfg.espRange, v -> { cfg.espRange = v; Config.save(); },
                        v -> v + " бл."));
                list.add(Row.toggle("Рамки и на существах", "Мобы и животные, без подписи",
                        () -> cfg.espMobs, () -> { cfg.espMobs = !cfg.espMobs; Config.save(); }));

                list.add(Row.label("Кто рядом"));
                HudModule near = NareHud.byId("players");
                if (near != null)
                    list.add(Row.toggle("Список на экране", "Команда /near печатает то же самое",
                            near::isEnabled, () -> { near.setEnabled(!near.isEnabled()); Config.save(); }));
                list.add(Row.slider("Радиус поиска", 16, 512,
                        () -> cfg.nearRange, v -> { cfg.nearRange = v; Config.save(); },
                        v -> v + " бл."));
                list.add(Row.toggle("Показывать здоровье", null,
                        () -> cfg.nearHealth, () -> { cfg.nearHealth = !cfg.nearHealth; Config.save(); }));

                list.add(Row.label("Блок под прицелом"));
                list.add(Row.toggle("Своя подсветка блока", "Вместо ванильной чёрной рамки",
                        () -> cfg.blockHighlight, () -> { cfg.blockHighlight = !cfg.blockHighlight; Config.save(); }));
                list.add(Row.toggle("Заливка граней", null,
                        () -> cfg.blockFill, () -> { cfg.blockFill = !cfg.blockFill; Config.save(); }));

                list.add(Row.label("Яркость"));
                list.add(Row.toggle("Поднять гамму", null,
                        () -> cfg.fullbright, () -> { cfg.fullbright = !cfg.fullbright; Config.save(); }));
                list.add(Row.toggle("Ночное зрение", "Эффект только на клиенте",
                        () -> cfg.nightVision, () -> { cfg.nightVision = !cfg.nightVision; Config.save(); }));
            }
            case 6 -> {
                list.add(Row.label("Прицел"));
                list.add(Row.toggle("Свой прицел", null,
                        () -> cfg.customCrosshair, () -> {
                            cfg.customCrosshair = !cfg.customCrosshair;
                            Config.save();
                        }));
                list.add(Row.action("Картинка прицела",
                        cfg.crosshairImage == null || cfg.crosshairImage.isEmpty()
                                ? "Сейчас — свой крест" : cfg.crosshairImage,
                        () -> minecraft.setScreen(new NarePickScreen.Images(this, true))));
                list.add(Row.slider("Размер прицела", 6, 48,
                        () -> cfg.crosshairSize, v -> { cfg.crosshairSize = v; Config.save(); },
                        v -> v + " px"));

                list.add(Row.label("Приближение"));
                list.add(Row.toggle("Включить зум",
                        NareKeys.ZOOM.getTranslatedKeyMessage().getString() + " — держать",
                        () -> cfg.zoomEnabled, () -> { cfg.zoomEnabled = !cfg.zoomEnabled; Config.save(); }));
                list.add(Row.slider("Угол обзора в зуме", 5, 70,
                        () -> cfg.zoomFov, v -> { cfg.zoomFov = v; Config.save(); },
                        v -> v + "°"));

                list.add(Row.label("Свободная камера"));
                list.add(Row.toggle("Включить сейчас",
                        NareKeys.TOGGLE_FREECAM.getTranslatedKeyMessage().getString() + " — в игре",
                        Freecam::active, Freecam::toggle));
                list.add(Row.slider("Скорость полёта", 10, 200,
                        () -> (int) Math.round(cfg.freecamSpeed * 100),
                        v -> { cfg.freecamSpeed = v / 100.0; Config.save(); },
                        v -> String.format("%.2f", v / 100.0)));
                list.add(Row.slider("Дальность действий", 0, 96,
                        () -> cfg.freecamReach, v -> { cfg.freecamReach = v; Config.save(); },
                        v -> v == 0 ? "как обычно" : v + " бл."));

                list.add(Row.label("Дальность и область"));
                list.add(Row.slider("Радиус ломания", 0, 128,
                        () -> cfg.reachBlocks, v -> { cfg.reachBlocks = v; Config.save(); },
                        NareReach::reachText));
                list.add(Row.slider("Радиус удара", 0, 64,
                        () -> cfg.reachEntities, v -> { cfg.reachEntities = v; Config.save(); },
                        NareReach::reachText));
                list.add(Row.toggle("Держать всегда", "А не только в свободной камере",
                        () -> cfg.reachAlways, () -> { cfg.reachAlways = !cfg.reachAlways; Config.save(); }));
                list.add(Row.label("Поднимает дальность только там, где сервер наш"));

                list.add(Row.toggle("Ломать область", "Общий выключатель",
                        () -> cfg.areaBreak, () -> { cfg.areaBreak = !cfg.areaBreak; Config.save(); }));
                list.add(Row.toggle("Бить по области", "Общий выключатель",
                        () -> cfg.areaAttack, () -> { cfg.areaAttack = !cfg.areaAttack; Config.save(); }));
                list.add(Row.slider("Размер области", 1, 15,
                        () -> cfg.breakSize,
                        v -> { cfg.breakSize = v % 2 == 0 ? v + 1 : v; Config.save(); },
                        v -> v <= 1 ? "один блок" : v + "x" + v));
                list.add(Row.slider("Глубина", 1, 5,
                        () -> cfg.breakDepth, v -> { cfg.breakDepth = v; Config.save(); },
                        v -> v + " бл."));
                list.add(Row.slider("Удар по области", 0, 8,
                        () -> cfg.attackArea, v -> { cfg.attackArea = v; Config.save(); },
                        v -> v <= 0 ? "только цель" : v + " бл."));
                list.add(Row.slider("Размер персонажа", 25, 300,
                        () -> cfg.playerScale, v -> { cfg.playerScale = v; Config.save(); },
                        v -> v + "%"));
                list.add(Row.toggle("Подсказка на экране", null,
                        () -> cfg.freecamHint, () -> { cfg.freecamHint = !cfg.freecamHint; Config.save(); }));

                list.add(Row.label("Небо"));
                list.add(Row.toggle("Своё небо",
                        NareKeys.TOGGLE_SKY.getTranslatedKeyMessage().getString(),
                        () -> cfg.customSky, () -> { cfg.customSky = !cfg.customSky; Config.save(); }));
                list.add(Row.action("Пресет неба", skyTitle(cfg), this::nextSky));
                list.add(Row.action("Своя картинка неба",
                        cfg.skyImage == null || cfg.skyImage.isEmpty() ? "не выбрана" : cfg.skyImage,
                        () -> minecraft.setScreen(new NarePickScreen.Images(this, false))));
                list.add(Row.toggle("Медленное вращение", null,
                        () -> cfg.skySpin, () -> { cfg.skySpin = !cfg.skySpin; Config.save(); }));
                list.add(Row.toggle("Оставить солнце и звёзды", null,
                        () -> cfg.skyKeepSun, () -> { cfg.skyKeepSun = !cfg.skyKeepSun; Config.save(); }));
            }
            case 7 -> {
                list.add(Row.label("Клавиши мода"));
                for (int i = 0; i < NareKeys.ALL.length; i++)
                    list.add(Row.key(NareKeys.TITLES[i], NareKeys.ALL[i]));

                list.add(Row.label("Разное"));
                list.add(Row.action("Сбросить всё", "Вернуть клавиши как были", this::resetKeys));
                list.add(Row.action("Управление игры", "Открыть ванильные настройки",
                        () -> minecraft.setScreen(new net.minecraft.client.gui.screens.options.controls.ControlsScreen(
                                this, minecraft.options))));
            }
            default -> {
                list.add(Row.action("Клавиши отдельным окном", "То же самое, но поверх игры",
                        () -> minecraft.setScreen(new NareKeysScreen(this))));
                list.add(Row.action("Панель: свои и телепорт", "Кто с модом, перенос к ним, возврат блоков",
                        () -> minecraft.setScreen(new NarePanelScreen(this))));
                list.add(Row.toggle("Метки сломанного", "Что ломают другие игроки с модом",
                        () -> cfg.showMarks, () -> { cfg.showMarks = !cfg.showMarks; Config.save(); }));
                list.add(Row.slider("Возвращать пачкой", 1, 128,
                        () -> cfg.restoreBatch, v -> { cfg.restoreBatch = v; Config.save(); },
                        v -> v + " бл."));
                list.add(Row.action("Автовход на сервер", "Команда и пароль для /login",
                        () -> minecraft.setScreen(new NareLoginScreen(this))));
                list.add(Row.action("Свои текстуры", "Заменить блок или предмет своей картинкой",
                        () -> minecraft.setScreen(new NareTextureScreen(this))));
                list.add(Row.action("Расположение HUD", "Перетащить элементы мышью",
                        () -> minecraft.setScreen(new HudEditScreen(this))));
                list.add(Row.action("Пресеты .nvc", "Настройки и картинки одним файлом",
                        () -> minecraft.setScreen(new NarePickScreen.Presets(this))));
                list.add(Row.toggle("Показывать HUD", "Быстрый выключатель всего разом",
                        () -> Config.get().hudEnabled,
                        () -> { Config.get().hudEnabled = !Config.get().hudEnabled; Config.save(); }));
            }
        }

        return list;
    }

    /**
     * Что показываем сейчас. Пока ничего не набрано — обычный раздел,
     * иначе проходим по всем разделам и оставляем подходящие строки.
     * Заголовки в поиск не берём: искать надо настройку, а не рубрику.
     */
    private List<Row> shown() {
        if (query.isEmpty()) return rows();

        String needle = query.toLowerCase();
        var found = new ArrayList<Row>();

        int saved = tab;
        for (int t = 0; t < TABS.length; t++) {
            tab = t;
            for (Row row : rows()) {
                if (row.kind == Row.Kind.LABEL) continue;
                if (row.title.toLowerCase().contains(needle)) found.add(row);
            }
        }
        tab = saved;

        return found;
    }

    private static String scaleText(int value) {
        return value == 0 ? "авто" : "x" + value;
    }

    /** Что стоит на ползунке: выбранное, а если не трогали — текущее в игре. */
    private int chosenScale() {
        return pendingScale < 0 ? minecraft.options.guiScale().get() : pendingScale;
    }

    private void applyScale() {
        int value = chosenScale();
        pendingScale = -1;

        if (value == minecraft.options.guiScale().get()) return;

        minecraft.options.guiScale().set(value);
        minecraft.options.save();
        minecraft.resizeDisplay();
    }

    private static String skyTitle(Config cfg) {
        if (cfg.skyImage != null && !cfg.skyImage.isEmpty()) return "своя картинка";
        for (int i = 0; i < SKY_PRESETS.length; i++)
            if (SKY_PRESETS[i].equals(cfg.skyPreset)) return SKY_TITLES[i];
        return SKY_TITLES[0];
    }

    private void nextSky() {
        Config cfg = Config.get();
        cfg.skyImage = "";
        int index = 0;
        for (int i = 0; i < SKY_PRESETS.length; i++)
            if (SKY_PRESETS[i].equals(cfg.skyPreset)) { index = i; break; }
        cfg.skyPreset = SKY_PRESETS[(index + 1) % SKY_PRESETS.length];
        Config.save();
    }

    // ---------------------------------------------------------------- мышь

    private void measure() {
        pw = Math.min(MAX_W, Math.max(210, this.width - 20));
        ph = Math.min(MAX_H, Math.max(130, this.height - 20));
        px = (this.width - pw) / 2;
        py = (this.height - ph) / 2;

        sideW = Math.max(74, Math.min(126, pw / 3));
        headH = ph < 200 ? 42 : 54;
        footH = ph < 200 ? 32 : 44;
        tabH = Math.max(12, Math.min(28, (ph - headH - footH) / TABS.length));
    }

    private int visibleRows() {
        return Math.max(1, (ph - headH - footH) / ROW_H);
    }

    @Override
    public boolean mouseClicked(double mx, double my, int button) {
        measure();

        // ждём клавишу — значит и кнопка мыши годится, как в ванильном управлении
        if (awaiting != null) {
            bindKey(awaiting, InputConstants.Type.MOUSE.getOrCreate(button));
            awaiting = null;
            return true;
        }

        for (int i = 0; i < TABS.length; i++) {
            if (inside(mx, my, px + 10, py + headH + i * tabH, sideW - 18, tabH - 4)) {
                if (tab != i) { tab = i; scroll = 0; }
                return true;
            }
        }

        if (inside(mx, my, px + pw - 94, py + ph - 30, 84, 22)) {
            onClose();
            return true;
        }

        int cx = px + sideW;
        int cw = pw - sideW - 14;
        var list = shown();
        clampScroll(list.size());
        int limit = Math.min(list.size() - scroll, visibleRows());

        for (int i = 0; i < limit; i++) {
            int ry = py + headH + i * ROW_H;
            if (!inside(mx, my, cx, ry, cw, ROW_H - 2)) continue;

            Row row = list.get(i + scroll);

            if (row.kind == Row.Kind.LABEL) return true;

            if (row.kind == Row.Kind.ACCENT) {
                int sx = cx + cw - ACCENTS.length * 15 - 4;
                for (int c = 0; c < ACCENTS.length; c++) {
                    if (mx >= sx + c * 15 && mx <= sx + c * 15 + 12) {
                        Config.get().accent = ACCENTS[c];
                        Config.save();
                        return true;
                    }
                }
                return true;
            }

            if (row.kind == Row.Kind.SLIDER) {
                // только по самой дорожке: раньше клик по названию строки
                // утаскивал значение в минимум
                if (onTrack(mx, cx, cw)) {
                    dragging = i + scroll;
                    applySlider(row, mx, cx, cw);
                }
                return true;
            }

            if (row.kind == Row.Kind.KEY) {
                awaiting = row.mapping;
                return true;
            }

            if (row.action != null) row.action.run();
            return true;
        }

        return super.mouseClicked(mx, my, button);
    }

    @Override
    public boolean charTyped(char c, int modifiers) {
        // ждём клавишу для привязки — буквы сейчас не для поиска
        if (awaiting != null) return super.charTyped(c, modifiers);

        if (c >= ' ') {
            query = query + c;
            scroll = 0;
            return true;
        }
        return super.charTyped(c, modifiers);
    }

    @Override
    public boolean keyPressed(int keyCode, int scanCode, int modifiers) {
        if (awaiting == null) {
            if (keyCode == InputConstants.KEY_BACKSPACE && !query.isEmpty()) {
                query = query.substring(0, query.length() - 1);
                scroll = 0;
                return true;
            }

            // Esc сначала убирает поиск, и только потом закрывает окно
            if (keyCode == InputConstants.KEY_ESCAPE && !query.isEmpty()) {
                query = "";
                scroll = 0;
                return true;
            }
        }

        if (awaiting != null) {
            // Esc снимает привязку — так же, как это делает игра
            bindKey(awaiting, keyCode == InputConstants.KEY_ESCAPE
                    ? InputConstants.UNKNOWN
                    : InputConstants.getKey(keyCode, scanCode));
            awaiting = null;
            return true;
        }
        return super.keyPressed(keyCode, scanCode, modifiers);
    }

    private void bindKey(KeyMapping mapping, InputConstants.Key key) {
        mapping.setKeyModifierAndCode(KeyModifier.NONE, key);
        if (minecraft != null) {
            minecraft.options.save();
            KeyMapping.resetMapping();
        }
    }

    private void resetKeys() {
        for (KeyMapping mapping : NareKeys.ALL)
            mapping.setKeyModifierAndCode(KeyModifier.NONE, mapping.getDefaultKey());

        if (minecraft != null) {
            minecraft.options.save();
            KeyMapping.resetMapping();
        }
    }

    /** Занята ли эта клавиша кем-то ещё — хоть нами, хоть самой игрой. */
    private boolean conflicts(KeyMapping mapping) {
        if (minecraft == null || mapping.isUnbound()) return false;

        for (KeyMapping other : minecraft.options.keyMappings)
            if (other != mapping && other.same(mapping)) return true;

        return false;
    }

    @Override
    public boolean mouseDragged(double mx, double my, int button, double dx, double dy) {
        if (dragging >= 0) {
            measure();
            var list = shown();
            if (dragging < list.size()) {
                Row row = list.get(dragging);
                if (row.kind == Row.Kind.SLIDER)
                    applySlider(row, mx, px + sideW, pw - sideW - 14);
            }
            return true;
        }
        return super.mouseDragged(mx, my, button, dx, dy);
    }

    @Override
    public boolean mouseReleased(double mx, double my, int button) {
        dragging = -1;
        return super.mouseReleased(mx, my, button);
    }

    /** Попал ли курсор в дорожку ползунка — с небольшим запасом по краям. */
    private boolean onTrack(double mx, int cx, int cw) {
        int w = sliderWidth(cw);
        int x = cx + cw - w - 8;
        return mx >= x - 5 && mx <= x + w + 5;
    }

    private void applySlider(Row row, double mx, int cx, int cw) {
        int trackX = cx + cw - sliderWidth(cw) - 8;
        int trackW = sliderWidth(cw);
        double t = (mx - trackX) / Math.max(1, trackW);
        t = Math.max(0, Math.min(1, t));

        int value = row.min + (int) Math.round(t * (row.max - row.min));
        // ползунок размера интерфейса пересобирает экран, так что дёргаем
        // обработчик только когда значение и правда поменялось
        if (value != row.get.getAsInt()) row.set.accept(value);
    }

    private int sliderWidth(int cw) {
        return Math.max(50, Math.min(110, cw / 3));
    }

    @Override
    public boolean mouseScrolled(double mx, double my, double sx, double sy) {
        if (sy != 0) {
            scroll -= (int) Math.signum(sy);
            clampScroll(shown().size());
        }
        return true;
    }

    private void clampScroll(int total) {
        int max = Math.max(0, total - visibleRows());
        if (scroll > max) scroll = max;
        if (scroll < 0) scroll = 0;
    }

    private static boolean inside(double mx, double my, int x, int y, int w, int h) {
        return mx >= x && mx <= x + w && my >= y && my <= y + h;
    }

    // ---------------------------------------------------------------- отрисовка

    @Override
    public void render(GuiGraphics g, int mouseX, int mouseY, float partialTick) {
        super.render(g, mouseX, mouseY, partialTick);

        measure();

        g.fill(0, 0, this.width, this.height, 0x99000000);

        panel(g);
        head(g);
        search(g);
        side(g, mouseX, mouseY);
        content(g, mouseX, mouseY, partialTick);
        foot(g, mouseX, mouseY);
    }

    private void panel(GuiGraphics g) {
        g.fill(px + 4, py + ph, px + pw - 4, py + ph + 2, 0x44000000);
        g.fill(px + 8, py + ph + 2, px + pw - 8, py + ph + 4, 0x22000000);

        NareTheme.roundRect(g, px, py, pw, ph, 8, 0xF40D1018);
        g.fillGradient(px + 1, py + 1, px + pw - 1, py + ph - 1, 0x10FFFFFF, 0x00000000);
        NareTheme.outline(g, px, py, pw, ph, 8, 0x2EFFFFFF);

        g.fill(px + 1, py + headH - 10, px + pw - 1, py + headH - 9, 0x1AFFFFFF);
        g.fill(px + 1, py + ph - footH, px + pw - 1, py + ph - footH + 1, 0x1AFFFFFF);
        g.fill(px + sideW - 8, py + headH - 4, px + sideW - 7, py + ph - footH - 4, 0x14FFFFFF);
    }

    private void head(GuiGraphics g) {
        int accent = NareTheme.accent();

        NareTheme.roundRect(g, px + 12, py + 11, 16, 16, 4, accent);
        g.fillGradient(px + 12, py + 11, px + 28, py + 27, 0x30FFFFFF, 0x00000000);
        g.drawString(font, "N", px + 17, py + 15, 0xFF0B0E16, false);

        NareTheme.bold(g, font, Forge_narevisuals.NAME, px + 36, py + 11, NareTheme.TEXT);

        if (headH >= 54) {
            String key = NareKeys.OPEN_SETTINGS.getTranslatedKeyMessage().getString();
            g.drawString(font, key + " — открыть это окно", px + 36, py + 23, NareTheme.DIM, false);
        }
    }

    /** Строка поиска: отдельного поля нет, просто печатаем. */
    private void search(GuiGraphics g) {
        int x = px + sideW;
        int y = py + headH - 13;
        int w = pw - sideW - 14;

        if (query.isEmpty()) {
            if (pw >= 380)
                g.drawString(font, "печатайте — это поиск по всем разделам",
                        x + 10, y, 0x55FFFFFF, false);
            return;
        }

        NareTheme.bold(g, font, "Поиск: " + query, x + 10, y, NareTheme.accent());

        String count = shown().size() + " найдено · Esc — убрать";
        g.drawString(font, count, x + w - font.width(count) - 6, y, NareTheme.DIM, false);
    }

    private void side(GuiGraphics g, int mouseX, int mouseY) {
        for (int i = 0; i < TABS.length; i++) {
            int x = px + 10;
            int y = py + headH + i * tabH;
            int w = sideW - 18;
            int h = tabH - 4;
            boolean hover = inside(mouseX, mouseY, x, y, w, h);
            boolean active = tab == i;

            if (active) {
                NareTheme.roundRect(g, x, y, w, h, 6, NareTheme.alpha(NareTheme.accent(), 0.20f));
                g.fill(x, y + 3, x + 2, y + h - 3, NareTheme.accent());
            } else if (hover) {
                NareTheme.roundRect(g, x, y, w, h, 6, 0x14FFFFFF);
            }

            int color = active ? NareTheme.TEXT : hover ? NareTheme.MUTED : NareTheme.DIM;
            int ty = y + (h - 8) / 2;
            if (active) NareTheme.bold(g, font, TABS[i], x + 10, ty, color);
            else g.drawString(font, TABS[i], x + 10, ty, color, false);
        }
    }

    private void content(GuiGraphics g, int mouseX, int mouseY, float partialTick) {
        int cx = px + sideW;
        int cw = pw - sideW - 14;
        var list = shown();
        clampScroll(list.size());

        int shown = visibleRows();
        int limit = Math.min(list.size() - scroll, shown);

        if (list.size() > shown) {
            int trackY = py + headH;
            int trackH = shown * ROW_H - 4;
            int barH = Math.max(14, trackH * shown / list.size());
            int barY = trackY + (trackH - barH) * scroll / Math.max(1, list.size() - shown);

            g.fill(px + pw - 7, trackY, px + pw - 5, trackY + trackH, 0x18FFFFFF);
            g.fill(px + pw - 7, barY, px + pw - 5, barY + barH, 0x55FFFFFF);
        }

        for (int i = 0; i < limit; i++) {
            Row row = list.get(i + scroll);
            int y = py + headH + i * ROW_H;

            if (row.kind == Row.Kind.LABEL) {
                NareTheme.bold(g, font, row.title.toUpperCase(), cx + 10, y + 9,
                        NareTheme.alpha(NareTheme.accent(), 0.85f));
                g.fill(cx + 12 + font.width(row.title) + 4, y + 12,
                        cx + cw - 6, y + 13, 0x14FFFFFF);
                continue;
            }

            boolean hover = inside(mouseX, mouseY, cx, y, cw, ROW_H - 2);
            if (hover) NareTheme.roundRect(g, cx, y, cw, ROW_H - 2, 5, 0x12FFFFFF);

            int textY = row.hint == null ? y + 7 : y + 3;
            g.drawString(font, row.title, cx + 10, textY,
                    hover ? NareTheme.TEXT : 0xFFD2DAE8, false);

            if (row.hint != null)
                g.drawString(font, row.hint, cx + 10, y + 13, NareTheme.DIM, false);

            switch (row.kind) {
                case TOGGLE -> {
                    boolean on = row.value.getAsBoolean();
                    float target = on ? 1f : 0f;
                    float t = knobs.getOrDefault(row.title, target);
                    t += (target - t) * Math.min(1f, partialTick * 0.5f);
                    knobs.put(row.title, t);

                    toggle(g, cx + cw - 34, y + 5, on, t);
                }
                case ACCENT -> {
                    int sx = cx + cw - ACCENTS.length * 15 - 4;
                    for (int c = 0; c < ACCENTS.length; c++) {
                        int x = sx + c * 15;
                        NareTheme.roundRect(g, x, y + 5, 12, 12, 3, ACCENTS[c]);
                        if (ACCENTS[c] == Config.get().accent)
                            NareTheme.outline(g, x - 1, y + 4, 14, 14, 4, 0xFFFFFFFF);
                    }
                }
                case SLIDER -> slider(g, row, cx, cw, y);
                case KEY -> keyRow(g, row, cx, cw, y, hover);
                case ACTION -> g.drawString(font, "›", cx + cw - 16, y + 7,
                        hover ? NareTheme.accent() : NareTheme.DIM, false);
                default -> { }
            }
        }
    }

    /** Строка привязки: слева название, справа «кнопка» с клавишей. */
    private void keyRow(GuiGraphics g, Row row, int cx, int cw, int y, boolean hover) {
        boolean waiting = awaiting == row.mapping;
        boolean clash = !waiting && conflicts(row.mapping);

        String text = waiting ? "> ? <" : row.mapping.getTranslatedKeyMessage().getString();
        if (!waiting && row.mapping.isUnbound()) text = "—";

        int w = Math.max(44, Math.min(96, font.width(text) + 16));
        int x = cx + cw - w - 8;
        int bh = ROW_H - 10;
        int by = y + 4;

        int fill = waiting ? NareTheme.alpha(NareTheme.accent(), 0.28f)
                 : clash   ? 0x33FF5C5C
                 : hover   ? 0x1EFFFFFF : 0x14FFFFFF;

        NareTheme.roundRect(g, x, by, w, bh, 4, fill);
        NareTheme.outline(g, x, by, w, bh, 4,
                waiting ? NareTheme.alpha(NareTheme.accent(), 0.9f)
                        : clash ? 0x66FF5C5C : 0x26FFFFFF);

        int color = waiting ? NareTheme.accent() : clash ? 0xFFFF8A8A : 0xFFD2DAE8;
        g.drawString(font, text, x + (w - font.width(text)) / 2, by + (bh - 8) / 2, color, false);

        if (clash)
            g.drawString(font, "!", x - 8, y + 7, 0xFFFF5C5C, false);
    }

    private void slider(GuiGraphics g, Row row, int cx, int cw, int y) {
        int w = sliderWidth(cw);
        int x = cx + cw - w - 8;
        int ty = y + 10;

        int value = row.get.getAsInt();
        float t = (float) (value - row.min) / Math.max(1, row.max - row.min);
        t = Math.max(0f, Math.min(1f, t));

        g.fill(x, ty, x + w, ty + 2, 0x33FFFFFF);
        g.fill(x, ty, x + Math.round(w * t), ty + 2, NareTheme.accent());

        int kx = x + Math.round((w - 6) * t);
        NareTheme.roundRect(g, kx, ty - 3, 6, 8, 2, 0xFFDDE4F0);

        String text = row.format.apply(value);
        g.drawString(font, text, x - font.width(text) - 8, y + 7, NareTheme.MUTED, false);
    }

    /** Переключатель-пилюля с едущим кружком. */
    private void toggle(GuiGraphics g, int x, int y, boolean on, float t) {
        int w = 26, h = 13;

        int track = on ? NareTheme.alpha(NareTheme.accent(), 0.85f) : 0x33FFFFFF;
        NareTheme.roundRect(g, x, y, w, h, 6, track);
        NareTheme.outline(g, x, y, w, h, 6, 0x26FFFFFF);

        int kx = x + 2 + Math.round((w - h) * t);
        NareTheme.roundRect(g, kx, y + 2, h - 4, h - 4, 4, on ? 0xFF0B0E16 : 0xFFDDE4F0);
    }

    private void foot(GuiGraphics g, int mouseX, int mouseY) {
        int x = px + pw - 94;
        int y = py + ph - 30;

        hoverDone = inside(mouseX, mouseY, x, y, 84, 22);

        int accent = NareTheme.accent();
        NareTheme.roundRect(g, x, y, 84, 22, 6,
                hoverDone ? accent : NareTheme.alpha(accent, 0.80f));
        g.fillGradient(x + 1, y + 1, x + 83, y + 21, 0x26FFFFFF, 0x00000000);
        NareTheme.outline(g, x, y, 84, 22, 6, 0x40FFFFFF);

        String text = "Готово";
        NareTheme.bold(g, font, text, x + (84 - font.width(text)) / 2, y + 7, 0xFF0B0E16);

        if (awaiting != null)
            g.drawString(font, "Нажми клавишу или кнопку мыши · Esc — снять", px + 14, y + 7,
                    NareTheme.accent(), false);
        else if (pw >= 380)
            g.drawString(font, "Настройки сохраняются сразу", px + 14, y + 7, NareTheme.DIM, false);
    }

    // ---------------------------------------------------------------- прочее

    @Override
    public void onClose() {
        pendingScale = -1;      // невыбранный размер не должен «залипать» до следующего раза
        awaiting = null;
        query = "";
        Config.save();
        minecraft.setScreen(parent);
    }

    @Override
    public boolean isPauseScreen() {
        return false;
    }
}
