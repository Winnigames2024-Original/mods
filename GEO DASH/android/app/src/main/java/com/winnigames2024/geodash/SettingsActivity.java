package com.winnigames2024.geodash;

import android.graphics.Color;
import android.text.InputType;
import android.widget.EditText;
import android.widget.SeekBar;

/** Настройки: сервер, громкость, синхронизация, папка данных. */
public class SettingsActivity extends GdActivity {

    EditText host, port, site;
    SeekBar music, sfx;

    @Override protected int bgA() { return Color.rgb(70, 75, 205); }
    @Override protected int bgId() { return 14; }
    @Override protected int groundId() { return 8; }
    @Override protected String headTitle() { return "НАСТРОЙКИ"; }
    @Override protected String headSub() { return Store.assets().getAbsolutePath(); }

    @Override protected void build() {
        text("Сервер (IP)", 0.06f, 0.20f, 0.22f, 0.06f, Gd.NEON, 15);
        host = input("IP", Store.host(), 0.28f, 0.19f, 0.30f, 0.08f);

        text("Порт", 0.06f, 0.30f, 0.22f, 0.06f, Gd.NEON, 15);
        port = input("порт", String.valueOf(Store.port()), 0.28f, 0.29f, 0.18f, 0.08f);
        port.setInputType(InputType.TYPE_CLASS_NUMBER);

        text("Сайт", 0.06f, 0.40f, 0.22f, 0.06f, Gd.NEON, 15);
        site = input("сайт", Store.site(), 0.28f, 0.39f, 0.46f, 0.08f);

        text("Громкость музыки", 0.06f, 0.50f, 0.22f, 0.06f, Gd.NEON, 15);
        music = new SeekBar(this);
        music.setMax(100);
        music.setProgress((int) (Store.musicVolume() * 100));
        root.addView(music, Ui.lp(W, H, 0.28f, 0.49f, 0.40f, 0.08f));

        text("Громкость звуков", 0.06f, 0.60f, 0.22f, 0.06f, Gd.NEON, 15);
        sfx = new SeekBar(this);
        sfx.setMax(100);
        sfx.setProgress((int) (Store.sfxVolume() * 100));
        root.addView(sfx, Ui.lp(W, H, 0.28f, 0.59f, 0.40f, 0.08f));

        btn("СОХРАНИТЬ", Ui.I_SAVE, Color.rgb(120, 255, 170), Color.rgb(20, 150, 100),
                0.06f, 0.72f, 0.22f, 0.12f, this::save);
        btn("СИНХРОНИЗАЦИЯ", Ui.I_REFRESH, Color.rgb(120, 220, 255), Color.rgb(20, 110, 180),
                0.30f, 0.72f, 0.24f, 0.12f, () -> {
                    toast("Синхронизация…");
                    Api.async(() -> { Api.sync(null); toast(Api.online ? "Готово" : "Нет сети"); });
                });
        btn("ОЧИСТИТЬ КЭШ", Ui.I_TRASH, Color.rgb(230, 140, 110), Color.rgb(150, 60, 30),
                0.56f, 0.72f, 0.22f, 0.12f, () -> confirm("Очистить кэш списков?", () -> {
                    java.io.File[] fs = Store.cache().listFiles();
                    if (fs != null) for (java.io.File f : fs) f.delete();
                    toast("Кэш очищен");
                }));
        back(0.80f, 0.86f);

        text("Песни и уровни лежат в assets: songs, levels, saved, official",
                0.06f, 0.87f, 0.70f, 0.07f, Gd.DIM, 13);
    }

    void save() {
        int p = 37534;
        try { p = Integer.parseInt(port.getText().toString().trim()); } catch (Exception ignored) { }
        Store.setServer(host.getText().toString().trim(), p, site.getText().toString().trim());
        Store.setVolumes(music.getProgress() / 100f, sfx.getProgress() / 100f);
        toast("Сохранено");
    }
}
