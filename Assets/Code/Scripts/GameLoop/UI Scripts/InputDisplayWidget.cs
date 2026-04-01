using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class InputDisplayWidget : MonoBehaviour
{
    private const int JOYSTICK_STATE_COUNT = 9;

    [Header("Joystick")]
    [SerializeField]
    private Image joystickImage;

    [Tooltip("9 sprites in numpad order: 1,2,3,4,5,6,7,8,9 (5 = neutral)")]
    [SerializeField]
    private Sprite[] joystickSprites = new Sprite[JOYSTICK_STATE_COUNT];

    [Header("Joystick Trail")]
    [SerializeField]
    private RectTransform trailLayer;

    [SerializeField]
    private Sprite trailSprite;

    [SerializeField]
    private Color trailColor = new Color(1f, 0.55f, 0.1f, 0.9f);

    [SerializeField, Range(2f, 40f)]
    private float trailThickness = 12f;

    [SerializeField, Range(0.1f, 5f)]
    private float trailFadeSeconds = 1f;

    [SerializeField, Range(1, 20)]
    private int maxTrailCount = 3;

    [Tooltip("How much existing trails whiten each time a new trail is created.")]
    [SerializeField, Range(0f, 1f)]
    private float historyWhitenPerNewTrail = 0.15f;

    [Tooltip("Distance from neutral to each cardinal direction in UI pixels (X/Y).")]
    [SerializeField]
    private Vector2 numpadStep = new Vector2(28f, 28f);

    [Tooltip("Scale applied only to diagonal directions (1,3,7,9). Lower values pull corners toward center.")]
    [SerializeField, Range(0.5f, 1f)]
    private float diagonalRadiusScale = 0.9f;

    [Header("Joystick Trail Render Ordering")]
    [SerializeField]
    private bool forceJoystickTrailCanvasOverrideSorting = true;

    [SerializeField]
    private int joystickTrailSortingOrder = RenderOrder.UI.InputDisplay;

    [Header("Buttons")]
    [SerializeField]
    private ButtonSlot[] buttons = new ButtonSlot[6];

    private readonly List<TrailSegment> activeTrails = new List<TrailSegment>();
    private bool hasLastNumpadDirection;
    private int lastNumpadDirection = 5;
    private RectTransform runtimeTrailLayer;

    private void LateUpdate()
    {
        UpdateTrailFades(Time.unscaledDeltaTime);

        if (GameInput.Instance == null)
            return;

        InputFrame frame = GameInput.Instance.LatestCapturedInput;
        int numpadDirection = GetNumpadDirection(frame);
        UpdateJoystick(numpadDirection);
        UpdateJoystickTrail(numpadDirection);
        UpdateButtons(frame);
    }

    private void OnDisable()
    {
        ClearTrails();
        hasLastNumpadDirection = false;
    }

    private void UpdateJoystick(int numpadDirection)
    {
        if (joystickImage == null || joystickSprites == null || joystickSprites.Length != JOYSTICK_STATE_COUNT)
            return;

        int spriteIndex = numpadDirection - 1;

        if (spriteIndex < 0 || spriteIndex >= joystickSprites.Length)
            return;

        Sprite sprite = joystickSprites[spriteIndex];
        if (sprite != null && joystickImage.sprite != sprite)
            joystickImage.sprite = sprite;
    }

    private void UpdateJoystickTrail(int numpadDirection)
    {
        if (!hasLastNumpadDirection)
        {
            hasLastNumpadDirection = true;
            lastNumpadDirection = numpadDirection;
            return;
        }

        if (numpadDirection == lastNumpadDirection)
            return;

        SpawnTrail(lastNumpadDirection, numpadDirection);
        lastNumpadDirection = numpadDirection;
    }

    private void SpawnTrail(int fromDirection, int toDirection)
    {
        RectTransform layer = ResolveTrailLayer();
        if (layer == null || trailSprite == null || joystickImage == null)
            return;

        ApplyHistoryWhitenStep();

        EnsureLayerRendersInFront(layer);
        EnsureLayerCanvasSorting(layer);

        Vector2 startPoint = GetNumpadPointInLayer(fromDirection, layer);
        Vector2 endPoint = GetNumpadPointInLayer(toDirection, layer);
        Vector2 delta = endPoint - startPoint;
        float length = delta.magnitude;
        if (length <= 0.001f)
            return;

        Image trailImage = CreateTrailImage(layer);
        RectTransform trailRect = trailImage.rectTransform;
        trailRect.anchorMin = new Vector2(0.5f, 0.5f);
        trailRect.anchorMax = new Vector2(0.5f, 0.5f);
        trailRect.pivot = new Vector2(0.5f, 0.5f);
        trailRect.anchoredPosition = (startPoint + endPoint) * 0.5f;
        trailRect.sizeDelta = new Vector2(length, Mathf.Max(1f, trailThickness));
        trailRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

        trailImage.sprite = trailSprite;
        trailImage.color = trailColor;
        trailImage.raycastTarget = false;

        activeTrails.Add(new TrailSegment(trailImage, trailColor));
        EnforceTrailLimit();
    }

    private void EnforceTrailLimit()
    {
        int allowedTrailCount = Mathf.Max(1, maxTrailCount);
        while (activeTrails.Count > allowedTrailCount)
            RemoveTrailAt(0);
    }

    private void ApplyHistoryWhitenStep()
    {
        float step = Mathf.Max(0f, historyWhitenPerNewTrail);
        if (step <= 0f || activeTrails.Count == 0)
            return;

        for (int i = 0; i < activeTrails.Count; i++)
        {
            TrailSegment segment = activeTrails[i];
            segment.historyWhiten = Mathf.Clamp01(segment.historyWhiten + step);
            activeTrails[i] = segment;
        }
    }

    private void UpdateTrailFades(float deltaTime)
    {
        if (activeTrails.Count == 0)
            return;

        float fadeDuration = Mathf.Max(0.01f, trailFadeSeconds);
        for (int i = activeTrails.Count - 1; i >= 0; i--)
        {
            TrailSegment segment = activeTrails[i];
            if (segment.image == null)
            {
                activeTrails.RemoveAt(i);
                continue;
            }

            segment.elapsed += deltaTime;
            float t = Mathf.Clamp01(segment.elapsed / fadeDuration);
            float eased = EaseOut(t);

            float whitenAmount = Mathf.Clamp01(eased + segment.historyWhiten);
            Color fadedColor = FadeColorToWhite(segment.baseColor, whitenAmount);
            fadedColor.a = Mathf.Lerp(segment.baseColor.a, 0f, eased);

            segment.image.color = fadedColor;

            activeTrails[i] = segment;

            if (segment.elapsed >= fadeDuration)
                RemoveTrailAt(i);
        }
    }

    private static float EaseOut(float t)
    {
        float clamped = Mathf.Clamp01(t);
        return 1f - ((1f - clamped) * (1f - clamped));
    }

    private static Color FadeColorToWhite(Color source, float t)
    {
        Color.RGBToHSV(source, out float hue, out float saturation, out float value);
        float nextSaturation = Mathf.Lerp(saturation, 0f, t);
        float nextValue = Mathf.Lerp(value, 1f, t);
        Color rgb = Color.HSVToRGB(hue, nextSaturation, nextValue);
        rgb.a = source.a;
        return rgb;
    }

    private void RemoveTrailAt(int index)
    {
        if (index < 0 || index >= activeTrails.Count)
            return;

        TrailSegment segment = activeTrails[index];
        activeTrails.RemoveAt(index);
        if (segment.image != null)
            Destroy(segment.image.gameObject);
    }

    private void ClearTrails()
    {
        for (int i = activeTrails.Count - 1; i >= 0; i--)
        {
            if (activeTrails[i].image != null)
                Destroy(activeTrails[i].image.gameObject);
        }
        activeTrails.Clear();
    }

    private RectTransform ResolveTrailLayer()
    {
        if (trailLayer != null)
            return trailLayer;

        if (joystickImage == null)
            return null;

        if (runtimeTrailLayer != null)
            return runtimeTrailLayer;

        RectTransform joystickParent = joystickImage.transform.parent as RectTransform;
        if (joystickParent == null)
            return null;

        GameObject layerObject = new GameObject("JoystickTrailLayer", typeof(RectTransform));
        runtimeTrailLayer = layerObject.GetComponent<RectTransform>();
        runtimeTrailLayer.SetParent(joystickParent, false);
        runtimeTrailLayer.anchorMin = new Vector2(0f, 0f);
        runtimeTrailLayer.anchorMax = new Vector2(1f, 1f);
        runtimeTrailLayer.offsetMin = Vector2.zero;
        runtimeTrailLayer.offsetMax = Vector2.zero;
        runtimeTrailLayer.pivot = new Vector2(0.5f, 0.5f);

        int joystickSibling = joystickImage.rectTransform.GetSiblingIndex();
        runtimeTrailLayer.SetSiblingIndex(joystickSibling + 1);

        return runtimeTrailLayer;
    }

    private Image CreateTrailImage(RectTransform layer)
    {
        GameObject trailObject = new GameObject("JoystickTrail", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        trailObject.transform.SetParent(layer, false);
        trailObject.transform.SetAsLastSibling();
        return trailObject.GetComponent<Image>();
    }

    private static void EnsureLayerRendersInFront(RectTransform layer)
    {
        if (layer == null)
            return;

        layer.SetAsLastSibling();
    }

    private void EnsureLayerCanvasSorting(RectTransform layer)
    {
        if (!forceJoystickTrailCanvasOverrideSorting || layer == null)
            return;

        Canvas layerCanvas = layer.GetComponent<Canvas>();
        if (layerCanvas == null)
            layerCanvas = layer.gameObject.AddComponent<Canvas>();

        layerCanvas.overrideSorting = true;
        layerCanvas.sortingOrder = joystickTrailSortingOrder;

        if (layer.GetComponent<GraphicRaycaster>() == null)
            layer.gameObject.AddComponent<GraphicRaycaster>();
    }

    private Vector2 GetNumpadPointInLayer(int numpadDirection, RectTransform layer)
    {
        RectTransform joystickRect = joystickImage.rectTransform;
        Vector2 dir = GetNumpadUnitVector(numpadDirection);
        bool isDiagonal = Mathf.Abs(dir.x) > 0.5f && Mathf.Abs(dir.y) > 0.5f;
        float radiusScale = isDiagonal ? diagonalRadiusScale : 1f;
        Vector2 localPointInJoystick = joystickRect.rect.center + new Vector2(
            dir.x * numpadStep.x * radiusScale,
            dir.y * numpadStep.y * radiusScale
        );

        Vector3 worldPoint = joystickRect.TransformPoint(localPointInJoystick);
        Vector3 layerLocalPoint = layer.InverseTransformPoint(worldPoint);
        return new Vector2(layerLocalPoint.x, layerLocalPoint.y);
    }

    private static Vector2 GetNumpadUnitVector(int numpadDirection)
    {
        switch (numpadDirection)
        {
            case 1:
                return new Vector2(-1f, -1f);
            case 2:
                return new Vector2(0f, -1f);
            case 3:
                return new Vector2(1f, -1f);
            case 4:
                return new Vector2(-1f, 0f);
            case 5:
                return Vector2.zero;
            case 6:
                return new Vector2(1f, 0f);
            case 7:
                return new Vector2(-1f, 1f);
            case 8:
                return new Vector2(0f, 1f);
            case 9:
                return new Vector2(1f, 1f);
            default:
                return Vector2.zero;
        }
    }

    private void UpdateButtons(InputFrame frame)
    {
        if (buttons == null)
            return;

        for (int i = 0; i < buttons.Length; i++)
        {
            ButtonSlot slot = buttons[i];
            if (slot == null || slot.image == null)
                continue;

            bool isOn = IsPressed(slot.source, frame);
            Sprite target = isOn ? slot.onSprite : slot.offSprite;
            if (target != null && slot.image.sprite != target)
                slot.image.sprite = target;
        }
    }

    private static int GetNumpadDirection(InputFrame frame)
    {
        int moveX = Mathf.RoundToInt(Mathf.Clamp(frame.moveX, -1f, 1f));
        int moveY = Mathf.RoundToInt(Mathf.Clamp(frame.moveY, -1f, 1f));

        if (moveY > 0)
        {
            if (moveX < 0)
                return 7;
            if (moveX > 0)
                return 9;
            return 8;
        }

        if (moveY < 0)
        {
            if (moveX < 0)
                return 1;
            if (moveX > 0)
                return 3;
            return 2;
        }

        if (moveX < 0)
            return 4;
        if (moveX > 0)
            return 6;
        return 5;
    }

    private static bool IsPressed(InputDisplayButtonSource source, InputFrame frame)
    {
        switch (source)
        {
            case InputDisplayButtonSource.PunchLight:
                return frame.punchLight;
            case InputDisplayButtonSource.PunchMedium:
                return frame.punchMedium;
            case InputDisplayButtonSource.PunchHeavy:
                return frame.punchHeavy;
            default:
                return false;
        }
    }

    private struct TrailSegment
    {
        public Image image;
        public Color baseColor;
        public float elapsed;
        public float historyWhiten;

        public TrailSegment(Image image, Color baseColor)
        {
            this.image = image;
            this.baseColor = baseColor;
            elapsed = 0f;
            historyWhiten = 0f;
        }
    }
}

public enum InputDisplayButtonSource
{
    None = 0,
    PunchLight = 1,
    PunchMedium = 2,
    PunchHeavy = 3,
}

[System.Serializable]
public sealed class ButtonSlot
{
    public InputDisplayButtonSource source = InputDisplayButtonSource.None;
    public Image image;
    public Sprite offSprite;
    public Sprite onSprite;
}
