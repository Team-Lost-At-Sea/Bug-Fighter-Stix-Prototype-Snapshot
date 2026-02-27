using UnityEngine;

public class FighterShadow : MonoBehaviour
{
    [SerializeField]
    private Transform fighterRoot;

    [SerializeField]
    private float minScale = 0.2f;

    [SerializeField]
    private float maxHeight = 5f;

    private float baseY;

    void Awake()
    {
        baseY = transform.position.y;
    }

    void LateUpdate()
    {
        if (fighterRoot == null)
            return;

        float height = fighterRoot.position.y;

        UpdatePosition(height);
        UpdateScale(height);
    }

    private void UpdatePosition(float height)
    {
        Vector3 pos = transform.position;
        pos.y = baseY;
        transform.position = pos;
    }

    private void UpdateScale(float height)
    {
        float t = Mathf.Clamp01(height / maxHeight);
        float scale = Mathf.Lerp(1f, minScale, t);

        transform.localScale = new Vector3(scale, 1f, scale);
    }
}
