using UnityEngine;

// Fighter.cs drives the simulation state for a single fighter.
// The class encapsulates the simulation work done for a single fighter character.
public class Fighter
{
    // --- Public Simulation State (Read-Only for View) ---

    // Public Positional and movement data
    public Vector2 Position => position;
    public Vector2 Velocity => velocity;
    public bool FacingRight => facingRight;
    public float PushboxHalfWidth => config.pushboxHalfWidth;
    public bool IsGrounded => isGrounded;
    public bool IsInPrejump => IsInJumpStartup;
    public int PrejumpFramesRemaining => jumpStartupTicksRemaining;
    public bool JustBecameAirborne { get; private set; }
    public float VerticalSpeed => velocity.y;

    // Public Attack state properties
    public bool IsAttacking =>
        controlState == FighterControlState.AttackStartup
        || controlState == FighterControlState.AttackRecovery;
    public AttackType CurrentAttack => currentAttack;
    public FighterControlState CurrentControlState => controlState;

    // Tracks whether this attack was just triggered
    public bool AttackTriggered { get; private set; }
    // One-tick pulse when a grounded jump input is accepted.
    public bool JustJumped { get; private set; }

    // Future lockdown states mapped to action state for compatibility.
    public bool IsInHitstun => controlState == FighterControlState.Hitstun;
    public bool IsInBlockstun => controlState == FighterControlState.Blockstun;
    public bool IsKnockdown => controlState == FighterControlState.Knockdown;

    // Computed property
    public bool IsActionable => controlState == FighterControlState.Neutral && !IsInJumpStartup;

    public float ForwardMoveSpeed
    {
        get
        {
            // If facing right, forward is +X
            // If facing left, forward is -X
            return facingRight ? velocity.x : -velocity.x;
        }
    }

    // --- Private Simulation Data ---
    private Vector2 position;
    private Vector2 velocity;

    private FighterConfig config;
    private FighterView view;
    private int playerIndex;
    private Fighter opponent; // Reference to opponent for facing direction

    private bool isGrounded = true;
    // Action state remains explicit; locomotion substates are derived from kinematics.
    private FighterControlState controlState = FighterControlState.Neutral;
    private AttackType currentAttack = AttackType.None;

    private float attackTimer;
    private bool facingRight = true;
    private const int TICKS_PER_SECOND = 60;
    private const int JUMP_STARTUP_TICKS = 4;
    private int jumpStartupTicksRemaining;
    private int queuedJumpMoveX;

    public Fighter(int playerIndex, FighterView view, Vector2 startPosition)
    {
        this.playerIndex = playerIndex;
        this.view = view;
        this.config = view.Config;

        this.position = startPosition;
        this.velocity = Vector2.zero;
    }

    public void Tick(InputFrame input)
    {
        AttackTriggered = false;
        JustJumped = false;
        JustBecameAirborne = false;
        // Re-evaluate grounded state at tick start from current kinematics.
        RefreshGroundedState();
        bool wasGroundedAtTickStart = isGrounded;
        HandleJumpStartup(input);

        // Simulation step order: input -> attacks -> physics -> contacts -> facing.
        HandleMovement(input);
        HandleAttacks(input);
        ApplyGravity();
        Integrate(); // Update position based on velocity
        ResolveGroundContact(); // Prevent going below ground level and emit transitions
        if (wasGroundedAtTickStart && !isGrounded)
            JustBecameAirborne = true;
        UpdateFacing(opponent); // Update facing direction based on opponent position
    }

    private void RefreshGroundedState()
    {
        if (position.y <= 0f && velocity.y <= 0f)
            isGrounded = true;
        else
            isGrounded = false;
    }

    private void HandleMovement(InputFrame input)
    {
        if (IsInJumpStartup)
        {
            velocity.x = 0f;
            return;
        }

        // Grounded attacks lock horizontal movement by default.
        if (
            (
                controlState == FighterControlState.AttackStartup
                || controlState == FighterControlState.AttackRecovery
            ) && isGrounded
        )
        {
            velocity.x = 0f;
            return;
        }

        if (isGrounded)
        {
            if (input.moveX == -1)
                velocity.x = -config.moveSpeed;
            else if (input.moveX == 1)
                velocity.x = config.moveSpeed;
            else
                ApplyFriction();

            if (input.moveY == 1 && IsActionable)
            {
                StartJumpStartup(input.moveX);
            }
        }
    }

    private bool IsInJumpStartup => jumpStartupTicksRemaining > 0;

