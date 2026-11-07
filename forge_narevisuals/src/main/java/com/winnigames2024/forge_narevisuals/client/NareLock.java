package com.winnigames2024.forge_narevisuals.client;

import com.winnigames2024.forge_narevisuals.Launch;

/**
 * Режим «с этого сервера не выходим».
 *
 * Включает лаунчер аргументом <c>--lockserver</c>, когда запускает игру
 * кнопкой конкретного сервера. Кнопка выхода в паузе при этом пропадает.
 *
 * Закрыть игру человек всё равно может — крестиком или Alt+F4, и так и
 * должно быть: запирать в игре насмерть нельзя, да и незачем.
 */
public final class NareLock {

    private NareLock() { }

    public static boolean on() {
        return Launch.flag("--lockserver");
    }
}
