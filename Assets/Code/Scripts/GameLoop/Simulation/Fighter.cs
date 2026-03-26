using UnityEngine;
using System.Text;

// Fighter.cs drives deterministic simulation for one fighter.
// Gameplay timing is fully frame-based and independent from Animator timing.
public class Fighter
{
    public static int HitstopFrames { get; set; } = 8;
    public static bool LogSpecialInputReads { get; set; } = true;
    private const int NormalInputBufferFrames = 4;
    private const int DebugInputHistoryDisplayLength = 30;
    private const int DebugInputFreezeFrames = 75;
    private const string DebugLightPunchColor = "#F4D03F";
    private const string DebugMediumPunchColor = "#3498DB";
    private const string DebugHeavyPunchColor = "#8E44AD";
    private const string DebugSeparatorColor = "#000000";

    public Vector2 Position => position;
    public Vector2 Velocity => velocity;
    public bool FacingRight => facingRight;
    public float PushboxHalfWidth => config.pushboxHalfWidth;
    public bool IsGrounded => isGrounded;
    public bool HasActiveUnspentHitbox => attackController.HasActiveUnspentHitbox;
    public Hitbox CurrentHitbox => attackController.CurrentHitbox;
    public Box CurrentHurtbox => new Box(GetHurtboxCenter(), config.hurtboxHalfSize);
    public FighterState CurrentState => state;
    public FighterAirPhase CurrentAirPhase => GetAirPhase();
    public bool IsRisingInAir => GetAirPhase() == FighterAirPhase.Rising;
    public bool IsFallingInAir => GetAirPhase() == FighterAirPhase.Falling;
    public int StateFrame => stateFrame;
    public MoveType CurrentMoveType => attackController.CurrentMoveType;
    public bool IsInHitstop => hitstopFramesRemaining > 0;
    public bool IsHoldingBlockInput => isHoldingBlockInput;
    public bool CanCurrentlyBlock => canCurrentlyBlock;
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

    // Dedicated controllers keep Fighter focused on state orchestration.
    private readonly FighterAttackController attackController = new FighterAttackController();
    private readonly FighterMovementController movementController = new FighterMovementController();
    private readonly FighterRenderStateBuilder renderStateBuilder = new FighterRenderStateBuilder();
    private readonly InputHistoryBuffer inputHistory = new InputHistoryBuffer(90);

    private int hitstopFramesRemaining;
    private int hitstunFramesRemaining;
    private bool isHoldingBlockInput;
    private bool canCurrentlyBlock;
    private bool isHoldingValidBlockDirection;
    private bool hadAttackInputThisTick;
    private int debugInputHistoryFreezeFramesRemaining;
    private string debugInputHistoryDisplay = "No input history yet";
    private bool hasPendingProjectileSpawn;
    private ProjectileSpawnRequest pendingProjectileSpawn;
    private int lightPressBufferFramesRemaining;
    private int mediumPressBufferFramesRemaining;
    private int heavyPressBufferFramesRemaining;

    private FighterRenderSnapshot renderSnapshot;

    public Fighter(FighterView view, Vector2 startPosition)
    {
        this.view = view;
        config = view.Config;
        position = startPosition;
        velocity = Vector2.zero;
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
        // Tick order is intentional and should stay stable for gameplay feel:
        // 1) sample block intent, 2) resolve freeze states, 3) run gameplay logic,
        // 4) finalize facing/render snapshot in EndTick.
        transitionedThisTick = false;
        stateFrameFrozenThisTick = false;
        hadAttackInputThisTick = input.punchLight || input.punchMedium || input.punchHeavy;
        inputHistory.Push(input, facingRight);
        UpdateNormalPressBuffers(input);
        UpdateInputHistoryDebugDisplay(input);
        UpdateBlockingState(input);
        if (hitstopFramesRemaining > 0)
        {
            hitstopFramesRemaining--;
            stateFrameFrozenThisTick = true;
            UpdateBlockingState(input);
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
            UpdateAttackDataAttack(input);
            movementController.ApplyGravity(isGrounded, ref velocity, config);
            movementController.Integrate(ref position, velocity);
            ResolveGroundContact();
            AdvanceLandingRecovery();
        }

        UpdateBlockingState(input);
        EndTick();
    }

