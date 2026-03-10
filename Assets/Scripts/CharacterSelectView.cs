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

    [SerializeField]
    private Image player1HighlightImage;

    [SerializeField]
    private Image player2HighlightImage;

    [Header("Colors")]
    [SerializeField]
    private Color player1ActiveColor = new Color(0.2f, 0.8f, 1f, 1f);

    [SerializeField]
    private Color player1ConfirmedColor = new Color(0.1f, 0.6f, 0.8f, 1f);

    [SerializeField]
    private Color player2ActiveColor = new Color(1f, 0.6f, 0.2f, 1f);

    [SerializeField]
    private Color player2ConfirmedColor = new Color(0.8f, 0.5f, 0.1f, 1f);

    private void Update()
    {
        if (controller == null || slotAnchors == null || slotAnchors.Length == 0)
            return;

        UpdateHighlight(player1Highlight, player1HighlightImage, controller.Player1Index);
        UpdateHighlight(player2Highlight, player2HighlightImage, controller.Player2Index);

        UpdateHighlightColors();
    }

    private void UpdateHighlight(RectTransform highlight, Image highlightImage, int index)
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

    private void UpdateHighlightColors()
    {
        if (player1HighlightImage != null)
            player1HighlightImage.color = controller.Player1Confirmed ? player1ConfirmedColor : player1ActiveColor;

        if (player2HighlightImage != null)
            player2HighlightImage.color = controller.Player2Confirmed ? player2ConfirmedColor : player2ActiveColor;
    }
}
