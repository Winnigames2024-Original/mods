package com.winnigames2024.forge_narevisuals.client;

import com.winnigames2024.forge_narevisuals.Config;
import com.winnigames2024.forge_narevisuals.Forge_narevisuals;

import java.util.ArrayDeque;
import java.util.Deque;
import java.util.HashSet;
import java.util.Set;

/**
 * Очередь ошибок мода. Ошибка может прилететь из чужого потока — например из
 * захвата звука, — поэтому мы её не показываем сразу, а кладём сюда. Окно
 * откроет игровой поток, когда человек не занят другим экраном.
 *
 * Одну и ту же ошибку второй раз подряд не показываем: микрофон, который не
 * открывается, умеет ругаться хоть сорок раз в секунду.
 */
public final class NareErrors {

    /** Что показываем. key — чтобы «больше не показывать» работало по смыслу, а не по тексту. */
    public record Trouble(String key, String title, String text) { }

    private static final int MAX = 8;

    private static final Deque<Trouble> queue = new ArrayDeque<>();
    private static final Set<String> shownOnce = new HashSet<>();

    private NareErrors() { }

    /**
     * Кладёт ошибку в очередь. Зовётся откуда угодно, в том числе из потока
     * захвата звука.
     */
    public static void report(String key, String title, String text) {
        if (key == null) key = "error";
        if (title == null || title.isEmpty()) title = "Что-то пошло не так";
        if (text == null) text = "";

        Forge_narevisuals.LOGGER.warn("{}: {} — {}", Forge_narevisuals.NAME, title, text);

        synchronized (queue) {
            if (Config.get().errorsIgnored.contains(key)) return;

            // уже показали и человек ничего не поменял — не нудим
            if (!shownOnce.add(key)) return;

            if (queue.size() >= MAX) queue.pollFirst();
            queue.addLast(new Trouble(key, title, text));
        }
    }

    /** Есть ли что показать. */
    public static boolean waiting() {
        synchronized (queue) { return !queue.isEmpty(); }
    }

    /** Следующая ошибка, не убирая из очереди. */
    public static Trouble peek() {
        synchronized (queue) { return queue.peekFirst(); }
    }

    /** Сколько ещё ждёт после текущей. */
    public static int left() {
        synchronized (queue) { return Math.max(0, queue.size() - 1); }
    }

    /** «Продолжить» — закрываем только эту. */
    public static void drop() {
        synchronized (queue) { queue.pollFirst(); }
    }

    /** «Игнорировать эту ошибку» — больше её не показывать никогда. */
    public static void ignore(String key) {
        synchronized (queue) {
            queue.pollFirst();
            queue.removeIf(t -> t.key().equals(key));
        }

        Config cfg = Config.get();
        if (!cfg.errorsIgnored.contains(key)) {
            cfg.errorsIgnored.add(key);
            Config.save();
        }
    }

    /** «Закрыть все ошибки» — очищаем очередь, но ничего не запрещаем навсегда. */
    public static void dropAll() {
        synchronized (queue) { queue.clear(); }
    }

    /**
     * Разрешает ошибке показаться снова — например когда человек сам полез
     * чинить микрофон и нажал «попробовать ещё раз».
     */
    public static void allowAgain(String key) {
        synchronized (queue) { shownOnce.remove(key); }
    }

    /** Список запрещённых — для настроек, чтобы можно было вернуть обратно. */
    public static java.util.List<String> ignored() {
        return new java.util.ArrayList<>(Config.get().errorsIgnored);
    }

    public static void unignoreAll() {
        Config.get().errorsIgnored.clear();
        Config.save();
        synchronized (queue) { shownOnce.clear(); }
    }
}
