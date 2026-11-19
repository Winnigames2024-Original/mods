package com.winnigames2024.geodash;

import android.graphics.RectF;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Random;

/** Движок игры: физика всех режимов, порталы, шары, пады, триггеры. */
public class Engine {

    // ------------------------------------------------------------ константы

    static final double CUBE_G = 2800, CUBE_JUMP = 661;
    // плавание: тянет слабее, зажатая кнопка гребёт вверх; в лаве живём пять секунд
    static final double SWIM_G = 0.34, SWIM_PUSH = 1750, SWIM_MAX = 340, LAVA_SECONDS = 5.0;
    static final double BALL_G = 2500, BALL_MAX = 1100;
    static final double SHIP_G = 1500, SHIP_THRUST = 3000, SHIP_MAX = 520;
    static final double UFO_G = 2300, UFO_JUMP = 520;
    static final double WAVE_SLOPE = 1.0, WAVE_SLOPE_MINI = 2.0;
    static final double ROBOT_G = 2800, ROBOT_JUMP = 430, ROBOT_THRUST = 3400, ROBOT_HOLD = 0.28;
    static final double WORM_G = 2600, WORM_MAX = 1200;
    static final double ORB_YELLOW = 661, ORB_PINK = 460, ORB_RED = 900;
    static final double PAD_YELLOW = 700, PAD_PINK = 500, PAD_RED = 1000;

    public static class Player {
        public double x, y, vy, rotation, gravityMul = 1, wormBurrow, spiderFlash, robotHold;
        /** След телепорта: откуда шёл и сколько ещё рисовать. */
        public double teleFromY, teleT;
        /** Червяк просит прорыться сквозь блок. */
        public boolean wormDig;
        public int gravityDir = 1, mode = Gd.CUBE, speed = 1, index;
        public boolean mini, mirror, onGround, onCeiling, hidden, noClipWorm, robotJumping, trailOn = true;
        /** В воде или лаве — плавает; в лаве ещё и горит. */
        public boolean inLiquid, inLava;
        public double lavaTime;
        public final List<float[]> waveTrail = new ArrayList<>();

        public double size() { return 30 * (mini ? 0.6 : 1.0); }
        public double speedUnits() { return Gd.speedUnits(speed); }

        public RectF box() {
            float s = (float) size();
            return new RectF((float) x - s / 2, (float) y - s / 2, (float) x + s / 2, (float) y + s / 2);
        }

        public RectF inner() {
            float s = (float) (size() * 0.62);
            return new RectF((float) x - s / 2, (float) y - s / 2, (float) x + s / 2, (float) y + s / 2);
        }

        public double gravity() {
            double g;
            switch (mode) {
                case Gd.BALL: g = BALL_G; break;
                case Gd.SHIP: g = SHIP_G; break;
                case Gd.UFO: g = UFO_G; break;
                case Gd.ROBOT: g = ROBOT_G; break;
                case Gd.WORM: g = WORM_G; break;
                default: g = CUBE_G;
            }
            return g * gravityMul * (mini ? 1.15 : 1.0);
        }

        public void jump(double power) { vy = power * gravityDir; onGround = false; onCeiling = false; }
        public void flip() { gravityDir = -gravityDir; onGround = false; onCeiling = false; }

        public void reset(double px, double py, Level.Settings s) {
            x = px; y = py; vy = 0;
            mode = s.startMode; speed = s.startSpeed;
            mini = s.startMini; mirror = s.startMirror;
            gravityDir = s.startGravityUp ? -1 : 1;
            gravityMul = s.gravityScale <= 0 ? 1 : s.gravityScale;
            rotation = 0; onGround = onCeiling = false;
            robotHold = 0; robotJumping = false; wormBurrow = 0; hidden = false;
            waveTrail.clear();
        }
    }

    public static class ObjState {
        public double dx, dy, rot, sx = 1, sy = 1, alpha = 1, pulseT, pulseScale = 1;
        public boolean enabled = true, collected, fired, dynamic;
        public int pulseColor; public boolean hasPulse;
    }

    // ------------------------------------------------------------ состояние

    public Level level;
    public ObjState[] st = new ObjState[0];
    public final Player p1 = new Player(), p2 = new Player();
    public boolean dual, reverse, dead, finished, practice, noclip;
    public double time, progress, deathTimer;
    public int attempts = 1, coinsGot;
    public double camX, camY, camZoom = 1, camOffX, camOffY, shakeX, shakeY;
    double shakeStrength, shakeInterval, shakeTimer, shakeT;
    boolean camStatic;
    /** Экранные эффекты и прочее из 2.2. */
    public final Fx fx = new Fx();
    public final java.util.Set<Integer> events = new java.util.HashSet<>();
    public final java.util.Set<Integer> persistentItems = new java.util.HashSet<>();
    public double gameplayOffX, gameplayOffY, gameplayAngle;

    public double groundY = 0, roofY = 1e7, rightEdge = 1200;   // потолка у уровня нет
    public final Map<Integer, Integer> items = new HashMap<>();
    public final List<double[]> checkpoints = new ArrayList<>();   // x,y,vy,gravityDir,mode,speed,mini,time

    public interface Listener {
        void sfx(String name);
        void song(int id, double volume, double start, boolean loop);
        void onDeath();
        void onFinish();
    }
    public Listener listener;

    final Map<Integer, List<Integer>> grid = new HashMap<>();
    final List<Integer> dynamic = new ArrayList<>();
    final Map<Integer, List<Integer>> groups = new HashMap<>();
    final List<Integer> trigOrder = new ArrayList<>();
    int trigPtr;
    final Random rnd = new Random();
    static final int CELL = 120;

    // триггеры в работе
    static class Act {
        int kind;            // 0 move, 1 rot, 2 scale, 3 alpha, 4 follow
        List<Integer> targets;
        double[] fromA, fromB;
        double a, b, dur, t;
        int easing, group;
        double lastPx, lastPy, maxSpeed;
        boolean playerY;
        // площадные триггеры 2.2
        int areaType, tint;
        double deg, sx = 1, sy = 1, alpha = 1;
        double[] power;
    }
    final List<Act> acts = new ArrayList<>();
    static class Spawn { int group; double delay; int selfIdx = -1; }
    final List<Spawn> spawns = new ArrayList<>();
    static class TimerAct { double t; int group; }
    final List<TimerAct> timers = new ArrayList<>();
    final List<Level.Trig> onDeathTrigs = new ArrayList<>();
    final List<Level.Trig> counts = new ArrayList<>();

    // цветовые каналы
    public final Map<Integer, int[]> colors = new HashMap<>();   // id -> {current, from, to}
    final Map<Integer, double[]> colorFade = new HashMap<>();    // id -> {t, dur}
    public static final int CH_BG = 1000, CH_GROUND = 1001, CH_LINE = 1002, CH_P1 = 1005, CH_P2 = 1006;

    // ------------------------------------------------------------ init

    public void init(Level lv, boolean practiceMode) {
        level = lv;
        practice = practiceMode;
        st = new ObjState[lv.objects.size()];
        for (int i = 0; i < st.length; i++) st[i] = new ObjState();
        buildIndex();
        rightEdge = lv.rightEdge();
        reverse = lv.settings.reverse;
        platformer = lv.settings.platformerFreeMove;
        restart();
    }

