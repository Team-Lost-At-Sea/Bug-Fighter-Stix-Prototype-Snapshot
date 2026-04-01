using UnityEngine;

public readonly struct ProjectileSpawnRequest
{
    public readonly Fighter owner;
    public readonly Vector2 position;
    public readonly Vector2 velocity;
    public readonly Vector2 halfSize;
    public readonly int lifetimeFrames;
    public readonly int damage;
    public readonly int hitstunFrames;
    public readonly int blockstunFrames;
    public readonly float blockPushback;
    public readonly int chipDamage;
    public readonly int attackerBlockstopFrames;
    public readonly HitLevel hitLevel;

    public ProjectileSpawnRequest(
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
        this.owner = owner;
        this.position = position;
        this.velocity = velocity;
        this.halfSize = halfSize;
        this.lifetimeFrames = Mathf.Max(1, lifetimeFrames);
        this.damage = Mathf.Max(0, damage);
        this.hitstunFrames = Mathf.Max(1, hitstunFrames);
        this.blockstunFrames = Mathf.Max(1, blockstunFrames);
        this.blockPushback = Mathf.Max(0f, blockPushback);
        this.chipDamage = Mathf.Max(0, chipDamage);
        this.attackerBlockstopFrames = attackerBlockstopFrames;
        this.hitLevel = hitLevel;
    }
}
