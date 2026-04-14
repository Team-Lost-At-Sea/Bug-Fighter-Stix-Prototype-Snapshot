using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public sealed class TrainingModeFeature : IGameLoopFeature
{
    [Header("Save State")]
    [SerializeField]
    [Min(0.1f)]
    private float clearSaveHoldDurationSeconds = 1f;

    [SerializeField]
    private Color saveStateFlashColor = Color.green;

    [SerializeField]
    private Color clearStateFlashColor = Color.red;

    [Header("Recording Playback")]
    [SerializeField]
    [Min(0.1f)]
    private float clearRecordingHoldDurationSeconds = 1f;

    [SerializeField]
    private bool enableFlashForRecordingStandby = true;

    [SerializeField]
    private Color recordingStandbyFlashColor = Color.blue;

    [SerializeField]
    private bool enableFlashForRecordingActive = true;

    [SerializeField]
    private Color recordingActiveFlashColor = Color.blue;

    [SerializeField]
    private bool enableFlashForRecordingPlaybackReady = true;

    [SerializeField]
    private Color recordingPlaybackReadyFlashColor = Color.blue;

    [SerializeField]
    private bool enableFlashForRecordingPlaybackActive = true;

    [SerializeField]
    private Color recordingPlaybackActiveFlashColor = Color.blue;

    [SerializeField]
    private bool enableFlashForRecordingReset = true;

    [SerializeField]
    private Color recordingResetFlashColor = Color.red;

    [NonSerialized]
    private bool hasSavedState;

    [NonSerialized]
    private NetState savedState;

    [NonSerialized]
    private bool saveStateButtonHeldLastFrame;

    [NonSerialized]
    private float saveStateButtonHoldTime;

    [NonSerialized]
    private bool saveStateClearedByHoldThisPress;

    [NonSerialized]
    private GameLoop.RecordingPlaybackState recordingState = GameLoop.RecordingPlaybackState.RecordingSlotAvailable;

    [NonSerialized]
    private readonly List<InputFrame> recordedPlayer2Frames = new List<InputFrame>(2048);

    [NonSerialized]
    private int playbackCursor;

    [NonSerialized]
    private bool recordingButtonHeldLastFrame;

    [NonSerialized]
    private float recordingButtonHoldTime;

    [NonSerialized]
    private bool recordingClearedByHoldThisPress;

    [NonSerialized]
    private GUIStyle playbackIndicatorStyle;

    public GameLoop.RecordingPlaybackState CurrentRecordingState => recordingState;

    public void Initialize(GameLoop gameLoop)
    {
        recordedPlayer2Frames.Clear();
        recordingState = GameLoop.RecordingPlaybackState.RecordingSlotAvailable;
        playbackCursor = 0;
    }

    public void OnUpdate(GameLoop gameLoop)
    {
        HandleRecordingInput(gameLoop);
        HandleSaveStateInput(gameLoop);
    }

    public void OnPostRenderGui(GameLoop gameLoop)
    {
        DrawRecordingIndicator();
    }

    public void OnDispose(GameLoop gameLoop)
    {
    }

    public void RewriteLocalInputs(GameLoop gameLoop, InputFrame livePlayer1, InputFrame livePlayer2, ref InputFrame finalPlayer1, ref InputFrame finalPlayer2)
    {
        switch (recordingState)
        {
            case GameLoop.RecordingPlaybackState.Standby:
                finalPlayer1 = InputFrame.Neutral;
                finalPlayer2 = livePlayer1;
                break;
            case GameLoop.RecordingPlaybackState.ActiveRecording:
                finalPlayer1 = InputFrame.Neutral;
                finalPlayer2 = livePlayer1;
                recordedPlayer2Frames.Add(finalPlayer2);
                break;
            case GameLoop.RecordingPlaybackState.PlaybackActive:
                finalPlayer1 = livePlayer1;
                if (playbackCursor < recordedPlayer2Frames.Count)
                {
                    finalPlayer2 = recordedPlayer2Frames[playbackCursor];
                    playbackCursor++;
                    if (playbackCursor >= recordedPlayer2Frames.Count)
                        StopPlaybackInternal(gameLoop);
                }
                else
                {
                    finalPlayer2 = InputFrame.Neutral;
                    StopPlaybackInternal(gameLoop);
                }
                break;
        }
    }

    private void HandleSaveStateInput(GameLoop gameLoop)
    {
        bool isHeld = IsSaveStateButtonHeld();
        if (isHeld)
        {
            saveStateButtonHoldTime += Time.unscaledDeltaTime;
            if (!saveStateClearedByHoldThisPress && saveStateButtonHoldTime >= clearSaveHoldDurationSeconds)
            {
                ClearSavedState(gameLoop);
                saveStateClearedByHoldThisPress = true;
            }
        }
        else
        {
            if (saveStateButtonHeldLastFrame && !saveStateClearedByHoldThisPress)
                ToggleSaveOrRestore(gameLoop);

            saveStateButtonHoldTime = 0f;
            saveStateClearedByHoldThisPress = false;
        }

        saveStateButtonHeldLastFrame = isHeld;
    }

    private void HandleRecordingInput(GameLoop gameLoop)
    {
        bool isHeld = GameInput.Instance != null && GameInput.Instance.IsRecordingButtonHeld();
        if (isHeld)
        {
            recordingButtonHoldTime += Time.unscaledDeltaTime;
            if (!recordingClearedByHoldThisPress && recordingButtonHoldTime >= clearRecordingHoldDurationSeconds)
            {
                ResetRecordingSystem(gameLoop);
                recordingClearedByHoldThisPress = true;
            }
        }
        else
        {
            if (recordingButtonHeldLastFrame && !recordingClearedByHoldThisPress)
                HandleRecordingButtonTap(gameLoop);

            recordingButtonHoldTime = 0f;
            recordingClearedByHoldThisPress = false;
        }

        recordingButtonHeldLastFrame = isHeld;
    }

    private static bool IsSaveStateButtonHeld()
    {
        bool keyboardHeld = Keyboard.current != null && Keyboard.current.backspaceKey.isPressed;
        bool gamepadHeld = Gamepad.current != null && Gamepad.current.selectButton.isPressed;
        return keyboardHeld || gamepadHeld;
    }

    private void ToggleSaveOrRestore(GameLoop gameLoop)
    {
        Simulation simulation = gameLoop.ActiveSimulation;
        if (simulation == null)
            return;

        if (!hasSavedState)
        {
            savedState = simulation.CaptureNetState();
            if (gameLoop.NetStateSerializer != null)
            {
                byte[] blob = gameLoop.NetStateSerializer.Serialize(savedState);
                savedState = gameLoop.NetStateSerializer.Deserialize(blob);
            }

            hasSavedState = true;
            gameLoop.TriggerScreenFlash(saveStateFlashColor);
            Debug.Log("Save state captured.");
            return;
        }

        if (recordingState == GameLoop.RecordingPlaybackState.Standby || recordingState == GameLoop.RecordingPlaybackState.ActiveRecording)
            ResetRecordingSystem(gameLoop);
        else if (recordingState == GameLoop.RecordingPlaybackState.PlaybackActive)
            StopPlaybackInternal(gameLoop);

        simulation.RestoreNetState(savedState);
        gameLoop.ResetSimulationAccumulator();
        GameInput.Instance?.ClearBufferedInputState();
        gameLoop.RenderImmediately();
        Debug.Log("Save state restored.");
    }

    private void ClearSavedState(GameLoop gameLoop)
    {
        if (!hasSavedState)
            return;

        hasSavedState = false;
        gameLoop.TriggerScreenFlash(clearStateFlashColor);
        Debug.Log("Save state cleared.");
    }

    private void HandleRecordingButtonTap(GameLoop gameLoop)
    {
        if (!gameLoop.UseLocalP2PacketStream)
        {
            Debug.LogWarning("Recording playback requires Use Local P2 Packet Stream.");
            return;
        }

        switch (recordingState)
        {
            case GameLoop.RecordingPlaybackState.RecordingSlotAvailable:
                recordingState = GameLoop.RecordingPlaybackState.Standby;
                GameInput.Instance?.ClearBufferedInputState();
                TriggerRecordingStateFlash(gameLoop, recordingState);
                Debug.Log("Recording standby: P1 input now controls P2.");
                break;
            case GameLoop.RecordingPlaybackState.Standby:
                recordedPlayer2Frames.Clear();
                recordingState = GameLoop.RecordingPlaybackState.ActiveRecording;
                TriggerRecordingStateFlash(gameLoop, recordingState);
                Debug.Log("Recording started.");
                break;
            case GameLoop.RecordingPlaybackState.ActiveRecording:
                recordingState = GameLoop.RecordingPlaybackState.PlaybackReady;
                playbackCursor = 0;
                GameInput.Instance?.ClearBufferedInputState();
                TriggerRecordingStateFlash(gameLoop, recordingState);
                Debug.Log($"Recording stopped. Frames captured: {recordedPlayer2Frames.Count}.");
                break;
            case GameLoop.RecordingPlaybackState.PlaybackReady:
                if (recordedPlayer2Frames.Count == 0)
                    return;

                recordingState = GameLoop.RecordingPlaybackState.PlaybackActive;
                playbackCursor = 0;
                GameInput.Instance?.ClearBufferedInputState();
                TriggerRecordingStateFlash(gameLoop, recordingState);
                Debug.Log("Playback started.");
                break;
        }
    }

    private void StopPlaybackInternal(GameLoop gameLoop)
    {
        if (recordingState == GameLoop.RecordingPlaybackState.PlaybackActive)
        {
            recordingState = GameLoop.RecordingPlaybackState.PlaybackReady;
            TriggerRecordingStateFlash(gameLoop, recordingState);
        }

        playbackCursor = 0;
    }

    private void ResetRecordingSystem(GameLoop gameLoop)
    {
        recordingState = GameLoop.RecordingPlaybackState.RecordingSlotAvailable;
        recordedPlayer2Frames.Clear();
        StopPlaybackInternal(gameLoop);
        GameInput.Instance?.ClearBufferedInputState();
        if (enableFlashForRecordingReset)
            gameLoop.TriggerScreenFlash(recordingResetFlashColor);
        Debug.Log("Recording reset. Slot available.");
    }

    private void TriggerRecordingStateFlash(GameLoop gameLoop, GameLoop.RecordingPlaybackState state)
    {
        switch (state)
        {
            case GameLoop.RecordingPlaybackState.Standby:
                if (enableFlashForRecordingStandby)
                    gameLoop.TriggerScreenFlash(recordingStandbyFlashColor);
                break;
            case GameLoop.RecordingPlaybackState.ActiveRecording:
                if (enableFlashForRecordingActive)
                    gameLoop.TriggerScreenFlash(recordingActiveFlashColor);
                break;
            case GameLoop.RecordingPlaybackState.PlaybackReady:
                if (enableFlashForRecordingPlaybackReady)
                    gameLoop.TriggerScreenFlash(recordingPlaybackReadyFlashColor);
                break;
            case GameLoop.RecordingPlaybackState.PlaybackActive:
                if (enableFlashForRecordingPlaybackActive)
                    gameLoop.TriggerScreenFlash(recordingPlaybackActiveFlashColor);
                break;
        }
    }

    private void DrawRecordingIndicator()
    {
        const float size = 10f;
        const float margin = 12f;
        Rect indicatorRect = new Rect(Screen.width - margin - size, margin, size, size);

        if (recordingState == GameLoop.RecordingPlaybackState.ActiveRecording)
        {
            Color previousColor = GUI.color;
            GUI.color = Color.red;
            GUI.DrawTexture(indicatorRect, Texture2D.whiteTexture);
            GUI.color = previousColor;
            return;
        }

        if (recordingState != GameLoop.RecordingPlaybackState.PlaybackActive)
            return;

        EnsurePlaybackIndicatorStyle();
        GUI.Label(
            new Rect(indicatorRect.x - 2f, indicatorRect.y - 6f, size + 8f, size + 12f),
            "▶",
            playbackIndicatorStyle
        );
    }

    private void EnsurePlaybackIndicatorStyle()
    {
        if (playbackIndicatorStyle != null)
            return;

        playbackIndicatorStyle = new GUIStyle(GUI.skin.label);
        playbackIndicatorStyle.normal.textColor = Color.green;
        playbackIndicatorStyle.fontSize = 18;
        playbackIndicatorStyle.alignment = TextAnchor.UpperLeft;
    }
}
