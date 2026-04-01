public static class NetStateMigrator
{
    public static NetState MigrateToCurrent(NetState state)
    {
        if (state.stateVersion <= 0)
            state.stateVersion = 1;

        if (state.stateVersion == NetState.CurrentVersion)
            return state;

        if (state.stateVersion < 3)
        {
            state.player1.blockstunFramesRemaining = 0;
            state.player2.blockstunFramesRemaining = 0;
            state.player1.health = state.player1.health <= 0 ? 100 : state.player1.health;
            state.player2.health = state.player2.health <= 0 ? 100 : state.player2.health;
        }

        state.stateVersion = NetState.CurrentVersion;
        return state;
    }
}
