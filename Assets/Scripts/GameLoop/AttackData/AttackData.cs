using UnityEngine;

[System.Serializable]
public class AttackData
{
    public int startupFrames;
    public int activeFrames;
    public int recoveryFrames;

    public int damage;
    public int hitstunFrames;

    public Vector2 hitboxOffset;
    public Vector2 hitboxSize;
}