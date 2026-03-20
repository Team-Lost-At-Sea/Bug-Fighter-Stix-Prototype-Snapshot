using UnityEngine;

public sealed class FighterMovementController
{
    private const int JUMP_STARTUP_TICKS = 4;
    private const int LANDING_RECOVERY_TICKS = 3;

    private int landingRecoveryTicksRemaining;
    private int queuedJumpMoveX;
    private bool usedAirNormalThisJump;

    public void StartJumpStartup(float moveX)
    {
        queuedJumpMoveX = Mathf.RoundToInt(Mathf.Clamp(moveX, -1f, 1f));
        usedAirNormalThisJump = false;
    }

    public bool HandleJumpStartup(
        FighterState state,
        int stateFrame,
        InputFrame input,
        FighterConfig config,
        ref Vector2 velocity,
        ref bool isGrounded
    )
    {
        if (state != FighterState.JumpStartup)
            return false;

        int moveX = Mathf.RoundToInt(Mathf.Clamp(input.moveX, -1f, 1f));
        if (queuedJumpMoveX == 0 && moveX != 0)
            queuedJumpMoveX = moveX;

        // Drive jump startup timing from fighter state frames to avoid countdown
        // desync that can leave fighters stuck in JumpStartup.
        if (stateFrame < JUMP_STARTUP_TICKS - 1)
            return false;

        if (queuedJumpMoveX == -1)
            velocity.x = -config.moveSpeed * config.jumpHorizontalBoostScale;
        else if (queuedJumpMoveX == 1)
            velocity.x = config.moveSpeed * config.jumpHorizontalBoostScale;

        velocity.y = config.jumpForce;
        isGrounded = false;
        return true;
    }

    public void MarkAirNormalUsed()
    {
        usedAirNormalThisJump = true;
    }

    public void StartLandingRecovery()
    {
        landingRecoveryTicksRemaining = LANDING_RECOVERY_TICKS;
    }

    public void ClearLandingRecovery()
    {
        landingRecoveryTicksRemaining = 0;
    }

    public bool AdvanceLandingRecovery(FighterState state)
    {
        if (state != FighterState.LandingRecovery)
            return false;

        if (landingRecoveryTicksRemaining > 0)
            landingRecoveryTicksRemaining--;

        return landingRecoveryTicksRemaining <= 0;
    }

    public bool CanCancelLandingRecoveryIntoAttack()
    {
        if (usedAirNormalThisJump)
            return false;

        int currentLandingFrame = LANDING_RECOVERY_TICKS - landingRecoveryTicksRemaining + 1;
        return currentLandingFrame == 2 || currentLandingFrame == 3;
    }

    public void ApplyFriction(ref Vector2 velocity, FighterConfig config)
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

    public void ApplyGravity(bool isGrounded, ref Vector2 velocity, FighterConfig config)
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

    public void Integrate(ref Vector2 position, Vector2 velocity)
    {
        position += velocity * GameLoop.FIXED_DT;
    }

    public GroundContactResult ResolveGroundContact(ref Vector2 position, ref Vector2 velocity, ref bool isGrounded)
    {
        GroundContactResult result = default;
        bool wasAirborne = !isGrounded;
        bool wasGrounded = isGrounded;

        if (position.y <= 0f)
        {
            position.y = 0f;
            velocity.y = 0f;
            isGrounded = true;
            result.landedFromAir = wasAirborne;
        }
        else
        {
            isGrounded = false;
            result.becameAirborne = wasGrounded;
        }

        return result;
    }
}

public struct GroundContactResult
{
    public bool landedFromAir;
    public bool becameAirborne;
}
