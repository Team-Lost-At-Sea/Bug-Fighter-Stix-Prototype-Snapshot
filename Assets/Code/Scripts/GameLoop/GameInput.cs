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

    // Simple queue of inputs for the next simulation ticks
    private Queue<InputFrame> inputBuffer = new Queue<InputFrame>();

    // Maximum number of frames to buffer
    [SerializeField]
    private int maxBufferedFrames = 5;

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

    private bool invertYEnabled;
    private float lastRawMoveY;
    private int lastQuantizedMoveY;
    private InputConsumeSource lastConsumeSource = InputConsumeSource.LiveFallback;
    private bool hasQuantizedMoveYSample;

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

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        inputActions = new InputSystem_Actions();
        inputActions.Enable();

        int defaultValue = invertYDefault ? 1 : 0;
        invertYEnabled = PlayerPrefs.GetInt(INVERT_Y_PREF_KEY, defaultValue) == 1;

        if (menuController == null)
            Debug.LogError("GameInput: MenuOptionsController not assigned; options menu will be disabled.");
    }

    void Update()
    {
        if (menuController != null)
        {
            bool openedThisFrame = menuController.Tick(inputActions, () => SetInvertYEnabled(!invertYEnabled));
            if (openedThisFrame)
                inputBuffer.Clear();

            menuController.UpdateView(invertYEnabled);

            if (menuController.IsMenuOpen)
                return;
        }

        CapturePlayer1Input();
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

    private void CapturePlayer1Input()
    {
        InputFrame frame = BuildCurrentInputFrame();

        // Only buffer if there is actual input.
        if (
            frame.moveX != 0
            || frame.moveY != 0
            || frame.punchLight
            || frame.punchMedium
            || frame.punchHeavy
        )
        {
            // Limit buffer size to avoid ghost inputs.
            if (inputBuffer.Count >= maxBufferedFrames)
                inputBuffer.Dequeue();

            inputBuffer.Enqueue(frame);
        }
    }

    /// <summary>
    /// Called by Simulation.Tick() to get the next input.
    /// Falls back to a live sample if the buffer is empty.
    /// </summary>
    public InputFrame ConsumeNextInput()
    {
        if (inputBuffer.Count > 0)
        {
            lastConsumeSource = InputConsumeSource.Buffer;
            return inputBuffer.Dequeue();
        }

        // If simulation catches up faster than Update enqueues, sample live input so held
        // directions/buttons (like crouch hold) do not flicker between active and neutral.
        lastConsumeSource = InputConsumeSource.LiveFallback;
        return BuildCurrentInputFrame();
    }

    private InputFrame BuildCurrentInputFrame()
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

        return new InputFrame
        {
            moveX = moveX,
            moveY = moveY,
            punchLight = inputActions.Gameplay.P1_LightAttack.IsPressed(),
            punchMedium = inputActions.Gameplay.P1_MediumAttack.IsPressed(),
            punchHeavy = inputActions.Gameplay.P1_HeavyAttack.IsPressed()
        };
    }

    private void OnGUI()
    {
        if (!showInputDebugOverlay)
            return;

        GUI.Label(
            new Rect(10f, 10f, 360f, 20f),
            $"InputY raw={lastRawMoveY:F3} quantized={lastQuantizedMoveY}"
        );
        GUI.Label(
            new Rect(10f, 30f, 360f, 20f),
            $"Input source={lastConsumeSource} buffered={inputBuffer.Count}"
        );
    }
}
