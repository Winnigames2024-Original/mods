package com.winnigames2024.geodash;

import android.content.Context;
import android.graphics.*;
import android.view.MotionEvent;
import android.view.SurfaceHolder;
import android.view.SurfaceView;

import java.util.List;

/** Игровой экран: цикл, отрисовка, управление касанием. */
public class GameView extends SurfaceView implements SurfaceHolder.Callback, Runnable, Engine.Listener {

    public interface Exit { void onExit(GameView v); }

    final Engine eng = new Engine();
    final Level level;
    Thread thread;
    volatile boolean running;
    boolean hold1, pressed1, hold2, pressed2, paused;
    public boolean practice;
    int attempts = 1;
    double best, deathDelay;
    String toast = "";
    double toastT;
    public Exit onExit;
    public boolean testMode;

    public GameView(Context c, Level lv, boolean practiceMode) {
        super(c);
        level = lv;
        practice = practiceMode;
        getHolder().addCallback(this);
        setFocusable(true);
        eng.listener = this;
        eng.init(lv, practiceMode);
    }

    // ------------------------------------------------------------ жизненный цикл

    @Override public void surfaceCreated(SurfaceHolder h) {
        running = true;
        thread = new Thread(this, "geodash-loop");
        thread.start();
        if (level.settings.songId != 0) Music.play(level.settings.songId, level.settings.songOffset, 1f, true);
    }

    @Override public void surfaceChanged(SurfaceHolder h, int f, int w, int hh) { }

    @Override public void surfaceDestroyed(SurfaceHolder h) { stop(); }

    public void stop() {
        running = false;
        try { if (thread != null) thread.join(600); } catch (Exception ignored) { }
        thread = null;
        Music.stop();
        saveRun();
    }

    void saveRun() {
        if (testMode) return;
        double pct = Math.max(best, eng.progress) * 100;
        Store.recordRun(level, pct, attempts, eng.coinsGot, eng.finished);
    }

    // ------------------------------------------------------------ цикл

    @Override public void run() {
        final double FIXED = 1.0 / 240.0;
        long prev = System.nanoTime();
        double acc = 0;

        while (running) {
            long now = System.nanoTime();
            double dt = (now - prev) / 1e9;
            prev = now;
            if (dt > 0.25) dt = 0.25;

            if (!paused) {
                acc += dt;
                int guard = 0;
                while (acc >= FIXED && guard++ < 400) {
                    stepOnce(FIXED);
                    acc -= FIXED;
                }
                if (toastT > 0) toastT -= dt;
            }

            Canvas c = null;
            try {
                c = getHolder().lockCanvas();
                if (c != null) render(c);
            } catch (Exception ignored) {
            } finally {
                if (c != null) try { getHolder().unlockCanvasAndPost(c); } catch (Exception ignored) { }
            }

            try { Thread.sleep(2); } catch (InterruptedException ignored) { }
        }
    }

    void stepOnce(double dt) {
        if (eng.dead) {
            deathDelay += dt;
            if (deathDelay > 0.55) respawn();
            return;
        }
        if (eng.finished) return;

        eng.update(dt, hold1, pressed1, hold2, pressed2);
        pressed1 = false;
        pressed2 = false;
    }

    void respawn() {
        attempts++;
        eng.attempts = attempts;
        eng.restart();
        deathDelay = 0;
        if (practice && !eng.checkpoints.isEmpty())
            Music.seek(level.settings.songOffset + eng.checkpoints.get(eng.checkpoints.size() - 1)[7]);
        else
            Music.seek(level.settings.songOffset);
    }

    // ------------------------------------------------------------ ввод

