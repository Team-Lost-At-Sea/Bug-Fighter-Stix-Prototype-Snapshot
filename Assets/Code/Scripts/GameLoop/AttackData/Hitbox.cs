using UnityEngine;

public struct Hitbox
{
    public Box box;
    public int damage;
    public int hitstunFrames;
    public bool active;
    public bool hasHit; // prevent multi-hits

    public void Reset()
    {
        active = false;
        hasHit = false;
    }
}