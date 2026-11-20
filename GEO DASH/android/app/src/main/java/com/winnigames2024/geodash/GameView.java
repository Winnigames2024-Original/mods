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
    // настраиваемый интерфейс
    // мультиплеер
    String mpRoom;
    int mpMax;
    org.json.JSONArray mpPlayers;
    long mpLast;

    final GuiLayout gui = GuiLayout.load();
    boolean guiEdit;
    GuiLayout.Item dragItem;
    float dragOffX, dragOffY;
    boolean btnLeft, btnRight;
    final java.util.List<Object[]> hit = new java.util.ArrayList<>();   // {RectF, String}
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
                eng.moveX = (btnRight ? 1 : 0) - (btnLeft ? 1 : 0);
                mpTick();
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
        int idx = e.getActionIndex();
        float x = e.getX(idx), y = e.getY(idx);
        boolean right = x > getWidth() / 2f;

        switch (act) {
            case MotionEvent.ACTION_DOWN:
            case MotionEvent.ACTION_POINTER_DOWN: {
                if (eng.finished) { exit(); return true; }
                String id = hitId(x, y);

                // режим настройки интерфейса
                if (guiEdit) {
                    if ("g:done".equals(id)) { guiEdit = false; gui.save(); say("Расположение сохранено"); return true; }
                    if ("g:reset".equals(id)) { gui.reset(); gui.save(); return true; }
                    if ("g:plus".equals(id)) { resizeGui(1.12); return true; }
                    if ("g:minus".equals(id)) { resizeGui(1 / 1.12); return true; }
                    if (id != null && id.startsWith("i:")) {
                        dragItem = itemByName(id.substring(2));
                        if (dragItem != null) {
                            dragOffX = x - (float) (dragItem.x * getWidth());
                            dragOffY = y - (float) (dragItem.y * getHeight());
                        }
                    }
                    return true;
                }

                if (paused) {
                    if ("p:resume".equals(id)) togglePause();
                    else if ("p:restart".equals(id)) { attempts++; eng.restart(); togglePause(); }
                    else if ("p:gui".equals(id)) { guiEdit = true; say("Тащи кнопки куда удобно"); }
                    else if ("p:practice".equals(id)) {
                        practice = !practice; eng.practice = practice;
                        say(practice ? "Тренировка включена" : "Обычный режим");
                    } else if ("p:exit".equals(id)) exit();
                    return true;
                }

                if ("menu".equals(id)) { togglePause(); return true; }
                if ("left".equals(id)) { btnLeft = true; return true; }
                if ("right".equals(id)) { btnRight = true; return true; }
                if ("jump".equals(id)) { if (!hold1) pressed1 = true; hold1 = true; return true; }

                if (level.settings.twoPlayer && right) { if (!hold2) pressed2 = true; hold2 = true; }
                else { if (!hold1) pressed1 = true; hold1 = true; }
                return true;
            }

            case MotionEvent.ACTION_MOVE:
                if (guiEdit && dragItem != null) {
                    dragItem.x = Math.max(0.02, Math.min(0.98, (x - dragOffX) / Math.max(1, getWidth())));
                    dragItem.y = Math.max(0.02, Math.min(0.98, (y - dragOffY) / Math.max(1, getHeight())));
                }
                return true;

            case MotionEvent.ACTION_UP:
            case MotionEvent.ACTION_POINTER_UP:
            case MotionEvent.ACTION_CANCEL:
                if (dragItem != null) { dragItem = null; gui.save(); }
                btnLeft = false; btnRight = false;
                if (level.settings.twoPlayer && right) hold2 = false; else hold1 = false;
                if (e.getPointerCount() <= 1) { hold1 = false; hold2 = false; }
                return true;
        }
        return true;
    }

    String hitId(float x, float y) {
        for (int i = hit.size() - 1; i >= 0; i--) {
            RectF r = (RectF) hit.get(i)[0];
            if (r.contains(x, y)) return (String) hit.get(i)[1];
        }
        return null;
    }

    GuiLayout.Item itemByName(String n) {
        for (GuiLayout.Item i : gui.items()) if (i.name.equals(n)) return i;
        return null;
    }

    void resizeGui(double k) {
        for (GuiLayout.Item i : gui.items()) i.size = Math.max(0.04, Math.min(0.6, i.size * k));
        gui.save();
    }

    /** Зайти в мультиплеерную комнату уровня. */
    public void mpStart() {
        if (!level.settings.multiplayer || level.settings.serverId <= 0 || !Store.loggedIn()) return;
        final int id = level.settings.serverId;
        final int max = Math.max(2, Math.min(50, level.settings.maxPlayers));
        Api.async(() -> {
            String room = Api.mpJoin(id, max);
            if (room != null) {
                mpRoom = room;
                mpMax = max;
                say("Мультиплеер: комната на " + max);
            }
        });
    }

    public void mpStop() {
        final String r = mpRoom;
        mpRoom = null;
        if (r != null) Api.async(() -> Api.mpLeave(r));
    }

    /** Раз в ~120 мс отправляем себя и забираем остальных. */
    void mpTick() {
        if (mpRoom == null) return;
        long now = System.currentTimeMillis();
        if (now - mpLast < 120) return;
        mpLast = now;
        final String room = mpRoom;
        Api.async(() -> {
            org.json.JSONObject r = Api.mpState(room, eng.p1.x, eng.p1.y, eng.p1.mode,
                    eng.p1.gravityDir, eng.p1.mini, eng.dead, eng.progress * 100,
                    eng.color(Engine.CH_P1));
            if (r != null) {
                mpPlayers = r.optJSONArray("players");
                mpMax = r.optInt("max", mpMax);
            }
        });
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

        // фон как в игре: ровный цвет с узором, едет медленнее уровня
        float tex = Math.max(0.35f, Math.min(3f, scale() * 0.95f));
        Backdrop.background(c, new RectF(0, 0, W, H), level.settings.bgId, bg,
                eng.camX * scale() * 0.26, eng.camY * scale() * 0.16, tex);

        if (eng.fx.gradientOn) {
            Paint gp = new Paint();
            gp.setShader(new LinearGradient(0, 0, 0, H, eng.fx.gradTop, eng.fx.gradBottom, Shader.TileMode.CLAMP));
            c.drawRect(0, 0, W, H, gp);
        }

        // огромные глаза на фоне — следят за игроком и вспыхивают в такт
        if (level.settings.eyesBackground) drawEyes(c, W, H);

        drawGround(c, W, H);

        float visLeft = (float) ((0 - W / 2.0) / scale() + eng.camX) - 120;
        float visRight = (float) ((W - W / 2.0) / scale() + eng.camX) + 120;

        for (int i = 0; i < eng.level.objects.size(); i++) {
            Engine.ObjState s = eng.st[i];
            if (!s.enabled) continue;
            Level.Obj o = eng.level.objects.get(i);
            Catalog.Def d = Catalog.get(o.typeId);
            if (d.invisible) continue;
            if (o.hideInGame) continue;            // «скрытый» блок — только в редакторе
            double x = o.x + s.dx;
            if (x + d.w * o.scaleX < visLeft || x > visRight) continue;
            drawObject(c, i, o, d, s);
        }

        if (!eng.p1.hidden) drawPlayer(c, eng.p1, eng.color(Engine.CH_P1));
        if (eng.dual && !eng.p2.hidden) drawPlayer(c, eng.p2, eng.color(Engine.CH_P2));

        drawFx(c, W, H);
        drawOthers(c);
        drawHud(c, W, H);
        if (eng.dead) c.drawColor(Color.argb(70, 160, 20, 30));
        if (eng.finished) drawOverlay(c, W, H, "УРОВЕНЬ ПРОЙДЕН", new String[]{
                level.settings.name,
                "попыток: " + attempts + "   монет: " + eng.coinsGot + "/3",
                "время: " + String.format("%.1f", eng.time) + " c",
                "коснись экрана, чтобы выйти"});

    }

    // кисти для глаз создаются один раз — в кадре ничего не выделяется
    private final Paint eyeWhite = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint eyeDark = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint eyeRed = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint eyeLine = new Paint(Paint.ANTI_ALIAS_FLAG);

    /** Фон уровня iSpy: три огромных глаза, зрачок тянется к игроку. */
    void drawEyes(Canvas c, int W, int H) {
        eyeDark.setColor(0xFF0C0C0E);
        eyeLine.setColor(0xFF0A0A0C);
        eyeLine.setStyle(Paint.Style.STROKE);
        eyeLine.setStrokeWidth(Math.max(3f, H * 0.008f));

        // доля такта трека BIKE: 141,3 удара в минуту
        final double BEAT = 60.0 / 141.3;
        double beat = (eng.time % BEAT) / BEAT;
        float flash = (float) Math.max(0, 1 - beat * 3);

        float px = (float) ((eng.p1.x - eng.camX) * scale() + W / 2.0);
        float py = (float) (H / 2.0 - (eng.p1.y - eng.camY) * scale());

        // шесть глаз разного размера и на разной глубине
        final float[][] spots = {
            {0.18f, 0.30f, 1.30f, 0.05f},
            {0.52f, 0.52f, 0.95f, 0.08f},
            {0.78f, 0.26f, 0.70f, 0.12f},
            {1.05f, 0.60f, 0.85f, 0.07f},
            {1.28f, 0.22f, 0.60f, 0.14f},
            {1.48f, 0.48f, 1.05f, 0.06f}
        };

        for (float[] sp : spots) {
            float pulse = 1f + flash * 0.07f;      // радужка дышит в такт
            float rw = H * 0.42f * sp[2] * pulse;
            float rh = rw * 0.62f;

            float ex = (float) ((sp[0] * W - eng.camX * scale() * sp[3]) % (W * 1.6));
            if (ex < -W * 0.5f) ex += W * 1.6f;
            float ey = H * sp[1];

            int g2 = (int) (245 * (1 - flash * 0.85f));
            eyeWhite.setColor(Color.argb(235, 245, g2, g2));
            RectF eye = new RectF(ex - rw / 2, ey - rh / 2, ex + rw / 2, ey + rh / 2);
            c.drawOval(eye, eyeWhite);
            c.drawOval(eye, eyeLine);

            float dx = Math.max(-1f, Math.min(1f, (px - ex) / (W * 0.5f)));
            float dy = Math.max(-1f, Math.min(1f, (py - ey) / (H * 0.5f)));
            float pr = rh * 0.34f;
            float cx = ex + dx * rw * 0.22f, cy = ey + dy * rh * 0.22f;
            c.drawCircle(cx, cy, pr, eyeDark);

            eyeRed.setColor(Color.argb((int) (120 + 135 * flash), 220, 30, 40));
            c.drawCircle(cx + pr * 0.45f, cy + pr * 0.45f, pr * 0.55f, eyeRed);
        }
    }

    void drawGround(Canvas c, int W, int H) {
        int gc = eng.color(Engine.CH_GROUND), line = eng.color(Engine.CH_LINE);
        int gid = level.settings.groundId;
        float tex = Math.max(0.35f, Math.min(3f, scale() * 0.95f));
        double scroll = eng.camX * scale();

        float top = sy(eng.groundY);
        if (top < H)
            Backdrop.ground(c, new RectF(0, top, W, H + 2), gid, gc, line, scroll, tex, false);

        float roof = sy(eng.roofY);
        if (roof > 0)
            Backdrop.ground(c, new RectF(0, -2, W, roof), gid, gc, line, scroll, tex, true);
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
            case Catalog.ST_SLOPE_DOWN:
            case Catalog.ST_SLOPE_UP_HALF:
            case Catalog.ST_SLOPE_DOWN_HALF:
            case Catalog.ST_SLOPE_UP_LONG:
            case Catalog.ST_SLOPE_DOWN_LONG: {
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
            case Catalog.ST_IMAGE: {
                android.graphics.Bitmap bm = Images.get(o.imageId);
                if (bm != null) {
                    Paint ip = new Paint(Paint.FILTER_BITMAP_FLAG);
                    ip.setAlpha(Math.max(0, Math.min(255, alpha)));
                    c.drawBitmap(bm, null, r, ip);
                } else {
                    c.drawRect(r, Gd.fill(Color.argb(Math.min(120, alpha), 120, 140, 220)));
                    Gd.outlined(c, "IMG #" + o.imageId, r.centerX(), r.centerY(),
                            Math.max(10f, r.height() * 0.2f), Gd.PAPER, Paint.Align.CENTER);
                }
                break;
            }
            case Catalog.ST_WATER:
            case Catalog.ST_LAVA:
                drawLiquid(c, r, d.style == Catalog.ST_LAVA, alpha);
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

    /** Вода и лава: заливка, волна по поверхности и пузырьки. */
    static void drawLiquid(Canvas c, RectF r, boolean lava, int alpha) {
        if (r.width() < 1 || r.height() < 1) return;
        double t = System.currentTimeMillis() / 1000.0;
        int a = Math.max(0, Math.min(255, alpha));

        int top = lava ? Color.rgb(255, 150, 40) : Color.rgb(90, 190, 255);
        int bot = lava ? Color.rgb(170, 30, 10) : Color.rgb(20, 70, 180);
        Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
        p.setShader(new LinearGradient(0, r.top, 0, r.bottom,
                Color.argb(a * (lava ? 230 : 170) / 255, Color.red(top), Color.green(top), Color.blue(top)),
                Color.argb(a * (lava ? 245 : 205) / 255, Color.red(bot), Color.green(bot), Color.blue(bot)),
                Shader.TileMode.CLAMP));
        c.drawRect(r, p);

        float amp = Math.min(6f, r.height() * 0.12f);
        int seg = (int) Math.max(4, Math.min(48, r.width() / 12));
        Path wave = new Path();
        for (int i = 0; i <= seg; i++) {
            float x = r.left + r.width() * i / seg;
            float y = r.top + (float) (Math.sin(t * (lava ? 1.6 : 2.4) + i * 0.6) * amp);
            if (i == 0) wave.moveTo(x, y); else wave.lineTo(x, y);
        }
        c.drawPath(wave, Gd.stroke(lava ? Color.rgb(255, 220, 120) : Color.rgb(200, 240, 255),
                Math.max(2f, r.height() * 0.06f)));

        int bubbles = (int) Math.max(1, Math.min(8, r.width() / 40));
        for (int i = 0; i < bubbles; i++) {
            double ph = (t * (lava ? 0.35 : 0.6) + i * 0.27) % 1.0;
            float bx = r.left + r.width() * (((i * 0.37f) + 0.12f) % 1f);
            float by = r.bottom - (float) (r.height() * ph);
            float bs = Math.max(2f, r.height() * (lava ? 0.09f : 0.06f));
            int bc = lava ? Color.rgb(255, 230, 150) : Color.WHITE;
            c.drawCircle(bx, by, bs / 2, Gd.fill(Color.argb((int) (a * 0.5 * (1 - ph)),
                    Color.red(bc), Color.green(bc), Color.blue(bc))));
        }
    }

    /** Частицы, ударная волна, виньетка и вспышка. */
    void drawFx(Canvas c, int W, int H) {
        Fx fx = eng.fx;

        for (Fx.Particle p : fx.particles) {
            float x = sx(p.x), y = sy(p.y);
            float sz = (float) (p.size * Math.max(0.4f, scale()));
            int a = (int) Math.max(0, Math.min(255, 255 * (p.life / Math.max(0.1, p.maxLife))));
            c.drawCircle(x, y, sz / 2, Gd.fill(Color.argb(a,
                    Color.red(p.color), Color.green(p.color), Color.blue(p.color))));
        }

        if (fx.shock.on()) {
            double k = 1 - fx.shock.k() / Math.max(0.001, fx.shock.str);
            float cx = sx(fx.shockX), cy = sy(fx.shockY);
            float rad = (float) (40 + k * 900 * scale());
            int a = (int) Math.max(0, Math.min(200, 200 * fx.shock.k() / Math.max(0.001, fx.shock.str)));
            c.drawCircle(cx, cy, rad, Gd.stroke(Color.argb(a, 255, 255, 255),
                    (float) (14 * fx.shock.str * (1 - k) + 2)));
        }

        if (fx.vignette.on()) {
            Paint vp = new Paint();
            vp.setShader(new RadialGradient(W / 2f, H / 2f, Math.max(W, H) * 0.75f,
                    Color.TRANSPARENT, Color.argb((int) (220 * fx.vignette.k()), 0, 0, 0), Shader.TileMode.CLAMP));
            c.drawRect(0, 0, W, H, vp);
        }

        if (fx.flash.on())
            c.drawColor(Color.argb((int) Math.max(0, Math.min(255, 255 * fx.flash.k())),
                    Color.red(fx.flashColor), Color.green(fx.flashColor), Color.blue(fx.flashColor)));
    }

    /** Другие игроки в мультиплеере. */
    void drawOthers(Canvas c) {
        if (mpPlayers == null || mpPlayers.length() == 0) return;
        for (int i = 0; i < mpPlayers.length(); i++) {
            org.json.JSONObject o = mpPlayers.optJSONObject(i);
            if (o == null) continue;
            double size = o.optBoolean("mini") ? 22 : 30;
            RectF r = worldRect(o.optDouble("x", 0) - size / 2, o.optDouble("y", 0) - size / 2, size, size);
            if (r.right < -60 || r.left > getWidth() + 60) continue;
            int col = o.optInt("color", Color.rgb(102, 204, 255));
            boolean dead = o.optBoolean("dead");
            c.drawRect(r, Gd.fill(Color.argb(dead ? 70 : 150, Color.red(col), Color.green(col), Color.blue(col))));
            c.drawRect(r, Gd.stroke(Color.argb(dead ? 90 : 200, 10, 10, 25), 2.5f));
            Gd.outlined(c, o.optString("name"), r.centerX(), r.top - 8,
                    Math.max(10f, r.height() * 0.42f), Color.argb(230, 235, 245, 255), Paint.Align.CENTER);
        }
    }

    void drawHud(Canvas c, int W, int H) {
        hit.clear();

        // ---- полоса прогресса
        GuiLayout.Item pi = gui.progress;
        float pw = (float) (pi.size * W), ph = Math.max(14f, H * 0.05f);
        RectF bar = new RectF((float) (pi.x * W) - pw / 2, (float) (pi.y * H) - ph / 2,
                (float) (pi.x * W) + pw / 2, (float) (pi.y * H) + ph / 2);
        c.drawRoundRect(bar, ph / 2, ph / 2, Gd.fill(Color.argb(190, 10, 12, 26)));
        float fw = (bar.width() - 6) * (float) eng.progress;
        if (fw > 2) {
            RectF f = new RectF(bar.left + 3, bar.top + 3, bar.left + 3 + fw, bar.bottom - 3);
            Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
            p.setShader(new LinearGradient(0, f.top, 0, f.bottom,
                    Color.rgb(140, 255, 170), Color.rgb(20, 170, 110), Shader.TileMode.CLAMP));
            c.drawRoundRect(f, (bar.height() - 6) / 2, (bar.height() - 6) / 2, p);
        }
        c.drawRoundRect(bar, ph / 2, ph / 2, Gd.stroke(Gd.INK, 3f));
        Gd.outlined(c, (int) (eng.progress * 100) + "%", bar.centerX(), bar.centerY() + ph * 0.22f,
                ph * 0.6f, Gd.PAPER, Paint.Align.CENTER);
        hit.add(new Object[]{bar, "i:" + pi.name});

        if (practice)
            for (double[] cp : eng.checkpoints) {
                float x = bar.left + (float) (bar.width() * Math.max(0, Math.min(1, cp[0] / Math.max(1, eng.rightEdge))));
                c.drawLine(x, bar.top, x, bar.bottom, Gd.stroke(Color.rgb(120, 255, 160), 2f));
            }

        // ---- кнопка «=»
        RectF mr = GuiLayout.rect(gui.menu, W, H);
        Gd.buttonPlate(c, mr, Color.rgb(150, 160, 205), Color.rgb(60, 66, 110), mr.height() * 0.26f, false);
        Paint lines = Gd.stroke(Gd.PAPER, Math.max(3f, mr.height() * 0.09f));
        c.drawLine(mr.centerX() - mr.width() * 0.26f, mr.centerY() - mr.height() * 0.12f,
                mr.centerX() + mr.width() * 0.26f, mr.centerY() - mr.height() * 0.12f, lines);
        c.drawLine(mr.centerX() - mr.width() * 0.26f, mr.centerY() + mr.height() * 0.12f,
                mr.centerX() + mr.width() * 0.26f, mr.centerY() + mr.height() * 0.12f, lines);
        hit.add(new Object[]{mr, guiEdit ? "i:" + gui.menu.name : "menu"});

        // ---- кнопки платформера
        if (eng.platformer || guiEdit) {
            padButton(c, GuiLayout.rect(gui.left, W, H), "◄", "left", gui.left.name, btnLeft);
            padButton(c, GuiLayout.rect(gui.right, W, H), "►", "right", gui.right.name, btnRight);
            padButton(c, GuiLayout.rect(gui.jump, W, H), "▲", "jump", gui.jump.name, hold1);
        }

        // ---- сведения
        float fs = Math.min(H * 0.045f, 26f);
        Gd.outlined(c, "ПОПЫТКА " + attempts, mr.right + 12, mr.centerY() + fs * 0.35f, fs, Gd.PAPER, Paint.Align.LEFT);
        String info = Gd.MODE_NAMES[eng.p1.mode] + " · " + Gd.SPEED_NAMES[eng.p1.speed]
                + (eng.p1.mini ? " · мини" : "") + (eng.dual ? " · дуал" : "")
                + (practice ? " · тренировка" : "");
        Gd.outlined(c, info, mr.right + 12, mr.bottom + fs * 0.7f, fs * 0.62f,
                Color.argb(235, 215, 230, 255), Paint.Align.LEFT);
        if (eng.coinsGot > 0)
            for (int i = 0; i < eng.coinsGot; i++)
                Gd.coin(c, new RectF(mr.right + 12 + i * fs * 1.1f, mr.bottom + fs, 
                        mr.right + 12 + i * fs * 1.1f + fs * 0.9f, mr.bottom + fs * 1.9f), true);
        if (!Api.online)
            Gd.outlined(c, "ОФЛАЙН", W - 16, fs * 1.4f, fs * 0.62f, Gd.GOLD, Paint.Align.RIGHT);
        if (mpRoom != null)
            Gd.outlined(c, "В КАРТЕ: " + ((mpPlayers == null ? 0 : mpPlayers.length()) + 1) + " / " + mpMax,
                    W - 16, fs * 2.3f, fs * 0.62f, Gd.LIME, Paint.Align.RIGHT);

        // в лаве — обратный отсчёт до смерти
        if (eng.p1.lavaTime > 0.05) {
            float lw = Math.min(420, W * 0.34f);
            RectF lr = new RectF(W / 2f - lw / 2, H - 150, W / 2f + lw / 2, H - 124);
            double left = Math.max(0, Engine.LAVA_SECONDS - eng.p1.lavaTime);
            c.drawRoundRect(lr, 13f, 13f, Gd.fill(Color.argb(200, 40, 8, 4)));
            float lavaW = (float) ((lr.width() - 6) * (left / Engine.LAVA_SECONDS));
            if (lavaW > 2) {
                RectF f2 = new RectF(lr.left + 3, lr.top + 3, lr.left + 3 + lavaW, lr.bottom - 3);
                Paint lp = new Paint(Paint.ANTI_ALIAS_FLAG);
                lp.setShader(new LinearGradient(0, f2.top, 0, f2.bottom,
                        Color.rgb(255, 210, 90), Color.rgb(220, 60, 20), Shader.TileMode.CLAMP));
                c.drawRoundRect(f2, 10f, 10f, lp);
            }
            c.drawRoundRect(lr, 13f, 13f, Gd.stroke(Gd.INK, 3f));
            Gd.outlined(c, String.format("ЛАВА — %.1f с", left), lr.centerX(),
                    lr.centerY() + lr.height() * 0.22f, lr.height() * 0.58f, Gd.PAPER, Paint.Align.CENTER);
        }

        if (toastT > 0)
            Gd.outlined(c, toast, W / 2f, H - 40, fs * 0.9f, Gd.GOLD, Paint.Align.CENTER);

        if (guiEdit) drawGuiEditor(c, W, H);
        else if (paused) drawPause(c, W, H);
    }

    void padButton(Canvas c, RectF r, String sign, String id, String name, boolean down) {
        Gd.buttonPlate(c, r, Color.rgb(120, 190, 255), Color.rgb(25, 85, 180), r.height() * 0.26f, down);
        Gd.outlined(c, sign, r.centerX(), r.centerY() + r.height() * 0.17f, r.height() * 0.42f,
                Gd.PAPER, Paint.Align.CENTER);
        hit.add(new Object[]{r, guiEdit ? "i:" + name : id});
    }

    void gdBtn(Canvas c, RectF r, String text, String id, int c1, int c2) {
        Gd.buttonPlate(c, r, c1, c2, r.height() * 0.26f, false);
        Gd.outlined(c, text, r.centerX(), r.centerY() + r.height() * 0.13f,
                Math.min(r.height() * 0.38f, r.width() * 0.11f), Gd.PAPER, Paint.Align.CENTER);
        hit.add(new Object[]{r, id});
    }

    /** Режим «настроить GUI». */
    void drawGuiEditor(Canvas c, int W, int H) {
        c.drawColor(Color.argb(120, 8, 10, 24));
        for (GuiLayout.Item i : gui.items()) {
            RectF r = i == gui.progress
                    ? new RectF((float) (i.x * W) - (float) (i.size * W) / 2, (float) (i.y * H) - 18,
                                (float) (i.x * W) + (float) (i.size * W) / 2, (float) (i.y * H) + 18)
                    : GuiLayout.rect(i, W, H);
            Paint p = Gd.stroke(Gd.NEON, 3f);
            p.setPathEffect(new DashPathEffect(new float[]{12, 8}, 0));
            c.drawRect(r, p);
            Gd.outlined(c, i.name, r.centerX(), r.top - 10, Math.min(H * 0.035f, 20f), Gd.NEON, Paint.Align.CENTER);
        }
        float bw = W * 0.17f, bh = H * 0.12f, y = H - bh - 14;
        float x = W / 2f - (bw * 2 + bh * 2 + 24) / 2f;
        gdBtn(c, new RectF(x, y, x + bw, y + bh), "ГОТОВО", "g:done", Color.rgb(120, 255, 170), Color.rgb(20, 150, 100));
        gdBtn(c, new RectF(x + bw + 8, y, x + bw * 2 + 8, y + bh), "СБРОС", "g:reset", Color.rgb(255, 180, 90), Color.rgb(170, 95, 10));
        gdBtn(c, new RectF(x + bw * 2 + 16, y, x + bw * 2 + bh + 16, y + bh), "+", "g:plus", Color.rgb(150, 160, 205), Color.rgb(60, 66, 110));
        gdBtn(c, new RectF(x + bw * 2 + bh + 24, y, x + bw * 2 + bh * 2 + 24, y + bh), "−", "g:minus", Color.rgb(150, 160, 205), Color.rgb(60, 66, 110));
        Gd.outlined(c, "тащи кнопки пальцем · + и − меняют размер · ГОТОВО — сохранить",
                W / 2f, y - 16, Math.min(H * 0.04f, 22f), Gd.PAPER, Paint.Align.CENTER);
    }

    void drawPause(Canvas c, int W, int H) {
        c.drawColor(Color.argb(185, 10, 12, 26));
        Gd.outlined(c, "ПАУЗА", W / 2f, H * 0.16f, Math.min(54f, W * 0.055f), Gd.PAPER, Paint.Align.CENTER);
        Gd.outlined(c, level.settings.name + "   ·   лучший " + (int) (best * 100) + "%   ·   попыток " + attempts,
                W / 2f, H * 0.23f, Math.min(22f, W * 0.022f), Color.argb(235, 215, 230, 255), Paint.Align.CENTER);

        float bw = Math.min(W * 0.34f, 420), bh = H * 0.115f, gap = H * 0.025f;
        float x = W / 2f - bw / 2, y = H * 0.28f;
        gdBtn(c, new RectF(x, y, x + bw, y + bh), "ПРОДОЛЖИТЬ", "p:resume", Color.rgb(120, 255, 170), Color.rgb(20, 150, 100));
        y += bh + gap;
        gdBtn(c, new RectF(x, y, x + bw, y + bh), "ЗАНОВО", "p:restart", Color.rgb(255, 190, 90), Color.rgb(175, 100, 10));
        y += bh + gap;
        gdBtn(c, new RectF(x, y, x + bw, y + bh), "НАСТРОИТЬ GUI", "p:gui", Color.rgb(140, 200, 255), Color.rgb(30, 95, 190));
        y += bh + gap;
        gdBtn(c, new RectF(x, y, x + bw, y + bh), practice ? "ТРЕНИРОВКА: ВКЛ" : "ТРЕНИРОВКА: ВЫКЛ", "p:practice",
                Color.rgb(200, 170, 255), Color.rgb(100, 60, 180));
        y += bh + gap;
        gdBtn(c, new RectF(x, y, x + bw, y + bh), "ВЫХОД", "p:exit", Color.rgb(240, 120, 120), Color.rgb(150, 40, 50));
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
