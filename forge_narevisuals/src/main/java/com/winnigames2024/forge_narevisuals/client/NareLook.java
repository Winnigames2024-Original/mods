package com.winnigames2024.forge_narevisuals.client;

import com.winnigames2024.forge_narevisuals.Config;
import com.winnigames2024.forge_narevisuals.Forge_narevisuals;
import com.winnigames2024.forge_narevisuals.Launch;
import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.GuiGraphics;
import net.minecraft.client.renderer.RenderType;
import net.minecraft.resources.ResourceLocation;
import net.minecraft.world.effect.MobEffectInstance;
import net.minecraft.world.item.Items;
import net.minecraftforge.api.distmarker.Dist;
import net.minecraftforge.client.event.RenderBlockScreenEffectEvent;
import net.minecraftforge.client.event.RenderHandEvent;
import net.minecraftforge.eventbus.api.SubscribeEvent;
import net.minecraftforge.fml.common.Mod;

/**
 * Что закрывает обзор: огонь во весь экран, здоровенный щит в руке и рябь
 * от эффектов.
 *
 * Важное: гореть человек не перестаёт и урон идёт как обычно. Мы меняем
 * только картинку — ровно то же самое делает любой ресурспак, где огонь
 * нарисован прозрачным.
 */
@Mod.EventBusSubscriber(modid = Forge_narevisuals.MODID, value = Dist.CLIENT)
public final class NareLook {

    /** Ванильная картинка огня: одна полоса из кадров, каждый 16 на 16. */
    private static final ResourceLocation FIRE =
            ResourceLocation.withDefaultNamespace("textures/block/fire_1.png");

    private static final int FRAMES = 32;

    private NareLook() { }

    // -------------------------------------------------------------- огонь

    /**
     * Ванильный огонь убираем, когда человек просил его убрать или уменьшить:
     * «поменьше» мы рисуем сами, иначе пришлось бы лезть в чужой рендер.
     */
    @SubscribeEvent
    public static void onFire(RenderBlockScreenEffectEvent event) {
        if (!Launch.visuals()) return;
        if (event.getOverlayType() != RenderBlockScreenEffectEvent.OverlayType.FIRE) return;

        Config cfg = Config.get();
        if (cfg.fireOff || cfg.fireSize < 100) event.setCanceled(true);
    }

    /** Наш маленький огонь — полоса внизу экрана вместо картинки во весь. */
    public static void drawFire(GuiGraphics g, int width, int height) {
        if (!Launch.visuals()) return;

        Config cfg = Config.get();
        if (cfg.fireOff || cfg.fireSize >= 100) return;

        Minecraft mc = Minecraft.getInstance();
        if (mc.player == null || !mc.player.isOnFire()) return;
        if (mc.player.isSpectator()) return;
        if (mc.player.hasEffect(net.minecraft.world.effect.MobEffects.FIRE_RESISTANCE)) return;

        int size = Math.max(16, height * Math.max(10, Math.min(99, cfg.fireSize)) / 100);
        int y = height - size;

        // кадр выбираем по времени — огонь должен шевелиться
        int frame = (int) ((System.currentTimeMillis() / 60L) % FRAMES);
        float v = frame * 16f;

        for (int x = 0; x < width; x += size) {
            int draw = Math.min(size, width - x);

            g.blit(RenderType::guiTextured, FIRE, x, y, 0f, v,
                    draw, size, 16, 16, 16, 16 * FRAMES, 0xCCFFFFFF);
        }
    }

    // --------------------------------------------------------------- щит

    /** Щит в руке уменьшаем, чтобы он не закрывал пол-лица. */
    @SubscribeEvent
    public static void onHand(RenderHandEvent event) {
        if (!Launch.visuals()) return;
        if (!event.getItemStack().is(Items.SHIELD)) return;

        int size = Math.max(30, Math.min(100, Config.get().shieldSize));
        if (size >= 100) return;

        float k = size / 100f;

        event.getPoseStack().scale(k, k, k);
        event.getPoseStack().translate(0f, (1f - k) * 0.3f, 0f);
    }

    // ------------------------------------------------------------ эффекты

    /**
     * Показывать ли этот эффект.
     *
     * Сразу честно: отменить сам эффект клиент не может — его накладывает
     * сервер, и урон или замедление всё равно будут. Фильтр убирает значки
     * и мельтешение, когда от них рябит в глазах, не больше.
     */
    public static boolean show(MobEffectInstance effect) {
        Config cfg = Config.get();
        if (!cfg.effectsFilter) return true;
        if (effect == null) return true;

        try {
            var key = net.minecraft.core.registries.BuiltInRegistries.MOB_EFFECT
                    .getKey(effect.getEffect().value());

            return key == null || cfg.effectsKeep.contains(key.toString());
        }
        catch (Exception e) {
            Forge_narevisuals.LOGGER.debug("{}: эффект не опознан: {}",
                    Forge_narevisuals.NAME, e.getMessage());
            return true;
        }
    }

    /** Название эффекта для списка в настройках. */
    public static String nameOf(MobEffectInstance effect) {
        try { return effect.getEffect().value().getDisplayName().getString(); }
        catch (Exception e) { return "эффект"; }
    }

    /** Ключ эффекта, каким он записан в настройках. */
    public static String keyOf(MobEffectInstance effect) {
        try {
            var key = net.minecraft.core.registries.BuiltInRegistries.MOB_EFFECT
                    .getKey(effect.getEffect().value());
            return key == null ? "" : key.toString();
        }
        catch (Exception e) { return ""; }
    }
}
