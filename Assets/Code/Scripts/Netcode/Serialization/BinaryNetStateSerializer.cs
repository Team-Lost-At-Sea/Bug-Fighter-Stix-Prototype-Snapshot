using System;
using System.IO;

public sealed class BinaryNetStateSerializer : INetStateSerializer
{
    public byte[] Serialize(NetState state)
    {
        using (MemoryStream stream = new MemoryStream(4096))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            WriteState(writer, state);
            writer.Flush();
            return stream.ToArray();
        }
    }

    public NetState Deserialize(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return NetState.CreateDefault();

        using (MemoryStream stream = new MemoryStream(bytes, false))
        using (BinaryReader reader = new BinaryReader(stream))
        {
            NetState state = ReadState(reader);
            return NetStateMigrator.MigrateToCurrent(state);
        }
    }

    private static void WriteState(BinaryWriter writer, NetState state)
    {
        writer.Write(state.stateVersion);
        writer.Write(state.frame);
        writer.Write(state.nextProjectileId);
        writer.Write(state.randomSeed);
        writer.Write(state.roundTimerFramesRemaining);
        writer.Write(state.roundTimerEnabled);
        writer.Write(state.previousPlayer1X);
        writer.Write(state.previousPlayer2X);

        WriteFighterState(writer, state.player1);
        WriteFighterState(writer, state.player2);

        NetProjectileState[] projectiles = state.projectiles ?? Array.Empty<NetProjectileState>();
        writer.Write(projectiles.Length);
        for (int i = 0; i < projectiles.Length; i++)
            WriteProjectileState(writer, projectiles[i]);
    }

    private static NetState ReadState(BinaryReader reader)
    {
        NetState state = NetState.CreateDefault();
        state.stateVersion = reader.ReadInt32();
        state.frame = reader.ReadInt32();
        state.nextProjectileId = reader.ReadInt32();
        state.randomSeed = reader.ReadInt32();
        state.roundTimerFramesRemaining = reader.ReadInt32();
        state.roundTimerEnabled = reader.ReadBoolean();
        state.previousPlayer1X = reader.ReadSingle();
        state.previousPlayer2X = reader.ReadSingle();
        state.player1 = ReadFighterState(reader, state.stateVersion);
        state.player2 = ReadFighterState(reader, state.stateVersion);

        int projectileCount = reader.ReadInt32();
        if (projectileCount < 0)
            projectileCount = 0;

        state.projectiles = new NetProjectileState[projectileCount];
        for (int i = 0; i < projectileCount; i++)
            state.projectiles[i] = ReadProjectileState(reader);

        return state;
    }

    private static void WriteFighterState(BinaryWriter writer, NetFighterState state)
    {
        writer.Write(state.positionX);
        writer.Write(state.positionY);
        writer.Write(state.velocityX);
        writer.Write(state.velocityY);
        writer.Write(state.isGrounded);
        writer.Write(state.facingRight);
        writer.Write(state.fighterState);
        writer.Write(state.stateFrame);
        writer.Write(state.transitionedThisTick);
        writer.Write(state.stateFrameFrozenThisTick);
        writer.Write(state.hitstopFramesRemaining);
        writer.Write(state.hitstunFramesRemaining);
        writer.Write(state.isHoldingBlockInput);
        writer.Write(state.canCurrentlyBlock);
        writer.Write(state.isHoldingValidBlockDirection);
        writer.Write(state.hadAttackInputThisTick);
        writer.Write(state.lightPressBufferFramesRemaining);
        writer.Write(state.mediumPressBufferFramesRemaining);
        writer.Write(state.heavyPressBufferFramesRemaining);
        WritePendingProjectileRequest(writer, state.pendingProjectileRequest);

        writer.Write(state.renderVisualState);
        writer.Write(state.renderMoveType);
        writer.Write(state.renderVisualStateFrame);
        writer.Write(state.renderAnimationSerial);
        writer.Write(state.renderRestartAnimation);
        writer.Write(state.renderFreezeAnimation);

        writer.Write(state.attackMoveType);
        writer.Write(state.attackFrame);
        writer.Write(state.attackHasData);
        writer.Write(state.hitboxCenterX);
        writer.Write(state.hitboxCenterY);
        writer.Write(state.hitboxHalfX);
        writer.Write(state.hitboxHalfY);
        writer.Write(state.hitboxDamage);
        writer.Write(state.hitboxHitstunFrames);
        writer.Write(state.hitboxActive);
        writer.Write(state.hitboxHasHit);

        writer.Write(state.landingRecoveryTicksRemaining);
        writer.Write(state.queuedJumpMoveX);
        writer.Write(state.usedAirNormalThisJump);

        writer.Write(state.builderLastVisualState);
        writer.Write(state.builderLastVisualMoveType);
        writer.Write(state.builderVisualStateFrame);
        writer.Write(state.builderAnimationSerial);
        writer.Write(state.builderLastSimulationState);
        writer.Write(state.builderHasLastSimulationState);
        writer.Write(state.builderCrouchTransitionFramesRemaining);

        NetInputHistoryEntry[] entries = state.inputHistoryEntries ?? Array.Empty<NetInputHistoryEntry>();
        writer.Write(entries.Length);
        for (int i = 0; i < entries.Length; i++)
            WriteInputHistoryEntry(writer, entries[i]);
        writer.Write(state.inputHistoryNextWriteIndex);
        writer.Write(state.inputHistoryCount);
    }

    private static NetFighterState ReadFighterState(BinaryReader reader, int stateVersion)
    {
        NetFighterState state = default;
        state.positionX = reader.ReadSingle();
        state.positionY = reader.ReadSingle();
        state.velocityX = reader.ReadSingle();
        state.velocityY = reader.ReadSingle();
        state.isGrounded = reader.ReadBoolean();
        state.facingRight = reader.ReadBoolean();
        state.fighterState = reader.ReadInt32();
        state.stateFrame = reader.ReadInt32();
        state.transitionedThisTick = reader.ReadBoolean();
        state.stateFrameFrozenThisTick = reader.ReadBoolean();
        state.hitstopFramesRemaining = reader.ReadInt32();
        state.hitstunFramesRemaining = reader.ReadInt32();
        state.isHoldingBlockInput = reader.ReadBoolean();
        state.canCurrentlyBlock = reader.ReadBoolean();
        state.isHoldingValidBlockDirection = reader.ReadBoolean();
        state.hadAttackInputThisTick = reader.ReadBoolean();
        if (stateVersion <= 1)
        {
            // Legacy v1 payload fields removed in v2. Consume and discard.
            reader.ReadInt32();
            reader.ReadString();
        }
        state.lightPressBufferFramesRemaining = reader.ReadInt32();
        state.mediumPressBufferFramesRemaining = reader.ReadInt32();
        state.heavyPressBufferFramesRemaining = reader.ReadInt32();
        state.pendingProjectileRequest = ReadPendingProjectileRequest(reader);

        state.renderVisualState = reader.ReadInt32();
        state.renderMoveType = reader.ReadInt32();
        state.renderVisualStateFrame = reader.ReadInt32();
        state.renderAnimationSerial = reader.ReadUInt32();
        state.renderRestartAnimation = reader.ReadBoolean();
        state.renderFreezeAnimation = reader.ReadBoolean();

        state.attackMoveType = reader.ReadInt32();
        state.attackFrame = reader.ReadInt32();
        state.attackHasData = reader.ReadBoolean();
        state.hitboxCenterX = reader.ReadSingle();
        state.hitboxCenterY = reader.ReadSingle();
        state.hitboxHalfX = reader.ReadSingle();
        state.hitboxHalfY = reader.ReadSingle();
        state.hitboxDamage = reader.ReadInt32();
        state.hitboxHitstunFrames = reader.ReadInt32();
        state.hitboxActive = reader.ReadBoolean();
        state.hitboxHasHit = reader.ReadBoolean();

        state.landingRecoveryTicksRemaining = reader.ReadInt32();
        state.queuedJumpMoveX = reader.ReadInt32();
        state.usedAirNormalThisJump = reader.ReadBoolean();

        state.builderLastVisualState = reader.ReadInt32();
        state.builderLastVisualMoveType = reader.ReadInt32();
        state.builderVisualStateFrame = reader.ReadInt32();
        state.builderAnimationSerial = reader.ReadUInt32();
        state.builderLastSimulationState = reader.ReadInt32();
        state.builderHasLastSimulationState = reader.ReadBoolean();
        state.builderCrouchTransitionFramesRemaining = reader.ReadInt32();

        int historyCount = reader.ReadInt32();
        if (historyCount < 0)
            historyCount = 0;
        state.inputHistoryEntries = new NetInputHistoryEntry[historyCount];
        for (int i = 0; i < historyCount; i++)
            state.inputHistoryEntries[i] = ReadInputHistoryEntry(reader);
        state.inputHistoryNextWriteIndex = reader.ReadInt32();
        state.inputHistoryCount = reader.ReadInt32();
        return state;
    }

    private static void WriteProjectileState(BinaryWriter writer, NetProjectileState state)
    {
        writer.Write(state.id);
        writer.Write(state.ownerPlayerId);
        writer.Write(state.positionX);
        writer.Write(state.positionY);
        writer.Write(state.velocityX);
        writer.Write(state.velocityY);
        writer.Write(state.halfSizeX);
        writer.Write(state.halfSizeY);
        writer.Write(state.lifetimeFramesRemaining);
        writer.Write(state.damage);
        writer.Write(state.hitstunFrames);
        writer.Write(state.active);
    }

    private static NetProjectileState ReadProjectileState(BinaryReader reader)
    {
        NetProjectileState state = default;
        state.id = reader.ReadInt32();
        state.ownerPlayerId = reader.ReadInt32();
        state.positionX = reader.ReadSingle();
        state.positionY = reader.ReadSingle();
        state.velocityX = reader.ReadSingle();
        state.velocityY = reader.ReadSingle();
        state.halfSizeX = reader.ReadSingle();
        state.halfSizeY = reader.ReadSingle();
        state.lifetimeFramesRemaining = reader.ReadInt32();
        state.damage = reader.ReadInt32();
        state.hitstunFrames = reader.ReadInt32();
        state.active = reader.ReadBoolean();
        return state;
    }

    private static void WritePendingProjectileRequest(BinaryWriter writer, NetPendingProjectileRequestState request)
    {
        writer.Write(request.hasPending);
        writer.Write(request.ownerPlayerId);
        writer.Write(request.positionX);
        writer.Write(request.positionY);
        writer.Write(request.velocityX);
        writer.Write(request.velocityY);
        writer.Write(request.halfSizeX);
        writer.Write(request.halfSizeY);
        writer.Write(request.lifetimeFrames);
        writer.Write(request.damage);
        writer.Write(request.hitstunFrames);
    }

    private static NetPendingProjectileRequestState ReadPendingProjectileRequest(BinaryReader reader)
    {
        NetPendingProjectileRequestState request = default;
        request.hasPending = reader.ReadBoolean();
        request.ownerPlayerId = reader.ReadInt32();
        request.positionX = reader.ReadSingle();
        request.positionY = reader.ReadSingle();
        request.velocityX = reader.ReadSingle();
        request.velocityY = reader.ReadSingle();
        request.halfSizeX = reader.ReadSingle();
        request.halfSizeY = reader.ReadSingle();
        request.lifetimeFrames = reader.ReadInt32();
        request.damage = reader.ReadInt32();
        request.hitstunFrames = reader.ReadInt32();
        return request;
    }

    private static void WriteInputHistoryEntry(BinaryWriter writer, NetInputHistoryEntry entry)
    {
        writer.Write(entry.input.moveX);
        writer.Write(entry.input.moveY);
        writer.Write(entry.input.punchLight);
        writer.Write(entry.input.punchMedium);
        writer.Write(entry.input.punchHeavy);
        writer.Write(entry.input.punchLightPressed);
        writer.Write(entry.input.punchMediumPressed);
        writer.Write(entry.input.punchHeavyPressed);
        writer.Write(entry.relativeDirection);
    }

    private static NetInputHistoryEntry ReadInputHistoryEntry(BinaryReader reader)
    {
        NetInputHistoryEntry entry = default;
        InputFrame input = default;
        input.moveX = reader.ReadSingle();
        input.moveY = reader.ReadSingle();
        input.punchLight = reader.ReadBoolean();
        input.punchMedium = reader.ReadBoolean();
        input.punchHeavy = reader.ReadBoolean();
        input.punchLightPressed = reader.ReadBoolean();
        input.punchMediumPressed = reader.ReadBoolean();
        input.punchHeavyPressed = reader.ReadBoolean();
        entry.input = input;
        entry.relativeDirection = reader.ReadInt32();
        return entry;
    }
}
