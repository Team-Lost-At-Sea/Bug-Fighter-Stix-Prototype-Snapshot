using System.Text;
using UnityEngine;

public class SimulationDeterminismSmokeHarness : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private MatchConfig matchConfig;

    [SerializeField]
    private FighterConfig player1Config;

    [SerializeField]
    private FighterConfig player2Config;

    [Header("Run Settings")]
    [SerializeField]
    [Min(1)]
    private int frameCount = 600;

    [SerializeField]
    [Min(1)]
    private int checkpointInterval = 60;

    [SerializeField]
    private bool runOnStart;

    [SerializeField]
    private bool logCheckpointHashes = true;

    private void Start()
    {
        if (runOnStart)
            RunDeterminismSmokeTest();
    }

    [ContextMenu("Run Determinism Smoke Test")]
    public void RunDeterminismSmokeTest()
    {
        if (!HasValidSetup())
            return;

        SmokeRunResult firstRun = RunSinglePass();
        SmokeRunResult secondRun = RunSinglePass();

        int mismatchFrame = FindFirstMismatchFrame(firstRun.frameHashes, secondRun.frameHashes);
        if (mismatchFrame >= 0)
        {
            int frameIndex = mismatchFrame + 1;
            Debug.LogError(
                $"[DeterminismSmoke] MISMATCH at frame {frameIndex}. " +
                $"run1={firstRun.frameHashes[mismatchFrame]} run2={secondRun.frameHashes[mismatchFrame]}",
                this
            );
            return;
        }

        StringBuilder builder = new StringBuilder();
        builder.Append($"[DeterminismSmoke] PASS ({frameCount} frames). FinalHash={firstRun.finalHash}");
        if (logCheckpointHashes)
            AppendCheckpointSummary(builder, firstRun.frameHashes);

        Debug.Log(builder.ToString(), this);
    }

    private bool HasValidSetup()
    {
        if (player1Config == null || player2Config == null)
        {
            Debug.LogError(
                "[DeterminismSmoke] Missing fighter configs. Assign both player configs before running.",
                this
            );
            return false;
        }

        if (frameCount <= 0 || checkpointInterval <= 0)
        {
            Debug.LogError("[DeterminismSmoke] frameCount and checkpointInterval must be greater than zero.", this);
            return false;
        }

        return true;
    }

    private SmokeRunResult RunSinglePass()
    {
        Simulation simulation = new Simulation(matchConfig);
        simulation.Initialize(player1Config, player2Config, "SmokeP1", "SmokeP2");

        int[] frameHashes = new int[frameCount];
        for (int frame = 0; frame < frameCount; frame++)
        {
            FrameInput frameInput = new FrameInput
            {
                frameIndex = frame + 1,
                player1 = BuildInputForFrame(frame),
                player2 = InputFrame.Neutral
            };
            simulation.Tick(frameInput);
            frameHashes[frame] = simulation.ComputeDeterminismHash();
        }

        return new SmokeRunResult(frameHashes);
    }

    private static int FindFirstMismatchFrame(int[] first, int[] second)
    {
        int count = Mathf.Min(first.Length, second.Length);
        for (int i = 0; i < count; i++)
        {
            if (first[i] != second[i])
                return i;
        }

        if (first.Length != second.Length)
            return count;

        return -1;
    }

    private void AppendCheckpointSummary(StringBuilder builder, int[] frameHashes)
    {
        builder.Append(" Checkpoints:");
        int checkpoint = checkpointInterval;
        while (checkpoint <= frameHashes.Length)
        {
            builder.Append($" [{checkpoint}f:{frameHashes[checkpoint - 1]}]");
            checkpoint += checkpointInterval;
        }
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
        else if (phase < 84)
            moveX = phase < 79 ? 1f : -1f;
        else if (phase < 95)
            moveY = -1f;
        else if (phase < 101)
        {
            moveX = 1f;
            moveY = -1f;
        }
        else if (phase < 107)
            moveX = 1f;

        bool lightDown = IsHeld(frame, 14, 16) || IsHeld(frame, 124, 126);
        bool mediumDown = IsHeld(frame, 42, 43) || IsHeld(frame, 107, 108);
        bool heavyDown = IsHeld(frame, 73, 74) || IsHeld(frame, 154, 156);

        InputFrame input = InputFrame.Neutral;
        input.moveX = moveX;
        input.moveY = moveY;
        input.punchLight = lightDown;
        input.punchMedium = mediumDown;
        input.punchHeavy = heavyDown;
        input.punchLightPressed = lightDown && !IsHeld(frame - 1, 14, 16) && !IsHeld(frame - 1, 124, 126);
        input.punchMediumPressed = mediumDown && !IsHeld(frame - 1, 42, 43) && !IsHeld(frame - 1, 107, 108);
        input.punchHeavyPressed = heavyDown && !IsHeld(frame - 1, 73, 74) && !IsHeld(frame - 1, 154, 156);
        return input;
    }

    private static bool IsHeld(int frame, int phaseStartInclusive, int phaseEndExclusive)
    {
        if (frame < 0)
            return false;

        int phase = frame % 180;
        return phase >= phaseStartInclusive && phase < phaseEndExclusive;
    }

    private readonly struct SmokeRunResult
    {
        public readonly int[] frameHashes;
        public readonly int finalHash;

        public SmokeRunResult(int[] frameHashes)
        {
            this.frameHashes = frameHashes;
            finalHash = frameHashes.Length > 0 ? frameHashes[frameHashes.Length - 1] : 0;
        }
    }
}
