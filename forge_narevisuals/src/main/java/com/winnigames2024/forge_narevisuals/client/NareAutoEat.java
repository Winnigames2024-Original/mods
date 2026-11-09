package com.winnigames2024.forge_narevisuals.client;

import com.winnigames2024.forge_narevisuals.Config;
import com.winnigames2024.forge_narevisuals.Forge_narevisuals;
import net.minecraft.client.Minecraft;
import net.minecraft.core.component.DataComponents;
import net.minecraft.network.protocol.game.ServerboundSetCarriedItemPacket;
import net.minecraft.world.entity.player.Player;
import net.minecraft.world.item.Item;
import net.minecraft.world.item.ItemStack;
import net.minecraft.world.item.Items;
import net.minecraftforge.api.distmarker.Dist;
import net.minecraftforge.event.TickEvent;
import net.minecraftforge.eventbus.api.SubscribeEvent;
import net.minecraftforge.fml.common.Mod;

import java.util.Set;

/**
 * Авто-еда: когда голод падает, мод сам берёт еду из хотбара, съедает и
 * возвращает прежний предмет в руку. Вредную еду (гнильё, паучий глаз и т.п.)
 * не ест.
 */
@Mod.EventBusSubscriber(modid = Forge_narevisuals.MODID, value = Dist.CLIENT)
public final class NareAutoEat {

    private static final int EAT_BELOW = 16;   // есть, когда голод <= 16 из 20
    private static final int FULL = 20;

    private static final Set<Item> BAD = Set.of(
            Items.ROTTEN_FLESH, Items.SPIDER_EYE, Items.POISONOUS_POTATO,
            Items.PUFFERFISH, Items.CHICKEN, Items.SUSPICIOUS_STEW,
            Items.CHORUS_FRUIT);

    private static boolean eating;
    private static int prevSlot = -1;

    private NareAutoEat() { }

    @SubscribeEvent
    public static void onTick(TickEvent.ClientTickEvent event) {
        if (event.phase != TickEvent.Phase.END) return;

        Minecraft mc = Minecraft.getInstance();
        Player p = mc.player;
        if (!Config.get().autoEat || p == null || mc.level == null || mc.screen != null) {
            if (eating) stop(mc);
            return;
        }

        if (eating) {
            boolean hungry = p.getFoodData().getFoodLevel() < FULL;
            if (hungry && isFood(p.getMainHandItem())) {
                mc.options.keyUse.setDown(true);   // держим ПКМ — едим дальше
            } else {
                stop(mc);
            }
            return;
        }

        if (p.getFoodData().getFoodLevel() > EAT_BELOW) return;
        if (p.isUsingItem()) return;

        int slot = findFood(p);
        if (slot < 0) return;

        prevSlot = p.getInventory().selected;
        select(mc, p, slot);
        eating = true;
        mc.options.keyUse.setDown(true);
    }

    private static void stop(Minecraft mc) {
        mc.options.keyUse.setDown(false);
        if (mc.player != null && prevSlot >= 0 && prevSlot < 9) select(mc, mc.player, prevSlot);
        eating = false;
        prevSlot = -1;
    }

    private static void select(Minecraft mc, Player p, int slot) {
        p.getInventory().selected = slot;
        if (mc.getConnection() != null)
            mc.getConnection().send(new ServerboundSetCarriedItemPacket(slot));
    }

    private static int findFood(Player p) {
        for (int i = 0; i < 9; i++) {
            if (isFood(p.getInventory().getItem(i))) return i;
        }
        return -1;
    }

    private static boolean isFood(ItemStack st) {
        if (st == null || st.isEmpty()) return false;
        if (BAD.contains(st.getItem())) return false;
        return st.has(DataComponents.FOOD);
    }
}
