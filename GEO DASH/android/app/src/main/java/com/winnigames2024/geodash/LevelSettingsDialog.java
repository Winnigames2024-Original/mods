package com.winnigames2024.geodash;

import android.app.Activity;
import android.app.AlertDialog;
import android.graphics.Color;
import android.text.InputType;
import android.view.ViewGroup;
import android.widget.*;

/** Настройки уровня: режим, скорость, сложность, музыка по ID, защита от копирования. */
public class LevelSettingsDialog {

    public static void show(Activity a, Level lv, Runnable onSaved) {
        Level.Settings s = lv.settings;

        ScrollView sv = new ScrollView(a);
        LinearLayout box = new LinearLayout(a);
        box.setOrientation(LinearLayout.VERTICAL);
        box.setPadding(40, 20, 40, 20);
        sv.addView(box);

        EditText name = field(a, box, "Название", s.name, false);
        EditText desc = field(a, box, "Описание", s.desc, false);

        Spinner diff = spinner(a, box, "Сложность", Gd.DIFFS, s.difficulty);
        Spinner mode = spinner(a, box, "Режим игрока на старте", Gd.MODE_NAMES, s.startMode);
        Spinner speed = spinner(a, box, "Скорость на старте", Gd.SPEED_NAMES, s.startSpeed);

        CheckBox mini = check(a, box, "мини-размер", s.startMini);
        CheckBox dual = check(a, box, "дуал", s.startDual);
        CheckBox mirror = check(a, box, "зеркально", s.startMirror);
        CheckBox gravUp = check(a, box, "старт с перевёрнутой гравитацией", s.startGravityUp);
        CheckBox two = check(a, box, "режим на двоих", s.twoPlayer);

        EditText song = field(a, box, "ID трека (минус — встроенный)", String.valueOf(s.songId), true);
        EditText songName = field(a, box, "Название трека", s.songName, false);

        Spinner copy = spinner(a, box, "Копирование уровня",
                new String[]{"Запрещено", "По паролю", "Свободно"}, Math.max(0, Math.min(2, s.copyMode)));
        EditText copyPass = field(a, box, "Пароль для копирования", s.copyPassword, false);

        new AlertDialog.Builder(a).setTitle("Настройки уровня").setView(sv)
                .setPositiveButton("СОХРАНИТЬ", (d, w) -> {
                    s.name = name.getText().toString().trim();
                    s.desc = desc.getText().toString();
                    s.difficulty = diff.getSelectedItemPosition();
                    s.startMode = mode.getSelectedItemPosition();
                    s.startSpeed = speed.getSelectedItemPosition();
                    s.startMini = mini.isChecked();
                    s.startDual = dual.isChecked();
                    s.startMirror = mirror.isChecked();
                    s.startGravityUp = gravUp.isChecked();
                    s.twoPlayer = two.isChecked();
                    try { s.songId = Integer.parseInt(song.getText().toString().trim()); } catch (Exception ignored) { }
                    s.songName = songName.getText().toString().trim();
                    s.copyMode = copy.getSelectedItemPosition();
                    s.copyPassword = copyPass.getText().toString().trim();
                    if (s.songId != 0) Api.async(() -> Music.ensureSong(s.songId));
                    if (onSaved != null) onSaved.run();
                })
                .setNegativeButton("Отмена", null).show();
    }

    static EditText field(Activity a, LinearLayout box, String label, String value, boolean number) {
        box.addView(label(a, label));
        EditText e = new EditText(a);
        e.setText(value == null ? "" : value);
        e.setTextColor(Gd.PAPER);
        e.setBackgroundColor(Color.rgb(20, 18, 48));
        e.setSingleLine(true);
        if (number) e.setInputType(InputType.TYPE_CLASS_NUMBER | InputType.TYPE_NUMBER_FLAG_SIGNED);
        box.addView(e, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));
        return e;
    }

    static Spinner spinner(Activity a, LinearLayout box, String label, String[] items, int sel) {
        box.addView(label(a, label));
        Spinner sp = new Spinner(a);
        ArrayAdapter<String> ad = new ArrayAdapter<>(a, android.R.layout.simple_spinner_dropdown_item, items);
        sp.setAdapter(ad);
        sp.setSelection(Math.max(0, Math.min(items.length - 1, sel)));
        box.addView(sp);
        return sp;
    }

    static CheckBox check(Activity a, LinearLayout box, String label, boolean v) {
        CheckBox c = new CheckBox(a);
        c.setText(label);
        c.setChecked(v);
        c.setTextColor(Gd.PAPER);
        box.addView(c);
        return c;
    }

    static TextView label(Activity a, String s) {
        TextView t = new TextView(a);
        t.setText(s);
        t.setTextColor(Gd.NEON);
        t.setPadding(0, 16, 0, 4);
        return t;
    }
}
