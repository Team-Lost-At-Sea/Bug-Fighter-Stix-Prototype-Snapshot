public struct InputFrame
{
    public float moveX;
    public float moveY;

    // Attack inputs (can be multiple in the same frame)
    public bool punchLight;
    public bool punchMedium;
    public bool punchHeavy;
    public bool dirt;
    public bool punchLightPressed;
    public bool punchMediumPressed;
    public bool punchHeavyPressed;
    public bool dirtPressed;

    public bool HasAttackPress
    {
        get
        {
            return punchLightPressed || punchMediumPressed || punchHeavyPressed || dirtPressed;
        }
    }

    public bool HasPunchPress
    {
        get
        {
            return punchLightPressed || punchMediumPressed || punchHeavyPressed;
        }
    }

    // Static default / neutral input
    public static readonly InputFrame Neutral = new InputFrame
    {
        moveX = 0f,
        moveY = 0f,
        punchLight = false,
        punchMedium = false,
        punchHeavy = false,
        dirt = false,
        punchLightPressed = false,
        punchMediumPressed = false,
        punchHeavyPressed = false,
        dirtPressed = false,
    };
}