    void buildIndex() {
        grid.clear(); dynamic.clear(); groups.clear(); trigOrder.clear();
        List<Integer> movingGroups = new ArrayList<>();

        for (int i = 0; i < level.objects.size(); i++) {
            Level.Obj o = level.objects.get(i);
            for (int g : o.groups) {
                List<Integer> l = groups.get(g);
                if (l == null) groups.put(g, l = new ArrayList<>());
                l.add(i);
            }
            if (Catalog.get(o.typeId).kind == Catalog.TRIGGER) trigOrder.add(i);
            if (o.trig != null) {
                int t = o.trig.type;
                if (t == Level.T_MOVE || t == Level.T_ROTATE || t == Level.T_SCALE
                        || t == Level.T_FOLLOW || t == Level.T_FOLLOWY || t == Level.T_KEYFRAME) {
                    if (o.trig.targetGroup != 0) movingGroups.add(o.trig.targetGroup);
                }
            }
        }

        for (int i = 0; i < level.objects.size(); i++) {
            Level.Obj o = level.objects.get(i);
            boolean dyn = false;
            for (int g : o.groups) if (movingGroups.contains(g)) { dyn = true; break; }
            if (dyn) { dynamic.add(i); st[i].dynamic = true; continue; }
            Catalog.Def d = Catalog.get(o.typeId);
            int c0 = (int) Math.floor(o.x / CELL), c1 = (int) Math.floor((o.x + d.w * o.scaleX) / CELL);
            for (int c = c0; c <= c1; c++) {
                List<Integer> l = grid.get(c);
                if (l == null) grid.put(c, l = new ArrayList<>());
                l.add(i);
            }
        }

        // сортировка триггеров по X
        trigOrder.sort((a, b) -> Double.compare(level.objects.get(a).x, level.objects.get(b).x));
    }

    public void restart() {
        dead = false; finished = false; time = 0; progress = 0; deathTimer = 0;
        for (ObjState s : st) {
            s.dx = s.dy = 0; s.rot = 0; s.sx = s.sy = 1; s.alpha = 1;
            s.enabled = true; s.fired = false; s.collected = false; s.hasPulse = false; s.pulseT = 0;
        }
        items.clear();
        acts.clear(); spawns.clear(); timers.clear(); onDeathTrigs.clear(); counts.clear();
        trigPtr = 0;
        coinsGot = 0;
        dual = level.settings.startDual;

        colors.clear(); colorFade.clear();
        setColor(CH_BG, level.settings.bgColor);
        setColor(CH_GROUND, level.settings.groundColor);
        setColor(CH_LINE, level.settings.lineColor);
        setColor(1, level.settings.objColor);
        setColor(2, level.settings.obj2Color);
        setColor(CH_P1, 0xFF46DCFF);
        setColor(CH_P2, 0xFFFFDC46);

        if (practice && !checkpoints.isEmpty()) {
            double[] cp = checkpoints.get(checkpoints.size() - 1);
            p1.reset(cp[0], cp[1], level.settings);
            p1.vy = cp[2]; p1.gravityDir = (int) cp[3]; p1.mode = (int) cp[4];
            p1.speed = (int) cp[5]; p1.mini = cp[6] > 0.5; time = cp[7];
            while (trigPtr < trigOrder.size() && level.objects.get(trigOrder.get(trigPtr)).x < cp[0]) trigPtr++;
        } else {
            p1.reset(0, 15, level.settings);
        }
        p2.reset(p1.x, p1.y + (dual ? 240 : 0), level.settings);
        p2.gravityDir = -p1.gravityDir;
        p2.index = 1;

        camX = p1.x + 180; camY = p1.y; camZoom = 1; camOffX = camOffY = 0;
        shakeTimer = 0; shakeX = shakeY = 0; camStatic = false;
        fx.reset(); events.clear();
        gameplayOffX = gameplayOffY = gameplayAngle = 0;
    }

    void setColor(int id, int argb) { colors.put(id, new int[]{argb, argb, argb}); }

    public int color(int id) {
        int[] c = colors.get(id);
        if (c == null) return 0xFFFFFFFF;
        return c[0];
    }

    // ------------------------------------------------------------ шаг

    /** Платформер: игрок сам ходит влево-вправо. */
    public boolean platformer;
    /** -1 влево, +1 вправо, 0 стоим (только для платформера). */
    public int moveX;

    public void update(double dt, boolean hold1, boolean pressed1, boolean hold2, boolean pressed2) {
        fx.update(dt);
        dt *= Math.max(0.1, Math.min(4, fx.timeScale));
        if (dead) { deathTimer += dt; return; }
        if (finished) { time += dt; return; }

        time += dt;
        updateColors(dt);
        updateActs(dt);

        step(p1, dt, hold1, pressed1);
        if (dual) step(p2, dt, level.settings.twoPlayer ? hold2 : hold1,
                level.settings.twoPlayer ? pressed2 : pressed1);

        fireTriggersByX(p1.x);
        progress = Math.max(0, Math.min(1, p1.x / Math.max(1, rightEdge)));
        updateCamera(dt);

        for (ObjState s : st) if (s.pulseT > 0) { s.pulseT -= dt; if (s.pulseT <= 0) { s.hasPulse = false; s.pulseScale = 1; } }
    }

    void step(Player p, double dt, boolean hold, boolean pressed) {
        // платформер: едем туда, куда жмём, а не вперёд сами
        double vx = platformer
                ? p.speedUnits() * Math.max(-1, Math.min(1, moveX))
                : p.speedUnits() * (reverse ? -1 : 1);
        double prevY = p.y;
        p.x += vx * dt;

        p.noClipWorm = p.mode == Gd.WORM && overlapSolid(p);

        integrate(p, dt, hold, pressed);
        p.y += p.vy * dt;

        if (p.wormDig) { p.wormDig = false; wormDig(p); }
        if (p.teleT > 0) p.teleT = Math.max(0, p.teleT - dt);
        if (p.spiderFlash > 0) p.spiderFlash = Math.max(0, p.spiderFlash - dt);

        resolveSolids(p, prevY);
        resolveBounds(p);
        touchObjects(p, pressed);

        // в лаве можно пробыть пять секунд, потом сгораешь
        if (p.inLava) {
            if (p.lavaTime <= 0) sfx("orb");
            p.lavaTime += dt;
            if (p.lavaTime >= LAVA_SECONDS) { die(); return; }
        } else if (p.lavaTime > 0) {
            p.lavaTime = Math.max(0, p.lavaTime - dt * 0.8);
        }

        updateRotation(p, dt);

        if (p.mode == Gd.WAVE && p.trailOn) {
            p.waveTrail.add(new float[]{(float) p.x, (float) p.y});
            if (p.waveTrail.size() > 900) p.waveTrail.subList(0, 200).clear();
        }
    }

