package com.winnigames2024.forge_narevisuals.client;

import com.winnigames2024.forge_narevisuals.Config;
import com.winnigames2024.forge_narevisuals.Forge_narevisuals;
import com.winnigames2024.forge_narevisuals.Launch;
import net.minecraft.client.Minecraft;
import net.minecraft.core.BlockPos;
import net.minecraft.core.Direction;
import net.minecraft.core.Holder;
import net.minecraft.resources.ResourceLocation;
import net.minecraft.server.MinecraftServer;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.world.InteractionHand;
import net.minecraft.world.entity.Entity;
import net.minecraft.world.entity.LivingEntity;
import net.minecraft.world.entity.ai.attributes.Attribute;
import net.minecraft.world.entity.ai.attributes.AttributeInstance;
import net.minecraft.world.entity.ai.attributes.AttributeModifier;
import net.minecraft.world.entity.ai.attributes.Attributes;
import net.minecraft.world.phys.AABB;
import net.minecraft.world.phys.BlockHitResult;
import net.minecraft.world.phys.EntityHitResult;
import net.minecraft.world.phys.HitResult;
import net.minecraft.world.phys.Vec3;
import net.minecraftforge.api.distmarker.Dist;
import net.minecraftforge.client.event.InputEvent;
import net.minecraftforge.event.TickEvent;
import net.minecraftforge.eventbus.api.SubscribeEvent;
import net.minecraftforge.fml.common.Mod;

import java.util.ArrayList;
import java.util.List;
import java.util.UUID;

/**
 * Дальность действий, работа по области и размер персонажа.
 *
 * Важное про дальность: сколько игрок достаёт, решает сервер по атрибутам
 * block_interaction_range и entity_interaction_range. Клиент может лишь
 * прицелиться дальше — поэтому раньше и выходило, что блок ломается «только
 * с одной стороны»: с той, где ближе стоит тело игрока. В одиночной игре
 * сервер наш, и атрибуты поднимаются по-настоящему. На чужом сервере это
 * может сделать только он сам.
 */
@Mod.EventBusSubscriber(modid = Forge_narevisuals.MODID, value = Dist.CLIENT)
public final class NareReach {

    private static final ResourceLocation BLOCK_ID = id("reach_blocks");
    private static final ResourceLocation ENTITY_ID = id("reach_entities");
    private static final ResourceLocation SCALE_ID = id("player_scale");

    /** Что мы в последний раз отправили на сервер — чтобы не слать каждый тик. */
    private static double sentBlocks = -1, sentEntities = -1, sentScale = -1;

    private static int breakCooldown;
    private static int attackCooldown;

    private NareReach() { }

    private static ResourceLocation id(String path) {
        return ResourceLocation.fromNamespaceAndPath(Forge_narevisuals.MODID, path);
    }

    // ------------------------------------------------------------------ тик

    @SubscribeEvent
    public static void onTick(TickEvent.ClientTickEvent.Post event) {
        Minecraft mc = Minecraft.getInstance();
        if (mc.player == null || mc.level == null) { sentBlocks = sentEntities = sentScale = -1; return; }

        Config cfg = Config.get();
        boolean on = Launch.visuals();

        sync(mc, cfg, on);

        if (breakCooldown > 0) breakCooldown--;
        if (attackCooldown > 0) attackCooldown--;

        if (on) areaBreak(mc, cfg);
    }

    // ------------------------------------------------------- атрибуты игрока

    /** Какая дальность сейчас нужна. 0 — обычная, трогать ничего не надо. */
    private static double wantBlocks(Config cfg) {
        boolean free = Freecam.active();
        if (!cfg.reachAlways && !free) return 0;

        double want = cfg.reachBlocks;
        // свой ползунок не трогали — берём старую «дальность действий» камеры
        if (want <= 0 && free) want = cfg.freecamReach;
        return Math.max(0, want);
    }

    private static double wantEntities(Config cfg) {
        boolean free = Freecam.active();
        if (!cfg.reachAlways && !free) return 0;

        double want = cfg.reachEntities;
        if (want <= 0 && free) want = cfg.freecamReach;
        return Math.max(0, want);
    }

    private static void sync(Minecraft mc, Config cfg, boolean on) {
        double blocks = on ? wantBlocks(cfg) : 0;
        double entities = on ? wantEntities(cfg) : 0;
        double scale = on ? Math.max(0.1, cfg.playerScale / 100.0) : 1.0;

        if (blocks == sentBlocks && entities == sentEntities && scale == sentScale) return;

        sentBlocks = blocks;
        sentEntities = entities;
        sentScale = scale;

        // на клиенте ставим сразу — иначе игра сама не даст замахнуться,
        // пока с сервера не придёт обновление
        set(mc.player.getAttribute(Attributes.BLOCK_INTERACTION_RANGE), BLOCK_ID, blocks);
        set(mc.player.getAttribute(Attributes.ENTITY_INTERACTION_RANGE), ENTITY_ID, entities);

        MinecraftServer server = mc.getSingleplayerServer();
        if (server == null) return;

        UUID who = mc.player.getUUID();
        server.execute(() -> {
            ServerPlayer player = server.getPlayerList().getPlayer(who);
            if (player == null) return;

            set(player.getAttribute(Attributes.BLOCK_INTERACTION_RANGE), BLOCK_ID, blocks);
            set(player.getAttribute(Attributes.ENTITY_INTERACTION_RANGE), ENTITY_ID, entities);
            scale(player, scale);
        });
    }

