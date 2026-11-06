package com.winnigames2024.forge_narevisuals.client;

import com.winnigames2024.forge_narevisuals.Config;
import com.winnigames2024.forge_narevisuals.Forge_narevisuals;
import net.minecraft.client.Minecraft;
import net.minecraft.server.level.ParticleStatus;
import net.minecraft.core.registries.BuiltInRegistries;
import net.minecraft.world.entity.Entity;
import net.minecraft.world.entity.EntityType;
import net.minecraft.world.entity.LivingEntity;
import net.minecraft.world.entity.ExperienceOrb;
import net.minecraft.world.entity.item.ItemEntity;
import net.minecraft.world.entity.item.PrimedTnt;
import net.minecraft.world.entity.player.Player;
import net.minecraft.world.entity.projectile.Projectile;
import net.minecraftforge.api.distmarker.Dist;
import net.minecraftforge.event.TickEvent;
import net.minecraftforge.eventbus.api.SubscribeEvent;
import net.minecraftforge.fml.common.Mod;

import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

/**
 * FPS+ — всё, что можно не рисовать, чтобы не проседать.
 *
 * Важное: это только ваша картинка. Сущности остаются на месте, сервер о них
 * знает, взорвётся всё ровно так же — просто вы этого не рисуете. Поэтому
 * прятать можно смело: нечестного преимущества тут нет, скорее наоборот.
 *
 * Работает по счёту: пока на экране десяток динамита — рисуем. Стало их две
 * сотни, и кадры поехали — оставляем ближайшие, остальные убираем. Как
 * только станет меньше порога, всё вернётся само.
 */
@Mod.EventBusSubscriber(modid = Forge_narevisuals.MODID, value = Dist.CLIENT)
public final class NareFps {

    /** Сработал ли миксин отсечения хоть раз — чтобы честно сказать в меню. */
    public static boolean working;

    /** Кого не рисуем прямо сейчас. Пересобирается каждый тик. */
    private static Set<Integer> hidden = new HashSet<>();

    /** Что сейчас спрятано — для строки в меню. */
    public static int hiddenCount;
    public static int tntSeen, mobsSeen, itemsSeen, orbsSeen;

    private static ParticleStatus savedParticles;

    // бюджет частиц на тик и счётчик взрывных звуков
    private static int particlesThisTick;
    private static int particlesDropped;
    private static final java.util.ArrayDeque<Long> booms = new java.util.ArrayDeque<>();

    private NareFps() { }

    /** Виды, которые можно выключить списком. Ключ реестра плюс подпись. */
    public static final String[] TYPES = {
            "minecraft:tnt",
            "minecraft:item",
            "minecraft:experience_orb",
            "minecraft:arrow",
            "minecraft:armor_stand",
            "minecraft:item_frame",
            "minecraft:painting",
            "minecraft:boat",
            "minecraft:minecart",
            "minecraft:falling_block",
            "minecraft:firework_rocket",
            "minecraft:area_effect_cloud",
            "minecraft:end_crystal",
            "minecraft:zombie",
            "minecraft:skeleton",
            "minecraft:creeper",
            "minecraft:bat",
            "minecraft:chicken"
    };

    public static final String[] TYPE_TITLES = {
            "Динамит",
            "Выпавшие предметы",
            "Шарики опыта",
            "Стрелы",
            "Стойки для брони",
            "Рамки",
            "Картины",
            "Лодки",
            "Вагонетки",
            "Падающие блоки",
            "Фейерверки",
            "Облака зелий",
            "Кристаллы Края",
            "Зомби",
            "Скелеты",
            "Криперы",
            "Летучие мыши",
            "Куры"
    };

    // ------------------------------------------------------------- вопрос

    /** Рисовать ли эту сущность. Зовётся из миксина на каждый кадр. */
    public static boolean skip(Entity entity) {
        working = true;

        if (entity == null) return false;

        Config cfg = Config.get();
        if (!cfg.fpsPlus) return false;

        Minecraft mc = Minecraft.getInstance();
        if (entity == mc.player) return false;
        if (entity == Freecam.camera()) return false;

        return hidden.contains(entity.getId());
    }

    /**
     * Можно ли добавить ещё одну частицу. Зовётся из миксина на каждую.
     *
     * Полсотни динамита разом — десятки тысяч частиц за тик, и кадр встаёт
     * колом именно на них. Первые сотни рисуют взрыв целиком, остальные
     * ложатся поверх и видны только счётчику кадров.
     */
    public static boolean allowParticle() {
        Config cfg = Config.get();
        if (!cfg.fpsPlus || !cfg.fpsExplosions) return true;
        if (cfg.fpsParticleBudget <= 0) return true;

        if (particlesThisTick >= cfg.fpsParticleBudget) {
            particlesDropped++;
            return false;
        }

        particlesThisTick++;
        return true;
    }

    /**
     * Звук взрыва. Когда их десяток разом, они не складываются во что-то
     * осмысленное — только рвут микшер и подтормаживают. Лишние глушим.
     */
    @SubscribeEvent
    public static void onSound(net.minecraftforge.client.event.sound.PlaySoundEvent event) {
        Config cfg = Config.get();
        if (!cfg.fpsPlus || !cfg.fpsExplosions || cfg.fpsBoomsAtOnce <= 0) return;

        var sound = event.getSound();
        if (sound == null) return;

        String name = sound.getLocation() == null ? "" : sound.getLocation().getPath();
        if (name.indexOf("explode") < 0 && name.indexOf("explosion") < 0) return;

        long now = System.currentTimeMillis();
        while (!booms.isEmpty() && now - booms.peekFirst() > 400L) booms.pollFirst();

        if (booms.size() >= cfg.fpsBoomsAtOnce) {
            event.setSound(null);
            return;
        }

        booms.addLast(now);
    }

