using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameInput : MonoBehaviour
{
    public enum InputConsumeSource
    {
        Buffer,
        LiveFallback
    }

    public static GameInput Instance { get; private set; }

    private const string INVERT_Y_PREF_KEY = "input.invert_y";

    private InputSystem_Actions inputActions;
    private InputAction recordingAction;

    // Simple queue of inputs for the next simulation ticks
    private Queue<InputFrame> inputBuffer = new Queue<InputFrame>();
    private Queue<InputFrame> player2InputBuffer = new Queue<InputFrame>();

    // Maximum number of frames to buffer
    [SerializeField]
    private int maxBufferedFrames = 20;

    [Header("Debug")]
    [SerializeField]
    private bool verboseCrouchDebug;

    [SerializeField]
    private bool showInputDebugOverlay;

    [Header("Invert Y")]
    [SerializeField]
    private bool invertYDefault;

    [Header("Menu")]
    [SerializeField]
    private MenuOptionsController menuController;

    [Header("Local P2")]
    [SerializeField]
    private bool enableLocalPlayer2Input = true;

    [SerializeField]
    private Key p2LeftKey = Key.LeftArrow;

    [SerializeField]
    private Key p2RightKey = Key.RightArrow;

    [SerializeField]
    private Key p2DownKey = Key.DownArrow;

    [SerializeField]
    private Key p2UpKey = Key.UpArrow;

    [SerializeField]
    private Key p2LightKey = Key.Numpad1;

    [SerializeField]
    private Key p2MediumKey = Key.Numpad2;

    [SerializeField]
    private Key p2HeavyKey = Key.Numpad3;
    
    [Header("References")]
    [SerializeField]
    private GameLoop gameLoop;

    private bool invertYEnabled;
    private float lastRawMoveY;
    private int lastQuantizedMoveY;
    private InputFrame lastCapturedFrame = InputFrame.Neutral;
    private InputConsumeSource lastConsumeSource = InputConsumeSource.LiveFallback;
    private bool hasQuantizedMoveYSample;
    private bool hasAppliedInputMode;
    private MenuOptionsController.InputMode lastInputMode;
    private bool lastLightPressed;
    private bool lastMediumPressed;
    private bool lastHeavyPressed;
    private bool lastP2LightPressed;
    private bool lastP2MediumPressed;
    private bool lastP2HeavyPressed;
    private GUIStyle debugOverlayStyle;
    private InputFrame lastEnqueuedFrame = InputFrame.Neutral;
    private InputFrame lastEnqueuedPlayer2Frame = InputFrame.Neutral;
    private bool hasLastEnqueuedFrame;
    private bool hasLastEnqueuedPlayer2Frame;
    private InputFrame lastCapturedPlayer2Frame = InputFrame.Neutral;

    public bool InvertYEnabled => invertYEnabled;
    public bool IsMenuOpen => menuController != null && menuController.IsMenuOpen;
    public bool AllowMenuOpening
    {
        get => menuController != null && menuController.AllowMenuOpening;
        set
        {
            if (menuController != null)
                menuController.AllowMenuOpening = value;
        }
    }
    public bool VerboseCrouchDebug => verboseCrouchDebug;
    public float LastRawMoveY => lastRawMoveY;
    public int LastQuantizedMoveY => lastQuantizedMoveY;
    public InputConsumeSource LastConsumeSource => lastConsumeSource;
    public InputFrame LatestCapturedInput => lastCapturedFrame;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (gameLoop == null)
            gameLoop = FindFirstObjectByType<GameLoop>();

        inputActions = new InputSystem_Actions();
        inputActions.Enable();
        recordingAction = inputActions.asset.FindAction("Gameplay/P1_Recording", throwIfNotFound: false);
        if (recordingAction == null)
            Debug.LogWarning("GameInput: Gameplay/P1_Recording action not found. Recording controls will be unavailable.");

        int defaultValue = invertYDefault ? 1 : 0;
        invertYEnabled = PlayerPrefs.GetInt(INVERT_Y_PREF_KEY, defaultValue) == 1;

        if (menuController == null)
            Debug.LogError("GameInput: MenuOptionsController not assigned; options menu will be disabled.");

        ApplyInputMode();
    }

    void Update()
    {
        ApplyInputMode();

        if (menuController != null)
        {
            bool openedThisFrame = menuController.Tick(inputActions, () => SetInvertYEnabled(!invertYEnabled));
            if (openedThisFrame)
            {
                ClearBufferedInputState();
            }

            menuController.UpdateView(invertYEnabled);

            if (menuController.IsMenuOpen)
            {
                lastCapturedFrame = InputFrame.Neutral;
                lastCapturedPlayer2Frame = InputFrame.Neutral;
                return;
            }
        }

        CapturePlayerInputs();
    }

    public void ClearBufferedInputState()
    {
        inputBuffer.Clear();
        player2InputBuffer.Clear();
        lastCapturedFrame = InputFrame.Neutral;
        lastCapturedPlayer2Frame = InputFrame.Neutral;
        hasLastEnqueuedFrame = false;
        hasLastEnqueuedPlayer2Frame = false;
        lastLightPressed = false;
        lastMediumPressed = false;
        lastHeavyPressed = false;
        lastP2LightPressed = false;
        lastP2MediumPressed = false;
        lastP2HeavyPressed = false;
    }

    public void SetInvertYEnabled(bool enabled)
    {
        if (invertYEnabled == enabled)
            return;

        invertYEnabled = enabled;
        PlayerPrefs.SetInt(INVERT_Y_PREF_KEY, invertYEnabled ? 1 : 0);
        PlayerPrefs.Save();
        Debug.Log($"Invert Y: {(invertYEnabled ? "ON" : "OFF")}");
    }

    private void CapturePlayerInputs()
    {
        InputFrame player1Frame = BuildCurrentPlayer1InputFrame();
        lastCapturedFrame = player1Frame;

        if (ShouldEnqueueFrame(player1Frame))
        {
            // Limit buffer size to avoid ghost inputs.
            if (inputBuffer.Count >= maxBufferedFrames)
                inputBuffer.Dequeue();

            inputBuffer.Enqueue(player1Frame);
            lastEnqueuedFrame = player1Frame;
            hasLastEnqueuedFrame = true;
        }

        InputFrame player2Frame = enableLocalPlayer2Input
            ? BuildCurrentPlayer2InputFrame()
            : InputFrame.Neutral;
        lastCapturedPlayer2Frame = player2Frame;

        if (!enableLocalPlayer2Input)
            return;

        if (ShouldEnqueuePlayer2Frame(player2Frame))
        {
            if (player2InputBuffer.Count >= maxBufferedFrames)
                player2InputBuffer.Dequeue();

            player2InputBuffer.Enqueue(player2Frame);
            lastEnqueuedPlayer2Frame = player2Frame;
            hasLastEnqueuedPlayer2Frame = true;
        }
    }

    public FrameInput ConsumeNextFrameInput(int frameIndex)
    {
        return new FrameInput
        {
            frameIndex = frameIndex,
            player1 = ConsumeNextPlayer1Input(),
            player2 = enableLocalPlayer2Input ? ConsumeNextPlayer2Input() : InputFrame.Neutral
        };
    }

    public FrameInputPacket ConsumeNextPlayerPacket(int frameIndex, int playerId = 1)
    {
        InputFrame input = ConsumeNextPlayerInputFrame(playerId);
        return EncodeInputFrameToPacket(input, frameIndex, playerId, 0);
    }

    public InputFrame ConsumeNextPlayerInputFrame(int playerId)
    {
        if (playerId == 2)
            return enableLocalPlayer2Input ? ConsumeNextPlayer2Input() : InputFrame.Neutral;

        return ConsumeNextPlayer1Input();
    }

    public FrameInputPacket EncodeInputFrameToPacket(InputFrame input, int frameIndex, int playerId, uint sequence = 0)
    {
        return InputPacketCodec.Encode(input, frameIndex, playerId, sequence);
    }

    public bool IsRecordingButtonHeld()
    {
        return recordingAction != null && recordingAction.IsPressed();
    }

    /// <summary>
    /// Called by Simulation.Tick() to get the next P1 input.
    /// Falls back to the most recently captured frame if the buffer is empty.
    /// </summary>
    public InputFrame ConsumeNextInput()
    {
        return ConsumeNextPlayer1Input();
    }

    private InputFrame ConsumeNextPlayer1Input()
    {
        if (inputBuffer.Count > 0)
        {
            lastConsumeSource = InputConsumeSource.Buffer;
            return inputBuffer.Dequeue();
        }

        // If simulation catches up faster than Update enqueues, reuse the latest captured
        // frame instead of re-sampling live input mid-frame. This keeps input timing
        // consistent across catch-up ticks.
        lastConsumeSource = InputConsumeSource.LiveFallback;
        return lastCapturedFrame;
    }

    private InputFrame ConsumeNextPlayer2Input()
    {
        if (!enableLocalPlayer2Input)
            return InputFrame.Neutral;

        if (player2InputBuffer.Count > 0)
            return player2InputBuffer.Dequeue();

        return lastCapturedPlayer2Frame;
    }

    private InputFrame BuildCurrentPlayer1InputFrame()
    {
        if (menuController != null && menuController.IsMenuOpen)
        {
            lastRawMoveY = 0f;
            lastQuantizedMoveY = 0;
            hasQuantizedMoveYSample = true;
            return InputFrame.Neutral;
        }

        float rawMoveX = inputActions.Gameplay.P1_MoveX.ReadValue<float>();
        float rawMoveY = inputActions.Gameplay.P1_MoveY.ReadValue<float>();

        if (invertYEnabled)
            rawMoveY = -rawMoveY;

        int moveX = 0;
        int moveY = 0;
        bool lightPressed = inputActions.Gameplay.P1_LightAttack.IsPressed();
        bool mediumPressed = inputActions.Gameplay.P1_MediumAttack.IsPressed();
        bool heavyPressed = inputActions.Gameplay.P1_HeavyAttack.IsPressed();

        if (rawMoveX > 0.5f)
            moveX = 1;
        else if (rawMoveX < -0.5f)
            moveX = -1;

        if (rawMoveY > 0.5f)
            moveY = 1;
        else if (rawMoveY < -0.5f)
            moveY = -1;

        lastRawMoveY = rawMoveY;
        if (verboseCrouchDebug && (!hasQuantizedMoveYSample || moveY != lastQuantizedMoveY))
        {
            Debug.Log($"[InputY] raw={rawMoveY:F3} quantized={moveY}");
        }

        lastQuantizedMoveY = moveY;
        hasQuantizedMoveYSample = true;

        InputFrame frame = new InputFrame
        {
            moveX = moveX,
            moveY = moveY,
            punchLight = lightPressed,
            punchMedium = mediumPressed,
            punchHeavy = heavyPressed,
            punchLightPressed = lightPressed && !lastLightPressed,
            punchMediumPressed = mediumPressed && !lastMediumPressed,
            punchHeavyPressed = heavyPressed && !lastHeavyPressed
        };

        lastLightPressed = lightPressed;
        lastMediumPressed = mediumPressed;
        lastHeavyPressed = heavyPressed;
        return frame;
    }

    private InputFrame BuildCurrentPlayer2InputFrame()
    {
        if (menuController != null && menuController.IsMenuOpen)
            return InputFrame.Neutral;

        int moveX = 0;
        int moveY = 0;
        bool lightPressed = false;
        bool mediumPressed = false;
        bool heavyPressed = false;

        Gamepad secondaryPad = GetSecondaryGamepad();
        if (secondaryPad != null)
        {
            float padX = secondaryPad.leftStick.x.ReadValue();
            float padY = secondaryPad.leftStick.y.ReadValue();

            if (padX > 0.5f)
                moveX = 1;
            else if (padX < -0.5f)
                moveX = -1;

            if (padY > 0.5f)
                moveY = 1;
            else if (padY < -0.5f)
                moveY = -1;

            lightPressed = secondaryPad.buttonSouth.isPressed;
            mediumPressed = secondaryPad.buttonEast.isPressed;
            heavyPressed = secondaryPad.buttonNorth.isPressed;
        }
        else if (Keyboard.current != null)
        {
            if (Keyboard.current[p2LeftKey].isPressed)
                moveX = -1;
            else if (Keyboard.current[p2RightKey].isPressed)
                moveX = 1;

            if (Keyboard.current[p2UpKey].isPressed)
                moveY = 1;
            else if (Keyboard.current[p2DownKey].isPressed)
                moveY = -1;

            lightPressed = Keyboard.current[p2LightKey].isPressed;
            mediumPressed = Keyboard.current[p2MediumKey].isPressed;
            heavyPressed = Keyboard.current[p2HeavyKey].isPressed;
        }

        InputFrame frame = new InputFrame
        {
            moveX = moveX,
            moveY = moveY,
            punchLight = lightPressed,
            punchMedium = mediumPressed,
            punchHeavy = heavyPressed,
            punchLightPressed = lightPressed && !lastP2LightPressed,
            punchMediumPressed = mediumPressed && !lastP2MediumPressed,
            punchHeavyPressed = heavyPressed && !lastP2HeavyPressed
        };

        lastP2LightPressed = lightPressed;
        lastP2MediumPressed = mediumPressed;
        lastP2HeavyPressed = heavyPressed;
        return frame;
    }

    private void ApplyInputMode()
    {
        if (menuController == null || inputActions == null)
            return;

        MenuOptionsController.InputMode mode = menuController.CurrentInputMode;
        if (hasAppliedInputMode && mode == lastInputMode)
            return;

        switch (mode)
        {
            case MenuOptionsController.InputMode.Gamepad:
                inputActions.bindingMask = null;
                inputActions.Gameplay.Get().bindingMask = InputBinding.MaskByGroup("Gamepad");
                break;
            case MenuOptionsController.InputMode.Keyboard:
                inputActions.bindingMask = null;
                inputActions.Gameplay.Get().bindingMask = InputBinding.MaskByGroup("Keyboard&Mouse");
                break;
            default:
                inputActions.bindingMask = null;
                inputActions.Gameplay.Get().bindingMask = null;
                break;
        }

        lastInputMode = mode;
        hasAppliedInputMode = true;
    }

    private void OnGUI()
    {
        if (!showInputDebugOverlay)
            return;

        EnsureDebugOverlayStyle();

        GUI.Label(
            new Rect(10f, 10f, 360f, 20f),
            $"InputY raw={lastRawMoveY:F3} quantized={lastQuantizedMoveY}",
            debugOverlayStyle
        );
        GUI.Label(
            new Rect(10f, 30f, 360f, 20f),
            $"Input source={lastConsumeSource} buffered={inputBuffer.Count}",
            debugOverlayStyle
        );
        GUI.Label(
            new Rect(10f, 50f, 420f, 20f),
            $"Press edges LP={lastCapturedFrame.punchLightPressed} MP={lastCapturedFrame.punchMediumPressed} HP={lastCapturedFrame.punchHeavyPressed}",
            debugOverlayStyle
        );
        GUI.Label(
            new Rect(10f, 70f, 900f, 20f),
            $"P1 history oldest->newest: {GetPlayer1InputHistoryDebugString()}",
            debugOverlayStyle
        );
    }

    private void EnsureDebugOverlayStyle()
    {
        if (debugOverlayStyle != null)
            return;

        debugOverlayStyle = new GUIStyle(GUI.skin.label);
        debugOverlayStyle.richText = true;
        debugOverlayStyle.normal.textColor = Color.white;
    }

    private bool ShouldEnqueueFrame(InputFrame frame)
    {
        bool hasAnyInput =
            frame.moveX != 0
            || frame.moveY != 0
            || frame.punchLight
            || frame.punchMedium
            || frame.punchHeavy;

        if (!hasAnyInput)
            return false;

        if (frame.HasAttackPress)
            return true;

        if (!hasLastEnqueuedFrame)
            return true;

        return frame.moveX != lastEnqueuedFrame.moveX
            || frame.moveY != lastEnqueuedFrame.moveY
            || frame.punchLight != lastEnqueuedFrame.punchLight
            || frame.punchMedium != lastEnqueuedFrame.punchMedium
            || frame.punchHeavy != lastEnqueuedFrame.punchHeavy;
    }

    private bool ShouldEnqueuePlayer2Frame(InputFrame frame)
    {
        bool hasAnyInput =
            frame.moveX != 0
            || frame.moveY != 0
            || frame.punchLight
            || frame.punchMedium
            || frame.punchHeavy;

        if (!hasAnyInput)
            return false;

        if (frame.HasAttackPress)
            return true;

        if (!hasLastEnqueuedPlayer2Frame)
            return true;

        return frame.moveX != lastEnqueuedPlayer2Frame.moveX
            || frame.moveY != lastEnqueuedPlayer2Frame.moveY
            || frame.punchLight != lastEnqueuedPlayer2Frame.punchLight
            || frame.punchMedium != lastEnqueuedPlayer2Frame.punchMedium
            || frame.punchHeavy != lastEnqueuedPlayer2Frame.punchHeavy;
    }

    private static Gamepad GetSecondaryGamepad()
    {
        if (Gamepad.all.Count < 2)
            return null;

        return Gamepad.all[1];
    }

    private string GetPlayer1InputHistoryDebugString()
    {
        if (gameLoop == null)
            gameLoop = FindFirstObjectByType<GameLoop>();

        if (gameLoop == null || gameLoop.ActiveSimulation == null)
            return "Simulation not available";

        return gameLoop.ActiveSimulation.GetPlayer1InputHistoryDebugString();
    }
}
