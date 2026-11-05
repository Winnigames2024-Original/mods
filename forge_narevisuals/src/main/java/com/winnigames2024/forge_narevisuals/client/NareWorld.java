package com.winnigames2024.forge_narevisuals.client;

import com.mojang.blaze3d.vertex.PoseStack;
import com.mojang.blaze3d.vertex.VertexConsumer;
import com.winnigames2024.forge_narevisuals.Config;
import com.winnigames2024.forge_narevisuals.Forge_narevisuals;
import com.winnigames2024.forge_narevisuals.Launch;
import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.Font;
import net.minecraft.client.renderer.MultiBufferSource;
import net.minecraft.client.renderer.RenderType;
import net.minecraft.client.renderer.ShapeRenderer;
import net.minecraft.client.renderer.entity.state.PlayerRenderState;
import net.minecraft.core.BlockPos;
import net.minecraft.world.entity.Entity;
import net.minecraft.world.entity.player.Player;
import net.minecraft.world.level.block.state.BlockState;
import net.minecraft.world.phys.AABB;
import net.minecraft.world.phys.BlockHitResult;
import net.minecraft.world.phys.Vec3;
import net.minecraft.world.phys.shapes.CollisionContext;
import net.minecraft.world.phys.shapes.VoxelShape;
import net.minecraftforge.api.distmarker.Dist;
import net.minecraftforge.client.event.RenderHighlightEvent;
import net.minecraftforge.client.event.RenderLivingEvent;
import net.minecraftforge.eventbus.api.SubscribeEvent;
import net.minecraftforge.fml.common.Mod;
import org.joml.Matrix4f;

import java.util.ArrayList;
import java.util.List;

/**
 * То, что рисуется в самом мире: рамка вокруг игроков и своя подсветка блока
 * под прицелом.
 *
 * В Forge 1.21.4 события отрисовки мира (RenderLevelStageEvent) больше нет —
 * вместо него кадр собирается графом проходов. Поэтому рамка рисуется там же,
 * где рисуется сама модель игрока: RenderLivingEvent.Post. Матрица в этот
 * момент стоит в ногах существа и не повёрнута, так что коробку можно строить
 * прямо в локальных координатах.
 */
@Mod.EventBusSubscriber(modid = Forge_narevisuals.MODID, value = Dist.CLIENT)
public final class NareWorld {

    private NareWorld() { }

    // ------------------------------------------------------------ игроки

    @SubscribeEvent
    public static void onRenderLiving(RenderLivingEvent.Post<?, ?, ?> event) {
        if (!Launch.visuals()) return;

        Config cfg = Config.get();
        if (!cfg.playerEsp) return;

        Minecraft mc = Minecraft.getInstance();
        if (mc.player == null || mc.level == null) return;

        // существа кроме игроков: только рамка, без подписи — имени и здоровья
        // в состоянии отрисовки нет, а искать существо по координатам каждый кадр дорого
        if (!(event.getState() instanceof PlayerRenderState state)) {
            if (!cfg.espMobs) return;
            drawPlainBox(event, cfg.espColor);
            return;
        }

        // себя не обводим, пока камера на своём же теле
        if (state.id == mc.player.getId() && mc.getCameraEntity() == mc.player) return;

        double range = cfg.espRange;
        if (state.distanceToCameraSq > range * range) return;

        Entity entity = mc.level.getEntity(state.id);
        Player player = entity instanceof Player p ? p : null;

        float health = player != null ? player.getHealth() : 20f;
        float maxHealth = player != null ? Math.max(1f, player.getMaxHealth()) : 20f;
        float ratio = Math.max(0f, Math.min(1f, health / maxHealth));

        int color = cfg.espHealthColor ? healthColor(ratio) : cfg.espColor;

        float w = Math.max(0.3f, state.boundingBoxWidth);
        float h = Math.max(0.5f, state.boundingBoxHeight);

        PoseStack ps = event.getPoseStack();
        MultiBufferSource buffers = event.getMultiBufferSource();

        AABB box = new AABB(-w / 2f, 0, -w / 2f, w / 2f, h, w / 2f).inflate(0.04);

        VertexConsumer lines = buffers.getBuffer(RenderType.lines());
        ShapeRenderer.renderLineBox(ps, lines, box,
                red(color), green(color), blue(color), 0.9f);

        // полоска здоровья по левой грани рамки
        if (cfg.espHealth && player != null) {
            VertexConsumer bar = buffers.getBuffer(RenderType.lines());
            double x = -w / 2f - 0.08;
            double top = h * ratio;
            ShapeRenderer.renderLineBox(ps, bar,
                    new AABB(x, 0, -w / 2f - 0.04, x, top, -w / 2f - 0.04),
                    red(color), green(color), blue(color), 1.0f);
        }

        List<String> rows = lines(mc, cfg, state, player, Math.sqrt(state.distanceToCameraSq));
        if (!rows.isEmpty()) {
            drawLabel(mc, ps, buffers, rows, h, color, ratio);
        }
    }

