public struct NetInputMessage
{
    public const ushort CurrentProtocolVersion = 1;

    public ushort protocolVersion;
    public uint sessionId;
    public int senderPlayerId;
    public int currentFrame;
    public uint sequence;
    public uint ackSequence;
    public FrameInputPacket[] packets;
}
