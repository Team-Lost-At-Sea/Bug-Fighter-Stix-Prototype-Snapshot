using UnityEngine;

// Mainly created to be put on UI elements so that they can be hidden to work easily in editor,
// without having to remember to re-enable them to test the game.

[RequireComponent(typeof(CanvasGroup))]
public class EnableThisUIOnAwake : MonoBehaviour
{
    void Awake()
    {
        var canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }
}
