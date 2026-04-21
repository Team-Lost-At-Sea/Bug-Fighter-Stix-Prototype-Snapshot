using System;
using UnityEngine;

public class GameLoop : MonoBehaviour
{
    public const int TICKS_PER_SECOND = 60;
    public const float FIXED_DT = 1f / TICKS_PER_SECOND;

    public enum RecordingPlaybackState
    {
        RecordingSlotAvailable,
        Standby,
        ActiveRecording,
        PlaybackReady,
        PlaybackActive
    }

    private float accumulator;

    [Header("Views")]
    public FighterView player1View;
    public FighterView player2View;

    [SerializeField]
    private MatchHudController matchHud;

    [Header("Character Selection")]
    [SerializeField]
    private CharacterDefinition player1Character;

    [SerializeField]
    private CharacterDefinition player2Character;

    [Header("Debug")]
    [SerializeField]
    private int hitstopFrames = 8;

    [SerializeField]
    private bool showCombatDebugHud = true;
    
    [Header("Match")]
    [SerializeField]
    private MatchConfig matchConfig;

    [Header("Audio")]
    [SerializeField]
    private AudioSource battleMusicSource;

    [Tooltip("Used when entering the match scene without character select setup.")]
    [SerializeField]
    private AudioClip fallbackBattleMusic;

    [SerializeField]
    [Min(0.01f)]
    private float saveStateFlashDurationSeconds = 0.12f;

    [SerializeField]
    [Range(0f, 1f)]
    private float saveStateFlashMaxAlpha = 0.6f;

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

    [SerializeField]
    private bool useLocalP2PacketStream = true;

    [Header("Online")]
    [SerializeField]
    private OnlineConnectionMode onlineConnectionMode = OnlineConnectionMode.Offline;

    [SerializeField]
    [Min(1)]
    private int onlineSessionId = 1;

    [SerializeField]
    private bool onlineIsHost = true;

    [SerializeField]
    private int onlineLocalPlayerId = 1;

    [SerializeField]
    private string onlineRemoteAddress = "127.0.0.1";

    [SerializeField]
    [Range(1, 65535)]
    private int onlineLocalPort = 7777;

    [SerializeField]
    [Range(1, 65535)]
    private int onlineRemotePort = 7778;

    [SerializeField]
    private bool onlineDisableTrainingTools = true;

    [SerializeField]
    private bool onlineShowNetDebugHud = true;

    [Header("Replay")]
    [SerializeField]
    private bool recordReplayPackets = true;

    [SerializeField]
    private TrainingModeFeature trainingModeFeature = new TrainingModeFeature();

    private Simulation simulation;
    private readonly MatchPresenter presenter = new MatchPresenter();
    private IMatchSession matchSession;
    private INetworkAdapter activeNetworkAdapter;
    private INetStateSerializer netStateSerializer;
    private ReplayRecorder replayRecorder;
    private uint localInputSequence;
    private float flashTimeRemaining;
    private Color activeFlashColor;

