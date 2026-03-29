using UnityEngine;

[CreateAssetMenu(menuName = "Fighter/Match Config")]
public class MatchConfig : ScriptableObject
{
    [Header("Identity")]
    public string modeId = "Default";

    [Header("Simulation")]
    [Min(1)]
    public int ticksPerSecond = 60;

    [Min(0)]
    public int hitstopFrames = 8;

    [Header("Stage")]
    public float fighterStartPositionOffset = 10f;
    public float stageLeft = -40f;
    public float stageRight = 40f;

    [Header("Spacing Tether")]
    [Tooltip("When enabled, grounded fighters cannot separate beyond maxBackwalkSeparation.")]
    public bool enableBackwalkTether = true;

    [Min(0f)]
    [Tooltip("Tune this against camera max zoom-out distance (for example 22).")]
    public float maxBackwalkSeparation = 40f;

    [Header("Round")]
    [Min(1)]
    public int roundStartHealth = 100;

    [Min(0)]
    public int startingSuperMeter = 0;

    public bool useRoundTimer = true;

    [Min(1)]
    public int roundTimerSeconds = 99;

    public float FixedDt => 1f / Mathf.Max(1, ticksPerSecond);

    private void OnValidate()
    {
        ticksPerSecond = Mathf.Max(1, ticksPerSecond);
        hitstopFrames = Mathf.Max(0, hitstopFrames);
        roundStartHealth = Mathf.Max(1, roundStartHealth);
        startingSuperMeter = Mathf.Max(0, startingSuperMeter);
        roundTimerSeconds = Mathf.Max(1, roundTimerSeconds);

        if (stageRight < stageLeft)
        {
            float temp = stageLeft;
            stageLeft = stageRight;
            stageRight = temp;
        }

        maxBackwalkSeparation = Mathf.Max(0f, maxBackwalkSeparation);

        float halfStageWidth = (stageRight - stageLeft) * 0.5f;
        fighterStartPositionOffset = Mathf.Clamp(
            Mathf.Abs(fighterStartPositionOffset),
            0f,
            halfStageWidth
        );
    }
}
