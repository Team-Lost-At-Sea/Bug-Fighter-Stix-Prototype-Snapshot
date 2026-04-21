using System.Collections.Generic;
using System.Diagnostics;

public sealed class RollbackMatchSession : IMatchSession
{
    private readonly ISimulationCore simulationCore;
    private readonly int localPlayerId;
    private readonly int remotePlayerId;
    private readonly int inputDelayFrames;
    private readonly int rollbackWindowFrames;
    private readonly INetworkAdapter networkAdapter;

    private readonly Dictionary<int, FrameInputPacket> localInputsByFrame = new Dictionary<int, FrameInputPacket>();
    private readonly Dictionary<int, FrameInputPacket> remoteInputsByFrame = new Dictionary<int, FrameInputPacket>();
    private readonly Dictionary<int, FrameInputPacket> predictedRemoteInputsByFrame = new Dictionary<int, FrameInputPacket>();
    private readonly Dictionary<int, FrameInputPacket> usedPlayer1PacketsByFrame = new Dictionary<int, FrameInputPacket>();
    private readonly Dictionary<int, FrameInputPacket> usedPlayer2PacketsByFrame = new Dictionary<int, FrameInputPacket>();
    private readonly Dictionary<int, NetState> stateBufferByFrame = new Dictionary<int, NetState>();
    private readonly List<FrameInputPacket> polledRemotePackets = new List<FrameInputPacket>(32);

    private RollbackMetrics metrics;
    private FrameInputPacket lastRemoteAuthoritativePacket;
    private bool hasLastRemoteAuthoritativePacket;
    private int rollbackStartFrame = -1;

    public RollbackMetrics Metrics => metrics;

    public RollbackMatchSession(
        ISimulationCore simulationCore,
        int localPlayerId = 1,
        int inputDelayFrames = 2,
        int rollbackWindowFrames = 240,
        INetworkAdapter networkAdapter = null
    )
    {
        this.simulationCore = simulationCore;
        this.localPlayerId = localPlayerId == 2 ? 2 : 1;
        remotePlayerId = this.localPlayerId == 1 ? 2 : 1;
        this.inputDelayFrames = inputDelayFrames < 0 ? 0 : inputDelayFrames;
        this.rollbackWindowFrames = rollbackWindowFrames < 10 ? 10 : rollbackWindowFrames;
        this.networkAdapter = networkAdapter;

        stateBufferByFrame[simulationCore.CurrentFrame] = simulationCore.CaptureNetState();
    }

    public void SubmitLocalInput(FrameInputPacket packet)
    {
        localInputsByFrame[packet.frame] = packet;
        networkAdapter?.SendLocalInput(packet, simulationCore.CurrentFrame);
    }

    public int PollRemoteInputs()
    {
        if (networkAdapter == null)
            return 0;

        int count = networkAdapter.PollRemoteInputs(simulationCore.CurrentFrame, polledRemotePackets);
        for (int i = 0; i < polledRemotePackets.Count; i++)
            RegisterRemoteInput(polledRemotePackets[i]);

        return count;
    }

    public void AdvanceFrame()
    {
        PollRemoteInputs();

        int nextFrame = simulationCore.CurrentFrame + 1;
        CaptureStateForFrame(simulationCore.CurrentFrame);

        FrameInputPacket localPacket = ResolveLocalPacket(nextFrame);
        FrameInputPacket remotePacket = ResolveRemotePacket(nextFrame);

        ApplyFrame(nextFrame, localPacket, remotePacket);

        if (rollbackStartFrame > 0 && rollbackStartFrame <= simulationCore.CurrentFrame)
        {
            ExecuteRollback(rollbackStartFrame, simulationCore.CurrentFrame);
            rollbackStartFrame = -1;
        }

        TrimOldBuffers(simulationCore.CurrentFrame - rollbackWindowFrames);
    }

    private void RegisterRemoteInput(FrameInputPacket packet)
    {
        remoteInputsByFrame[packet.frame] = packet;
        if (!hasLastRemoteAuthoritativePacket || packet.frame >= lastRemoteAuthoritativePacket.frame)
        {
            lastRemoteAuthoritativePacket = packet;
            hasLastRemoteAuthoritativePacket = true;
        }

        if (!predictedRemoteInputsByFrame.TryGetValue(packet.frame, out FrameInputPacket predicted))
            return;

        if (InputPacketCodec.ContentEquals(predicted, packet))
            return;

        if (packet.frame <= simulationCore.CurrentFrame)
        {
            if (rollbackStartFrame < 0)
                rollbackStartFrame = packet.frame;
            else
                rollbackStartFrame = packet.frame < rollbackStartFrame ? packet.frame : rollbackStartFrame;
        }
    }

