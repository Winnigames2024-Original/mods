package com.winnigames2024.geodash;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.List;

/** Уровень .gdl — тот же формат, что у игры на Windows. */
public class Level {

    public static final String[] PORTALS = {
            "None","ModeCube","ModeShip","ModeBall","ModeUfo","ModeWave","ModeRobot","ModeSpider","ModeWorm",
            "GravityUp","GravityDown","GravityToggle","Mini","Big","Dual","Single","Mirror","Unmirror",
            "SpeedSlow","SpeedNormal","SpeedFast","SpeedFaster","SpeedFastest","SpeedHyper","SpeedUltra","Teleport"};
    public static final int P_NONE=0,P_CUBE=1,P_SHIP=2,P_BALL=3,P_UFO=4,P_WAVE=5,P_ROBOT=6,P_SPIDER=7,P_WORM=8,
            P_GUP=9,P_GDOWN=10,P_GTOG=11,P_MINI=12,P_BIG=13,P_DUAL=14,P_SINGLE=15,P_MIRROR=16,P_UNMIRROR=17,
            P_S05=18,P_S1=19,P_S2=20,P_S3=21,P_S4=22,P_S5=23,P_S6=24,P_TP=25;

    public static final String[] ORBS = {"Yellow","Pink","Blue","Green","Red","Black","Dash","GravityDash","Spider","Toggle"};
    public static final String[] PADS = {"Yellow","Pink","Blue","Red","Spider"};
    public static final String[] MODES = {"Cube","Ship","Ball","Ufo","Wave","Robot","Spider","Worm"};
    public static final String[] SPEEDS = {"Slow","Normal","Fast","Faster","Fastest","Hyper","Ultra"};
    public static final String[] DIFFS = {"NA","Auto","Easy","Normal","Hard","Harder","Insane",
            "EasyDemon","MediumDemon","HardDemon","InsaneDemon","ExtremeDemon"};
    public static final String[] TRIGS = {"Move","Rotate","Scale","Alpha","Toggle","Spawn","Stop","Pulse","Color",
            "BgColor","GroundColor","Shake","Follow","FollowPlayerY","Animate","Touch","Count","InstantCount",
            "Pickup","Collision","InstantCollision","Random","Timer","Camera","CameraZoom","Song","Gravity",
            "Teleport","OnDeath","Trail","HidePlayer","ShowPlayer","Options","Checkpoint","End","Reverse",
            "Keyframe","SpeedChange",
            // 2.2
            "Gradient","Particles","Shockwave","Chromatic","Pixelate","Glitch","InvertColor","Grayscale",
            "Blur","Vignette","TimeWarp","AreaMove","AreaRotate","AreaScale","AreaFade","AreaTint",
            "Sequence","AdvancedFollow","CameraEdge","CameraGuide","CameraStatic","CameraRotate",
            "Event","Reset","ItemEdit","ItemCompare","PersistentItem","ToggleBlock","LinkVisible",
            "Sfx","EditSong","GameplayOffset","RotateGameplay","EnterEffect","Flash","Bump"};
    public static final int T_MOVE=0,T_ROTATE=1,T_SCALE=2,T_ALPHA=3,T_TOGGLE=4,T_SPAWN=5,T_STOP=6,T_PULSE=7,
            T_COLOR=8,T_BG=9,T_GROUND=10,T_SHAKE=11,T_FOLLOW=12,T_FOLLOWY=13,T_ANIMATE=14,T_TOUCH=15,T_COUNT=16,
            T_ICOUNT=17,T_PICKUP=18,T_COLLISION=19,T_ICOLLISION=20,T_RANDOM=21,T_TIMER=22,T_CAMERA=23,T_ZOOM=24,
            T_SONG=25,T_GRAVITY=26,T_TELEPORT=27,T_ONDEATH=28,T_TRAIL=29,T_HIDE=30,T_SHOW=31,T_OPTIONS=32,
            T_CHECKPOINT=33,T_END=34,T_REVERSE=35,T_KEYFRAME=36,T_SPEED=37,
            // 2.2
            T_GRADIENT=38,T_PARTICLES=39,T_SHOCK=40,T_CHROMATIC=41,T_PIXELATE=42,T_GLITCH=43,
            T_INVERT=44,T_GRAY=45,T_BLUR=46,T_VIGNETTE=47,T_TIMEWARP=48,T_AREAMOVE=49,T_AREAROT=50,
            T_AREASCALE=51,T_AREAFADE=52,T_AREATINT=53,T_SEQUENCE=54,T_ADVFOLLOW=55,T_CAMEDGE=56,
            T_CAMGUIDE=57,T_CAMSTATIC=58,T_CAMROTATE=59,T_EVENT=60,T_RESET=61,T_ITEMEDIT=62,
            T_ITEMCMP=63,T_PERSIST=64,T_TOGGLEBLOCK=65,T_LINKVIS=66,T_SFX=67,T_EDITSONG=68,
            T_GPOFFSET=69,T_ROTGAMEPLAY=70,T_ENTER=71,T_FLASH=72,T_BUMP=73;

