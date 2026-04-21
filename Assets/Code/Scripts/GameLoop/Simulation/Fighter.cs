using UnityEngine;
using System.Text;
using System;

// Fighter.cs drives deterministic simulation for one fighter.
// Gameplay timing is fully frame-based and independent from Animator timing.
public class Fighter
{
    public readonly struct Snapshot
    {
        public readonly Vector2 position;
        public readonly Vector2 velocity;
        public readonly bool isGrounded;
        public readonly bool facingRight;
        public readonly FighterState state;
        public readonly int stateFrame;
        public readonly bool transitionedThisTick;
        public readonly bool stateFrameFrozenThisTick;
        public readonly int hitstopFramesRemaining;
        public readonly int hitstunFramesRemaining;
        public readonly int blockstunFramesRemaining;
        public readonly bool isHoldingBlockInput;
        public readonly bool canCurrentlyBlock;
        public readonly bool isHoldingValidBlockDirection;
        public readonly bool parryInputPressedThisTick;
        public readonly int lastMoveXSign;
        public readonly int parryWindowFramesRemaining;
        public readonly int parryLockoutFramesRemaining;
        public readonly int health;
        public readonly int totalDamageTaken;
        public readonly int totalChipDamageTaken;
        public readonly HitResultType lastReceivedHitResult;
        public readonly HitLevel lastReceivedHitLevel;
        public readonly int lastReceivedStunFrames;
        public readonly int lastReceivedChipDamage;
        public readonly bool hadAttackInputThisTick;
        public readonly int debugInputHistoryFreezeFramesRemaining;
        public readonly string debugInputHistoryDisplay;
        public readonly bool hasPendingProjectileSpawn;
        public readonly ProjectileSpawnRequest pendingProjectileSpawn;
        public readonly int lightPressBufferFramesRemaining;
        public readonly int mediumPressBufferFramesRemaining;
        public readonly int heavyPressBufferFramesRemaining;
        public readonly bool isDefeated;
        public readonly FighterRenderSnapshot renderSnapshot;
        public readonly FighterAttackController.Snapshot attackSnapshot;
        public readonly FighterMovementController.Snapshot movementSnapshot;
        public readonly FighterRenderStateBuilder.Snapshot renderStateBuilderSnapshot;
        public readonly InputHistoryBuffer.Snapshot inputHistorySnapshot;

        public Snapshot(
            Vector2 position,
            Vector2 velocity,
            bool isGrounded,
            bool facingRight,
            FighterState state,
            int stateFrame,
            bool transitionedThisTick,
            bool stateFrameFrozenThisTick,
            int hitstopFramesRemaining,
            int hitstunFramesRemaining,
            int blockstunFramesRemaining,
            bool isHoldingBlockInput,
            bool canCurrentlyBlock,
            bool isHoldingValidBlockDirection,
            bool parryInputPressedThisTick,
            int lastMoveXSign,
            int parryWindowFramesRemaining,
            int parryLockoutFramesRemaining,
            int health,
            int totalDamageTaken,
            int totalChipDamageTaken,
            HitResultType lastReceivedHitResult,
            HitLevel lastReceivedHitLevel,
            int lastReceivedStunFrames,
            int lastReceivedChipDamage,
            bool hadAttackInputThisTick,
            int debugInputHistoryFreezeFramesRemaining,
            string debugInputHistoryDisplay,
            bool hasPendingProjectileSpawn,
            ProjectileSpawnRequest pendingProjectileSpawn,
            int lightPressBufferFramesRemaining,
            int mediumPressBufferFramesRemaining,
            int heavyPressBufferFramesRemaining,
            bool isDefeated,
            FighterRenderSnapshot renderSnapshot,
            FighterAttackController.Snapshot attackSnapshot,
            FighterMovementController.Snapshot movementSnapshot,
            FighterRenderStateBuilder.Snapshot renderStateBuilderSnapshot,
            InputHistoryBuffer.Snapshot inputHistorySnapshot
        )
        {
            this.position = position;
            this.velocity = velocity;
            this.isGrounded = isGrounded;
            this.facingRight = facingRight;
            this.state = state;
            this.stateFrame = stateFrame;
            this.transitionedThisTick = transitionedThisTick;
            this.stateFrameFrozenThisTick = stateFrameFrozenThisTick;
            this.hitstopFramesRemaining = hitstopFramesRemaining;
            this.hitstunFramesRemaining = hitstunFramesRemaining;
            this.blockstunFramesRemaining = blockstunFramesRemaining;
            this.isHoldingBlockInput = isHoldingBlockInput;
            this.canCurrentlyBlock = canCurrentlyBlock;
            this.isHoldingValidBlockDirection = isHoldingValidBlockDirection;
            this.parryInputPressedThisTick = parryInputPressedThisTick;
            this.lastMoveXSign = lastMoveXSign;
            this.parryWindowFramesRemaining = parryWindowFramesRemaining;
            this.parryLockoutFramesRemaining = parryLockoutFramesRemaining;
            this.health = health;
            this.totalDamageTaken = totalDamageTaken;
            this.totalChipDamageTaken = totalChipDamageTaken;
            this.lastReceivedHitResult = lastReceivedHitResult;
            this.lastReceivedHitLevel = lastReceivedHitLevel;
            this.lastReceivedStunFrames = lastReceivedStunFrames;
            this.lastReceivedChipDamage = lastReceivedChipDamage;
            this.hadAttackInputThisTick = hadAttackInputThisTick;
            this.debugInputHistoryFreezeFramesRemaining = debugInputHistoryFreezeFramesRemaining;
            this.debugInputHistoryDisplay = debugInputHistoryDisplay;
            this.hasPendingProjectileSpawn = hasPendingProjectileSpawn;
            this.pendingProjectileSpawn = pendingProjectileSpawn;
            this.lightPressBufferFramesRemaining = lightPressBufferFramesRemaining;
            this.mediumPressBufferFramesRemaining = mediumPressBufferFramesRemaining;
            this.heavyPressBufferFramesRemaining = heavyPressBufferFramesRemaining;
            this.isDefeated = isDefeated;
            this.renderSnapshot = renderSnapshot;
            this.attackSnapshot = attackSnapshot;
            this.movementSnapshot = movementSnapshot;
            this.renderStateBuilderSnapshot = renderStateBuilderSnapshot;
            this.inputHistorySnapshot = inputHistorySnapshot;
        }
    }

