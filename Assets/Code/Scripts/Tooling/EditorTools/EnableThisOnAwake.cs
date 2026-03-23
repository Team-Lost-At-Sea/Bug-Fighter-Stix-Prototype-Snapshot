using UnityEngine;

// Mainly created to be put on UI elements so that they can be hidden to work easily in editor,
// without having to remember to re-enable them to test the game.

public class EnableThisOnAwake : MonoBehaviour
{
    void Awake()
    {
        gameObject.SetActive(true);
    }
}