    void integrate(Player p, double dt, boolean hold, boolean pressed) {
        double g = -p.gravity() * p.gravityDir;

        // ---- в воде и лаве плывём
        if (p.inLiquid) {
            p.vy += g * SWIM_G * dt;
            if (p.mode == Gd.WORM && pressed) {
                p.gravityDir = -p.gravityDir;
                p.vy = 0;
            } else if (p.mode == Gd.BALL && pressed && (p.onGround || p.onCeiling)) {
                p.gravityDir = -p.gravityDir;
                p.vy = 0;
                p.onGround = p.onCeiling = false;
            } else if (hold) {
                p.vy += SWIM_PUSH * p.gravityDir * dt;
            }
            double lim = p.mode == Gd.WAVE ? SWIM_MAX * 0.8 : SWIM_MAX;
            p.vy = clamp(p.vy, -lim, lim);
            return;
        }

        switch (p.mode) {
            case Gd.CUBE:
                p.vy += g * dt;
                if (hold && p.onGround) p.jump(CUBE_JUMP);
                break;
            case Gd.SHIP:
                p.vy += (hold ? SHIP_THRUST * p.gravityDir : g) * dt;
                p.vy = clamp(p.vy, -SHIP_MAX, SHIP_MAX);
                break;
            case Gd.BALL:
                p.vy += g * dt;
                p.vy = clamp(p.vy, -BALL_MAX, BALL_MAX);
                if (pressed && (p.onGround || p.onCeiling)) { p.flip(); p.vy = 0; }
                break;
            case Gd.UFO:
                p.vy += g * dt;
                if (pressed) p.jump(UFO_JUMP);
                break;
            case Gd.WAVE: {
                double slope = p.mini ? WAVE_SLOPE_MINI : WAVE_SLOPE;
                p.vy = (hold ? 1 : -1) * p.gravityDir * p.speedUnits() * slope;
                break;
            }
            case Gd.ROBOT:
                p.vy += g * dt;
                if (pressed && p.onGround) {
                    p.robotJumping = true; p.robotHold = 0;
                    p.vy = ROBOT_JUMP * p.gravityDir; p.onGround = false;
                }
                if (p.robotJumping) {
                    if (hold && p.robotHold < ROBOT_HOLD) {
                        p.robotHold += dt;
                        p.vy += ROBOT_THRUST * p.gravityDir * dt;
                    } else p.robotJumping = false;
                }
                break;
            case Gd.SPIDER:
                p.vy += g * dt;
                if (pressed) spiderTeleport(p);
                break;
            case Gd.WORM:
                // червяк — перевёрнутый паук: по клику прорывается сквозь блок на другую сторону
                if (pressed && (p.onGround || p.onCeiling)) p.wormDig = true;
                if (p.noClipWorm) {
                    p.vy = 0;
                    p.wormBurrow = Math.min(1, p.wormBurrow + dt * 6);
                } else {
                    p.vy += g * dt;
                    p.vy = clamp(p.vy, -WORM_MAX, WORM_MAX);
                    p.wormBurrow = Math.max(0, p.wormBurrow - dt * 6);
                }
                break;
        }
    }

    void updateRotation(Player p, double dt) {
        switch (p.mode) {
            case Gd.CUBE: case Gd.ROBOT: case Gd.SPIDER:
                if (!p.onGround && !p.onCeiling) p.rotation += 418 * dt * p.gravityDir * (p.mirror ? -1 : 1);
                else p.rotation = Math.round(p.rotation / 90.0) * 90.0;
                break;
            case Gd.BALL:
                p.rotation += p.speedUnits() * dt * 1.6 * p.gravityDir * (p.mirror ? -1 : 1);
                break;
            case Gd.SHIP: case Gd.UFO: {
                double target = -clamp(p.vy * 0.045, -32, 32) * p.gravityDir;
                p.rotation += (target - p.rotation) * Math.min(1, dt * 14);
                break;
            }
            case Gd.WAVE: p.rotation = 0; break;
            case Gd.WORM: {
                double target = p.noClipWorm ? 0 : -clamp(p.vy * 0.03, -24, 24) * p.gravityDir;
                p.rotation += (target - p.rotation) * Math.min(1, dt * 12);
                break;
            }
        }
        if (p.rotation > 3600 || p.rotation < -3600) p.rotation %= 360;
    }

    // ------------------------------------------------------------ геометрия

    public RectF objHit(int i) {
        Level.Obj o = level.objects.get(i);
        ObjState s = st[i];
        Catalog.Def d = Catalog.get(o.typeId);
        double sx = o.scaleX * s.sx, sy = o.scaleY * s.sy;
        float x = (float) (o.x + s.dx + d.hx * sx), y = (float) (o.y + s.dy + d.hy * sy);
        return new RectF(x, y, x + (float) (d.hw * Math.abs(sx)), y + (float) (d.hh * Math.abs(sy)));
    }

    public RectF objBounds(int i) {
        Level.Obj o = level.objects.get(i);
        ObjState s = st[i];
        Catalog.Def d = Catalog.get(o.typeId);
        float x = (float) (o.x + s.dx), y = (float) (o.y + s.dy);
        return new RectF(x, y, x + (float) (d.w * o.scaleX * s.sx), y + (float) (d.h * o.scaleY * s.sy));
    }

    final List<Integer> scratch = new ArrayList<>();

    List<Integer> candidates(RectF r) {
        scratch.clear();
        int c0 = (int) Math.floor(r.left / CELL) - 1, c1 = (int) Math.floor(r.right / CELL) + 1;
        for (int c = c0; c <= c1; c++) {
            List<Integer> l = grid.get(c);
            if (l != null) for (int i : l) if (st[i].enabled) scratch.add(i);
        }
        for (int i : dynamic) if (st[i].enabled) scratch.add(i);
        return scratch;
    }

    static boolean hit(RectF a, RectF b) {
        return a.left < b.right && b.left < a.right && a.top < b.bottom && b.top < a.bottom;
    }

    boolean overlapSolid(Player p) {
        RectF box = p.box();
        box.inset(2, 2);
        for (int i : new ArrayList<>(candidates(box))) {
            Catalog.Def d = Catalog.get(level.objects.get(i).typeId);
            if (!d.solid || !d.wormPhase) continue;
            if (hit(objHit(i), box)) return true;
        }
        return false;
    }

