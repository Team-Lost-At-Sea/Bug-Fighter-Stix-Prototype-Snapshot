public struct InputFrame
{
    public float moveX;
    public float moveY;

    // Attack inputs (can be multiple in the same frame)
    public bool punchLight;
    public bool punchMedium;
    public bool punchHeavy;

    // Static default / neutral input
    public static readonly InputFrame Neutral = new InputFrame
    {
        moveX = 0f,
        moveY = 0f,
        punchLight = false,
        punchMedium = false,
        punchHeavy = false,
    };
}
