using System.IO;

public static class NetInputMessageCodec
{
    public const int MaxPacketsPerMessage = 16;

    private const ushort Magic = 0x4253;

    public static byte[] Serialize(NetInputMessage message)
    {
        FrameInputPacket[] packets = message.packets;
        int packetCount = packets != null ? packets.Length : 0;
        if (packetCount > MaxPacketsPerMessage)
            throw new InvalidDataException("Net input message contains too many packets.");

        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write(Magic);
            writer.Write(message.protocolVersion == 0 ? NetInputMessage.CurrentProtocolVersion : message.protocolVersion);
            writer.Write(message.sessionId);
            writer.Write(message.senderPlayerId);
            writer.Write(message.currentFrame);
            writer.Write(message.sequence);
            writer.Write(message.ackSequence);
            writer.Write((byte)packetCount);

            for (int i = 0; i < packetCount; i++)
                WritePacket(writer, packets[i]);

            return stream.ToArray();
        }
    }

    public static bool TryDeserialize(byte[] bytes, out NetInputMessage message)
    {
        message = new NetInputMessage();
        if (bytes == null || bytes.Length == 0)
            return false;

        try
        {
            using (MemoryStream stream = new MemoryStream(bytes))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                ushort magic = reader.ReadUInt16();
                if (magic != Magic)
                    return false;

                ushort protocolVersion = reader.ReadUInt16();
                if (protocolVersion != NetInputMessage.CurrentProtocolVersion)
                    return false;

                message.protocolVersion = protocolVersion;
                message.sessionId = reader.ReadUInt32();
                message.senderPlayerId = reader.ReadInt32();
                message.currentFrame = reader.ReadInt32();
                message.sequence = reader.ReadUInt32();
                message.ackSequence = reader.ReadUInt32();

                int packetCount = reader.ReadByte();
                if (packetCount > MaxPacketsPerMessage)
                    return false;

                FrameInputPacket[] packets = new FrameInputPacket[packetCount];
                for (int i = 0; i < packetCount; i++)
                    packets[i] = ReadPacket(reader);

                if (stream.Position != stream.Length)
                    return false;

                message.packets = packets;
                return true;
            }
        }
        catch (EndOfStreamException)
        {
            message = new NetInputMessage();
            return false;
        }
        catch (IOException)
        {
            message = new NetInputMessage();
            return false;
        }
    }

    public static NetInputMessage Deserialize(byte[] bytes)
    {
        if (TryDeserialize(bytes, out NetInputMessage message))
            return message;

        throw new InvalidDataException("Invalid net input message.");
    }

    private static void WritePacket(BinaryWriter writer, FrameInputPacket packet)
    {
        writer.Write(packet.frame);
        writer.Write(packet.playerId);
        writer.Write(packet.buttonsBitmask);
        writer.Write(packet.moveX);
        writer.Write(packet.moveY);
        writer.Write(packet.sequence);
    }

    private static FrameInputPacket ReadPacket(BinaryReader reader)
    {
        return new FrameInputPacket
        {
            frame = reader.ReadInt32(),
            playerId = reader.ReadInt32(),
            buttonsBitmask = reader.ReadUInt16(),
            moveX = reader.ReadSByte(),
            moveY = reader.ReadSByte(),
            sequence = reader.ReadUInt32()
        };
    }
}
