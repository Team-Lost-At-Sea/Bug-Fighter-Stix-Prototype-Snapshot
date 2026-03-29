public struct FrameInput
{
    public int frameIndex;
    public InputFrame player1;
    public InputFrame player2;

    public static FrameInput Neutral(int frameIndex)
    {
        return new FrameInput
        {
            frameIndex = frameIndex,
            player1 = InputFrame.Neutral,
            player2 = InputFrame.Neutral
        };
    }
}
