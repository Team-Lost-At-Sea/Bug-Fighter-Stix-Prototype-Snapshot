using UnityEngine;

public class Simulation
{
    private static Simulation _instance;
    public static Simulation Instance
    {
        get
        {
            if (_instance == null)
                _instance = new Simulation();
            return _instance;
        }
    }

    private Fighter player1;
    private Fighter player2;
    const float FIGHTER_START_POSITION_OFFSET = 10f;

    public void Initialize(FighterView p1View, FighterView p2View)
    {
        player1 = new Fighter(1, p1View, new Vector2(-FIGHTER_START_POSITION_OFFSET, 0f));
        player2 = new Fighter(2, p2View, new Vector2(FIGHTER_START_POSITION_OFFSET, 0f));
    }

    public void Tick()
    {
        player1.Tick();
        player2.Tick();
    }

    public void Render()
    {
        player1.Render();
        player2.Render();
    }
}