    static int indexOf(String[] arr, String v, int def) {
        if (v == null) return def;
        for (int i = 0; i < arr.length; i++) if (arr[i].equalsIgnoreCase(v)) return i;
        try { return Integer.parseInt(v); } catch (Exception e) { return def; }
    }

    // ------------------------------------------------------------ настройки

    public static class Settings {
        public String name = "Уровень", desc = "", author = "", songName = "", songArtist = "";
        public int startMode = Gd.CUBE, startSpeed = 1, difficulty = 0;
        public boolean startMini, startDual, startMirror, startGravityUp, twoPlayer, reverse;
        public boolean platformerFreeMove;
        /** Тип уровня: мультиплеер, и сколько игроков в одной карте. */
        public boolean multiplayer;
        public int maxPlayers = 4;
        public int songId = 0;
        public double songOffset = 0, gravityScale = 1, lengthUnits = 0;
        public int bgColor = 0xFF2040A0, groundColor = 0xFF1A2E80, lineColor = 0xFFFFFFFF,
                objColor = 0xFF000000, obj2Color = 0xFFFFFFFF;
        public int version = 1, serverId = 0, copyMode = 0;
        public int bgId = 1, groundId = 1;
        public boolean isCopy;
        public int copiedFrom;
        public String copiedAuthor = "";
        public String localId = "", password = "", copyPassword = "";
    }

    public static class Trig {
        public int type = T_MOVE, targetGroup, centerGroup, colorChannel = 1, color = 0xFFFFFFFF;
        public double moveX, moveY, duration = 0.5, degrees, scaleX = 1, scaleY = 1, opacity = 1;
        public double spawnDelay, fadeTime = 0.5, pulseIn, pulseHold = 0.2, pulseOut = 0.3;
        public double shakeStrength = 10, shakeInterval = 0.05, followX = 1, followY = 1, followMax;
        public double chance = 50, timerSeconds = 1, camOffX, camOffY, camZoom = 1;
        public double songVolume = 1, songStart, teleportY, gravityMul = 1, easeRate = 2;
        public int itemId = 1, count = 1, targetCount = 1, groupB, blockA, songId, gravityDir, speed = 1, easing;
        public boolean activate = true, touchTriggered, spawnTriggered, multiTrigger, pulseToGroup = true,
                camStatic, songLoop, countMulti, blending, optChangeMode, optMini, optDual, optMirror;
        public int optMode = Gd.CUBE;
        // 2.2
        public double fxStrength = 0.6, areaRadius = 300, timeScale = 1, stepDelay = 0.25;
        public int color2 = 0xFF000000, eventId, compare, trueGroup, falseGroup;
        public String sfxName = "click";
    }

