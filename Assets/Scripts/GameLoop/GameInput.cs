using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameInput : MonoBehaviour
{
    public static GameInput Instance { get; private set; }

    private InputSystem_Actions inputActions;

    // Simple queue of inputs for the next simulation ticks
    private Queue<InputFrame> inputBuffer = new Queue<InputFrame>();

    // Maximum number of frames to buffer
    [SerializeField] private int maxBufferedFrames = 5;

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
    }

    void Update()
    {
        CapturePlayer1Input();
    }

    private void CapturePlayer1Input()
    {
        float rawMoveX = inputActions.Gameplay.P1_MoveX.ReadValue<float>();
        float rawMoveY = inputActions.Gameplay.P1_MoveY.ReadValue<float>();

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

        bool light = inputActions.Gameplay.P1_LightAttack.IsPressed();
        bool medium = inputActions.Gameplay.P1_MediumAttack.IsPressed();
        bool heavy = inputActions.Gameplay.P1_HeavyAttack.IsPressed();

        // Only buffer if there’s actual input
        if (moveX != 0 || moveY != 0 || light || medium || heavy)
        {
            InputFrame frame = new InputFrame
            {
                moveX = moveX,
                moveY = moveY,
                punchLight = light,
                punchMedium = medium,
                punchHeavy = heavy
            };

            // Limit buffer size to avoid “ghost inputs”
            if (inputBuffer.Count >= maxBufferedFrames)
                inputBuffer.Dequeue();

            inputBuffer.Enqueue(frame);
        }
    }

    /// <summary>
    /// Called by Simulation.Tick() to get the next input.
    /// Returns neutral if no input is buffered.
    /// </summary>
    public InputFrame ConsumeNextInput()
    {
        if (inputBuffer.Count == 0)
            return InputFrame.Neutral;

        return inputBuffer.Dequeue();
    }
}