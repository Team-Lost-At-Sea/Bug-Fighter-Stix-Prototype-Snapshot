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

public readonly struct FighterRenderSnapshot
{
    public readonly FighterVisualState visualState;
    public readonly AttackType attackType;
    public readonly bool attackIsAirborne;
    public readonly bool attackIsCrouching;
    public readonly int visualStateFrame;
    public readonly uint animationSerial;
    public readonly bool restartAnimation;
    public readonly bool freezeAnimation;

    public FighterRenderSnapshot(
        FighterVisualState visualState,
        AttackType attackType,
        bool attackIsAirborne,
        bool attackIsCrouching,
        int visualStateFrame,
        uint animationSerial,
        bool restartAnimation,
        bool freezeAnimation
    )
    {
        this.visualState = visualState;
        this.attackType = attackType;
        this.attackIsAirborne = attackIsAirborne;
        this.attackIsCrouching = attackIsCrouching;
        this.visualStateFrame = visualStateFrame;
        this.animationSerial = animationSerial;
        this.restartAnimation = restartAnimation;
        this.freezeAnimation = freezeAnimation;
    }
}

public enum AttackType
{
    None = 0,
    Light = 1,
    Medium = 2,
    Heavy = 3,
}

public enum AttackStance
{
    Standing,
    Crouching,
    Airborne,
}
