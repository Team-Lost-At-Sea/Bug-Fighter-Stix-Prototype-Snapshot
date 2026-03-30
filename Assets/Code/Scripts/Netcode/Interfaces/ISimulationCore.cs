public interface ISimulationCore
{
    int CurrentFrame { get; }
    void Tick(FrameInputPacket frameInputPacket);
    NetState CaptureNetState();
    void RestoreNetState(NetState state);
    int ComputeDeterminismHash();
}
