package com.winnigames2024.geodash;

import android.content.Context;
import android.graphics.*;
import android.view.View;

import java.io.File;
import java.util.List;
import java.util.UUID;

/** MY LEVELS — уровни редактора: листаем карточки, создаём, правим, заливаем. */
public class MyLevelsActivity extends GdActivity {

    List<Level> levels;
    int index;
    Card card;
    boolean copied;

    /** Открыть список: copies = true — скопированные уровни. */
    public static void open(android.app.Activity from, boolean copies) {
        android.content.Intent i = new android.content.Intent(from, MyLevelsActivity.class);
        i.putExtra("copied", copies);
        from.startActivity(i);
        from.overridePendingTransition(0, 0);
    }

    @Override protected int bgA() { return copied ? Color.rgb(150, 90, 235) : Color.rgb(235, 130, 20); }
    @Override protected int bgId() { return copied ? 11 : 5; }
    @Override protected int groundId() { return copied ? 2 : 4; }
    @Override protected String headTitle() { return copied ? "СКОПИРОВАННЫЕ" : "МОИ УРОВНИ"; }
    @Override protected String headSub() {
        return copied ? "копии уровней — правь и публикуй как свой" : "уровни редактора — assets/levels";
    }

    @Override protected void build() {
        copied = getIntent() != null && getIntent().getBooleanExtra("copied", false);
        levels = Store.myLevels(copied);

        card = new Card(this);
        root.addView(card, Ui.lp(W, H, 0.12f, 0.19f, 0.76f, 0.42f));

        btn("", Ui.I_BACK, Color.rgb(150, 150, 185), Color.rgb(60, 62, 100),
                0.02f, 0.32f, 0.07f, 0.13f, () -> move(-1));
        btn("", Ui.I_PLAY, Color.rgb(150, 150, 185), Color.rgb(60, 62, 100),
                0.91f, 0.32f, 0.07f, 0.13f, () -> move(1));

        float bw = 0.18f, gx = 0.015f, y1 = 0.64f, y2 = 0.78f;
        float x0 = (1 - (bw * 4 + gx * 3)) / 2f;

        btn("СОЗДАТЬ", Ui.I_PLUS, Color.rgb(120, 255, 170), Color.rgb(20, 150, 100),
                x0, y1, bw, 0.12f, this::create);
        btn("РЕДАКТОР", Ui.I_EDIT, Color.rgb(255, 180, 70), Color.rgb(175, 95, 10),
                x0 + (bw + gx), y1, bw, 0.12f, this::edit);
        btn("ИГРАТЬ", Ui.I_PLAY, Color.rgb(120, 220, 255), Color.rgb(20, 110, 180),
                x0 + (bw + gx) * 2, y1, bw, 0.12f, () -> play(false));
        btn("ТРЕНИРОВКА", Ui.I_CHECK, Color.rgb(160, 190, 255), Color.rgb(60, 80, 170),
                x0 + (bw + gx) * 3, y1, bw, 0.12f, () -> play(true));

        btn("НАСТРОЙКИ", Ui.I_SETTINGS, Color.rgb(170, 175, 210), Color.rgb(70, 76, 125),
                x0, y2, bw, 0.12f, this::settings);
        btn("КОПИЯ", Ui.I_COPY, Color.rgb(200, 170, 255), Color.rgb(100, 60, 180),
                x0 + (bw + gx), y2, bw, 0.12f, this::copy);
        btn("ОПУБЛИКОВАТЬ", Ui.I_UPLOAD, Color.rgb(255, 210, 90), Color.rgb(170, 120, 10),
                x0 + (bw + gx) * 2, y2, bw, 0.12f, this::upload);
        btn("УДАЛИТЬ", Ui.I_TRASH, Color.rgb(240, 120, 120), Color.rgb(150, 40, 50),
                x0 + (bw + gx) * 3, y2, bw, 0.12f, this::delete);

        back(0.02f, 0.86f);
    }

    @Override protected void onResume() {
        super.onResume();
        if (card != null) { levels = Store.myLevels(copied); card.invalidate(); }
    }

    Level cur() { return levels.isEmpty() ? null : levels.get(Math.min(index, levels.size() - 1)); }

    File fileOf(Level lv) { return new File(Store.levels(), Store.safe(lv.settings.localId) + ".gdl"); }

    void move(int d) {
        if (levels.isEmpty()) return;
        index = (index + d + levels.size()) % levels.size();
        card.invalidate();
    }

    void create() {
        Level lv = new Level();
        lv.settings.name = "Уровень " + (levels.size() + 1);
        lv.settings.author = Store.user();
        lv.settings.localId = UUID.randomUUID().toString().replace("-", "");
        lv.settings.isCopy = copied;
        Store.writeFile(fileOf(lv), lv.toJson());
        levels = Store.myLevels(copied);
        for (int i = 0; i < levels.size(); i++)
            if (levels.get(i).settings.localId.equals(lv.settings.localId)) index = i;
        card.invalidate();
        EditorCanvasActivity.edit(this, levels.get(index));
    }

