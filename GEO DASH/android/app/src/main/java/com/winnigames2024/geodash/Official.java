package com.winnigames2024.geodash;

import java.util.ArrayList;
import java.util.List;

/** Основные уровни: Easy / Hard / Insane — те же, что в игре на Windows. */
public class Official {

    static class B {
        Level lv = new Level();

        B(String name, int diff, int songId, String songName) {
            lv.settings.name = name;
            lv.settings.author = "Winnigames2024";
            lv.settings.difficulty = diff;
            lv.settings.songId = songId;
            lv.settings.songName = songName;
            lv.settings.localId = "official_" + Math.abs(songId);
        }

        void obj(int type, double gx, double gy) { obj(type, gx, gy, 0, 0, 0); }

        void obj(int type, double gx, double gy, int portal, int orb, int coin) {
            Level.Obj o = new Level.Obj();
            o.typeId = type; o.x = gx * 30; o.y = gy * 30;
            o.portal = portal; o.orb = orb; o.coinIndex = coin;
            lv.objects.add(o);
        }

        void block(double gx, double gy, int n) { for (int i = 0; i < n; i++) obj(1, gx + i, gy); }
        void spike(double gx) { obj(20, gx, 0); }
        void spike(double gx, double gy) { obj(20, gx, gy); }
        void small(double gx) { obj(21, gx, 0); }
        void ceil(double gx, double gy) { obj(22, gx, gy); }
        void saw(double gx, double gy) { obj(23, gx, gy); }
        void orb(double gx, double gy, int kind) { obj(40, gx, gy, 0, kind, 0); }
        void pad(double gx, double gy, int kind) {
            Level.Obj o = new Level.Obj();
            o.typeId = 50; o.x = gx * 30; o.y = gy * 30; o.pad = kind;
            lv.objects.add(o);
        }
        void portal(double gx, double gy, int kind) { obj(60, gx, gy, kind, 0, 0); }
        void coin(double gx, double gy, int idx) { obj(80, gx, gy, 0, 0, idx); }
        void text(double gx, double gy, String t) {
            Level.Obj o = new Level.Obj();
            o.typeId = 75; o.x = gx * 30; o.y = gy * 30; o.text = t; o.baseColor = 2;
            lv.objects.add(o);
        }
        void corridor(double gx, int len, int floorY, int ceilY) {
            for (int i = 0; i < len; i++) { obj(1, gx + i, floorY); obj(1, gx + i, ceilY); }
        }
        void finish(double gx) { obj(90, gx, 0); }

        /** Парящий «мост»: сквозь него прорывается червяк. */
        void bridge(double gx, int len, int y, int thick) {
            for (int i = 0; i < len; i++)
                for (int k = 0; k < thick; k++) obj(1, gx + i, y + k);
        }

        /** Слоёная платформа: два моста друг над другом. */
        void layered(double gx, int len, int lowY, int highY, int thick) {
            bridge(gx, len, lowY, thick);
            bridge(gx, len, highY, thick);
        }

        /** Глаз: кольцо из блоков с пилой-зрачком. */
        void eye(double gx, int cy, int rad, boolean pupil) {
            for (int a = 0; a < 20; a++) {
                double ang = a * Math.PI * 2 / 20;
                obj(1, Math.round(gx + Math.cos(ang) * rad), Math.round(cy + Math.sin(ang) * rad));
            }
            if (pupil) saw(gx, cy);
        }

        Level build() { lv.settings.lengthUnits = lv.rightEdge(); return lv; }
    }

    // =================================================================
    //  Пять уровней игры — те же, что в версии для Windows.
    // =================================================================