    public string GetInputHistoryDebugString(int maxEntries = 10)
    {
        if (maxEntries == DebugInputHistoryDisplayLength)
            return debugInputHistoryDisplay;

        return BuildInputHistoryDebugString(maxEntries);
    }

    private void UpdateInputHistoryDebugDisplay(InputFrame input)
    {
        if (input.HasAttackPress)
        {
            debugInputHistoryDisplay = BuildInputHistoryDebugStringEndingAtAttackPress(DebugInputHistoryDisplayLength);
            debugInputHistoryFreezeFramesRemaining = DebugInputFreezeFrames;
            return;
        }

        if (debugInputHistoryFreezeFramesRemaining > 0)
        {
            debugInputHistoryFreezeFramesRemaining--;
            return;
        }

        debugInputHistoryDisplay = BuildInputHistoryDebugString(DebugInputHistoryDisplayLength);
    }

    private string BuildInputHistoryDebugString(int maxEntries)
    {
        StringBuilder builder = new StringBuilder();
        int entriesToShow = Mathf.Min(maxEntries, inputHistory.Count);

        for (int framesAgo = entriesToShow - 1; framesAgo >= 0; framesAgo--)
        {
            InputHistoryBuffer.HistoryEntry entry = inputHistory.GetRecent(framesAgo);
            if (builder.Length > 0)
                builder.Append($" <color={DebugSeparatorColor}>|</color> ");

            builder.Append(entry.relativeDirection);

            if (entry.input.punchLightPressed)
                builder.Append($" <color={DebugLightPunchColor}>LP</color>");
            if (entry.input.punchMediumPressed)
                builder.Append($" <color={DebugMediumPunchColor}>MP</color>");
            if (entry.input.punchHeavyPressed)
                builder.Append($" <color={DebugHeavyPunchColor}>HP</color>");
        }

        if (builder.Length == 0)
            builder.Append("No input history yet");

        return builder.ToString();
    }

    private string BuildInputHistoryDebugStringEndingAtAttackPress(int maxEntries)
    {
        int attackPressFramesAgo = -1;
        int searchCount = Mathf.Min(maxEntries - 1, inputHistory.Count - 1);
        for (int i = 0; i <= searchCount; i++)
        {
            if (inputHistory.GetRecent(i).input.HasAttackPress)
            {
                attackPressFramesAgo = i;
                break;
            }
        }

        if (attackPressFramesAgo < 0)
            return BuildInputHistoryDebugString(maxEntries);

        StringBuilder builder = new StringBuilder();
        int oldestFramesAgo = Mathf.Min(inputHistory.Count - 1, attackPressFramesAgo + (maxEntries - 1));

        for (int framesAgo = oldestFramesAgo; framesAgo >= attackPressFramesAgo; framesAgo--)
        {
            InputHistoryBuffer.HistoryEntry entry = inputHistory.GetRecent(framesAgo);
            if (builder.Length > 0)
                builder.Append($" <color={DebugSeparatorColor}>|</color> ");

            builder.Append(entry.relativeDirection);

            if (entry.input.punchLightPressed)
                builder.Append($" <color={DebugLightPunchColor}>LP</color>");
            if (entry.input.punchMediumPressed)
                builder.Append($" <color={DebugMediumPunchColor}>MP</color>");
            if (entry.input.punchHeavyPressed)
                builder.Append($" <color={DebugHeavyPunchColor}>HP</color>");
        }

        if (builder.Length == 0)
            builder.Append("No input history yet");

        return builder.ToString();
    }

