public struct FrameInputPacket
{
    public int frame;
    public int playerId;
    public ushort buttonsBitmask;
    public sbyte moveX;
    public sbyte moveY;
    public uint sequence;

    public static FrameInputPacket Neutral(int frame, int playerId, uint sequence = 0)
    {
        return new FrameInputPacket
        {
            frame = frame,
            playerId = playerId,
            buttonsBitmask = (ushort)InputButtons.None,
            moveX = 0,
            moveY = 0,
            sequence = sequence
        };
    }
}
