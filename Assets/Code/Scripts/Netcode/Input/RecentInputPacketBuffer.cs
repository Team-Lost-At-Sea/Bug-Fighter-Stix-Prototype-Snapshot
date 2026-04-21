public sealed class RecentInputPacketBuffer
{
    private readonly FrameInputPacket[] packets;
    private int nextWriteIndex;
    private int count;

    public RecentInputPacketBuffer(int capacity = NetInputMessageCodec.MaxPacketsPerMessage)
    {
        if (capacity < 1)
            capacity = 1;

        packets = new FrameInputPacket[capacity];
    }

    public int Capacity => packets.Length;

    public int Count => count;

    public void Add(FrameInputPacket packet)
    {
        packets[nextWriteIndex] = packet;
        nextWriteIndex = (nextWriteIndex + 1) % packets.Length;
        if (count < packets.Length)
            count++;
    }

    public int CopyRecent(FrameInputPacket[] destination)
    {
        if (destination == null || destination.Length == 0 || count == 0)
            return 0;

        int copyCount = destination.Length < count ? destination.Length : count;
        int start = nextWriteIndex - copyCount;
        while (start < 0)
            start += packets.Length;

        for (int i = 0; i < copyCount; i++)
            destination[i] = packets[(start + i) % packets.Length];

        return copyCount;
    }
}
