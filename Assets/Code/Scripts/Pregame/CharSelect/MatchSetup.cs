using UnityEngine;

public static class MatchSetup
{
    public static CharacterDefinition Player1Character { get; private set; }
    public static CharacterDefinition Player2Character { get; private set; }
    public static AudioClip BattleMusic { get; private set; }

    public static bool HasSelections => Player1Character != null && Player2Character != null;

    public static void SetSelections(CharacterDefinition player1, CharacterDefinition player2, AudioClip battleMusic)
    {
        Player1Character = player1;
        Player2Character = player2;
        BattleMusic = battleMusic;
    }

    public static void ClearSelections()
    {
        Player1Character = null;
        Player2Character = null;
        BattleMusic = null;
    }
}
