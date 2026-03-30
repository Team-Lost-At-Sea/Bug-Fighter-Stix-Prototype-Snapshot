using System;

[Serializable]
public struct ReplayPacketRecord
{
    public FrameInputPacket packet;
}

[Serializable]
public class ReplayData
{
    public int version = 1;
    public string modeId;
    public int ticksPerSecond;
    public int frameCount;
    public ReplayPacketRecord[] packets = Array.Empty<ReplayPacketRecord>();
}