    // --------------------------------------------------------------- тик

    @SubscribeEvent
    public static void onTick(TickEvent.ClientTickEvent event) {
        if (event.phase != TickEvent.Phase.END) return;

        Minecraft mc = Minecraft.getInstance();
        Config cfg = Config.get();

        particles(mc, cfg);

        // бюджет частиц начинается заново каждый тик
        particlesThisTick = 0;

        if (!cfg.fpsPlus || mc.level == null || mc.player == null) {
            if (!hidden.isEmpty()) hidden = new HashSet<>();
            hiddenCount = tntSeen = mobsSeen = itemsSeen = orbsSeen = 0;
            return;
        }

        var fresh = new HashSet<Integer>();

        var tnt = new ArrayList<Entity>();
        var mobs = new ArrayList<Entity>();
        var items = new ArrayList<Entity>();
        var orbs = new ArrayList<Entity>();

        double rangeSq = cfg.fpsRange > 0 ? (double) cfg.fpsRange * cfg.fpsRange : 0;

        for (Entity entity : mc.level.entitiesForRendering()) {
            if (entity == mc.player || entity == Freecam.camera()) continue;

            boolean player = entity instanceof Player;
            if (player && !cfg.fpsHidePlayers) continue;

            // список «никогда не рисовать»
            if (!cfg.fpsNever.isEmpty()) {
                String id = idOf(entity);
                if (id != null && cfg.fpsNever.contains(id)) {
                    fresh.add(entity.getId());
                    continue;
                }
            }

            // слишком далеко
            if (rangeSq > 0 && entity.distanceToSqr(mc.player) > rangeSq) {
                fresh.add(entity.getId());
                continue;
            }

            if (entity instanceof PrimedTnt) tnt.add(entity);
            else if (entity instanceof ItemEntity) items.add(entity);
            else if (entity instanceof ExperienceOrb) orbs.add(entity);
            else if (entity instanceof LivingEntity && !player) mobs.add(entity);
        }

        tntSeen = tnt.size();
        mobsSeen = mobs.size();
        itemsSeen = items.size();
        orbsSeen = orbs.size();

        keepNearest(fresh, tnt, cfg.fpsTntOver, mc);
        keepNearest(fresh, mobs, cfg.fpsMobsOver, mc);
        keepNearest(fresh, items, cfg.fpsItemsOver, mc);
        keepNearest(fresh, orbs, cfg.fpsOrbsOver, mc);

        hidden = fresh;
        hiddenCount = fresh.size();
    }

    /**
     * Пока их меньше порога — не трогаем вовсе. Стало больше — оставляем
     * ближайшие, остальные прячем: так пропадает именно то, что всё равно
     * сливается в кашу на горизонте.
     */
    private static void keepNearest(Set<Integer> out, List<Entity> list, int limit, Minecraft mc) {
        if (limit <= 0 || list.size() <= limit) return;

        list.sort((a, b) -> Double.compare(
                a.distanceToSqr(mc.player), b.distanceToSqr(mc.player)));

        for (int i = limit; i < list.size(); i++) out.add(list.get(i).getId());
    }

    private static String idOf(Entity entity) {
        try {
            EntityType<?> type = entity.getType();
            var key = BuiltInRegistries.ENTITY_TYPE.getKey(type);
            return key == null ? null : key.toString();
        } catch (Throwable ignored) { return null; }
    }

    /**
     * Частицы — самая дешёвая правка: ванильная настройка уже умеет их
     * урезать, мы только переключаем её и возвращаем обратно, когда FPS+
     * выключают.
     */
    private static void particles(Minecraft mc, Config cfg) {
        if (mc.options == null) return;

        boolean want = cfg.fpsPlus && cfg.fpsLessParticles;

        if (want) {
            if (savedParticles == null) savedParticles = mc.options.particles().get();
            if (mc.options.particles().get() != ParticleStatus.MINIMAL)
                mc.options.particles().set(ParticleStatus.MINIMAL);
        }
        else if (savedParticles != null) {
            mc.options.particles().set(savedParticles);
            savedParticles = null;
        }
    }

    // ------------------------------------------------------------ для окна

    public static String state() {
        if (!Config.get().fpsPlus) return "выключен";
        if (!working) return "не включился — отсечение недоступно";
        if (hiddenCount == 0) return "включён, прятать нечего";
        return "включён, скрыто " + hiddenCount;
    }

    public static String boomText(int value) {
        return value <= 0 ? "не ограничивать" : value + " разом";
    }

    public static String budgetText(int value) {
        return value <= 0 ? "не ограничивать" : value + " за тик";
    }

    public static String droppedText() {
        return particlesDropped == 0 ? "" : "частиц отброшено: " + particlesDropped;
    }

    public static String counts() {
        return "динамит " + tntSeen + " · мобы " + mobsSeen
             + " · предметы " + itemsSeen + " · опыт " + orbsSeen;
    }

    public static String limitText(int value) {
        return value <= 0 ? "не прятать" : "больше " + value;
    }

    public static String rangeText(int value) {
        return value <= 0 ? "без ограничения" : value + " бл.";
    }

    /** Переключает вид в списке «никогда не рисовать». */
    public static void flipType(String id) {
        Config cfg = Config.get();
        if (cfg.fpsNever.contains(id)) cfg.fpsNever.remove(id);
        else cfg.fpsNever.add(id);
        Config.save();
    }

    public static boolean typeOff(String id) {
        return Config.get().fpsNever.contains(id);
    }
}
