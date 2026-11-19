package com.winnigames2024.geodash;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.Context;
import android.graphics.*;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.text.InputType;
import android.view.*;
import android.widget.*;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.List;

/** Главная активность: меню, экраны и запуск игры. */
public class MainActivity extends Activity {

    FrameLayout root;
    MenuView menu;
    final Handler ui = new Handler(Looper.getMainLooper());
    String status = "Запуск…";
    JSONObject account;

    @Override protected void onCreate(Bundle b) {
        super.onCreate(b);
        requestWindowFeature(Window.FEATURE_NO_TITLE);
        getWindow().setFlags(WindowManager.LayoutParams.FLAG_FULLSCREEN, WindowManager.LayoutParams.FLAG_FULLSCREEN);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);

        Store.init(this);
        root = new FrameLayout(this);
        root.setBackgroundColor(Gd.DEEP);
        setContentView(root);

        showMenu();

        Api.async(() -> {
            setStatus("Готовлю встроенный саундтрек…");
            Soundtrack.ensureAll();
            ui.post(() -> Music.play(-1, 0, 0.6f, true));
            setStatus("Синхронизация…");
            Api.sync(null);
            account = Api.me();
            setStatus(Api.online ? "Готово" : "Офлайн — доступны локальные и сохранённые уровни");
            checkUpdate();
        });
    }

    void setStatus(String s) {
        status = s;
        ui.post(() -> { if (menu != null) menu.invalidate(); });
    }

    @Override protected void onDestroy() {
        super.onDestroy();
        Music.stop();
    }

    @Override public void onBackPressed() {
        if (root.getChildCount() > 0 && !(root.getChildAt(0) instanceof MenuView)) showMenu();
        else super.onBackPressed();
    }

    void show(View v) {
        root.removeAllViews();
        root.addView(v, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT,
                ViewGroup.LayoutParams.MATCH_PARENT));
    }

    void showMenu() {
        menu = new MenuView(this);
        show(menu);
        Music.play(-1, 0, 0.6f, true);
    }

    // ------------------------------------------------------------ игра

    void playLevel(Level lv, boolean practice) {
        Music.stop();
        GameView gv = new GameView(this, lv, practice);
        gv.onExit = v -> { v.stop(); showMenu(); };
        show(gv);

        // кнопки поверх игры
        LinearLayout bar = new LinearLayout(this);
        bar.setOrientation(LinearLayout.HORIZONTAL);
        bar.addView(smallBtn("⏸", v -> gv.togglePause()));
        if (practice) {
            bar.addView(smallBtn("＋", v -> gv.addCheckpoint()));
            bar.addView(smallBtn("－", v -> gv.removeCheckpoint()));
        }
        bar.addView(smallBtn("✕", v -> { gv.stop(); showMenu(); }));
        FrameLayout.LayoutParams lp = new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT, Gravity.END | Gravity.BOTTOM);
        lp.setMargins(0, 0, 16, 16);
        root.addView(bar, lp);
    }

    Button smallBtn(String text, View.OnClickListener l) {
        Button b = new Button(this);
        b.setText(text);
        b.setTextSize(18);
        b.setTextColor(Color.WHITE);
        b.setBackgroundColor(Color.argb(170, 40, 44, 90));
        b.setPadding(24, 8, 24, 8);
        b.setOnClickListener(l);
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        lp.setMargins(8, 0, 0, 0);
        b.setLayoutParams(lp);
        return b;
    }

    // ------------------------------------------------------------ списки

    interface RowAction { void run(int index); }

    ScrollView listScreen(String title, List<String[]> rows, String[] actions, RowAction[] handlers) {
        ScrollView sv = new ScrollView(this);
        LinearLayout box = new LinearLayout(this);
        box.setOrientation(LinearLayout.VERTICAL);
        box.setPadding(24, 24, 24, 24);
        sv.addView(box);
        sv.setBackgroundColor(Gd.DEEP);

        TextView h = new TextView(this);
        h.setText(title);
        h.setTextSize(26);
        h.setTypeface(Typeface.DEFAULT_BOLD);
        h.setTextColor(Gd.PAPER);
        h.setPadding(0, 0, 0, 16);
        box.addView(h);

        Button back = new Button(this);
        back.setText("← НАЗАД");
        back.setTextColor(Color.WHITE);
        back.setBackgroundColor(Color.argb(255, 60, 64, 110));
        back.setOnClickListener(v -> showMenu());
        box.addView(back);

        if (rows.isEmpty()) {
            TextView e = new TextView(this);
            e.setText("Пусто.");
            e.setTextColor(Gd.DIM);
            e.setPadding(0, 24, 0, 0);
            box.addView(e);
        }

        for (int i = 0; i < rows.size(); i++) {
            final int idx = i;
            String[] row = rows.get(i);

            LinearLayout card = new LinearLayout(this);
            card.setOrientation(LinearLayout.VERTICAL);
            card.setPadding(20, 16, 20, 16);
            card.setBackgroundColor(Color.argb(255, 28, 24, 62));
            LinearLayout.LayoutParams clp = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
            clp.setMargins(0, 14, 0, 0);
            card.setLayoutParams(clp);

            TextView t1 = new TextView(this);
            t1.setText(row[0]);
            t1.setTextSize(18);
            t1.setTypeface(Typeface.DEFAULT_BOLD);
            t1.setTextColor(Gd.PAPER);
            card.addView(t1);

            if (row.length > 1) {
                TextView t2 = new TextView(this);
                t2.setText(row[1]);
                t2.setTextColor(Gd.DIM);
                card.addView(t2);
            }

            LinearLayout br = new LinearLayout(this);
            br.setOrientation(LinearLayout.HORIZONTAL);
            br.setPadding(0, 10, 0, 0);
            for (int a = 0; a < actions.length; a++) {
                final int ai = a;
                Button btn = new Button(this);
                btn.setText(actions[a]);
                btn.setTextColor(Color.BLACK);
                btn.setBackgroundColor(a == 0 ? Color.rgb(140, 240, 180) : Color.rgb(160, 190, 240));
                LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
                lp.setMargins(0, 0, 10, 0);
                btn.setLayoutParams(lp);
                btn.setOnClickListener(v -> handlers[ai].run(idx));
                br.addView(btn);
            }
            card.addView(br);
            box.addView(card);
        }
        return sv;
    }

    // --------------------------- основные уровни

    void showOfficial() {
        final List<Level> levels = Official.all();
        List<String[]> rows = new ArrayList<>();
        for (Level lv : levels) {
            double[] pr = Store.progress(lv);
            rows.add(new String[]{
                    lv.settings.name + "  —  " + Gd.DIFFS[lv.settings.difficulty],
                    String.format("лучший результат: %.0f%%   монет: %d/3   музыка: %s",
                            pr[0], (int) pr[2], lv.settings.songName)});
        }
        show(listScreen("ОСНОВНЫЕ УРОВНИ", rows, new String[]{"ИГРАТЬ", "ТРЕНИРОВКА"},
                new RowAction[]{
                        i -> playLevel(levels.get(i), false),
                        i -> playLevel(levels.get(i), true)
                }));
    }

    // --------------------------- сохранённые

    void showSaved() {
        final List<Level> levels = Store.savedLevels();
        List<String[]> rows = new ArrayList<>();
        for (Level lv : levels) {
            double[] pr = Store.progress(lv);
            rows.add(new String[]{
                    lv.settings.name + (lv.settings.serverId > 0 ? "  #" + lv.settings.serverId : ""),
                    "автор: " + lv.settings.author + "   " + Gd.DIFFS[lv.settings.difficulty] +
                            String.format("   лучший: %.0f%%", pr[0]) +
                            (Music.isCached(lv.settings.songId) ? "   ♪ офлайн" : "")});
        }
        show(listScreen("SAVED — ОФЛАЙН-УРОВНИ", rows, new String[]{"ИГРАТЬ", "ТРЕНИРОВКА"},
                new RowAction[]{
                        i -> playLevel(levels.get(i), false),
                        i -> playLevel(levels.get(i), true)
                }));
    }

    // --------------------------- онлайн

    void showOnline() {
        Api.async(() -> {
            JSONArray arr = Api.levels("", "recent", -1);
            final List<int[]> ids = new ArrayList<>();
            final List<String[]> rows = new ArrayList<>();
            for (int i = 0; i < arr.length(); i++) {
                JSONObject o = arr.optJSONObject(i);
                if (o == null) continue;
                ids.add(new int[]{o.optInt("id")});
                rows.add(new String[]{
                        "#" + o.optInt("id") + "  " + o.optString("name"),
                        "автор: " + o.optString("author") + "   " + Gd.DIFFS[Math.min(11, o.optInt("difficulty"))]
                                + "   ★" + o.optInt("stars") + "  ♥" + o.optInt("likes")
                                + (Store.isSaved(o.optInt("id")) ? "   ✔ сохранён" : "")});
            }
            ui.post(() -> show(listScreen(Api.online ? "ОНЛАЙН-УРОВНИ" : "ОНЛАЙН (кэш)", rows,
                    new String[]{"ИГРАТЬ", "СОХРАНИТЬ", "♥"},
                    new RowAction[]{
                            i -> downloadAndPlay(ids.get(i)[0]),
                            i -> Api.async(() -> {
                                Api.downloadLevel(ids.get(i)[0]);
                                ui.post(() -> toast("Сохранено в SAVED"));
                            }),
                            i -> Api.async(() -> {
                                Api.like(ids.get(i)[0]);
                                ui.post(() -> toast("Лайк отправлен"));
                            })
                    })));
        });
        toast("Загружаю список…");
    }

    void downloadAndPlay(int id) {
        toast("Скачиваю уровень…");
        Api.async(() -> {
            Level lv = Api.downloadLevel(id);
            if (lv == null && Store.isSaved(id)) lv = Store.loadSaved(id);
            final Level f = lv;
            ui.post(() -> {
                if (f == null) toast("Нет сети и уровень не сохранён");
                else playLevel(f, false);
            });
        });
    }

    // --------------------------- daily / weekly

    void showDaily() {
        Api.async(() -> {
            JSONObject d = Api.daily(), w = Api.weekly();
            final List<int[]> ids = new ArrayList<>();
            final List<String[]> rows = new ArrayList<>();
            addDaily(rows, ids, d, "DAILY — уровень дня");
            addDaily(rows, ids, w, "WEEKLY — уровень недели");
            ui.post(() -> show(listScreen("DAILY / WEEKLY", rows, new String[]{"ИГРАТЬ", "СОХРАНИТЬ"},
                    new RowAction[]{
                            i -> downloadAndPlay(ids.get(i)[0]),
                            i -> Api.async(() -> {
                                Api.downloadLevel(ids.get(i)[0]);
                                ui.post(() -> toast("Сохранено офлайн"));
                            })
                    })));
        });
    }

    void addDaily(List<String[]> rows, List<int[]> ids, JSONObject d, String label) {
        if (d == null) return;
        JSONObject l = d.optJSONObject("level");
        if (l == null) return;
        ids.add(new int[]{l.optInt("id")});
        long left = d.optLong("secondsLeft");
        rows.add(new String[]{label + ": " + l.optString("name"),
                "автор: " + l.optString("author") + "   " + Gd.DIFFS[Math.min(11, l.optInt("difficulty"))]
                        + "   сменится через " + (left / 86400) + "д " + (left % 86400 / 3600) + "ч"
                        + (Store.isSaved(l.optInt("id")) ? "   ✔ офлайн" : "")});
    }

    // --------------------------- друзья

    void showFriends() {
        if (!Store.loggedIn()) { showLogin(); return; }
        Api.async(() -> {
            JSONArray fr = Api.friends(), rq = Api.friendRequests();
            final List<String> names = new ArrayList<>();
            final List<String[]> rows = new ArrayList<>();
            final List<Boolean> incoming = new ArrayList<>();

            for (int i = 0; i < rq.length(); i++) {
                JSONObject o = rq.optJSONObject(i);
                if (o == null) continue;
                boolean in = o.optString("to").equalsIgnoreCase(Store.user());
                names.add(in ? o.optString("from") : o.optString("to"));
                incoming.add(in);
                rows.add(new String[]{(in ? "запрос от " : "отправлен → ") + (in ? o.optString("from") : o.optString("to")),
                        in ? "можно принять или отклонить" : "ждём ответа"});
            }
            for (int i = 0; i < fr.length(); i++) {
                JSONObject o = fr.optJSONObject(i);
                if (o == null) continue;
                names.add(o.optString("name"));
                incoming.add(false);
                rows.add(new String[]{"друг: " + o.optString("name"), "★ " + o.optInt("stars")});
            }

            ui.post(() -> {
                ScrollView sv = listScreen("ДРУЗЬЯ", rows, new String[]{"ПРИНЯТЬ", "ОТКЛОНИТЬ", "УДАЛИТЬ"},
                        new RowAction[]{
                                i -> friendAct("accept", names.get(i)),
                                i -> friendAct("decline", names.get(i)),
                                i -> friendAct("remove", names.get(i))
                        });
                LinearLayout box = (LinearLayout) sv.getChildAt(0);
                Button add = new Button(this);
                add.setText("+ ДОБАВИТЬ ДРУГА");
                add.setTextColor(Color.BLACK);
                add.setBackgroundColor(Color.rgb(255, 180, 220));
                add.setOnClickListener(v -> askText("Имя игрока", name -> friendAct("request", name)));
                box.addView(add, 2);
                show(sv);
            });
        });
    }

    void friendAct(String action, String name) {
        Api.async(() -> {
            boolean ok = Api.friendAction(action, name);
            ui.post(() -> {
                toast(ok ? "Готово" : "Нет сети — задача в очереди");
                showFriends();
            });
        });
    }

    // --------------------------- аккаунт

    void showAccount() {
        if (!Store.loggedIn()) { showLogin(); return; }
        Api.async(() -> {
            final JSONObject me = Api.me();
            account = me;
            ui.post(() -> {
                List<String[]> rows = new ArrayList<>();
                if (me != null) {
                    rows.add(new String[]{me.optString("name"),
                            "★ " + me.optInt("stars") + "   монет: " + me.optInt("coins")
                                    + "   демонов: " + me.optInt("demons")
                                    + "   уровней: " + me.optInt("levels")});
                }
                rows.add(new String[]{"Локальная статистика",
                        "монет: " + Store.coinsTotal() + "   демонов пройдено: " + Store.demonsBeaten()});
                rows.add(new String[]{"Папка данных", Store.assets().getAbsolutePath()});
                rows.add(new String[]{"Сервер", Store.host() + ":" + Store.port()
                        + (Api.online ? "   (онлайн)" : "   (офлайн)")});

                ScrollView sv = listScreen("АККАУНТ", rows, new String[]{"ОК"},
                        new RowAction[]{i -> { }});
                LinearLayout box = (LinearLayout) sv.getChildAt(0);
                Button out = new Button(this);
                out.setText("ВЫЙТИ ИЗ АККАУНТА");
                out.setTextColor(Color.WHITE);
                out.setBackgroundColor(Color.rgb(170, 60, 70));
                out.setOnClickListener(v -> {
                    Store.setAccount("", "", false);
                    toast("Вышли из аккаунта");
                    showMenu();
                });
                box.addView(out, 2);
                show(sv);
            });
        });
    }

    void showLogin() {
        LinearLayout box = new LinearLayout(this);
        box.setOrientation(LinearLayout.VERTICAL);
        box.setPadding(40, 20, 40, 20);
        final EditText name = new EditText(this);
        name.setHint("имя игрока");
        name.setTextColor(Gd.PAPER);
        name.setText(Store.user());
        final EditText pass = new EditText(this);
        pass.setHint("пароль");
        pass.setTextColor(Gd.PAPER);
        pass.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_VARIATION_PASSWORD);
        box.addView(name);
        box.addView(pass);

        new AlertDialog.Builder(this)
                .setTitle("Вход в GEO DASH")
                .setView(box)
                .setPositiveButton("ВОЙТИ", (d, w) -> auth(name.getText().toString(), pass.getText().toString(), false))
                .setNeutralButton("РЕГИСТРАЦИЯ", (d, w) -> auth(name.getText().toString(), pass.getText().toString(), true))
                .setNegativeButton("Отмена", null)
                .show();
    }

    void auth(String n, String p, boolean register) {
        Api.async(() -> {
            JSONObject r = register ? Api.register(n, p) : Api.login(n, p);
            ui.post(() -> {
                if (r != null && r.optBoolean("ok")) {
                    JSONObject acc = r.optJSONObject("account");
                    Store.setAccount(r.optString("token"), acc != null ? acc.optString("name") : n,
                            acc != null && acc.optBoolean("admin"));
                    toast("Привет, " + Store.user() + "!");
                    showMenu();
                } else {
                    toast(r != null ? r.optString("error", "Не вышло") :
                            (Api.online ? "Ошибка" : "Нет сети — войти нельзя, но играть офлайн можно"));
                }
            });
        });
    }

    void askText(String title, java.util.function.Consumer<String> cb) {
        final EditText e = new EditText(this);
        e.setTextColor(Gd.PAPER);
        new AlertDialog.Builder(this).setTitle(title).setView(e)
                .setPositiveButton("OK", (d, w) -> cb.accept(e.getText().toString().trim()))
                .setNegativeButton("Отмена", null).show();
    }

    void toast(String s) { Toast.makeText(this, s, Toast.LENGTH_SHORT).show(); }

    // --------------------------- настройки

    void showSettings() {
        LinearLayout box = new LinearLayout(this);
        box.setOrientation(LinearLayout.VERTICAL);
        box.setPadding(40, 20, 40, 20);

        final EditText host = new EditText(this);
        host.setHint("IP сервера"); host.setText(Store.host()); host.setTextColor(Gd.PAPER);
        final EditText port = new EditText(this);
        port.setHint("порт"); port.setText(String.valueOf(Store.port()));
        port.setInputType(InputType.TYPE_CLASS_NUMBER); port.setTextColor(Gd.PAPER);
        final EditText site = new EditText(this);
        site.setHint("сайт"); site.setText(Store.site()); site.setTextColor(Gd.PAPER);

        final SeekBar music = new SeekBar(this);
        music.setMax(100); music.setProgress((int) (Store.musicVolume() * 100));
        final SeekBar sfx = new SeekBar(this);
        sfx.setMax(100); sfx.setProgress((int) (Store.sfxVolume() * 100));

        box.addView(label("Сервер")); box.addView(host); box.addView(port); box.addView(site);
        box.addView(label("Громкость музыки")); box.addView(music);
        box.addView(label("Громкость звуков")); box.addView(sfx);

        new AlertDialog.Builder(this).setTitle("Настройки").setView(box)
                .setPositiveButton("СОХРАНИТЬ", (d, w) -> {
                    int p = 37534;
                    try { p = Integer.parseInt(port.getText().toString().trim()); } catch (Exception ignored) { }
                    Store.setServer(host.getText().toString().trim(), p, site.getText().toString().trim());
                    Store.setVolumes(music.getProgress() / 100f, sfx.getProgress() / 100f);
                    toast("Сохранено");
                })
                .setNeutralButton("СИНХРОНИЗИРОВАТЬ", (d, w) -> Api.async(() -> {
                    Api.sync(null);
                    ui.post(() -> toast(Api.online ? "Синхронизировано" : "Нет сети"));
                }))
                .setNegativeButton("Закрыть", null).show();
    }

    TextView label(String s) {
        TextView t = new TextView(this);
        t.setText(s);
        t.setTextColor(Gd.NEON);
        t.setPadding(0, 18, 0, 4);
        return t;
    }

    // --------------------------- обновление

    void checkUpdate() {
        if (!Api.online) return;
        String s = Api.rawGet("/api/version");
        if (s == null) return;
        try {
            JSONObject v = new JSONObject(s);
            final String remote = v.optString("version", "");
            final String apk = v.optString("apk", "");
            if (remote.isEmpty() || remote.equals("1.0.0") || apk.isEmpty()) return;
            final String notes = v.optString("notes", "");
            ui.post(() -> new AlertDialog.Builder(this)
                    .setTitle("Есть обновление " + remote)
                    .setMessage(notes + "\n\nСкачать новую версию?")
                    .setPositiveButton("СКАЧАТЬ", (d, w) -> {
                        try {
                            startActivity(new android.content.Intent(android.content.Intent.ACTION_VIEW,
                                    android.net.Uri.parse(apk)));
                        } catch (Exception e) { toast("Не вышло открыть ссылку"); }
                    })
                    .setNegativeButton("Позже", null).show());
        } catch (Exception ignored) { }
    }

    // ------------------------------------------------------------ меню

    class MenuView extends View {
        final List<Object[]> buttons = new ArrayList<>();   // rect, label, icon, runnable
        double t;
        RectF playRect = new RectF();

        MenuView(Context c) {
            super(c);
            postDelayed(new Runnable() {
                @Override public void run() {
                    t += 0.033;
                    invalidate();
                    postDelayed(this, 33);
                }
            }, 33);
        }

        @Override protected void onDraw(Canvas c) {
            int W = getWidth(), H = getHeight();
            Gd.background(c, W, H, t);

            Gd.outlined(c, "GEO DASH", W / 2f, H * 0.17f, Math.min(72f, W * 0.075f), Gd.PAPER, Paint.Align.CENTER);
            Gd.outlined(c, "by Winnigames2024", W / 2f, H * 0.24f, Math.min(22f, W * 0.022f), Gd.NEON, Paint.Align.CENTER);

            float ps = Math.min(W, H) * 0.3f;
            playRect.set(W / 2f - ps / 2, H * 0.36f, W / 2f + ps / 2, H * 0.36f + ps);
            Gd.playButton(c, playRect, t);

            buttons.clear();
            float bw = W * 0.21f, bh = H * 0.12f;
            addBtn(W * 0.06f, H * 0.40f, bw, bh, "SAVED", Color.rgb(120, 230, 160), () -> showSaved());
            addBtn(W * 0.06f, H * 0.56f, bw, bh, "ОНЛАЙН", Color.rgb(90, 170, 255), () -> showOnline());
            addBtn(W - W * 0.06f - bw, H * 0.40f, bw, bh, "DAILY", Color.rgb(210, 140, 255), () -> showDaily());
            addBtn(W - W * 0.06f - bw, H * 0.56f, bw, bh, "ДРУЗЬЯ", Color.rgb(255, 150, 200), () -> showFriends());

            float sw = W * 0.16f, sh = H * 0.10f;
            addBtn(W / 2f - sw * 1.6f, H * 0.80f, sw, sh, "АККАУНТ", Color.rgb(120, 190, 255), () -> showAccount());
            addBtn(W / 2f - sw * 0.5f, H * 0.80f, sw, sh, "НАСТРОЙКИ", Color.rgb(160, 170, 200), () -> showSettings());
            addBtn(W / 2f + sw * 0.6f, H * 0.80f, sw, sh, "СИНХРО", Color.rgb(120, 220, 180), () -> Api.async(() -> {
                Api.sync(null);
                ui.post(() -> { toast(Api.online ? "Синхронизировано" : "Нет сети"); invalidate(); });
            }));

            for (Object[] b : buttons) {
                RectF r = (RectF) b[0];
                int col = (Integer) b[2];
                Gd.plate(c, r, Gd.lighten(col, 0.15f), Gd.lighten(col, -0.45f), r.height() * 0.28f);
                Gd.outlined(c, (String) b[1], r.centerX(), r.centerY() + r.height() * 0.12f,
                        Math.min(r.height() * 0.38f, r.width() * 0.17f), Gd.PAPER, Paint.Align.CENTER);
            }

            // карточка аккаунта
            RectF card = new RectF(W - W * 0.3f, 14, W - 14, 14 + H * 0.14f);
            Gd.glass(c, card, 16f);
            String name = Store.loggedIn() ? Store.user() : "Гость";
            Gd.outlined(c, name, card.left + 16, card.top + card.height() * 0.38f,
                    Math.min(26f, W * 0.022f), Gd.PAPER, Paint.Align.LEFT);
            int stars = account != null ? account.optInt("stars") : 0;
            int coins = Math.max(account != null ? account.optInt("coins") : 0, Store.coinsTotal());
            int demons = Math.max(account != null ? account.optInt("demons") : 0, Store.demonsBeaten());
            Gd.outlined(c, "★ " + stars + "   ◉ " + coins + "   ☠ " + demons,
                    card.left + 16, card.top + card.height() * 0.78f,
                    Math.min(20f, W * 0.017f), Gd.GOLD, Paint.Align.LEFT);

            // статус
            c.drawRect(0, H - 34, W, H, Gd.fill(Color.argb(170, 8, 6, 22)));
            Gd.outlined(c, status, 14, H - 11, 18f, Gd.PAPER, Paint.Align.LEFT);
            Gd.outlined(c, Api.online ? "● ОНЛАЙН" : "● ОФЛАЙН", W - 14, H - 11, 18f,
                    Api.online ? Gd.LIME : Gd.GOLD, Paint.Align.RIGHT);
        }

        void addBtn(float x, float y, float w, float h, String label, int col, Runnable r) {
            buttons.add(new Object[]{new RectF(x, y, x + w, y + h), label, col, r});
        }

        @Override public boolean onTouchEvent(MotionEvent e) {
            if (e.getAction() != MotionEvent.ACTION_DOWN) return true;
            float x = e.getX(), y = e.getY();
            if (playRect.contains(x, y)) { Music.sfx("click"); showOfficial(); return true; }
            for (Object[] b : buttons) {
                if (((RectF) b[0]).contains(x, y)) {
                    Music.sfx("click");
                    ((Runnable) b[3]).run();
                    return true;
                }
            }
            return true;
        }
    }
}
