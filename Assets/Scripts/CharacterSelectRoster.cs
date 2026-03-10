using UnityEngine;

public class CharacterSelectRoster : MonoBehaviour
{
    [SerializeField]
    private CharacterDefinition[] characters;

    public int CharacterCount => characters != null ? characters.Length : 0;

    public CharacterDefinition[] Characters => characters;

    public CharacterDefinition GetCharacter(int index)
    {
        if (characters == null || index < 0 || index >= characters.Length)
            return null;

        return characters[index];
    }
}
