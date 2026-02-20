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

        player1.SetOpponent(player2);
        player2.SetOpponent(player1);
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
        // Compute horizontal overlap
        float distance = Mathf.Abs(player2.Position.x - player1.Position.x);
        float minDistance = player1.PushboxHalfWidth + player2.PushboxHalfWidth;

        if (distance < minDistance)
        {
            float overlap = minDistance - distance;
            float separation = overlap * 0.5f;

            if (player1.Position.x < player2.Position.x)
            {
                player1.MoveHorizontal(-separation);
                player2.MoveHorizontal(separation);
            }
            else
            {
                player1.MoveHorizontal(separation);
                player2.MoveHorizontal(-separation);
            }
        }
    }

    private float stageLeft = -80f;
    private float stageRight = 80f;

    private void ClampToStage(Fighter fighter)
    {
        float half = fighter.PushboxHalfWidth;

        if (fighter.Position.x - half < stageLeft)
            fighter.SetHorizontal(stageLeft + half);

        if (fighter.Position.x + half > stageRight)
            fighter.SetHorizontal(stageRight - half);
    }
}
