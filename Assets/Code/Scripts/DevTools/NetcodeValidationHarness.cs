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
        bool rollbackPass = RunRollbackMismatch(report);
        bool replayPass = RunReplayVerification(report);

        bool allPass = determinismPass && serializationPass && rollbackPass && replayPass;
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

    private bool RunRollbackMismatch(StringBuilder report)
    {
        Simulation reference = CreateSimulation("RollbackRef");
        Simulation underTest = CreateSimulation("RollbackTest");

        ScriptedRemoteAdapter adapter = new ScriptedRemoteAdapter();
        adapter.Schedule(PacketForFrame(48, 2, InputFrame.Neutral), deliverAtFrame: 48);
        InputFrame lateAttack = InputFrame.Neutral;
        lateAttack.punchLight = true;
        lateAttack.punchLightPressed = true;
        adapter.Schedule(PacketForFrame(60, 2, lateAttack), deliverAtFrame: 76);

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

            InputFrame remote = frame == 60 ? lateAttack : InputFrame.Neutral;
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
