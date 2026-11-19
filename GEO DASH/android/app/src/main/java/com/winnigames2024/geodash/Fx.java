package com.winnigames2024.geodash;

import android.graphics.Color;

import java.util.ArrayList;
import java.util.List;
import java.util.Random;

/**
 * Экранные эффекты как в 2.2: инверсия, ч/б, пиксели, глюк, хроматика,
 * размытие, виньетка, вспышка, ударная волна, градиент и частицы.
 */
public class Fx {

    public static class Slot {
        public double t, dur, str;

        public void set(double d, double s) {
            dur = Math.max(0.02, d);
            t = dur;
            str = Math.max(0, Math.min(1, s));
        }

        public void update(double dt) { if (t > 0) t = Math.max(0, t - dt); }

        public double k() { return (t <= 0 || dur <= 0) ? 0 : (t / dur) * str; }
        public boolean on() { return t > 0 && str > 0.001; }
    }

    public static class Particle {
        public double x, y, vx, vy, life, maxLife, size;
        public int color;
    }

    public final Slot invert = new Slot(), gray = new Slot(), pixelate = new Slot(),
            glitch = new Slot(), chromatic = new Slot(), blur = new Slot(),
            vignette = new Slot(), flash = new Slot(), shock = new Slot();

    public int flashColor = Color.WHITE;
    public double shockX, shockY;

    public boolean gradientOn;
    public int gradTop = Color.TRANSPARENT, gradBottom = Color.TRANSPARENT;

    public double timeScale = 1;

    public final List<Particle> particles = new ArrayList<>();
    static final Random RND = new Random();

    public void reset() {
        for (Slot s : new Slot[]{invert, gray, pixelate, glitch, chromatic, blur, vignette, flash, shock}) {
            s.t = 0; s.dur = 0; s.str = 0;
        }
        gradientOn = false;
        timeScale = 1;
        particles.clear();
    }

    /** Нужна ли отрисовка через картинку (цветовые эффекты). */
    public boolean anyPost() {
        return invert.on() || gray.on() || pixelate.on() || glitch.on() || chromatic.on() || blur.on();
    }

    public void burst(double x, double y, int count, int col, double power) {
        for (int i = 0; i < count; i++) {
            double a = RND.nextDouble() * Math.PI * 2;
            double v = power * (0.4 + RND.nextDouble());
            Particle p = new Particle();
            p.x = x; p.y = y;
            p.vx = Math.cos(a) * v; p.vy = Math.sin(a) * v;
            p.life = 0.5 + RND.nextDouble() * 0.7;
            p.maxLife = 1.2;
            p.size = 4 + RND.nextDouble() * 8;
            p.color = col;
            particles.add(p);
        }
        while (particles.size() > 400) particles.remove(0);
    }

    public void update(double dt) {
        invert.update(dt); gray.update(dt); pixelate.update(dt); glitch.update(dt);
        chromatic.update(dt); blur.update(dt); vignette.update(dt); flash.update(dt); shock.update(dt);

        for (int i = particles.size() - 1; i >= 0; i--) {
            Particle p = particles.get(i);
            p.life -= dt;
            if (p.life <= 0) { particles.remove(i); continue; }
            p.x += p.vx * dt;
            p.y += p.vy * dt;
            p.vy -= 420 * dt;
            p.vx *= 1 - Math.min(0.9, dt * 1.2);
        }
    }
}
