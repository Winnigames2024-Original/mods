package com.winnigames2024.forge_narevisuals;

import com.google.gson.Gson;
import com.google.gson.GsonBuilder;
import net.minecraftforge.fml.loading.FMLPaths;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.LinkedHashMap;
import java.util.Map;

/**
 * Настройки NareVisuals. Хранятся в config/narevisuals.json,
 * чтобы их можно было править руками и переносить между сборками.
 */
public class Config {

    /** Настройки одного элемента HUD. Координаты — доли экрана, а не пиксели. */
    public static class Element {
        public boolean enabled = true;
        public float x;
        public float y;

        public Element() { }

        public Element(boolean enabled, float x, float y) {
            this.enabled = enabled;
            this.x = x;
            this.y = y;
        }
    }

    public boolean hudEnabled = true;
    public boolean background = true;
    public boolean shadow = true;
    public boolean hitMarker = true;
    public int accent = 0xFF5E86FF;

    /** Имя выбранной темы оформления. */
    public String theme = "Полночь";

    /**
     * Каким меню открывается мод: «Обычный» — окно с разделами, «Панели» —
     * отдельные окошки по категориям поверх игры.
     */
    public String menuStyle = "Обычный";

    /** Акцент выбран руками — тема его больше не трогает. */
    public boolean ownAccent = false;

    /** Скруглять углы карточек. */
    public boolean rounded = true;

    /** Мягкая тень под карточками. */
    public boolean panelShadow = true;

    /** Подключать встроенный ресурспак (кнопки, поля, фон главного меню). Нужен перезапуск. */
    public boolean resourcePack = true;

    /** Своё главное меню вместо ванильного. */
    public boolean customMenu = true;

    // ------------------------------------------------------------ главное меню

    /** Звёзды на фоне своего главного меню. */
    public boolean menuStars = true;
    /** Сколько звёзд рисовать. */
    public int menuStarCount = 220;
    /** Свечение у горизонта. */
    public boolean menuGlow = true;
    /** Значок с буквой N и надпись NareVisuals. */
    public boolean menuMark = true;
    /** Подписи по углам: издание, версии, кто собрал. */
    public boolean menuCorners = true;
    /** Пункты по центру экрана вместо левого края. */
    public boolean menuCentered = false;
    /** Отступ списка от левого края, пикселей. */
    public int menuLeft = 44;
    /** Размер пунктов в процентах от обычного. */
    public int menuScale = 100;

    // ------------------------------------------------------- подсветка игроков

    public boolean playerEsp = false;
    /** Цвет рамки по здоровью: зелёная — целый, красная — почти мёртвый. */
    public boolean espHealthColor = true;
    public int espColor = 0xFF5E86FF;
    /** Подпись под рамкой: имя, здоровье, расстояние. */
    /** Броня игрока над головой — её сервер присылает всем. */
    public boolean espArmor = true;
    /** Опыт и сытость — сервер шлёт их только про вас самих. */
    public boolean espExtra = true;
    /** Полоска здоровья над головой, а не только цифры. */
    public boolean espBar = true;
    public boolean espName = true;
    public boolean espHealth = true;
    public boolean espDistance = false;
    /** Насколько далеко подсвечивать, в блоках. */
    public int espRange = 64;
    /** Рамки и на остальных существах, не только на игроках. */
    public boolean espMobs = false;

    // --------------------------------------------------------- подсветка блока

    public boolean blockHighlight = false;
    public int blockColor = 0xFF5E86FF;
    /** Залить грани полупрозрачным цветом, а не только обвести. */
    public boolean blockFill = true;

    // ------------------------------------------------------------ яркость

    /** Поднять гамму до предела. */
    public boolean fullbright = false;
    /** Эффект ночного зрения на клиенте. */
    public boolean nightVision = false;

    // ------------------------------------------------------------ прицел

    public boolean customCrosshair = false;
    /** Имя файла в config/narevisuals/images. Пусто — встроенная точка. */
    public String crosshairImage = "";
    public int crosshairSize = 16;
    public int crosshairColor = 0xFFFFFFFF;

    // ------------------------------------------------------------ свободная камера

    /** Скорость полёта камеры, блоков за тик. */
    public double freecamSpeed = 0.55;
    /** Дальность, на которой во freecam можно ломать и бить. 0 — как обычно. */
    public int freecamReach = 24;
    /** Показывать подсказку с управлением. */
    /** Сколько блоков возвращать кнопкой «вернуть пачкой». */
    public int restoreBatch = 16;
    /** Бить по области вообще. */
    public boolean areaAttack = true;
    /** Показывать метки сломанного от других игроков. */
    public boolean showMarks = true;

    // ------------------------------------------------------------- автовход
    /** Слать команду входа после захода на сервер. */
    public boolean autoLogin = false;
    /** Какая команда: login, l, reg — у серверов по-разному. */
    public String loginCommand = "login";
    /** Пароль. Лежит в этом файле открытым текстом — помните об этом. */
    public String loginPassword = "";