    public Simulation ActiveSimulation => simulation;
    public RollbackMetrics CurrentRollbackMetrics => matchSession != null ? matchSession.Metrics : default;
    public RecordingPlaybackState CurrentRecordingState => trainingModeFeature != null
        ? trainingModeFeature.CurrentRecordingState
        : RecordingPlaybackState.RecordingSlotAvailable;
    public INetStateSerializer NetStateSerializer => netStateSerializer;
    public bool UseLocalP2PacketStream => !ShouldDisableTrainingToolsForOnline() && useLocalP2PacketStream;

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
        ApplyBattleMusic();

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
        if (!ShouldDisableTrainingToolsForOnline())
            trainingModeFeature?.Initialize(this);
        MusicSettings.Changed += HandleMusicSettingsChanged;
        if (matchHud == null)
            matchHud = FindFirstObjectByType<MatchHudController>();
        if (matchHud == null)
            matchHud = new GameObject("MatchHud").AddComponent<MatchHudController>();
        matchHud.Initialize(matchConfig);
    }

    void Update()
    {
        if (simulation == null)
            return;

        if (!ShouldDisableTrainingToolsForOnline())
            trainingModeFeature?.OnUpdate(this);
        if (flashTimeRemaining > 0f)
            flashTimeRemaining = Mathf.Max(0f, flashTimeRemaining - Time.unscaledDeltaTime);
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
        matchHud?.Render(simulation);
    }

    private void OnDisable()
    {
        trainingModeFeature?.OnDispose(this);
        DisposeActiveNetworkAdapter();
        MusicSettings.Changed -= HandleMusicSettingsChanged;
        presenter.Dispose();
    }

    private void OnGUI()
    {
        if (flashTimeRemaining <= 0f)
        {
            DrawCombatDebugHud();
            if (!ShouldDisableTrainingToolsForOnline())
                trainingModeFeature?.OnPostRenderGui(this);
            return;
        }

        float normalized = saveStateFlashDurationSeconds > 0f
            ? Mathf.Clamp01(flashTimeRemaining / saveStateFlashDurationSeconds)
            : 0f;
        float alpha = saveStateFlashMaxAlpha * normalized;
        if (alpha > 0f)
        {
            Color previousColor = GUI.color;
            GUI.color = new Color(activeFlashColor.r, activeFlashColor.g, activeFlashColor.b, alpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        DrawCombatDebugHud();
        if (!ShouldDisableTrainingToolsForOnline())
            trainingModeFeature?.OnPostRenderGui(this);
    }

    private void DrawCombatDebugHud()
    {
        if (!showCombatDebugHud || simulation == null)
            return;

        Fighter p1 = simulation.Player1;
        Fighter p2 = simulation.Player2;
        if (p1 == null || p2 == null)
            return;

        Simulation.CombatInteractionSnapshot interaction = simulation.LatestCombatInteraction;
        Rect rect = new Rect(8f, 8f, 560f, 124f);
        GUI.Box(rect, GUIContent.none);

        string text =
            $"Frame: {simulation.CurrentFrame}  Last: {interaction.resultType} ({interaction.hitLevel})\n" +
            $"A{interaction.attackerPlayerId} -> D{interaction.defenderPlayerId}  stun={interaction.stunFrames} chip={interaction.chipDamage} push={interaction.pushback:F2} adv={interaction.attackerAdvantageEstimate}\n" +
            $"P1 HP={p1.Health}  State={p1.CurrentState}  Hitstun={p1.HitstunFramesRemaining}  Blockstun={p1.BlockstunFramesRemaining}  Last={p1.LastReceivedHitResult}\n" +
            $"P2 HP={p2.Health}  State={p2.CurrentState}  Hitstun={p2.HitstunFramesRemaining}  Blockstun={p2.BlockstunFramesRemaining}  Last={p2.LastReceivedHitResult}";

        GUI.Label(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f), text);
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

    private void ApplyBattleMusic()
    {
        if (battleMusicSource == null)
            return;

        AudioClip selectedClip = MatchSetup.BattleMusic != null ? MatchSetup.BattleMusic : fallbackBattleMusic;
        if (selectedClip == null || !MusicSettings.MusicEnabled)
        {
            if (battleMusicSource.isPlaying)
                battleMusicSource.Stop();
            return;
        }

        if (battleMusicSource.clip != selectedClip)
            battleMusicSource.clip = selectedClip;

        if (!battleMusicSource.isPlaying)
            battleMusicSource.Play();
    }

    private void HandleMusicSettingsChanged()
    {
        ApplyBattleMusic();
    }

    private void InitializeSession()
    {
        DisposeActiveNetworkAdapter();

        if (!useRollbackSession || simulation == null)
        {
            matchSession = null;
            return;
        }

        INetworkAdapter adapter = null;
        int localPlayerId = GetActiveLocalPlayerId();
        if (onlineConnectionMode == OnlineConnectionMode.DirectUdp)
        {
            try
            {
                adapter = new UdpNetworkAdapter(
                    GetOnlineSessionId(),
                    localPlayerId,
                    onlineRemoteAddress,
                    onlineLocalPort,
                    onlineRemotePort
                );
                activeNetworkAdapter = adapter;
                Debug.Log(
                    $"GameLoop: Direct UDP online session started as P{localPlayerId}. Local port {onlineLocalPort}, remote {onlineRemoteAddress}:{onlineRemotePort}, session {GetOnlineSessionId()}."
                );
            }
            catch (Exception exception)
            {
                Debug.LogError($"GameLoop: Failed to start Direct UDP online session. {exception.Message}", this);
            }
        }
        else if (onlineConnectionMode == OnlineConnectionMode.LocalLoopback || simulateNetworkInEditor)
        {
            adapter = new LocalLoopbackNetworkAdapter(
                mirrorTargetPlayerId: localPlayerId == 1 ? 2 : 1,
                baseLatencyFrames: simulatedBaseLatencyFrames,
                maxJitterFrames: simulatedJitterFrames,
                dropChance: simulatedDropChance,
                reorderChance: simulatedReorderChance
            );
        }
        else if (onlineConnectionMode == OnlineConnectionMode.Relay)
        {
            Debug.LogWarning("GameLoop: Relay online mode is not implemented yet. Starting rollback without a network adapter.");
        }

        matchSession = new RollbackMatchSession(
            simulation,
            localPlayerId: localPlayerId,
            inputDelayFrames: rollbackInputDelayFrames,
            rollbackWindowFrames: rollbackWindowFrames,
            networkAdapter: adapter
        );
    }

    private void TickSimulationOnce()
    {
        if (simulation == null)
            return;

        if (UseLocalP2PacketStream)
        {
            int nextFrame = simulation.CurrentFrame + 1;
            BuildLocalPacketsForFrame(nextFrame, out FrameInputPacket player1Packet, out FrameInputPacket player2Packet);
            simulation.Tick(player1Packet);
            simulation.Tick(player2Packet);

            if (recordReplayPackets)
            {
                replayRecorder.Record(player1Packet);
                replayRecorder.Record(player2Packet);
            }

            return;
        }

        if (matchSession != null)
        {
            int nextFrame = simulation.CurrentFrame + 1;
            FrameInputPacket packet = ConsumeLocalPacket(nextFrame, GetActiveLocalPlayerId());
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

    private void BuildLocalPacketsForFrame(int nextFrame, out FrameInputPacket player1Packet, out FrameInputPacket player2Packet)
    {
        if (GameInput.Instance == null)
        {
            player1Packet = FrameInputPacket.Neutral(nextFrame, 1, localInputSequence++);
            player2Packet = FrameInputPacket.Neutral(nextFrame, 2, localInputSequence++);
            return;
        }

        InputFrame livePlayer1 = GameInput.Instance.ConsumeNextPlayerInputFrame(1);
        InputFrame livePlayer2 = GameInput.Instance.ConsumeNextPlayerInputFrame(2);
        InputFrame finalPlayer1 = livePlayer1;
        InputFrame finalPlayer2 = livePlayer2;
        if (!ShouldDisableTrainingToolsForOnline())
            trainingModeFeature?.RewriteLocalInputs(this, livePlayer1, livePlayer2, ref finalPlayer1, ref finalPlayer2);

        player1Packet = GameInput.Instance.EncodeInputFrameToPacket(finalPlayer1, nextFrame, 1, localInputSequence++);
        player2Packet = GameInput.Instance.EncodeInputFrameToPacket(finalPlayer2, nextFrame, 2, localInputSequence++);
    }

    private FrameInputPacket ConsumeLocalPacket(int nextFrame, int playerId)
    {
        if (GameInput.Instance == null)
            return FrameInputPacket.Neutral(nextFrame, playerId, localInputSequence++);

        FrameInputPacket packet = GameInput.Instance.ConsumeNextPlayerPacket(nextFrame, playerId);
        packet.sequence = localInputSequence++;
        return packet;
    }

    public void TriggerScreenFlash(Color color)
    {
        activeFlashColor = color;
        flashTimeRemaining = Mathf.Max(0.01f, saveStateFlashDurationSeconds);
    }

    public void ResetSimulationAccumulator()
    {
        accumulator = 0f;
    }

    public void RenderImmediately()
    {
        presenter.Render(simulation);
        matchHud?.Render(simulation);
    }

    public OnlineMatchConfig BuildOnlineMatchConfig()
    {
        int localPlayerId = GetOnlineLocalPlayerId();
        return new OnlineMatchConfig
        {
            connectionMode = onlineConnectionMode,
            sessionId = GetOnlineSessionId(),
            isHost = onlineIsHost,
            localPlayerId = localPlayerId,
            remotePlayerId = localPlayerId == 1 ? 2 : 1,
            remoteAddress = onlineRemoteAddress,
            localPort = (ushort)Mathf.Clamp(onlineLocalPort, 1, 65535),
            remotePort = (ushort)Mathf.Clamp(onlineRemotePort, 1, 65535),
            inputDelayFrames = rollbackInputDelayFrames,
            rollbackWindowFrames = rollbackWindowFrames,
            allowTrainingTools = !onlineDisableTrainingTools,
            allowLocalP2Input = !onlineDisableTrainingTools && useLocalP2PacketStream,
            allowDebugStateMutation = !onlineDisableTrainingTools,
            showNetDebugHud = onlineShowNetDebugHud
        };
    }

    private int GetActiveLocalPlayerId()
    {
        return IsOnlineConnectionModeActive() ? GetOnlineLocalPlayerId() : 1;
    }

    private int GetOnlineLocalPlayerId()
    {
        return onlineLocalPlayerId == 2 ? 2 : 1;
    }

    private uint GetOnlineSessionId()
    {
        return (uint)Mathf.Max(1, onlineSessionId);
    }

    private bool IsOnlineConnectionModeActive()
    {
        return onlineConnectionMode == OnlineConnectionMode.LocalLoopback
            || onlineConnectionMode == OnlineConnectionMode.DirectUdp
            || onlineConnectionMode == OnlineConnectionMode.Relay;
    }

    private bool ShouldDisableTrainingToolsForOnline()
    {
        return onlineDisableTrainingTools && IsOnlineConnectionModeActive();
    }

    private void DisposeActiveNetworkAdapter()
    {
        if (activeNetworkAdapter is IDisposable disposable)
            disposable.Dispose();

        activeNetworkAdapter = null;
    }
}
