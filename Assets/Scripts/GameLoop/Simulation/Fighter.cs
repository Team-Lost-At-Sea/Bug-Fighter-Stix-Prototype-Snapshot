using UnityEngine;

// Fighter.cs drives deterministic simulation for one fighter.
// Gameplay timing is fully frame-based and independent from Animator timing.
public class Fighter
{
    private const int JUMP_STARTUP_TICKS = 4;
    private const int LANDING_RECOVERY_TICKS = 3;
    private const int DEFENDER_HITSTOP_FRAMES = 8;
    private const int ATTACKER_HITSTOP_FRAMES = 6;

    public Vector2 Position => position;
    public Vector2 Velocity => velocity;
    public bool FacingRight => facingRight;
    public float PushboxHalfWidth => config.pushboxHalfWidth;
    public bool IsGrounded => isGrounded;
    public bool HasActiveUnspentHitbox => hitbox.active && !hitbox.hasHit;
    public Hitbox CurrentHitbox => hitbox;
    public Box CurrentHurtbox => new Box(position, hurtbox.halfSize);
    public FighterState CurrentState => state;
    public int StateFrame => stateFrame;
    public AttackType CurrentAttackType => currentAttackType;
    public bool IsInHitstop => hitstopFramesRemaining > 0;
    public FighterRenderSnapshot RenderSnapshot => renderSnapshot;

    private Vector2 position;
    private Vector2 velocity;
    private readonly FighterConfig config;
    private readonly FighterView view;
    private Fighter opponent;

    private bool isGrounded = true;
    private bool facingRight = true;
    private FighterState state = FighterState.NeutralGround;
    private int stateFrame;
    private bool transitionedThisTick;
    private bool stateFrameFrozenThisTick;

    private AttackType currentAttackType = AttackType.None;
    private AttackData currentAttack;
    private bool currentAttackStartedAirborne;
    private int attackFrame;
    private Hitbox hitbox;
    private Box hurtbox;

    private int hitstopFramesRemaining;
    private int hitstunFramesRemaining;
    private int jumpStartupTicksRemaining;
    private int landingRecoveryTicksRemaining;
    private int queuedJumpMoveX;
    private bool usedAirNormalThisJump;

    private FighterRenderSnapshot renderSnapshot;
    private FighterVisualState lastVisualState = FighterVisualState.None;
    private AttackType lastVisualAttackType = AttackType.None;
    private int visualStateFrame;
    private uint animationSerial;

    public Fighter(FighterView view, Vector2 startPosition)
    {
        this.view = view;
        config = view.Config;
        position = startPosition;
        velocity = Vector2.zero;
        hurtbox = new Box(position, config.hurtboxHalfSize);
        hitbox.Reset();
        BuildRenderSnapshot(forceRestart: true);
    }

    public void Tick(InputFrame input)
    {
        transitionedThisTick = false;
        stateFrameFrozenThisTick = false;
        if (hitstopFramesRemaining > 0)
        {
            hitstopFramesRemaining--;
            stateFrameFrozenThisTick = true;
            EndTick();
            return;
        }

        RefreshGroundedStateFromKinematics();

        if (state == FighterState.Hitstun)
        {
            SimulateHitstun();
        }
        else
        {
            HandleJumpStartup(input);
            HandleMovement(input);
            HandleAttacks(input);
            UpdateAttackDataAttack();
            ApplyGravity();
            Integrate();
            ResolveGroundContact();
            AdvanceLandingRecovery();
        }

        EndTick();
    }

    private void SimulateHitstun()
    {
        if (hitstunFramesRemaining > 0)
            hitstunFramesRemaining--;

        ApplyGravity();
        Integrate();
        ResolveGroundContact();

        if (hitstunFramesRemaining <= 0)
            EnterState(isGrounded ? FighterState.NeutralGround : FighterState.NeutralAir);
    }

    private void RefreshGroundedStateFromKinematics()
    {
        isGrounded = position.y <= 0f && velocity.y <= 0f;
    }

    private void HandleJumpStartup(InputFrame input)
    {
        if (state != FighterState.JumpStartup)
            return;

        int moveX = Mathf.RoundToInt(Mathf.Clamp(input.moveX, -1f, 1f));
        if (queuedJumpMoveX == 0 && moveX != 0)
            queuedJumpMoveX = moveX;

        jumpStartupTicksRemaining--;
        if (jumpStartupTicksRemaining > 0)
            return;

        if (queuedJumpMoveX == -1)
            velocity.x = -config.moveSpeed * config.jumpHorizontalBoostScale;
        else if (queuedJumpMoveX == 1)
            velocity.x = config.moveSpeed * config.jumpHorizontalBoostScale;

        velocity.y = config.jumpForce;
        isGrounded = false;
        EnterState(FighterState.NeutralAir);
    }

