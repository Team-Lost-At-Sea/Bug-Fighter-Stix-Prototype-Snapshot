using UnityEngine;

[System.Serializable]
public class AttackData
{
    public int startupFrames;
    public int activeFrames;
    public int recoveryFrames;

    public int damage;
    public int hitstunFrames;
    public int blockstunFrames;
    public float blockPushback = 0.45f;
    public int chipDamage;
    public int attackerBlockstopFrames = -1;
    public HitLevel hitLevel = HitLevel.Mid;

    public Vector2 hitboxOffset;
    public Vector2 hitboxSize;
}
