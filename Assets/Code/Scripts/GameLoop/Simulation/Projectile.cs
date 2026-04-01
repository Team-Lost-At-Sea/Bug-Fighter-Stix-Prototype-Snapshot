using UnityEngine;

public sealed class Projectile
{
    public readonly int id;
    public readonly Fighter owner;
    public readonly int damage;
    public readonly int hitstunFrames;
    public readonly int blockstunFrames;
    public readonly float blockPushback;
    public readonly int chipDamage;
    public readonly int attackerBlockstopFrames;
    public readonly HitLevel hitLevel;
    public readonly Vector2 halfSize;

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
        int blockstunFrames,
        float blockPushback,
        int chipDamage,
        int attackerBlockstopFrames,
        HitLevel hitLevel
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
        this.blockstunFrames = blockstunFrames;
        this.blockPushback = blockPushback;
        this.chipDamage = chipDamage;
        this.attackerBlockstopFrames = attackerBlockstopFrames;
        this.hitLevel = hitLevel;
    }

    public Box CurrentBox => new Box(position, halfSize);

    public Hitbox ToHitbox()
    {
        return new Hitbox
        {
            box = CurrentBox,
            damage = damage,
            hitstunFrames = hitstunFrames,
            blockstunFrames = blockstunFrames,
            blockPushback = blockPushback,
            chipDamage = chipDamage,
            attackerBlockstopFrames = attackerBlockstopFrames,
            hitLevel = hitLevel,
            isProjectile = true,
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