    // ------------------------------------------------- дальность и область
    /** Насколько далеко можно ломать. 0 — как обычно. */
    public int reachBlocks = 0;
    /** Насколько далеко можно бить. 0 — как обычно. */
    public int reachEntities = 0;
    /** Держать дальность не только в свободной камере, но и всегда. */
    public boolean reachAlways = false;
    /** Радиус удара по области. 0 — бьём только по цели. */
    public int attackArea = 0;
    /** Размер персонажа в процентах. */
    public int playerScale = 100;

    /** Свободную камеру один раз перенесли со старой N на F. */
    public boolean freecamKeyMoved = false;
    public boolean brightKeyMoved = false;   // яркость переехала с G на K (G занял телепорт)
    public boolean autoEat = true;           // авто-еда при голоде
    public boolean freecamHint = true;

    // ------------------------------------------------------------ зум

    /** Приближение по клавише. */
    public boolean zoomEnabled = true;
    /** Угол обзора в приближении, градусов. */
    public int zoomFov = 25;

    // ------------------------------------------------------------ игроки рядом

    /** Радиус для /near и списка «игроки рядом». */
    public int nearRange = 128;
    /** Показывать здоровье в списке. */
    public boolean nearHealth = true;

    // ------------------------------------------------------------ небо

    public boolean customSky = false;
    /** Встроенный пресет: sunset, nebula, aurora, void, dawn. Пусто — своя картинка. */
    public String skyPreset = "nebula";
    /** Имя файла в config/narevisuals/images для своего неба. */
    public String skyImage = "";
    /** Медленное вращение неба. */
    public boolean skySpin = true;
    /** Оставить солнце, луну и звёзды поверх своего неба. */
    public boolean skyKeepSun = true;

    // ------------------------------------------------------------ FPS+

    /** Не рисовать лишнее, когда его становится слишком много. */
    public boolean fpsPlus = false;

    /** Сколько штук терпим, прежде чем начать прятать. 0 — не прятать. */
    public int fpsTntOver = 24;
    public int fpsMobsOver = 60;
    public int fpsItemsOver = 80;
    public int fpsOrbsOver = 40;

    /** Дальше этого не рисуем ничего. 0 — без ограничения. */
    public int fpsRange = 0;

    /** Ванильные частицы на минимум, пока FPS+ включён. */
    public boolean fpsLessParticles = false;

    /** Беречь кадры на взрывах: предел частиц и одновременных «бумов». */
    public boolean fpsExplosions = true;

    /** Сколько частиц разрешаем добавить за тик. 0 — не ограничивать. */
    public int fpsParticleBudget = 600;

    /** Сколько звуков взрыва звучит разом. 0 — не ограничивать. */
    public int fpsBoomsAtOnce = 4;

    /** Прятать и игроков тоже. По умолчанию их не трогаем. */
    public boolean fpsHidePlayers = false;

    // ----------------------------------------------------------- голос

    /** Голосовой чат. */
    public boolean voiceEnabled = true;

    /** 0 — говорить, пока держишь клавишу; 1 — нажал и говоришь; 2 — по голосу. */
    public int voiceMode = 0;

    /** Усиление микрофона в процентах. */
    public int voiceMicGain = 100;

    /** Громкость чужих голосов в процентах. */
    public int voiceVolume = 100;

    /** Порог срабатывания в режиме «по голосу», 0..100. */
    public int voiceThreshold = 12;

    /** За сколько блоков ещё слышно. Сервер может урезать. */
    public int voiceDistance = 48;

    /**
     * Трогал ли игрок ползунок дальности. Пока не трогал — слушаем столько,
     * сколько сказал сервер: иначе на сервере с общим голосом человек всё
     * равно слышал бы только на 48 блоков и не понял бы почему.
     */
    public boolean voiceDistanceCustom = false;

    /** Имя микрофона. Пусто — системный по умолчанию. */
    public String voiceDevice = "";

    /** Свой микрофон выключен — вас не слышно, но вы слышите. */
    public boolean voiceMuteMic = false;

    /** Выключен звук: никого не слышно. */
    public boolean voiceMuteAll = false;

    /**
     * Громкость отдельных людей, 0..200 процентов. -1 — заглушён совсем.
     * Ключ — ник, как его прислал сервер.
     */
    public java.util.Map<String, Integer> voicePeople = new java.util.LinkedHashMap<>();

    /**
     * Ошибки, которые человек попросил больше не показывать. Хранятся по
     * короткому ключу, а не по тексту: текст может меняться от случая к случаю.
     */
    public java.util.List<String> errorsIgnored = new java.util.ArrayList<>();

    /**
     * Команды, которые мод отправляет сам при входе в мир или на сервер.
     * Со слешем — команда, без слеша — обычное сообщение в чат.
     */
    public java.util.List<String> joinCommands = new java.util.ArrayList<>();

    /** Оформление как в обычном Minecraft: мод ничего не подменяет на экране. */
    public boolean vanillaLook = false;

    /** Просить плагин увести на спавн, когда рядом рвётся слишком много. */
    public boolean panicRescue = true;

