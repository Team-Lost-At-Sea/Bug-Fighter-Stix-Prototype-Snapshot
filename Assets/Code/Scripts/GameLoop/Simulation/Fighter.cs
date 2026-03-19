using UnityEngine;

// Fighter.cs drives deterministic simulation for one fighter.
// Gameplay timing is fully frame-based and independent from Animator timing.
public class Fighter
{
    public static int HitstopFrames { get; set; } = 8;

    public Vector2 Position => position;
    public Vector2 Velocity => velocity;
    public bool FacingRight => facingRight;
    public float PushboxHalfWidth => config.pushboxHalfWidth;
    public bool IsGrounded => isGrounded;
    public bool HasActiveUnspentHitbox => attackController.HasActiveUnspentHitbox;
    public Hitbox CurrentHitbox => attackController.CurrentHitbox;
    public Box CurrentHurtbox => new Box(position, hurtbox.halfSize);
    public FighterState CurrentState => state;
    public int StateFrame => stateFrame;
    public MoveType CurrentMoveType => attackController.CurrentMoveType;
    public bool IsInHitstop => hitstopFramesRemaining > 0;
    public bool IsHoldingValidBlockDirection => isHoldingValidBlockDirection;
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

    private readonly FighterAttackController attackController = new FighterAttackController();
    private readonly FighterMovementController movementController = new FighterMovementController();
    private readonly FighterRenderStateBuilder renderStateBuilder = new FighterRenderStateBuilder();
    private Box hurtbox;

    private int hitstopFramesRemaining;
    private int hitstunFramesRemaining;
    private bool isHoldingValidBlockDirection;
    private bool hadAttackInputThisTick;

    private FighterRenderSnapshot renderSnapshot;

    public Fighter(FighterView view, Vector2 startPosition)
    {
        this.view = view;
        config = view.Config;
        position = startPosition;
        velocity = Vector2.zero;
        hurtbox = new Box(position, config.hurtboxHalfSize);
        renderSnapshot = renderStateBuilder.BuildSnapshot(
            state,
            velocity,
            facingRight,
            attackController.CurrentMoveType,
            hadAttackInputThisTick,
            IsInHitstop,
            forceRestart: true
        );
    }

    public void Tick(InputFrame input)
    {
        transitionedThisTick = false;
        stateFrameFrozenThisTick = false;
        hadAttackInputThisTick = input.punchLight || input.punchMedium || input.punchHeavy;
        UpdateBlockDirectionHold(input);
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
            movementController.ApplyGravity(isGrounded, ref velocity, config);
            movementController.Integrate(ref position, velocity);
            ResolveGroundContact();
            AdvanceLandingRecovery();
        }

