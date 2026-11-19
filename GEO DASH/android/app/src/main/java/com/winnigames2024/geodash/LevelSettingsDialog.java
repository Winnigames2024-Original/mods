package com.winnigames2024.geodash;

import android.app.Activity;
import android.graphics.Color;
import android.graphics.Typeface;
import android.text.InputType;
import android.view.Gravity;
import android.view.ViewGroup;
import android.widget.*;

/** Настройки уровня — свой диалог: поля и переключатели ◀ ▶, без системных виджетов. */
public class LevelSettingsDialog {

    /** Переключатель значения стрелками. */
    static class Picker {
        int value;
        final String[] options;
        final TextView label;

        Picker(String[] options, int value, TextView label) {
            this.options = options; this.value = value; this.label = label;
            update();
        }

        void move(int d) {
            value = (value + d + options.length) % options.length;
            update();
        }

        void update() { label.setText(options[Math.max(0, Math.min(options.length - 1, value))]); }
    }

    public static void show(Activity a, Level lv, Runnable onSaved) {
        Level.Settings s = lv.settings;
        final EditText[] f = new EditText[6];
        final Picker[] p = new Picker[4];
        final boolean[] flags = {s.startMini, s.startDual, s.startMirror, s.startGravityUp,
                s.twoPlayer, s.platformerFreeMove, s.multiplayer};

        LinearLayout box = GdDialog.custom(a, "НАСТРОЙКИ УРОВНЯ", "СОХРАНИТЬ", () -> {
            s.name = f[0].getText().toString().trim();
            s.desc = f[1].getText().toString();
            try { s.songId = Integer.parseInt(f[2].getText().toString().trim()); } catch (Exception ignored) { }
            s.songName = f[3].getText().toString().trim();
            s.copyPassword = f[4].getText().toString().trim();
            s.difficulty = p[0].value;
            s.startMode = p[1].value;
            s.startSpeed = p[2].value;
            s.copyMode = p[3].value;
            s.startMini = flags[0];
            s.startDual = flags[1];
            s.startMirror = flags[2];
            s.startGravityUp = flags[3];
            s.twoPlayer = flags[4];
            s.platformerFreeMove = flags[5];
            s.multiplayer = flags[6];
            try { s.maxPlayers = Math.max(2, Math.min(50,
                    Integer.parseInt(f[5].getText().toString().trim()))); } catch (Exception ignored) { }
            if (s.songId != 0) Api.async(() -> Music.ensureSong(s.songId));
            if (onSaved != null) onSaved.run();
        });

        f[0] = field(a, box, "название", s.name, false);
        f[1] = field(a, box, "описание", s.desc, false);

        p[0] = picker(a, box, "Сложность", Gd.DIFFS, s.difficulty);
        p[1] = picker(a, box, "Режим на старте", Gd.MODE_NAMES, s.startMode);
        p[2] = picker(a, box, "Скорость на старте", Gd.SPEED_NAMES, s.startSpeed);

        toggle(a, box, "мини-размер", flags, 0);
        toggle(a, box, "дуал", flags, 1);
        toggle(a, box, "зеркально", flags, 2);
        toggle(a, box, "гравитация вверх", flags, 3);
        toggle(a, box, "на двоих", flags, 4);
        toggle(a, box, "платформер (ходишь сам)", flags, 5);
        toggle(a, box, "тип уровня: мультиплеер", flags, 6);
        f[5] = field(a, box, "максимум игроков в карте", String.valueOf(Math.max(2, s.maxPlayers)), true);

        f[2] = field(a, box, "ID трека (минус — встроенный)", String.valueOf(s.songId), true);
        f[3] = field(a, box, "название трека", s.songName, false);

        // выбор трека, залитого на сайт
        final EditText songField = f[2], songName = f[3];
        Ui.GdBtn pickSong = new Ui.GdBtn(a).set("ТРЕК С САЙТА", Ui.I_MUSIC,
                Color.rgb(255, 150, 215), Color.rgb(160, 40, 120));
        pickSong.setOnClickListener(v -> Api.async(() -> {
            org.json.JSONArray list = Api.songs();
            if (list == null || list.length() == 0) {
                a.runOnUiThread(() -> GdDialog.info(a, "МУЗЫКА",
                        "На сайте пока нет треков. Залей свой на странице «Музыка»."));
                return;
            }
            final String[] names = new String[list.length()];
            final int[] ids = new int[list.length()];
            for (int i = 0; i < list.length(); i++) {
                org.json.JSONObject o = list.optJSONObject(i);
                ids[i] = o == null ? 0 : o.optInt("id");
                names[i] = "#" + ids[i] + "  " + (o == null ? "" : o.optString("name"));
            }
            a.runOnUiThread(() -> GdDialog.items(a, "ТРЕКИ С САЙТА", names, i -> {
                if (i < 0 || i >= ids.length) return;
                songField.setText(String.valueOf(ids[i]));
                songName.setText(names[i].substring(names[i].indexOf("  ") + 2));
                final int sid = ids[i];
                Api.async(() -> Music.ensureSong(sid));
            }));
        }));
        box.addView(pickSong, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, (int) (a.getResources().getDisplayMetrics().density * 48)));