    public static int HitstopFrames { get; set; } = 8;
    public static bool LogSpecialInputReads { get; set; } = true;
    public static bool EnableParry { get; set; }
    public static int ParryActiveWindowFrames { get; set; } = 3;
    public static int ParryWhiffLockoutFrames { get; set; } = 12;
    private const int NormalInputBufferFrames = 4;
    private const int ThrowInputWindowFrames = 3;
    private const int DebugInputHistoryDisplayLength = 30;
    private const int DebugInputFreezeFrames = 75;
    private const string DebugLightPunchColor = "#F4D03F";
    private const string DebugMediumPunchColor = "#3498DB";
    private const string DebugHeavyPunchColor = "#8E44AD";
    private const string DebugDirtColor = "#8B5A2B";
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
    public int HitstunFramesRemaining => hitstunFramesRemaining;
    public int BlockstunFramesRemaining => blockstunFramesRemaining;
    public bool IsHoldingBlockInput => isHoldingBlockInput;
    public bool CanCurrentlyBlock => canCurrentlyBlock;
    public bool IsHoldingValidBlockDirection => isHoldingValidBlockDirection;
    public bool IsParryWindowActive => parryWindowFramesRemaining > 0;
    public bool ParryInputPressedThisTick => parryInputPressedThisTick;
    public int Health => health;
    public bool IsDefeated => isDefeated;
    public HitResultType LastReceivedHitResult => lastReceivedHitResult;
    public HitLevel LastReceivedHitLevel => lastReceivedHitLevel;
    public int LastReceivedStunFrames => lastReceivedStunFrames;
    public int LastReceivedChipDamage => lastReceivedChipDamage;
    public int TotalDamageTaken => totalDamageTaken;
    public int TotalChipDamageTaken => totalChipDamageTaken;
    public FighterRenderSnapshot RenderSnapshot => renderSnapshot;
    public FighterConfig Config => config;
    public string DebugName => debugName;

