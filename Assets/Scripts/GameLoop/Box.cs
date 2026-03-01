using UnityEngine;

public struct Box
{
    public Vector2 center;
    public Vector2 halfSize;

    public Box(Vector2 center, Vector2 halfSize)
    {
        this.center = center;
        this.halfSize = halfSize;
    }

    public bool Overlaps(Box other)
    {
        return Mathf.Abs(center.x - other.center.x) <= (halfSize.x + other.halfSize.x)
            && Mathf.Abs(center.y - other.center.y) <= (halfSize.y + other.halfSize.y);
    }
}