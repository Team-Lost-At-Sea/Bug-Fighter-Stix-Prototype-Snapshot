using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class TitleScreenController : MonoBehaviour
{
    [Header("Navigation")]
    [SerializeField]
    private float navigationRepeatDelay = 0.2f;

    [SerializeField]
    private string playSceneName = "Character Select";

    [Header("UI References")]
    [SerializeField]
    private GameObject menuRoot;

    [SerializeField]
    private TMP_Text titleLabel;

    [SerializeField]
    private TMP_Text playLabel;

    [SerializeField]
    private TMP_Text musicLabel;

    [SerializeField]
    private TMP_Text hintLabel;

    [Header("Audio")]
    [SerializeField]
    private AudioSource musicSource;

    private InputSystem_Actions inputActions;
    private int selectedIndex;
    private float nextNavigationTime;

    private void Awake()
    {
        inputActions = new InputSystem_Actions();
        inputActions.Enable();

        if (menuRoot == null)
            Debug.LogError("TitleScreenController: Menu root is not assigned.");

        ApplyScale();
        RefreshView();
        ApplyMusicState();
    }

    private void OnEnable()
    {
        VideoSettings.Changed += HandleVideoSettingsChanged;
        MusicSettings.Changed += HandleMusicSettingsChanged;
        RefreshView();
        ApplyMusicState();
    }

    private void OnDisable()
    {
        VideoSettings.Changed -= HandleVideoSettingsChanged;
        MusicSettings.Changed -= HandleMusicSettingsChanged;
        inputActions?.Disable();
    }

    private void OnDestroy()
    {
        inputActions?.Dispose();
    }

    private void Update()
    {
        HandleNavigation();
        HandleSubmit();
    }

    private void HandleNavigation()
    {
        if (Time.unscaledTime < nextNavigationTime)
            return;

        Vector2 nav = inputActions.UI.Navigate.ReadValue<Vector2>();
        if (Mathf.Abs(nav.y) <= 0.5f)
            return;

        if (nav.y > 0.5f)
            selectedIndex = Mathf.Max(0, selectedIndex - 1);
        else if (nav.y < -0.5f)
            selectedIndex = Mathf.Min(1, selectedIndex + 1);

        nextNavigationTime = Time.unscaledTime + navigationRepeatDelay;
        RefreshView();
    }

    private void HandleSubmit()
    {
        if (!inputActions.UI.Submit.WasPressedThisFrame())
            return;

        if (selectedIndex == 0)
        {
            if (string.IsNullOrWhiteSpace(playSceneName))
            {
                Debug.LogWarning("TitleScreenController: Play scene name is not configured.");
                return;
            }

            SceneManager.LoadScene(playSceneName);
            return;
        }

        MusicSettings.ToggleMusicEnabled();
        RefreshView();
    }

    private void RefreshView()
    {
        if (menuRoot != null)
            menuRoot.SetActive(true);

        if (titleLabel != null)
            titleLabel.text = "Dirtcrawlers";

        if (playLabel != null)
            playLabel.text = $"{GetPrefix(0)}Play";

        if (musicLabel != null)
            musicLabel.text = $"{GetPrefix(1)}Toggle Music: {(MusicSettings.MusicEnabled ? "On" : "Off")}";

        if (hintLabel != null)
            hintLabel.text = selectedIndex == 0
                ? "Enter Training mode."
                : "Toggle Music on/off.";
    }

    private string GetPrefix(int index)
    {
        return selectedIndex == index ? "> " : "  ";
    }

    private void ApplyScale()
    {
        if (menuRoot == null)
            return;

        float scale = Mathf.Max(0.25f, VideoSettings.UIScale);
        menuRoot.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private void ApplyMusicState()
    {
        if (musicSource == null)
            return;

        if (MusicSettings.MusicEnabled)
        {
            if (!musicSource.isPlaying && musicSource.clip != null)
                musicSource.Play();
        }
        else if (musicSource.isPlaying)
        {
            musicSource.Stop();
        }
    }

    private void HandleVideoSettingsChanged()
    {
        ApplyScale();
    }

    private void HandleMusicSettingsChanged()
    {
        ApplyMusicState();
        RefreshView();
    }
}
