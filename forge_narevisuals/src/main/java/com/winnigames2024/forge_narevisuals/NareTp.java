package com.winnigames2024.forge_narevisuals;

import com.mojang.brigadier.arguments.StringArgumentType;
import net.minecraft.commands.CommandSourceStack;
import net.minecraft.commands.Commands;
import net.minecraft.network.chat.Component;
import net.minecraft.server.level.ServerPlayer;
import net.minecraftforge.event.RegisterCommandsEvent;
import net.minecraftforge.eventbus.api.SubscribeEvent;
import net.minecraftforge.fml.common.Mod;

/**
 * Серверная команда телепорта «/ntp &lt;игрок&gt;». Доступна всем (уровень прав 0),
 * но выполняется самим сервером — то есть с правами владельца/оператора. Поэтому на
 * мире «Вместе» (интегрированный сервер у хоста) и на любом Forge-сервере с модом
 * обычный игрок может перенестись к другому, хотя ванильный /tp ему недоступен.
 *
 * Клиент шлёт эту команду из экрана телепорта (кнопка G). На сервере без мода,
 * но с плагином (WMine/NareServer) команду должен регистрировать плагин — это
 * отдельная доработка.
 */
@Mod.EventBusSubscriber(modid = Forge_narevisuals.MODID)
public final class NareTp {

    private NareTp() { }

    @SubscribeEvent
    public static void onRegisterCommands(RegisterCommandsEvent e) {
        e.getDispatcher().register(
                Commands.literal("ntp")
                        .requires(src -> true)
                        .then(Commands.argument("игрок", StringArgumentType.word())
                                .executes(ctx -> run(ctx.getSource(),
                                        StringArgumentType.getString(ctx, "игрок")))));
    }

    private static int run(CommandSourceStack src, String targetName) {
        ServerPlayer me;
        try {
            me = src.getPlayerOrException();
        } catch (Exception ex) {
            return 0;
        }
        ServerPlayer target = me.server.getPlayerList().getPlayerByName(targetName);
        if (target == null) {
            src.sendFailure(Component.literal("Игрок «" + targetName + "» не в сети"));
            return 0;
        }
        if (target == me) {
            src.sendFailure(Component.literal("Это вы и есть"));
            return 0;
        }
        me.teleportTo(target.serverLevel(), target.getX(), target.getY(), target.getZ(),
                java.util.Set.of(), target.getYRot(), target.getXRot(), false);
        final String who = targetName;
        src.sendSuccess(() -> Component.literal("Телепорт к " + who), false);
        return 1;
    }
}
