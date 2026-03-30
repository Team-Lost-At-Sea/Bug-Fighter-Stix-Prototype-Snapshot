public static class NetStateMigrator
{
    public static NetState MigrateToCurrent(NetState state)
    {
        if (state.stateVersion <= 0)
            state.stateVersion = 1;

        if (state.stateVersion == NetState.CurrentVersion)
            return state;

        // Future schema migration hooks should branch by stateVersion here.
        state.stateVersion = NetState.CurrentVersion;
        return state;
    }
}