    /** Ниже скольки кадров считаем, что игра уже не тянет. */
    public int panicFps = 12;

    /** И сколько зажжённой ТНТ рядом для этого должно быть. */
    public int panicTnt = 40;

    // ---------------------------------------------------------- инвентарь

    /** Доставать тотем в руку, когда здоровья осталось мало. */
    public boolean autoTotem = false;

    /** При скольки сердцах доставать тотем. */
    public int autoTotemAt = 6;

    /** Надевать лучшую броню из инвентаря, когда свободен слот. */
    public boolean autoArmor = false;

    /** Менять броню и на более крепкую, а не только пустые слоты. */
    public boolean autoArmorUpgrade = true;

    /** Не показывать на игроке никаких эффектов, кроме перечисленных. */
    public boolean effectsFilter = false;

    /**
     * Что показывать, когда фильтр включён. Ключи — как в игре:
     * minecraft:speed, minecraft:strength и так далее.
     */
    public java.util.List<String> effectsKeep = new java.util.ArrayList<>(
            java.util.List.of("minecraft:speed", "minecraft:strength",
                              "minecraft:regeneration", "minecraft:fire_resistance"));

    // ------------------------------------------------------------- огонь

    /** Не рисовать огонь на весь экран, когда горим. */
    public boolean fireOff = false;

    /** Насколько уменьшить картинку огня, в процентах от обычной. */
    public int fireSize = 45;

    /** Насколько уменьшить щит в руке, в процентах. */
    public int shieldSize = 70;

    /**
     * Держать настройки мода на сервере, в папке плагина. Тогда они
     * переезжают вместе с игроком на другой компьютер.
     */
    /** Сборный пункт для вкладки «Вместе»: адрес:порт. */
    public String togetherHost = "151.245.217.104:2445";

    /** Все известные сборные пункты, через точку с запятой. */
    public String togetherHosts = "151.245.217.104:2445";

    /** Порт, на котором открывать мир. 0 — выбрать свободный самому. */
    public int togetherPort = 0;

    public boolean syncToServer = true;

    /** Виды, которые не рисуем совсем. Ключи реестра. */
    public java.util.List<String> fpsNever = new java.util.ArrayList<>();

    public Map<String, Element> elements = new LinkedHashMap<>();

    // ------------------------------------------------------------------

    private static Config instance;
    private static final Gson GSON = new GsonBuilder().setPrettyPrinting().create();

    public static Config get() {
        if (instance == null) load();
        return instance;
    }

    public static Path file() {
        return FMLPaths.CONFIGDIR.get().resolve("narevisuals.json");
    }

    /** Папка мода: туда кладутся картинки и пресеты .nvc. */
    public static Path folder() {
        return FMLPaths.CONFIGDIR.get().resolve("narevisuals");
    }

    public static Path images() {
        return folder().resolve("images");
    }

    public static void load() {
        Config loaded = null;
        try {
            Path path = file();
            if (Files.exists(path)) {
                String json = new String(Files.readAllBytes(path), StandardCharsets.UTF_8);
                loaded = GSON.fromJson(json, Config.class);
            }
        } catch (Exception ignored) {
            // битый конфиг не должен мешать запуску — просто начнём с настроек по умолчанию
        }

        if (loaded == null) loaded = new Config();
        if (loaded.elements == null) loaded.elements = new LinkedHashMap<>();
        if (loaded.fpsNever == null) loaded.fpsNever = new java.util.ArrayList<>();
        if (loaded.crosshairImage == null) loaded.crosshairImage = "";
        if (loaded.skyImage == null) loaded.skyImage = "";
        if (loaded.skyPreset == null) loaded.skyPreset = "";
        if (loaded.loginCommand == null) loaded.loginCommand = "login";
        if (loaded.loginPassword == null) loaded.loginPassword = "";
        instance = loaded;
    }

    public static void save() {
        try {
            Path path = file();
            Files.createDirectories(path.getParent());
            Files.write(path, GSON.toJson(get()).getBytes(StandardCharsets.UTF_8));
        } catch (IOException ignored) {
        }

        // и на сервер — но не сразу, а когда человек перестанет крутить
        try { com.winnigames2024.forge_narevisuals.client.NareCloud.touched(); }
        catch (Throwable ignored) { /* сервера может и не быть */ }
    }

    /** Полностью заменить настройки (после загрузки пресета .nvc). */
    public static void replace(Config cfg) {
        if (cfg == null) return;
        if (cfg.elements == null) cfg.elements = new LinkedHashMap<>();
        instance = cfg;
    }

    public static String toJson() {
        return GSON.toJson(get());
    }

    public static Config fromJson(String json) {
        return GSON.fromJson(json, Config.class);
    }

    /** Настройки элемента; если их ещё нет — создаются со значениями по умолчанию. */
    public Element element(String id, boolean enabled, float x, float y) {
        Element e = elements.get(id);
        if (e == null) {
            e = new Element(enabled, x, y);
            elements.put(id, e);
        }
        return e;
    }
}
