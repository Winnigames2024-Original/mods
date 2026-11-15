package com.winnigames2024.sulak;

import com.mojang.brigadier.CommandDispatcher;
import com.mojang.brigadier.arguments.IntegerArgumentType;
import com.mojang.brigadier.arguments.StringArgumentType;
import net.minecraft.commands.CommandSourceStack;
import net.minecraft.commands.Commands;
import net.minecraft.commands.arguments.EntityArgument;
import net.minecraft.network.chat.Component;
import net.minecraft.server.level.ServerPlayer;

/** /sulak — админские команды и личный ключ игрока. */
public final class SulakCommands {

    private SulakCommands() {}

    public static void register(CommandDispatcher<CommandSourceStack> d) {
        d.register(Commands.literal("sulak")

                .then(Commands.literal("key")
                        .then(Commands.argument("apikey", StringArgumentType.greedyString())
                                .executes(ctx -> {
                                    ServerPlayer p = ctx.getSource().getPlayerOrException();
                                    SulakState.get().setKey(p.getUUID(), StringArgumentType.getString(ctx, "apikey"));
                                    ctx.getSource().sendSuccess(() ->
                                            Component.literal("Ключ сохранён. Бесплатный ключ: https://ai.winnigames2024.ru/api/index.php"), false);
                                    return 1;
                                })))

                .then(Commands.literal("left")
                        .executes(ctx -> {
                            ServerPlayer p = ctx.getSource().getPlayerOrException();
                            int left = SulakState.get().remaining(p.getUUID());
                            ctx.getSource().sendSuccess(() ->
                                    Component.literal("Осталось желаний: " + left), false);
                            return left;
                        }))

                .then(Commands.literal("spawn").requires(s -> s.hasPermission(2))
                        .executes(ctx -> {
                            SulakManager.spawnFor(ctx.getSource().getPlayerOrException());
                            return 1;
                        })
                        .then(Commands.argument("player", EntityArgument.player())
                                .executes(ctx -> {
                                    SulakManager.spawnFor(EntityArgument.getPlayer(ctx, "player"));
                                    return 1;
                                })))

                .then(Commands.literal("roll").requires(s -> s.hasPermission(2))
                        .executes(ctx -> {
                            SulakManager.rollOwner(ctx.getSource().getServer());
                            return 1;
                        }))

                .then(Commands.literal("despawn").requires(s -> s.hasPermission(2))
                        .executes(ctx -> {
                            SulakManager.removeExisting(ctx.getSource().getServer());
                            return 1;
                        }))

                .then(Commands.literal("limit").requires(s -> s.hasPermission(2))
                        .then(Commands.argument("player", EntityArgument.player())
                                .then(Commands.argument("used", IntegerArgumentType.integer(0))
                                        .executes(ctx -> {
                                            ServerPlayer p = EntityArgument.getPlayer(ctx, "player");
                                            SulakState.get().setUsed(p.getUUID(), IntegerArgumentType.getInteger(ctx, "used"));
                                            ctx.getSource().sendSuccess(() ->
                                                    Component.literal("Счётчик желаний обновлён."), true);
                                            return 1;
                                        }))))

                .then(Commands.literal("reset").requires(s -> s.hasPermission(2))
                        .then(Commands.argument("player", EntityArgument.player())
                                .executes(ctx -> {
                                    ServerPlayer p = EntityArgument.getPlayer(ctx, "player");
                                    SulakState.get().resetPlayer(p.getUUID());
                                    SulakCurse.cure(p);
                                    ctx.getSource().sendSuccess(() ->
                                            Component.literal("Игрок очищен от Сулака."), true);
                                    return 1;
                                })))

                .then(Commands.literal("curse").requires(s -> s.hasPermission(2))
                        .then(Commands.argument("player", EntityArgument.player())
                                .executes(ctx -> {
                                    SulakCurse.afflict(EntityArgument.getPlayer(ctx, "player"));
                                    return 1;
                                })))

                .then(Commands.literal("drops").requires(s -> s.hasPermission(2))
                        .then(Commands.literal("clear")
                                .executes(ctx -> {
                                    SulakState.get().clearDropRules(null);
                                    ctx.getSource().sendSuccess(() ->
                                            Component.literal("Правила дропа очищены."), true);
                                    return 1;
                                })))
        );
    }
}
