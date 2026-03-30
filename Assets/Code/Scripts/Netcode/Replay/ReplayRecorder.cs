using System.Collections.Generic;

public sealed class ReplayRecorder
{
    private readonly List<ReplayPacketRecord> packets = new List<ReplayPacketRecord>(4096);

    public void Clear()
    {
        packets.Clear();
    }

    public void Record(FrameInputPacket packet)
    {
        packets.Add(new ReplayPacketRecord
        {
            packet = packet
        });
    }

    public ReplayData Build(string modeId, int ticksPerSecond, int frameCount)
    {
        return new ReplayData
        {
            modeId = modeId,
            ticksPerSecond = ticksPerSecond,
            frameCount = frameCount,
            packets = packets.ToArray()
        };
    }
}