    private static void drawPlainBox(RenderLivingEvent.Post<?, ?, ?> event, int color) {
        var state = event.getState();

        double range = Config.get().espRange;
        if (state.distanceToCameraSq > range * range) return;

        float w = Math.max(0.2f, state.boundingBoxWidth);
        float h = Math.max(0.2f, state.boundingBoxHeight);

        VertexConsumer lines = event.getMultiBufferSource().getBuffer(RenderType.lines());
        ShapeRenderer.renderLineBox(event.getPoseStack(), lines,
                new AABB(-w / 2f, 0, -w / 2f, w / 2f, h, w / 2f).inflate(0.03),
                red(color), green(color), blue(color), 0.7f);
    }

    /**
     * Что пишем над головой. Имя, здоровье и броню сервер присылает про всех,
     * а опыт и сытость — только про вас самих: такие пакеты другим игрокам
     * просто не уходят, выдумывать числа мы не будем.
     */
    private static List<String> lines(Minecraft mc, Config cfg, PlayerRenderState state,
                                      Player player, double distance) {
        List<String> out = new ArrayList<>();

        if (cfg.espName) out.add(state.name);

        if (cfg.espHealth && player != null) {
            float health = player.getHealth();
            float max = Math.max(1f, player.getMaxHealth());
            String row = round(health) + "/" + round(max);
            if (cfg.espBar) row = bar(health / max) + "  " + row;
            out.add(row);
        }

        StringBuilder extra = new StringBuilder();

        if (cfg.espArmor && player != null && player.getArmorValue() > 0)
            extra.append("броня ").append(player.getArmorValue());

        // еду и опыт игра знает только про своего игрока
        if (cfg.espExtra && player != null && player == mc.player) {
            if (extra.length() > 0) extra.append("  ");
            extra.append("опыт ").append(player.experienceLevel)
                 .append("  еда ").append(player.getFoodData().getFoodLevel());
        }

        if (cfg.espDistance) {
            if (extra.length() > 0) extra.append("  ");
            extra.append(Math.round(distance)).append('м');
        }

        if (extra.length() > 0) out.add(extra.toString());
        return out;
    }

    private static int round(float value) {
        return (int) Math.ceil(value);
    }

    /** Полоска здоровья десятью делениями — шрифт игры рисует их ровно. */
    private static String bar(float ratio) {
        int full = Math.max(0, Math.min(10, Math.round(ratio * 10)));
        StringBuilder sb = new StringBuilder(10);
        for (int i = 0; i < 10; i++) sb.append(i < full ? '|' : '.');
        return sb.toString();
    }

    private static void drawLabel(Minecraft mc, PoseStack ps, MultiBufferSource buffers,
                                  List<String> lines, float height, int color, float ratio) {
        Font font = mc.font;

        ps.pushPose();
        ps.translate(0.0, height + 0.55 + (lines.size() - 1) * 0.22, 0.0);
        ps.mulPose(mc.getEntityRenderDispatcher().cameraOrientation());
        ps.scale(-0.025f, -0.025f, 0.025f);

        Matrix4f pose = ps.last().pose();

        for (int i = 0; i < lines.size(); i++) {
            String text = lines.get(i);
            float x = -font.width(text) / 2f;

            // имя — цветом рамки, остальное спокойнее, чтобы не рябило
            int tint = i == 0 ? color : (i == 1 ? healthColor(ratio) : 0xFFB8C2D6);

            font.drawInBatch(text, x, i * 10f, tint, false, pose, buffers,
                    Font.DisplayMode.SEE_THROUGH, 0x40000000, 0xF000F0);
        }

        ps.popPose();
    }

