using UnityEngine;

[CreateAssetMenu(menuName = "Fighter/Character Definition")]
public class CharacterDefinition : ScriptableObject
{
    public string characterId;
    public FighterConfig fighterConfig;
    public RuntimeAnimatorController animatorController;
}