    private void StartJumpStartup(float moveX)
    {
        if (!isGrounded || IsInJumpStartup)
            return;

        // Only keep -1/0/1 directions for launch decision.
        queuedJumpMoveX = Mathf.RoundToInt(Mathf.Clamp(moveX, -1f, 1f));
        jumpStartupTicksRemaining = JUMP_STARTUP_TICKS;
        JustJumped = true;
    }

    private void HandleJumpStartup(InputFrame input)
    {
        if (!IsInJumpStartup)
            return;

        int moveX = Mathf.RoundToInt(Mathf.Clamp(input.moveX, -1f, 1f));
        // Neutral jump can be redirected to diagonal during pre-jump.
        // Diagonal jump cannot be redirected back to neutral.
        if (queuedJumpMoveX == 0 && moveX != 0)
            queuedJumpMoveX = moveX;

        jumpStartupTicksRemaining--;
        if (jumpStartupTicksRemaining > 0)
            return;

        if (queuedJumpMoveX == -1)
            velocity.x = -config.moveSpeed * config.jumpHorizontalBoostScale;
        else if (queuedJumpMoveX == 1)
            velocity.x = config.moveSpeed * config.jumpHorizontalBoostScale;

        // Jump launches once startup frames complete.
        velocity.y = config.jumpForce;
        isGrounded = false;
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
        // Continue current attack until its timer expires.
        if (controlState == FighterControlState.AttackStartup)
        {
            attackTimer -= GameLoop.FIXED_DT;

            if (attackTimer <= 0f)
            {
                controlState = FighterControlState.AttackRecovery;
                attackTimer = GetAttackRecoveryDurationSeconds(currentAttack);
            }
            return;
        }

        if (controlState == FighterControlState.AttackRecovery)
        {
            attackTimer -= GameLoop.FIXED_DT;

            if (attackTimer <= 0f)
            {
                controlState = FighterControlState.Neutral;
                currentAttack = AttackType.None;
            }
            return;
        }

        // No grounded attack starts while airborne or during non-actionable states.
        if (!isGrounded || !IsActionable)
            return;

        // Priority order if multiple attack inputs are true in one frame.
        if (input.punchLight)
            StartAttack(AttackType.Light);
        else if (input.punchMedium)
            StartAttack(AttackType.Medium);
        else if (input.punchHeavy)
            StartAttack(AttackType.Heavy);
    }

    private void StartAttack(AttackType type)
    {
        controlState = FighterControlState.AttackStartup;
        currentAttack = type;

        // Startup then recovery are modeled separately for future combat rules.
        attackTimer = GetAttackStartupDurationSeconds(type);

        // One-frame pulse consumed by the view/animator layer.
        AttackTriggered = true;
    }

    private float GetAttackStartupDurationSeconds(AttackType type)
    {
        int startupFrames = 0;
        int animationSampleRate = 24; // FPS of your animation clip

        switch (type)
        {
            case AttackType.Light:
                startupFrames = 2;
                break;
            case AttackType.Medium:
                startupFrames = 4;
                break;
            case AttackType.Heavy:
                startupFrames = 6;
                break;
            default:
                return 0f;
        }

        return startupFrames / (float)animationSampleRate;
    }

    private float GetAttackRecoveryDurationSeconds(AttackType type)
    {
        int recoveryFrames = 0;
        int animationSampleRate = 24; // FPS of your animation clip

        switch (type)
        {
            case AttackType.Light:
                recoveryFrames = 4;
                break;
            case AttackType.Medium:
                recoveryFrames = 6;
                break;
            case AttackType.Heavy:
                recoveryFrames = 8;
                break;
            default:
                return 0f;
        }

        return recoveryFrames / (float)animationSampleRate;
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
        if (position.y <= 0f)
        {
            position.y = 0f;
            velocity.y = 0f;

            isGrounded = true;
        }
        else
            isGrounded = false;
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

    // Moves the fighter horizontally by a delta
    public void MoveHorizontal(float deltaX)
    {
        position.x += deltaX;
    }

    // Sets the fighter's horizontal position directly
    public void SetHorizontal(float newX)
    {
        position.x = newX;
    }
}

public enum FighterControlState
{
    Neutral,
    AttackStartup,
    AttackRecovery,
    Hitstun,
    Blockstun,
    Knockdown,
}

public enum AttackType
{
    None = 0,
    Light = 1,
    Medium = 2,
    Heavy = 3,
}
