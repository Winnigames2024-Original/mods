package com.winnigames2024.sulak;

import com.google.gson.Gson;
import com.google.gson.GsonBuilder;
import net.minecraft.server.MinecraftServer;
import net.minecraft.world.level.storage.LevelResource;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.UUID;

/**
 * Состояние мода: лимиты игроков, проклятые игроки, личные api_key и
 * правила дропа ("из эндердракона падают алмазы").
 * Хранится в <мир>/sulak.json — без SavedData/NBT, чтобы не ломаться при
 * обновлениях версии игры.
 */
public class SulakState {

    private static final Gson GSON = new GsonBuilder().setPrettyPrinting().create();
    private static SulakState INSTANCE;
    private static Path file;

    public Map<String, Integer> used = new HashMap<>();
    public Set<String> cursed = new HashSet<>();
    public Map<String, String> apiKeys = new HashMap<>();
    public Map<String, List<DropRule>> dropRules = new HashMap<>();
    public String ownerUuid = null;

    public static class DropRule {
        public String item;
        public int count = 1;

        public DropRule() {}

        public DropRule(String item, int count) {
            this.item = item;
            this.count = count;
        }
    }

    public static SulakState get() {
        if (INSTANCE == null) INSTANCE = new SulakState();
        return INSTANCE;
    }

    public static void load(MinecraftServer server) {
        file = server.getWorldPath(LevelResource.ROOT).resolve("sulak.json");
        try {
            if (Files.exists(file)) {
                String json = Files.readString(file, StandardCharsets.UTF_8);
                SulakState s = GSON.fromJson(json, SulakState.class);
                if (s != null) {
                    if (s.used == null) s.used = new HashMap<>();
                    if (s.cursed == null) s.cursed = new HashSet<>();
                    if (s.apiKeys == null) s.apiKeys = new HashMap<>();
                    if (s.dropRules == null) s.dropRules = new HashMap<>();
                    INSTANCE = s;
                    Sulak.LOGGER.info("[Sulak] state loaded from {}", file);
                    return;
                }
            }
        } catch (Exception e) {
            Sulak.LOGGER.warn("[Sulak] cannot read sulak.json", e);
        }
        INSTANCE = new SulakState();
    }

    public static void save() {
        if (file == null || INSTANCE == null) return;
        try {
            Files.writeString(file, GSON.toJson(INSTANCE), StandardCharsets.UTF_8);
        } catch (IOException e) {
            Sulak.LOGGER.warn("[Sulak] cannot write sulak.json", e);
        }
    }

    public static void unload() {
        save();
        INSTANCE = null;
        file = null;
    }

    // ---- лимиты -------------------------------------------------------

    public int usedBy(UUID id) {
        return used.getOrDefault(id.toString(), 0);
    }

    public int remaining(UUID id) {
        return Math.max(0, Config.requestLimit - usedBy(id));
    }

    public void spend(UUID id) {
        used.merge(id.toString(), 1, Integer::sum);
        save();
    }

    public void setUsed(UUID id, int value) {
        used.put(id.toString(), Math.max(0, value));
        save();
    }

    public void resetPlayer(UUID id) {
        used.remove(id.toString());
        cursed.remove(id.toString());
        save();
    }

    // ---- проклятие ----------------------------------------------------

    public boolean isCursed(UUID id) {
        return cursed.contains(id.toString());
    }

    public void setCursed(UUID id, boolean value) {
        if (value) cursed.add(id.toString());
        else cursed.remove(id.toString());
        save();
    }

    // ---- ключи --------------------------------------------------------

    public String keyFor(UUID id) {
        String k = apiKeys.get(id.toString());
        if (k != null && !k.isBlank()) return k;
        return Config.apiKey;
    }

    public void setKey(UUID id, String key) {
        if (key == null || key.isBlank()) apiKeys.remove(id.toString());
        else apiKeys.put(id.toString(), key);
        save();
    }

    // ---- правила дропа ------------------------------------------------

    public void addDropRule(String entityId, String item, int count) {
        dropRules.computeIfAbsent(entityId, k -> new ArrayList<>()).add(new DropRule(item, count));
        save();
    }

    public List<DropRule> rulesFor(String entityId) {
        return dropRules.get(entityId);
    }

    public void clearDropRules(String entityId) {
        if (entityId == null) dropRules.clear();
        else dropRules.remove(entityId);
        save();
    }
}