    /** 1. «Neon Tunnel» — знакомство с прыжками и червяком. */
    public static Level neonTunnel() {
        B b = new B("Neon Tunnel", 2, -1, "Neon Start");
        b.lv.settings.localId = "gd_neon_tunnel";
        b.lv.settings.bgColor = 0xFF0B1646;
        b.lv.settings.groundColor = 0xFF050A26;
        b.lv.settings.bgId = 3; b.lv.settings.groundId = 2;
        b.lv.settings.startSpeed = 1;
        b.lv.settings.desc = "Неоновый тоннель: первые прыжки и первый червяк.";
        b.text(6, 6, "NEON TUNNEL");

        for (int i = 0; i < 5; i++) b.spike(16 + i * 8);
        b.block(58, 0, 5);
        b.spike(64);
        b.pad(70, 1, 0);

        b.block(78, 1, 4); b.block(85, 2, 4); b.block(92, 3, 4);
        b.spike(83); b.spike(90, 1);
        b.orb(100, 5, 0);
        b.coin(104, 7, 1);

        b.portal(112, 1, Level.P_WORM);
        b.text(112, 7, "WORM: КЛИК = СКВОЗЬ БЛОК");
        b.bridge(116, 26, 3, 2);
        for (int i = 0; i < 4; i++) b.obj(20, 122 + i * 7, 5);
        b.coin(138, 8, 2);

        b.bridge(148, 18, 3, 2);
        for (int i = 0; i < 3; i++) b.obj(22, 152 + i * 6, 2);
        b.bridge(172, 18, 3, 2);
        for (int i = 0; i < 3; i++) b.obj(20, 176 + i * 6, 5);

        b.portal(196, 1, Level.P_CUBE);

        for (int i = 0; i < 4; i++) b.spike(204 + i * 9);
        b.orb(240, 4, 1);
        b.coin(246, 6, 3);
        b.text(252, 6, "GG!");
        b.finish(260);
        return b.build();
    }

    /** 2. «Subterranean Pulse» — корабль, куб и слоёные платформы. */
    public static Level subterraneanPulse() {
        B b = new B("Subterranean Pulse", 3, -2, "Static Rush");
        b.lv.settings.localId = "gd_subterranean_pulse";
        b.lv.settings.bgColor = 0xFF06352C;
        b.lv.settings.groundColor = 0xFF031A16;
        b.lv.settings.bgId = 9; b.lv.settings.groundId = 4;
        b.lv.settings.startSpeed = 2;
        b.lv.settings.desc = "Подземный импульс: два слоя подряд — два прорыва.";
        b.text(6, 7, "SUBTERRANEAN PULSE");

        for (int i = 0; i < 6; i++) { b.spike(14 + i * 8); b.small(17 + i * 8); }
        b.orb(64, 4, 0);
        b.block(70, 0, 5);
        b.pad(76, 1, 1);
        b.coin(82, 8, 1);

        b.portal(90, 1, Level.P_SHIP);
        b.corridor(92, 40, -1, 12);
        for (int i = 0; i < 7; i++) b.obj(1, 98 + i * 5, 2 + (i * 3) % 8);
        b.portal(132, 1, Level.P_CUBE);

        b.portal(140, 1, Level.P_WORM);
        for (int s = 0; s < 4; s++) {
            double gx = 146 + s * 26;
            b.layered(gx, 16, 2, 6, 1);
            b.obj(20, gx + 4, 8);
            b.obj(22, gx + 10, 1);
        }
        b.coin(200, 10, 2);
        b.portal(254, 1, Level.P_CUBE);

        b.orb(262, 4, 0);
        b.spike(264); b.spike(265);
        b.orb(272, 5, 1);
        b.block(280, 0, 6);
        for (int i = 0; i < 3; i++) b.spike(290 + i * 8);
        b.coin(312, 6, 3);
        b.text(318, 6, "PULSE");
        b.finish(326);
        return b.build();
    }

    /** 3. «Phase Shift» — паук, червяк и шар вперемешку. */
    public static Level phaseShift() {
        B b = new B("Phase Shift", 4, -3, "Hyperdrive");
        b.lv.settings.localId = "gd_phase_shift";
        b.lv.settings.bgColor = 0xFF3A0A4E;
        b.lv.settings.groundColor = 0xFF1B0426;
        b.lv.settings.bgId = 16; b.lv.settings.groundId = 7;
        b.lv.settings.startSpeed = 3;
        b.lv.settings.desc = "Фазовый сдвиг: паук прыгает через пустоту, червяк ныряет сквозь блок.";
        b.text(6, 7, "PHASE SHIFT");

        for (int i = 0; i < 7; i++) { b.spike(14 + i * 7); b.spike(15 + i * 7); }
        b.orb(66, 5, 1);
        b.coin(72, 8, 1);

        b.portal(80, 1, Level.P_SPIDER);
        b.corridor(82, 34, -1, 10);
        for (int i = 0; i < 7; i++) b.obj(20, 88 + i * 5, i % 2 == 0 ? 0 : 9);

        b.portal(118, 1, Level.P_WORM);
        for (int s = 0; s < 3; s++) {
            double gx = 124 + s * 22;
            b.bridge(gx, 14, 3, 3);
            b.obj(20, gx + 5, 6);
            b.obj(22, gx + 10, 2);
        }
        b.coin(160, 9, 2);

        b.portal(192, 1, Level.P_SPIDER);
        b.corridor(194, 30, -1, 9);
        for (int i = 0; i < 6; i++) b.saw(200 + i * 5, i % 2 == 0 ? 2 : 7);
        b.portal(224, 1, Level.P_BALL);
        b.corridor(226, 28, -1, 8);
        for (int i = 0; i < 6; i++) { b.obj(20, 232 + i * 4, 0); b.obj(22, 234 + i * 4, 7); }
        b.portal(254, 1, Level.P_CUBE);

        b.portal(262, 1, Level.P_S4);
        for (int i = 0; i < 6; i++) { b.spike(268 + i * 7); b.small(271 + i * 7); }
        b.orb(312, 4, 4);
        b.coin(318, 7, 3);
        b.text(324, 7, "SHIFT");
        b.finish(332);
        return b.build();
    }

