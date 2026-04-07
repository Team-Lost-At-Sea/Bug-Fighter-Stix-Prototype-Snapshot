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
            state.player1.health = state.player1.health <= 0 ? 1000 : state.player1.health;
            state.player2.health = state.player2.health <= 0 ? 1000 : state.player2.health;
        }

        if (state.stateVersion < 4)
        {
            state.roundPhase = (int)RoundPhase.Fighting;
            state.lastRoundResult = (int)RoundResult.None;
            state.lastRoundEndType = (int)RoundEndType.None;
            state.matchWinner = (int)MatchWinner.None;
            state.player1RoundWins = 0;
            state.player2RoundWins = 0;
            state.roundNumber = 1;
            state.phaseFramesRemaining = 0;
            state.player1.isDefeated = state.player1.health <= 0;
            state.player2.isDefeated = state.player2.health <= 0;
        }

        state.stateVersion = NetState.CurrentVersion;
        return state;
    }
}