    void resolveSolids(Player p, double prevY) {
        boolean worm = p.mode == Gd.WORM;
        p.onGround = false; p.onCeiling = false;
        List<Integer> list = new ArrayList<>(candidates(p.box()));

        for (int i : list) {
            if (level.objects.get(i).noTouch) continue;   // «без касания» — проходим насквозь
            Catalog.Def d = Catalog.get(level.objects.get(i).typeId);
            if (!d.solid) continue;
            RectF r = objHit(i);
            RectF box = p.box();
            if (!hit(r, box)) continue;

            if (worm && d.wormPhase) { p.noClipWorm = true; continue; }

            // склон не убивает: по нему заезжают наверх, как в игре
            if (d.kind == Catalog.SLOPE) {
                double lo = Math.min(r.top, r.bottom), hi = Math.max(r.top, r.bottom);
                double left = Math.min(rBottomX(r), rTopX(r));
                double wide = Math.max(1, Math.abs(rTopX(r) - rBottomX(r)));
                double t = Math.max(0, Math.min(1, (p.x - left) / wide));
                double frac = d.style == Catalog.ST_SLOPE_UP ? t : 1 - t;
                double surf = lo + (hi - lo) * frac;
                double halfS = p.size() / 2;

                if (p.gravityDir > 0) {
                    double want = surf + halfS;
                    if (p.y <= want + 4) {
                        p.y = want;
                        if (p.vy < 0) p.vy = 0;
                        p.onGround = true; p.robotJumping = false;
                    }
                } else {
                    double want = hi - (surf - lo) - halfS;
                    if (p.y >= want - 4) {
                        p.y = want;
                        if (p.vy > 0) p.vy = 0;
                        p.onGround = true; p.robotJumping = false;
                    }
                }
                continue;
            }

            double half = p.size() / 2;
            double prevBottom = prevY - half, prevTop = prevY + half;
            boolean fromAbove = prevBottom >= r.bottom - 0.6;   // в экранных координатах RectF top<bottom,
            boolean fromBelow = prevTop <= r.top + 0.6;          // но мир Y вверх: используем ниже свои поля

            // переводим прямоугольник мира: top = верх (большее Y)
            double rTop = Math.max(r.top, r.bottom), rBottom = Math.min(r.top, r.bottom);
            fromAbove = prevBottom >= rTop - 0.6;
            fromBelow = prevTop <= rBottom + 0.6;

            double overlapL = (p.x + half) - rBottomX(r), overlapR = rTopX(r) - (p.x - half);
            double halfW = Math.min(overlapL, overlapR);

            // стоим со стороны притяжения; удар другой стороной — смерть, как в игре
            boolean groundMode = p.mode == Gd.CUBE || p.mode == Gd.BALL
                    || p.mode == Gd.ROBOT || p.mode == Gd.SPIDER;

            if (fromAbove && p.vy <= 0.0001) {
                if (p.gravityDir < 0 && groundMode && Math.abs(p.vy) > 40) { die(); return; }
                p.y = rTop + half; p.vy = 0;
                if (p.gravityDir > 0) { p.onGround = true; p.robotJumping = false; } else p.onCeiling = true;
                continue;
            }
            if (fromBelow && p.vy >= -0.0001) {
                if (d.topOnly) continue;
                // прыгнул, а сверху блок — разбился
                if (p.gravityDir > 0 && groundMode && Math.abs(p.vy) > 40) { die(); return; }
                p.y = rBottom - half; p.vy = 0;
                if (p.gravityDir < 0) { p.onGround = true; p.robotJumping = false; } else p.onCeiling = true;
                continue;
            }
            if (d.topOnly) continue;
            if (halfW > 1.5) { die(); return; }
        }
    }

    static double rTopX(RectF r) { return r.right; }
    static double rBottomX(RectF r) { return r.left; }

    void resolveBounds(Player p) {
        double half = p.size() / 2;
        if (p.y - half < groundY) {
            p.y = groundY + half; p.vy = 0;
            if (p.gravityDir > 0) { p.onGround = true; p.robotJumping = false; } else p.onCeiling = true;
            if (p.mode == Gd.WAVE) die();
        }
        if (p.y + half > roofY) {
            p.y = roofY - half; p.vy = 0;
            if (p.gravityDir < 0) { p.onGround = true; p.robotJumping = false; } else p.onCeiling = true;
        }
    }

    void spiderTeleport(Player p) {
        int dir = -p.gravityDir;
        double half = p.size() / 2, from = p.y;
        double best = dir > 0 ? roofY - half : groundY + half;

        RectF probe = new RectF((float) (p.x - half + 2), (float) Math.min(from, best) - 2,
                (float) (p.x + half - 2), (float) Math.max(from, best) + 2);
        for (int i : new ArrayList<>(candidates(probe))) {
            Catalog.Def d = Catalog.get(level.objects.get(i).typeId);
            if (!d.solid) continue;
            RectF r = objHit(i);
            if (r.right <= p.x - half + 1 || r.left >= p.x + half - 1) continue;
            double rTop = Math.max(r.top, r.bottom), rBottom = Math.min(r.top, r.bottom);
            if (dir > 0) {
                double y = rBottom - half;
                if (y > from + 1 && y < best) best = y;
            } else {
                double y = rTop + half;
                if (y < from - 1 && y > best) best = y;
            }
        }
        p.teleFromY = p.y;
        p.teleT = 0.2;
        p.y = best; p.vy = 0;
        p.gravityDir = -p.gravityDir;
        p.spiderFlash = 0.18;
        sfx("orb");
    }

    /**
     * Червяк прорывается сквозь блок, на котором стоит: проходит всю сплошную толщу
     * и выходит с другой стороны, гравитация переворачивается. Нет места — остаёмся.
     */
    void wormDig(Player p) {
        double half = p.size() / 2;
        int dir = p.gravityDir > 0 ? -1 : 1;
        double face = p.gravityDir > 0 ? p.y - half : p.y + half;

        RectF probe = new RectF((float) (p.x - half + 2), (float) (face - 4000),
                (float) (p.x + half - 2), (float) (face + 4000));
        List<double[]> spans = new ArrayList<>();
        for (int i : new ArrayList<>(candidates(probe))) {
            Level.Obj o = level.objects.get(i);
            if (o.noTouch) continue;
            Catalog.Def d = Catalog.get(o.typeId);
            if (!d.solid) continue;
            RectF r = objHit(i);
            if (r.right <= p.x - half + 1 || r.left >= p.x + half - 1) continue;
            spans.add(new double[]{Math.min(r.top, r.bottom), Math.max(r.top, r.bottom)});
        }
        if (spans.isEmpty()) return;

        double edge = face;
        boolean moved = true;
        int guard = 0;
        while (moved && guard++ < 64) {
            moved = false;
            for (double[] sp : spans) {
                if (dir < 0) {
                    if (sp[1] >= edge - 1.5 && sp[0] < edge) { edge = sp[0]; moved = true; }
                } else {
                    if (sp[0] <= edge + 1.5 && sp[1] > edge) { edge = sp[1]; moved = true; }
                }
            }
        }
        if (Math.abs(edge - face) < 1) return;

        double targetY = dir < 0 ? edge - half : edge + half;
        if (targetY - half < groundY || targetY + half > roofY) return;

        RectF test = new RectF((float) (p.x - half + 2), (float) (targetY - half + 2),
                (float) (p.x + half - 2), (float) (targetY + half - 2));
        for (int i : new ArrayList<>(candidates(test))) {
            Level.Obj o = level.objects.get(i);
            if (o.noTouch) continue;
            Catalog.Def d = Catalog.get(o.typeId);
            if (!d.solid) continue;
            RectF r = objHit(i);
            if (r.right <= test.left || r.left >= test.right) continue;
            double lo = Math.min(r.top, r.bottom), hi = Math.max(r.top, r.bottom);
            if (hi > targetY - half + 1 && lo < targetY + half - 1) return;
        }

        p.teleFromY = p.y;
        p.teleT = 0.22;
        p.y = targetY;
        p.vy = 0;
        p.gravityDir = -p.gravityDir;
        p.onGround = true;
        p.onCeiling = false;
        p.wormBurrow = 1;
        sfx("portal");
    }