    /** 4. «Core Overload» — скорость, волна, робот и слалом червяком. */
    public static Level coreOverload() {
        B b = new B("Core Overload", 6, -5, "Afterburner");
        b.lv.settings.localId = "gd_core_overload";
        b.lv.settings.bgColor = 0xFF521405;
        b.lv.settings.groundColor = 0xFF260903;
        b.lv.settings.bgId = 18; b.lv.settings.groundId = 9;
        b.lv.settings.startSpeed = 4;
        b.lv.settings.desc = "Перегрузка ядра: узкие коридоры и слалом сквозь парящие кубы.";
        b.text(6, 8, "CORE OVERLOAD");

        for (int i = 0; i < 8; i++) { b.spike(14 + i * 6); b.small(16 + i * 6); }
        b.orb(66, 4, 0);
        b.coin(72, 9, 1);

        b.portal(80, 1, Level.P_WAVE);
        b.portal(80, 4, Level.P_MINI);
        b.corridor(82, 40, -1, 8);
        for (int i = 0; i < 13; i++) {
            b.obj(1, 86 + i * 3, i % 2 == 0 ? 0 : 6);
            b.obj(1, 86 + i * 3, i % 2 == 0 ? 1 : 7);
        }
        b.portal(122, 1, Level.P_CUBE);
        b.portal(122, 4, Level.P_BIG);

        b.portal(130, 1, Level.P_ROBOT);
        for (int i = 0; i < 6; i++) {
            b.block(136 + i * 9, i % 3, 5);
            b.spike(142 + i * 9);
        }
        b.portal(192, 1, Level.P_CUBE);

        b.portal(200, 1, Level.P_WORM);
        for (int i = 0; i < 14; i++) {
            double gx = 206 + i * 8;
            int y = i % 2 == 0 ? 2 : 5;
            b.bridge(gx, 5, y, 2);
            b.obj(i % 2 == 0 ? 20 : 22, gx + 2, i % 2 == 0 ? y + 2 : y - 1);
        }
        b.coin(260, 9, 2);
        b.portal(320, 1, Level.P_CUBE);

        b.portal(328, 1, Level.P_S5);
        for (int i = 0; i < 8; i++) { b.spike(334 + i * 6); b.ceil(337 + i * 6, 7); }
        b.orb(386, 4, 4);
        b.coin(392, 7, 3);
        b.text(398, 8, "OVERLOAD");
        b.finish(406);
        return b.build();
    }

    // ---------------------------------------------------------- iSpy

    static final double[] SPEED = {0.807, 1.0, 1.243, 1.502, 1.849, 2.25, 2.70};
    static final double[] PLAN_T  = {0, 10, 20, 30, 40, 50, 60, 70, 80, 90};
    static final int[]    PLAN_SP = {1,  2,  3,  3,  4,  2,  4,  3,  5,  3};
    static final double TRACK_END = 99.6;

    /** Позиция в клетках к секунде трека. */
    static double gx(double t) {
        double x = 0;
        for (int i = 0; i < PLAN_T.length; i++) {
            double a = PLAN_T[i];
            double c = i + 1 < PLAN_T.length ? PLAN_T[i + 1] : TRACK_END;
            if (t <= a) break;
            x += SPEED[PLAN_SP[i]] * 311.58 * (Math.min(t, c) - a);
            if (t <= c) break;
        }
        return x / 30.0;
    }

