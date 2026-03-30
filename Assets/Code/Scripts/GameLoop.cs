using UnityEngine;
using UnityEngine.InputSystem;

public class GameLoop : MonoBehaviour
{
    public const int TICKS_PER_SECOND = 60;
    public const float FIXED_DT = 1f / TICKS_PER_SECOND;

    private float accumulator;

    [Header("Views")]
    public FighterView player1View;
    public FighterView player2View;

    [Header("Character Selection")]
    [SerializeField]
    private CharacterDefinition player1Character;

    [SerializeField]
    private CharacterDefinition player2Character;

    [Header("Debug")]
    [SerializeField]
    private int hitstopFrames = 8;
    
    [Header("Match")]
    [SerializeField]
    private MatchConfig matchConfig;

    [Header("Save State")]
    [SerializeField]
    [Min(0.1f)]
    private float clearSaveHoldDurationSeconds = 2f;

    [Header("Netcode Session")]
    [SerializeField]
    private bool useRollbackSession = true;

    [SerializeField]
    [Min(0)]
    private int rollbackInputDelayFrames = 2;

    [SerializeField]
    [Min(10)]
    private int rollbackWindowFrames = 240;

    [SerializeField]
    private bool simulateNetworkInEditor;

    [SerializeField]
    [Min(0)]
    private int simulatedBaseLatencyFrames = 2;

    [SerializeField]
    [Min(0)]
    private int simulatedJitterFrames = 1;

    [SerializeField]
    [Range(0f, 1f)]
    private float simulatedDropChance = 0f;

    [SerializeField]
    [Range(0f, 1f)]
    private float simulatedReorderChance = 0f;

    [Header("Replay")]
    [SerializeField]
    private bool recordReplayPackets = true;

    private Simulation simulation;
    private readonly MatchPresenter presenter = new MatchPresenter();
    private bool hasSavedState;
    private NetState savedState;
    private bool saveStateButtonHeldLastFrame;
    private float saveStateButtonHoldTime;
    private bool saveStateClearedByHoldThisPress;
    private IMatchSession matchSession;
    private INetStateSerializer netStateSerializer;
    private ReplayRecorder replayRecorder;
    private uint localInputSequence;

    public Simulation ActiveSimulation => simulation;
    public RollbackMetrics CurrentRollbackMetrics => matchSession != null ? matchSession.Metrics : default;

    void Start()
    {
        int configuredTicksPerSecond = matchConfig != null
            ? matchConfig.ticksPerSecond
            : TICKS_PER_SECOND;
        SimulationTime.Configure(configuredTicksPerSecond);

        simulation = new Simulation(matchConfig);
        netStateSerializer = new BinaryNetStateSerializer();
        replayRecorder = new ReplayRecorder();
        Fighter.HitstopFrames = matchConfig != null
            ? Mathf.Max(0, matchConfig.hitstopFrames)
            : Mathf.Max(0, hitstopFrames);
        ApplySelectedCharacters();

        FighterConfig player1Config = player1View != null ? player1View.Config : null;
        FighterConfig player2Config = player2View != null ? player2View.Config : null;
        if (player1Config == null || player2Config == null)
        {
            Debug.LogError(
                "GameLoop: Missing fighter config(s) on player views. Ensure character definitions are applied before match start."
            );
            simulation = null;
            return;
        }

        string player1Name = player1View != null ? player1View.name : "Player1";
        string player2Name = player2View != null ? player2View.name : "Player2";
        simulation.Initialize(player1Config, player2Config, player1Name, player2Name);
        InitializeSession();
        presenter.Initialize(simulation, player1View, player2View);
    }

    void Update()
    {
        if (simulation == null)
            return;

        HandleSaveStateInput();
        accumulator += Time.deltaTime;

        int safety = 0;
        float fixedDt = SimulationTime.FixedDt;
        while (accumulator >= fixedDt && safety < 5)
        {
            TickSimulationOnce();

            accumulator -= fixedDt;
            safety++;
        }

        // Render after simulation updates
        presenter.Render(simulation);
    }

    private void OnDisable()
    {
        presenter.Dispose();
    }

