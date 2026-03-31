using System;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class MenuOptionsController : MonoBehaviour
{
    public enum InputMode
    {
        Auto,
        Gamepad,
        Keyboard
    }

    [Header("Menu")]
    [SerializeField]
    private bool allowMenuOpening = true;

    [SerializeField]
    private float navigationRepeatDelay = 0.2f;

    [Header("UI References")]
    [SerializeField]
    private GameObject menuRoot;

    [SerializeField]
    private TMP_Text optionLabel;

    [SerializeField]
    private TMP_Text uiScaleLabel;

    [SerializeField]
    private TMP_Text inputModeLabel;

    [SerializeField]
    private TMP_Text hitboxViewLabel;

    [SerializeField]
    private TMP_Text returnToCharSelectLabel;

    [Header("Hints")]
    [SerializeField]
    private TMP_Text toggleHintLabel;

    [Header("Context Hint Popup")]
    [SerializeField]
    private GameObject contextHintRoot;

    [SerializeField]
    private TMP_Text contextHintLabel;

    [SerializeField]
    private TMP_Text closeHintLabel;

    [Header("Scene Navigation")]
    [SerializeField]
    private string characterSelectSceneName = "Character Select";

    private bool menuOpen;
    private int selectedIndex;
    private float nextNavigationTime;
    private InputMode inputMode = InputMode.Auto;
    private bool showHitboxView = false;

    public bool IsMenuOpen => menuOpen;
    public bool AllowMenuOpening
    {
        get => allowMenuOpening;
        set => allowMenuOpening = value;
    }

    public InputMode CurrentInputMode => inputMode;

    private void Awake()
    {
        if (menuRoot == null)
            Debug.LogError("MenuOptionsController: Menu Root is not assigned; options menu UI will not be visible.");

        // Always start in Auto each session.
        inputMode = InputMode.Auto;
        showHitboxView = false;
        FighterView.GlobalShowBoxes = showHitboxView;

        ApplyScale();
        UpdateMenuVisibility();
    }

    private void OnEnable()
    {
        VideoSettings.Changed += HandleVideoSettingsChanged;
    }

    private void OnDisable()
    {
        VideoSettings.Changed -= HandleVideoSettingsChanged;
    }

    public bool Tick(InputSystem_Actions actions, Action onToggleInvertY)
    {
        if (!allowMenuOpening)
        {
            if (menuOpen)
                SetMenuOpen(false);

            return false;
        }

        bool openedThisFrame = false;
        bool menuPressed =
            actions.UI.Menu.WasPressedThisFrame()
            || actions.Gameplay.P1_Menu.WasPressedThisFrame();

        if (menuPressed)
        {
            SetMenuOpen(!menuOpen);
            if (menuOpen)
                openedThisFrame = true;
        }

        if (menuOpen)
        {
            HandleNavigation(actions);

            if (actions.UI.Submit.WasPressedThisFrame())
            {
                if (selectedIndex == 0)
                    onToggleInvertY?.Invoke();
                else if (selectedIndex == 3)
                    ToggleHitboxView();
                else if (selectedIndex == 4)
                    ReturnToCharacterSelect();
            }
        }

        return openedThisFrame;
    }

    public void UpdateView(bool invertYEnabled)
    {
        selectedIndex = Mathf.Clamp(selectedIndex, 0, GetMaxSelectableIndex());
        UpdateMenuVisibility();
        string optionState = invertYEnabled ? "On" : "Off";
        if (optionLabel != null)
            optionLabel.text = $"{GetPrefix(0)}Invert Y axis: {optionState}";
        if (uiScaleLabel != null)
            uiScaleLabel.text = $"{GetPrefix(1)}UI Scale: {VideoSettings.UIScale:0.##}x";
        if (inputModeLabel != null)
            inputModeLabel.text = $"{GetPrefix(2)}Input Mode: {GetInputModeLabel(inputMode)}";
        if (hitboxViewLabel != null)
            hitboxViewLabel.text = $"{GetPrefix(3)}Hitbox View: {(showHitboxView ? "On" : "Off")}";
        if (returnToCharSelectLabel != null)
            returnToCharSelectLabel.text = $"{GetPrefix(4)}Return to Character Select";
        if (toggleHintLabel != null)
        {
            string hint = selectedIndex == 4
                ? "Press Submit to return to character select"
                : "Press Submit to toggle - Left/Right to adjust";
            toggleHintLabel.text = hint;
        }
        if (closeHintLabel != null)
            closeHintLabel.text = "Press Start to close";

        UpdateContextHint();
    }

    private void SetMenuOpen(bool open)
    {
        if (menuOpen == open)
            return;

        menuOpen = open;
        if (menuOpen)
        {
            selectedIndex = 0;
            nextNavigationTime = 0f;
        }
        UpdateMenuVisibility();
    }

    private void HandleNavigation(InputSystem_Actions actions)
    {
        if (Time.unscaledTime < nextNavigationTime)
            return;

        Vector2 nav = actions.UI.Navigate.ReadValue<Vector2>();
        if (nav.sqrMagnitude < 0.25f)
            return;

        if (Mathf.Abs(nav.y) > Mathf.Abs(nav.x))
        {
            if (nav.y > 0.5f)
                selectedIndex = Mathf.Max(0, selectedIndex - 1);
            else if (nav.y < -0.5f)
                selectedIndex = Mathf.Min(GetMaxSelectableIndex(), selectedIndex + 1);
        }
        else
        {
            if (nav.x > 0.5f)
                HandleHorizontalAdjust(1);
            else if (nav.x < -0.5f)
                HandleHorizontalAdjust(-1);
        }

        nextNavigationTime = Time.unscaledTime + navigationRepeatDelay;
    }

    private void HandleHorizontalAdjust(int direction)
    {
        if (selectedIndex == 1)
        {
            AdjustUIScale(0.25f * direction);
            return;
        }

        if (selectedIndex == 2)
        {
            CycleInputMode(direction);
        }
    }

    private int GetMaxSelectableIndex()
    {
        return returnToCharSelectLabel != null ? 4 : 3;
    }

    private void AdjustUIScale(float delta)
    {
        VideoSettings.SetUIScale(VideoSettings.UIScale + delta);
        ApplyScale();
    }

    private void CycleInputMode(int direction)
    {
        int modeCount = Enum.GetValues(typeof(InputMode)).Length;
        int next = ((int)inputMode + direction) % modeCount;
        if (next < 0)
            next += modeCount;

        inputMode = (InputMode)next;
    }

    private void ToggleHitboxView()
    {
        showHitboxView = !showHitboxView;
        FighterView.GlobalShowBoxes = showHitboxView;
    }


    private string GetPrefix(int index)
    {
        return selectedIndex == index ? "> " : "  ";
    }

    private string GetInputModeLabel(InputMode mode)
    {
        switch (mode)
        {
            case InputMode.Gamepad:
                return "Gamepad";
            case InputMode.Keyboard:
                return "Keyboard";
            default:
                return "Auto";
        }
    }

    private void UpdateMenuVisibility()
    {
        if (menuRoot != null)
            menuRoot.SetActive(menuOpen);
    }

    private void ApplyScale()
    {
        if (menuRoot == null)
            return;

        float scale = Mathf.Max(0.25f, VideoSettings.UIScale);
        menuRoot.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private void HandleVideoSettingsChanged()
    {
        ApplyScale();
    }

    private void UpdateContextHint()
    {
        if (contextHintLabel == null)
            return;

        if (!menuOpen)
        {
            if (contextHintRoot != null)
                contextHintRoot.SetActive(false);
            return;
        }

        string hint = GetContextHint();
        contextHintLabel.text = hint;
        if (contextHintRoot != null)
            contextHintRoot.SetActive(!string.IsNullOrEmpty(hint));
    }

    private string GetContextHint()
    {
        switch (selectedIndex)
        {
            case 0:
                return "Invert Y flips vertical movement input.";
            case 1:
                return "UI Scale adjusts the size of the menu text.";
            case 2:
                if (inputMode == InputMode.Auto)
                    return "Auto accepts input from any active device.";
                return "Device mode is mainly for local or tournament setups.";
            case 3:
                return "Hitbox View shows hitboxes and hurtboxes for debugging.";
            case 4:
                return "Leave this match and go back to character select.";
            default:
                return string.Empty;
        }
    }

    private void ReturnToCharacterSelect()
    {
        if (string.IsNullOrWhiteSpace(characterSelectSceneName))
        {
            Debug.LogWarning("MenuOptionsController: Character select scene name is not configured.");
            return;
        }

        MatchSetup.ClearSelections();
        SceneManager.LoadScene(characterSelectSceneName);
    }

    private void OnValidate()
    {
        ApplyScale();
    }
}
