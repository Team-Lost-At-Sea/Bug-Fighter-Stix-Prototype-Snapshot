using UnityEngine;

public struct ProjectileSpawnRequest
{
    public Fighter owner;
    public Vector2 position;
    public Vector2 velocity;
    public Vector2 halfSize;
    public int lifetimeFrames;
    public int damage;
    public int hitstunFrames;
    public Sprite sprite;
    public Color tint;
}
