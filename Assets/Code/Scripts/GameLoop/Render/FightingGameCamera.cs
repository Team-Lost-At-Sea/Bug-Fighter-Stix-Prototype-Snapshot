using UnityEngine;

public class FightingGameCamera : MonoBehaviour
{
    [SerializeField]
    private Transform fighter1;

    [SerializeField]
    private Transform fighter2;

    [SerializeField]
    private StageBounds stage;

    [SerializeField]
    private float smoothSpeed = 5f;

    [SerializeField]
    private float yOffset = 2f;
    
    [Header("Zoom")]
    [SerializeField]
    [Range(1f, 40f)]
    private float minOrthographicSize = 4.5f;

    [SerializeField]
    [Range(1f, 40f)]
    private float maxOrthographicSize = 8f;

    [SerializeField]
    [Range(0f, 80f)]
    private float zoomInStopDistance = 4f;

    [SerializeField]
    [Range(0f, 80f)]
    private float zoomOutStopDistance = 22f;

    [SerializeField]
    [Min(0f)]
    private float zoomSmoothSpeed = 6f;

    private Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (fighter1 == null || fighter2 == null)
            return;

        UpdateZoom();

        // 1. Midpoint between fighters
        float midX = (fighter1.position.x + fighter2.position.x) * 0.5f;

        Vector3 targetPos = transform.position;
        targetPos.x = midX;
        targetPos.y = yOffset;

        // 2. Clamp inside stage
        if (stage != null)
        {
            float halfWidth = GetCameraHalfWidth();
            float minX = stage.leftLimit + halfWidth;
            float maxX = stage.rightLimit - halfWidth;

            if (minX <= maxX)
                targetPos.x = Mathf.Clamp(targetPos.x, minX, maxX);
            else
                targetPos.x = (stage.leftLimit + stage.rightLimit) * 0.5f;
        }

        // 3. Smooth move
        transform.position = Vector3.Lerp(
            transform.position,
            targetPos,
            smoothSpeed * Time.deltaTime
        );
    }

    private void UpdateZoom()
    {
        if (cam == null || !cam.orthographic)
            return;

        float fighterDistance = Mathf.Abs(fighter2.position.x - fighter1.position.x);
        float t = Mathf.InverseLerp(zoomInStopDistance, zoomOutStopDistance, fighterDistance);
        float targetSize = Mathf.Lerp(minOrthographicSize, maxOrthographicSize, t);

        if (zoomSmoothSpeed <= 0f)
        {
            cam.orthographicSize = targetSize;
            return;
        }

        cam.orthographicSize = Mathf.Lerp(
            cam.orthographicSize,
            targetSize,
            zoomSmoothSpeed * Time.deltaTime
        );
    }

    float GetCameraHalfWidth()
    {
        if (cam.orthographic)
        {
            return cam.orthographicSize * cam.aspect;
        }
        else
        {
            float distance = Mathf.Abs(transform.position.z);
            float height = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return (height * cam.aspect) * 0.5f;
        }
    }

    private void OnValidate()
    {
        minOrthographicSize = Mathf.Max(1f, minOrthographicSize);
        maxOrthographicSize = Mathf.Max(minOrthographicSize, maxOrthographicSize);
        zoomInStopDistance = Mathf.Max(0f, zoomInStopDistance);
        zoomOutStopDistance = Mathf.Max(zoomInStopDistance + 0.01f, zoomOutStopDistance);
        zoomSmoothSpeed = Mathf.Max(0f, zoomSmoothSpeed);
    }
}
