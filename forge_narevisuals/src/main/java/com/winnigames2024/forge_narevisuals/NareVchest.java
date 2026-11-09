package com.winnigames2024.forge_narevisuals;

import net.minecraft.core.HolderLookup;
import net.minecraft.nbt.CompoundTag;
import net.minecraft.nbt.ListTag;
import net.minecraft.nbt.NbtAccounter;
import net.minecraft.nbt.NbtIo;
import net.minecraft.nbt.Tag;
import net.minecraft.network.chat.Component;
import net.minecraft.world.SimpleContainer;
import net.minecraft.world.SimpleMenuProvider;
import net.minecraft.world.inventory.ChestMenu;
import net.minecraft.world.item.ItemStack;
import net.minecraftforge.fml.loading.FMLPaths;

import java.nio.file.Files;
import java.nio.file.Path;
import java.util.HashMap;
import java.util.Map;

/**
 * Личный виртуальный сундук игрока (27 слотов) с сохранением. Содержимое пишется
 * в narevchests.dat (NBT) по UUID игрока и сохраняется при каждом изменении.
 */
public final class NareVchest {

    private static final int SIZE = 27;
    private static final Map<String, SimpleContainer> LIVE = new HashMap<>();

    private NareVchest() { }

    private static Path file() {
        return FMLPaths.GAMEDIR.get().resolve("narevchests.dat");
    }

    private static CompoundTag readAll() {
        try {
            Path f = file();
            if (Files.exists(f)) return NbtIo.readCompressed(f, NbtAccounter.unlimitedHeap());
        } catch (Exception e) {
            Forge_narevisuals.LOGGER.warn("virtualchest чтение: {}", e.toString());
        }
        return new CompoundTag();
    }

    private static void writeAll(CompoundTag root) {
        try {
            NbtIo.writeCompressed(root, file());
        } catch (Exception e) {
            Forge_narevisuals.LOGGER.warn("virtualchest запись: {}", e.toString());
        }
    }

    public static void open(net.minecraft.server.level.ServerPlayer p) {
        SimpleContainer c = load(p);
        p.openMenu(new SimpleMenuProvider(
                (id, inv, pl) -> ChestMenu.threeRows(id, inv, c),
                Component.literal("Виртуальный сундук")));
    }

    private static synchronized SimpleContainer load(net.minecraft.server.level.ServerPlayer p) {
        String uuid = p.getStringUUID();
        SimpleContainer live = LIVE.get(uuid);
        if (live != null) return live;

        SimpleContainer c = new SimpleContainer(SIZE);
        HolderLookup.Provider prov = p.server.registryAccess();
        CompoundTag root = readAll();
        if (root.contains(uuid)) {
            ListTag list = root.getCompound(uuid).getList("items", Tag.TAG_COMPOUND);
            for (int i = 0; i < list.size(); i++) {
                CompoundTag it = list.getCompound(i);
                int slot = it.getInt("slot");
                ItemStack st = ItemStack.parse(prov, it.getCompound("item")).orElse(ItemStack.EMPTY);
                if (slot >= 0 && slot < SIZE && !st.isEmpty()) c.setItem(slot, st);
            }
        }
        // слушатель вешаем ПОСЛЕ загрузки, чтобы начальная установка не вызвала запись
        c.addListener(cont -> save(uuid, c, prov));
        LIVE.put(uuid, c);
        return c;
    }

    private static synchronized void save(String uuid, SimpleContainer c, HolderLookup.Provider prov) {
        CompoundTag root = readAll();
        ListTag list = new ListTag();
        for (int i = 0; i < c.getContainerSize(); i++) {
            ItemStack st = c.getItem(i);
            if (st.isEmpty()) continue;
            CompoundTag it = new CompoundTag();
            it.putInt("slot", i);
            it.put("item", st.save(prov));
            list.add(it);
        }
        CompoundTag pc = new CompoundTag();
        pc.put("items", list);
        root.put(uuid, pc);
        writeAll(root);
    }
}