    private void HandleMovement(InputFrame input)
    {
        if (!isGrounded)
            return;

        bool holdingDown = input.moveY < 0f;
        if (holdingDown && CanEnterCrouch())
        {
            velocity.x = 0f;
            EnterState(FighterState.Crouching);
            return;
        }

        if (state == FighterState.Crouching)
            EnterState(FighterState.NeutralGround);

        if (ShouldLockMovement())
        {
            velocity.x = 0f;
            return;
        }

        if (input.moveX == -1)
            velocity.x = -config.moveSpeed;
        else if (input.moveX == 1)
            velocity.x = config.moveSpeed;
        else
            ApplyFriction();

        if (input.moveY == 1 && IsActionableGrounded)
            StartJumpStartup(input.moveX);
    }

    private bool ShouldLockMovement()
    {
        if (state == FighterState.JumpStartup)
            return true;
        if (state == FighterState.Crouching)
            return true;
        if (state == FighterState.LandingRecovery)
            return true;
        if (
            state == FighterState.AttackStartup
            || state == FighterState.AttackActive
            || state == FighterState.AttackRecovery
        )
            return isGrounded;
        if (state == FighterState.Hitstun || state == FighterState.Blockstun || state == FighterState.Knockdown)
            return true;

        return false;
    }

    private bool IsActionableGrounded => state == FighterState.NeutralGround;

    private bool CanEnterCrouch()
    {
        return state == FighterState.NeutralGround || state == FighterState.Crouching;
    }

    private void StartJumpStartup(float moveX)
    {
        if (!isGrounded || state == FighterState.JumpStartup)
            return;

        queuedJumpMoveX = Mathf.RoundToInt(Mathf.Clamp(moveX, -1f, 1f));
        usedAirNormalThisJump = false;
        jumpStartupTicksRemaining = JUMP_STARTUP_TICKS;
        EnterState(FighterState.JumpStartup);
    }

    private void ApplyFriction()
    {
        if (velocity.x > 0f)
        {
            velocity.x -= config.groundFriction * GameLoop.FIXED_DT;
            if (velocity.x < 0f)
                velocity.x = 0f;
        }
        else if (velocity.x < 0f)
        {
            velocity.x += config.groundFriction * GameLoop.FIXED_DT;
            if (velocity.x > 0f)
                velocity.x = 0f;
        }
    }

    private void HandleAttacks(InputFrame input)
    {
        if (
            state == FighterState.AttackStartup
            || state == FighterState.AttackActive
            || state == FighterState.AttackRecovery
        )
            return;

        if (!CanStartAttack())
            return;

        if (input.punchLight)
            StartAttack(AttackType.Light, ResolveAttackData(AttackType.Light));
        else if (input.punchMedium)
            StartAttack(AttackType.Medium, ResolveAttackData(AttackType.Medium));
        else if (input.punchHeavy)
            StartAttack(AttackType.Heavy, ResolveAttackData(AttackType.Heavy));
    }

    private bool CanStartAttack()
    {
        if (state == FighterState.LandingRecovery)
            return CanCancelLandingRecoveryIntoAttack();

        if (isGrounded)
            return state == FighterState.NeutralGround;

        return state == FighterState.NeutralAir;
    }

    private void StartAttack(AttackType type, AttackData attackData)
    {
        if (attackData == null)
            return;

        currentAttackType = type;
        currentAttack = attackData;
        currentAttackStartedAirborne = !isGrounded;
        attackFrame = 0;
        hitbox.Reset();
        landingRecoveryTicksRemaining = 0;

        if (!isGrounded)
            usedAirNormalThisJump = true;

        EnterState(FighterState.AttackStartup);
    }

    public void StartAttack(AttackData attack)
    {
        if (attack == null)
            return;

        StartAttack(AttackType.Light, attack);
    }