    public static class Obj {
        public int id, typeId = 1, zOrder, baseColor = 1, detailColor = 2;
        public double x, y, rotation, scaleX = 1, scaleY = 1, opacity = 1, teleportTargetY;
        public boolean flipX, flipY;
        public int portal = 0, orb = 0, pad = 0, coinIndex;
        public int[] groups = new int[0];
        public Trig trig;
        public String text = "";
        /** ID картинки с сайта для объекта «картинка». */
        public int imageId;
    }

    public Settings settings = new Settings();
    public List<Obj> objects = new ArrayList<>();
    public String raw = "";

    public double rightEdge() {
        double max = 30 * 40;
        for (Obj o : objects) if (o.x + 30 > max) max = o.x + 30;
        return max;
    }

    // ------------------------------------------------------------ запись

    /** Сохраняет уровень в тот же формат .gdl, что и игра на Windows. */
    public String toJson() {
        try {
            JSONObject root = new JSONObject();
            root.put("Format", 1);

            JSONObject s = new JSONObject();
            Settings t = settings;
            s.put("Name", t.name);
            s.put("Description", t.desc);
            s.put("Author", t.author);
            s.put("StartMode", MODES[clamp(t.startMode, MODES.length)]);
            s.put("StartSpeed", SPEEDS[clamp(t.startSpeed, SPEEDS.length)]);
            s.put("StartMini", t.startMini);
            s.put("StartDual", t.startDual);
            s.put("StartMirror", t.startMirror);
            s.put("StartGravityUp", t.startGravityUp);
            s.put("Difficulty", DIFFS[clamp(t.difficulty, DIFFS.length)]);
            s.put("SongId", t.songId);
            s.put("SongName", t.songName);
            s.put("SongArtist", t.songArtist);
            s.put("SongOffsetSec", t.songOffset);
            s.put("BgColor", t.bgColor);
            s.put("GroundColor", t.groundColor);
            s.put("LineColor", t.lineColor);
            s.put("ObjColor", t.objColor);
            s.put("Obj2Color", t.obj2Color);
            s.put("TwoPlayer", t.twoPlayer);
            s.put("ReverseGameplay", t.reverse);
            s.put("PlatformerFreeMove", t.platformerFreeMove);
            s.put("Multiplayer", t.multiplayer);
            s.put("MaxPlayers", t.maxPlayers);
            s.put("GravityScale", t.gravityScale);
            s.put("LengthUnits", rightEdge());
            s.put("Version", t.version);
            s.put("LocalId", t.localId);
            s.put("ServerId", t.serverId);
            s.put("Password", t.password);
            s.put("CopyMode", t.copyMode);
            s.put("CopyPassword", t.copyPassword);
            s.put("BgId", t.bgId);
            s.put("GroundId", t.groundId);
            s.put("IsCopy", t.isCopy);
            s.put("CopiedFrom", t.copiedFrom);
            s.put("CopiedAuthor", t.copiedAuthor);
            root.put("Settings", s);

            JSONArray arr = new JSONArray();
            int id = 1;
            for (Obj o : objects) arr.put(objJson(o, o.id > 0 ? o.id : id++));
            root.put("Objects", arr);
            return root.toString();
        } catch (Exception e) {
            return raw == null ? "{}" : raw;
        }
    }

