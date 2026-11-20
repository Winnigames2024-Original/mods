package com.winnigames2024.geodash;

import android.content.Intent;
import android.net.Uri;

/**
 * Проверка лицензии на телефоне. Отдельного экрана нет: если ключ ещё не
 * введён, игра просто спрашивает его диалогом и тут же проверяет на сервере.
 *
 * Правила те же, что на компьютере: ключ привязывается к аккаунту навсегда,
 * первый запуск обязан пройти через сервер, дальше игра открывается и без сети.
 * У админа игра открыта всегда.
 */
public class LicenceGate {

    private static boolean asking = false;

    /** Проверить лицензию и, если нужно, спросить ключ. Зовётся из главного экрана. */
    public static void check(final GdActivity a) {
        if (asking || Store.admin()) return;
        asking = true;
        Api.async(() -> {
            boolean ok = Lic.check();

            // ключ мог передать установщик — пробуем его молча
            if (!ok && Lic.online) {
                String k = a.getIntent() != null ? a.getIntent().getStringExtra("gd_key") : null;
                if (k == null || !Lic.wellFormed(k)) k = "";
                if (!k.isEmpty() && Lic.activate(k) == null) ok = true;
            }

            if (ok && !Lic.installedProperly()) { a.ui.post(() -> notInstaller(a)); return; }
            if (ok) { asking = false; return; }
            if (!Lic.online) { a.ui.post(() -> offline(a)); return; }

            a.ui.post(() -> ask(a, "", ""));
        });
    }

    /** Интернета нет — проверить ключ нечем. */
    private static void offline(final GdActivity a) {
        GdDialog.message(a, "НУЖЕН ИНТЕРНЕТ",
                "Ключ проверяется на сервере, а связи нет.\n\n" +
                "Включите интернет и нажмите «Проверить» — дальше игра будет открываться и без сети.",
                new String[]{"ПРОВЕРИТЬ", "ВЫЙТИ"}, i -> {
                    asking = false;
                    if (i == 0) check(a); else a.finishAffinity();
                });
    }

    /** Игру поставили мимо установщика. */
    private static void notInstaller(final GdActivity a) {
        GdDialog.message(a, "НУЖЕН УСТАНОВЩИК",
                "Игра поставлена не установщиком. Скачайте его на сайте — он поставит игру и привяжет ключ.",
                new String[]{"СКАЧАТЬ", "ВЫЙТИ"}, i -> {
                    asking = false;
                    if (i == 0) {
                        try {
                            a.startActivity(new Intent(Intent.ACTION_VIEW,
                                    Uri.parse(Store.site() + "/download/")));
                        } catch (Exception ignored) { }
                    }
                    a.finishAffinity();
                });
    }

    /** Спросить ключ и проверить его. */
    private static void ask(final GdActivity a, String prefill, String problem) {
        String text = (problem.isEmpty() ? "" : problem + "\n\n") +
                "Ключ открывает игру на аккаунте " + Store.user() + " — навсегда и на всех ваших " +
                "устройствах. Он есть в вашем аккаунте на сайте, его может выдать Winnigames2024, " +
                "и он приходит сразу после покупки.";

        GdDialog.inputOr(a, "ЛИЦЕНЗИОННЫЙ КЛЮЧ", text, "GD-XXXX-XXXX-XXXX-XXXX", prefill,
                "ПРОВЕРИТЬ", "ВЫЙТИ",
                value -> {
                    final String k = Lic.pretty(value);
                    if (!Lic.wellFormed(k)) {
                        ask(a, value, "Ключ набран не полностью: GD- и 16 знаков.");
                        return;
                    }
                    Api.async(() -> {
                        String err = Lic.activate(k);
                        a.ui.post(() -> {
                            if (err == null) {
                                asking = false;
                                GdDialog.info(a, "ГОТОВО", "Ключ закреплён за аккаунтом " + Store.user() +
                                        ". Приятной игры!");
                            } else {
                                ask(a, k, err);
                            }
                        });
                    });
                },
                () -> { asking = false; a.finishAffinity(); });
    }
}
