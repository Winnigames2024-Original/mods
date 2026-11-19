package com.winnigames2024.geodash;

import android.graphics.Color;
import android.widget.LinearLayout;

import java.io.File;
import java.util.List;

/** МОИ ПЕСНИ — что скачано в assets/songs: слушаем, качаем новое, удаляем. */
public class SongsActivity extends GdActivity {

    List<SongLib.Entry> data;
    LinearLayout box;

    @Override protected int bgA() { return Color.rgb(200, 60, 170); }
    @Override protected int bgId() { return 12; }
    @Override protected int groundId() { return 6; }
    @Override protected String headTitle() { return "МОИ ПЕСНИ"; }
    @Override protected String headSub() { return "скачанное лежит в assets/songs"; }

    @Override protected void build() {
        box = scrollBox(0.06f, 0.20f, 0.88f, 0.58f);

        btn("СКАЧАТЬ ПО ID", Ui.I_DOWNLOAD, Color.rgb(120, 200, 255), Color.rgb(25, 95, 185),
                0.06f, 0.80f, 0.26f, 0.11f, this::download);
        btn("СТОП", Ui.I_CROSS, Color.rgb(180, 150, 210), Color.rgb(90, 60, 145),
                0.34f, 0.80f, 0.18f, 0.11f, Music::stop);
        btn("ОБНОВИТЬ", Ui.I_REFRESH, Color.rgb(150, 170, 220), Color.rgb(60, 70, 140),
                0.54f, 0.80f, 0.22f, 0.11f, this::reload);
        back(0.78f, 0.80f);

        reload();
    }

    @Override protected void onPause() {
        super.onPause();
        Music.stop();
    }

    void reload() {
        if (box == null) return;
        box.removeAllViews();
        data = SongLib.scan();
        long total = 0;
        for (SongLib.Entry e : data) total += e.size;

        if (data.isEmpty()) {
            card(box, "Пока ничего не скачано", "выбери музыку в настройках уровня или кнопкой ниже", "", -1, null);
            return;
        }
        for (final SongLib.Entry e : data)
            card(box,
                    e.name,
                    (e.artist.isEmpty() ? "" : e.artist + "   ·   ") + e.sizeText()
                            + (e.builtIn ? "   ·   встроенная" : "   ·   Newgrounds"),
                    e.builtIn ? String.valueOf(e.id) : "#" + e.id,
                    -1,
                    () -> actions(e));

        card(box, "Всего: " + data.size(), String.format("%.1f МБ", total / 1024.0 / 1024.0), "", -1, null);
    }

    void actions(final SongLib.Entry e) {
        GdDialog.items(this, e.name, new String[]{"Прослушать", "Удалить", "Отмена"}, i -> {
            if (i == 0) {
                toast("Играет: " + e.name);
                Music.playFileAsync(e.file, e.id);
            } else if (i == 1) {
                if (e.builtIn) { toast("Встроенные треки нужны основным уровням"); return; }
                confirm("Удалить «" + e.name + "»?", () -> {
                    Music.stop();
                    e.file.delete();
                    reload();
                });
            }
        });
    }

    void download() {
        ask("ID трека на Newgrounds", "", s -> {
            int id;
            try { id = Integer.parseInt(s.trim()); } catch (Exception ex) { return; }
            if (id <= 0) return;
            toast("Скачиваю трек #" + id + "…");
            Api.async(() -> {
                File f = Music.ensureSong(id);
                String[] info = Api.songInfo(id);
                if (info != null) SongLib.remember(id, info[0], info[1]);
                ui.post(() -> {
                    toast(f != null ? "Трек #" + id + " скачан" : "Не вышло: " + Music.lastError);
                    reload();
                });
            });
        });
    }
}
