using UnityEngine;

public sealed class Projectile
{
    public readonly int id;
    public readonly Fighter owner;
    public readonly int damage;
    public readonly int hitstunFrames;
    public readonly Vector2 halfSize;
    public readonly Sprite sprite;
    public readonly Color tint;

    public Vector2 position;
    public Vector2 velocity;
    public int lifetimeFramesRemaining;
    public bool active = true;

    public Projectile(
        int id,
        Fighter owner,
        Vector2 position,
        Vector2 velocity,
        Vector2 halfSize,
        int lifetimeFrames,
        int damage,
        int hitstunFrames,
        Sprite sprite,
        Color tint
    )
    {
        this.id = id;
        this.owner = owner;
        this.position = position;
        this.velocity = velocity;
        this.halfSize = halfSize;
        lifetimeFramesRemaining = lifetimeFrames;
        this.damage = damage;
        this.hitstunFrames = hitstunFrames;
        this.sprite = sprite;
        this.tint = tint;
    }

    public Box CurrentBox => new Box(position, halfSize);

    public Hitbox ToHitbox()
    {
        return new Hitbox
        {
            box = CurrentBox,
            damage = damage,
            hitstunFrames = hitstunFrames,
            active = active,
            hasHit = false
        };
    }

    public void Tick()
    {
        if (!active)
            return;

        position += velocity;
        lifetimeFramesRemaining--;
        if (lifetimeFramesRemaining <= 0)
            active = false;
    }
}