    void touchObjects(Player p, boolean pressed) {
        RectF box = p.box(), inner = p.inner();
        p.inLiquid = false;
        p.inLava = false;
        for (int i : new ArrayList<>(candidates(box))) {
            Level.Obj o = level.objects.get(i);
            ObjState s = st[i];
            if (!s.enabled || s.collected) continue;
            Catalog.Def d = Catalog.get(o.typeId);
            RectF r = objHit(i);

            switch (d.kind) {
                case Catalog.LIQUID:
                    if (hit(r, inner)) {
                        p.inLiquid = true;
                        if (d.style == Catalog.ST_LAVA) p.inLava = true;
                    }
                    break;
                case Catalog.HAZARD:
                    if (hit(r, inner)) { die(); return; }
                    break;
                case Catalog.PORTAL:
                    if (hit(r, box)) applyPortal(p, o, i);
                    break;
                case Catalog.ORB:
                    if (pressed && hit(r, box)) applyOrb(p, o, i);
                    break;
                case Catalog.PAD:
                    if (hit(r, box)) applyPad(p, o, i);
                    break;
                case Catalog.COIN:
                    if (hit(r, box)) {
                        s.collected = true;
                        if (o.typeId == 80) coinsGot++;
                        else addItem(o.coinIndex == 0 ? 1 : o.coinIndex, 1);
                        sfx("coin");
                    }
                    break;
                case Catalog.FINISH:
                    if (hit(r, box)) { finish(); return; }
                    break;
                case Catalog.DECO:
                    if (d.style == Catalog.ST_CHECKPOINT && practice && hit(r, box)) addCheckpoint(p);
                    break;
                case Catalog.TRIGGER:
                    if (o.trig != null && o.trig.touchTriggered && hit(r, box)) {
                        if (!s.fired || o.trig.multiTrigger) { s.fired = true; fire(o.trig, i); }
                    }
                    break;
            }
        }
    }

    void applyPortal(Player p, Level.Obj o, int idx) {
        switch (o.portal) {
            case Level.P_CUBE: p.mode = Gd.CUBE; break;
            case Level.P_SHIP: p.mode = Gd.SHIP; break;
            case Level.P_BALL: p.mode = Gd.BALL; break;
            case Level.P_UFO: p.mode = Gd.UFO; break;
            case Level.P_WAVE: p.mode = Gd.WAVE; p.waveTrail.clear(); break;
            case Level.P_ROBOT: p.mode = Gd.ROBOT; break;
            case Level.P_SPIDER: p.mode = Gd.SPIDER; break;
            case Level.P_WORM: p.mode = Gd.WORM; p.wormBurrow = 0; break;
            case Level.P_GDOWN: if (p.gravityDir != 1) p.flip(); break;
            case Level.P_GUP: if (p.gravityDir != -1) p.flip(); break;
            case Level.P_GTOG: p.flip(); break;
            case Level.P_MINI: p.mini = true; break;
            case Level.P_BIG: p.mini = false; break;
            case Level.P_MIRROR: p.mirror = true; break;
            case Level.P_UNMIRROR: p.mirror = false; break;
            case Level.P_DUAL:
                if (!dual) {
                    dual = true;
                    p2.reset(p1.x, p1.y + 150, level.settings);
                    p2.mode = p1.mode; p2.speed = p1.speed; p2.mini = p1.mini;
                    p2.gravityDir = -p1.gravityDir; p2.index = 1;
                }
                break;
            case Level.P_SINGLE: dual = false; break;
            case Level.P_S05: p.speed = 0; break;
            case Level.P_S1: p.speed = 1; break;
            case Level.P_S2: p.speed = 2; break;
            case Level.P_S3: p.speed = 3; break;
            case Level.P_S4: p.speed = 4; break;
            case Level.P_S5: p.speed = 5; break;
            case Level.P_S6: p.speed = 6; break;
            case Level.P_TP:
                if (o.teleportTargetY != 0) { p.y = o.teleportTargetY; p.vy = 0; }
                break;
            default: return;
        }
        if (!st[idx].fired) { st[idx].fired = true; sfx("portal"); }
    }

    void applyOrb(Player p, Level.Obj o, int idx) {
        switch (o.orb) {
            case 0: p.jump(ORB_YELLOW); break;
            case 1: p.jump(ORB_PINK); break;
            case 2: p.flip(); p.vy = 0; p.jump(240); break;
            case 3: p.flip(); p.jump(ORB_YELLOW); break;
            case 4: p.jump(ORB_RED); break;
            case 5: p.vy = -ORB_RED * p.gravityDir; break;
            case 6: p.vy = 0; break;
            case 7: p.flip(); p.vy = 0; break;
            case 8: spiderTeleport(p); break;
            default: p.flip(); break;
        }
        sfx("orb");
        st[idx].pulseT = 0.25;
        st[idx].pulseScale = 1.3;
    }

    void applyPad(Player p, Level.Obj o, int idx) {
        if (st[idx].pulseT > 0.1) return;
        switch (o.pad) {
            case 0: p.jump(PAD_YELLOW); break;
            case 1: p.jump(PAD_PINK); break;
            case 2: p.flip(); p.jump(200); break;
            case 3: p.jump(PAD_RED); break;
            default: spiderTeleport(p); break;
        }
        st[idx].pulseT = 0.3;
        st[idx].pulseScale = 1.25;
        sfx("orb");
    }

    // ------------------------------------------------------------ триггеры

    void fireTriggersByX(double x) {
        while (trigPtr < trigOrder.size()) {
            int i = trigOrder.get(trigPtr);
            Level.Obj o = level.objects.get(i);
            if (o.x > x) break;
            trigPtr++;
            if (o.trig == null || o.trig.touchTriggered || o.trig.spawnTriggered) continue;
            if (!st[i].enabled) continue;
            st[i].fired = true;
            fire(o.trig, i);
        }
    }

    void fireGroup(int group) {
        List<Integer> list = groups.get(group);
        if (list == null) return;
        for (int i : list) {
            Level.Obj o = level.objects.get(i);
            if (o.trig == null || !st[i].enabled) continue;
            st[i].fired = true;
            if (o.trig.spawnDelay > 0.0001) {
                Spawn s = new Spawn(); s.selfIdx = i; s.delay = o.trig.spawnDelay; spawns.add(s);
            } else execute(o.trig, o);
        }
    }

    void fire(Level.Trig t, int selfIdx) {
        if (t.spawnDelay > 0.0001 && t.type != Level.T_SPAWN) {
            Spawn s = new Spawn(); s.selfIdx = selfIdx; s.delay = t.spawnDelay; spawns.add(s);
            return;
        }
        execute(t, selfIdx >= 0 && selfIdx < level.objects.size() ? level.objects.get(selfIdx) : null);
    }

    void execute(Level.Trig t) { execute(t, null); }