    static int speedPortal(int tier) {
        switch (tier) {
            case 0: return Level.P_S05;
            case 1: return Level.P_S1;
            case 2: return Level.P_S2;
            case 3: return Level.P_S3;
            case 4: return Level.P_S4;
            case 5: return Level.P_S5;
            default: return Level.P_S6;
        }
    }

    static void trig(B b, double t, double gy, int type, double dur, double strength,
                     double shake, int color) {
        Level.Obj o = new Level.Obj();
        o.typeId = 100;
        o.x = Math.round(gx(t)) * 30;
        o.y = gy * 30;
        o.trig = new Level.Trig();
        o.trig.type = type;
        o.trig.duration = dur;
        o.trig.fxStrength = strength;
        if (shake > 0) { o.trig.shakeStrength = shake; o.trig.shakeInterval = 0.04; }
        if (color != 0) o.trig.color = color;
        b.lv.objects.add(o);
    }

    static void fx(B b, double t, int type, double strength, double dur) { trig(b, t, 9, type, dur, strength, 0, 0); }
    static void shake(B b, double t, double power, double dur) { trig(b, t, 10, Level.T_SHAKE, dur, 0.6, power, 0); }
    static void bg(B b, double t, int color) { trig(b, t, 11, Level.T_BG, 0.8, 0.6, 0, color); }

    static void tunnel(B b, double t0, double t1, int floorY, int ceilY) {
        int a = (int) Math.round(gx(t0)), c = (int) Math.round(gx(t1));
        for (int x = a; x <= c; x++) { b.obj(1, x, floorY); b.obj(1, x, ceilY); }
    }

