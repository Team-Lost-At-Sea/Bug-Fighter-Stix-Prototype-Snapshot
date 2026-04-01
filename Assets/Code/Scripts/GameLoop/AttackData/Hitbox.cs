using UnityEngine;

public struct Hitbox
{
    public Box box;
    public int damage;
    public int hitstunFrames;
    public int blockstunFrames;
    public float blockPushback;
    public int chipDamage;
    public int attackerBlockstopFrames;
    public HitLevel hitLevel;
    public bool isProjectile;
    public bool active;
    public bool hasHit; // prevent multi-hits

    public void Reset()
    {
        active = false;
        hasHit = false;
        hitLevel = HitLevel.Mid;
        blockPushback = 0f;
        attackerBlockstopFrames = -1;
        isProjectile = false;
    }
}
