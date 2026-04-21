using UnityEngine;

public enum BlockstunParticlePreset
{
    None,
    ShieldPulse,
    GuardSparks,
    HitMist
}

[CreateAssetMenu(menuName = "Fighter/Fighter Config")]
public class FighterConfig : ScriptableObject
{
    [Header("Movement")]
    public float moveSpeed = 6f;
    public float jumpForce = 12f;
    public float jumpHorizontalBoostScale = 2f;
    public int backdashDurationFrames = 12;
    public int backdashRecoveryFrames = 20;
    public float backdashSpeedMultiplier = 2.5f;
    public int backdashInputWindowFrames = 8;
    public float gravity = -30f;

    [Header("Advanced")]
    public float maxFallSpeed = -25f;
    public float groundFriction = 20f;

    [Header("Pushbox")]
    public float pushboxHalfWidth = 0.5f;

    [Header("Hurtbox")]
    public Vector2 hurtboxHalfSize = new Vector2(0.5f, 1.0f);
    public Vector2 hurtboxOffsetFromFeet = Vector2.zero;

    [Header("Shared State Animations")]
    public string idleStateName = "Idle";
    public string crouchStateName = "Crouching";
    public string crouchTransitionStateName = "Crouch Transition";
    public string walkForwardStateName = "Walk Forward";
    public string walkBackwardStateName = "Walk Backward";
    public string jumpStartupStateName = "Jumpsquat";
    public string airborneStateName = "Rising";
    public string landingStateName = "Landing";
    public string hitstunStateName = "Hitstun";
    public string blockstunStateName = "Blockstun";
    public string knockdownStateName = "Knockdown";
    public string backdashStateName = "Backdash";

    [Header("Move Animations")]
    public string standingLightStateName = "s_LP";
    public string standingMediumStateName = "s_MP";
    public string standingHeavyStateName = "s_HP";
    public string crouchingLightStateName = "c_LP";
    public string crouchingMediumStateName = "c_MP";
    public string crouchingHeavyStateName = "c_HP";
    public string jumpingLightStateName = "j_LP";
    public string jumpingMediumStateName = "j_MP";
    public string jumpingHeavyStateName = "j_HP";
    public string fireballLightStateName = "s_LP";
    public string fireballMediumStateName = "s_MP";
    public string fireballHeavyStateName = "s_HP";
    public string dragonPunchLightStateName = "s_LP";
    public string dragonPunchMediumStateName = "s_MP";
    public string dragonPunchHeavyStateName = "s_HP";
    public string downDownChargeStateName = "DownDownCharge";
    public string throwStateName = "Throw";

    [Header("Shared Moves")]
    public bool enableDownDownCharge = false;
    public AttackData standingLightAttackData;
    public AttackData standingMediumAttackData;
    public AttackData standingHeavyAttackData;
    public AttackData crouchingLightAttackData;
    public AttackData crouchingMediumAttackData;
    public AttackData crouchingHeavyAttackData;
    public AttackData jumpingLightAttackData;
    public AttackData jumpingMediumAttackData;
    public AttackData jumpingHeavyAttackData;
    public AttackData fireballLightAttackData;
    public AttackData fireballMediumAttackData;
    public AttackData fireballHeavyAttackData;
    public AttackData dragonPunchLightAttackData;
    public AttackData dragonPunchMediumAttackData;
    public AttackData dragonPunchHeavyAttackData;
    public AttackData downDownChargeAttackData;
    public AttackData throwAttackData;

    [Header("Fireball Projectile")]
    public Sprite fireballProjectileSprite;
    public Color fireballProjectileTint = new Color(1f, 0.6f, 0.1f, 0.9f);
    public Vector2 fireballSpawnOffset = new Vector2(1.0f, 1.0f);
    public Vector2 fireballProjectileHalfSize = new Vector2(0.35f, 0.35f);
    public float fireballProjectileSpeedPerFrame = 0.42f;
    public int fireballProjectileLifetimeFrames = 120;
    public int fireballProjectileDamage = 7;
    public int fireballProjectileHitstunFrames = 16;

    [Header("Blockstun Effect")]
    [Tooltip("Animator-driven effect prefab shown while this fighter is in blockstun.")]
    public GameObject blockstunEffectPrefab;
    [Tooltip("Local offset from fighter feet where the blockstun effect is centered.")]
    public Vector2 blockstunEffectOffsetFromFeet = new Vector2(0f, 1f);
    [Tooltip("Multiplier applied to the instantiated effect's default local scale.")]
    public Vector3 blockstunEffectScaleMultiplier = Vector3.one;
    [Min(1)]
    [Tooltip("Playback speed reference: speed = referenceFrames / lastReceivedStunFrames.")]
    public int blockstunEffectReferenceFrames = 20;
    [Min(0f)]
    public float blockstunEffectMinSpeed = 0.2f;
    [Min(0f)]
    public float blockstunEffectMaxSpeed = 3f;
    [Tooltip("Debug toggle: keeps the blockshield animation always visible to help tune offset/scale.")]
    public bool previewBlockshieldAnim = false;

