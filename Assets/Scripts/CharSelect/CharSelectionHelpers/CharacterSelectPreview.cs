using UnityEngine;

public class CharacterSelectPreview : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private CharacterSelectController controller;

    [SerializeField]
    private CharacterSelectRoster roster;

    [Header("Preview Targets")]
    [SerializeField]
    private Animator player1PreviewAnimator;

    [SerializeField]
    private Animator player2PreviewAnimator;

    [Header("Animation")]
    [SerializeField]
    private string fallbackIdleStateName = "Idle";

    private int lastPlayer1Index = -1;
    private int lastPlayer2Index = -1;

    private void Update()
    {
        if (controller == null || roster == null)
            return;

        UpdatePreviewIfChanged(ref lastPlayer1Index, controller.Player1Index, player1PreviewAnimator);
        UpdatePreviewIfChanged(ref lastPlayer2Index, controller.Player2Index, player2PreviewAnimator);
    }

    private void UpdatePreviewIfChanged(ref int lastIndex, int currentIndex, Animator animator)
    {
        if (animator == null)
            return;

        if (currentIndex == lastIndex)
            return;

        lastIndex = currentIndex;
        CharacterDefinition definition = roster.GetCharacter(currentIndex);
        if (definition == null)
            return;

        animator.runtimeAnimatorController = definition.animatorController;
        animator.Rebind();
        animator.Update(0f);

        string idleState = GetIdleStateName(definition);
        if (!string.IsNullOrWhiteSpace(idleState))
            animator.Play(idleState, 0, 0f);
    }

    private string GetIdleStateName(CharacterDefinition definition)
    {
        if (definition == null || definition.fighterConfig == null)
            return fallbackIdleStateName;

        string configuredIdle = definition.fighterConfig.idleStateName;
        if (string.IsNullOrWhiteSpace(configuredIdle))
            return fallbackIdleStateName;

        return configuredIdle;
    }
}
