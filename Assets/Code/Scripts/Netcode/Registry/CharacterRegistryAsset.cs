using UnityEngine;

[CreateAssetMenu(menuName = "Fighter/Character Registry")]
public class CharacterRegistryAsset : ScriptableObject, ICharacterRegistry
{
    [SerializeField]
    private CharacterDefinition[] characters;

    public CharacterDefinition ResolveCharacter(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId) || characters == null)
            return null;

        for (int i = 0; i < characters.Length; i++)
        {
            CharacterDefinition definition = characters[i];
            if (definition == null)
                continue;

            if (definition.characterId == characterId)
                return definition;
        }

        return null;
    }
}