    void execute(Level.Trig t, Level.Obj src) {
        List<Integer> list = groups.get(t.targetGroup);
        switch (t.type) {
            case Level.T_MOVE: case Level.T_KEYFRAME: {
                if (list == null) break;
                Act a = act(0, list, t.targetGroup, t.duration, t.easing);
                a.a = t.moveX; a.b = t.moveY;
                a.fromA = new double[list.size()]; a.fromB = new double[list.size()];
                for (int k = 0; k < list.size(); k++) { a.fromA[k] = st[list.get(k)].dx; a.fromB[k] = st[list.get(k)].dy; }
                if (a.dur <= 0) { for (int i : list) { st[i].dx += a.a; st[i].dy += a.b; } }
                else acts.add(a);
                break;
            }
            case Level.T_ROTATE: {
                if (list == null) break;
                Act a = act(1, list, t.targetGroup, t.duration, t.easing);
                a.a = t.degrees;
                a.fromA = new double[list.size()];
                for (int k = 0; k < list.size(); k++) a.fromA[k] = st[list.get(k)].rot;
                if (a.dur <= 0) { for (int i : list) st[i].rot += a.a; } else acts.add(a);
                break;
            }
            case Level.T_SCALE: {
                if (list == null) break;
                Act a = act(2, list, t.targetGroup, t.duration, t.easing);
                a.a = t.scaleX; a.b = t.scaleY;
                a.fromA = new double[list.size()]; a.fromB = new double[list.size()];
                for (int k = 0; k < list.size(); k++) { a.fromA[k] = st[list.get(k)].sx; a.fromB[k] = st[list.get(k)].sy; }
                if (a.dur <= 0) { for (int i : list) { st[i].sx = a.a; st[i].sy = a.b; } } else acts.add(a);
                break;
            }
            case Level.T_ALPHA: {
                if (list == null) break;
                Act a = act(3, list, t.targetGroup, t.duration, 0);
                a.a = t.opacity;
                a.fromA = new double[list.size()];
                for (int k = 0; k < list.size(); k++) a.fromA[k] = st[list.get(k)].alpha;
                if (a.dur <= 0) { for (int i : list) st[i].alpha = a.a; } else acts.add(a);
                break;
            }
            case Level.T_TOGGLE:
                if (list != null) for (int i : list) st[i].enabled = t.activate;
                break;
            case Level.T_SPAWN: {
                Spawn s = new Spawn(); s.group = t.targetGroup; s.delay = t.spawnDelay; spawns.add(s);
                break;
            }
            case Level.T_STOP: {
                for (int i = acts.size() - 1; i >= 0; i--) if (acts.get(i).group == t.targetGroup) acts.remove(i);
                for (int i = spawns.size() - 1; i >= 0; i--) if (spawns.get(i).group == t.targetGroup) spawns.remove(i);
                for (int i = timers.size() - 1; i >= 0; i--) if (timers.get(i).group == t.targetGroup) timers.remove(i);
                break;
            }
            case Level.T_PULSE:
                if (t.pulseToGroup) {
                    if (list != null) for (int i : list) {
                        st[i].hasPulse = true; st[i].pulseColor = t.color;
                        st[i].pulseT = t.pulseIn + t.pulseHold + t.pulseOut;
                    }
                } else setColor(t.colorChannel, t.color);
                break;
            case Level.T_COLOR: fadeColor(t.colorChannel, t.color, t.fadeTime); break;
            case Level.T_BG: fadeColor(CH_BG, t.color, t.fadeTime); break;
            case Level.T_GROUND: fadeColor(CH_GROUND, t.color, t.fadeTime); break;
            case Level.T_SHAKE:
                shakeStrength = t.shakeStrength; shakeInterval = Math.max(0.01, t.shakeInterval);
                shakeTimer = Math.max(0.05, t.duration); shakeT = 0;
                break;
            case Level.T_FOLLOW: case Level.T_FOLLOWY: {
                if (list == null) break;
                Act a = act(4, list, t.targetGroup, t.duration <= 0 ? 999 : t.duration, 0);
                a.a = t.followX; a.b = t.followY;
                a.playerY = t.type == Level.T_FOLLOWY;
                a.maxSpeed = t.followMax;
                a.lastPx = p1.x; a.lastPy = p1.y;
                acts.add(a);
                break;
            }
            case Level.T_TOUCH: if (t.targetGroup != 0) fireGroup(t.targetGroup); break;
            case Level.T_COUNT: counts.add(t); break;
            case Level.T_ICOUNT: {
                int v = getItem(t.itemId);
                boolean okc = t.countMulti ? v >= t.targetCount : v == t.targetCount;
                if (okc) fireGroup(t.targetGroup);
                else if (t.groupB != 0) fireGroup(t.groupB);
                break;
            }
            case Level.T_PICKUP: addItem(t.itemId, t.count); break;
            case Level.T_COLLISION: case Level.T_ICOLLISION: {
                boolean h = false;
                List<Integer> la = groups.get(t.blockA);
                if (la != null) { RectF pb = p1.box(); for (int i : la) if (hit(objHit(i), pb)) { h = true; break; } }
                if (h) { if (t.targetGroup != 0) fireGroup(t.targetGroup); }
                else if (t.groupB != 0) fireGroup(t.groupB);
                break;
            }
            case Level.T_RANDOM: {
                int g = rnd.nextDouble() * 100 < t.chance ? t.targetGroup : t.groupB;
                if (g != 0) fireGroup(g);
                break;
            }
            case Level.T_TIMER: {
                TimerAct a = new TimerAct(); a.t = Math.max(0, t.timerSeconds); a.group = t.targetGroup;
                timers.add(a);
                break;
            }
            case Level.T_CAMERA: camStatic = t.camStatic; camOffX = t.camOffX; camOffY = t.camOffY; break;
            case Level.T_ZOOM: camZoom = Math.max(0.3, Math.min(3, t.camZoom)); break;
            case Level.T_SONG:
                if (listener != null) listener.song(t.songId, t.songVolume, t.songStart, t.songLoop);
                break;
            case Level.T_GRAVITY:
                p1.gravityMul = t.gravityMul <= 0 ? 1 : t.gravityMul;
                if (t.gravityDir == 0 || p1.gravityDir != t.gravityDir) p1.flip();
                break;
            case Level.T_TELEPORT: p1.y = t.teleportY; p1.vy = 0; break;
            case Level.T_ONDEATH: onDeathTrigs.add(t); break;
            case Level.T_TRAIL: p1.trailOn = t.activate; break;
            case Level.T_HIDE: p1.hidden = true; p2.hidden = true; break;
            case Level.T_SHOW: p1.hidden = false; p2.hidden = false; break;
            case Level.T_OPTIONS:
                if (t.optChangeMode) p1.mode = t.optMode;
                p1.mini = t.optMini; p1.mirror = t.optMirror; dual = t.optDual;
                break;
            case Level.T_CHECKPOINT: addCheckpoint(p1); break;
            case Level.T_END: finish(); break;
            case Level.T_REVERSE: reverse = t.activate; break;
            case Level.T_SPEED: p1.speed = t.speed; if (dual) p2.speed = t.speed; break;

            // ======================= 2.2 =======================
            case Level.T_GRADIENT:
                fx.gradientOn = t.activate;
                fx.gradTop = t.color;
                fx.gradBottom = t.color2;
                break;
            case Level.T_PARTICLES:
                fx.burst(src != null ? src.x + 15 : p1.x, src != null ? src.y + 15 : p1.y,
                        Math.max(1, Math.min(120, t.count)), t.color, 120 + t.fxStrength * 400);
                break;
            case Level.T_SHOCK:
                fx.shock.set(Math.max(0.15, t.duration), t.fxStrength);
                fx.shockX = src != null ? src.x + 15 : p1.x;
                fx.shockY = src != null ? src.y + 15 : p1.y;
                break;
            case Level.T_CHROMATIC: fx.chromatic.set(t.duration, t.fxStrength); break;
            case Level.T_PIXELATE: fx.pixelate.set(t.duration, t.fxStrength); break;
            case Level.T_GLITCH: fx.glitch.set(t.duration, t.fxStrength); break;
            case Level.T_INVERT: fx.invert.set(t.duration, t.fxStrength); break;
            case Level.T_GRAY: fx.gray.set(t.duration, t.fxStrength); break;
            case Level.T_BLUR: fx.blur.set(t.duration, t.fxStrength); break;
            case Level.T_VIGNETTE: fx.vignette.set(t.duration, t.fxStrength); break;
            case Level.T_FLASH:
                fx.flash.set(Math.max(0.1, t.duration), t.fxStrength);
                fx.flashColor = t.color;
                break;
            case Level.T_BUMP:
                shakeStrength = t.shakeStrength; shakeInterval = Math.max(0.01, t.shakeInterval);
                shakeTimer = Math.max(0.12, t.duration); shakeT = 0;
                fx.shock.set(Math.max(0.2, t.duration), 0.5);
                fx.shockX = p1.x; fx.shockY = p1.y;
                break;
            case Level.T_TIMEWARP:
                fx.timeScale = Math.max(0.1, Math.min(4, t.timeScale <= 0 ? 1 : t.timeScale));
                break;

            case Level.T_AREAMOVE: case Level.T_AREAROT: case Level.T_AREASCALE:
            case Level.T_AREAFADE: case Level.T_AREATINT:
                area(t, src);
                break;

            case Level.T_SEQUENCE: {
                int[] gs = {t.targetGroup, t.groupB, t.centerGroup, 0};
                double delay = 0;
                for (int g : gs) {
                    if (g == 0) continue;
                    Spawn sp = new Spawn(); sp.selfIdx = -1; sp.group = g; sp.delay = delay;
                    spawns.add(sp);
                    delay += Math.max(0.02, t.stepDelay);
                }
                break;
            }

            case Level.T_ADVFOLLOW: {
                if (list == null) break;
                Act a = act(4, list, t.targetGroup, t.duration <= 0 ? 9999 : t.duration, t.easing);
                a.a = t.followX; a.b = t.followY;
                a.lastPx = p1.x; a.lastPy = p1.y;
                acts.add(a);
                break;
            }

            case Level.T_CAMEDGE: case Level.T_CAMGUIDE:
                camOffX = t.camOffX; camOffY = t.camOffY;
                break;
            case Level.T_CAMSTATIC: camStatic = t.camStatic; break;
            case Level.T_CAMROTATE: case Level.T_ROTGAMEPLAY: gameplayAngle = t.degrees; break;
            case Level.T_GPOFFSET: gameplayOffX = t.moveX; gameplayOffY = t.moveY; break;

            case Level.T_EVENT:
                events.add(t.eventId);
                if (t.targetGroup != 0) fireGroup(t.targetGroup);
                break;
            case Level.T_RESET:
                events.remove(t.eventId);
                break;

            case Level.T_ITEMEDIT: addItem(t.itemId, t.count); break;
            case Level.T_PERSIST: persistentItems.add(t.itemId); break;
            case Level.T_ITEMCMP: {
                Integer have = items.get(t.itemId);
                int v = have == null ? 0 : have;
                boolean okCmp;
                switch (t.compare) {
                    case 1: okCmp = v > t.targetCount; break;
                    case 2: okCmp = v < t.targetCount; break;
                    case 3: okCmp = v >= t.targetCount; break;
                    case 4: okCmp = v <= t.targetCount; break;
                    case 5: okCmp = v != t.targetCount; break;
                    default: okCmp = v == t.targetCount;
                }
                int g = okCmp ? t.trueGroup : t.falseGroup;
                if (g != 0) fireGroup(g);
                break;
            }

            case Level.T_TOGGLEBLOCK:
                if (list != null) for (int i : list) st[i].enabled = !st[i].enabled;
                break;
            case Level.T_LINKVIS:
                if (list != null) for (int i : list) st[i].alpha = t.activate ? 1 : 0;
                break;
            case Level.T_ENTER:
                if (list != null) for (int i : list) st[i].alpha = Math.max(0, Math.min(1, t.opacity));
                break;

            case Level.T_SFX: sfx(t.sfxName == null || t.sfxName.isEmpty() ? "click" : t.sfxName); break;
            case Level.T_EDITSONG:
                if (listener != null) listener.song(t.songId != 0 ? t.songId : level.settings.songId,
                        t.songVolume, t.songStart, t.songLoop);
                break;
        }
    }

