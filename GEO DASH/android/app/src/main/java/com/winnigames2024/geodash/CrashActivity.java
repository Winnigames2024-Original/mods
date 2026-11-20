package com.winnigames2024.geodash;

import android.graphics.Color;
import android.view.Gravity;
import android.view.ViewGroup;
import android.widget.LinearLayout;
import android.widget.TextView;

import org.json.JSONArray;
import org.json.JSONObject;

/**
 * Что случилось с игрой. Открывается в двух случаях:
 *   • прошлый запуск упал — игрок видит, на чём именно, отчёт уже ушёл;
 *   • зашёл админ — видит отчёты всех игроков, присланные игрой.
 */
public class CrashActivity extends GdActivity {

    private LinearLayout box;

    @Override protected int bgA() { return Color.rgb(90, 50, 130); }
    @Override protected int bgId() { return 6; }
    @Override protected int groundId() { return 4; }
    @Override protected String headTitle() { return "ОТЧЁТ ОБ ОШИБКЕ"; }

    @Override protected String headSub() {
        return Store.admin() ? "что присылают игроки" : "игра закрылась сама";
    }

    @Override protected void build() {
        String mine = Store.prefs.getString("crash_text", "");
        String stage = Store.prefs.getString("crash_stage", "");

        if (!mine.isEmpty()) {
            text("Прошлый запуск закрылся сам" + (stage.isEmpty() ? "" : ". Игра была занята: " + stage)
                            + ". Отчёт уже ушёл администратору — делать ничего не нужно.",
                    0.06f, 0.17f, 0.88f, 0.10f, Gd.PAPER, 14);
        }

        box = scrollBox(0.06f, Store.admin() ? 0.28f : 0.29f, 0.88f, 0.52f);

        if (!mine.isEmpty()) {
            note("Ваш вылет", mine);
            // показали — больше не напоминаем
            Store.prefs.edit().remove("crash_text").remove("crash_stage").apply();
        }

        if (Store.admin()) {
            text("Отчёты игроков", 0.06f, 0.23f, 0.50f, 0.05f, Gd.NEON, 15);
            btn("ПРОЧИТАНО", Ui.I_CHECK, Color.rgb(230, 200, 120), Color.rgb(150, 110, 20),
                    0.60f, 0.215f, 0.16f, 0.07f, () -> Api.async(() -> {
                        Api.rawPostAny("/api/admin/reports/seen", "{}");
                        ui.post(this::recreate);
                    }));
            btn("ОБНОВИТЬ", Ui.I_REFRESH, Color.rgb(140, 220, 255), Color.rgb(25, 100, 170),
                    0.78f, 0.215f, 0.16f, 0.07f, this::load);
            load();
        }

        back(0.06f, 0.86f);
    }

    /** Забрать список отчётов с сервера — только у админа он есть. */
    private void load() {
        Api.async(() -> {
            final String s = Api.rawGet("/api/admin/reports");
            ui.post(() -> {
                if (s == null) { note("Сервер", "Список не загрузился — нет связи."); return; }
                try {
                    JSONObject o = new JSONObject(s);
                    if (!o.optBoolean("ok")) { note("Сервер", o.optString("error", "отказ")); return; }
                    JSONArray a = o.optJSONArray("reports");
                    if (a == null || a.length() == 0) {
                        note("Пусто", "Игра ни у кого не падала.");
                        return;
                    }
                    for (int i = 0; i < Math.min(a.length(), 40); i++) {
                        JSONObject r = a.optJSONObject(i);
                        if (r == null) continue;
                        String head = (r.optBoolean("seen") ? "" : "• ")
                                + r.optString("at", "").replace("T", " ")
                                + "  " + r.optString("user", "—")
                                + "  " + r.optString("platform", "") + " " + r.optString("version", "");
                        String body = r.optString("kind", "") + " · "
                                + r.optString("stage", "неизвестно") + "\n\n"
                                + r.optString("text", "");
                        note(head, body);
                    }
                } catch (Exception e) {
                    note("Ошибка", "Ответ сервера не разобрать.");
                }
            });
        });
    }

    /** Карточка с текстом — заголовок и подробности. */
    private void note(String head, String body) {
        LinearLayout c = new LinearLayout(this);
        c.setOrientation(LinearLayout.VERTICAL);
        c.setBackgroundColor(Color.argb(210, 20, 18, 48));
        c.setPadding(22, 16, 22, 16);

        TextView t = new TextView(this);
        t.setText(head);
        t.setTextColor(Gd.NEON);
        t.setTextSize(14);
        t.setGravity(Gravity.START);
        c.addView(t);

        TextView b = new TextView(this);
        b.setText(body.length() > 2500 ? body.substring(0, 2500) + "…" : body);
        b.setTextColor(Gd.PAPER);
        b.setTextSize(12);
        c.addView(b);

        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        lp.setMargins(0, 0, 0, 14);
        box.addView(c, lp);
    }
}