    @Override public boolean onTouchEvent(MotionEvent e) {
        int act = e.getActionMasked();
        float x = e.getX(e.getActionIndex());
        boolean right = x > getWidth() / 2f;

        switch (act) {
            case MotionEvent.ACTION_DOWN:
            case MotionEvent.ACTION_POINTER_DOWN:
                if (eng.finished) { exit(); return true; }
                if (level.settings.twoPlayer && right) { if (!hold2) pressed2 = true; hold2 = true; }
                else { if (!hold1) pressed1 = true; hold1 = true; }
                return true;

            case MotionEvent.ACTION_UP:
            case MotionEvent.ACTION_POINTER_UP:
            case MotionEvent.ACTION_CANCEL:
                if (level.settings.twoPlayer && right) hold2 = false; else hold1 = false;
                if (e.getPointerCount() <= 1) { hold1 = false; hold2 = false; }
                return true;
        }
        return true;
    }

    public void exit() { if (onExit != null) onExit.onExit(this); }

    public void togglePause() {
        paused = !paused;
        if (paused) Music.pause(); else Music.resume();
    }

    public void addCheckpoint() { if (practice) { eng.addCheckpoint(eng.p1); say("Чекпоинт"); } }
    public void removeCheckpoint() { if (practice) { eng.removeCheckpoint(); say("Чекпоинт убран"); } }

    void say(String s) { toast = s; toastT = 2.0; }

    // ------------------------------------------------------------ Listener

    @Override public void sfx(String name) { Music.sfx(name); }
    @Override public void song(int id, double volume, double start, boolean loop) {
        Music.play(id, start, (float) volume, loop);
    }
    @Override public void onDeath() { deathDelay = 0; best = Math.max(best, eng.progress); }
    @Override public void onFinish() { best = 1; say("Уровень пройден!"); saveRun(); }

    // ------------------------------------------------------------ отрисовка

    float scale() { return (float) (getHeight() / (480.0 / Math.max(0.05, eng.camZoom))); }

    float sx(double wx) { return (float) ((wx - (eng.camX + eng.camOffX + eng.shakeX)) * scale() + getWidth() / 2.0); }
    float sy(double wy) { return (float) (getHeight() / 2.0 - (wy - (eng.camY + eng.camOffY + eng.shakeY)) * scale()); }

    RectF worldRect(double x, double y, double w, double h) {
        float left = sx(x), top = sy(y + h);
        float s = scale();
        return new RectF(left, top, left + (float) (w * s), top + (float) (h * s));
    }

    void render(Canvas c) {
        int W = getWidth(), H = getHeight();
        int bg = eng.color(Engine.CH_BG);

        Paint p = new Paint();
        p.setShader(new LinearGradient(0, 0, 0, H, Gd.blend(bg, Color.BLACK, 0.25), Gd.blend(bg, Color.WHITE, 0.06), Shader.TileMode.CLAMP));
        c.drawRect(0, 0, W, H, p);

        drawGround(c, W, H);

        float visLeft = (float) ((0 - W / 2.0) / scale() + eng.camX) - 120;
        float visRight = (float) ((W - W / 2.0) / scale() + eng.camX) + 120;

        for (int i = 0; i < eng.level.objects.size(); i++) {
            Engine.ObjState s = eng.st[i];
            if (!s.enabled) continue;
            Level.Obj o = eng.level.objects.get(i);
            Catalog.Def d = Catalog.get(o.typeId);
            if (d.invisible) continue;
            double x = o.x + s.dx;
            if (x + d.w * o.scaleX < visLeft || x > visRight) continue;
            drawObject(c, i, o, d, s);
        }

        if (!eng.p1.hidden) drawPlayer(c, eng.p1, eng.color(Engine.CH_P1));
        if (eng.dual && !eng.p2.hidden) drawPlayer(c, eng.p2, eng.color(Engine.CH_P2));

        drawHud(c, W, H);
        if (eng.dead) c.drawColor(Color.argb(70, 160, 20, 30));
        if (eng.finished) drawOverlay(c, W, H, "УРОВЕНЬ ПРОЙДЕН", new String[]{
                level.settings.name,
                "попыток: " + attempts + "   монет: " + eng.coinsGot + "/3",
                "время: " + String.format("%.1f", eng.time) + " c",
                "коснись экрана, чтобы выйти"});
        if (paused) drawOverlay(c, W, H, "ПАУЗА", new String[]{
                "прогресс: " + (int) (eng.progress * 100) + "%",
                "попытка " + attempts,
                practice ? "режим тренировки" : "обычный режим"});
    }

