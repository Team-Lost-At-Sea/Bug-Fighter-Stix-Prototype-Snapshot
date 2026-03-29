using UnityEngine;

public class FightingGameCamera : MonoBehaviour
{
    private enum ZoomMode
    {
        AutoByProjection,
        OrthographicSize,
        Perspective
    }

    private enum PerspectiveZoomStyle
    {
        DollyDistance,
        FieldOfView
    }

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

    [Header("X Clamp")]
    [SerializeField]
    [Tooltip("Raw additive offset applied to the resolved left clamp X. Negative = laxer, positive = tighter.")]
    private float leftClampXOffset = 0f;

    [SerializeField]
    [Tooltip("Raw additive offset applied to the resolved right clamp X. Positive = laxer, negative = tighter.")]
    private float rightClampXOffset = 0f;

    [Header("Vertical Follow")]
    [SerializeField]
    private bool followJumpHeight = true;

    [SerializeField]
    [Range(0f, 1f)]
    private float jumpFollowStrength = 0.35f;

    [SerializeField]
    [Min(0f)]
    private float maxJumpFollowOffset = 2.5f;
    
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

    [Header("Zoom Mode")]
    [SerializeField]
    private ZoomMode zoomMode = ZoomMode.AutoByProjection;

    [SerializeField]
    private PerspectiveZoomStyle perspectiveZoomStyle = PerspectiveZoomStyle.DollyDistance;

    [Header("Perspective Zoom")]
    [SerializeField]
    [Min(0.01f)]
    private float perspectiveMinDistance = 9f;

    [SerializeField]
    [Min(0.01f)]
    private float perspectiveMaxDistance = 16f;

    [SerializeField]
    [Range(1f, 179f)]
    private float perspectiveMinFov = 42f;

    [SerializeField]
    [Range(1f, 179f)]
    private float perspectiveMaxFov = 60f;

    [SerializeField]
    [Min(0f)]
    private float perspectiveZoomSmoothSpeed = 6f;
    
    [Header("Debug")]
    [SerializeField]
    [Tooltip("Runtime-only reference for tuning zoom zones.")]
    private float currentFighterDistance;

    [SerializeField]
    [Tooltip("Runtime-only: resolved left X clamp after stage + offset math.")]
    private float currentResolvedMinClampX;

    [SerializeField]
    [Tooltip("Runtime-only: resolved right X clamp after stage + offset math.")]
    private float currentResolvedMaxClampX;

    private Camera cam;
    private float cameraZSign = -1f;

    void Awake()
    {
        cam = GetComponent<Camera>();
        cameraZSign = transform.position.z >= 0f ? 1f : -1f;
        if (Mathf.Approximately(transform.position.z, 0f))
            cameraZSign = -1f;
    }

    void LateUpdate()
    {
        if (fighter1 == null || fighter2 == null)
            return;

        currentFighterDistance = Mathf.Abs(fighter2.position.x - fighter1.position.x);
        UpdateZoom();

        // 1. Midpoint between fighters
        float midX = (fighter1.position.x + fighter2.position.x) * 0.5f;

        Vector3 targetPos = transform.position;
        targetPos.x = midX;
        targetPos.y = yOffset + GetJumpFollowOffset();

        // 2. Clamp inside stage
        if (stage != null)
        {
            float halfWidth = GetCameraHalfWidth();
            float clampLeft = stage.leftLimit;
            float clampRight = stage.rightLimit;
            float minX = (clampLeft + halfWidth) + leftClampXOffset;
            float maxX = (clampRight - halfWidth) + rightClampXOffset;
            currentResolvedMinClampX = minX;
            currentResolvedMaxClampX = maxX;

            if (minX <= maxX)
                targetPos.x = Mathf.Clamp(targetPos.x, minX, maxX);
            else
                targetPos.x = (clampLeft + clampRight) * 0.5f;
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
        if (cam == null)
            return;

        float t = Mathf.InverseLerp(zoomInStopDistance, zoomOutStopDistance, currentFighterDistance);
        bool usePerspectiveZoom = ShouldUsePerspectiveZoom();
        if (!usePerspectiveZoom)
        {
            if (!cam.orthographic)
                return;

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
            return;
        }

        if (cam.orthographic)
            return;

        if (perspectiveZoomStyle == PerspectiveZoomStyle.FieldOfView)
        {
            float targetFov = Mathf.Lerp(perspectiveMinFov, perspectiveMaxFov, t);
            if (perspectiveZoomSmoothSpeed <= 0f)
            {
                cam.fieldOfView = targetFov;
                return;
            }

            cam.fieldOfView = Mathf.Lerp(
                cam.fieldOfView,
                targetFov,
                perspectiveZoomSmoothSpeed * Time.deltaTime
            );
            return;
        }

        float targetDistance = Mathf.Lerp(perspectiveMinDistance, perspectiveMaxDistance, t);
        float currentDistance = Mathf.Abs(transform.position.z);
        float nextDistance = perspectiveZoomSmoothSpeed <= 0f
            ? targetDistance
            : Mathf.Lerp(currentDistance, targetDistance, perspectiveZoomSmoothSpeed * Time.deltaTime);

        Vector3 pos = transform.position;
        pos.z = cameraZSign * nextDistance;
        transform.position = pos;
    }

    private bool ShouldUsePerspectiveZoom()
    {
        switch (zoomMode)
        {
            case ZoomMode.Perspective:
                return true;
            case ZoomMode.OrthographicSize:
                return false;
            default:
                return !cam.orthographic;
        }
    }

    private float GetJumpFollowOffset()
    {
        if (!followJumpHeight)
            return 0f;

        float highestFighterY = Mathf.Max(fighter1.position.y, fighter2.position.y);
        if (highestFighterY <= 0f)
            return 0f;

        float offset = highestFighterY * jumpFollowStrength;
        return Mathf.Clamp(offset, 0f, maxJumpFollowOffset);
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
        perspectiveMinDistance = Mathf.Max(0.01f, perspectiveMinDistance);
        perspectiveMaxDistance = Mathf.Max(perspectiveMinDistance, perspectiveMaxDistance);
        perspectiveMinFov = Mathf.Clamp(perspectiveMinFov, 1f, 179f);
        perspectiveMaxFov = Mathf.Clamp(perspectiveMaxFov, perspectiveMinFov, 179f);
        perspectiveZoomSmoothSpeed = Mathf.Max(0f, perspectiveZoomSmoothSpeed);
        jumpFollowStrength = Mathf.Clamp01(jumpFollowStrength);
        maxJumpFollowOffset = Mathf.Max(0f, maxJumpFollowOffset);
    }
}
