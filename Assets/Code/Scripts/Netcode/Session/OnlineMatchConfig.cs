public enum OnlineConnectionMode
{
    Offline,
    LocalLoopback,
    DirectUdp,
    Relay
}

public struct OnlineMatchConfig
{
    public OnlineConnectionMode connectionMode;
    public uint sessionId;
    public bool isHost;
    public int localPlayerId;
    public int remotePlayerId;
    public string remoteAddress;
    public ushort localPort;
    public ushort remotePort;
    public int inputDelayFrames;
    public int rollbackWindowFrames;
    public bool allowTrainingTools;
    public bool allowLocalP2Input;
    public bool allowDebugStateMutation;
    public bool showNetDebugHud;
}