    void drawGround(Canvas c, int W, int H) {
        int gc = eng.color(Engine.CH_GROUND), line = eng.color(Engine.CH_LINE);
        float top = sy(eng.groundY);
        if (top < H) {
            Paint p = new Paint();
            p.setShader(new LinearGradient(0, top, 0, H, gc, Gd.blend(gc, Color.BLACK, 0.45), Shader.TileMode.CLAMP));
            c.drawRect(0, top, W, H, p);
            c.drawLine(0, top, W, top, Gd.stroke(line, 3f));

            float step = 60 * scale();
            if (step > 6) {
                Paint pp = Gd.stroke(Color.argb(40, 255, 255, 255), 2f);
                float off = (float) (-(eng.camX * scale()) % step);
                for (float x = off; x < W + step; x += step) c.drawLine(x, top + 2, x, H, pp);
            }
        }
        float roof = sy(eng.roofY);
        if (roof > 0) {
            c.drawRect(0, -2, W, roof, Gd.fill(Color.argb(200,
                    Color.red(Gd.blend(gc, Color.BLACK, 0.3)),
                    Color.green(Gd.blend(gc, Color.BLACK, 0.3)),
                    Color.blue(Gd.blend(gc, Color.BLACK, 0.3)))));
            c.drawLine(0, roof, W, roof, Gd.stroke(line, 3f));
        }
    }

