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

        Level build() { lv.settings.lengthUnits = lv.rightEdge(); return lv; }
    }

    public static Level easy() {
        B b = new B("Neon Start", 2, -1, "Neon Start");
        b.lv.settings.bgColor = 0xFF2A2060;
        b.lv.settings.groundColor = 0xFF151038;
        b.lv.settings.startSpeed = 1;
        b.text(6, 4, "GEO DASH");

        for (int i = 0; i < 5; i++) b.spike(16 + i * 7);
        b.block(56, 0, 4); b.block(62, 1, 4); b.block(68, 2, 4);
        b.spike(60); b.spike(66, 1);
        b.coin(70, 5, 1);

        b.orb(80, 3, 0);
        b.spike(82); b.spike(83);
        b.orb(88, 4, 0);
        b.block(92, 0, 6);
        b.pad(98, 1, 0);
        b.spike(102); b.spike(103);

        b.portal(110, 1, Level.P_SHIP);
        b.corridor(112, 40, -1, 9);
        for (int i = 0; i < 6; i++) b.obj(1, 118 + i * 6, 2 + (i % 2) * 3);
        b.coin(134, 7, 2);
        b.portal(154, 1, Level.P_CUBE);

        b.portal(160, 1, Level.P_S2);
        for (int i = 0; i < 4; i++) { b.spike(166 + i * 9); b.saw(170 + i * 9, 3); }
        b.block(204, 0, 8);
        b.orb(210, 4, 1);

        b.portal(216, 1, Level.P_BALL);
        for (int i = 0; i < 14; i++) b.obj(1, 218 + i, 6);
        b.spike(224); b.ceil(228, 5);
        b.coin(236, 3, 3);
        b.portal(236, 1, Level.P_CUBE);

        b.portal(242, 1, Level.P_S1);
        for (int i = 0; i < 3; i++) b.spike(248 + i * 8);
        b.text(268, 5, "GG!");
        b.finish(278);
        return b.build();
    }

    public static Level hard() {
        B b = new B("Static Rush", 4, -2, "Static Rush");
        b.lv.settings.bgColor = 0xFF35105A;
        b.lv.settings.groundColor = 0xFF1B0836;
        b.lv.settings.startSpeed = 2;

        for (int i = 0; i < 6; i++) { b.spike(14 + i * 10); b.spike(15 + i * 10); }
        b.block(80, 1, 3); b.block(88, 2, 3); b.block(96, 3, 3);
        b.spike(84); b.spike(92, 1);
        b.coin(98, 7, 1);

        b.portal(106, 1, Level.P_MINI);
        for (int i = 0; i < 5; i++) { b.spike(112 + i * 6); b.small(114 + i * 6); }
        b.portal(144, 1, Level.P_BIG);

        b.portal(150, 1, Level.P_UFO);
        b.corridor(152, 46, -1, 11);
        for (int i = 0; i < 9; i++) b.obj(1, 158 + i * 5, 1 + (i * 3) % 7);
        b.coin(180, 9, 2);
        b.portal(200, 1, Level.P_CUBE);

        b.portal(208, 1, Level.P_WAVE);
        b.portal(208, 4, Level.P_S3);
        b.corridor(210, 44, -1, 10);
        for (int i = 0; i < 10; i++) {
            b.obj(1, 216 + i * 4, i % 2 == 0 ? 0 : 8);
            b.obj(1, 216 + i * 4, i % 2 == 0 ? 1 : 9);
        }
        b.portal(256, 1, Level.P_CUBE);
        b.portal(256, 4, Level.P_S2);

        b.portal(264, 1, Level.P_SPIDER);
        b.corridor(266, 40, -1, 9);
        for (int i = 0; i < 8; i++) b.obj(20, 272 + i * 5, (i % 2 == 0) ? 0 : 8);
        b.coin(294, 4, 3);
        b.portal(308, 1, Level.P_CUBE);

        b.portal(316, 1, Level.P_ROBOT);
        b.block(322, 0, 5); b.block(330, 2, 5); b.block(340, 4, 5);
        b.spike(328); b.spike(338, 2);
        b.orb(348, 7, 4);
        for (int i = 0; i < 4; i++) b.spike(356 + i * 7);
        b.finish(392);
        return b.build();
    }

    public static Level insane() {
        B b = new B("Hyperdrive", 6, -3, "Hyperdrive");
        b.lv.settings.bgColor = 0xFF4A0A2E;
        b.lv.settings.groundColor = 0xFF220518;
        b.lv.settings.startSpeed = 3;

        for (int i = 0; i < 8; i++) { b.spike(12 + i * 8); b.spike(13 + i * 8); b.small(16 + i * 8); }
        b.orb(78, 3, 0); b.spike(80); b.spike(81); b.spike(82);
        b.orb(86, 5, 1);
        b.coin(92, 8, 1);

        b.portal(96, 1, Level.P_S5);
        b.portal(100, 1, Level.P_SHIP);
        b.corridor(102, 56, -1, 10);
        for (int i = 0; i < 12; i++) {
            b.obj(23, 108 + i * 4, 2 + (i * 5) % 6);
            if (i % 3 == 0) b.obj(1, 110 + i * 4, 0);
        }
        b.portal(160, 1, Level.P_CUBE);

        b.portal(168, 1, Level.P_WORM);
        b.text(168, 9, "WORM");
        for (int i = 0; i < 60; i++) { b.obj(1, 172 + i, 0); b.obj(1, 172 + i, 7); }
        for (int i = 0; i < 24; i++) b.obj(1, 176 + i * 2, 1);
        for (int i = 0; i < 10; i++) b.obj(20, 180 + i * 5, 2);
        for (int i = 0; i < 8; i++) b.obj(22, 184 + i * 6, 5);
        b.coin(210, 4, 2);
        b.portal(234, 1, Level.P_CUBE);

        b.portal(242, 1, Level.P_DUAL);
        b.portal(246, 1, Level.P_S6);
        b.corridor(248, 40, -1, 14);
        for (int i = 0; i < 10; i++) { b.obj(20, 252 + i * 4, 0); b.obj(22, 254 + i * 4, 13); }
        b.portal(290, 1, Level.P_SINGLE);
        b.portal(290, 4, Level.P_S4);

        b.portal(296, 1, Level.P_WAVE);
        b.portal(296, 4, Level.P_MINI);
        b.corridor(298, 48, -1, 8);
        for (int i = 0; i < 16; i++) {
            b.obj(1, 302 + i * 3, i % 2 == 0 ? 0 : 6);
            b.obj(1, 302 + i * 3, i % 2 == 0 ? 1 : 7);
        }
        b.coin(330, 3, 3);
        b.portal(348, 1, Level.P_CUBE);
        b.portal(348, 4, Level.P_BIG);

        b.portal(354, 1, Level.P_S5);
        for (int i = 0; i < 7; i++) { b.spike(360 + i * 6); b.spike(361 + i * 6); }
        b.orb(404, 3, 4);
        b.text(412, 6, "HYPERDRIVE");
        b.finish(424);
        return b.build();
    }


    /** «Oko» — наш демон про глаза: кольца-зрачки и постоянная смена режимов. */
    public static Level oko() {
        B b = new B("Oko", 9, -4, "Violet Sky");
        b.lv.settings.bgColor = 0xFF101A52;
        b.lv.settings.groundColor = 0xFF070B28;
        b.lv.settings.startSpeed = 2;
        b.lv.settings.desc = "Смотри в оба: зрачки следят за тобой.";
        b.text(6, 6, "OKO");

        for (int i = 0; i < 6; i++) { b.spike(16 + i * 9); b.small(18 + i * 9); }
        b.block(72, 0, 4); b.block(78, 2, 4); b.block(84, 4, 4);
        b.spike(76); b.spike(82, 2);
        b.coin(88, 8, 1);

        b.portal(96, 1, Level.P_SHIP);
        b.corridor(98, 52, -1, 13);
        eye(b, 112, 6, 3); eye(b, 132, 8, 3); eye(b, 152, 5, 3);
        b.coin(142, 11, 2);
        b.portal(152, 1, Level.P_CUBE);

        b.portal(160, 1, Level.P_WAVE);
        b.portal(160, 4, Level.P_MINI);
        b.corridor(162, 46, -1, 9);
        for (int i = 0; i < 14; i++) {
            b.obj(1, 166 + i * 3, i % 2 == 0 ? 0 : 7);
            b.obj(1, 166 + i * 3, i % 2 == 0 ? 1 : 8);
        }
        b.portal(208, 1, Level.P_CUBE);
        b.portal(208, 4, Level.P_BIG);

        b.portal(216, 1, Level.P_SPIDER);
        b.corridor(218, 44, -1, 10);
        for (int i = 0; i < 9; i++) b.obj(20, 224 + i * 5, i % 2 == 0 ? 0 : 9);
        for (int i = 0; i < 4; i++) b.saw(230 + i * 11, 5);
        b.coin(252, 5, 3);
        b.portal(262, 1, Level.P_CUBE);

        b.portal(270, 1, Level.P_UFO);
        b.corridor(272, 42, -1, 12);
        for (int i = 0; i < 10; i++) {
            for (int k = 0; k < 3; k++) b.obj(1, 278 + i * 4, 1 + k);
            for (int k = 0; k < 3; k++) b.obj(1, 280 + i * 4, 8 + k);
        }
        b.portal(314, 1, Level.P_CUBE);

        b.portal(322, 1, Level.P_S3);
        b.orb(330, 4, 0);
        b.spike(332); b.spike(333);
        b.orb(340, 5, 1);
        b.orb(348, 6, 4);
        for (int i = 0; i < 6; i++) { b.spike(356 + i * 7); b.ceil(359 + i * 7, 6); }
        b.text(400, 7, "OKO");
        b.finish(406);
        return b.build();
    }

    /** «Eye Spy» — большой демон про глаза. Уровень наш собственный. */
    public static Level eyeSpy() {
        B b = new B("Eye Spy", 10, -5, "Afterburner");
        b.lv.settings.bgColor = 0xFF0B1038;
        b.lv.settings.groundColor = 0xFF050722;
        b.lv.settings.startSpeed = 3;
        b.lv.settings.desc = "Глаза повсюду. Смотри в оба и не моргай.";
        b.text(6, 7, "EYE SPY");

        for (int i = 0; i < 7; i++) { b.spike(14 + i * 8); b.spike(15 + i * 8); b.small(18 + i * 8); }
        b.orb(72, 4, 0);
        b.spike(74); b.spike(75); b.spike(76);
        b.orb(82, 5, 1);
        b.block(88, 0, 6);
        b.coin(94, 8, 1);
        b.pad(96, 1, 1);
        for (int i = 0; i < 4; i++) b.spike(100 + i * 7);

        b.portal(132, 1, Level.P_SHIP);
        b.corridor(134, 60, -1, 14);
        eye(b, 146, 7, 4); eye(b, 168, 5, 3); eye(b, 188, 9, 3);
        for (int i = 0; i < 6; i++) b.saw(152 + i * 8, 1 + (i * 3) % 10);
        b.portal(194, 1, Level.P_CUBE);

        b.portal(202, 1, Level.P_WAVE);
        b.portal(202, 4, Level.P_MINI);
        b.portal(202, 7, Level.P_S5);
        b.corridor(204, 54, -1, 8);
        for (int i = 0; i < 17; i++) {
            b.obj(1, 208 + i * 3, i % 2 == 0 ? 0 : 6);
            b.obj(1, 208 + i * 3, i % 2 == 0 ? 1 : 7);
        }
        b.coin(232, 4, 2);
        b.portal(258, 1, Level.P_CUBE);
        b.portal(258, 4, Level.P_BIG);
        b.portal(258, 7, Level.P_S3);

        b.portal(266, 1, Level.P_UFO);
        b.corridor(268, 48, -1, 13);
        for (int i = 0; i < 11; i++) {
            for (int k = 0; k < 4; k++) b.obj(1, 274 + i * 4, 1 + k);
            for (int k = 0; k < 4; k++) b.obj(1, 276 + i * 4, 8 + k);
        }
        eye(b, 300, 6, 2);
        b.portal(316, 1, Level.P_CUBE);

        b.portal(324, 1, Level.P_DUAL);
        b.corridor(326, 46, -1, 15);
        for (int i = 0; i < 11; i++) {
            b.obj(20, 330 + i * 4, 0);
            b.obj(22, 332 + i * 4, 14);
        }
        b.coin(352, 7, 3);
        b.portal(372, 1, Level.P_SINGLE);

        b.portal(380, 1, Level.P_SPIDER);
        b.corridor(382, 42, -1, 10);
        for (int i = 0; i < 9; i++) b.obj(20, 388 + i * 5, i % 2 == 0 ? 0 : 9);
        for (int i = 0; i < 4; i++) b.saw(392 + i * 10, 5);
        b.portal(424, 1, Level.P_CUBE);

        b.portal(432, 1, Level.P_ROBOT);
        b.block(438, 0, 5); b.block(446, 2, 5); b.block(456, 4, 5);
        b.spike(444); b.spike(454, 2);
        b.orb(466, 7, 4);
        b.portal(472, 1, Level.P_CUBE);
        b.portal(472, 4, Level.P_S6);
        for (int i = 0; i < 8; i++) { b.spike(478 + i * 6); b.ceil(481 + i * 6, 7); }
        eye(b, 532, 6, 4);
        b.text(544, 8, "I SEE YOU");
        b.finish(552);
        return b.build();
    }

    /** Глаз: кольцо из блоков, пила вместо зрачка и пара шипов-ресниц. */
    static void eye(B b, double gx, int cy, int rad) {
        for (int a = 0; a < 20; a++) {
            double ang = a * Math.PI * 2 / 20;
            b.obj(1, Math.round(gx + Math.cos(ang) * rad), Math.round(cy + Math.sin(ang) * rad));
        }
        b.saw(gx, cy);
        b.obj(20, gx - 1, cy + rad + 1);
        b.obj(20, gx + 1, cy + rad + 1);
    }


    // ---------------------------------------------------------- Night Ride

    /** Множители скорости по тирам — как в игре. */
    static final double[] SPEED = {0.807, 1.0, 1.243, 1.502, 1.849, 2.25, 2.70};
    /** Разметка трека BIKE: с какой секунды какой тир скорости. */
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
                     double shake, int color, int color2) {
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
        if (color2 != 0) o.trig.color2 = color2;
        b.lv.objects.add(o);
    }

    static void fx(B b, double t, int type, double strength, double dur) {
        trig(b, t, 9, type, dur, strength, 0, 0, 0);
    }

    static void shake(B b, double t, double power, double dur) {
        trig(b, t, 10, Level.T_SHAKE, dur, 0.6, power, 0, 0);
    }

    static void bg(B b, double t, int color) {
        trig(b, t, 11, Level.T_BG, 0.5, 0.6, 0, color, 0);
    }

    static void grad(B b, double t, int top, int bottom, double dur) {
        trig(b, t, 12, Level.T_GRADIENT, dur, 0.75, 0, top, bottom);
    }

    static void tunnel(B b, double t0, double t1, int floorY, int ceilY) {
        int a = (int) Math.round(gx(t0)), c = (int) Math.round(gx(t1));
        for (int x = a; x <= c; x++) { b.obj(1, x, floorY); b.obj(1, x, ceilY); }
    }

    /**
     * «Night Ride» — демон под трек BIKE с Newgrounds (#1191782).
     * Скорости, режимы и эффекты стоят там, где в треке подъёмы и сбросы.
     * Сам трек не вшит в игру — качается по ID, как любая музыка с сайта.
     */
    public static Level bike() {
        B b = new B("Night Ride", 10, 1191782, "BIKE");
        b.lv.settings.songArtist = "Newgrounds #1191782";
        b.lv.settings.bgColor = 0xFF0A0D2E;
        b.lv.settings.groundColor = 0xFF05061A;
        b.lv.settings.bgId = 13;
        b.lv.settings.groundId = 6;
        b.lv.settings.startSpeed = 1;
        b.lv.settings.desc = "Ночная поездка под BIKE. Трек тянется с сайта по ID 1191782.";

        b.text(gx(0.9), 8, "NIGHT RIDE");
        grad(b, 0.2, 0x8820306A, 0x00000000, 2.0);
        for (double t = 2.0; t < 9.6; t += 1.15) {
            b.spike(Math.round(gx(t)));
            if (t > 5) b.small(Math.round(gx(t)) + 2);
        }
        b.coin(Math.round(gx(8.4)), 6, 1);
        fx(b, 9.4, Level.T_VIGNETTE, 0.5, 1.2);

        b.portal(Math.round(gx(10)), 1, speedPortal(2));
        bg(b, 10, 0xFF14215E);
        shake(b, 10, 9, 0.5);
        for (double t = 10.6; t < 19.4; t += 0.95) {
            double x = Math.round(gx(t));
            b.block(x, 0, 3);
            b.spike(x + 4);
            if ((int) (t * 10) % 3 == 0) b.orb(x + 6, 4, 0);
        }
        fx(b, 18.0, Level.T_PIXELATE, 0.35, 1.6);

        b.portal(Math.round(gx(20)), 1, speedPortal(3));
        b.portal(Math.round(gx(20)) + 1, 1, Level.P_SHIP);
        shake(b, 20, 20, 0.9);
        fx(b, 20, Level.T_FLASH, 0.9, 0.5);
        fx(b, 20.1, Level.T_CHROMATIC, 0.8, 1.4);
        bg(b, 20, 0xFF2A1060);
        grad(b, 20, 0x993C1CFF, 0x11000000, 9.0);
        tunnel(b, 20.3, 29.6, -1, 13);
        for (double t = 21; t < 29.4; t += 0.62) {
            double x = Math.round(gx(t));
            int y = 2 + (int) ((t * 7) % 9);
            b.obj(1, x, y);
            if ((int) (t * 3) % 4 == 0) b.saw(x + 2, y + 2);
        }
        b.coin(Math.round(gx(26.5)), 11, 2);

        b.portal(Math.round(gx(30)), 1, Level.P_CUBE);
        fx(b, 30, Level.T_SHOCK, 0.7, 0.8);
        for (double t = 30.4; t < 33.4; t += 0.7) {
            double x = Math.round(gx(t));
            b.spike(x); b.spike(x + 1);
            b.orb(x + 4, 4, 1);
        }
        double[] hits = {33.7, 35.7, 38.0};
        for (double hit : hits) {
            fx(b, hit, Level.T_FLASH, 0.65, 0.3);
            shake(b, hit, 14, 0.4);
            double x = Math.round(gx(hit));
            b.pad(x, 1, 0);
            b.spike(x + 3); b.spike(x + 4); b.spike(x + 5);
        }
        b.portal(Math.round(gx(36.2)), 1, Level.P_BALL);
        tunnel(b, 36.4, 39.6, -1, 8);
        for (double t = 36.8; t < 39.4; t += 0.5) {
            b.obj(20, Math.round(gx(t)), 0);
            b.obj(22, Math.round(gx(t)) + 2, 7);
        }
        b.portal(Math.round(gx(39.7)), 1, Level.P_CUBE);

        b.portal(Math.round(gx(40)), 1, speedPortal(4));
        b.portal(Math.round(gx(40)) + 1, 1, Level.P_WAVE);
        b.portal(Math.round(gx(40)), 4, Level.P_MINI);
        shake(b, 40, 22, 1.0);
        fx(b, 40, Level.T_FLASH, 1.0, 0.45);
        fx(b, 40.1, Level.T_GLITCH, 0.75, 1.2);
        bg(b, 40, 0xFF5A1038);
        grad(b, 40, 0x99FF2E6E, 0x11000000, 9.5);
        tunnel(b, 40.4, 49.6, -1, 9);
        {
            int a = (int) Math.round(gx(40.6)), c = (int) Math.round(gx(49.4));
            int k = 0;
            for (int x = a; x < c; x += 5, k++) {
                int lo = k % 2 == 0 ? 0 : 6;
                b.obj(1, x, lo); b.obj(1, x, lo + 1);
            }
        }
        b.coin(Math.round(gx(45.0)), 4, 3);

        b.portal(Math.round(gx(50)), 1, speedPortal(2));
        b.portal(Math.round(gx(50)) + 1, 1, Level.P_CUBE);
        b.portal(Math.round(gx(50)), 4, Level.P_BIG);
        b.portal(Math.round(gx(50.6)), 1, Level.P_UFO);
        bg(b, 50, 0xFF0D2A4E);
        grad(b, 50, 0x6616C8FF, 0x00000000, 9.0);
        fx(b, 50, Level.T_BLUR, 0.4, 1.5);
        tunnel(b, 50.8, 59.4, -1, 12);
        for (double t = 51.4; t < 59.2; t += 0.78) {
            double x = Math.round(gx(t));
            for (int k = 0; k < 3; k++) b.obj(1, x, 1 + k);
            for (int k = 0; k < 3; k++) b.obj(1, x + 3, 8 + k);
        }

        b.portal(Math.round(gx(60)), 1, speedPortal(4));
        b.portal(Math.round(gx(60)) + 1, 1, Level.P_CUBE);
        b.portal(Math.round(gx(60)), 4, Level.P_DUAL);
        shake(b, 60, 24, 1.1);
        fx(b, 60, Level.T_FLASH, 1.0, 0.5);
        fx(b, 60.1, Level.T_CHROMATIC, 0.9, 1.6);
        fx(b, 60.1, Level.T_SHOCK, 0.9, 0.9);
        bg(b, 60, 0xFF3A0A52);
        grad(b, 60, 0xAA9B2BFF, 0x11000000, 9.5);
        tunnel(b, 60.4, 69.6, -1, 15);
        for (double t = 60.8; t < 69.4; t += 0.55) {
            double x = Math.round(gx(t));
            b.obj(20, x, 0);
            b.obj(22, x + 2, 14);
            if ((int) (t * 2) % 5 == 0) b.orb(x + 1, 7, 2);
        }

        b.portal(Math.round(gx(70)), 1, speedPortal(3));
        b.portal(Math.round(gx(70)) + 1, 1, Level.P_SINGLE);
        b.portal(Math.round(gx(70.4)), 1, Level.P_SPIDER);
        fx(b, 70, Level.T_PIXELATE, 0.4, 1.4);
        bg(b, 70, 0xFF102A22);
        tunnel(b, 70.6, 79.6, -1, 10);
        for (double t = 71.2; t < 79.2; t += 0.62) {
            double x = Math.round(gx(t));
            b.obj(20, x, ((int) (t * 2)) % 2 == 0 ? 0 : 9);
            if ((int) (t * 4) % 7 == 0) b.saw(x + 2, 5);
        }

        b.portal(Math.round(gx(80)), 1, speedPortal(5));
        b.portal(Math.round(gx(80)) + 1, 1, Level.P_CUBE);
        shake(b, 80, 26, 1.2);
        fx(b, 80, Level.T_FLASH, 1.0, 0.5);
        fx(b, 80.1, Level.T_GLITCH, 0.85, 1.4);
        bg(b, 80, 0xFF5E1A08);
        grad(b, 80, 0xAAFF7A1E, 0x11000000, 9.5);
        b.portal(Math.round(gx(80.6)), 1, Level.P_ROBOT);
        for (double t = 81; t < 87.6; t += 0.8) {
            double x = Math.round(gx(t));
            b.block(x, (int) ((t * 3) % 5), 4);
            b.spike(x + 6);
        }
        shake(b, 87.9, 30, 1.0);
        fx(b, 87.9, Level.T_FLASH, 1.0, 0.4);
        fx(b, 87.9, Level.T_SHOCK, 1.0, 1.0);
        b.portal(Math.round(gx(88.0)), 1, Level.P_CUBE);
        for (double t = 88.3; t < 89.6; t += 0.42) {
            double x = Math.round(gx(t));
            b.spike(x); b.ceil(x + 2, 7);
        }

        b.portal(Math.round(gx(90)), 1, speedPortal(3));
        bg(b, 90, 0xFF0A0D2E);
        grad(b, 90, 0x6616C8FF, 0x00000000, 9.0);
        fx(b, 90, Level.T_VIGNETTE, 0.6, 2.0);
        for (double t = 90.6; t < 97.6; t += 0.9) {
            double x = Math.round(gx(t));
            b.spike(x);
            if ((int) (t * 2) % 3 == 0) b.orb(x + 3, 4, 0);
        }
        b.text(gx(98.2), 7, "RIDE ON");
        b.finish(Math.round(gx(TRACK_END)) - 2);
        return b.build();
    }

    public static List<Level> all() {
        List<Level> l = new ArrayList<>();
        l.add(easy()); l.add(hard()); l.add(insane());
        l.add(oko()); l.add(eyeSpy()); l.add(bike());
        return l;
    }
}
