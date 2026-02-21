using UnityEngine;

public class Fighter
{
    // --- Public Simulation State (Read-Only for View) ---
    public Vector2 Position => position;
    public Vector2 Velocity => velocity;
    public bool IsAttacking => state == FighterState.Attacking;
    public AttackType CurrentAttack => currentAttack;
    public bool FacingRight => facingRight;

    public float PushboxHalfWidth => config.pushboxHalfWidth;
    public bool IsJumping => position.y > 0f;

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

    private int attackTimer;
    private bool facingRight = true;

    public Fighter(int playerIndex, FighterView view, Vector2 startPosition)
    {
        this.playerIndex = playerIndex;
        this.view = view;
        this.config = view.Config;

        this.position = startPosition;
        this.velocity = Vector2.zero;
    }

    public void Tick()
    {
        InputFrame input = GameInput.Instance.GetInputForPlayer(playerIndex);

        HandleMovement(input);
        HandleAttacks(input);
        ApplyGravity();
        Integrate();
        ClampToGround();
        UpdateFacing(opponent);
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
            if (input.left)
                velocity.x = -config.moveSpeed;
            else if (input.right)
                velocity.x = config.moveSpeed;
            else
                ApplyFriction();

            if (input.jump)
            {
                if (input.left)
                    velocity.x = -config.moveSpeed * config.jumpHorizontalBoostScale;
                else if (input.right)
                    velocity.x = config.moveSpeed * config.jumpHorizontalBoostScale;

                velocity.y = config.jumpForce;
            }
        }
    }

    private void HandleAttacks(InputFrame input)
    {
        if (state == FighterState.Attacking)
        {
            attackTimer--;

            if (attackTimer <= 0)
            {
                state = FighterState.Idle;
                currentAttack = AttackType.None;
            }

            return;
        }

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
        attackTimer = GetAttackDuration(type);
    }

    private int GetAttackDuration(AttackType type)
    {
        switch (type)
        {
            case AttackType.Light:
                return 18;
            case AttackType.Medium:
                return 24;
            case AttackType.Heavy:
                return 32;
            default:
                return 0;
        }
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
