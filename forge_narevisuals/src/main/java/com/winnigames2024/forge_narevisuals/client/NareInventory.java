package com.winnigames2024.forge_narevisuals.client;

import com.winnigames2024.forge_narevisuals.Config;
import com.winnigames2024.forge_narevisuals.Forge_narevisuals;
import com.winnigames2024.forge_narevisuals.Launch;
import net.minecraft.client.Minecraft;
import net.minecraft.client.player.LocalPlayer;
import net.minecraft.world.entity.EquipmentSlot;
import net.minecraft.world.inventory.ClickType;
import net.minecraft.world.item.ItemStack;
import net.minecraft.world.item.Items;
import net.minecraftforge.api.distmarker.Dist;
import net.minecraftforge.event.TickEvent;
import net.minecraftforge.eventbus.api.SubscribeEvent;
import net.minecraftforge.fml.common.Mod;

/**
 * Помощь с инвентарём в бою: тотем в руку и броня по слотам.
 *
 * Делается ровно тем же способом, каким это делает человек: клики по
 * слотам через обычное окно инвентаря. Ничего сверх того, что доступно
 * руками, мод не умеет — просто успевает быстрее.
 *
 * Между кликами держим паузу: сервер справедливо не любит, когда в один
 * тик приходит десяток перекладываний.
 */
@Mod.EventBusSubscriber(modid = Forge_narevisuals.MODID, value = Dist.CLIENT)
public final class NareInventory {

    private static final int GAP = 5;           // тиков между действиями

    private static int cooldown;

    private NareInventory() { }

    @SubscribeEvent
    public static void onTick(TickEvent.ClientTickEvent event) {
        if (event.phase != TickEvent.Phase.END) return;
        if (!Launch.visuals()) return;

        Minecraft mc = Minecraft.getInstance();
        if (mc.player == null || mc.gameMode == null) return;
        if (mc.screen != null) return;                    // человек сам в инвентаре

        if (cooldown > 0) { cooldown--; return; }

        Config cfg = Config.get();

        if (cfg.autoTotem && totem(mc, mc.player)) { cooldown = GAP; return; }
        if (cfg.autoArmor && armor(mc, mc.player)) cooldown = GAP;
    }

    // ------------------------------------------------------------- тотем

    /** Кладёт тотем во вторую руку, когда здоровья осталось мало. */
    private static boolean totem(Minecraft mc, LocalPlayer player) {
        float hearts = (player.getHealth() + player.getAbsorptionAmount()) / 2f;
        if (hearts > Config.get().autoTotemAt) return false;

        if (player.getOffhandItem().is(Items.TOTEM_OF_UNDYING)) return false;

        int slot = find(player, Items.TOTEM_OF_UNDYING);
        if (slot < 0) return false;

        click(mc, player, inventoryIndex(slot));
        click(mc, player, 45);                            // 45 — вторая рука
        click(mc, player, inventoryIndex(slot));

        Forge_narevisuals.LOGGER.debug("{}: тотем в руку", Forge_narevisuals.NAME);
        return true;
    }

    private static int find(LocalPlayer player, net.minecraft.world.item.Item want) {
        var inv = player.getInventory();

        for (int i = 0; i < inv.getContainerSize(); i++)
            if (inv.getItem(i).is(want)) return i;

        return -1;
    }

    // ------------------------------------------------------------- броня

    /** Надевает лучшую броню, какая есть в инвентаре. */
    private static boolean armor(Minecraft mc, LocalPlayer player) {
        var inv = player.getInventory();
        boolean upgrade = Config.get().autoArmorUpgrade;

        for (EquipmentSlot slot : new EquipmentSlot[] {
                EquipmentSlot.HEAD, EquipmentSlot.CHEST, EquipmentSlot.LEGS, EquipmentSlot.FEET }) {

            ItemStack worn = player.getItemBySlot(slot);
            int wornValue = value(worn, slot);

            int best = -1;
            int bestValue = wornValue;

            for (int i = 0; i < inv.getContainerSize(); i++) {
                ItemStack stack = inv.getItem(i);
                int candidate = value(stack, slot);
                if (candidate <= bestValue) continue;

                best = i;
                bestValue = candidate;
            }

            if (best < 0) continue;
            if (!worn.isEmpty() && !upgrade) continue;    // менять крепкое не просили

            click(mc, player, inventoryIndex(best));
            click(mc, player, slotIndex(slot));
            click(mc, player, inventoryIndex(best));      // то, что сняли, обратно

            Forge_narevisuals.LOGGER.debug("{}: надел броню в слот {}",
                    Forge_narevisuals.NAME, slot);
            return true;
        }

        return false;
    }

    /**
     * Насколько эта вещь годится в этот слот. 0 — не годится.
     *
     * Считаем по названию предмета, а не по атрибутам: названия не менялись
     * с бета-версий, а внутренняя кухня брони переписывалась в каждой второй
     * версии игры. Так мод переживает обновления.
     */
    private static int value(ItemStack stack, EquipmentSlot slot) {
        if (stack.isEmpty()) return 0;

        String id = net.minecraft.core.registries.BuiltInRegistries.ITEM
                .getKey(stack.getItem()).getPath();

        String need = switch (slot) {
            case HEAD -> "helmet";
            case CHEST -> "chestplate";
            case LEGS -> "leggings";
            default -> "boots";
        };

        boolean fits = id.endsWith(need)
                || (slot == EquipmentSlot.HEAD && id.equals("turtle_scute"))
                || (slot == EquipmentSlot.HEAD && id.equals("turtle_helmet"));

        if (!fits) return 0;

        int material =
                  id.startsWith("netherite") ? 60
                : id.startsWith("diamond")   ? 50
                : id.startsWith("iron")      ? 40
                : id.startsWith("chainmail") ? 30
                : id.startsWith("turtle")    ? 30
                : id.startsWith("golden")    ? 20
                : id.startsWith("leather")   ? 10
                : 5;

        // зачарования считаем грубо: лучше хоть какой-то учёт, чем никакого
        var enchants = stack.get(net.minecraft.core.component.DataComponents.ENCHANTMENTS);
        if (enchants != null) material += enchants.size();

        // целое лучше раздолбанного
        if (stack.isDamageableItem() && stack.getMaxDamage() > 0) {
            int left = stack.getMaxDamage() - stack.getDamageValue();
            if (left * 10 < stack.getMaxDamage()) material -= 8;
        }

        return Math.max(1, material);
    }

    // ---------------------------------------------------------- хелперы

    /** Номер слота брони в окне инвентаря: голова 5, грудь 6, ноги 7, ступни 8. */
    private static int slotIndex(EquipmentSlot slot) {
        return switch (slot) {
            case HEAD -> 5;
            case CHEST -> 6;
            case LEGS -> 7;
            default -> 8;
        };
    }

    /**
     * Номер слота инвентаря в окне. В окне игрока хотбар идёт последним,
     * поэтому первые девять слотов инвентаря смещены в конец.
     */
    private static int inventoryIndex(int slot) {
        return slot < 9 ? slot + 36 : slot;
    }

    private static void click(Minecraft mc, LocalPlayer player, int slot) {
        try {
            mc.gameMode.handleInventoryMouseClick(
                    player.inventoryMenu.containerId, slot, 0, ClickType.PICKUP, player);
        }
        catch (Exception e) {
            Forge_narevisuals.LOGGER.warn("{}: не вышло переложить вещь: {}",
                    Forge_narevisuals.NAME, e.getMessage());
        }
    }
}