    private static int healthColor(float ratio) {
        // от красного (пусто) к зелёному (полное), жёлтый посередине
        int r = ratio < 0.5f ? 255 : (int) (255 * (1f - (ratio - 0.5f) * 2f));
        int g = ratio > 0.5f ? 255 : (int) (255 * ratio * 2f);
        return 0xFF000000 | (clamp(r) << 16) | (clamp(g) << 8) | 0x30;
    }

    private static int clamp(int v) {
        return v < 0 ? 0 : v > 255 ? 255 : v;
    }

    private static float red(int argb)   { return ((argb >> 16) & 0xFF) / 255f; }
    private static float green(int argb) { return ((argb >> 8) & 0xFF) / 255f; }
    private static float blue(int argb)  { return (argb & 0xFF) / 255f; }

    // ------------------------------------------------------------ блок под прицелом

    @SubscribeEvent
    public static void onBlockHighlight(RenderHighlightEvent.Block event) {
        if (!Launch.visuals()) return;

        Config cfg = Config.get();
        if (!cfg.blockHighlight) return;

        Minecraft mc = Minecraft.getInstance();
        if (mc.level == null) return;

        BlockHitResult hit = event.getTarget();
        BlockPos pos = hit.getBlockPos();
        BlockState state = mc.level.getBlockState(pos);
        if (state.isAir()) return;

        Vec3 cam = event.getCamera().getPosition();
        VoxelShape shape = state.getShape(mc.level, pos, CollisionContext.of(event.getCamera().getEntity()));
        if (shape.isEmpty()) return;

        double ox = pos.getX() - cam.x;
        double oy = pos.getY() - cam.y;
        double oz = pos.getZ() - cam.z;

        PoseStack ps = event.getPoseStack();
        MultiBufferSource buffers = event.getMultiBufferSource();

        int color = cfg.blockColor;

        // своя обводка вместо ванильной чёрной
        VertexConsumer lines = buffers.getBuffer(RenderType.lines());
        ShapeRenderer.renderShape(ps, lines, shape, ox, oy, oz, color);

        // лёгкая заливка граней, чтобы блок читался издалека
        if (cfg.blockFill) {
            AABB bounds = shape.bounds().move(ox, oy, oz).inflate(0.002);
            VertexConsumer quads = buffers.getBuffer(RenderType.debugQuads());
            fillBox(ps, quads, bounds, (color & 0xFFFFFF) | 0x33000000);
        }

        event.setCanceled(true);
    }

    /** Заливка коробки полупрозрачным цветом: шесть граней по два треугольника. */
    private static void fillBox(PoseStack ps, VertexConsumer vc, AABB b, int argb) {
        Matrix4f m = ps.last().pose();

        float x0 = (float) b.minX, y0 = (float) b.minY, z0 = (float) b.minZ;
        float x1 = (float) b.maxX, y1 = (float) b.maxY, z1 = (float) b.maxZ;

        // низ и верх
        quad(vc, m, argb, x0, y0, z0, x1, y0, z0, x1, y0, z1, x0, y0, z1);
        quad(vc, m, argb, x0, y1, z1, x1, y1, z1, x1, y1, z0, x0, y1, z0);
        // север и юг
        quad(vc, m, argb, x0, y0, z0, x0, y1, z0, x1, y1, z0, x1, y0, z0);
        quad(vc, m, argb, x1, y0, z1, x1, y1, z1, x0, y1, z1, x0, y0, z1);
        // запад и восток
        quad(vc, m, argb, x0, y0, z1, x0, y1, z1, x0, y1, z0, x0, y0, z0);
        quad(vc, m, argb, x1, y0, z0, x1, y1, z0, x1, y1, z1, x1, y0, z1);
    }

    private static void quad(VertexConsumer vc, Matrix4f m, int argb,
                             float ax, float ay, float az, float bx, float by, float bz,
                             float cx, float cy, float cz, float dx, float dy, float dz) {
        vc.addVertex(m, ax, ay, az).setColor(argb);
        vc.addVertex(m, bx, by, bz).setColor(argb);
        vc.addVertex(m, cx, cy, cz).setColor(argb);
        vc.addVertex(m, dx, dy, dz).setColor(argb);
    }
}