    void drawObject(Canvas c, int i, Level.Obj o, Catalog.Def d, Engine.ObjState s) {
        RectF r = worldRect(o.x + s.dx, o.y + s.dy, d.w * o.scaleX * s.sx, d.h * o.scaleY * s.sy);
        int col = s.hasPulse ? s.pulseColor : eng.color(o.baseColor);
        int alpha = (int) Math.max(0, Math.min(255, s.alpha * o.opacity * 255));
        col = Color.argb(alpha, Color.red(col), Color.green(col), Color.blue(col));
        int detail = Gd.blend(col, Color.WHITE, 0.75);

        c.save();
        double rot = o.rotation + s.rot;
        if (Math.abs(rot) > 0.01) c.rotate((float) -rot, r.centerX(), r.centerY());

        switch (d.style) {
            case Catalog.ST_BLOCK:
            case Catalog.ST_SLAB:
                c.drawRect(r, Gd.fill(col));
                c.drawRect(r, Gd.stroke(detail, Math.max(1.5f, r.height() * 0.09f)));
                break;
            case Catalog.ST_BRICK:
                c.drawRect(r, Gd.fill(col));
                c.drawRect(r, Gd.stroke(Gd.blend(col, Color.WHITE, 0.45), 1.6f));
                c.drawLine(r.left, r.centerY(), r.right, r.centerY(), Gd.stroke(Gd.blend(col, Color.WHITE, 0.45), 1.4f));
                break;
            case Catalog.ST_GLASS:
                c.drawRect(r, Gd.fill(Color.argb(alpha / 3, Color.red(col), Color.green(col), Color.blue(col))));
                c.drawRect(r, Gd.stroke(Gd.blend(col, Color.WHITE, 0.8), 2f));
                break;
            case Catalog.ST_PLATFORM:
                c.drawRect(r, Gd.fill(col));
                c.drawLine(r.left, r.top, r.right, r.top, Gd.stroke(detail, 2f));
                break;
            case Catalog.ST_SLOPE_UP:
            case Catalog.ST_SLOPE_DOWN: {
                Path path = new Path();
                if (d.style == Catalog.ST_SLOPE_UP) {
                    path.moveTo(r.left, r.bottom); path.lineTo(r.right, r.bottom); path.lineTo(r.right, r.top);
                } else {
                    path.moveTo(r.left, r.bottom); path.lineTo(r.right, r.bottom); path.lineTo(r.left, r.top);
                }
                path.close();
                c.drawPath(path, Gd.fill(col));
                c.drawPath(path, Gd.stroke(detail, 2f));
                break;
            }
            case Catalog.ST_SPIKE:
            case Catalog.ST_SPIKE_SMALL:
            case Catalog.ST_SPIKE_HALF: {
                boolean ceil = o.typeId == 22;
                Path path = new Path();
                if (ceil) {
                    path.moveTo(r.left, r.top); path.lineTo(r.right, r.top); path.lineTo(r.centerX(), r.bottom);
                } else {
                    path.moveTo(r.left, r.bottom); path.lineTo(r.right, r.bottom); path.lineTo(r.centerX(), r.top);
                }
                path.close();
                c.drawPath(path, Gd.fill(col));
                c.drawPath(path, Gd.stroke(detail, 1.8f));
                break;
            }
            case Catalog.ST_SPIKE_ROW: {
                float w3 = r.width() / 3f;
                for (int k = 0; k < 3; k++) {
                    Path path = new Path();
                    path.moveTo(r.left + k * w3, r.bottom);
                    path.lineTo(r.left + (k + 1) * w3, r.bottom);
                    path.lineTo(r.left + (k + 0.5f) * w3, r.top);
                    path.close();
                    c.drawPath(path, Gd.fill(col));
                }
                break;
            }
            case Catalog.ST_SAW:
            case Catalog.ST_SAW_BIG: {
                float cx = r.centerX(), cy = r.centerY(), rad = Math.min(r.width(), r.height()) / 2;
                int teeth = d.style == Catalog.ST_SAW_BIG ? 12 : 8;
                Path path = new Path();
                double rotT = (System.currentTimeMillis() % 4000) / 4000.0 * Math.PI * 2;
                for (int k = 0; k < teeth * 2; k++) {
                    double ang = rotT + k * Math.PI / teeth;
                    double rr = (k % 2 == 0) ? rad : rad * 0.62;
                    float x = (float) (cx + Math.cos(ang) * rr), y = (float) (cy + Math.sin(ang) * rr);
                    if (k == 0) path.moveTo(x, y); else path.lineTo(x, y);
                }
                path.close();
                c.drawPath(path, Gd.fill(Gd.blend(col, Color.rgb(180, 180, 200), 0.5)));
                c.drawPath(path, Gd.stroke(detail, 1.6f));
                c.drawCircle(cx, cy, rad * 0.25f, Gd.fill(Color.rgb(40, 40, 50)));
                break;
            }
            case Catalog.ST_HAZARD_BLOCK:
                c.drawRect(r, Gd.fill(Color.rgb(200, 50, 60)));
                c.drawRect(r, Gd.stroke(Color.rgb(255, 160, 160), 2f));
                break;
            case Catalog.ST_ORB: {
                int oc = orbColor(o.orb);
                RectF rr = new RectF(r);
                float k = (float) ((s.pulseScale - 1) * r.width() * 0.5);
                rr.inset(-k, -k);
                RectF glow = new RectF(rr); glow.inset(-r.width() * 0.3f, -r.width() * 0.3f);
                c.drawOval(glow, Gd.fill(Color.argb(60, Color.red(oc), Color.green(oc), Color.blue(oc))));
                c.drawOval(rr, Gd.stroke(oc, Math.max(2f, r.width() * 0.14f)));
                RectF in = new RectF(rr); in.inset(r.width() * 0.3f, r.width() * 0.3f);
                c.drawOval(in, Gd.fill(Gd.blend(oc, Color.WHITE, 0.5)));
                break;
            }
            case Catalog.ST_PAD: {
                int pc = padColor(o.pad);
                c.drawRect(r, Gd.fill(pc));
                c.drawRect(new RectF(r.left, r.top - r.height(), r.right, r.top),
                        Gd.fill(Color.argb(70, Color.red(pc), Color.green(pc), Color.blue(pc))));
                break;
            }
            case Catalog.ST_PORTAL: {
                int[] look = portalLook(o.portal);
                Paint pp = new Paint(Paint.ANTI_ALIAS_FLAG);
                pp.setShader(new LinearGradient(r.left, r.top, r.left, r.bottom,
                        look[0], Gd.blend(look[0], Color.WHITE, 0.6), Shader.TileMode.CLAMP));
                c.drawOval(r, pp);
                c.drawOval(r, Gd.stroke(Gd.blend(look[0], Color.WHITE, 0.5), Math.max(2f, r.width() * 0.12f)));
                String lbl = portalLabel(o.portal);
                if (r.height() > 26)
                    Gd.outlined(c, lbl, r.centerX(), r.centerY() + r.width() * 0.12f,
                            Math.max(9f, r.width() * 0.42f), Color.WHITE, Paint.Align.CENTER);
                break;
            }
            case Catalog.ST_DECO: c.drawRect(r, Gd.fill(col)); break;
            case Catalog.ST_CIRCLE: c.drawOval(r, Gd.fill(col)); break;
            case Catalog.ST_TRI: {
                Path path = new Path();
                path.moveTo(r.left, r.bottom); path.lineTo(r.right, r.bottom); path.lineTo(r.centerX(), r.top);
                path.close();
                c.drawPath(path, Gd.fill(col));
                break;
            }
            case Catalog.ST_GLOW:
                c.drawOval(r, Gd.fill(Color.argb(Math.min(90, alpha), Color.red(col), Color.green(col), Color.blue(col))));
                break;
            case Catalog.ST_TEXT:
                Gd.outlined(c, o.text, r.left, r.bottom, Math.max(10f, r.height() * 0.8f), col, Paint.Align.LEFT);
                break;
            case Catalog.ST_COIN:
                Gd.coin(c, r, !s.collected);
                break;
            case Catalog.ST_PICKUP:
                if (!s.collected) {
                    Path path = new Path();
                    path.moveTo(r.centerX(), r.top); path.lineTo(r.right, r.centerY());
                    path.lineTo(r.centerX(), r.bottom); path.lineTo(r.left, r.centerY());
                    path.close();
                    c.drawPath(path, Gd.fill(Color.rgb(120, 255, 180)));
                }
                break;
            case Catalog.ST_FINISH: {
                float cell = r.width() / 2;
                for (float y = r.top; y < r.bottom; y += cell)
                    for (int k = 0; k < 2; k++) {
                        boolean white = (((int) ((y - r.top) / cell) + k) % 2) == 0;
                        c.drawRect(r.left + k * cell, y, r.left + (k + 1) * cell, y + cell,
                                Gd.fill(white ? Color.rgb(250, 250, 250) : Color.rgb(30, 30, 40)));
                    }
                break;
            }
            case Catalog.ST_CHECKPOINT:
                c.drawRect(r, Gd.stroke(Color.rgb(120, 255, 160), 2f));
                break;
        }
        c.restore();
    }

