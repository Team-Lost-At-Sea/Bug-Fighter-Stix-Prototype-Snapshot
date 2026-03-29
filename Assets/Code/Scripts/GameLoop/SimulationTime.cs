using UnityEngine;

public static class SimulationTime
{
    public const int DefaultTicksPerSecond = 60;

    private static int ticksPerSecond = DefaultTicksPerSecond;
    private static float fixedDt = 1f / DefaultTicksPerSecond;

    public static int TicksPerSecond => ticksPerSecond;
    public static float FixedDt => fixedDt;

    public static void Configure(int configuredTicksPerSecond)
    {
        ticksPerSecond = Mathf.Max(1, configuredTicksPerSecond);
        fixedDt = 1f / ticksPerSecond;
    }
}
