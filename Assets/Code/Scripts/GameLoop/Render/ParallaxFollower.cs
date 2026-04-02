using UnityEngine;

[DisallowMultipleComponent]
public class ParallaxFollower : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    [Tooltip("Camera transform to follow. If empty, tries Camera.main.")]
    private Transform cameraTransform;
    private FightingGameCamera fightingGameCamera;

    [Header("Parallax")]
    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("0 = stays in world space, 1 = fully follows camera on X.")]
    private float followStrengthX = 0.5f;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("0 = stays in world space, 1 = fully follows camera on Y.")]
    private float followStrengthY = 0f;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("0 = stays in world space, 1 = fully follows camera on Z.")]
    private float followStrengthZ = 0f;

    private Vector3 initialObjectPosition;
    private Vector3 initialCameraPosition;
    private bool initialized;

    private void Awake()
    {
        TryResolveCamera();
        CaptureInitialState();
    }

    private void OnEnable()
    {
        if (!initialized)
            CaptureInitialState();
    }

    private void LateUpdate()
    {
        if (!TryResolveCamera())
            return;

        if (!initialized)
            CaptureInitialState();

        Vector3 cameraDelta = GetResolvedCameraPosition() - initialCameraPosition;
        Vector3 targetPosition = initialObjectPosition;
        targetPosition.x += cameraDelta.x * Mathf.Clamp01(followStrengthX);
        targetPosition.y += cameraDelta.y * Mathf.Clamp01(followStrengthY);
        targetPosition.z += cameraDelta.z * Mathf.Clamp01(followStrengthZ);

        transform.position = targetPosition;
    }

    [ContextMenu("Re-capture Initial State")]
    public void RecaptureInitialState()
    {
        CaptureInitialState();
    }

    private bool TryResolveCamera()
    {
        if (cameraTransform != null)
        {
            if (fightingGameCamera == null)
                fightingGameCamera = cameraTransform.GetComponent<FightingGameCamera>();
            return true;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return false;

        cameraTransform = mainCamera.transform;
        fightingGameCamera = cameraTransform.GetComponent<FightingGameCamera>();
        return true;
    }

    private void CaptureInitialState()
    {
        initialObjectPosition = transform.position;
        initialCameraPosition = GetResolvedCameraPosition();
        initialized = true;
    }

    private Vector3 GetResolvedCameraPosition()
    {
        if (fightingGameCamera != null)
            return fightingGameCamera.ResolvedPosition;

        return cameraTransform != null ? cameraTransform.position : Vector3.zero;
    }

    private void OnValidate()
    {
        followStrengthX = Mathf.Clamp01(followStrengthX);
        followStrengthY = Mathf.Clamp01(followStrengthY);
        followStrengthZ = Mathf.Clamp01(followStrengthZ);
    }
}
