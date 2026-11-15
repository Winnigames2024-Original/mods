package com.winnigames2024.sulak.client;

import com.winnigames2024.sulak.SulakCurse;
import net.minecraft.client.player.AbstractClientPlayer;
import net.minecraft.world.scores.Team;

/** Клиентская часть проклятия: кому подменять скин. */
public final class CurseClient {

    private CurseClient() {}

    public static final net.minecraft.resources.ResourceLocation CURSED_SKIN =
            com.winnigames2024.sulak.Sulak.id("textures/entity/cursed_player.png");

    public static boolean isCursed(AbstractClientPlayer player) {
        if (player == null) return false;
        Team team = player.getTeam();
        return team != null && SulakCurse.TEAM.equals(team.getName());
    }
}
