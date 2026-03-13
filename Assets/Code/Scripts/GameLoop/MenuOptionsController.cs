using System;
using UnityEngine;
using UnityEngine.UI;

public class MenuOptionsController : MonoBehaviour
{
    [Header("Menu")]
    [SerializeField]
    private bool allowMenuOpening = true;

    [Header("UI Scale")]
    [SerializeField]
    private float uiScale = 1f;

    [Header("UI References")]
    [SerializeField]
    private GameObject menuRoot;

    [SerializeField]
    private Text optionLabel;

    [SerializeField]
    private Text toggleHintLabel;

    [SerializeField]
    private Text closeHintLabel;

    private bool menuOpen;

    public bool IsMenuOpen => menuOpen;
    public bool AllowMenuOpening
    {
        get => allowMenuOpening;
        set => allowMenuOpening = value;
    }

    public float UIScale => uiScale;

    private void Awake()
    {
        if (menuRoot == null)
            Debug.LogError("MenuOptionsController: Menu Root is not assigned; options menu UI will not be visible.");

        ApplyScale();
        UpdateMenuVisibility();
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

        if (menuOpen && actions.UI.Submit.WasPressedThisFrame())
            onToggleInvertY?.Invoke();

        return openedThisFrame;
    }

    public void UpdateView(bool invertYEnabled)
    {
        UpdateMenuVisibility();
        string optionState = invertYEnabled ? "On" : "Off";
        if (optionLabel != null)
            optionLabel.text = $"> Invert Y axis: {optionState}";
        if (toggleHintLabel != null)
            toggleHintLabel.text = "Press Submit to toggle";
        if (closeHintLabel != null)
            closeHintLabel.text = "Press Start to close";
    }

    private void SetMenuOpen(bool open)
    {
        if (menuOpen == open)
            return;

        menuOpen = open;
        UpdateMenuVisibility();
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

        float scale = Mathf.Max(0.25f, uiScale * 4f);
        menuRoot.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private void OnValidate()
    {
        float[] steps = { 1f, 1.25f, 1.5f, 2f, 3f, 4f };
        int closestIndex = 0;
        float closestDistance = Mathf.Abs(uiScale - steps[0]);
        for (int i = 1; i < steps.Length; i++)
        {
            float distance = Mathf.Abs(uiScale - steps[i]);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        uiScale = steps[closestIndex];
        ApplyScale();
    }
}
