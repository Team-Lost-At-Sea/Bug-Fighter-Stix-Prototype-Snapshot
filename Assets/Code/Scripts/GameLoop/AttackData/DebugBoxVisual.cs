using UnityEngine;

public class DebugBoxVisual : MonoBehaviour
{
    private static Sprite fallbackSprite;
    private SpriteRenderer sprite;
    private Color currentColor;

    public void Initialize(Color color)
    {
        sprite = gameObject.AddComponent<SpriteRenderer>();
        sprite.sprite = GetFallbackSprite();
        currentColor = color;
        sprite.color = color;
        sprite.sortingOrder = RenderOrder.World.DebugBoxes;
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

    public void SetSprite(Sprite customSprite)
    {
        if (sprite == null)
            return;

        sprite.sprite = customSprite != null ? customSprite : GetFallbackSprite();
    }

    public void SetSortingOrder(int sortingOrder)
    {
        if (sprite != null)
            sprite.sortingOrder = sortingOrder;
    }

    private static Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null)
            return fallbackSprite;

        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();

        fallbackSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return fallbackSprite;
    }
}
