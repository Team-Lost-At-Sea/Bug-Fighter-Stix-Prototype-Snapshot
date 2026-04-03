public sealed class MatchPresenter
{
    private readonly ProjectileDebugRenderer projectileRenderer = new ProjectileDebugRenderer();
    private FighterView player1View;
    private FighterView player2View;
    private Fighter lastPlayer1;
    private Fighter lastPlayer2;

    public void Initialize(Simulation simulation, FighterView player1View, FighterView player2View)
    {
        this.player1View = player1View;
        this.player2View = player2View;
        SyncFighterViewBindings(simulation);

        projectileRenderer.ClearAll();
    }

    public void Render(Simulation simulation)
    {
        if (simulation == null)
            return;

        SyncFighterViewBindings(simulation);
        projectileRenderer.Render(simulation.ActiveProjectiles);
    }

    public void Dispose()
    {
        projectileRenderer.ClearAll();
        player1View = null;
        player2View = null;
        lastPlayer1 = null;
        lastPlayer2 = null;
    }

    private void SyncFighterViewBindings(Simulation simulation)
    {
        if (simulation == null)
            return;

        Fighter currentPlayer1 = simulation.Player1;
        Fighter currentPlayer2 = simulation.Player2;
        if (player1View != null && currentPlayer1 != null && !ReferenceEquals(lastPlayer1, currentPlayer1))
        {
            player1View.Initialize(currentPlayer1);
            lastPlayer1 = currentPlayer1;
        }

        if (player2View != null && currentPlayer2 != null && !ReferenceEquals(lastPlayer2, currentPlayer2))
        {
            player2View.Initialize(currentPlayer2);
            lastPlayer2 = currentPlayer2;
        }
    }
}
