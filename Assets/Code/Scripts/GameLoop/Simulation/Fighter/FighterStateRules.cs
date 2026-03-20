public static class FighterStateRules
{
    public static bool IsAttackState(FighterState state)
    {
        return state == FighterState.AttackStartup
            || state == FighterState.AttackActive
            || state == FighterState.AttackRecovery;
    }

    public static bool ShouldLockMovement(FighterState state, bool isGrounded)
    {
        if (state == FighterState.JumpStartup)
            return true;
        if (state == FighterState.Crouching)
            return true;
        if (state == FighterState.LandingRecovery)
            return true;
        if (IsAttackState(state))
            return isGrounded;
        if (state == FighterState.Hitstun || state == FighterState.Blockstun || state == FighterState.Knockdown)
            return true;

        return false;
    }

    public static bool CanEnterCrouchFromIdle(FighterState state, bool isGrounded)
    {
        return isGrounded && state == FighterState.NeutralGround;
    }

    public static bool IsActionableGrounded(FighterState state)
    {
        return state == FighterState.NeutralGround;
    }

    public static bool CanHoldBlock(FighterState state, bool isGrounded)
    {
        if (!isGrounded)
            return false;

        return state == FighterState.NeutralGround || state == FighterState.Crouching;
    }

    public static bool CanStartAttack(
        FighterState state,
        bool isGrounded,
        bool canCancelLandingRecoveryIntoAttack
    )
    {
        if (state == FighterState.LandingRecovery)
            return canCancelLandingRecoveryIntoAttack;

        if (isGrounded)
            return state == FighterState.NeutralGround || state == FighterState.Crouching;

        return state == FighterState.NeutralAir;
    }

    public static bool ShouldApplyLandingRecovery(FighterState state)
    {
        return state != FighterState.Hitstun
            && state != FighterState.Blockstun
            && state != FighterState.Knockdown;
    }
}
