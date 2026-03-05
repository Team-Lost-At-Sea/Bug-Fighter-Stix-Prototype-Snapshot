using UnityEngine;

public sealed class FighterAttackController
{
    public AttackType CurrentAttackType => currentAttackType;
    public bool CurrentAttackStartedAirborne => currentAttackStartedAirborne;
    public bool CurrentAttackStartedCrouching => currentAttackStartedCrouching;
    public bool HasActiveUnspentHitbox => hitbox.active && !hitbox.hasHit;
    public Hitbox CurrentHitbox => hitbox;

    private AttackType currentAttackType = AttackType.None;
    private AttackData currentAttack;
    private bool currentAttackStartedAirborne;
    private bool currentAttackStartedCrouching;
    private int attackFrame;
    private Hitbox hitbox;

    public bool StartAttack(AttackType type, AttackData attackData, AttackStance stance)
    {
        if (attackData == null)
            return false;

        currentAttackType = type;
        currentAttack = attackData;
        currentAttackStartedAirborne = stance == AttackStance.Airborne;
        currentAttackStartedCrouching = stance == AttackStance.Crouching;
        attackFrame = 0;
        hitbox.Reset();
        return true;
    }

    public AttackData ResolveAttackData(
        AttackType type,
        AttackStance stance,
        FighterConfig config,
        bool facingRight
    )
    {
        AttackData configuredAttack = ResolveConfiguredAttackData(type, stance, config);
        if (configuredAttack != null)
            return configuredAttack;

        AttackTiming timing = GetDefaultTiming(type);
        return new AttackData
        {
            startupFrames = timing.startupFrames,
            activeFrames = timing.activeFrames,
            recoveryFrames = timing.recoveryFrames,
            damage = 5,
            hitstunFrames = 10,
            hitboxOffset = new Vector2(facingRight ? 0.9f : -0.9f, 0.9f),
            hitboxSize = new Vector2(1.0f, 0.8f),
        };
    }

    public AttackUpdateOutcome Update(FighterState state, Vector2 position, bool facingRight)
    {
        if (currentAttack == null)
            return default;

        attackFrame++;
        FighterState simulatedState = state;
        AttackUpdateOutcome outcome = default;

        if (simulatedState == FighterState.AttackStartup && attackFrame >= currentAttack.startupFrames)
        {
            simulatedState = FighterState.AttackActive;
            outcome.enterActive = true;
            hitbox.active = true;
            hitbox.hasHit = false;
            hitbox.damage = currentAttack.damage;
            hitbox.hitstunFrames = currentAttack.hitstunFrames;
        }

        if (simulatedState == FighterState.AttackActive)
        {
            Vector2 hitboxOffset = currentAttack.hitboxOffset;
            if (!facingRight)
                hitboxOffset.x = -hitboxOffset.x;

            hitbox.box = new Box(position + hitboxOffset, currentAttack.hitboxSize * 0.5f);

            int activeEndFrame = currentAttack.startupFrames + currentAttack.activeFrames;
            if (attackFrame >= activeEndFrame)
            {
                simulatedState = FighterState.AttackRecovery;
                outcome.enterRecovery = true;
                hitbox.active = false;
            }
        }

        if (simulatedState == FighterState.AttackRecovery)
        {
            int recoveryEndFrame =
                currentAttack.startupFrames + currentAttack.activeFrames + currentAttack.recoveryFrames;
            if (attackFrame >= recoveryEndFrame)
                outcome.endAttack = true;
        }

        return outcome;
    }

    public void EndAttack()
    {
        hitbox.Reset();
        currentAttack = null;
        currentAttackType = AttackType.None;
        currentAttackStartedAirborne = false;
        currentAttackStartedCrouching = false;
    }

    public void MarkCurrentHitboxAsSpent()
    {
        hitbox.hasHit = true;
    }

    private static AttackData ResolveConfiguredAttackData(
        AttackType type,
        AttackStance stance,
        FighterConfig config
    )
    {
        if (stance == AttackStance.Crouching)
        {
            if (type == AttackType.Light)
                return config.crouchingLightAttackData;
            if (type == AttackType.Medium)
                return config.crouchingMediumAttackData;
            if (type == AttackType.Heavy)
                return config.crouchingHeavyAttackData;
            return null;
        }

        if (stance == AttackStance.Standing)
        {
            if (type == AttackType.Light)
                return config.groundedLightAttackData;
            if (type == AttackType.Medium)
                return config.groundedMediumAttackData;
            if (type == AttackType.Heavy)
                return config.groundedHeavyAttackData;
            return null;
        }

        if (type == AttackType.Light)
            return config.jumpingLightAttackData;
        if (type == AttackType.Medium)
            return config.jumpingMediumAttackData;
        if (type == AttackType.Heavy)
            return config.jumpingHeavyAttackData;

        return null;
    }

    private static AttackTiming GetDefaultTiming(AttackType type)
    {
        switch (type)
        {
            case AttackType.Light:
                return new AttackTiming(4, 3, 14);
            case AttackType.Medium:
                return new AttackTiming(10, 4, 19);
            case AttackType.Heavy:
                return new AttackTiming(16, 5, 24);
            default:
                return new AttackTiming(0, 0, 0);
        }
    }

    private readonly struct AttackTiming
    {
        public readonly int startupFrames;
        public readonly int activeFrames;
        public readonly int recoveryFrames;

        public AttackTiming(int startupFrames, int activeFrames, int recoveryFrames)
        {
            this.startupFrames = startupFrames;
            this.activeFrames = activeFrames;
            this.recoveryFrames = recoveryFrames;
        }
    }
}

public struct AttackUpdateOutcome
{
    public bool enterActive;
    public bool enterRecovery;
    public bool endAttack;
}
