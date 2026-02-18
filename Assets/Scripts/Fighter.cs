using UnityEngine;

public class Fighter
{
    public Vector2 position;
    public Vector2 velocity;

    private FighterConfig config;
    private FighterView view;
    private int playerIndex;

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

        // Grounded movement
        if (position.y == 0f)
        {
            // Horizontal
            if (input.left)
                velocity.x = -config.moveSpeed;
            else if (input.right)
                velocity.x = config.moveSpeed;
            else
                ApplyFriction();

            // Jump
            if (input.jump)
            {
                if (input.left)
                    velocity.x = -config.moveSpeed * config.jumpHorizontalBoostScale;
                else if (input.right)
                    velocity.x = config.moveSpeed * config.jumpHorizontalBoostScale;

                velocity.y = config.jumpForce;
            }
        }

        // Gravity
        velocity.y += config.gravity * GameLoop.FIXED_DT;

        if (velocity.y < config.maxFallSpeed)
            velocity.y = config.maxFallSpeed;

        // Integrate
        position += velocity * GameLoop.FIXED_DT;

        // Ground clamp
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

    public void Render()
    {
        view.SetPosition(position);
    }
}
