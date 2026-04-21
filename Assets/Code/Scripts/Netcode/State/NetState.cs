public struct NetInputHistoryEntry
{
    public InputFrame input;
    public int relativeDirection;
}

public struct NetPendingProjectileRequestState
{
    public bool hasPending;
    public int ownerPlayerId;
    public float positionX;
    public float positionY;
    public float velocityX;
    public float velocityY;
    public float halfSizeX;
    public float halfSizeY;
    public int lifetimeFrames;
    public int damage;
    public int hitstunFrames;
    public int blockstunFrames;
    public float blockPushback;
    public int chipDamage;
    public int attackerBlockstopFrames;
    public int hitLevel;
}

public struct NetFighterState
{
    public float positionX;
    public float positionY;
    public float velocityX;
    public float velocityY;
    public bool isGrounded;
    public bool facingRight;
    public int fighterState;
    public int stateFrame;
    public bool transitionedThisTick;
    public bool stateFrameFrozenThisTick;
    public int hitstopFramesRemaining;
    public int hitstunFramesRemaining;
    public int blockstunFramesRemaining;
    public bool isHoldingBlockInput;
    public bool canCurrentlyBlock;
    public bool isHoldingValidBlockDirection;
    public bool parryInputPressedThisTick;
    public int parryLastMoveXSign;
    public int parryWindowFramesRemaining;
    public int parryLockoutFramesRemaining;
    public int health;
    public int totalDamageTaken;
    public int totalChipDamageTaken;
    public int lastReceivedHitResult;
    public int lastReceivedHitLevel;
    public int lastReceivedStunFrames;
    public int lastReceivedChipDamage;
    public bool hadAttackInputThisTick;
    public int lightPressBufferFramesRemaining;
    public int mediumPressBufferFramesRemaining;
    public int heavyPressBufferFramesRemaining;
    public bool isDefeated;
    public NetPendingProjectileRequestState pendingProjectileRequest;

    public int renderVisualState;
    public int renderMoveType;
    public int renderVisualStateFrame;
    public uint renderAnimationSerial;
    public bool renderRestartAnimation;
    public bool renderFreezeAnimation;

    public int attackMoveType;
    public int attackFrame;
    public bool attackHasData;
    public float hitboxCenterX;
    public float hitboxCenterY;
    public float hitboxHalfX;
    public float hitboxHalfY;
    public int hitboxDamage;
    public int hitboxHitstunFrames;
    public int hitboxBlockstunFrames;
    public float hitboxBlockPushback;
    public int hitboxChipDamage;
    public int hitboxAttackerBlockstopFrames;
    public int hitboxHitLevel;
    public bool hitboxIsProjectile;
    public bool hitboxIsThrow;
    public bool hitboxActive;
    public bool hitboxHasHit;

    public int landingRecoveryTicksRemaining;
    public int queuedJumpMoveX;
    public bool usedAirNormalThisJump;

    public int builderLastVisualState;
    public int builderLastVisualMoveType;
    public int builderVisualStateFrame;
    public uint builderAnimationSerial;
    public int builderLastSimulationState;
    public bool builderHasLastSimulationState;
    public int builderCrouchTransitionFramesRemaining;

    public NetInputHistoryEntry[] inputHistoryEntries;
    public int inputHistoryNextWriteIndex;
    public int inputHistoryCount;
}

public struct NetProjectileState
{
    public int id;
    public int ownerPlayerId;
    public float positionX;
    public float positionY;
    public float velocityX;
    public float velocityY;
    public float halfSizeX;
    public float halfSizeY;
    public int lifetimeFramesRemaining;
    public int damage;
    public int hitstunFrames;
    public int blockstunFrames;
    public float blockPushback;
    public int chipDamage;
    public int attackerBlockstopFrames;
    public int hitLevel;
    public bool active;
}

public struct NetState
{
    public const int CurrentVersion = 5;

    public int stateVersion;
    public int frame;
    public int nextProjectileId;
    public int randomSeed;
    public int roundPhase;
    public int lastRoundResult;
    public int lastRoundEndType;
    public int matchWinner;
    public int player1RoundWins;
    public int player2RoundWins;
    public int roundNumber;
    public int phaseFramesRemaining;
    public int roundTimerFramesRemaining;
    public bool roundTimerEnabled;
    public float previousPlayer1X;
    public float previousPlayer2X;
    public NetFighterState player1;
    public NetFighterState player2;
    public NetProjectileState[] projectiles;

    public static NetState CreateDefault()
    {
        return new NetState
        {
            stateVersion = CurrentVersion,
            frame = 0,
            nextProjectileId = 1,
            randomSeed = 0,
            roundPhase = (int)RoundPhase.Fighting,
            lastRoundResult = (int)RoundResult.None,
            lastRoundEndType = (int)RoundEndType.None,
            matchWinner = (int)MatchWinner.None,
            player1RoundWins = 0,
            player2RoundWins = 0,
            roundNumber = 1,
            phaseFramesRemaining = 0,
            roundTimerFramesRemaining = 0,
            roundTimerEnabled = false,
            projectiles = System.Array.Empty<NetProjectileState>()
        };
    }
}