    static int orbColor(int k) {
        switch (k) {
            case 0: return Color.rgb(255, 230, 90);
            case 1: return Color.rgb(255, 120, 200);
            case 2: return Color.rgb(90, 160, 255);
            case 3: return Color.rgb(100, 255, 130);
            case 4: return Color.rgb(255, 90, 90);
            case 5: return Color.rgb(60, 60, 70);
            case 6: return Color.rgb(120, 255, 230);
            case 7: return Color.rgb(190, 120, 255);
            case 8: return Color.rgb(170, 70, 255);
            default: return Color.rgb(230, 230, 230);
        }
    }

    static int padColor(int k) {
        switch (k) {
            case 0: return Color.rgb(255, 230, 90);
            case 1: return Color.rgb(255, 120, 200);
            case 2: return Color.rgb(90, 160, 255);
            case 3: return Color.rgb(255, 90, 90);
            default: return Color.rgb(170, 70, 255);
        }
    }

    static int[] portalLook(int k) {
        switch (k) {
            case Level.P_CUBE: return new int[]{Color.rgb(90, 230, 120)};
            case Level.P_SHIP: return new int[]{Color.rgb(255, 140, 220)};
            case Level.P_BALL: return new int[]{Color.rgb(255, 160, 60)};
            case Level.P_UFO: return new int[]{Color.rgb(255, 230, 90)};
            case Level.P_WAVE: return new int[]{Color.rgb(90, 220, 255)};
            case Level.P_ROBOT: return new int[]{Color.rgb(170, 170, 190)};
            case Level.P_SPIDER: return new int[]{Color.rgb(180, 80, 255)};
            case Level.P_WORM: return new int[]{Color.rgb(120, 255, 170)};
            case Level.P_GUP: return new int[]{Color.rgb(90, 150, 255)};
            case Level.P_GDOWN: return new int[]{Color.rgb(255, 240, 120)};
            case Level.P_GTOG: return new int[]{Color.rgb(200, 120, 255)};
            case Level.P_MINI: return new int[]{Color.rgb(120, 255, 120)};
            case Level.P_BIG: return new int[]{Color.rgb(255, 120, 120)};
            case Level.P_DUAL: return new int[]{Color.rgb(255, 110, 180)};
            case Level.P_SINGLE: return new int[]{Color.rgb(150, 200, 255)};
            case Level.P_S05: return new int[]{Color.rgb(120, 200, 255)};
            case Level.P_S1: return new int[]{Color.rgb(120, 255, 160)};
            case Level.P_S2: return new int[]{Color.rgb(255, 230, 120)};
            case Level.P_S3: return new int[]{Color.rgb(255, 170, 90)};
            case Level.P_S4: return new int[]{Color.rgb(255, 110, 110)};
            case Level.P_S5: return new int[]{Color.rgb(230, 90, 255)};
            case Level.P_S6: return new int[]{Color.rgb(255, 60, 160)};
            default: return new int[]{Color.rgb(140, 255, 255)};
        }
    }

