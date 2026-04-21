using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class NetcodeValidationHarness : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private MatchConfig matchConfig;

    [SerializeField]
    private FighterConfig player1Config;

    [SerializeField]
    private FighterConfig player2Config;

    [Header("Settings")]
    [SerializeField]
    [Min(1)]
    private int determinismFrames = 10000;

    [SerializeField]
    [Min(1)]
    private int serializationFrames = 300;

    [SerializeField]
    [Min(1)]
    private int rollbackFrames = 240;

    [ContextMenu("Run Netcode Validation Suite")]
    public void RunValidationSuite()
    {
        if (!ValidateSetup())
            return;

        StringBuilder report = new StringBuilder();
        report.AppendLine("[NetcodeValidation] Running...");

        bool determinismPass = RunDeterminism(report);
        bool serializationPass = RunSerializationRoundTrip(report);
        bool inputMessagePass = RunInputMessageProtocol(report);
        bool rollbackPass = RunRollbackMismatch(report);
        bool replayPass = RunReplayVerification(report);

        bool allPass = determinismPass && serializationPass && inputMessagePass && rollbackPass && replayPass;
        report.AppendLine(allPass ? "[NetcodeValidation] PASS" : "[NetcodeValidation] FAIL");
        Debug.Log(report.ToString(), this);
    }

    private bool ValidateSetup()
    {
        if (player1Config == null || player2Config == null)
        {
            Debug.LogError("[NetcodeValidation] Missing fighter configs.", this);
            return false;
        }

        return true;
    }

    private bool RunDeterminism(StringBuilder report)
    {
        int[] runA = RunHashTimeline(determinismFrames);
        int[] runB = RunHashTimeline(determinismFrames);
        int mismatch = FindMismatch(runA, runB);
        if (mismatch >= 0)
        {
            report.AppendLine($"- Determinism: FAIL at frame {mismatch + 1} (A={runA[mismatch]}, B={runB[mismatch]})");
            return false;
        }

        report.AppendLine($"- Determinism: PASS ({determinismFrames} frames, final={runA[runA.Length - 1]})");
        return true;
    }

    private bool RunSerializationRoundTrip(StringBuilder report)
    {
        Simulation baseline = CreateSimulation("SerializeA");
        for (int frame = 1; frame <= serializationFrames; frame++)
            StepWithScriptedPackets(baseline, frame);

        NetState captured = baseline.CaptureNetState();
        INetStateSerializer serializer = new BinaryNetStateSerializer();
        NetState roundTrip = serializer.Deserialize(serializer.Serialize(captured));

        Simulation restored = CreateSimulation("SerializeB");
        restored.RestoreNetState(roundTrip);

        const int verifyFrames = 300;
        for (int step = 1; step <= verifyFrames; step++)
        {
            int frame = serializationFrames + step;
            StepWithScriptedPackets(baseline, frame);
            StepWithScriptedPackets(restored, frame);
            int hashA = baseline.ComputeDeterminismHash();
            int hashB = restored.ComputeDeterminismHash();
            if (hashA != hashB)
            {
                report.AppendLine($"- Serialization Round Trip: FAIL at frame {frame} (A={hashA}, B={hashB})");
                return false;
            }
        }

        report.AppendLine($"- Serialization Round Trip: PASS (post-restore {verifyFrames} frame parity)");
        return true;
    }

    private bool RunInputMessageProtocol(StringBuilder report)
    {
        InputFrame dirtInput = InputFrame.Neutral;
        dirtInput.moveX = 1f;
        dirtInput.moveY = -1f;
        dirtInput.punchLight = true;
        dirtInput.dirt = true;
        dirtInput.punchLightPressed = true;
        dirtInput.dirtPressed = true;

        FrameInputPacket dirtPacket = InputPacketCodec.Encode(dirtInput, 24, 1, 99);
        NetInputMessage singleMessage = new NetInputMessage
        {
            protocolVersion = NetInputMessage.CurrentProtocolVersion,
            sessionId = 0x1234ABCD,
            senderPlayerId = 1,
            currentFrame = 24,
            sequence = 101,
            ackSequence = 88,
            packets = new[] { dirtPacket }
        };

        if (!RoundTripMessage(singleMessage, out NetInputMessage singleRoundTrip))
        {
            report.AppendLine("- Input Message Protocol: FAIL (single packet round trip rejected)");
            return false;
        }

        if (!MessagesEqual(singleMessage, singleRoundTrip))
        {
            report.AppendLine("- Input Message Protocol: FAIL (single packet changed during round trip)");
            return false;
        }

        InputFrame decodedDirt = InputPacketCodec.Decode(singleRoundTrip.packets[0]);
        if (!decodedDirt.dirt || !decodedDirt.dirtPressed || !decodedDirt.punchLight || !decodedDirt.punchLightPressed)
        {
            report.AppendLine("- Input Message Protocol: FAIL (Dirt/LP did not survive full input path)");
            return false;
        }

        RecentInputPacketBuffer recentBuffer = new RecentInputPacketBuffer(4);
        for (int frame = 1; frame <= 6; frame++)
            recentBuffer.Add(PacketForFrame(frame, 1, BuildInputForFrame(frame)));

        FrameInputPacket[] recentPackets = new FrameInputPacket[NetInputMessageCodec.MaxPacketsPerMessage];
        int recentCount = recentBuffer.CopyRecent(recentPackets);
        if (recentCount != 4 || recentPackets[0].frame != 3 || recentPackets[3].frame != 6)
        {
            report.AppendLine("- Input Message Protocol: FAIL (recent input buffer ordering/capacity)");
            return false;
        }

        FrameInputPacket[] bundledPackets = new FrameInputPacket[recentCount];
        for (int i = 0; i < recentCount; i++)
            bundledPackets[i] = recentPackets[i];

        NetInputMessage multiMessage = singleMessage;
        multiMessage.currentFrame = 6;
        multiMessage.sequence = 202;
        multiMessage.packets = bundledPackets;

        if (!RoundTripMessage(multiMessage, out NetInputMessage multiRoundTrip) || !MessagesEqual(multiMessage, multiRoundTrip))
        {
            report.AppendLine("- Input Message Protocol: FAIL (multi-packet round trip)");
            return false;
        }

        NetInputMessage emptyMessage = singleMessage;
        emptyMessage.sequence = 303;
        emptyMessage.packets = new FrameInputPacket[0];
        if (!RoundTripMessage(emptyMessage, out NetInputMessage emptyRoundTrip) || !MessagesEqual(emptyMessage, emptyRoundTrip))
        {
            report.AppendLine("- Input Message Protocol: FAIL (empty packet message round trip)");
            return false;
        }

        byte[] validBytes = NetInputMessageCodec.Serialize(singleMessage);
        byte[] badMagic = (byte[])validBytes.Clone();
        badMagic[0] = 0;
        if (NetInputMessageCodec.TryDeserialize(badMagic, out _))
        {
            report.AppendLine("- Input Message Protocol: FAIL (bad magic accepted)");
            return false;
        }

        byte[] badVersion = (byte[])validBytes.Clone();
        badVersion[2] = 0x7F;
        if (NetInputMessageCodec.TryDeserialize(badVersion, out _))
        {
            report.AppendLine("- Input Message Protocol: FAIL (unsupported protocol version accepted)");
            return false;
        }

        byte[] truncated = new byte[validBytes.Length - 1];
        for (int i = 0; i < truncated.Length; i++)
            truncated[i] = validBytes[i];
        if (NetInputMessageCodec.TryDeserialize(truncated, out _))
        {
            report.AppendLine("- Input Message Protocol: FAIL (truncated bytes accepted)");
            return false;
        }

        FrameInputPacket[] oversizedPackets = new FrameInputPacket[NetInputMessageCodec.MaxPacketsPerMessage + 1];
        NetInputMessage oversizedMessage = singleMessage;
        oversizedMessage.packets = oversizedPackets;
        bool oversizedRejected = false;
        try
        {
            NetInputMessageCodec.Serialize(oversizedMessage);
        }
        catch (System.IO.InvalidDataException)
        {
            oversizedRejected = true;
        }

        if (!oversizedRejected)
        {
            report.AppendLine("- Input Message Protocol: FAIL (oversized packet bundle serialized)");
            return false;
        }

        report.AppendLine("- Input Message Protocol: PASS (single, bundled, empty, Dirt, and malformed message checks)");
        return true;
    }

    private bool RunRollbackMismatch(StringBuilder report)
    {
        Simulation reference = CreateSimulation("RollbackRef");
        Simulation underTest = CreateSimulation("RollbackTest");

        ScriptedRemoteAdapter adapter = new ScriptedRemoteAdapter();
        for (int frame = 1; frame <= rollbackFrames; frame++)
        {
            InputFrame remote = BuildRemoteRollbackInput(frame);
            int deliverAtFrame = frame == 60 ? 76 : frame - 1;
            adapter.Schedule(PacketForFrame(frame, 2, remote), deliverAtFrame);
        }

        RollbackMatchSession session = new RollbackMatchSession(
            underTest,
            localPlayerId: 1,
            inputDelayFrames: 0,
            rollbackWindowFrames: 180,
            networkAdapter: adapter
        );

        for (int frame = 1; frame <= rollbackFrames; frame++)
        {
            InputFrame local = BuildInputForFrame(frame - 1);
            FrameInputPacket localPacket = PacketForFrame(frame, 1, local);
            session.SubmitLocalInput(localPacket);
            session.AdvanceFrame();

            InputFrame remote = BuildRemoteRollbackInput(frame);
            reference.Tick(PacketForFrame(frame, 1, local));
            reference.Tick(PacketForFrame(frame, 2, remote));
        }

        int expectedHash = reference.ComputeDeterminismHash();
        int rollbackHash = underTest.ComputeDeterminismHash();
        if (expectedHash != rollbackHash)
        {
            report.AppendLine($"- Rollback Forced Mismatch: FAIL (expected={expectedHash}, actual={rollbackHash})");
            return false;
        }

        RollbackMetrics metrics = session.Metrics;
        if (metrics.rollbackCount <= 0)
        {
            report.AppendLine("- Rollback Forced Mismatch: FAIL (no rollback observed)");
            return false;
        }

        report.AppendLine(
            $"- Rollback Forced Mismatch: PASS (rollbacks={metrics.rollbackCount}, maxFrames={metrics.maxRollbackFrames}, maxResimMs={metrics.maxResimCostMs:F3})"
        );
        return true;
    }

    private bool RunReplayVerification(StringBuilder report)
    {
        ReplayRecorder recorder = new ReplayRecorder();
        Simulation sim = CreateSimulation("ReplayCapture");
        const int frames = 360;
        for (int frame = 1; frame <= frames; frame++)
        {
            InputFrame p1 = BuildInputForFrame(frame - 1);
            InputFrame p2 = InputFrame.Neutral;
            FrameInputPacket p1Packet = PacketForFrame(frame, 1, p1);
            FrameInputPacket p2Packet = PacketForFrame(frame, 2, p2);
            sim.Tick(p1Packet);
            sim.Tick(p2Packet);
            recorder.Record(p1Packet);
            recorder.Record(p2Packet);
        }

        ReplayData replay = recorder.Build(matchConfig != null ? matchConfig.modeId : "Default", 60, frames);
        bool pass = ReplayVerifier.VerifyDeterministicHashes(player1Config, player2Config, matchConfig, replay, out string replayReport);
        report.AppendLine(pass ? "- Replay Verification: PASS" : "- Replay Verification: FAIL");
        report.AppendLine(replayReport);
        return pass;
    }

    private int[] RunHashTimeline(int frames)
    {
        Simulation simulation = CreateSimulation("DetRun");
        int[] hashes = new int[frames];
        for (int frame = 1; frame <= frames; frame++)
        {
            StepWithScriptedPackets(simulation, frame);
            hashes[frame - 1] = simulation.ComputeDeterminismHash();
        }

        return hashes;
    }

    private void StepWithScriptedPackets(Simulation simulation, int frame)
    {
        InputFrame p1 = BuildInputForFrame(frame - 1);
        simulation.Tick(PacketForFrame(frame, 1, p1));
        simulation.Tick(PacketForFrame(frame, 2, InputFrame.Neutral));
    }

    private Simulation CreateSimulation(string tag)
    {
        Simulation simulation = new Simulation(matchConfig);
        simulation.Initialize(player1Config, player2Config, $"{tag}_P1", $"{tag}_P2");
        return simulation;
    }

    private static int FindMismatch(int[] a, int[] b)
    {
        int count = Mathf.Min(a.Length, b.Length);
        for (int i = 0; i < count; i++)
        {
            if (a[i] != b[i])
                return i;
        }

        return -1;
    }

    private static FrameInputPacket PacketForFrame(int frame, int playerId, InputFrame input)
    {
        return InputPacketCodec.Encode(input, frame, playerId, (uint)frame);
    }

    private static InputFrame BuildRemoteRollbackInput(int frame)
    {
        if (frame != 60)
            return InputFrame.Neutral;

        InputFrame input = InputFrame.Neutral;
        input.punchLight = true;
        input.punchLightPressed = true;
        return input;
    }

    private static bool RoundTripMessage(NetInputMessage source, out NetInputMessage roundTrip)
    {
        return NetInputMessageCodec.TryDeserialize(NetInputMessageCodec.Serialize(source), out roundTrip);
    }

    private static bool MessagesEqual(NetInputMessage a, NetInputMessage b)
    {
        if (a.protocolVersion != b.protocolVersion
            || a.sessionId != b.sessionId
            || a.senderPlayerId != b.senderPlayerId
            || a.currentFrame != b.currentFrame
            || a.sequence != b.sequence
            || a.ackSequence != b.ackSequence)
        {
            return false;
        }

        int aCount = a.packets != null ? a.packets.Length : 0;
        int bCount = b.packets != null ? b.packets.Length : 0;
        if (aCount != bCount)
            return false;

        for (int i = 0; i < aCount; i++)
        {
            if (!PacketsEqual(a.packets[i], b.packets[i]))
                return false;
        }

        return true;
    }

    private static bool PacketsEqual(FrameInputPacket a, FrameInputPacket b)
    {
        return a.frame == b.frame
            && a.playerId == b.playerId
            && a.buttonsBitmask == b.buttonsBitmask
            && a.moveX == b.moveX
            && a.moveY == b.moveY
            && a.sequence == b.sequence;
    }

    private static InputFrame BuildInputForFrame(int frame)
    {
        int phase = frame % 180;
        float moveX = 0f;
        float moveY = 0f;

        if (phase < 24)
            moveX = 1f;
        else if (phase < 48)
            moveX = -1f;
        else if (phase < 68)
            moveY = -1f;
        else if (phase < 74)
            moveY = 1f;

        bool lightDown = IsHeld(frame, 14, 16);
        bool mediumDown = IsHeld(frame, 42, 43);
        bool heavyDown = IsHeld(frame, 73, 74);

        InputFrame input = InputFrame.Neutral;
        input.moveX = moveX;
        input.moveY = moveY;
        input.punchLight = lightDown;
        input.punchMedium = mediumDown;
        input.punchHeavy = heavyDown;
        input.punchLightPressed = lightDown && !IsHeld(frame - 1, 14, 16);
        input.punchMediumPressed = mediumDown && !IsHeld(frame - 1, 42, 43);
        input.punchHeavyPressed = heavyDown && !IsHeld(frame - 1, 73, 74);
        return input;
    }

    private static bool IsHeld(int frame, int phaseStartInclusive, int phaseEndExclusive)
    {
        if (frame < 0)
            return false;

        int phase = frame % 180;
        return phase >= phaseStartInclusive && phase < phaseEndExclusive;
    }

    private sealed class ScriptedRemoteAdapter : INetworkAdapter
    {
        private struct ScheduledPacket
        {
            public FrameInputPacket packet;
            public int deliverAtFrame;
        }

        private readonly List<ScheduledPacket> schedule = new List<ScheduledPacket>();

        public void Schedule(FrameInputPacket packet, int deliverAtFrame)
        {
            schedule.Add(new ScheduledPacket
            {
                packet = packet,
                deliverAtFrame = deliverAtFrame
            });
        }

        public void SendLocalInput(FrameInputPacket packet, int currentFrame)
        {
            // No-op for scripted adapter.
        }

        public int PollRemoteInputs(int currentFrame, List<FrameInputPacket> output)
        {
            output.Clear();
            for (int i = schedule.Count - 1; i >= 0; i--)
            {
                if (schedule[i].deliverAtFrame > currentFrame)
                    continue;

                output.Add(schedule[i].packet);
                schedule.RemoveAt(i);
            }

            return output.Count;
        }
    }
}
