package com.winnigames2024.forge_narevisuals.client;

import com.mojang.brigadier.arguments.IntegerArgumentType;
import com.winnigames2024.forge_narevisuals.Config;
import com.winnigames2024.forge_narevisuals.Forge_narevisuals;
import com.winnigames2024.forge_narevisuals.Launch;
import net.minecraft.client.Minecraft;
import net.minecraft.client.player.AbstractClientPlayer;
import net.minecraft.commands.CommandSourceStack;
import net.minecraft.commands.Commands;
import net.minecraft.network.chat.Component;
import net.minecraft.util.Mth;
import net.minecraft.world.phys.Vec3;
import net.minecraftforge.api.distmarker.Dist;
import net.minecraftforge.client.event.RegisterClientCommandsEvent;
import net.minecraftforge.eventbus.api.SubscribeEvent;
import net.minecraftforge.fml.common.Mod;

import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;

/**
 * «Кто рядом»: команда /near и тот же список элементом HUD.
 *
 * Команда клиентская — она не уходит на сервер и работает где угодно, даже
 * там, где своих команд нет. Если чат закрыт или команду набирать некогда,
 * тот же список включается элементом HUD «Игроки рядом».
 *
 * Видно ровно тех, кого прислал сервер: обычно это игроки в радиусе прогрузки.
 * Общее число в сети берём из списка игроков по Tab — там все, но без координат.
 */
@Mod.EventBusSubscriber(modid = Forge_narevisuals.MODID, value = Dist.CLIENT)
public final class NareNear {

    /** Один игрок рядом: имя, расстояние, перепад высоты, угол относительно взгляда. */
    public record Spot(String name, double distance, double dy, float relative,
                       float health, float maxHealth) { }

    private static final String[] ARROWS = { "^", "/", ">", "\\", "v", "\\", "<", "/" };
    private static final String[] SIDES = { "вперёд", "впр-впер", "вправо", "впр-наз",
                                            "назад", "влв-наз", "влево", "влв-впер" };

    private static List<Spot> cache = new ArrayList<>();
    private static long cacheAt;
    private static int cacheRange;

    private NareNear() { }

    // ------------------------------------------------------------ сканирование

    /** Пересчитывает не чаще раза в 200 мс — список нужен каждому кадру HUD. */
    public static List<Spot> cached() {
        int range = Config.get().nearRange;
        long now = System.currentTimeMillis();
        if (range != cacheRange || now - cacheAt > 200) {
            cache = scan(range);
            cacheAt = now;
            cacheRange = range;
        }
        return cache;
    }

    public static List<Spot> scan(int range) {
        List<Spot> out = new ArrayList<>();

        Minecraft mc = Minecraft.getInstance();
        if (mc.player == null || mc.level == null) return out;

        Vec3 me = mc.player.position();
        float look = mc.player.getYRot();

        for (AbstractClientPlayer other : mc.level.players()) {
            if (other == mc.player) continue;

            double distance = other.position().distanceTo(me);
            if (distance > range) continue;

            double dx = other.getX() - me.x;
            double dz = other.getZ() - me.z;

            // угол на цель в той же системе, что и поворот игрока
            float bearing = (float) (Math.toDegrees(Math.atan2(dz, dx)) - 90.0);
            float relative = Mth.wrapDegrees(bearing - look);

            out.add(new Spot(other.getGameProfile().getName(), distance,
                    other.getY() - me.y, relative,
                    other.getHealth(), Math.max(1f, other.getMaxHealth())));
        }

        out.sort(Comparator.comparingDouble(Spot::distance));
        return out;
    }

    /** Сколько игроков в сети по списку Tab — координат у них нет, но число честное. */
    public static int online() {
        Minecraft mc = Minecraft.getInstance();
        if (mc.getConnection() == null) return 0;
        return mc.getConnection().getOnlinePlayers().size();
    }

    // ------------------------------------------------------------ направление

    private static int sector(float relative) {
        int s = Math.round((relative + 360f) % 360f / 45f) % 8;
        return s;
    }

    public static String arrow(float relative) {
        return ARROWS[sector(relative)];
    }

    public static String side(float relative) {
        return SIDES[sector(relative)];
    }

    // ------------------------------------------------------------ команда

    @SubscribeEvent
    public static void registerCommands(RegisterClientCommandsEvent event) {
        event.getDispatcher().register(
                Commands.literal("near")
                        .executes(ctx -> report(ctx.getSource(), Config.get().nearRange))
                        .then(Commands.argument("radius", IntegerArgumentType.integer(1, 512))
                                .executes(ctx -> report(ctx.getSource(),
                                        IntegerArgumentType.getInteger(ctx, "radius")))));

        Forge_narevisuals.LOGGER.info("{}: команда /near зарегистрирована", Forge_narevisuals.NAME);
    }

    private static int report(CommandSourceStack source, int range) {
        if (!Launch.visuals()) {
            source.sendFailure(Component.literal("NareVisuals выключен: запусти сборку с --visuals"));
            return 0;
        }

        List<Spot> list = scan(range);
        int online = online();

        if (list.isEmpty()) {
            source.sendSuccess(() -> Component.literal(
                    "§7Рядом (§f" + range + " бл.§7) никого. В сети: §f" + online), false);
            return 0;
        }

        source.sendSuccess(() -> Component.literal(
                "§bРядом §f" + list.size() + "§b в радиусе §f" + range
                + "§b бл. · в сети §f" + online), false);

        for (Spot spot : list) {
            final String line = line(spot);
            source.sendSuccess(() -> Component.literal(line), false);
        }

        return list.size();
    }

    private static String line(Spot spot) {
        String height = spot.dy() >= 1 ? " §a+" + Math.round(spot.dy())
                      : spot.dy() <= -1 ? " §c" + Math.round(spot.dy())
                      : "";

        String health = Config.get().nearHealth
                ? " §7(" + (int) Math.ceil(spot.health()) + "/" + (int) Math.ceil(spot.maxHealth()) + ")"
                : "";

        return "§b" + arrow(spot.relative()) + " §f" + spot.name()
                + " §7— §f" + Math.round(spot.distance()) + "§7 бл., " + side(spot.relative())
                + height + health;
    }
}