    /**
     * 5. «iSpyWithMyLittleEye» — наш демон под трек BIKE (Newgrounds #1191782).
     * Геометрия и эффекты сделаны для GEO DASH, с чужого уровня ничего не скопировано.
     */
    public static Level iSpy() {
        B b = new B("iSpyWithMyLittleEye", 7, 1191782, "BIKE");
        b.lv.settings.localId = "gd_ispy";
        b.lv.settings.songArtist = "Tangermusic (Newgrounds #1191782)";
        b.lv.settings.bgColor = 0xFF101010;
        b.lv.settings.groundColor = 0xFF050505;
        b.lv.settings.bgId = 13; b.lv.settings.groundId = 6;
        b.lv.settings.eyesBackground = true;
        b.lv.settings.startSpeed = 1;
        b.lv.settings.desc = "Наш демон под BIKE. Глаза на фоне следят за тобой. Трек качается по ID 1191782.";

        b.text(gx(0.9), 8, "I SPY");
        for (double t = 2.0; t < 9.6; t += 1.2) b.spike(Math.round(gx(t)));
        b.coin(Math.round(gx(8.4)), 6, 1);

        b.portal(Math.round(gx(10)), 1, speedPortal(2));
        b.portal(Math.round(gx(10.2)), 1, Level.P_SPIDER);
        bg(b, 10, 0xFF1A1A1A);
        shake(b, 10, 8, 0.4);
        tunnel(b, 10.4, 19.4, -1, 10);
        for (double t = 11; t < 19; t += 0.9)
            b.obj(20, Math.round(gx(t)), ((int) (t * 2)) % 2 == 0 ? 0 : 9);

        b.portal(Math.round(gx(20)), 1, speedPortal(3));
        b.portal(Math.round(gx(20.2)), 1, Level.P_WORM);
        shake(b, 20, 20, 0.9);
        fx(b, 20, Level.T_FLASH, 0.9, 0.5);
        fx(b, 20.1, Level.T_CHROMATIC, 0.8, 1.4);
        bg(b, 20, 0xFF3A0606);
        for (int s = 0; s < 4; s++) {
            double x = Math.round(gx(21.2 + s * 2.1));
            b.bridge(x, 10, 3, 3);
            b.eye(x + 5, 7, 3, false);
            b.obj(20, x + 3, 6);
            b.obj(22, x + 7, 2);
        }
        b.coin(Math.round(gx(27.5)), 10, 2);

        b.portal(Math.round(gx(30)), 1, Level.P_WAVE);
        b.portal(Math.round(gx(30)), 4, Level.P_MINI);
        fx(b, 30, Level.T_SHOCK, 0.7, 0.8);
        tunnel(b, 30.3, 39.6, -1, 9);
        {
            int a = (int) Math.round(gx(30.6)), c = (int) Math.round(gx(39.4));
            int k = 0;
            for (int x = a; x < c; x += 5, k++) {
                int lo = k % 2 == 0 ? 0 : 6;
                b.obj(1, x, lo); b.obj(1, x, lo + 1);
            }
        }
        b.portal(Math.round(gx(39.8)), 1, Level.P_CUBE);
        b.portal(Math.round(gx(39.8)), 4, Level.P_BIG);

        b.portal(Math.round(gx(40)), 1, speedPortal(4));
        b.portal(Math.round(gx(40.2)), 1, Level.P_SHIP);
        shake(b, 40, 22, 1.0);
        fx(b, 40, Level.T_FLASH, 1.0, 0.45);
        fx(b, 40.1, Level.T_GLITCH, 0.75, 1.2);
        bg(b, 40, 0xFF5A0A0A);
        tunnel(b, 40.4, 49.6, -1, 14);
        b.eye(Math.round(gx(43)), 7, 4, true);
        b.eye(Math.round(gx(46)), 5, 3, true);
        b.eye(Math.round(gx(48.5)), 9, 3, true);
        b.coin(Math.round(gx(45.0)), 12, 3);

        b.portal(Math.round(gx(50)), 1, speedPortal(2));
        b.portal(Math.round(gx(50.2)), 1, Level.P_CUBE);
        bg(b, 50, 0xFF151515);
        fx(b, 50, Level.T_VIGNETTE, 0.5, 1.5);
        for (double t = 51; t < 59.4; t += 1.0) {
            double x = Math.round(gx(t));
            b.spike(x);
            if ((int) (t * 2) % 3 == 0) b.orb(x + 3, 4, 0);
        }

        b.portal(Math.round(gx(60)), 1, speedPortal(4));
        b.portal(Math.round(gx(60.2)), 1, Level.P_WORM);
        shake(b, 60, 24, 1.1);
        fx(b, 60, Level.T_FLASH, 1.0, 0.5);
        fx(b, 60.1, Level.T_CHROMATIC, 0.9, 1.6);
        bg(b, 60, 0xFF6E0808);
        for (int s = 0; s < 5; s++) {
            double x = Math.round(gx(60.8 + s * 1.7));
            b.layered(x, 12, 2, 6, 2);
            b.obj(20, x + 4, 8);
            b.obj(22, x + 8, 1);
        }

        b.portal(Math.round(gx(70)), 1, speedPortal(3));
        b.portal(Math.round(gx(70.2)), 1, Level.P_SHIP);
        tunnel(b, 70.4, 79.6, -1, 13);
        for (double t = 71; t < 79; t += 0.7)
            b.obj(1, Math.round(gx(t)), 2 + (int) ((t * 5) % 9));

        b.portal(Math.round(gx(80)), 1, speedPortal(5));
        b.portal(Math.round(gx(80.2)), 1, Level.P_WORM);
        shake(b, 80, 26, 1.2);
        fx(b, 80, Level.T_FLASH, 1.0, 0.5);
        fx(b, 80.1, Level.T_GLITCH, 0.85, 1.4);
        bg(b, 80, 0xFF8A0A0A);
        for (int s = 0; s < 6; s++) {
            double x = Math.round(gx(80.8 + s * 1.2));
            b.bridge(x, 8, 3, 2);
            b.obj(s % 2 == 0 ? 20 : 22, x + 3, s % 2 == 0 ? 5 : 2);
        }
        shake(b, 87.9, 30, 1.0);
        fx(b, 87.9, Level.T_SHOCK, 1.0, 1.0);
        b.portal(Math.round(gx(88.2)), 1, Level.P_CUBE);

        b.portal(Math.round(gx(90)), 1, speedPortal(3));
        bg(b, 90, 0xFF101010);
        fx(b, 90, Level.T_VIGNETTE, 0.6, 2.0);
        for (double t = 90.6; t < 97.6; t += 0.95) b.spike(Math.round(gx(t)));
        b.eye(Math.round(gx(98.0)), 6, 4, true);
        b.text(gx(98.6), 9, "I SEE YOU");
        b.finish(Math.round(gx(TRACK_END)) - 2);
        return b.build();
    }

    public static List<Level> all() {
        List<Level> l = new ArrayList<>();
        l.add(neonTunnel()); l.add(subterraneanPulse()); l.add(phaseShift());
        l.add(coreOverload()); l.add(iSpy());
        return l;
    }

}
