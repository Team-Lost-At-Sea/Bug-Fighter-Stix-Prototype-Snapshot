using UnityEngine;

public class Fighter
{
    // --- Public Simulation State (Read-Only for View) ---

    // Public Positional and movement data
    public Vector2 Position => position;
    public Vector2 Velocity => velocity;
    public bool FacingRight => facingRight;
    public float PushboxHalfWidth => config.pushboxHalfWidth;
    public bool IsJumping => position.y > 0f;

    // Public Attack state properties
    public bool IsAttacking => state == FighterState.Attacking;
    public AttackType CurrentAttack => currentAttack;

    // Tracks whether this attack was just triggered
    public bool AttackTriggered { get; private set; }

    // Future Lockdown states (not implemented yet)
    public bool IsInHitstun = false;
    public bool IsInBlockstun = false;
    public bool IsKnockdown = false;

    // Computed property
    public bool IsActionable => !IsAttacking && !IsInHitstun && !IsInBlockstun && !IsKnockdown;

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

    private FighterState state = FighterState.Idle;
    private AttackType currentAttack = AttackType.None;

    private float attackTimer;
    private bool facingRight = true;
    private const int TICKS_PER_SECOND = 60;

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
        HandleMovement(input);
        HandleAttacks(input);
        ApplyGravity();
        Integrate(); // Update position based on velocity
        ClampToGround(); // Prevent going below ground level
        UpdateFacing(opponent); // Update facing direction based on opponent position
    }

    private void HandleMovement(InputFrame input)
    {
        if (state == FighterState.Attacking)
        {
            velocity.x = 0f;
            return;
        }

        if (position.y == 0f)
        {
            if (input.moveX == -1)
                velocity.x = -config.moveSpeed;
            else if (input.moveX == 1)
                velocity.x = config.moveSpeed;
            else
                ApplyFriction();

            if (input.moveY == 1)
            {
                if (input.moveX == -1)
                    velocity.x = -config.moveSpeed * config.jumpHorizontalBoostScale;
                else if (input.moveX == 1)
                    velocity.x = config.moveSpeed * config.jumpHorizontalBoostScale;

                velocity.y = config.jumpForce;
            }
        }
    }

    private void HandleAttacks(InputFrame input)
    {
        // --- Handle ongoing attack ---
        if (state == FighterState.Attacking)
        {
            // Reduce timer in seconds per tick
            attackTimer -= GameLoop.FIXED_DT;

            // --- DEBUG LINE START ---
            var info = view.Animator.GetCurrentAnimatorStateInfo(0);
            Debug.Log(
                $"AttackTimer: {attackTimer:F2}s, State: {state}, CurrentAttack: {currentAttack}, AnimatorState: {info.shortNameHash}"
            );
            // --- DEBUG LINE END ---

            // Attack finished
            if (attackTimer <= 0f)
            {
                state = FighterState.Idle;
                currentAttack = AttackType.None;
            }

            // Once the attack has started, we can reset the trigger so Animator won't retrigger
            AttackTriggered = false;

            // Keep currentAttack set until timer hits 0
            return;
        }

        // --- Start a new attack if any button pressed ---
        if (input.punchLight)
            StartAttack(AttackType.Light);
        else if (input.punchMedium)
            StartAttack(AttackType.Medium);
        else if (input.punchHeavy)
            StartAttack(AttackType.Heavy);
    }

    private void StartAttack(AttackType type)
    {
        state = FighterState.Attacking;
        currentAttack = type;

        // Convert animation frames to seconds
        attackTimer = GetAttackDurationSeconds(type);

        // Set the triggered flag for the Animator
        AttackTriggered = true;
    }

    private float GetAttackDurationSeconds(AttackType type)
    {
        int animationFrames = 0;
        int animationSampleRate = 24; // FPS of your animation clip

        switch (type)
        {
            case AttackType.Light:
                animationFrames = 6;
                break;
            case AttackType.Medium:
                animationFrames = 10;
                break;
            case AttackType.Heavy:
                animationFrames = 14;
                break;
            default:
                return 0f;
        }

        // Duration in seconds = frames / clip FPS
        return animationFrames / (float)animationSampleRate;
    }

    private void ApplyGravity()
    {
        velocity.y += config.gravity * GameLoop.FIXED_DT;

        if (velocity.y < config.maxFallSpeed)
            velocity.y = config.maxFallSpeed;
    }

    private void Integrate()
    {
        position += velocity * GameLoop.FIXED_DT;
    }

    private void ClampToGround()
    {
        if (position.y < 0f)
        {
            position.y = 0f;
            velocity.y = 0f;
        }
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

    public void UpdateFacing(Fighter opponent)
    {
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

public enum FighterState
{
    Idle,
    Attacking,
}

public enum AttackType
{
    None = 0,
    Light = 1,
    Medium = 2,
    Heavy = 3,
}
