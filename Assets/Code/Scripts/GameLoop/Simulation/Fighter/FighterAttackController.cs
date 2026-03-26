using UnityEngine;

public sealed class FighterAttackController
{
    public MoveType CurrentMoveType => currentMoveType;
    public bool HasActiveUnspentHitbox => hitbox.active && !hitbox.hasHit;
    public Hitbox CurrentHitbox => hitbox;

    private MoveType currentMoveType = MoveType.None;
    private AttackData currentAttack;
    private int attackFrame;
    private Hitbox hitbox;

    public bool StartAttack(MoveType moveType, AttackData attackData)
    {
        if (attackData == null)
            return false;

        currentMoveType = moveType;
        currentAttack = attackData;
        attackFrame = 0;
        hitbox.Reset();
        return true;
    }

    public AttackData ResolveAttackData(MoveType moveType, FighterConfig config, bool facingRight)
    {
        AttackData configuredAttack = config.GetAttackData(moveType);
        if (configuredAttack != null)
            return configuredAttack;

        AttackTiming timing = GetDefaultTiming(moveType);
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
            if (!currentMoveType.IsFireball())
            {
                hitbox.active = true;
                hitbox.hasHit = false;
                hitbox.damage = currentAttack.damage;
                hitbox.hitstunFrames = currentAttack.hitstunFrames;
            }
        }

        if (simulatedState == FighterState.AttackActive)
        {
            Vector2 hitboxOffset = currentAttack.hitboxOffset;
            if (!facingRight)
                hitboxOffset.x = -hitboxOffset.x;

            if (!currentMoveType.IsFireball())
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
        currentMoveType = MoveType.None;
    }

    public void MarkCurrentHitboxAsSpent()
    {
        hitbox.hasHit = true;
    }

    private static AttackTiming GetDefaultTiming(MoveType moveType)
    {
        switch (moveType)
        {
            case MoveType.StandingLight:
            case MoveType.CrouchingLight:
            case MoveType.JumpingLight:
                return new AttackTiming(4, 3, 14);
            case MoveType.FireballLight:
                return new AttackTiming(10, 0, 18);
            case MoveType.StandingMedium:
            case MoveType.CrouchingMedium:
            case MoveType.JumpingMedium:
                return new AttackTiming(10, 4, 19);
            case MoveType.FireballMedium:
                return new AttackTiming(12, 0, 20);
            case MoveType.StandingHeavy:
            case MoveType.CrouchingHeavy:
            case MoveType.JumpingHeavy:
                return new AttackTiming(16, 5, 24);
            case MoveType.FireballHeavy:
                return new AttackTiming(14, 0, 22);
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