    /** Доводит атрибут до нужного значения добавкой поверх базового. */
    private static void set(AttributeInstance instance, ResourceLocation marker, double want) {
        if (instance == null) return;

        instance.removeModifier(marker);
        if (want <= 0) return;

        double bonus = want - instance.getBaseValue();
        if (bonus <= 0) return;

        instance.addTransientModifier(new AttributeModifier(
                marker, bonus, AttributeModifier.Operation.ADD_VALUE));
    }

    private static void scale(ServerPlayer player, double want) {
        AttributeInstance instance = player.getAttribute(Attributes.SCALE);
        if (instance == null) return;

        instance.removeModifier(SCALE_ID);
        if (Math.abs(want - 1.0) < 0.01) return;

        instance.addTransientModifier(new AttributeModifier(
                SCALE_ID, want - instance.getBaseValue(), AttributeModifier.Operation.ADD_VALUE));
    }

    // ----------------------------------------------------- ломание областью

    /**
     * Пока зажата кнопка удара, вместе с блоком под прицелом сносим и соседей.
     * Плоскость берём перпендикулярно той грани, на которую смотрим — так же,
     * как работают «молоты» в модах.
     */
    private static void areaBreak(Minecraft mc, Config cfg) {
        int size = Math.max(1, cfg.breakSize);
        int depth = Math.max(1, cfg.breakDepth);
        if (size <= 1 && depth <= 1) return;
        if (breakCooldown > 0) return;

        if (mc.screen != null || mc.player == null || mc.level == null) return;
        if (!mc.options.keyAttack.isDown()) return;
        if (!(mc.hitResult instanceof BlockHitResult hit)) return;
        if (hit.getType() != HitResult.Type.BLOCK) return;

        Direction face = hit.getDirection();
        BlockPos center = hit.getBlockPos();
        int r = (size - 1) / 2;

        List<BlockPos> picked = new ArrayList<>();
        for (int a = -r; a <= r; a++) {
            for (int b = -r; b <= r; b++) {
                for (int d = 0; d < depth; d++) {
                    BlockPos pos = shift(center, face, a, b, d);
                    if (pos.equals(center)) continue;
                    if (mc.level.getBlockState(pos).isAir()) continue;

                    picked.add(pos);
                    if (picked.size() >= 96) break;
                }
            }
        }

        if (picked.isEmpty()) { breakCooldown = 4; return; }

        if (NareNet.plugged()) {
            // на сервере ломает он сам — иначе блоки исчезнут только у нас
            // на экране, и мы упрёмся в стену, которой не видно
            NareNet.breakArea(picked);
        } else {
            for (BlockPos pos : picked) mc.gameMode.destroyBlock(pos);
        }

        breakCooldown = 4;
    }

    /** Сдвиг от центра: a и b — вдоль грани, d — вглубь блока. */
    private static BlockPos shift(BlockPos center, Direction face, int a, int b, int d) {
        BlockPos deep = center.relative(face.getOpposite(), d);
        return switch (face.getAxis()) {
            case Y -> deep.offset(a, 0, b);
            case X -> deep.offset(0, a, b);
            case Z -> deep.offset(a, b, 0);
        };
    }

    // -------------------------------------------------------- удар областью

    /**
     * Удар по всем, кто рядом с целью. Сервер каждый такой удар проверяет сам,
     * а любая защита от читов считает это подозрительным — на общих серверах
     * лучше держать выключенным.
     */
    @SubscribeEvent
    public static void onAttackKey(InputEvent.InteractionKeyMappingTriggered event) {
        if (!event.isAttack() || !Launch.visuals()) return;

        Config cfg = Config.get();
        double radius = Math.max(0, cfg.attackArea);
        if (radius <= 0 || attackCooldown > 0) return;

        Minecraft mc = Minecraft.getInstance();
        if (mc.player == null || mc.level == null || mc.gameMode == null) return;

        Vec3 at = centreOfSwing(mc);
        if (at == null) return;

        Entity main = mc.hitResult instanceof EntityHitResult e ? e.getEntity() : null;

        List<Entity> targets = new ArrayList<>();
        for (Entity other : mc.level.getEntities(mc.player,
                new AABB(at, at).inflate(radius), e -> e instanceof LivingEntity)) {
            if (other == main || other == mc.player || other == Freecam.camera()) continue;
            if (other.isSpectator() || !other.isAlive()) continue;
            if (other.position().distanceTo(at) > radius) continue;
            targets.add(other);
        }

        if (targets.isEmpty()) return;

        targets.sort((x, y) -> Double.compare(x.position().distanceTo(at), y.position().distanceTo(at)));

        int limit = Math.min(targets.size(), 8);
        for (int i = 0; i < limit; i++) {
            mc.gameMode.attack(mc.player, targets.get(i));
            mc.player.swing(InteractionHand.MAIN_HAND);
        }

        attackCooldown = 4;
    }

    /** Вокруг чего бьём: вокруг цели под прицелом, а если её нет — вокруг взгляда. */
    private static Vec3 centreOfSwing(Minecraft mc) {
        if (mc.hitResult == null || mc.hitResult.getType() == HitResult.Type.MISS) return null;
        return mc.hitResult.getLocation();
    }

    // ------------------------------------------------------------- для окна

    public static String reachText(int value) {
        return value <= 0 ? "как обычно" : value + " бл.";
    }

    public static Holder<Attribute> blockRange() {
        return Attributes.BLOCK_INTERACTION_RANGE;
    }
}