        p[3] = picker(a, box, "Копирование уровня",
                new String[]{"Запрещено", "По паролю", "Свободно"}, Math.max(0, Math.min(2, s.copyMode)));
        f[4] = field(a, box, "пароль для копирования", s.copyPassword, false);
    }

    static TextView caption(Activity a, LinearLayout box, String text) {
        TextView t = new TextView(a);
        t.setText(text);
        t.setTextColor(Gd.NEON);
        t.setTextSize(14);
        t.setTypeface(Typeface.DEFAULT_BOLD);
        t.setPadding(0, 14, 0, 4);
        box.addView(t);
        return t;
    }

    static EditText field(Activity a, LinearLayout box, String hint, String value, boolean number) {
        caption(a, box, hint);
        EditText e = new EditText(a);
        e.setText(value == null ? "" : value);
        e.setTextColor(Gd.PAPER);
        e.setBackgroundColor(Color.rgb(20, 18, 48));
        e.setPadding(18, 12, 18, 12);
        e.setSingleLine(true);
        if (number) e.setInputType(InputType.TYPE_CLASS_NUMBER | InputType.TYPE_NUMBER_FLAG_SIGNED);
        box.addView(e, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));
        return e;
    }

    static Picker picker(Activity a, LinearLayout box, String title, String[] options, int value) {
        caption(a, box, title);

        LinearLayout row = new LinearLayout(a);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);
        box.addView(row, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));

        TextView label = new TextView(a);
        label.setTextColor(Gd.PAPER);
        label.setTextSize(16);
        label.setTypeface(Typeface.DEFAULT_BOLD);
        label.setGravity(Gravity.CENTER);
        label.setBackgroundColor(Color.rgb(20, 18, 48));
        label.setPadding(12, 14, 12, 14);

        final Picker pick = new Picker(options, value, label);

        Ui.GdBtn left = new Ui.GdBtn(a).set("", Ui.I_BACK, Color.rgb(150, 160, 200), Color.rgb(60, 66, 110));
        left.setOnClickListener(v -> pick.move(-1));
        Ui.GdBtn right = new Ui.GdBtn(a).set("", Ui.I_PLAY, Color.rgb(150, 160, 200), Color.rgb(60, 66, 110));
        right.setOnClickListener(v -> pick.move(1));

        int h = 110;
        row.addView(left, new LinearLayout.LayoutParams(h, h));
        row.addView(label, new LinearLayout.LayoutParams(0, h, 1f));
        row.addView(right, new LinearLayout.LayoutParams(h, h));
        return pick;
    }

    static void toggle(Activity a, LinearLayout box, String title, boolean[] flags, int index) {
        Ui.GdBtn b = new Ui.GdBtn(a);
        Runnable paint = () -> b.set((flags[index] ? "✔  " : "✕  ") + title,
                flags[index] ? Ui.I_CHECK : Ui.I_CROSS,
                flags[index] ? Color.rgb(120, 255, 170) : Color.rgb(150, 150, 180),
                flags[index] ? Color.rgb(20, 150, 100) : Color.rgb(60, 62, 100));
        paint.run();
        b.setOnClickListener(v -> { flags[index] = !flags[index]; paint.run(); b.invalidate(); });
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, 110);
        lp.setMargins(0, 10, 0, 0);
        box.addView(b, lp);
    }
}
