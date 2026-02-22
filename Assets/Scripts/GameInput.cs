using UnityEngine;

public class GameInput
{
    private static GameInput _instance;
    public static GameInput Instance
    {
        get
        {
            if (_instance == null)
                _instance = new GameInput();
            return _instance;
        }
    }

    private InputFrame player1Input;
    private InputFrame player2Input;

    public struct FighterInput
    {
        public int MoveX; // -1, 0, 1 : left, neutral, right
        public int MoveY; // -1, 0, 1 : down, neutral, up

        public bool Light;
        public bool Medium;
        public bool Heavy;
    }

    public void CaptureInput()
    {
        // Player 1
        player1Input.left = Input.GetKey(KeyCode.A);
        player1Input.right = Input.GetKey(KeyCode.D);
        player1Input.jump = Input.GetKey(KeyCode.Space);

        player1Input.punchLight = Input.GetKey(KeyCode.J);
        player1Input.punchMedium = Input.GetKey(KeyCode.K);
        player1Input.punchHeavy = Input.GetKey(KeyCode.L);

        // Player 2 (example bindings)
        player2Input.left = Input.GetKey(KeyCode.LeftArrow);
        player2Input.right = Input.GetKey(KeyCode.RightArrow);
        player2Input.jump = Input.GetKey(KeyCode.UpArrow);
    }

    public InputFrame GetInputForPlayer(int playerIndex)
    {
        return playerIndex == 1 ? player1Input : player2Input;
    }
}
