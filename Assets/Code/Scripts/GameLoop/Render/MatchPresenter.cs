public sealed class MatchPresenter
{
    private readonly ProjectileDebugRenderer projectileRenderer = new ProjectileDebugRenderer();

    public void Initialize(Simulation simulation, FighterView player1View, FighterView player2View)
    {
        if (simulation != null)
        {
            if (player1View != null && simulation.Player1 != null)
                player1View.Initialize(simulation.Player1);

            if (player2View != null && simulation.Player2 != null)
                player2View.Initialize(simulation.Player2);
        }

        projectileRenderer.ClearAll();
    }

    public void Render(Simulation simulation)
    {
        if (simulation == null)
            return;

        projectileRenderer.Render(simulation.ActiveProjectiles);
    }

    public void Dispose()
    {
        projectileRenderer.ClearAll();
    }
}
