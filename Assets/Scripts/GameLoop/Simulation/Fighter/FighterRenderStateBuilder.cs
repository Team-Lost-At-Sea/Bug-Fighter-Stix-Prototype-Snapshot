using UnityEngine;

public sealed class FighterRenderStateBuilder
{
    private const int CROUCH_TRANSITION_FRAMES = 5;

    private FighterVisualState lastVisualState = FighterVisualState.None;
    private AttackType lastVisualAttackType = AttackType.None;
    private int visualStateFrame;
    private uint animationSerial;
    private FighterState lastSimulationState = FighterState.NeutralGround;
    private bool hasLastSimulationState;
    private int crouchTransitionFramesRemaining;
    private int nonCrouchFrames;

    public FighterRenderSnapshot BuildSnapshot(
        FighterState state,
        Vector2 velocity,
        bool facingRight,
        AttackType currentAttackType,
        bool attackStartedAirborne,
        bool attackStartedCrouching,
        bool hadAttackInputThisTick,
        bool freezeAnimation,
        bool forceRestart
    )
    {
        bool cancelledCrouchDuringTransition =
            state == FighterState.NeutralGround
            && hasLastSimulationState
            && lastSimulationState == FighterState.Crouching
            && crouchTransitionFramesRemaining > 0
            && !hadAttackInputThisTick;

        bool wasCrouchingLastFrame = hasLastSimulationState && lastSimulationState == FighterState.Crouching;
        bool enteredCrouch = false;
        if (state == FighterState.Crouching)
        {
            bool canReplayTransition = !hasLastSimulationState || nonCrouchFrames > 1;
            enteredCrouch = !wasCrouchingLastFrame && canReplayTransition;
            if (enteredCrouch)
                crouchTransitionFramesRemaining = CROUCH_TRANSITION_FRAMES;

            nonCrouchFrames = 0;
        }
        else
        {
            crouchTransitionFramesRemaining = 0;
            nonCrouchFrames++;
        }

        FighterVisualState visualState = ResolveVisualState(
            state,
            velocity,
            facingRight,
            crouchTransitionFramesRemaining,
            cancelledCrouchDuringTransition
        );
        AttackType visualAttackType = ResolveVisualAttackType(visualState, currentAttackType);

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

        if (state == FighterState.Crouching && crouchTransitionFramesRemaining > 0)
            crouchTransitionFramesRemaining--;

        lastSimulationState = state;
        hasLastSimulationState = true;

        return new FighterRenderSnapshot(
            visualState,
            visualAttackType,
            attackStartedAirborne,
            attackStartedCrouching,
            visualStateFrame,
            animationSerial,
            shouldRestart,
            freezeAnimation
        );
    }

    private static FighterVisualState ResolveVisualState(
        FighterState state,
        Vector2 velocity,
        bool facingRight,
        int crouchTransitionFramesRemaining,
        bool cancelledCrouchDuringTransition
    )
    {
        if (cancelledCrouchDuringTransition)
            return FighterVisualState.Idle;

        if (state == FighterState.Hitstun)
            return FighterVisualState.Hitstun;
        if (state == FighterState.Blockstun)
            return FighterVisualState.Blockstun;
        if (state == FighterState.Knockdown)
            return FighterVisualState.Knockdown;
        if (state == FighterState.JumpStartup)
            return FighterVisualState.JumpStartup;
        if (state == FighterState.Crouching)
        {
            if (crouchTransitionFramesRemaining > 0)
                return FighterVisualState.CrouchTransition;
            return FighterVisualState.Crouching;
        }
        if (state == FighterState.NeutralAir)
            return FighterVisualState.Airborne;
        if (state == FighterState.LandingRecovery)
            return FighterVisualState.Landing;
        if (
            state == FighterState.AttackStartup
            || state == FighterState.AttackActive
            || state == FighterState.AttackRecovery
        )
            return FighterVisualState.Attacking;

        if (Mathf.Abs(velocity.x) > 0.01f)
            return IsMovingForward(velocity, facingRight) ? FighterVisualState.WalkForward : FighterVisualState.WalkBackward;

        return FighterVisualState.Idle;
    }

    private static AttackType ResolveVisualAttackType(FighterVisualState visualState, AttackType currentAttackType)
    {
        if (visualState == FighterVisualState.Attacking)
            return currentAttackType;

        return AttackType.None;
    }

    private static bool IsMovingForward(Vector2 velocity, bool facingRight)
    {
        if (Mathf.Abs(velocity.x) <= 0.01f)
            return false;

        return facingRight ? velocity.x > 0f : velocity.x < 0f;
    }
}