    private void UpdateBlockingState(InputFrame input)
    {
        // "Holding block" and "can block" are tracked separately so visuals/gameplay
        // can reason about intent vs availability independently.
        int backDirection = facingRight ? -1 : 1;
        bool holdingBack = Mathf.RoundToInt(Mathf.Clamp(input.moveX, -1f, 1f)) == backDirection;
        isHoldingBlockInput = holdingBack;
        canCurrentlyBlock = FighterStateRules.CanHoldBlock(state, isGrounded);
        isHoldingValidBlockDirection = isHoldingBlockInput && canCurrentlyBlock;
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
            movementController.HandleJumpStartup(
                state,
                stateFrame,
                input,
                config,
                ref velocity,
                ref isGrounded
            )
        )
            EnterState(FighterState.NeutralAir);
    }

    private void HandleMovement(InputFrame input)
    {
        if (!isGrounded)
            return;

        bool holdingDown = input.moveY < 0f;
        bool hasAttackInput = input.punchLight || input.punchMedium || input.punchHeavy;

        // While crouching, down-hold or crouch-attack input keeps horizontal velocity locked.
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

        if (TryResolveGroundedSpecial(input, out MoveType specialMoveType))
        {
            StartAttack(specialMoveType);
            return;
        }

        if (lightPressBufferFramesRemaining > 0)
        {
            StartAttack(ResolveMoveType(AttackStrength.Light, input));
            lightPressBufferFramesRemaining = 0;
        }
        else if (mediumPressBufferFramesRemaining > 0)
        {
            StartAttack(ResolveMoveType(AttackStrength.Medium, input));
            mediumPressBufferFramesRemaining = 0;
        }
        else if (heavyPressBufferFramesRemaining > 0)
        {
            StartAttack(ResolveMoveType(AttackStrength.Heavy, input));
            heavyPressBufferFramesRemaining = 0;
        }
    }

    private bool CanStartAttack()
    {
        return FighterStateRules.CanStartAttack(
            state,
            isGrounded,
            movementController.CanCancelLandingRecoveryIntoAttack()
        );
    }

    private bool TryResolveGroundedSpecial(InputFrame input, out MoveType moveType)
    {
        const int quarterCircleForwardWindow = 20;
        moveType = MoveType.None;

        if (!isGrounded || !input.HasAttackPress)
            return false;

        if (!ContainsQuarterCircleForward(quarterCircleForwardWindow))
            return false;

        if (input.punchLightPressed)
            moveType = MoveType.FireballLight;
        else if (input.punchMediumPressed)
            moveType = MoveType.FireballMedium;
        else if (input.punchHeavyPressed)
            moveType = MoveType.FireballHeavy;

        if (moveType != MoveType.None && LogSpecialInputReads)
            Debug.Log($"[SpecialInput] Fireball read: {moveType}");

        return moveType != MoveType.None;
    }

    private bool ContainsQuarterCircleForward(int maxFrames)
    {
        int searchCount = inputHistory.Count < maxFrames ? inputHistory.Count : maxFrames;
        int requiredStep = 0;

        // Search backward from the button-press frame.
        // We accept extra repeated directions and neutral frames, but require
        // the core sequence 6 <- 3 <- 2 to appear in order.
        for (int framesAgo = 0; framesAgo < searchCount; framesAgo++)
        {
            int direction = inputHistory.GetRecent(framesAgo).relativeDirection;

            switch (requiredStep)
            {
                case 0:
                    if (direction == 6 || direction == 3)
                        requiredStep = 1;
                    break;
                case 1:
                    if (direction == 3)
                        requiredStep = 2;
                    else if (direction == 2)
                        requiredStep = 3;
                    break;
                case 2:
                    if (direction == 2)
                        return true;
                    break;
                case 3:
                    return true;
            }
        }

        return false;
    }

    private void UpdateNormalPressBuffers(InputFrame input)
    {
        if (lightPressBufferFramesRemaining > 0)
            lightPressBufferFramesRemaining--;
        if (mediumPressBufferFramesRemaining > 0)
            mediumPressBufferFramesRemaining--;
        if (heavyPressBufferFramesRemaining > 0)
            heavyPressBufferFramesRemaining--;

        if (input.punchLightPressed)
            lightPressBufferFramesRemaining = NormalInputBufferFrames;
        if (input.punchMediumPressed)
            mediumPressBufferFramesRemaining = NormalInputBufferFrames;
        if (input.punchHeavyPressed)
            heavyPressBufferFramesRemaining = NormalInputBufferFrames;
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

    private void UpdateAttackDataAttack(InputFrame input)
    {
        // Attack controller owns frame counting and phase boundaries; Fighter maps those
        // outcomes into simulation states.
        AttackUpdateOutcome outcome = attackController.Update(state, position, facingRight);

        if (outcome.enterActive)
        {
            TryQueueProjectileSpawnForCurrentMove();
            EnterState(FighterState.AttackActive);
        }

        if (outcome.enterRecovery)
            EnterState(FighterState.AttackRecovery);

        if (outcome.endAttack)
            EndAttackAndReturnPostAttackState(input);
    }

    private void EndAttackAndReturnPostAttackState(InputFrame input)
    {
        // Choose post-attack state before the next frame so we do not force extra neutral
        // frames that can create visual/feel artifacts (especially around crouch normals).
        MoveType completedMoveType = attackController.CurrentMoveType;
        attackController.EndAttack();

        if (!isGrounded)
        {
            EnterState(FighterState.NeutralAir);
            return;
        }

        bool holdCrouch = input.moveY < 0f;
        bool wasCrouchingAttack = IsCrouchingMoveType(completedMoveType);
        if (holdCrouch && wasCrouchingAttack)
        {
            EnterState(FighterState.Crouching);
            return;
        }

        EnterState(FighterState.NeutralGround);
    }

    private void TryQueueProjectileSpawnForCurrentMove()
    {
        MoveType moveType = attackController.CurrentMoveType;
        if (!moveType.IsFireball())
            return;

        float direction = facingRight ? 1f : -1f;
        Vector2 spawnOffset = config.fireballSpawnOffset;
        spawnOffset.x *= direction;
        pendingProjectileSpawn = new ProjectileSpawnRequest(
            owner: this,
            position: position + spawnOffset,
            velocity: new Vector2(config.fireballProjectileSpeedPerFrame * direction, 0f),
            halfSize: config.fireballProjectileHalfSize,
            lifetimeFrames: config.fireballProjectileLifetimeFrames,
            damage: config.fireballProjectileDamage,
            hitstunFrames: config.fireballProjectileHitstunFrames,
            sprite: config.fireballProjectileSprite,
            tint: config.fireballProjectileTint
        );
        hasPendingProjectileSpawn = true;
    }

    public bool TryConsumeProjectileSpawnRequest(out ProjectileSpawnRequest request)
    {
        if (!hasPendingProjectileSpawn)
        {
            request = default;
            return false;
        }

        request = pendingProjectileSpawn;
        hasPendingProjectileSpawn = false;
        return true;
    }

    private static bool IsCrouchingMoveType(MoveType moveType)
    {
        return moveType == MoveType.CrouchingLight
            || moveType == MoveType.CrouchingMedium
            || moveType == MoveType.CrouchingHeavy;
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
        // Ground contact can force state changes (landing recovery, air->ground, ground->air)
        // after movement integration has updated position/velocity.
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
                        EndAttackAndReturnPostAttackState(InputFrame.Neutral);

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
        // State frame advances only when no transition/freeze occurred this tick.
        if (!transitionedThisTick && !stateFrameFrozenThisTick)
            stateFrame++;

        // Facing and render snapshot are finalized last so rendering sees authoritative
        // post-simulation state for this tick.
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

    public FighterAirPhase GetAirPhase()
    {
        // Small deadzone around 0 avoids rapid rising/falling toggles near apex.
        if (isGrounded)
            return FighterAirPhase.Grounded;

        if (velocity.y > 0.01f)
            return FighterAirPhase.Rising;

        if (velocity.y < -0.01f)
            return FighterAirPhase.Falling;

        return FighterAirPhase.Apex;
    }

    private void EnterState(FighterState nextState)
    {
        if (state == nextState)
            return;

        // Every state transition resets frame counter and marks this tick as transitioned
        // so timing code can reliably key off stateFrame.
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

        // Preserve facing for the full airborne arc. This keeps left/right-sensitive
        // inputs (like QCF/QCB) stable until the fighter lands.
        if (!isGrounded)
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

    public void ClearInputHistory()
    {
        inputHistory.Clear();
        debugInputHistoryFreezeFramesRemaining = 0;
        debugInputHistoryDisplay = "No input history yet";
    }

    public void MoveHorizontal(float deltaX)
    {
        position.x += deltaX;
    }

    public void SetHorizontal(float newX)
    {
        position.x = newX;
    }

    private Vector2 GetHurtboxCenter()
    {
        // Position is the feet anchor. Lift hurtbox by half-height so its base sits at feet,
        // then allow per-character fine tuning through hurtboxOffsetFromFeet.
        Vector2 centerOffset = new Vector2(0f, config.hurtboxHalfSize.y) + config.hurtboxOffsetFromFeet;
        return position + centerOffset;
    }

    private enum AttackStrength
    {
        Light,
        Medium,
        Heavy,
    }

}