    private AttackData ResolveAttackData(AttackType type)
    {
        AttackData configuredAttack = ResolveConfiguredAttackData(type, isGrounded);
        if (configuredAttack != null)
            return configuredAttack;

        if (type == AttackType.Light && config.lightAttackData != null)
            return config.lightAttackData;

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

    private AttackData ResolveConfiguredAttackData(AttackType type, bool useGroundedSet)
    {
        if (useGroundedSet)
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

    private void UpdateAttackDataAttack()
    {
        if (currentAttack == null)
            return;

        attackFrame++;

        if (state == FighterState.AttackStartup && attackFrame >= currentAttack.startupFrames)
        {
            EnterState(FighterState.AttackActive);
            hitbox.active = true;
            hitbox.hasHit = false;
            hitbox.damage = currentAttack.damage;
            hitbox.hitstunFrames = currentAttack.hitstunFrames;
        }

        if (state == FighterState.AttackActive)
        {
            Vector2 hitboxOffset = currentAttack.hitboxOffset;
            if (!facingRight)
                hitboxOffset.x = -hitboxOffset.x;

            hitbox.box = new Box(position + hitboxOffset, currentAttack.hitboxSize * 0.5f);

            int activeEndFrame = currentAttack.startupFrames + currentAttack.activeFrames;
            if (attackFrame >= activeEndFrame)
            {
                hitbox.active = false;
                EnterState(FighterState.AttackRecovery);
            }
        }

        if (state == FighterState.AttackRecovery)
        {
            int recoveryEndFrame =
                currentAttack.startupFrames + currentAttack.activeFrames + currentAttack.recoveryFrames;
            if (attackFrame >= recoveryEndFrame)
                EndAttackAndReturnNeutral();
        }
    }

    private void EndAttackAndReturnNeutral()
    {
        hitbox.Reset();
        currentAttack = null;
        currentAttackType = AttackType.None;
        currentAttackStartedAirborne = false;
        EnterState(isGrounded ? FighterState.NeutralGround : FighterState.NeutralAir);
    }

    public void ApplyHit(Hitbox hit)
    {
        ApplyHitstop(DEFENDER_HITSTOP_FRAMES);
        hitstunFramesRemaining = Mathf.Max(1, hit.hitstunFrames);
        hitbox.Reset();
        currentAttack = null;
        currentAttackType = AttackType.None;
        currentAttackStartedAirborne = false;
        EnterState(FighterState.Hitstun);
    }

    public void ApplySuccessfulHitstopAsAttacker()
    {
        ApplyHitstop(ATTACKER_HITSTOP_FRAMES);
    }

    public void ApplyHitstop(int frames)
    {
        if (frames <= 0)
            return;

        hitstopFramesRemaining = Mathf.Max(hitstopFramesRemaining, frames);
    }

    public void MarkCurrentHitboxAsSpent()
    {
        hitbox.hasHit = true;
    }

    private void ApplyGravity()
    {
        if (isGrounded)
        {
            velocity.y = 0f;
            return;
        }

        velocity.y += config.gravity * GameLoop.FIXED_DT;

        if (velocity.y < config.maxFallSpeed)
            velocity.y = config.maxFallSpeed;
    }

    private void Integrate()
    {
        position += velocity * GameLoop.FIXED_DT;
    }

    private void ResolveGroundContact()
    {
        bool wasAirborne = !isGrounded;
        if (position.y <= 0f)
        {
            position.y = 0f;
            velocity.y = 0f;
            isGrounded = true;

            if (wasAirborne)
            {
                if (
                    state != FighterState.Hitstun
                    && state != FighterState.Blockstun
                    && state != FighterState.Knockdown
                )
                {
                    if (
                        state == FighterState.AttackStartup
                        || state == FighterState.AttackActive
                        || state == FighterState.AttackRecovery
                    )
                        EndAttackAndReturnNeutral();

                    landingRecoveryTicksRemaining = LANDING_RECOVERY_TICKS;
                    EnterState(FighterState.LandingRecovery);
                }
            }
            else if (state == FighterState.NeutralAir)
            {
                EnterState(FighterState.NeutralGround);
            }
        }
        else
        {
            isGrounded = false;

            if (state == FighterState.NeutralGround || state == FighterState.LandingRecovery)
                EnterState(FighterState.NeutralAir);
        }
    }

    private void AdvanceLandingRecovery()
    {
        if (state != FighterState.LandingRecovery)
            return;

        if (landingRecoveryTicksRemaining > 0)
            landingRecoveryTicksRemaining--;

        if (landingRecoveryTicksRemaining <= 0)
            EnterState(FighterState.NeutralGround);
    }

    private bool CanCancelLandingRecoveryIntoAttack()
    {
        if (usedAirNormalThisJump)
            return false;

        int currentLandingFrame = LANDING_RECOVERY_TICKS - landingRecoveryTicksRemaining + 1;
        return currentLandingFrame == 2 || currentLandingFrame == 3;
    }

    private void EndTick()
    {
        if (!transitionedThisTick && !stateFrameFrozenThisTick)
            stateFrame++;

        UpdateFacing(opponent);

        BuildRenderSnapshot(forceRestart: false);
    }

    private void BuildRenderSnapshot(bool forceRestart)
    {
        FighterVisualState visualState = ResolveVisualState();
        AttackType visualAttackType = ResolveVisualAttackType(visualState);

        bool shouldRestart =
            forceRestart
            || visualState != lastVisualState
            || visualAttackType != lastVisualAttackType;

        if (shouldRestart)
        {
            animationSerial++;
            visualStateFrame = 0;
            lastVisualState = visualState;
            lastVisualAttackType = visualAttackType;
        }
        else
        {
            visualStateFrame++;
        }

        renderSnapshot = new FighterRenderSnapshot(
            visualState,
            visualAttackType,
            currentAttackStartedAirborne,
            visualStateFrame,
            animationSerial,
            shouldRestart,
            IsInHitstop
        );
    }

    private FighterVisualState ResolveVisualState()
    {
        if (state == FighterState.Hitstun)
            return FighterVisualState.Hitstun;
        if (state == FighterState.Blockstun)
            return FighterVisualState.Blockstun;
        if (state == FighterState.Knockdown)
            return FighterVisualState.Knockdown;
        if (state == FighterState.JumpStartup)
            return FighterVisualState.JumpStartup;
        if (state == FighterState.Crouching)
            return FighterVisualState.Crouching;
        if (state == FighterState.NeutralAir)
            return FighterVisualState.Airborne;
        if (state == FighterState.LandingRecovery)
            return FighterVisualState.Landing;
        if (state == FighterState.AttackStartup)
            return FighterVisualState.AttackStartup;
        if (state == FighterState.AttackActive)
            return FighterVisualState.AttackActive;
        if (state == FighterState.AttackRecovery)
            return FighterVisualState.AttackRecovery;

        if (Mathf.Abs(velocity.x) > 0.01f)
            return IsMovingForward() ? FighterVisualState.WalkForward : FighterVisualState.WalkBackward;

        return FighterVisualState.Idle;
    }

    private AttackType ResolveVisualAttackType(FighterVisualState visualState)
    {
        if (
            visualState == FighterVisualState.AttackStartup
            || visualState == FighterVisualState.AttackActive
            || visualState == FighterVisualState.AttackRecovery
        )
            return currentAttackType;

        return AttackType.None;
    }

    private bool IsMovingForward()
    {
        if (Mathf.Abs(velocity.x) <= 0.01f)
            return false;

        return facingRight ? velocity.x > 0f : velocity.x < 0f;
    }

    private void EnterState(FighterState nextState)
    {
        if (state == nextState)
            return;

        state = nextState;
        stateFrame = 0;
        transitionedThisTick = true;
    }

    public void UpdateFacing(Fighter opponent)
    {
        if (opponent == null)
            return;

        facingRight = opponent.position.x > position.x;
    }

    public void Render()
    {
        view.SetPosition(position);
        view.SetFacing(facingRight);
    }

    public void SetOpponent(Fighter opponent)
    {
        this.opponent = opponent;
    }

    public void MoveHorizontal(float deltaX)
    {
        position.x += deltaX;
    }

    public void SetHorizontal(float newX)
    {
        position.x = newX;
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
    Crouching,
    WalkForward,
    WalkBackward,
    JumpStartup,
    Airborne,
    Landing,
    AttackStartup,
    AttackActive,
    AttackRecovery,
    Hitstun,
    Blockstun,
    Knockdown,
}

public readonly struct FighterRenderSnapshot
{
    public readonly FighterVisualState visualState;
    public readonly AttackType attackType;
    public readonly bool attackIsAirborne;
    public readonly int visualStateFrame;
    public readonly uint animationSerial;
    public readonly bool restartAnimation;
    public readonly bool freezeAnimation;

    public FighterRenderSnapshot(
        FighterVisualState visualState,
        AttackType attackType,
        bool attackIsAirborne,
        int visualStateFrame,
        uint animationSerial,
        bool restartAnimation,
        bool freezeAnimation
    )
    {
        this.visualState = visualState;
        this.attackType = attackType;
        this.attackIsAirborne = attackIsAirborne;
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