    private Vector2 position;
    private Vector2 velocity;
    private readonly FighterConfig config;
    private readonly string debugName;
    private readonly bool enableGlobalDamageOverride;
    private readonly int lightOverrideDamage;
    private readonly int mediumOverrideDamage;
    private readonly int heavyOverrideDamage;
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
    private int blockstunFramesRemaining;
    private bool isHoldingBlockInput;
    private bool canCurrentlyBlock;
    private bool isHoldingValidBlockDirection;
    private bool parryInputPressedThisTick;
    private int parryWindowFramesRemaining;
    private int parryLockoutFramesRemaining;
    private int lastMoveXSign;
    private int health;
    private int totalDamageTaken;
    private int totalChipDamageTaken;
    private HitResultType lastReceivedHitResult = HitResultType.None;
    private HitLevel lastReceivedHitLevel = HitLevel.Mid;
    private int lastReceivedStunFrames;
    private int lastReceivedChipDamage;
    private bool hadAttackInputThisTick;
    private int debugInputHistoryFreezeFramesRemaining;
    private string debugInputHistoryDisplay = "No input history yet";
    private bool hasPendingProjectileSpawn;
    private ProjectileSpawnRequest pendingProjectileSpawn;
    private int lightPressBufferFramesRemaining;
    private int mediumPressBufferFramesRemaining;
    private int heavyPressBufferFramesRemaining;
    private bool isDefeated;

    private FighterRenderSnapshot renderSnapshot;

    public Fighter(
        FighterConfig config,
        Vector2 startPosition,
        string debugName,
        int startHealth = 1000,
        bool enableGlobalDamageOverride = false,
        int lightOverrideDamage = 50,
        int mediumOverrideDamage = 100,
        int heavyOverrideDamage = 150
    )
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        this.config = config;
        this.debugName = string.IsNullOrWhiteSpace(debugName) ? "UnknownFighter" : debugName;
        this.enableGlobalDamageOverride = enableGlobalDamageOverride;
        this.lightOverrideDamage = Mathf.Max(0, lightOverrideDamage);
        this.mediumOverrideDamage = Mathf.Max(0, mediumOverrideDamage);
        this.heavyOverrideDamage = Mathf.Max(0, heavyOverrideDamage);
        position = startPosition;
        velocity = Vector2.zero;
        health = Mathf.Max(1, startHealth);
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
        parryInputPressedThisTick = false;
        if (isDefeated)
        {
            hadAttackInputThisTick = false;
            stateFrameFrozenThisTick = true;
            hitstopFramesRemaining = 0;
            hitstunFramesRemaining = 0;
            blockstunFramesRemaining = 0;
            velocity = Vector2.zero;
            isGrounded = true;
            EnterState(FighterState.Knockdown);
            EndTick();
            return;
        }

        hadAttackInputThisTick = input.punchLight || input.punchMedium || input.punchHeavy || input.dirt;
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
        else if (state == FighterState.Blockstun)
        {
            SimulateBlockstun();
        }
        else
        {
            bool startedBackdash = TryStartBackdash();
            if (startedBackdash)
            {
                // The transition frame locks actions; movement begins on the next tick.
            }
            else if (state == FighterState.Backdash || state == FighterState.BackdashRecovery)
            {
                HandleBackdash();
            }
            else
            {
                HandleJumpStartup(input);
                HandleMovement(input);
                HandleAttacks(input);
                UpdateAttackDataAttack(input);
            }

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
            if (entry.input.dirtPressed)
                builder.Append($" <color={DebugDirtColor}>D</color>");
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
            if (entry.input.dirtPressed)
                builder.Append($" <color={DebugDirtColor}>D</color>");
        }

        if (builder.Length == 0)
            builder.Append("No input history yet");