    static String portalLabel(int k) {
        switch (k) {
            case Level.P_CUBE: return "CUB";
            case Level.P_SHIP: return "SHP";
            case Level.P_BALL: return "BAL";
            case Level.P_UFO: return "UFO";
            case Level.P_WAVE: return "WAV";
            case Level.P_ROBOT: return "ROB";
            case Level.P_SPIDER: return "SPD";
            case Level.P_WORM: return "WRM";
            case Level.P_GUP: return "↑";
            case Level.P_GDOWN: return "↓";
            case Level.P_GTOG: return "⇅";
            case Level.P_MINI: return "MIN";
            case Level.P_BIG: return "BIG";
            case Level.P_DUAL: return "DUA";
            case Level.P_SINGLE: return "SNG";
            case Level.P_S05: return "0.5";
            case Level.P_S1: return "1x";
            case Level.P_S2: return "2x";
            case Level.P_S3: return "3x";
            case Level.P_S4: return "4x";
            case Level.P_S5: return "5x";
            case Level.P_S6: return "6x";
            default: return "TP";
        }
    }

    void drawPlayer(Canvas c, Engine.Player p, int col) {
        if (p.mode == Gd.WAVE && p.trailOn && p.waveTrail.size() > 1) {
            Paint pen = Gd.stroke(Color.argb(150, Color.red(col), Color.green(col), Color.blue(col)),
                    Math.max(2f, (float) (p.size() * 0.35 * scale())));
            Path path = new Path();
            int start = Math.max(0, p.waveTrail.size() - 500);
            for (int i = start; i < p.waveTrail.size(); i++) {
                float[] pt = p.waveTrail.get(i);
                if (i == start) path.moveTo(sx(pt[0]), sy(pt[1])); else path.lineTo(sx(pt[0]), sy(pt[1]));
            }
            c.drawPath(path, pen);
        }

        RectF box = p.box();
        RectF r = worldRect(box.left, box.top, box.width(), box.height());
        int dark = Gd.blend(col, Color.BLACK, 0.55), light = Gd.blend(col, Color.WHITE, 0.55);

        c.save();
        c.rotate((float) p.rotation, r.centerX(), r.centerY());
        if (p.mirror) c.scale(-1, 1, r.centerX(), r.centerY());

        switch (p.mode) {
            case Gd.CUBE: {
                c.drawRect(r, Gd.fill(col));
                c.drawRect(r, Gd.stroke(dark, Math.max(2f, r.width() * 0.11f)));
                RectF in = new RectF(r); in.inset(r.width() * 0.3f, r.width() * 0.3f);
                c.drawRect(in, Gd.fill(light));
                break;
            }
            case Gd.SHIP: {
                Path path = new Path();
                path.moveTo(r.left - r.width() * 0.45f, r.bottom);
                path.lineTo(r.right + r.width() * 0.5f, r.centerY());
                path.lineTo(r.left - r.width() * 0.45f, r.top);
                path.close();
                c.drawPath(path, Gd.fill(Gd.blend(col, Color.WHITE, 0.2)));
                RectF in = new RectF(r); in.inset(r.width() * 0.22f, r.width() * 0.22f);
                c.drawRect(in, Gd.fill(col));
                break;
            }
            case Gd.BALL:
                c.drawOval(r, Gd.fill(col));
                c.drawOval(r, Gd.stroke(dark, Math.max(2f, r.width() * 0.12f)));
                c.drawRect(new RectF(r.left, r.centerY() - r.height() * 0.1f, r.right, r.centerY() + r.height() * 0.1f), Gd.fill(light));
                break;
            case Gd.UFO:
                c.drawOval(new RectF(r.left + r.width() * 0.18f, r.top, r.right - r.width() * 0.18f, r.top + r.height() * 0.62f),
                        Gd.fill(Color.argb(180, Color.red(light), Color.green(light), Color.blue(light))));
                c.drawOval(new RectF(r.left - r.width() * 0.15f, r.top + r.height() * 0.45f,
                        r.right + r.width() * 0.15f, r.top + r.height() * 0.9f), Gd.fill(col));
                break;
            case Gd.WAVE: {
                Path path = new Path();
                path.moveTo(r.left, r.centerY()); path.lineTo(r.centerX(), r.top);
                path.lineTo(r.right, r.centerY()); path.lineTo(r.centerX(), r.bottom);
                path.close();
                c.drawPath(path, Gd.fill(col));
                c.drawPath(path, Gd.stroke(dark, 2f));
                break;
            }
            case Gd.ROBOT:
                c.drawRect(new RectF(r.left, r.top, r.right, r.top + r.height() * 0.62f), Gd.fill(col));
                c.drawRect(new RectF(r.left + r.width() * 0.12f, r.top + r.height() * 0.62f,
                        r.left + r.width() * 0.36f, r.bottom), Gd.fill(dark));
                c.drawRect(new RectF(r.left + r.width() * 0.64f, r.top + r.height() * 0.62f,
                        r.left + r.width() * 0.88f, r.bottom), Gd.fill(dark));
                break;
            case Gd.SPIDER: {
                RectF in = new RectF(r); in.inset(r.width() * 0.1f, r.width() * 0.1f);
                c.drawRect(in, Gd.fill(col));
                Paint pen = Gd.stroke(dark, Math.max(1.6f, r.width() * 0.09f));
                for (int i = -1; i <= 1; i += 2)
                    for (int k = 0; k < 3; k++) {
                        float y = r.top + r.height() * (0.2f + k * 0.3f);
                        c.drawLine(r.centerX(), y, r.centerX() + i * r.width() * 0.75f, y + r.height() * 0.25f, pen);
                    }
                break;
            }
            case Gd.WORM: {
                float seg = r.width() * 0.46f;
                for (int i = 3; i >= 0; i--) {
                    float k = 1f - i * 0.16f;
                    int c2 = Gd.blend(col, p.noClipWorm ? Color.BLACK : Color.WHITE, i * 0.12);
                    float cx = r.centerX() - i * seg * 0.62f;
                    float cy = r.centerY() + (float) Math.sin(System.currentTimeMillis() / 90.0 + i) * r.height() * 0.08f;
                    c.drawCircle(cx, cy, seg * k / 2, Gd.fill(Color.argb(i == 0 ? 255 : 210,
                            Color.red(c2), Color.green(c2), Color.blue(c2))));
                }
                c.drawCircle(r.left + r.width() * 0.68f, r.top + r.height() * 0.42f, r.width() * 0.07f, Gd.fill(Color.WHITE));
                break;
            }
        }
        c.restore();
    }