    static JSONObject trigJson(Trig t) throws Exception {
        JSONObject j = new JSONObject();
        j.put("Type", TRIGS[clamp(t.type, TRIGS.length)]);
        j.put("TargetGroup", t.targetGroup);
        j.put("MoveX", t.moveX);
        j.put("MoveY", t.moveY);
        j.put("Duration", t.duration);
        j.put("Degrees", t.degrees);
        j.put("ScaleX", t.scaleX);
        j.put("ScaleY", t.scaleY);
        j.put("Opacity", t.opacity);
        j.put("Activate", t.activate);
        j.put("SpawnDelay", t.spawnDelay);
        j.put("MultiTrigger", t.multiTrigger);
        j.put("TouchTriggered", t.touchTriggered);
        j.put("SpawnTriggered", t.spawnTriggered);
        j.put("ColorChannel", t.colorChannel);
        j.put("Color", t.color);
        j.put("FadeTime", t.fadeTime);
        j.put("PulseFadeIn", t.pulseIn);
        j.put("PulseHold", t.pulseHold);
        j.put("PulseFadeOut", t.pulseOut);
        j.put("PulseToGroup", t.pulseToGroup);
        j.put("ShakeStrength", t.shakeStrength);
        j.put("ShakeInterval", t.shakeInterval);
        j.put("ItemId", t.itemId);
        j.put("Count", t.count);
        j.put("TargetCount", t.targetCount);
        j.put("GroupB", t.groupB);
        j.put("BlockA", t.blockA);
        j.put("Chance", t.chance);
        j.put("TimerSeconds", t.timerSeconds);
        j.put("CamZoom", t.camZoom);
        j.put("SongId", t.songId);
        j.put("GravityDir", t.gravityDir);
        j.put("TeleportY", t.teleportY);
        j.put("Speed", SPEEDS[clamp(t.speed, SPEEDS.length)]);
        return j;
    }

    static int clamp(int v, int len) { return v < 0 ? 0 : (v >= len ? 0 : v); }

    // ------------------------------------------------------------ разбор

    public static Level parse(String json) {
        Level lv = new Level();
        lv.raw = json;
        try {
            JSONObject root = new JSONObject(json);
            JSONObject s = root.optJSONObject("Settings");
            if (s != null) {
                Settings t = lv.settings;
                t.name = s.optString("Name", t.name);
                t.desc = s.optString("Description", "");
                t.author = s.optString("Author", "");
                t.startMode = indexOf(MODES, s.optString("StartMode", "Cube"), 0);
                t.startSpeed = indexOf(SPEEDS, s.optString("StartSpeed", "Normal"), 1);
                t.difficulty = indexOf(DIFFS, s.optString("Difficulty", "NA"), 0);
                t.startMini = s.optBoolean("StartMini");
                t.startDual = s.optBoolean("StartDual");
                t.startMirror = s.optBoolean("StartMirror");
                t.startGravityUp = s.optBoolean("StartGravityUp");
                t.twoPlayer = s.optBoolean("TwoPlayer");
                t.reverse = s.optBoolean("ReverseGameplay");
                t.platformerFreeMove = s.optBoolean("PlatformerFreeMove", false);
                t.multiplayer = s.optBoolean("Multiplayer", false);
                t.maxPlayers = s.optInt("MaxPlayers", 4);
                t.songId = s.optInt("SongId", 0);
                t.songName = s.optString("SongName", "");
                t.songArtist = s.optString("SongArtist", "");
                t.songOffset = s.optDouble("SongOffsetSec", 0);
                t.gravityScale = s.optDouble("GravityScale", 1);
                t.lengthUnits = s.optDouble("LengthUnits", 0);
                t.bgColor = s.optInt("BgColor", t.bgColor);
                t.groundColor = s.optInt("GroundColor", t.groundColor);
                t.lineColor = s.optInt("LineColor", t.lineColor);
                t.objColor = s.optInt("ObjColor", t.objColor);
                t.obj2Color = s.optInt("Obj2Color", t.obj2Color);
                t.version = s.optInt("Version", 1);
                t.serverId = s.optInt("ServerId", 0);
                t.localId = s.optString("LocalId", "");
                t.password = s.optString("Password", "");
                t.copyMode = s.optInt("CopyMode", 0);
                t.copyPassword = s.optString("CopyPassword", "");
                t.bgId = s.optInt("BgId", 1);
                t.groundId = s.optInt("GroundId", 1);
                t.isCopy = s.optBoolean("IsCopy", false);
                t.copiedFrom = s.optInt("CopiedFrom", 0);
                t.copiedAuthor = s.optString("CopiedAuthor", "");
            }
            JSONArray arr = root.optJSONArray("Objects");
            if (arr != null) {
                for (int i = 0; i < arr.length(); i++) {
                    JSONObject o = arr.optJSONObject(i);
                    if (o == null) continue;
                    lv.objects.add(parseObj(o));
                }
            }
        } catch (Exception ignored) { }
        return lv;
    }

