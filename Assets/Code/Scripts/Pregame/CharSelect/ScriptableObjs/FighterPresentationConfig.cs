using UnityEngine;

[CreateAssetMenu(menuName = "Fighter/Fighter Presentation Config")]
public class FighterPresentationConfig : ScriptableObject
{
    [Header("Shadow")]
    [Min(0.01f)]
    public float shadowMaxScale = 1f;

    [Min(0.01f)]
    public float shadowMinScale = 0.2f;

    [Min(0.01f)]
    public float shadowMaxHeight = 5f;

    [Header("Visual Variants")]
    [Tooltip("Optional skin/palette list for character select integration.")]
    public VisualVariant[] variants;

    private void OnValidate()
    {
        shadowMaxScale = Mathf.Max(0.01f, shadowMaxScale);
        shadowMinScale = Mathf.Clamp(shadowMinScale, 0.01f, shadowMaxScale);
        shadowMaxHeight = Mathf.Max(0.01f, shadowMaxHeight);
    }
}

[System.Serializable]
public struct VisualVariant
{
    public string id;
    public Material materialOverride;
    public Color tint;
}