    /** Площадной триггер 2.2: действует на всё вокруг, в центре сильнее. */
    void area(Level.Trig t, Level.Obj src) {
        if (src == null) return;
        double cx = src.x + 15, cy = src.y + 15, rad = Math.max(30, t.areaRadius);
        List<Integer> targets = new ArrayList<>();
        List<Double> power = new ArrayList<>();
        for (int i = 0; i < level.objects.size(); i++) {
            Level.Obj o = level.objects.get(i);
            if (o.trig != null) continue;
            double dx = o.x + 15 - cx, dy = o.y + 15 - cy;
            double d = Math.sqrt(dx * dx + dy * dy);
            if (d > rad) continue;
            targets.add(i);
            power.add(1 - d / rad);
        }
        if (targets.isEmpty()) return;

        Act a = act(5, targets, 0, Math.max(0.05, t.duration), t.easing);
        a.areaType = t.type;
        a.a = t.moveX; a.b = t.moveY;
        a.deg = t.degrees; a.sx = t.scaleX; a.sy = t.scaleY; a.alpha = t.opacity;
        a.tint = t.color;
        a.fromA = new double[targets.size()];
        a.fromB = new double[targets.size()];
        a.power = new double[targets.size()];
        for (int k = 0; k < targets.size(); k++) {
            a.fromA[k] = st[targets.get(k)].dx;
            a.fromB[k] = st[targets.get(k)].dy;
            a.power[k] = power.get(k);
        }
        acts.add(a);
    }

    Act act(int kind, List<Integer> targets, int group, double dur, int easing) {
        Act a = new Act();
        a.kind = kind; a.targets = targets; a.group = group;
        a.dur = Math.max(0, dur); a.easing = easing;
        return a;
    }