    /** Объект в JSON — тем же форматом, что понимает parseObj. */
    static JSONObject objJson(Obj o, int id) {
        JSONObject j = new JSONObject();
        try {
            j.put("Id", id > 0 ? id : o.id);
            j.put("TypeId", o.typeId);
            j.put("X", o.x);
            j.put("Y", o.y);
            j.put("Rotation", o.rotation);
            j.put("ScaleX", o.scaleX);
            j.put("ScaleY", o.scaleY);
            j.put("ZOrder", o.zOrder);
            j.put("BaseColorChannel", o.baseColor);
            j.put("DetailColorChannel", o.detailColor);
            j.put("Opacity", o.opacity);
            if (o.groups.length > 0) {
                JSONArray g = new JSONArray();
                for (int v : o.groups) g.put(v);
                j.put("Groups", g);
            }
            j.put("Portal", PORTALS[clamp(o.portal, PORTALS.length)]);
            j.put("Orb", ORBS[clamp(o.orb, ORBS.length)]);
            j.put("Pad", PADS[clamp(o.pad, PADS.length)]);
            if (o.coinIndex != 0) j.put("CoinIndex", o.coinIndex);
            if (o.teleportTargetY != 0) j.put("TeleportTargetY", o.teleportTargetY);
            if (o.text != null && !o.text.isEmpty()) j.put("Text", o.text);
            if (o.imageId != 0) j.put("ImageId", o.imageId);
            if (o.trig != null) j.put("Trigger", trigJson(o.trig));
        } catch (Exception ignored) { }
        return j;
    }

    static Obj parseObj(JSONObject o) {
        Obj r = new Obj();
        r.id = o.optInt("Id", 0);
        r.typeId = o.optInt("TypeId", 1);
        r.x = o.optDouble("X", 0);
        r.y = o.optDouble("Y", 0);
        r.rotation = o.optDouble("Rotation", 0);
        r.scaleX = o.optDouble("ScaleX", 1);
        r.scaleY = o.optDouble("ScaleY", 1);
        if (r.scaleX == 0) r.scaleX = 1;
        if (r.scaleY == 0) r.scaleY = 1;
        r.flipX = o.optBoolean("FlipX");
        r.flipY = o.optBoolean("FlipY");
        r.zOrder = o.optInt("ZOrder", 0);
        r.baseColor = o.optInt("BaseColorChannel", 1);
        r.detailColor = o.optInt("DetailColorChannel", 2);
        r.opacity = o.optDouble("Opacity", 1);
        r.portal = indexOf(PORTALS, o.optString("Portal", "None"), 0);
        r.orb = indexOf(ORBS, o.optString("Orb", "Yellow"), 0);
        r.pad = indexOf(PADS, o.optString("Pad", "Yellow"), 0);
        r.coinIndex = o.optInt("CoinIndex", 0);
        r.teleportTargetY = o.optDouble("TeleportTargetY", 0);
        r.text = o.optString("Text", "");
        r.imageId = o.optInt("ImageId", 0);

        JSONArray g = o.optJSONArray("Groups");
        if (g != null) {
            r.groups = new int[g.length()];
            for (int i = 0; i < g.length(); i++) r.groups[i] = g.optInt(i);
        }

        JSONObject t = o.optJSONObject("Trigger");
        if (t != null) r.trig = parseTrig(t);
        return r;
    }