        EndTick();
    }

    private void UpdateBlockDirectionHold(InputFrame input)
    {
        int backDirection = facingRight ? -1 : 1;
        bool holdingBack = Mathf.RoundToInt(Mathf.Clamp(input.moveX, -1f, 1f)) == backDirection;
        isHoldingValidBlockDirection = isGrounded && holdingBack;
    }

    private void SimulateHitstun()
    {
        if (hitstunFramesRemaining > 0)
            hitstunFramesRemaining--;

        movementController.ApplyGravity(isGrounded, ref velocity, config);
        movementController.Integrate(ref position, velocity);
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
        if (
            movementController.HandleJumpStartup(state, input, config, ref velocity, ref isGrounded)
        )
            EnterState(FighterState.NeutralAir);
    }

    private void HandleMovement(InputFrame input)
    {
        if (!isGrounded)
            return;

        bool holdingDown = input.moveY < 0f;
        bool hasAttackInput = input.punchLight || input.punchMedium || input.punchHeavy;
        if (holdingDown && state == FighterState.Crouching)
        {
            velocity.x = 0f;
            return;
        }

        if (state == FighterState.Crouching && hasAttackInput)
        {
            velocity.x = 0f;
            return;
        }

        if (holdingDown && FighterStateRules.CanEnterCrouchFromIdle(state, isGrounded))
        {
            velocity.x = 0f;
            EnterState(FighterState.Crouching);
            return;
        }

        if (state == FighterState.Crouching)
            EnterState(FighterState.NeutralGround);

        if (FighterStateRules.ShouldLockMovement(state, isGrounded))
        {
            velocity.x = 0f;
            return;
        }

        if (input.moveX == -1)
            velocity.x = -config.moveSpeed;
        else if (input.moveX == 1)
            velocity.x = config.moveSpeed;
        else
            movementController.ApplyFriction(ref velocity, config);

        if (input.moveY == 1 && FighterStateRules.IsActionableGrounded(state))
            StartJumpStartup(input.moveX);
    }

    private void StartJumpStartup(float moveX)
    {
        if (!isGrounded || state == FighterState.JumpStartup)
            return;

        movementController.StartJumpStartup(moveX);
        EnterState(FighterState.JumpStartup);
    }

    private void HandleAttacks(InputFrame input)
    {
        if (FighterStateRules.IsAttackState(state))
            return;

        if (!CanStartAttack())
            return;

        if (input.punchLight)
            StartAttack(ResolveMoveType(AttackStrength.Light, input));
        else if (input.punchMedium)
            StartAttack(ResolveMoveType(AttackStrength.Medium, input));
        else if (input.punchHeavy)
            StartAttack(ResolveMoveType(AttackStrength.Heavy, input));
    }

    private bool CanStartAttack()
    {
        return FighterStateRules.CanStartAttack(
            state,
            isGrounded,
            movementController.CanCancelLandingRecoveryIntoAttack()
        );
    }

    private void StartAttack(MoveType moveType)
    {
        AttackData attackData = ResolveAttackData(moveType);
        if (!attackController.StartAttack(moveType, attackData))
            return;

        if (!isGrounded)
            movementController.MarkAirNormalUsed();

        movementController.ClearLandingRecovery();

        EnterState(FighterState.AttackStartup);
    }

    public void StartAttack(AttackData attack)
    {
        if (!attackController.StartAttack(MoveType.StandingLight, attack))
            return;

        movementController.ClearLandingRecovery();
        EnterState(FighterState.AttackStartup);
    }

    private AttackData ResolveAttackData(MoveType moveType)
    {
        return attackController.ResolveAttackData(moveType, config, facingRight);
    }

    private MoveType ResolveMoveType(AttackStrength strength, InputFrame input)
    {
        if (!isGrounded)
        {
            switch (strength)
            {
                case AttackStrength.Light:
                    return MoveType.JumpingLight;
                case AttackStrength.Medium:
                    return MoveType.JumpingMedium;
                case AttackStrength.Heavy:
                    return MoveType.JumpingHeavy;
                default:
                    return MoveType.None;
            }
        }

        bool crouchIntent = state == FighterState.Crouching || input.moveY < 0f;
        if (crouchIntent)
        {
            switch (strength)
            {
                case AttackStrength.Light:
                    return MoveType.CrouchingLight;
                case AttackStrength.Medium:
                    return MoveType.CrouchingMedium;
                case AttackStrength.Heavy:
                    return MoveType.CrouchingHeavy;
                default:
                    return MoveType.None;
            }
        }

        switch (strength)
        {
            case AttackStrength.Light:
                return MoveType.StandingLight;
            case AttackStrength.Medium:
                return MoveType.StandingMedium;
            case AttackStrength.Heavy:
                return MoveType.StandingHeavy;
            default:
                return MoveType.None;
        }
    }

    private void UpdateAttackDataAttack()
    {
        AttackUpdateOutcome outcome = attackController.Update(state, position, facingRight);

        if (outcome.enterActive)
            EnterState(FighterState.AttackActive);

        if (outcome.enterRecovery)
            EnterState(FighterState.AttackRecovery);

        if (outcome.endAttack)
            EndAttackAndReturnNeutral();
    }

    private void EndAttackAndReturnNeutral()
    {
        attackController.EndAttack();
        EnterState(isGrounded ? FighterState.NeutralGround : FighterState.NeutralAir);
    }

    public void ApplyHit(Hitbox hit)
    {
        ApplyHitstop(HitstopFrames);
        hitstunFramesRemaining = Mathf.Max(1, hit.hitstunFrames);
        attackController.EndAttack();
        EnterState(FighterState.Hitstun);
    }

    public void ApplySuccessfulHitstopAsAttacker()
    {
        ApplyHitstop(HitstopFrames);
    }

    public void ApplyHitstop(int frames)
    {
        if (frames <= 0)
            return;

        hitstopFramesRemaining = Mathf.Max(hitstopFramesRemaining, frames);
    }

    public void MarkCurrentHitboxAsSpent()
    {
        attackController.MarkCurrentHitboxAsSpent();
    }

    private void ResolveGroundContact()
    {
        GroundContactResult contact = movementController.ResolveGroundContact(
            ref position,
            ref velocity,
            ref isGrounded
        );

        if (isGrounded)
        {
            if (contact.landedFromAir)
            {
                if (FighterStateRules.ShouldApplyLandingRecovery(state))
                {
                    if (FighterStateRules.IsAttackState(state))
                        EndAttackAndReturnNeutral();

                    movementController.StartLandingRecovery();
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
            if (state == FighterState.NeutralGround || state == FighterState.LandingRecovery)
                EnterState(FighterState.NeutralAir);
        }
    }

    private void AdvanceLandingRecovery()
    {
        if (movementController.AdvanceLandingRecovery(state))
            EnterState(FighterState.NeutralGround);
    }

    private void EndTick()
    {
        if (!transitionedThisTick && !stateFrameFrozenThisTick)
            stateFrame++;

        UpdateFacing(opponent);
        renderSnapshot = renderStateBuilder.BuildSnapshot(
            state,
            velocity,
            facingRight,
            attackController.CurrentMoveType,
            hadAttackInputThisTick,
            IsInHitstop,
            forceRestart: false
        );
    }

    private void EnterState(FighterState nextState)
    {
        if (state == nextState)
            return;

        FighterState previousState = state;
        state = nextState;
        stateFrame = 0;
        transitionedThisTick = true;

        if (ShouldLogCrouchTransition(previousState, nextState))
        {
            string fighterName = view != null ? view.name : "UnknownFighter";
            Debug.Log(
                $"[CrouchState] {fighterName} {previousState} -> {nextState} " +
                $"grounded={isGrounded} posY={position.y:F3}"
            );
        }
    }

    private static bool ShouldLogCrouchTransition(FighterState previousState, FighterState nextState)
    {
        if (GameInput.Instance == null || !GameInput.Instance.VerboseCrouchDebug)
            return false;

        return previousState == FighterState.Crouching || nextState == FighterState.Crouching;
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

    private enum AttackStrength
    {
        Light,
        Medium,
        Heavy,
    }
}
