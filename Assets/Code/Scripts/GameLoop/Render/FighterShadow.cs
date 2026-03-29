using UnityEngine;

public class FighterShadow : MonoBehaviour
{
    [SerializeField]
    private Transform fighterRoot;

    [SerializeField]
    [Min(0.01f)]
    private float maxScale = 1f;

    [SerializeField]
    [Min(0.01f)]
    private float minScale = 0.2f;

    [SerializeField]
    [Min(0.01f)]
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
        float scale = Mathf.Lerp(maxScale, minScale, t);

        transform.localScale = new Vector3(scale, 1f, scale);
    }

    public void ApplyPresentationConfig(FighterPresentationConfig presentationConfig)
    {
        if (presentationConfig == null)
            return;

        maxScale = Mathf.Max(0.01f, presentationConfig.shadowMaxScale);
        minScale = Mathf.Clamp(presentationConfig.shadowMinScale, 0.01f, maxScale);
        maxHeight = Mathf.Max(0.01f, presentationConfig.shadowMaxHeight);
    }

    private void OnValidate()
    {
        maxScale = Mathf.Max(0.01f, maxScale);
        minScale = Mathf.Clamp(minScale, 0.01f, maxScale);
        maxHeight = Mathf.Max(0.01f, maxHeight);
    }
}
