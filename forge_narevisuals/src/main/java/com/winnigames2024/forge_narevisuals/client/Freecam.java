package com.winnigames2024.forge_narevisuals.client;

import com.mojang.blaze3d.platform.InputConstants;
import com.winnigames2024.forge_narevisuals.Config;
import com.winnigames2024.forge_narevisuals.Forge_narevisuals;
import com.winnigames2024.forge_narevisuals.Launch;
import net.minecraft.client.Minecraft;
import net.minecraft.client.player.LocalPlayer;
import net.minecraft.core.BlockPos;
import net.minecraft.resources.ResourceLocation;
import net.minecraft.server.MinecraftServer;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.world.entity.ai.attributes.AttributeInstance;
import net.minecraft.world.entity.ai.attributes.AttributeModifier;
import net.minecraft.world.entity.ai.attributes.Attributes;
import net.minecraft.core.Holder;
import net.minecraft.world.entity.ai.attributes.Attribute;
import net.minecraft.world.entity.Entity;
import net.minecraft.world.entity.decoration.ArmorStand;
import net.minecraft.world.entity.player.Input;
import net.minecraft.world.entity.projectile.ProjectileUtil;
import net.minecraft.world.level.ClipContext;
import net.minecraft.world.phys.AABB;
import net.minecraft.world.phys.BlockHitResult;
import net.minecraft.world.phys.EntityHitResult;
import net.minecraft.world.phys.HitResult;
import net.minecraft.world.phys.Vec3;
import net.minecraftforge.api.distmarker.Dist;
import net.minecraftforge.client.event.MovementInputUpdateEvent;
import net.minecraftforge.client.event.ViewportEvent;
import net.minecraftforge.event.TickEvent;
import net.minecraftforge.eventbus.api.SubscribeEvent;
import net.minecraftforge.fml.common.Mod;

/**
 * Свободная камера.
 *
 * Камера переносится на невидимое существо, которое никуда не добавлено в мир:
 * оно никем не видно, ничего не тикает и не отправляется на сервер. Тело игрока
 * остаётся на месте — ввод движения ему на время отключается, а поворот мыши
 * забирается у игрока и отдаётся камере.
 *
 * Пока камера свободна, цель под прицелом считается от неё и на заданную
 * дальность, так что ломать и бить можно оттуда, куда смотрит камера.
 * Разрешит ли это сервер — решает сервер: клиент лишь отправляет обычные
 * пакеты действия.
 */
@Mod.EventBusSubscriber(modid = Forge_narevisuals.MODID, value = Dist.CLIENT)
public final class Freecam {

    private static Entity ghost;
    private static boolean active;

    /** Поворот тела игрока, который мы держим неизменным, пока камера свободна. */
    private static float savedYaw, savedPitch;

    /** Поворот самой камеры — её ведёт мышь. */
    private static float yaw, pitch;

    private static double speedScale = 1.0;

    private Freecam() { }

    public static boolean active() {
        return active;
    }

    public static Entity camera() {
        return ghost;
    }

    // ------------------------------------------------------------ включение

    public static void toggle() {
        if (active) stop(); else start();
    }

    public static void start() {
        Minecraft mc = Minecraft.getInstance();
        if (mc.level == null || mc.player == null) return;
        if (active) return;

        LocalPlayer player = mc.player;

        ArmorStand stand = new ArmorStand(mc.level, player.getX(), player.getY(), player.getZ());
        stand.setInvisible(true);
        stand.setNoGravity(true);
        stand.noPhysics = true;
        stand.setSilent(true);

        // ставим так, чтобы глаза камеры оказались там же, где глаза игрока
        double eye = stand.getEyeHeight();
        stand.setPos(player.getX(), player.getEyeY() - eye, player.getZ());
        stand.setOldPosAndRot();
        stand.setYRot(player.getYRot());
        stand.setXRot(player.getXRot());
        stand.yRotO = stand.getYRot();
        stand.xRotO = stand.getXRot();

        ghost = stand;
        savedYaw = player.getYRot();
        savedPitch = player.getXRot();
        yaw = savedYaw;
        pitch = savedPitch;
        speedScale = 1.0;
        active = true;

        mc.setCameraEntity(stand);
        Forge_narevisuals.LOGGER.info("{}: свободная камера включена", Forge_narevisuals.NAME);
    }

