using UnityEngine;

public class FighterView : MonoBehaviour
{
    [Header("Config")]
    [SerializeField]
    private FighterConfig config;

    [SerializeField]
    private float depth = 0f;

    [Header("References")]
    [SerializeField]
    private Animator animator;

    private Fighter fighter;

    public FighterConfig Config => config;

    public void Initialize(Fighter fighter)
    {
        this.fighter = fighter;

        if (animator == null)
            animator = GetComponent<Animator>();
    }

    public void SetPosition(Vector2 simPosition)
    {
        transform.position = new Vector3(simPosition.x, simPosition.y, depth);
    }

    public void SetFacing(bool facingRight)
    {
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (facingRight ? 1 : -1);
        transform.localScale = scale;
    }

    void Update()
    {
        if (fighter == null || animator == null)
            return;

        UpdateAnimatorParameters();
        SetFacing(fighter.FacingRight);
    }

    private void UpdateAnimatorParameters()
    {
        animator.SetFloat("ForwardMovementSpeed", fighter.ForwardMovementSpeed);
        animator.SetBool("IsJumping", fighter.IsJumping);
        animator.SetBool("IsAttacking", fighter.IsAttacking);
        animator.SetInteger("AttackType", (int)fighter.CurrentAttack);
    }
}
