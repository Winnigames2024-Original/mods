package com.winnigames2024.geodash;

import android.graphics.Color;
import android.widget.SeekBar;

/** Настройки: сервер, громкость, синхронизация, папка данных. */
public class SettingsActivity extends GdActivity {

    SeekBar music, sfx;
    int renderH = Store.renderHeight();

    @Override protected int bgA() { return Color.rgb(70, 75, 205); }
    @Override protected int bgId() { return 14; }
    @Override protected int groundId() { return 8; }
    @Override protected String headTitle() { return "НАСТРОЙКИ"; }
    @Override protected String headSub() { return Store.assets().getAbsolutePath(); }

    @Override protected void build() {
        text("Сервер", 0.06f, 0.20f, 0.22f, 0.06f, Gd.NEON, 15);
        text(Store.SERVER_LABEL, 0.28f, 0.20f, 0.46f, 0.06f, Gd.PAPER, 15);

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

        text("Качество картинки", 0.06f, 0.40f, 0.22f, 0.06f, Gd.NEON, 15);
        qualityBtn = btn(qualityText(), Ui.I_REFRESH, Color.rgb(200, 180, 255), Color.rgb(80, 60, 160),
                0.28f, 0.38f, 0.30f, 0.10f, () -> {
                    renderH = renderH == 720 ? 540 : (renderH == 540 ? 0 : 720);
                    qualityBtn.text = qualityText();
                    qualityBtn.invalidate();
                });

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
        btn("ОБНОВЛЕНИЕ", Ui.I_DOWNLOAD, Color.rgb(140, 200, 255), Color.rgb(30, 95, 190),
                0.06f, 0.86f, 0.24f, 0.12f, () -> {
                    toast("Проверяю обновление…");
                    Updater.check(this, true);
                });
        back(0.80f, 0.86f);

        text("Песни и уровни лежат в assets: songs, levels, saved, official",
                0.06f, 0.87f, 0.70f, 0.07f, Gd.DIM, 13);
    }

    Ui.GdBtn qualityBtn;

    /**
     * Чем ниже качество, тем меньше картинка, в которой считается кадр:
     * видеочипу телефона не приходится закрашивать каждый пиксель экрана
     * полупрозрачными слоями, и уровни перестают подлагивать.
     */
    String qualityText() {
        if (renderH <= 0) return "ВЫСОКОЕ (КАК ЭКРАН)";
        return renderH <= 540 ? "НИЗКОЕ (960x540)" : "СРЕДНЕЕ (1280x720)";
    }

    void save() {
        Store.setVolumes(music.getProgress() / 100f, sfx.getProgress() / 100f);
        Store.setRenderHeight(renderH);
        toast("Сохранено");
    }
}
