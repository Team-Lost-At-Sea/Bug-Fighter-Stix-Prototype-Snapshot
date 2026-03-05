using UnityEngine;

public class DebugBoxVisual : MonoBehaviour
{
    private SpriteRenderer sprite;
    private Color currentColor;

    public void Initialize(Color color)
    {
        sprite = gameObject.AddComponent<SpriteRenderer>();
        sprite.sprite = GenerateSprite();
        currentColor = color;
        sprite.color = color;
        sprite.sortingOrder = 100; // Always on top
    }

    public void SetColor(Color color)
    {
        if (sprite == null || color == currentColor)
            return;

        currentColor = color;
        sprite.color = color;
    }

    public void SetBox(Box box)
    {
        transform.position = box.center;
        transform.localScale = new Vector3(box.halfSize.x * 2f, box.halfSize.y * 2f, 1f);
    }

    public void SetVisible(bool value)
    {
        if (sprite != null)
            sprite.enabled = value;
    }

    private Sprite GenerateSprite()
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }
}