    private void ApplySelectedCharacters()
    {
        CharacterDefinition resolvedPlayer1 = player1Character;
        CharacterDefinition resolvedPlayer2 = player2Character;

        if (MatchSetup.HasSelections)
        {
            resolvedPlayer1 = MatchSetup.Player1Character;
            resolvedPlayer2 = MatchSetup.Player2Character;
        }

        if (player1View != null && resolvedPlayer1 != null)
            player1View.ApplyCharacterDefinition(resolvedPlayer1);

        if (player2View != null && resolvedPlayer2 != null)
            player2View.ApplyCharacterDefinition(resolvedPlayer2);
    }

    private void InitializeSession()
    {
        if (!useRollbackSession || simulation == null)
        {
            matchSession = null;
            return;
        }

        INetworkAdapter adapter = null;
        if (simulateNetworkInEditor)
        {
            adapter = new LocalLoopbackNetworkAdapter(
                mirrorTargetPlayerId: 2,
                baseLatencyFrames: simulatedBaseLatencyFrames,
                maxJitterFrames: simulatedJitterFrames,
                dropChance: simulatedDropChance,
                reorderChance: simulatedReorderChance
            );
        }

        matchSession = new RollbackMatchSession(
            simulation,
            localPlayerId: 1,
            inputDelayFrames: rollbackInputDelayFrames,
            rollbackWindowFrames: rollbackWindowFrames,
            networkAdapter: adapter
        );
    }

    private void TickSimulationOnce()
    {
        if (simulation == null)
            return;

        if (matchSession != null)
        {
            int nextFrame = simulation.CurrentFrame + 1;
            FrameInputPacket packet = ConsumeLocalPacket(nextFrame);
            matchSession.SubmitLocalInput(packet);
            if (recordReplayPackets)
                replayRecorder.Record(packet);

            matchSession.AdvanceFrame();
            return;
        }

        int nextFrameIndex = simulation.CurrentFrame + 1;
        FrameInput frameInput = GameInput.Instance.ConsumeNextFrameInput(nextFrameIndex);
        simulation.Tick(frameInput);
    }

    private FrameInputPacket ConsumeLocalPacket(int nextFrame)
    {
        if (GameInput.Instance == null)
            return FrameInputPacket.Neutral(nextFrame, 1, localInputSequence++);

        FrameInputPacket packet = GameInput.Instance.ConsumeNextPlayerPacket(nextFrame, 1);
        packet.sequence = localInputSequence++;
        return packet;
    }

    private void HandleSaveStateInput()
    {
        bool isHeld = IsSaveStateButtonHeld();
        if (isHeld)
        {
            saveStateButtonHoldTime += Time.unscaledDeltaTime;
            if (!saveStateClearedByHoldThisPress && saveStateButtonHoldTime >= clearSaveHoldDurationSeconds)
            {
                ClearSavedState();
                saveStateClearedByHoldThisPress = true;
            }
        }
        else
        {
            if (saveStateButtonHeldLastFrame && !saveStateClearedByHoldThisPress)
                ToggleSaveOrRestore();

            saveStateButtonHoldTime = 0f;
            saveStateClearedByHoldThisPress = false;
        }

        saveStateButtonHeldLastFrame = isHeld;
    }

    private bool IsSaveStateButtonHeld()
    {
        bool keyboardHeld = Keyboard.current != null && Keyboard.current.backspaceKey.isPressed;
        bool gamepadHeld = Gamepad.current != null && Gamepad.current.selectButton.isPressed;
        return keyboardHeld || gamepadHeld;
    }

    private void ToggleSaveOrRestore()
    {
        if (!hasSavedState)
        {
            savedState = simulation.CaptureNetState();
            if (netStateSerializer != null)
            {
                byte[] blob = netStateSerializer.Serialize(savedState);
                savedState = netStateSerializer.Deserialize(blob);
            }
            hasSavedState = true;
            Debug.Log("Save state captured.");
            return;
        }

        simulation.RestoreNetState(savedState);
        accumulator = 0f;
        GameInput.Instance?.ClearBufferedInputState();
        presenter.Render(simulation);
        Debug.Log("Save state restored.");
    }

    private void ClearSavedState()
    {
        if (!hasSavedState)
            return;

        hasSavedState = false;
        Debug.Log("Save state cleared.");
    }
}