    public static void stop() {
        Minecraft mc = Minecraft.getInstance();
        active = false;
        ghost = null;

        if (mc.player != null) {
            mc.setCameraEntity(mc.player);
            mc.player.setYRot(savedYaw);
            mc.player.setXRot(savedPitch);
            mc.player.yRotO = savedYaw;
            mc.player.xRotO = savedPitch;
        } else {
            mc.setCameraEntity(null);
        }
    }

    // ------------------------------------------------------------ поворот

    /**
     * Поворот камеры. Событие приходит из самой настройки камеры, и игра тут же
     * применяет наши углы — поэтому камера крутится независимо от того, что
     * происходит с телом игрока.
     *
     * Мышь по-прежнему крутит игрока: мы забираем набежавший поворот себе и
     * возвращаем телу прежние углы, так что со стороны оно стоит неподвижно.
     */
    @SubscribeEvent
    public static void onCameraAngles(ViewportEvent.ComputeCameraAngles event) {
        if (!active || ghost == null) return;

        Minecraft mc = Minecraft.getInstance();
        if (mc.player == null) return;

        float dYaw = mc.player.getYRot() - savedYaw;
        float dPitch = mc.player.getXRot() - savedPitch;

        if (dYaw != 0f || dPitch != 0f) {
            yaw += dYaw;
            pitch = Math.max(-90f, Math.min(90f, pitch + dPitch));

            mc.player.setYRot(savedYaw);
            mc.player.setXRot(savedPitch);
            mc.player.yRotO = savedYaw;
            mc.player.xRotO = savedPitch;
            mc.player.setYHeadRot(savedYaw);
            mc.player.yHeadRotO = savedYaw;
            mc.player.yBodyRot = savedYaw;
            mc.player.yBodyRotO = savedYaw;
        }

        // существо-камера должно смотреть туда же: от него считается цель под прицелом
        ghost.setYRot(yaw);
        ghost.setXRot(pitch);
        ghost.yRotO = yaw;
        ghost.xRotO = pitch;

        event.setYaw(yaw);
        event.setPitch(pitch);
    }

    // ------------------------------------------------------------ полёт

    @SubscribeEvent
    public static void onTick(TickEvent.ClientTickEvent.Post event) {
        Minecraft mc = Minecraft.getInstance();

        if (active && (mc.level == null || mc.player == null)) {
            stop();
            return;
        }
        if (!active || ghost == null) return;

        // мир сменился — камера из старого мира больше не годится
        if (ghost.level() != mc.level) { stop(); return; }

        ghost.setOldPosAndRot();

        if (mc.screen == null) {
            long win = mc.getWindow().getWindow();
            Config cfg = Config.get();

            double speed = cfg.freecamSpeed * speedScale;
            if (down(win, mc.options.keySprint)) speed *= 2.2;

            float yaw = ghost.getYRot();
            double sin = Math.sin(Math.toRadians(yaw));
            double cos = Math.cos(Math.toRadians(yaw));

            double fx = 0, fz = 0, fy = 0;
            if (down(win, mc.options.keyUp))    { fx -= sin; fz += cos; }
            if (down(win, mc.options.keyDown))  { fx += sin; fz -= cos; }
            if (down(win, mc.options.keyLeft))  { fx += cos; fz += sin; }
            if (down(win, mc.options.keyRight)) { fx -= cos; fz -= sin; }
            if (down(win, mc.options.keyJump))        fy += 1;
            if (down(win, mc.options.keyShift))       fy -= 1;

            double len = Math.sqrt(fx * fx + fz * fz);
            if (len > 1e-4) { fx /= len; fz /= len; }

            if (fx != 0 || fy != 0 || fz != 0) {
                ghost.setPos(ghost.getX() + fx * speed,
                             ghost.getY() + fy * speed,
                             ghost.getZ() + fz * speed);
            }
        }

    }