        return builder.ToString();
    }

    private void UpdateBlockingState(InputFrame input)
    {
        // "Holding block" and "can block" are tracked separately so visuals/gameplay
        // can reason about intent vs availability independently.
        int moveXSign = Mathf.RoundToInt(Mathf.Clamp(input.moveX, -1f, 1f));
        int forwardDirection = facingRight ? 1 : -1;
        parryInputPressedThisTick = moveXSign == forwardDirection && lastMoveXSign != forwardDirection;
        lastMoveXSign = moveXSign;

        if (parryLockoutFramesRemaining > 0)
            parryLockoutFramesRemaining--;

        int backDirection = facingRight ? -1 : 1;
        bool holdingBack = moveXSign == backDirection;
        isHoldingBlockInput = holdingBack;
        canCurrentlyBlock = FighterStateRules.CanHoldBlock(state, isGrounded);
        isHoldingValidBlockDirection = isHoldingBlockInput && canCurrentlyBlock;

        if (EnableParry && canCurrentlyBlock && parryInputPressedThisTick && parryLockoutFramesRemaining <= 0)
        {
            parryWindowFramesRemaining = Mathf.Max(1, ParryActiveWindowFrames);
        }
        else if (parryWindowFramesRemaining > 0)
        {
            parryWindowFramesRemaining--;
            if (parryWindowFramesRemaining == 0 && EnableParry)
                parryLockoutFramesRemaining = Mathf.Max(parryLockoutFramesRemaining, ParryWhiffLockoutFrames);
        }
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

    private void SimulateBlockstun()
    {
        if (blockstunFramesRemaining > 0)
            blockstunFramesRemaining--;

        movementController.ApplyGravity(isGrounded, ref velocity, config);
        movementController.Integrate(ref position, velocity);
        ResolveGroundContact();

        if (blockstunFramesRemaining <= 0)
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
        bool hasAttackInput = input.punchLight || input.punchMedium || input.punchHeavy || input.dirt;

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

    private bool TryStartBackdash()
    {
        if (!isGrounded || state != FighterState.NeutralGround)
            return false;

        if (!HasBackdashInput())
            return false;

        EnterState(FighterState.Backdash);
        velocity.x = 0f;
        return true;
    }

    private bool HasBackdashInput()
    {
        return ContainsHeldDirectionNeutralDirectionInput(4, config.backdashInputWindowFrames);
    }

    private void HandleBackdash()
    {
        if (state == FighterState.Backdash)
        {
            int durationFrames = Mathf.Max(1, config.backdashDurationFrames);
            if (stateFrame >= durationFrames)
            {
                velocity.x = 0f;
                EnterState(FighterState.BackdashRecovery);
                return;
            }

            int backDirection = facingRight ? -1 : 1;
            velocity.x = backDirection * config.moveSpeed * Mathf.Max(0f, config.backdashSpeedMultiplier);
            return;
        }

        if (state == FighterState.BackdashRecovery)
        {
            velocity.x = 0f;
            int recoveryFrames = Mathf.Max(0, config.backdashRecoveryFrames);
            if (stateFrame >= recoveryFrames)
                EnterState(FighterState.NeutralGround);
        }
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
        {
            TryMorphLightNormalIntoThrow(input);
            return;
        }

        if (!CanStartAttack())
            return;

        if (TryResolveThrowInput(input))
        {
            StartAttack(MoveType.Throw);
            return;
        }

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
        const int dragonPunchWindow = 20;
        const int quarterCircleForwardWindow = 20;
        moveType = MoveType.None;

        if (!isGrounded || !input.HasPunchPress)
            return false;

        if (TryResolveDownDownCharge(input, out moveType))
        {
            if (LogSpecialInputReads)
                Debug.Log($"[SpecialInput] DownDownCharge read: {moveType}");

            return true;
        }

        if (ContainsDragonPunchForward(dragonPunchWindow))
        {
            moveType = ResolveDragonPunchMoveType(input);
            if (moveType != MoveType.None && LogSpecialInputReads)
                Debug.Log($"[SpecialInput] Dragon Punch read: {moveType}");

            if (moveType != MoveType.None)
                return true;
        }

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

    private bool TryResolveDownDownCharge(InputFrame input, out MoveType moveType)
    {
        moveType = MoveType.None;

        if (!config.enableDownDownCharge || !input.HasPunchPress)
            return false;

        if (!ContainsDownDownChargeInput(config.backdashInputWindowFrames, 4))
            return false;

        moveType = MoveType.DownDownCharge;
        return true;
    }

    private MoveType ResolveDragonPunchMoveType(InputFrame input)
    {
        if (input.punchLightPressed)
            return MoveType.DragonPunchLight;
        if (input.punchMediumPressed)
            return MoveType.DragonPunchMedium;
        if (input.punchHeavyPressed)
            return MoveType.DragonPunchHeavy;

        return MoveType.None;
    }

    private bool ContainsDragonPunchForward(int maxFrames)
    {
        // Search backward from the attack-press frame and require
        // 6 <- 3 <- 2 <- 6 (equivalent to 6236 in forward time).
        return ContainsMotionSequence(maxFrames, 6, 3, 2, 6);
    }

    private bool ContainsHeldDirectionNeutralDirectionInput(int direction, int maxHoldFrames)
    {
        int maxFrames = Mathf.Max(1, maxHoldFrames);
        if (inputHistory.Count < 3)
            return false;

        if (inputHistory.GetRecent(0).relativeDirection != direction)
            return false;

        int cursor = 1;
        if (cursor >= inputHistory.Count || inputHistory.GetRecent(cursor).relativeDirection != 5)
            return false;

        int neutralFrames = 0;
        while (cursor < inputHistory.Count && inputHistory.GetRecent(cursor).relativeDirection == 5)
        {
            neutralFrames++;
            if (neutralFrames > maxFrames)
                return false;

            cursor++;
        }

        if (cursor >= inputHistory.Count || inputHistory.GetRecent(cursor).relativeDirection != direction)
            return false;

        int firstDirectionFrames = 0;
        while (cursor < inputHistory.Count && inputHistory.GetRecent(cursor).relativeDirection == direction)
        {
            firstDirectionFrames++;
            if (firstDirectionFrames > maxFrames)
                return false;

            cursor++;
        }

        return true;
    }

    private bool ContainsDownDownChargeInput(int maxHoldFrames, int punchBufferFrames)
    {
        int punchSearchCount = Mathf.Min(inputHistory.Count - 1, Mathf.Max(0, punchBufferFrames));
        for (int secondDownFramesAgo = 0; secondDownFramesAgo <= punchSearchCount; secondDownFramesAgo++)
        {
            if (TryMatchDownDownChargeSequence(secondDownFramesAgo, maxHoldFrames))
                return true;
        }

        return false;
    }

    private bool TryMatchDownDownChargeSequence(int secondDownFramesAgo, int maxHoldFrames)
    {
        int maxFrames = Mathf.Max(1, maxHoldFrames);
        if (inputHistory.GetRecent(secondDownFramesAgo).relativeDirection != 2)
            return false;

        int cursor = secondDownFramesAgo + 1;
        if (cursor >= inputHistory.Count || inputHistory.GetRecent(cursor).relativeDirection != 5)
            return false;

        int neutralFrames = 0;
        while (cursor < inputHistory.Count && inputHistory.GetRecent(cursor).relativeDirection == 5)
        {
            neutralFrames++;
            if (neutralFrames > maxFrames)
                return false;

            cursor++;
        }

        if (cursor >= inputHistory.Count || inputHistory.GetRecent(cursor).relativeDirection != 2)
            return false;

        int firstDownFrames = 0;
        while (cursor < inputHistory.Count && inputHistory.GetRecent(cursor).relativeDirection == 2)
        {
            firstDownFrames++;
            if (firstDownFrames > maxFrames)
                return false;

            cursor++;
        }

        return true;
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

    private bool ContainsMotionSequence(int maxFrames, params int[] sequence)
    {
        if (sequence == null || sequence.Length == 0)
            return false;

        int searchCount = inputHistory.Count < maxFrames ? inputHistory.Count : maxFrames;
        int requiredStep = 0;

        // We allow extra directions between required steps, but the required
        // sequence must appear in order when scanning backward from press.
        for (int framesAgo = 0; framesAgo < searchCount; framesAgo++)
        {
            int direction = inputHistory.GetRecent(framesAgo).relativeDirection;
            if (direction != sequence[requiredStep])
                continue;

            requiredStep++;
            if (requiredStep >= sequence.Length)
                return true;
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

    private bool TryResolveThrowInput(InputFrame input)
    {
        if (!isGrounded)
            return false;

        if (!input.punchLightPressed && !input.dirtPressed)
            return false;

        return HasRecentButtonChord(
            entry => entry.input.punchLightPressed,
            entry => entry.input.dirtPressed,
            ThrowInputWindowFrames
        );
    }

    private void TryMorphLightNormalIntoThrow(InputFrame input)
    {
        if (!input.dirtPressed)
            return;

        if (!isGrounded)
            return;

        MoveType currentMoveType = attackController.CurrentMoveType;
        if (currentMoveType != MoveType.StandingLight && currentMoveType != MoveType.CrouchingLight)
            return;

        if (!HasRecentButtonChord(
            entry => entry.input.punchLightPressed,
            entry => entry.input.dirtPressed,
            ThrowInputWindowFrames
        ))
            return;

        StartAttack(MoveType.Throw);
    }

    private bool HasRecentButtonChord(
        Func<InputHistoryBuffer.HistoryEntry, bool> firstButtonPressed,
        Func<InputHistoryBuffer.HistoryEntry, bool> secondButtonPressed,
        int maxFrameGap
    )
    {
        int maxFramesAgo = Mathf.Min(inputHistory.Count - 1, Mathf.Max(0, maxFrameGap));
        int firstFramesAgo = -1;
        int secondFramesAgo = -1;

        for (int framesAgo = 0; framesAgo <= maxFramesAgo; framesAgo++)
        {
            InputHistoryBuffer.HistoryEntry entry = inputHistory.GetRecent(framesAgo);
            if (firstFramesAgo < 0 && firstButtonPressed(entry))
                firstFramesAgo = framesAgo;
            if (secondFramesAgo < 0 && secondButtonPressed(entry))
                secondFramesAgo = framesAgo;

            if (firstFramesAgo >= 0 && secondFramesAgo >= 0)
                return Mathf.Abs(firstFramesAgo - secondFramesAgo) <= maxFrameGap;
        }

        return false;
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
        return attackController.ResolveAttackData(
            moveType,
            config,
            facingRight,
            enableGlobalDamageOverride,
            lightOverrideDamage,
            mediumOverrideDamage,
            heavyOverrideDamage
        );
    }

    public AttackData ResolveAttackDataForNetState(MoveType moveType, bool forFacingRight)
    {
        return attackController.ResolveAttackData(
            moveType,
            config,
            forFacingRight,
            enableGlobalDamageOverride,
            lightOverrideDamage,
            mediumOverrideDamage,
            heavyOverrideDamage
        );
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
            damage: ResolveDamageForMoveType(moveType, config.fireballProjectileDamage),
            hitstunFrames: config.fireballProjectileHitstunFrames,
            blockstunFrames: Mathf.Max(1, config.fireballProjectileHitstunFrames - 2),
            blockPushback: 0.6f,
            chipDamage: 1,
            attackerBlockstopFrames: -1,
            hitLevel: HitLevel.Mid
        );
        hasPendingProjectileSpawn = true;
    }

    private int ResolveDamageForMoveType(MoveType moveType, int fallbackDamage)
    {
        if (!enableGlobalDamageOverride)
            return Mathf.Max(0, fallbackDamage);

        switch (moveType)
        {
            case MoveType.StandingLight:
            case MoveType.CrouchingLight:
            case MoveType.JumpingLight:
            case MoveType.FireballLight:
            case MoveType.DragonPunchLight:
                return lightOverrideDamage;
            case MoveType.StandingMedium:
            case MoveType.CrouchingMedium:
            case MoveType.JumpingMedium:
            case MoveType.FireballMedium:
            case MoveType.DragonPunchMedium:
                return mediumOverrideDamage;
            case MoveType.StandingHeavy:
            case MoveType.CrouchingHeavy:
            case MoveType.JumpingHeavy:
            case MoveType.FireballHeavy:
            case MoveType.DragonPunchHeavy:
            case MoveType.DownDownCharge:
                return heavyOverrideDamage;
            case MoveType.Throw:
                return 80;
            default:
                return Mathf.Max(0, fallbackDamage);
        }
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

    public bool IsInAttackStartup()
    {
        return state == FighterState.AttackStartup;
    }

    public int GetEstimatedRemainingRecoveryFrames()
    {
        return attackController.GetRemainingRecoveryFrames();
    }

    public void ApplyHit(Hitbox hit, int bonusHitstunFrames, HitResultType resultType)
    {
        if (isDefeated)
            return;

        ApplyHitstop(HitstopFrames);
        hitstunFramesRemaining = Mathf.Max(1, hit.hitstunFrames + Mathf.Max(0, bonusHitstunFrames));
        blockstunFramesRemaining = 0;
        attackController.EndAttack();
        if (state == FighterState.Backdash || state == FighterState.BackdashRecovery)
            velocity.x = 0f;
        int damageApplied = Mathf.Max(0, hit.damage);
        health = Mathf.Max(0, health - damageApplied);
        totalDamageTaken += damageApplied;
        lastReceivedHitResult = resultType;
        lastReceivedHitLevel = hit.hitLevel;
        lastReceivedStunFrames = hitstunFramesRemaining;
        lastReceivedChipDamage = 0;
        if (health <= 0)
        {
            MarkDefeated();
            return;
        }

        EnterState(FighterState.Hitstun);
    }

    public void ApplyBlockedHit(Hitbox hit, bool applyChipDamage, HitResultType resultType)
    {
        if (isDefeated)
            return;

        ApplyHitstop(HitstopFrames);
        blockstunFramesRemaining = Mathf.Max(1, hit.blockstunFrames);
        hitstunFramesRemaining = 0;
        attackController.EndAttack();
        int chipDamage = applyChipDamage ? Mathf.Max(0, hit.chipDamage) : 0;
        health = Mathf.Max(0, health - chipDamage);
        totalDamageTaken += chipDamage;
        totalChipDamageTaken += chipDamage;
        lastReceivedHitResult = resultType;
        lastReceivedHitLevel = hit.hitLevel;
        lastReceivedStunFrames = blockstunFramesRemaining;
        lastReceivedChipDamage = chipDamage;
        if (health <= 0)
        {
            MarkDefeated();
            return;
        }

        EnterState(FighterState.Blockstun);
    }

    public void ApplyParry(int defenderHitstopFrames, int lockoutFrames)
    {
        if (isDefeated)
            return;

        ApplyHitstop(defenderHitstopFrames);
        hitstunFramesRemaining = 0;
        blockstunFramesRemaining = 0;
        parryWindowFramesRemaining = 0;
        parryLockoutFramesRemaining = Mathf.Max(parryLockoutFramesRemaining, lockoutFrames);
        lastReceivedHitResult = HitResultType.Parry;
        lastReceivedStunFrames = 0;
        lastReceivedChipDamage = 0;
    }

    public void ApplySuccessfulHitstopAsAttacker(int frames = -1)
    {
        ApplyHitstop(frames >= 0 ? frames : HitstopFrames);
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
            Debug.Log(
                $"[CrouchState] {debugName} {previousState} -> {nextState} " +
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

    public bool CanPerformParryNow()
    {
        return !isDefeated && EnableParry && parryWindowFramesRemaining > 0;
    }

    public bool IsCrouchingGuard()
    {
        return isGrounded && state == FighterState.Crouching;
    }

    public Snapshot CaptureSnapshot()
    {
        return new Snapshot(
            position,
            velocity,
            isGrounded,
            facingRight,
            state,
            stateFrame,
            transitionedThisTick,
            stateFrameFrozenThisTick,
            hitstopFramesRemaining,
            hitstunFramesRemaining,
            blockstunFramesRemaining,
            isHoldingBlockInput,
            canCurrentlyBlock,
            isHoldingValidBlockDirection,
            parryInputPressedThisTick,
            lastMoveXSign,
            parryWindowFramesRemaining,
            parryLockoutFramesRemaining,
            health,
            totalDamageTaken,
            totalChipDamageTaken,
            lastReceivedHitResult,
            lastReceivedHitLevel,
            lastReceivedStunFrames,
            lastReceivedChipDamage,
            hadAttackInputThisTick,
            debugInputHistoryFreezeFramesRemaining,
            debugInputHistoryDisplay,
            hasPendingProjectileSpawn,
            pendingProjectileSpawn,
            lightPressBufferFramesRemaining,
            mediumPressBufferFramesRemaining,
            heavyPressBufferFramesRemaining,
            isDefeated,
            renderSnapshot,
            attackController.CaptureSnapshot(),
            movementController.CaptureSnapshot(),
            renderStateBuilder.CaptureSnapshot(),
            inputHistory.CaptureSnapshot()
        );
    }

    public void RestoreSnapshot(Snapshot snapshot)
    {
        position = snapshot.position;
        velocity = snapshot.velocity;
        isGrounded = snapshot.isGrounded;
        facingRight = snapshot.facingRight;
        state = snapshot.state;
        stateFrame = snapshot.stateFrame;
        transitionedThisTick = snapshot.transitionedThisTick;
        stateFrameFrozenThisTick = snapshot.stateFrameFrozenThisTick;
        hitstopFramesRemaining = snapshot.hitstopFramesRemaining;
        hitstunFramesRemaining = snapshot.hitstunFramesRemaining;
        blockstunFramesRemaining = snapshot.blockstunFramesRemaining;
        isHoldingBlockInput = snapshot.isHoldingBlockInput;
        canCurrentlyBlock = snapshot.canCurrentlyBlock;
        isHoldingValidBlockDirection = snapshot.isHoldingValidBlockDirection;
        parryInputPressedThisTick = snapshot.parryInputPressedThisTick;
        lastMoveXSign = snapshot.lastMoveXSign;
        parryWindowFramesRemaining = snapshot.parryWindowFramesRemaining;
        parryLockoutFramesRemaining = snapshot.parryLockoutFramesRemaining;
        health = snapshot.health;
        totalDamageTaken = snapshot.totalDamageTaken;
        totalChipDamageTaken = snapshot.totalChipDamageTaken;
        lastReceivedHitResult = snapshot.lastReceivedHitResult;
        lastReceivedHitLevel = snapshot.lastReceivedHitLevel;
        lastReceivedStunFrames = snapshot.lastReceivedStunFrames;
        lastReceivedChipDamage = snapshot.lastReceivedChipDamage;
        hadAttackInputThisTick = snapshot.hadAttackInputThisTick;
        debugInputHistoryFreezeFramesRemaining = snapshot.debugInputHistoryFreezeFramesRemaining;
        debugInputHistoryDisplay = snapshot.debugInputHistoryDisplay ?? "No input history yet";
        hasPendingProjectileSpawn = snapshot.hasPendingProjectileSpawn;
        pendingProjectileSpawn = snapshot.pendingProjectileSpawn;
        lightPressBufferFramesRemaining = snapshot.lightPressBufferFramesRemaining;
        mediumPressBufferFramesRemaining = snapshot.mediumPressBufferFramesRemaining;
        heavyPressBufferFramesRemaining = snapshot.heavyPressBufferFramesRemaining;
        isDefeated = snapshot.isDefeated;
        renderSnapshot = snapshot.renderSnapshot;
        attackController.RestoreSnapshot(snapshot.attackSnapshot);
        movementController.RestoreSnapshot(snapshot.movementSnapshot);
        renderStateBuilder.RestoreSnapshot(snapshot.renderStateBuilderSnapshot);
        inputHistory.RestoreSnapshot(snapshot.inputHistorySnapshot);
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

    private void MarkDefeated()
    {
        isDefeated = true;
        health = 0;
        hitstopFramesRemaining = 0;
        hitstunFramesRemaining = 0;
        blockstunFramesRemaining = 0;
        hasPendingProjectileSpawn = false;
        velocity = Vector2.zero;
        movementController.ClearLandingRecovery();
        attackController.EndAttack();
        EnterState(FighterState.Knockdown);
    }

}
