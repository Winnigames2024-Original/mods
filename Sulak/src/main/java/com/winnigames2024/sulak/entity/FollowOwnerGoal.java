package com.winnigames2024.sulak.entity;

import net.minecraft.world.entity.ai.goal.Goal;
import net.minecraft.world.entity.ai.navigation.PathNavigation;
import net.minecraft.world.entity.player.Player;

import java.util.EnumSet;

/** Сулак держится рядом со своим игроком и телепортируется, если отстал. */
public class FollowOwnerGoal extends Goal {

    private final SulakHelperEntity helper;
    private final double speed;
    private final float stopDistance;
    private final float startDistance;
    private final PathNavigation navigation;
    private Player owner;
    private int recalcCooldown;

    public FollowOwnerGoal(SulakHelperEntity helper, double speed, float stopDistance, float startDistance) {
        this.helper = helper;
        this.speed = speed;
        this.stopDistance = stopDistance;
        this.startDistance = startDistance;
        this.navigation = helper.getNavigation();
        this.setFlags(EnumSet.of(Goal.Flag.MOVE, Goal.Flag.LOOK));
    }

    @Override
    public boolean canUse() {
        Player p = helper.getOwner();
        if (p == null || p.isSpectator()) return false;
        if (helper.distanceToSqr(p) < stopDistance * stopDistance) return false;
        this.owner = p;
        return true;
    }

    @Override
    public boolean canContinueToUse() {
        return owner != null
                && !navigation.isDone()
                && helper.distanceToSqr(owner) > stopDistance * stopDistance;
    }

    @Override
    public void start() {
        recalcCooldown = 0;
    }

    @Override
    public void stop() {
        owner = null;
        navigation.stop();
    }

    @Override
    public void tick() {
        if (owner == null) return;
        helper.getLookControl().setLookAt(owner, 10.0F, helper.getMaxHeadXRot());

        if (--recalcCooldown > 0) return;
        recalcCooldown = 10;

        double distSqr = helper.distanceToSqr(owner);
        if (distSqr > startDistance * startDistance * 4.0D) {
            // отстал слишком далеко — догоняем телепортом
            helper.snapTo(owner.getX() + 1.5D, owner.getY(), owner.getZ() + 1.5D,
                    helper.getYRot(), helper.getXRot());
            navigation.stop();
            return;
        }
        navigation.moveTo(owner, speed);
    }
}
