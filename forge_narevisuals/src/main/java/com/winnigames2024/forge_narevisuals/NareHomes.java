package com.winnigames2024.forge_narevisuals;

import com.google.gson.Gson;
import com.google.gson.GsonBuilder;
import com.google.gson.reflect.TypeToken;
import net.minecraftforge.fml.loading.FMLPaths;

import java.io.IOException;
import java.lang.reflect.Type;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/**
 * Точки дома игроков: ник/UUID -&gt; имя точки -&gt; координаты. Хранится в
 * narehomes.json в папке игры/сервера. Простой JSON через Gson.
 */
public final class NareHomes {

    public static final class Home {
        public String dim;      // id измерения, напр. "minecraft:overworld"
        public double x, y, z;
        public float yaw, pitch;
    }

    private static final Gson GSON = new GsonBuilder().setPrettyPrinting().create();
    private static final Type TYPE = new TypeToken<Map<String, Map<String, Home>>>() { }.getType();

    private static Map<String, Map<String, Home>> data;

    private NareHomes() { }

    private static Path file() {
        return FMLPaths.GAMEDIR.get().resolve("narehomes.json");
    }

    private static synchronized void ensure() {
        if (data != null) return;
        try {
            Path f = file();
            if (Files.exists(f)) {
                String s = new String(Files.readAllBytes(f), StandardCharsets.UTF_8);
                data = GSON.fromJson(s, TYPE);
            }
        } catch (Exception e) {
            Forge_narevisuals.LOGGER.warn("не смог прочитать narehomes.json: {}", e.toString());
        }
        if (data == null) data = new LinkedHashMap<>();
    }

    private static synchronized void save() {
        try {
            Files.write(file(), GSON.toJson(data, TYPE).getBytes(StandardCharsets.UTF_8));
        } catch (IOException e) {
            Forge_narevisuals.LOGGER.warn("не смог сохранить narehomes.json: {}", e.toString());
        }
    }

    public static synchronized void set(String uuid, String name, Home home) {
        ensure();
        data.computeIfAbsent(uuid, k -> new LinkedHashMap<>()).put(name, home);
        save();
    }

    public static synchronized Home get(String uuid, String name) {
        ensure();
        Map<String, Home> m = data.get(uuid);
        return m == null ? null : m.get(name);
    }

    public static synchronized boolean delete(String uuid, String name) {
        ensure();
        Map<String, Home> m = data.get(uuid);
        if (m == null || m.remove(name) == null) return false;
        if (m.isEmpty()) data.remove(uuid);
        save();
        return true;
    }

    public static synchronized List<String> names(String uuid) {
        ensure();
        Map<String, Home> m = data.get(uuid);
        return m == null ? new ArrayList<>() : new ArrayList<>(m.keySet());
    }
}