    private static boolean down(long window, net.minecraft.client.KeyMapping key) {
        InputConstants.Key k = key.getKey();
        if (k.getType() != InputConstants.Type.KEYSYM) return key.isDown();
        int code = k.getValue();
        return code != InputConstants.UNKNOWN.getValue() && InputConstants.isKeyDown(window, code);
    }

    /** Колесо мыши меняет скорость полёта. */
    public static boolean scrolled(double delta) {
        if (!active) return false;
        speedScale = Math.max(0.1, Math.min(8.0, speedScale * (delta > 0 ? 1.2 : 1 / 1.2)));
        return true;
    }

    public static String speedText() {
        return String.format("%.1f", Config.get().freecamSpeed * speedScale);
    }

    // ------------------------------------------------------------ ввод игрока

    @SubscribeEvent
    public static void onMovementInput(MovementInputUpdateEvent event) {
        if (!active) return;
        if (Minecraft.getInstance().player != event.getEntity()) return;

        event.getInput().keyPresses = Input.EMPTY;
        event.getInput().leftImpulse = 0f;
        event.getInput().forwardImpulse = 0f;
    }

    // ------------------------------------------------------------ дальность

    /**
     * Пересчёт цели под прицелом. Зовётся миксином сразу после ванильного
     * расчёта — то есть в том же тике, но раньше, чем игра обработает удар
     * и ломание, так что наш результат уже не затрут.
     *
     * Тело самого игрока из списка целей выкинуто: ванильный расчёт считает
     * от камеры и потому видит игрока обычной целью, а удар по себе сервер
     * считает ошибкой и обрывает связь.
     */
    public static void repick(float partialTick) {
        if (!active || ghost == null) return;

        Minecraft mc = Minecraft.getInstance();
        if (mc.level == null || mc.player == null) return;

        Config cfg = Config.get();
        double reach = Math.max(cfg.reachBlocks, cfg.reachEntities);
        if (reach <= 0) reach = cfg.freecamReach;
        if (reach <= 0)
            reach = Math.max(mc.player.blockInteractionRange(), mc.player.entityInteractionRange());

        Vec3 from = ghost.getEyePosition(partialTick);
        Vec3 look = ghost.getViewVector(partialTick);
        Vec3 to = from.add(look.x * reach, look.y * reach, look.z * reach);

        HitResult block = mc.level.clip(new ClipContext(
                from, to, ClipContext.Block.OUTLINE, ClipContext.Fluid.NONE, mc.player));

        double limit = block.getType() == HitResult.Type.MISS
                ? reach * reach
                : block.getLocation().distanceToSqr(from);

        AABB area = new AABB(from, to).inflate(1.0);
        EntityHitResult entity = ProjectileUtil.getEntityHitResult(
                mc.player, from, to, area,
                e -> !e.isSpectator() && e.isPickable() && e != mc.player && e != ghost,
                limit);

        if (entity != null) {
            mc.hitResult = entity;
            mc.crosshairPickEntity = entity.getEntity();
        } else {
            mc.hitResult = block;
            mc.crosshairPickEntity = null;
        }
    }

    // ------------------------------------------------------------ подсказка

    public static void drawHint(net.minecraft.client.gui.GuiGraphics g, Minecraft mc, int w, int h) {
        if (!active || !Launch.visuals() || !Config.get().freecamHint) return;

        String key = NareKeys.TOGGLE_FREECAM.getTranslatedKeyMessage().getString();
        String head = "Свободная камера";
        String tail = "  ·  " + key + " — выйти  ·  WASD, пробел, Shift  ·  колесо — " + speedText();

        // прямо под прицелом: сверху и снизу стоят элементы HUD, там подсказка
        // налезала на карточку с координатами и на индикатор клавиш
        int tw = mc.font.width(head) + mc.font.width(tail) + 16;
        int x = (w - tw) / 2;
        int y = h / 2 + 18;

        NareTheme.roundRect(g, x, y, tw, 15, 4, 0xA00D1018);
        NareTheme.outline(g, x, y, tw, 15, 4, 0x2EFFFFFF);
        g.drawString(mc.font, head, x + 8, y + 4, NareTheme.TEXT, false);
        g.drawString(mc.font, tail, x + 8 + mc.font.width(head), y + 4, NareTheme.DIM, false);
    }
}
