using UnityEngine;

public class FighterView : MonoBehaviour
{
    [SerializeField] private FighterConfig config;
    [SerializeField] private float depth = 0f;

    public FighterConfig Config => config;

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
}
