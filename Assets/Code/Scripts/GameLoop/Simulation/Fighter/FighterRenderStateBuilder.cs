using UnityEngine;

public sealed class FighterRenderStateBuilder
{
    public readonly struct Snapshot
    {
        public readonly FighterVisualState lastVisualState;
        public readonly MoveType lastVisualMoveType;
        public readonly int visualStateFrame;
        public readonly uint animationSerial;
        public readonly FighterState lastSimulationState;
        public readonly bool hasLastSimulationState;
        public readonly int crouchTransitionFramesRemaining;

        public Snapshot(
            FighterVisualState lastVisualState,
            MoveType lastVisualMoveType,
            int visualStateFrame,
            uint animationSerial,
            FighterState lastSimulationState,
            bool hasLastSimulationState,
            int crouchTransitionFramesRemaining
        )
        {
            this.lastVisualState = lastVisualState;
            this.lastVisualMoveType = lastVisualMoveType;
            this.visualStateFrame = visualStateFrame;
            this.animationSerial = animationSerial;
            this.lastSimulationState = lastSimulationState;
            this.hasLastSimulationState = hasLastSimulationState;
            this.crouchTransitionFramesRemaining = crouchTransitionFramesRemaining;
        }
    }

    private const int CROUCH_TRANSITION_FRAMES = 5;

    private FighterVisualState lastVisualState = FighterVisualState.None;
    private MoveType lastVisualMoveType = MoveType.None;
    private int visualStateFrame;
    private uint animationSerial;
    private FighterState lastSimulationState = FighterState.NeutralGround;
    private bool hasLastSimulationState;
    private int crouchTransitionFramesRemaining;

    public FighterRenderSnapshot BuildSnapshot(
        FighterState state,
        Vector2 velocity,
        bool facingRight,
        MoveType currentMoveType,
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

        bool enteredCrouch = false;
        if (state == FighterState.Crouching)
        {
            // Crouch transition is only for standing idle/walk -> crouch.
            enteredCrouch =
                hasLastSimulationState
                && lastSimulationState == FighterState.NeutralGround
                && IsStandingLocomotionVisualState(lastVisualState);
            if (enteredCrouch)
                crouchTransitionFramesRemaining = CROUCH_TRANSITION_FRAMES;
        }
        else
        {
            crouchTransitionFramesRemaining = 0;
        }

        FighterVisualState visualState = ResolveVisualState(
            state,
            velocity,
            facingRight,
            crouchTransitionFramesRemaining,
            cancelledCrouchDuringTransition
        );
        MoveType visualMoveType = ResolveVisualMoveType(visualState, currentMoveType);

        bool shouldRestart =
            forceRestart
            || visualState != lastVisualState
            || visualMoveType != lastVisualMoveType;

        if (shouldRestart)
        {
            animationSerial++;
            visualStateFrame = 0;
            lastVisualState = visualState;
            lastVisualMoveType = visualMoveType;
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
            visualMoveType,
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

    private static MoveType ResolveVisualMoveType(FighterVisualState visualState, MoveType currentMoveType)
    {
        if (visualState == FighterVisualState.Attacking)
            return currentMoveType;

        return MoveType.None;
    }

    private static bool IsMovingForward(Vector2 velocity, bool facingRight)
    {
        if (Mathf.Abs(velocity.x) <= 0.01f)
            return false;

        return facingRight ? velocity.x > 0f : velocity.x < 0f;
    }

    private static bool IsStandingLocomotionVisualState(FighterVisualState visualState)
    {
        return visualState == FighterVisualState.Idle
            || visualState == FighterVisualState.WalkForward
            || visualState == FighterVisualState.WalkBackward;
    }

    public Snapshot CaptureSnapshot()
    {
        return new Snapshot(
            lastVisualState,
            lastVisualMoveType,
            visualStateFrame,
            animationSerial,
            lastSimulationState,
            hasLastSimulationState,
            crouchTransitionFramesRemaining
        );
    }

    public void RestoreSnapshot(Snapshot snapshot)
    {
        lastVisualState = snapshot.lastVisualState;
        lastVisualMoveType = snapshot.lastVisualMoveType;
        visualStateFrame = snapshot.visualStateFrame;
        animationSerial = snapshot.animationSerial;
        lastSimulationState = snapshot.lastSimulationState;
        hasLastSimulationState = snapshot.hasLastSimulationState;
        crouchTransitionFramesRemaining = snapshot.crouchTransitionFramesRemaining;
    }
}
