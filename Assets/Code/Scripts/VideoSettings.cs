using System;
using UnityEngine;

public static class VideoSettings
{
    private const float DEFAULT_REFERENCE_HEIGHT = 1080f;

    public static event Action Changed;

    public static int ResolutionWidth { get; private set; } = Screen.width;
    public static int ResolutionHeight { get; private set; } = Screen.height;
    public static bool Fullscreen { get; private set; } = Screen.fullScreen;

    public static float ReferenceHeight { get; private set; } = DEFAULT_REFERENCE_HEIGHT;

    public static void SetResolution(int width, int height, bool fullscreen)
    {
        if (width <= 0 || height <= 0)
        {
            Debug.LogWarning($"VideoSettings.SetResolution ignored invalid size {width}x{height}.");
            return;
        }

        bool changed = ResolutionWidth != width || ResolutionHeight != height || Fullscreen != fullscreen;
        ResolutionWidth = width;
        ResolutionHeight = height;
        Fullscreen = fullscreen;

        if (changed)
            Changed?.Invoke();
    }

    public static void SetReferenceHeight(float height)
    {
        if (height <= 0f)
        {
            Debug.LogWarning($"VideoSettings.SetReferenceHeight ignored invalid value {height}.");
            return;
        }

        if (Mathf.Approximately(ReferenceHeight, height))
            return;

        ReferenceHeight = height;
        Changed?.Invoke();
    }

    public static float GetEffectiveReferenceHeight()
    {
        return ReferenceHeight > 0f ? ReferenceHeight : DEFAULT_REFERENCE_HEIGHT;
    }
}
