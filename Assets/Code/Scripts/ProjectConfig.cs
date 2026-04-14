using UnityEngine;

[CreateAssetMenu(menuName = "Fighter/Project Config")]
public class ProjectConfig : ScriptableObject
{
    [Header("Input")]
    public InputBindingPolicy inputBindingPolicy;
}
