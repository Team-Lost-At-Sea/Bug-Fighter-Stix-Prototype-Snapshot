public enum FighterState
{
    NeutralGround,
    Crouching,
    NeutralAir,
    JumpStartup,
    LandingRecovery,
    AttackStartup,
    AttackActive,
    AttackRecovery,
    Hitstun,
    Blockstun,
    Knockdown,
}

public enum FighterVisualState
{
    None,
    Idle,
    CrouchTransition,
    Crouching,
    WalkForward,
    WalkBackward,
    JumpStartup,
    Airborne,
    Landing,
    Attacking,
    Hitstun,
    Blockstun,
    Knockdown,
}

public enum FighterAirPhase
{
    Grounded,
    Rising,
    Falling,
    Apex,
}

public readonly struct FighterRenderSnapshot
{
    public readonly FighterVisualState visualState;
    public readonly MoveType moveType;
    public readonly int visualStateFrame;
    public readonly uint animationSerial;
    public readonly bool restartAnimation;
    public readonly bool freezeAnimation;

    public FighterRenderSnapshot(
        FighterVisualState visualState,
        MoveType moveType,
        int visualStateFrame,
        uint animationSerial,
        bool restartAnimation,
        bool freezeAnimation
    )
    {
        this.visualState = visualState;
        this.moveType = moveType;
        this.visualStateFrame = visualStateFrame;
        this.animationSerial = animationSerial;
        this.restartAnimation = restartAnimation;
        this.freezeAnimation = freezeAnimation;
    }
}

public enum MoveType
{
    None = 0,
    StandingLight = 1,
    StandingMedium = 2,
    StandingHeavy = 3,
    CrouchingLight = 4,
    CrouchingMedium = 5,
    CrouchingHeavy = 6,
    JumpingLight = 7,
    JumpingMedium = 8,
    JumpingHeavy = 9,
    FireballLight = 10,
    FireballMedium = 11,
    FireballHeavy = 12,
    DragonPunchLight = 13,
    DragonPunchMedium = 14,
    DragonPunchHeavy = 15,
}

public static class MoveTypeExtensions
{
    public static bool IsFireball(this MoveType moveType)
    {
        return moveType == MoveType.FireballLight
            || moveType == MoveType.FireballMedium
            || moveType == MoveType.FireballHeavy;
    }

    public static bool IsDragonPunch(this MoveType moveType)
    {
        return moveType == MoveType.DragonPunchLight
            || moveType == MoveType.DragonPunchMedium
            || moveType == MoveType.DragonPunchHeavy;
    }
}