    private FrameInputPacket ResolveLocalPacket(int frame)
    {
        int delayedFrame = frame - inputDelayFrames;
        if (localInputsByFrame.TryGetValue(delayedFrame, out FrameInputPacket delayedPacket))
        {
            delayedPacket.frame = frame;
            delayedPacket.playerId = localPlayerId;
            return delayedPacket;
        }

        return FrameInputPacket.Neutral(frame, localPlayerId);
    }

    private FrameInputPacket ResolveRemotePacket(int frame)
    {
        if (remoteInputsByFrame.TryGetValue(frame, out FrameInputPacket packet))
            return packet;

        FrameInputPacket predicted = hasLastRemoteAuthoritativePacket
            ? BuildPredictedRemotePacket(frame)
            : FrameInputPacket.Neutral(frame, remotePlayerId);
        predictedRemoteInputsByFrame[frame] = predicted;
        return predicted;
    }

    private FrameInputPacket BuildPredictedRemotePacket(int frame)
    {
        FrameInputPacket predicted = lastRemoteAuthoritativePacket;
        predicted.frame = frame;
        predicted.playerId = remotePlayerId;

        InputButtons buttons = (InputButtons)predicted.buttonsBitmask;
        buttons &= ~InputButtons.PunchLightPressed;
        buttons &= ~InputButtons.PunchMediumPressed;
        buttons &= ~InputButtons.PunchHeavyPressed;
        buttons &= ~InputButtons.DirtPressed;
        predicted.buttonsBitmask = (ushort)buttons;

        return predicted;
    }

    private void ApplyFrame(int frame, FrameInputPacket localPacket, FrameInputPacket remotePacket)
    {
        FrameInputPacket player1Packet = localPacket.playerId == 1 ? localPacket : remotePacket;
        FrameInputPacket player2Packet = localPacket.playerId == 2 ? localPacket : remotePacket;

        simulationCore.Tick(player1Packet);
        simulationCore.Tick(player2Packet);

        usedPlayer1PacketsByFrame[frame] = player1Packet;
        usedPlayer2PacketsByFrame[frame] = player2Packet;
        CaptureStateForFrame(frame);
    }

    private void CaptureStateForFrame(int frame)
    {
        stateBufferByFrame[frame] = simulationCore.CaptureNetState();
    }

    private void ExecuteRollback(int startFrame, int currentFrame)
    {
        if (!stateBufferByFrame.TryGetValue(startFrame - 1, out NetState rollbackBase))
            return;

        Stopwatch stopwatch = Stopwatch.StartNew();
        simulationCore.RestoreNetState(rollbackBase);

        for (int frame = startFrame; frame <= currentFrame; frame++)
        {
            FrameInputPacket localPacket = ResolveLocalPacket(frame);
            FrameInputPacket remotePacket = ResolveRemotePacket(frame);
            ApplyFrame(frame, localPacket, remotePacket);
        }

        stopwatch.Stop();

        metrics.rollbackCount++;
        int rollbackFrames = currentFrame - startFrame + 1;
        if (rollbackFrames > metrics.maxRollbackFrames)
            metrics.maxRollbackFrames = rollbackFrames;

        metrics.lastResimCostMs = (float)stopwatch.Elapsed.TotalMilliseconds;
        if (metrics.lastResimCostMs > metrics.maxResimCostMs)
            metrics.maxResimCostMs = metrics.lastResimCostMs;
    }

    private void TrimOldBuffers(int minFrameInclusive)
    {
        if (minFrameInclusive <= 0)
            return;

        RemoveOlderThan(localInputsByFrame, minFrameInclusive);
        RemoveOlderThan(remoteInputsByFrame, minFrameInclusive);
        RemoveOlderThan(predictedRemoteInputsByFrame, minFrameInclusive);
        RemoveOlderThan(usedPlayer1PacketsByFrame, minFrameInclusive);
        RemoveOlderThan(usedPlayer2PacketsByFrame, minFrameInclusive);
        RemoveOlderThan(stateBufferByFrame, minFrameInclusive - 1);
    }

    private static void RemoveOlderThan<TValue>(Dictionary<int, TValue> map, int minFrameInclusive)
    {
        if (map.Count == 0)
            return;

        List<int> keysToRemove = null;
        foreach (KeyValuePair<int, TValue> kv in map)
        {
            if (kv.Key >= minFrameInclusive)
                continue;

            if (keysToRemove == null)
                keysToRemove = new List<int>();
            keysToRemove.Add(kv.Key);
        }

        if (keysToRemove == null)
            return;

        for (int i = 0; i < keysToRemove.Count; i++)
            map.Remove(keysToRemove[i]);
    }
}