    void updateActs(double dt) {
        for (int i = acts.size() - 1; i >= 0; i--) {
            Act a = acts.get(i);
            a.t += dt;
            double k = ease(Math.max(0, Math.min(1, a.dur <= 0 ? 1 : a.t / a.dur)), a.easing);
            if (a.kind == 5) {
                for (int j = 0; j < a.targets.size(); j++) {
                    ObjState s2 = st[a.targets.get(j)];
                    double pw = a.power[j];
                    if (a.areaType == Level.T_AREAMOVE) {
                        s2.dx = a.fromA[j] + a.a * k * pw;
                        s2.dy = a.fromB[j] + a.b * k * pw;
                    } else if (a.areaType == Level.T_AREAROT) {
                        s2.rot = a.deg * k * pw;
                    } else if (a.areaType == Level.T_AREASCALE) {
                        s2.sx = 1 + (a.sx - 1) * k * pw;
                        s2.sy = 1 + (a.sy - 1) * k * pw;
                    } else if (a.areaType == Level.T_AREAFADE) {
                        s2.alpha = 1 + (a.alpha - 1) * k * pw;
                    } else if (a.areaType == Level.T_AREATINT) {
                        s2.hasPulse = true; s2.pulseColor = a.tint;
                        s2.pulseT = Math.max(s2.pulseT, a.dur - a.t + 0.1);
                    }
                }
                if (a.t >= a.dur) acts.remove(i);
                continue;
            }
            switch (a.kind) {
                case 0:
                    for (int j = 0; j < a.targets.size(); j++) {
                        ObjState s = st[a.targets.get(j)];
                        s.dx = a.fromA[j] + a.a * k;
                        s.dy = a.fromB[j] + a.b * k;
                    }
                    break;
                case 1:
                    for (int j = 0; j < a.targets.size(); j++) st[a.targets.get(j)].rot = a.fromA[j] + a.a * k;
                    break;
                case 2:
                    for (int j = 0; j < a.targets.size(); j++) {
                        ObjState s = st[a.targets.get(j)];
                        s.sx = a.fromA[j] + (a.a - a.fromA[j]) * k;
                        s.sy = a.fromB[j] + (a.b - a.fromB[j]) * k;
                    }
                    break;
                case 3:
                    for (int j = 0; j < a.targets.size(); j++) {
                        ObjState s = st[a.targets.get(j)];
                        s.alpha = a.fromA[j] + (a.a - a.fromA[j]) * k;
                    }
                    break;
                case 4: {
                    double ddx = (p1.x - a.lastPx) * a.a, ddy = (p1.y - a.lastPy) * a.b;
                    if (a.maxSpeed > 0) {
                        double lim = a.maxSpeed * dt;
                        ddx = clamp(ddx, -lim, lim); ddy = clamp(ddy, -lim, lim);
                    }
                    for (int idx : a.targets) {
                        ObjState s = st[idx];
                        if (a.playerY) s.dy += ddy; else { s.dx += ddx; s.dy += ddy; }
                    }
                    a.lastPx = p1.x; a.lastPy = p1.y;
                    break;
                }
            }
            if (a.t >= a.dur) acts.remove(i);
        }

        for (int i = spawns.size() - 1; i >= 0; i--) {
            Spawn s = spawns.get(i);
            s.delay -= dt;
            if (s.delay <= 0) {
                spawns.remove(i);
                if (s.selfIdx >= 0) {
                    Level.Obj o = level.objects.get(s.selfIdx);
                    if (o.trig != null) {
                        double d = o.trig.spawnDelay; o.trig.spawnDelay = 0;
                        execute(o.trig, o);
                        o.trig.spawnDelay = d;
                    }
                } else fireGroup(s.group);
            }
        }

        for (int i = timers.size() - 1; i >= 0; i--) {
            TimerAct a = timers.get(i);
            a.t -= dt;
            if (a.t <= 0) { timers.remove(i); if (a.group != 0) fireGroup(a.group); }
        }
    }

    void updateColors(double dt) {
        for (Map.Entry<Integer, double[]> e : new ArrayList<>(colorFade.entrySet())) {
            double[] f = e.getValue();
            if (f[1] <= 0) continue;
            f[0] += dt;
            double k = Math.max(0, Math.min(1, f[0] / f[1]));
            int[] c = colors.get(e.getKey());
            if (c != null) c[0] = Gd.blend(c[1], c[2], k);
            if (k >= 1) f[1] = 0;
        }
    }

    void fadeColor(int id, int target, double dur) {
        int[] c = colors.get(id);
        if (c == null) { setColor(id, target); return; }
        c[1] = c[0]; c[2] = target;
        colorFade.put(id, new double[]{0, Math.max(0, dur)});
        if (dur <= 0) c[0] = target;
    }

    static double ease(double t, int type) {
        switch (type) {
            case 1: return t < 0.5 ? 2 * t * t : 1 - Math.pow(-2 * t + 2, 2) / 2;
            case 2: return t * t;
            case 3: return 1 - (1 - t) * (1 - t);
            case 11: return -(Math.cos(Math.PI * t) - 1) / 2;
            case 9: return 1 - Math.pow(1 - t, 3);
            default: return t;
        }
    }

    void updateCamera(double dt) {
        if (!camStatic) {
            double targetX = p1.x + 180;
            camX += (targetX - camX) * Math.min(1, dt * 12);
            double half = 480 / 2.0 / camZoom;
            double desired = camY;
            if (p1.y > camY + half * 0.45) desired = p1.y - half * 0.45;
            else if (p1.y < camY - half * 0.45) desired = p1.y + half * 0.45;
            boolean fly = p1.mode == Gd.SHIP || p1.mode == Gd.UFO || p1.mode == Gd.WAVE;
            camY += (desired - camY) * Math.min(1, dt * (fly ? 7 : 4.5));
            double minY = groundY + half - 30;
            if (camY < minY) camY = minY;
        }
        if (shakeTimer > 0) {
            shakeTimer -= dt; shakeT -= dt;
            if (shakeT <= 0) {
                shakeT = shakeInterval;
                shakeX = (rnd.nextDouble() * 2 - 1) * shakeStrength;
                shakeY = (rnd.nextDouble() * 2 - 1) * shakeStrength;
            }
        } else { shakeX = shakeY = 0; }
    }

    // ------------------------------------------------------------ прочее

    public void addItem(int id, int delta) {
        int v = getItem(id) + delta;
        items.put(id, v);
        for (Level.Trig t : new ArrayList<>(counts)) {
            if (t.itemId != id) continue;
            if (v == t.targetCount || (t.countMulti && v >= t.targetCount)) {
                fireGroup(t.targetGroup);
                if (!t.countMulti) counts.remove(t);
            }
        }
    }

    public int getItem(int id) { Integer v = items.get(id); return v == null ? 0 : v; }

    public void addCheckpoint(Player p) {
        if (!checkpoints.isEmpty() && Math.abs(checkpoints.get(checkpoints.size() - 1)[0] - p.x) < 40) return;
        checkpoints.add(new double[]{p.x, p.y, p.vy, p.gravityDir, p.mode, p.speed, p.mini ? 1 : 0, time});
    }

    public void removeCheckpoint() {
        if (!checkpoints.isEmpty()) checkpoints.remove(checkpoints.size() - 1);
    }

    void die() {
        if (dead || finished || noclip) return;
        dead = true; deathTimer = 0;
        p1.lavaTime = 0; p1.inLava = false; p1.inLiquid = false;
        p2.lavaTime = 0; p2.inLava = false; p2.inLiquid = false;
        sfx("death");
        for (Level.Trig t : new ArrayList<>(onDeathTrigs)) if (t.targetGroup != 0) fireGroup(t.targetGroup);
        if (listener != null) listener.onDeath();
    }

    void finish() {
        if (finished) return;
        finished = true; progress = 1;
        sfx("win");
        if (listener != null) listener.onFinish();
    }

    void sfx(String n) { if (listener != null) listener.sfx(n); }

    static double clamp(double v, double a, double b) { return v < a ? a : (v > b ? b : v); }
}
