using UnityEngine;

[CreateAssetMenu(menuName = "Fighter/Fighter Config")]
public class FighterConfig : ScriptableObject
{
    [Header("Movement")]
    public float moveSpeed = 6f;
    public float jumpForce = 12f;
    public float jumpHorizontalBoostScale = 2f;
    public float gravity = -30f;

    [Header("Advanced")]
    public float maxFallSpeed = -25f;
    public float groundFriction = 20f;
}