    [Header("Blockstun Particle Supplement")]
    [Tooltip("Optional particle effect prefab layered on top of blockstun animator effect.")]
    public GameObject blockstunParticlePrefab;
    public Vector2 blockstunParticleOffsetFromFeet = new Vector2(0f, 1f);
    public Vector3 blockstunParticleScaleMultiplier = Vector3.one;
    public BlockstunParticlePreset blockstunParticlePreset = BlockstunParticlePreset.ShieldPulse;
    [Tooltip("Extra multiplier applied after blockstun speed scaling.")]
    public float blockstunParticleSpeedMultiplier = 1f;

    public AttackData GetAttackData(MoveType moveType)
    {
        switch (moveType)
        {
            case MoveType.StandingLight:
                return standingLightAttackData;
            case MoveType.StandingMedium:
                return standingMediumAttackData;
            case MoveType.StandingHeavy:
                return standingHeavyAttackData;
            case MoveType.CrouchingLight:
                return crouchingLightAttackData;
            case MoveType.CrouchingMedium:
                return crouchingMediumAttackData;
            case MoveType.CrouchingHeavy:
                return crouchingHeavyAttackData;
            case MoveType.JumpingLight:
                return jumpingLightAttackData;
            case MoveType.JumpingMedium:
                return jumpingMediumAttackData;
            case MoveType.JumpingHeavy:
                return jumpingHeavyAttackData;
            case MoveType.FireballLight:
                return fireballLightAttackData;
            case MoveType.FireballMedium:
                return fireballMediumAttackData;
            case MoveType.FireballHeavy:
                return fireballHeavyAttackData;
            case MoveType.DragonPunchLight:
                return dragonPunchLightAttackData;
            case MoveType.DragonPunchMedium:
                return dragonPunchMediumAttackData;
            case MoveType.DragonPunchHeavy:
                return dragonPunchHeavyAttackData;
            case MoveType.DownDownCharge:
                return downDownChargeAttackData;
            case MoveType.Throw:
                return throwAttackData;
            default:
                return null;
        }
    }

    public string GetAnimationStateName(FighterVisualState visualState)
    {
        switch (visualState)
        {
            case FighterVisualState.Idle:
                return idleStateName;
            case FighterVisualState.CrouchTransition:
                return crouchTransitionStateName;
            case FighterVisualState.Crouching:
                return crouchStateName;
            case FighterVisualState.WalkForward:
                return walkForwardStateName;
            case FighterVisualState.WalkBackward:
                return walkBackwardStateName;
            case FighterVisualState.JumpStartup:
                return jumpStartupStateName;
            case FighterVisualState.Airborne:
                return airborneStateName;
            case FighterVisualState.Landing:
                return landingStateName;
            case FighterVisualState.Hitstun:
                return hitstunStateName;
            case FighterVisualState.Blockstun:
                return blockstunStateName;
            case FighterVisualState.Knockdown:
                return knockdownStateName;
            case FighterVisualState.Backdash:
                return backdashStateName;
            default:
                return null;
        }
    }

    public string GetAnimationStateName(MoveType moveType)
    {
        switch (moveType)
        {
            case MoveType.StandingLight:
                return standingLightStateName;
            case MoveType.StandingMedium:
                return standingMediumStateName;
            case MoveType.StandingHeavy:
                return standingHeavyStateName;
            case MoveType.CrouchingLight:
                return crouchingLightStateName;
            case MoveType.CrouchingMedium:
                return crouchingMediumStateName;
            case MoveType.CrouchingHeavy:
                return crouchingHeavyStateName;
            case MoveType.JumpingLight:
                return jumpingLightStateName;
            case MoveType.JumpingMedium:
                return jumpingMediumStateName;
            case MoveType.JumpingHeavy:
                return jumpingHeavyStateName;
            case MoveType.FireballLight:
                return fireballLightStateName;
            case MoveType.FireballMedium:
                return fireballMediumStateName;
            case MoveType.FireballHeavy:
                return fireballHeavyStateName;
            case MoveType.DragonPunchLight:
                return dragonPunchLightStateName;
            case MoveType.DragonPunchMedium:
                return dragonPunchMediumStateName;
            case MoveType.DragonPunchHeavy:
                return dragonPunchHeavyStateName;
            case MoveType.DownDownCharge:
                return downDownChargeStateName;
            case MoveType.Throw:
                return throwStateName;
            default:
                return null;
        }
    }
}
