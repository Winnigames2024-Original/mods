package com.winnigames2024.geodash;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.graphics.Color;
import android.os.Bundle;
import android.view.*;
import android.widget.*;

/** Экран игры: GameView внутри FrameLayout + кнопки поверх. */
public class GameActivity extends Activity {

    static Level pending;
    static boolean pendingPractice;
    static boolean pendingTest;

    GameView gv;

    /** Запустить уровень. */
    public static void play(Context ctx, Level lv, boolean practice) {
        pending = lv;
        pendingPractice = practice;
        pendingTest = false;
        Intent i = new Intent(ctx, GameActivity.class);
        i.addFlags(Intent.FLAG_ACTIVITY_NO_ANIMATION);
        ctx.startActivity(i);
        if (ctx instanceof Activity) ((Activity) ctx).overridePendingTransition(0, 0);
    }

    /** Тест из редактора — результат не пишется в прогресс. */
    public static void test(Context ctx, Level lv) {
        pending = lv;
        pendingPractice = false;
        pendingTest = true;
        Intent i = new Intent(ctx, GameActivity.class);
        i.addFlags(Intent.FLAG_ACTIVITY_NO_ANIMATION);
        ctx.startActivity(i);
        if (ctx instanceof Activity) ((Activity) ctx).overridePendingTransition(0, 0);
    }

    @Override protected void onCreate(Bundle b) {
        super.onCreate(b);
        requestWindowFeature(Window.FEATURE_NO_TITLE);
        getWindow().setFlags(WindowManager.LayoutParams.FLAG_FULLSCREEN, WindowManager.LayoutParams.FLAG_FULLSCREEN);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        Store.init(this);

        Level lv = pending;
        if (lv == null) { finish(); return; }

        FrameLayout root = new FrameLayout(this);
        setContentView(root);

        gv = new GameView(this, lv, pendingPractice);
        gv.testMode = pendingTest;
        gv.onExit = v -> { v.stop(); finish(); };
        root.addView(gv, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));

        LinearLayout bar = new LinearLayout(this);
        bar.setOrientation(LinearLayout.HORIZONTAL);
        bar.addView(mini("⏸", v -> gv.togglePause()));
        if (pendingPractice) {
            bar.addView(mini("＋", v -> gv.addCheckpoint()));
            bar.addView(mini("－", v -> gv.removeCheckpoint()));
        }
        bar.addView(mini("✕", v -> { gv.stop(); finish(); }));
        FrameLayout.LayoutParams lp = new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT,
                Gravity.END | Gravity.BOTTOM);
        lp.setMargins(0, 0, 18, 18);
        root.addView(bar, lp);
    }

    Button mini(String text, View.OnClickListener l) {
        Button b = new Button(this);
        b.setText(text);
        b.setTextSize(17);
        b.setTextColor(Color.WHITE);
        b.setBackgroundColor(Color.argb(170, 40, 44, 90));
        b.setOnClickListener(l);
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        lp.setMargins(10, 0, 0, 0);
        b.setLayoutParams(lp);
        return b;
    }

    @Override protected void onPause() {
        super.onPause();
        if (gv != null) gv.paused = true;
        Music.pause();
    }

    @Override protected void onDestroy() {
        super.onDestroy();
        if (gv != null) gv.stop();
        pending = null;
    }

    @Override public void onBackPressed() {
        if (gv != null) gv.stop();
        finish();
    }

    @Override public void finish() {
        super.finish();
        overridePendingTransition(0, 0);
    }
}