    static Trig parseTrig(JSONObject t) {
        Trig x = new Trig();
        x.type = indexOf(TRIGS, t.optString("Type", "Move"), 0);
        x.fxStrength = t.optDouble("FxStrength", 0.6);
        x.areaRadius = t.optDouble("AreaRadius", 300);
        x.timeScale = t.optDouble("TimeScale", 1);
        x.stepDelay = t.optDouble("StepDelay", 0.25);
        x.color2 = t.optInt("Color2", 0xFF000000);
        x.eventId = t.optInt("EventId", 0);
        x.compare = t.optInt("Compare", 0);
        x.trueGroup = t.optInt("TrueGroup", 0);
        x.falseGroup = t.optInt("FalseGroup", 0);
        x.sfxName = t.optString("SfxName", "click");
        x.targetGroup = t.optInt("TargetGroup", 0);
        x.centerGroup = t.optInt("CenterGroup", 0);
        x.moveX = t.optDouble("MoveX", 0);
        x.moveY = t.optDouble("MoveY", 0);
        x.duration = t.optDouble("Duration", 0.5);
        x.easing = indexOf(new String[]{"None","EaseInOut","EaseIn","EaseOut","ElasticInOut","ElasticIn",
                "ElasticOut","BounceInOut","BounceIn","BounceOut","ExponentialInOut","SineInOut","BackInOut"},
                t.optString("Easing", "None"), 0);
        x.easeRate = t.optDouble("EaseRate", 2);
        x.degrees = t.optDouble("Degrees", 0);
        x.scaleX = t.optDouble("ScaleX", 1);
        x.scaleY = t.optDouble("ScaleY", 1);
        x.opacity = t.optDouble("Opacity", 1);
        x.activate = t.optBoolean("Activate", true);
        x.spawnDelay = t.optDouble("SpawnDelay", 0);
        x.multiTrigger = t.optBoolean("MultiTrigger");
        x.touchTriggered = t.optBoolean("TouchTriggered");
        x.spawnTriggered = t.optBoolean("SpawnTriggered");
        x.colorChannel = t.optInt("ColorChannel", 1);
        x.color = t.optInt("Color", 0xFFFFFFFF);
        x.fadeTime = t.optDouble("FadeTime", 0.5);
        x.blending = t.optBoolean("Blending");
        x.pulseIn = t.optDouble("PulseFadeIn", 0);
        x.pulseHold = t.optDouble("PulseHold", 0.2);
        x.pulseOut = t.optDouble("PulseFadeOut", 0.3);
        x.pulseToGroup = t.optBoolean("PulseToGroup", true);
        x.shakeStrength = t.optDouble("ShakeStrength", 10);
        x.shakeInterval = t.optDouble("ShakeInterval", 0.05);
        x.followX = t.optDouble("FollowXMod", 1);
        x.followY = t.optDouble("FollowYMod", 1);
        x.followMax = t.optDouble("FollowMaxSpeed", 0);
        x.itemId = t.optInt("ItemId", 1);
        x.count = t.optInt("Count", 1);
        x.targetCount = t.optInt("TargetCount", 1);
        x.countMulti = t.optBoolean("CountMultiActivate");
        x.blockA = t.optInt("BlockA", 0);
        x.groupB = t.optInt("GroupB", 0);
        x.chance = t.optDouble("Chance", 50);
        x.timerSeconds = t.optDouble("TimerSeconds", 1);
        x.camOffX = t.optDouble("CamOffsetX", 0);
        x.camOffY = t.optDouble("CamOffsetY", 0);
        x.camZoom = t.optDouble("CamZoom", 1);
        x.camStatic = t.optBoolean("CamStatic");
        x.songId = t.optInt("SongId", 0);
        x.songVolume = t.optDouble("SongVolume", 1);
        x.songStart = t.optDouble("SongStartSec", 0);
        x.songLoop = t.optBoolean("SongLoop");
        x.gravityDir = t.optInt("GravityDir", 0);
        x.gravityMul = t.optDouble("GravityMul", 1);
        x.teleportY = t.optDouble("TeleportY", 0);
        x.speed = indexOf(SPEEDS, t.optString("Speed", "Normal"), 1);
        x.optChangeMode = t.optBoolean("OptChangeMode");
        x.optMode = indexOf(MODES, t.optString("OptMode", "Cube"), 0);
        x.optMini = t.optBoolean("OptMini");
        x.optDual = t.optBoolean("OptDual");
        x.optMirror = t.optBoolean("OptMirror");
        return x;
    }
}
