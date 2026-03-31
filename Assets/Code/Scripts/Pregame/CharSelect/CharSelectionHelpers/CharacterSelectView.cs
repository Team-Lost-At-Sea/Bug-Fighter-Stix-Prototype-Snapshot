using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

public class CharacterSelectView : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private CharacterSelectController controller;

    [SerializeField]
    private RectTransform[] slotAnchors;

    [Header("Cursors")]
    [SerializeField]
    [FormerlySerializedAs("player1Highlight")]
    private RectTransform player1Cursor;

    [SerializeField]
    [FormerlySerializedAs("player2Highlight")]
    private RectTransform player2Cursor;

    [Header("Cursor Animation")]
    [SerializeField]
    private Animator player1CursorAnimator;

    [SerializeField]
    private Animator player2CursorAnimator;

    [SerializeField]
    private string idleStateName = "Idle";

    [SerializeField]
    private string movingStateName = "Moving";

    [SerializeField]
    private string selectedStateName = "Selected";

    [Header("Slot Hover Highlight")]
    [Tooltip("Optional explicit slot visuals to tint. If empty, will auto-pull Graphic components from slotAnchors.")]
    [SerializeField]
    private Graphic[] slotHighlightTargets;

    [SerializeField]
    private Color slotNormalColor = Color.white;

    [SerializeField]
    private Color slotHoverColor = new Color(1f, 0.95f, 0.55f, 1f);

    private CharacterSelectController.CursorState? lastPlayer1AnimatedState;
    private CharacterSelectController.CursorState? lastPlayer2AnimatedState;

    private void OnEnable()
    {
        if (controller != null && slotAnchors != null && slotAnchors.Length > 0)
            controller.SetSlotAnchors(slotAnchors);

        EnsureSlotHighlightTargets();
        lastPlayer1AnimatedState = null;
        lastPlayer2AnimatedState = null;
    }

    private void Update()
    {
        if (controller == null)
            return;

        UpdateCursorPosition(player1Cursor, controller.Player1CursorPosition);
        UpdateCursorPosition(player2Cursor, controller.Player2CursorPosition);
        UpdateCursorAnimation(player1CursorAnimator, controller.Player1CursorVisualState, ref lastPlayer1AnimatedState);
        UpdateCursorAnimation(player2CursorAnimator, controller.Player2CursorVisualState, ref lastPlayer2AnimatedState);
        UpdateSlotHoverHighlight(controller.ActiveHoveredSlotIndex);

        // Colors are controlled by the scene, not the view script.
    }

    private void UpdateCursorPosition(RectTransform cursor, Vector3 worldPosition)
    {
        if (cursor == null)
            return;

        cursor.gameObject.SetActive(true);
        cursor.position = worldPosition;
    }

    private void UpdateCursorAnimation(
        Animator animator,
        CharacterSelectController.CursorState state,
        ref CharacterSelectController.CursorState? lastAnimatedState)
    {
        if (animator == null)
            return;

        if (lastAnimatedState.HasValue && lastAnimatedState.Value == state)
            return;

        string stateName = GetAnimatorStateName(state);
        if (string.IsNullOrWhiteSpace(stateName))
            return;

        int targetHash = Animator.StringToHash(stateName);
        if (!animator.HasState(0, targetHash))
            return;

        animator.Play(targetHash, 0, 0f);
        lastAnimatedState = state;
    }

    private string GetAnimatorStateName(CharacterSelectController.CursorState state)
    {
        switch (state)
        {
            case CharacterSelectController.CursorState.Idle:
                return idleStateName;
            case CharacterSelectController.CursorState.Moving:
                return movingStateName;
            case CharacterSelectController.CursorState.Selected:
                return selectedStateName;
            default:
                return idleStateName;
        }
    }

    private void EnsureSlotHighlightTargets()
    {
        if (slotAnchors == null)
        {
            slotHighlightTargets = new Graphic[0];
            return;
        }

        if (slotHighlightTargets != null && slotHighlightTargets.Length == slotAnchors.Length)
            return;

        if (slotAnchors.Length == 0)
        {
            slotHighlightTargets = new Graphic[0];
            return;
        }

        slotHighlightTargets = new Graphic[slotAnchors.Length];
        for (int i = 0; i < slotAnchors.Length; i++)
        {
            RectTransform anchor = slotAnchors[i];
            if (anchor == null)
                continue;

            slotHighlightTargets[i] = anchor.GetComponent<Graphic>();
        }
    }

    private void UpdateSlotHoverHighlight(int hoveredSlotIndex)
    {
        if (slotHighlightTargets == null || slotHighlightTargets.Length == 0)
            return;

        for (int i = 0; i < slotHighlightTargets.Length; i++)
        {
            Graphic slotGraphic = slotHighlightTargets[i];
            if (slotGraphic == null)
                continue;

            slotGraphic.color = i == hoveredSlotIndex ? slotHoverColor : slotNormalColor;
        }
    }

}
