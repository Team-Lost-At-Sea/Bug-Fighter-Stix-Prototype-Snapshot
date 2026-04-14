using System;
using UnityEngine;

public static class MusicSettings
{
    private const string MUSIC_ENABLED_PREF_KEY = "audio.music_enabled";

    private static bool initialized;

    public static event Action Changed;

    public static bool MusicEnabled
    {
        get
        {
            EnsureInitialized();
            return musicEnabled;
        }
        private set => musicEnabled = value;
    }

    private static bool musicEnabled = true;

    public static void SetMusicEnabled(bool enabled)
    {
        EnsureInitialized();
        if (musicEnabled == enabled)
            return;

        musicEnabled = enabled;
        PlayerPrefs.SetInt(MUSIC_ENABLED_PREF_KEY, musicEnabled ? 1 : 0);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static void ToggleMusicEnabled()
    {
        SetMusicEnabled(!MusicEnabled);
    }

    private static void EnsureInitialized()
    {
        if (initialized)
            return;

        initialized = true;
        musicEnabled = PlayerPrefs.GetInt(MUSIC_ENABLED_PREF_KEY, 1) == 1;
    }
}
