package com.winnigames2024.forge_narevisuals;

import net.minecraft.world.entity.player.Player;
import net.minecraftforge.event.entity.living.LivingFallEvent;
import net.minecraftforge.eventbus.api.SubscribeEvent;
import net.minecraftforge.fml.common.Mod;

import java.util.HashMap;
import java.util.Map;
import java.util.UUID;

/**
 * Защита от урона при падении после телепорта/rtp/home. На переносе отмечаем
 * игрока на несколько секунд — пока он не приземлится, падение не вредит.
 */
@Mod.EventBusSubscriber(modid = Forge_narevisuals.MODID)
public final class NareFall {

    private static final Map<UUID, Long> SAFE = new HashMap<>();
    private static final long WINDOW_MS = 12000;

    private NareFall() { }

    public static void mark(Player p) {
        if (p != null) SAFE.put(p.getUUID(), System.currentTimeMillis() + WINDOW_MS);
    }

    @SubscribeEvent
    public static void onFall(LivingFallEvent e) {
        if (!(e.getEntity() instanceof Player)) return;
        Long until = SAFE.get(e.getEntity().getUUID());
        if (until == null) return;
        if (System.currentTimeMillis() > until) {
            SAFE.remove(e.getEntity().getUUID());
            return;
        }
        // приземлился — урон гасим и снимаем отметку
        e.setDamageMultiplier(0f);
        e.setCanceled(true);
        SAFE.remove(e.getEntity().getUUID());
    }
}
