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

    private Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (fighter1 == null || fighter2 == null)
            return;

        // 1. Midpoint between fighters
        float midX = (fighter1.position.x + fighter2.position.x) * 0.5f;

        Vector3 targetPos = transform.position;
        targetPos.x = midX;
        targetPos.y = yOffset;

        // 2. Clamp inside stage
        float halfWidth = GetCameraHalfWidth();

        float minX = stage.leftLimit + halfWidth;
        float maxX = stage.rightLimit - halfWidth;

        targetPos.x = Mathf.Clamp(targetPos.x, minX, maxX);

        // 3. Smooth move
        transform.position = Vector3.Lerp(
            transform.position,
            targetPos,
            smoothSpeed * Time.deltaTime
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
}
