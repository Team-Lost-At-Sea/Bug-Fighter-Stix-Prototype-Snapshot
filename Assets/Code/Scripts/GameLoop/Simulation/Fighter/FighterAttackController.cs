using UnityEngine;

public sealed class FighterAttackController
{
    public readonly struct Snapshot
    {
        public readonly MoveType moveType;
        public readonly AttackData attackData;
        public readonly int attackFrame;
        public readonly Hitbox hitbox;

        public Snapshot(MoveType moveType, AttackData attackData, int attackFrame, Hitbox hitbox)
        {
            this.moveType = moveType;
            this.attackData = attackData;
            this.attackFrame = attackFrame;
            this.hitbox = hitbox;
        }
    }

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

    public AttackData ResolveAttackData(
        MoveType moveType,
        FighterConfig config,
        bool facingRight,
        bool enableGlobalDamageOverride,
        int lightOverrideDamage,
        int mediumOverrideDamage,
        int heavyOverrideDamage
    )
    {
        AttackData configuredAttack = config.GetAttackData(moveType);
        if (configuredAttack != null)
        {
            if (
                enableGlobalDamageOverride
                && TryResolveOverrideDamage(
                    moveType,
                    lightOverrideDamage,
                    mediumOverrideDamage,
                    heavyOverrideDamage,
                    out int overrideDamage
                )
            )
            {
                return CloneAttackDataWithDamage(configuredAttack, overrideDamage);
            }

            return configuredAttack;
        }

        AttackTiming timing = GetDefaultTiming(moveType);
        AttackData fallback = new AttackData
        {
            startupFrames = timing.startupFrames,
            activeFrames = timing.activeFrames,
            recoveryFrames = timing.recoveryFrames,
            damage = 5,
            hitstunFrames = 10,
            blockstunFrames = 8,
            blockPushback = 0.45f,
            chipDamage = moveType.IsFireball() || moveType.IsDragonPunch() ? 1 : 0,
            attackerBlockstopFrames = -1,
            hitLevel = ResolveDefaultHitLevel(moveType),
            hitboxOffset = new Vector2(facingRight ? 0.9f : -0.9f, 0.9f),
            hitboxSize = new Vector2(1.0f, 0.8f),
        };

        if (moveType.IsThrow())
        {
            fallback.damage = 80;
            fallback.hitstunFrames = 18;
            fallback.blockstunFrames = 1;
            fallback.blockPushback = 0f;
            fallback.chipDamage = 0;
            fallback.hitLevel = HitLevel.Unblockable;
            fallback.hitboxOffset = new Vector2(facingRight ? 0.55f : -0.55f, 0.9f);
            fallback.hitboxSize = new Vector2(0.65f, 1.25f);
        }

        if (
            enableGlobalDamageOverride
            && TryResolveOverrideDamage(
                moveType,
                lightOverrideDamage,
                mediumOverrideDamage,
                heavyOverrideDamage,
                out int fallbackOverrideDamage
            )
        )
        {
            fallback.damage = fallbackOverrideDamage;
        }

        return fallback;
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
                hitbox.blockstunFrames = Mathf.Max(1, currentAttack.blockstunFrames);
                hitbox.blockPushback = Mathf.Max(0f, currentAttack.blockPushback);
                hitbox.chipDamage = Mathf.Max(0, currentAttack.chipDamage);
                hitbox.attackerBlockstopFrames = currentAttack.attackerBlockstopFrames;
                hitbox.hitLevel = currentAttack.hitLevel;
                hitbox.isProjectile = false;
                hitbox.isThrow = currentMoveType.IsThrow();
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

    public Snapshot CaptureSnapshot()
    {
        return new Snapshot(currentMoveType, currentAttack, attackFrame, hitbox);
    }

    public void RestoreSnapshot(Snapshot snapshot)
    {
        currentMoveType = snapshot.moveType;
        currentAttack = snapshot.attackData;
        attackFrame = snapshot.attackFrame;
        hitbox = snapshot.hitbox;
    }

    public int GetRemainingRecoveryFrames()
    {
        if (currentAttack == null)
            return 0;

        int totalFrames = currentAttack.startupFrames + currentAttack.activeFrames + currentAttack.recoveryFrames;
        return Mathf.Max(0, totalFrames - attackFrame);
    }

    private static HitLevel ResolveDefaultHitLevel(MoveType moveType)
    {
        if (moveType == MoveType.CrouchingLight || moveType == MoveType.CrouchingMedium || moveType == MoveType.CrouchingHeavy)
            return HitLevel.Low;

        if (moveType == MoveType.JumpingLight || moveType == MoveType.JumpingMedium || moveType == MoveType.JumpingHeavy)
            return HitLevel.High;

        return HitLevel.Mid;
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
            case MoveType.DragonPunchLight:
                return new AttackTiming(6, 5, 22);
            case MoveType.StandingMedium:
            case MoveType.CrouchingMedium:
            case MoveType.JumpingMedium:
                return new AttackTiming(10, 4, 19);
            case MoveType.FireballMedium:
                return new AttackTiming(12, 0, 20);
            case MoveType.DragonPunchMedium:
                return new AttackTiming(8, 6, 24);
            case MoveType.StandingHeavy:
            case MoveType.CrouchingHeavy:
            case MoveType.JumpingHeavy:
            case MoveType.DownDownCharge:
                return new AttackTiming(16, 5, 24);
            case MoveType.Throw:
                return new AttackTiming(5, 2, 24);
            case MoveType.FireballHeavy:
                return new AttackTiming(14, 0, 22);
            case MoveType.DragonPunchHeavy:
                return new AttackTiming(10, 7, 26);
            default:
                return new AttackTiming(0, 0, 0);
        }
    }

    private static bool TryResolveOverrideDamage(
        MoveType moveType,
        int lightOverrideDamage,
        int mediumOverrideDamage,
        int heavyOverrideDamage,
        out int damage
    )
    {
        switch (moveType)
        {
            case MoveType.StandingLight:
            case MoveType.CrouchingLight:
            case MoveType.JumpingLight:
            case MoveType.FireballLight:
            case MoveType.DragonPunchLight:
                damage = Mathf.Max(0, lightOverrideDamage);
                return true;
            case MoveType.StandingMedium:
            case MoveType.CrouchingMedium:
            case MoveType.JumpingMedium:
            case MoveType.FireballMedium:
            case MoveType.DragonPunchMedium:
                damage = Mathf.Max(0, mediumOverrideDamage);
                return true;
            case MoveType.StandingHeavy:
            case MoveType.CrouchingHeavy:
            case MoveType.JumpingHeavy:
            case MoveType.FireballHeavy:
            case MoveType.DragonPunchHeavy:
            case MoveType.DownDownCharge:
                damage = Mathf.Max(0, heavyOverrideDamage);
                return true;
            default:
                damage = 0;
                return false;
        }
    }

    private static AttackData CloneAttackDataWithDamage(AttackData source, int damage)
    {
        if (source == null)
            return null;

        return new AttackData
        {
            startupFrames = source.startupFrames,
            activeFrames = source.activeFrames,
            recoveryFrames = source.recoveryFrames,
            damage = Mathf.Max(0, damage),
            hitstunFrames = source.hitstunFrames,
            blockstunFrames = source.blockstunFrames,
            blockPushback = source.blockPushback,
            chipDamage = source.chipDamage,
            attackerBlockstopFrames = source.attackerBlockstopFrames,
            hitLevel = source.hitLevel,
            hitboxOffset = source.hitboxOffset,
            hitboxSize = source.hitboxSize
        };
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