    void edit() {
        Level lv = cur();
        if (lv == null) { toast("Сначала создай уровень"); return; }
        EditorCanvasActivity.edit(this, lv);
    }

    void play(boolean practice) {
        Level lv = cur();
        if (lv != null) GameActivity.play(this, lv, practice);
    }

    void settings() {
        Level lv = cur();
        if (lv == null) return;
        LevelSettingsDialog.show(this, lv, () -> {
            Store.writeFile(fileOf(lv), lv.toJson());
            card.invalidate();
        });
    }

    void copy() {
        Level lv = cur();
        if (lv == null) return;
        Level c = Level.parse(lv.toJson());
        c.settings.localId = UUID.randomUUID().toString().replace("-", "");
        c.settings.serverId = 0;
        c.settings.isCopy = true;
        c.settings.copiedFrom = lv.settings.serverId;
        c.settings.copiedAuthor = lv.settings.author;
        c.settings.version = 1;
        c.settings.password = "";
        c.settings.name = lv.settings.name + " (копия)";
        Store.writeFile(fileOf(c), c.toJson());
        levels = Store.myLevels(copied);
        card.invalidate();
        toast(copied ? "Копия создана" : "Копия создана — ищи её в «СКОПИРОВАННЫЕ»");
    }

    void delete() {
        Level lv = cur();
        if (lv == null) return;
        confirm("Удалить «" + lv.settings.name + "»?", () -> {
            fileOf(lv).delete();
            levels = Store.myLevels(copied);
            if (index >= levels.size()) index = Math.max(0, levels.size() - 1);
            card.invalidate();
        });
    }

    void upload() {
        Level lv = cur();
        if (lv == null) return;
        if (!Store.loggedIn()) { toast("Сначала войди в аккаунт"); return; }
        toast("Заливаю на сервер…");
        Api.async(() -> {
            int id = Api.uploadLevel(lv);
            ui.post(() -> {
                if (id > 0) {
                    lv.settings.serverId = id;
                    lv.settings.version++;
                    Store.writeFile(fileOf(lv), lv.toJson());
                    toast("Готово! ID #" + id);
                } else toast("Нет сети — уедет при появлении связи");
                card.invalidate();
            });
        });
    }

    class Card extends View {
        Card(Context c) { super(c); }

        @Override protected void onDraw(Canvas c) {
            RectF r = new RectF(0, 0, getWidth(), getHeight());
            Gd.plate(c, r, Color.rgb(52, 42, 86), Color.rgb(24, 20, 52), 24f);

            Level lv = cur();
            if (lv == null) {
                Gd.outlined(c, copied ? "Копий нет — скопируй уровень" : "Уровней нет — нажми «СОЗДАТЬ»", r.centerX(), r.centerY(),
                        r.height() * 0.12f, Gd.GOLD, Paint.Align.CENTER);
                return;
            }
            Level.Settings s = lv.settings;
            double[] pr = Store.progress(lv);

            float fs = r.height() * 0.42f;
            RectF face = new RectF(r.left + r.width() * 0.04f, r.top + r.height() * 0.2f,
                    r.left + r.width() * 0.04f + fs, r.top + r.height() * 0.2f + fs);
            Gd.face(c, face, s.difficulty);

            float x = face.right + r.width() * 0.05f;
            Gd.outlined(c, s.name, x, r.top + r.height() * 0.22f, r.height() * 0.15f, Gd.PAPER, Paint.Align.LEFT);
            String[] lines = {
                    "объектов: " + lv.objects.size() + "   длина: " + (int) (lv.rightEdge() / 30) + " клеток",
                    "режим: " + Gd.MODE_NAMES[s.startMode] + "   скорость: " + Gd.SPEED_NAMES[s.startSpeed],
                    "музыка: " + (s.songId == 0 ? "нет" : (s.songId < 0 ? "встроенная" : "#" + s.songId)),
                    "копирование: " + (s.copyMode == 0 ? "закрыто" : s.copyMode == 1 ? "по паролю" : "свободное"),
                    s.serverId > 0 ? "опубликован: #" + s.serverId : "ещё не опубликован",
                    s.isCopy ? "копия уровня #" + s.copiedFrom : "свой уровень",
                    String.format("лучший результат: %.0f%%", pr[0])
            };
            float y = r.top + r.height() * 0.36f;
            for (String l : lines) {
                Gd.outlined(c, l, x, y, r.height() * 0.075f, Color.argb(228, 210, 225, 255), Paint.Align.LEFT);
                y += r.height() * 0.1f;
            }

            Gd.outlined(c, (index + 1) + " / " + levels.size(), r.centerX(), r.bottom - r.height() * 0.04f,
                    r.height() * 0.08f, Gd.NEON, Paint.Align.CENTER);
        }
    }
}
