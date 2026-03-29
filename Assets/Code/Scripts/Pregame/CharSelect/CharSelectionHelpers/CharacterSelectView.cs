using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectView : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private CharacterSelectController controller;

    [SerializeField]
    private RectTransform[] slotAnchors;

    [Header("Highlights")]
    [SerializeField]
    private RectTransform player1Highlight;

    [SerializeField]
    private RectTransform player2Highlight;

    private void Update()
    {
        if (controller == null || slotAnchors == null || slotAnchors.Length == 0)
            return;

        UpdateHighlight(player1Highlight, controller.Player1Index);
        UpdateHighlight(player2Highlight, controller.Player2Index);

        // Colors are controlled by the scene, not the view script.
    }

    private void UpdateHighlight(RectTransform highlight, int index)
    {
        if (highlight == null)
            return;

        if (index < 0 || index >= slotAnchors.Length)
        {
            highlight.gameObject.SetActive(false);
            return;
        }

        RectTransform anchor = slotAnchors[index];
        if (anchor == null)
        {
            highlight.gameObject.SetActive(false);
            return;
        }

        highlight.gameObject.SetActive(true);
        highlight.position = anchor.position;
        highlight.sizeDelta = anchor.sizeDelta;
    }

}
