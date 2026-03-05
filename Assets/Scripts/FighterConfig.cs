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

    [Header("Pushbox")]
    public float pushboxHalfWidth = 0.5f;

    [Header("Hurtbox")]
    public Vector2 hurtboxHalfSize = new Vector2(0.5f, 1.0f);

    [Header("Attacks")]
    public AttackData groundedLightAttackData;
    public AttackData groundedMediumAttackData;
    public AttackData groundedHeavyAttackData;

    [Header("Crouch Attacks")]
    public AttackData crouchingLightAttackData;
    public AttackData crouchingMediumAttackData;
    public AttackData crouchingHeavyAttackData;

    [Header("Air Attacks")]
    public AttackData jumpingLightAttackData;
    public AttackData jumpingMediumAttackData;
    public AttackData jumpingHeavyAttackData;

}
