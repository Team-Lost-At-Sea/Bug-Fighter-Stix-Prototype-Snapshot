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

        ResolvePushboxes(); // Prevents fighters from overlapping

        ClampToStage(player1); // Keeps fighters within stage bounds
        ClampToStage(player2);
    }

    public void Render()
    {
        player1.Render();
        player2.Render();
    }

    private void ResolvePushboxes()
    {
        float p1Left = player1.position.x - player1.PushboxHalfWidth;
        float p1Right = player1.position.x + player1.PushboxHalfWidth;

        float p2Left = player2.position.x - player2.PushboxHalfWidth;
        float p2Right = player2.position.x + player2.PushboxHalfWidth;

        float overlap = Mathf.Min(p1Right, p2Right) - Mathf.Max(p1Left, p2Left);

        if (overlap > 0f)
        {
            float separation = overlap * 0.5f;

            if (player1.position.x < player2.position.x)
            {
                player1.position.x -= separation;
                player2.position.x += separation;
            }
            else
            {
                player1.position.x += separation;
                player2.position.x -= separation;
            }
        }
    }

    private float stageLeft = -80f;
    private float stageRight = 80f;

    private void ClampToStage(Fighter fighter)
    {
        float half = fighter.PushboxHalfWidth;

        if (fighter.position.x - half < stageLeft)
            fighter.position.x = stageLeft + half;

        if (fighter.position.x + half > stageRight)
            fighter.position.x = stageRight - half;
    }
}
