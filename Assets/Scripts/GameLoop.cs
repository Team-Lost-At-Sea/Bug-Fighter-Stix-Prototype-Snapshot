using UnityEngine;

public class GameLoop : MonoBehaviour
{
    public const int TICKS_PER_SECOND = 60;
    public const float FIXED_DT = 1f / TICKS_PER_SECOND;

    private float accumulator;

    public FighterView player1View;
    public FighterView player2View;

    void Start()
    {
        Simulation.Instance.Initialize(player1View, player2View);
    }

    void Update()
    {
        accumulator += Time.deltaTime;

        int safety = 0;
        while (accumulator >= FIXED_DT && safety < 5)
        {
            // Tick the simulation with the next buffered input
            InputFrame p1Input = GameInput.Instance.ConsumeNextInput();

            Simulation.Instance.Tick(p1Input);

            accumulator -= FIXED_DT;
            safety++;
        }

        // Render after simulation updates
        Simulation.Instance.Render();
    }
}
