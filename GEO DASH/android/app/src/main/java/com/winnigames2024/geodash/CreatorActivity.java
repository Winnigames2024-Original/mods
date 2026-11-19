package com.winnigames2024.geodash;

import android.graphics.Color;

/** CREATOR — хаб: Saved, Search, Daily, Weekly, Featured, Epic, Users, Top, My Levels. */
public class CreatorActivity extends GdActivity {

    @Override protected int bgA() { return Color.rgb(22, 36, 86); }
    @Override protected String headTitle() { return "CREATOR"; }
    @Override protected String headSub() {
        return Api.online ? "сервер онлайн — поиск и игроки доступны" : "офлайн — работают SAVED и мои уровни";
    }

    @Override protected void build() {
        float bw = 0.21f, bh = 0.17f, gx = 0.025f, gy = 0.03f;
        float x0 = (1 - (bw * 4 + gx * 3)) / 2f, y0 = 0.22f;

        cell(0, 0, x0, y0, bw, bh, gx, gy, "SAVED", "офлайн-уровни", Ui.I_DOWNLOAD,
                Color.rgb(120, 230, 160), Color.rgb(30, 130, 80),
                () -> go(LevelListActivity.class, "mode", "saved"));
        cell(1, 0, x0, y0, bw, bh, gx, gy, "SEARCH", "по имени и ID", Ui.I_SEARCH,
                Color.rgb(90, 180, 255), Color.rgb(20, 80, 180),
                () -> go(SearchActivity.class, "mode", "levels"));
        cell(2, 0, x0, y0, bw, bh, gx, gy, "DAILY", "уровень дня", Ui.I_DAILY,
                Color.rgb(150, 200, 255), Color.rgb(40, 90, 190),
                () -> go(DailyActivity.class));
        cell(3, 0, x0, y0, bw, bh, gx, gy, "WEEKLY", "уровень недели", Ui.I_WEEKLY,
                Color.rgb(210, 140, 255), Color.rgb(110, 40, 180),
                () -> go(DailyActivity.class, "tab", "weekly"));

        cell(0, 1, x0, y0, bw, bh, gx, gy, "FEATURED", "отобранные", Ui.I_STAR,
                Color.rgb(255, 210, 90), Color.rgb(170, 110, 10),
                () -> go(LevelListActivity.class, "mode", "featured"));
        cell(1, 1, x0, y0, bw, bh, gx, gy, "EPIC", "лучшее", Ui.I_ADMIN,
                Color.rgb(255, 130, 230), Color.rgb(160, 40, 140),
                () -> go(LevelListActivity.class, "mode", "epic"));
        cell(2, 1, x0, y0, bw, bh, gx, gy, "USERS", "поиск игроков", Ui.I_FRIENDS,
                Color.rgb(255, 150, 200), Color.rgb(160, 50, 110),
                () -> go(SearchActivity.class, "mode", "users"));
        cell(3, 1, x0, y0, bw, bh, gx, gy, "TOP", "таблица лидеров", Ui.I_PROFILE,
                Color.rgb(160, 180, 230), Color.rgb(60, 80, 150),
                () -> go(SearchActivity.class, "mode", "top"));

        cell(0, 2, x0, y0, bw, bh, gx, gy, "MY LEVELS", "редактор", Ui.I_EDIT,
                Color.rgb(255, 170, 60), Color.rgb(180, 90, 10),
                () -> go(EditorActivity.class));
        cell(1, 2, x0, y0, bw, bh, gx, gy, "ONLINE", "все уровни", Ui.I_ONLINE,
                Color.rgb(120, 220, 255), Color.rgb(20, 110, 180),
                () -> go(LevelListActivity.class, "mode", "online"));
        cell(2, 2, x0, y0, bw, bh, gx, gy, "FRIENDS", "друзья", Ui.I_FRIENDS,
                Color.rgb(255, 170, 210), Color.rgb(150, 50, 110),
                () -> go(FriendsActivity.class));
        cell(3, 2, x0, y0, bw, bh, gx, gy, "ACCOUNT", "профиль", Ui.I_PROFILE,
                Color.rgb(120, 190, 255), Color.rgb(30, 90, 190),
                () -> go(ProfileActivity.class));

        back(0.02f, 0.86f);
    }

    void cell(int col, int row, float x0, float y0, float bw, float bh, float gx, float gy,
              String t, String sub, int icon, int c1, int c2, Runnable r) {
        btn(t, sub, icon, c1, c2, x0 + col * (bw + gx), y0 + row * (bh + gy), bw, bh, r);
    }
}
