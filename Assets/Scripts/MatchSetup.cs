public static class MatchSetup
{
    public static CharacterDefinition Player1Character { get; private set; }
    public static CharacterDefinition Player2Character { get; private set; }

    public static bool HasSelections => Player1Character != null && Player2Character != null;

    public static void SetSelections(CharacterDefinition player1, CharacterDefinition player2)
    {
        Player1Character = player1;
        Player2Character = player2;
    }

    public static void ClearSelections()
    {
        Player1Character = null;
        Player2Character = null;
    }
}