    void drawHud(Canvas c, int W, int H) {
        float barW = Math.min(W * 0.6f, 600 * (W / 1280f));
        RectF bar = new RectF(W / 2f - barW / 2, 16, W / 2f + barW / 2, 36);
        c.drawRect(bar, Gd.fill(Color.argb(120, 10, 12, 22)));
        Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
        p.setShader(new LinearGradient(bar.left, 0, bar.right, 0, Gd.NEON, Gd.LIME, Shader.TileMode.CLAMP));
        c.drawRect(new RectF(bar.left, bar.top, bar.left + (float) (bar.width() * eng.progress), bar.bottom), p);
        c.drawRect(bar, Gd.stroke(Color.argb(200, 255, 255, 255), 2f));
        Gd.outlined(c, (int) (eng.progress * 100) + "%", bar.right + 10, bar.bottom - 2, 20f, Color.WHITE, Paint.Align.LEFT);

        if (practice)
            for (double[] cp : eng.checkpoints) {
                float x = bar.left + (float) (bar.width() * Math.max(0, Math.min(1, cp[0] / Math.max(1, eng.rightEdge))));
                c.drawLine(x, bar.top, x, bar.bottom, Gd.stroke(Color.rgb(120, 255, 160), 2f));
            }

        Gd.outlined(c, "Попытка " + attempts, 16, 36, 22f, Color.WHITE, Paint.Align.LEFT);
        String info = Gd.MODE_NAMES[eng.p1.mode] + " · " + Gd.SPEED_NAMES[eng.p1.speed]
                + (eng.p1.mini ? " · мини" : "") + (eng.dual ? " · дуал" : "")
                + (practice ? " · тренировка" : "");
        Gd.outlined(c, info, 16, 62, 16f, Gd.DIM, Paint.Align.LEFT);
        if (eng.coinsGot > 0) Gd.outlined(c, "Монеты: " + eng.coinsGot, 16, 86, 16f, Gd.GOLD, Paint.Align.LEFT);
        if (!Api.online) Gd.outlined(c, "ОФЛАЙН", W - 16, 36, 16f, Gd.GOLD, Paint.Align.RIGHT);

        if (toastT > 0)
            Gd.outlined(c, toast, W / 2f, H - 40, 22f, Color.WHITE, Paint.Align.CENTER);
    }

    void drawOverlay(Canvas c, int W, int H, String title, String[] lines) {
        c.drawColor(Color.argb(205, 12, 14, 26));
        Gd.outlined(c, title, W / 2f, H * 0.3f, Math.min(54f, W * 0.055f), Gd.PAPER, Paint.Align.CENTER);
        float y = H * 0.42f;
        for (String l : lines) {
            Gd.outlined(c, l, W / 2f, y, 20f, Gd.DIM, Paint.Align.CENTER);
            y += 30;
        }
    }
}
