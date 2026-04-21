using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public sealed class UdpNetworkAdapter : INetworkAdapter, IDisposable
{
    private readonly UdpClient client;
    private readonly IPEndPoint remoteEndPoint;
    private readonly RecentInputPacketBuffer recentInputs;
    private readonly FrameInputPacket[] packetScratch;
    private readonly uint sessionId;
    private readonly int localPlayerId;
    private readonly int remotePlayerId;
    private uint messageSequence;
    private uint lastReceivedSequence;
    private bool disposed;

    public UdpNetworkAdapter(
        uint sessionId,
        int localPlayerId,
        string remoteAddress,
        int localPort,
        int remotePort,
        int recentInputCapacity = NetInputMessageCodec.MaxPacketsPerMessage
    )
    {
        this.sessionId = sessionId;
        this.localPlayerId = localPlayerId == 2 ? 2 : 1;
        remotePlayerId = this.localPlayerId == 1 ? 2 : 1;

        IPAddress address = ResolveAddress(remoteAddress);

        remoteEndPoint = new IPEndPoint(address, ClampPort(remotePort));
        client = new UdpClient(ClampPort(localPort));
        client.Client.Blocking = false;
        recentInputs = new RecentInputPacketBuffer(ClampRecentInputCapacity(recentInputCapacity));
        packetScratch = new FrameInputPacket[recentInputs.Capacity];
    }

    public void SendLocalInput(FrameInputPacket packet, int currentFrame)
    {
        if (disposed)
            return;

        packet.playerId = localPlayerId;
        recentInputs.Add(packet);

        int packetCount = recentInputs.CopyRecent(packetScratch);
        FrameInputPacket[] packets = new FrameInputPacket[packetCount];
        for (int i = 0; i < packetCount; i++)
            packets[i] = packetScratch[i];

        NetInputMessage message = new NetInputMessage
        {
            protocolVersion = NetInputMessage.CurrentProtocolVersion,
            sessionId = sessionId,
            senderPlayerId = localPlayerId,
            currentFrame = currentFrame,
            sequence = ++messageSequence,
            ackSequence = lastReceivedSequence,
            packets = packets
        };

        byte[] bytes = NetInputMessageCodec.Serialize(message);
        client.Send(bytes, bytes.Length, remoteEndPoint);
    }

    public int PollRemoteInputs(int currentFrame, List<FrameInputPacket> output)
    {
        if (output == null)
            return 0;

        output.Clear();
        if (disposed)
            return 0;

        while (client.Available > 0)
        {
            IPEndPoint sender = null;
            byte[] bytes;
            try
            {
                bytes = client.Receive(ref sender);
            }
            catch (SocketException)
            {
                break;
            }

            NetInputMessage message;
            if (!NetInputMessageCodec.TryDeserialize(bytes, out message))
                continue;

            if (message.sessionId != sessionId || message.senderPlayerId == localPlayerId)
                continue;

            if (message.senderPlayerId != remotePlayerId)
                continue;

            if (message.sequence > lastReceivedSequence)
                lastReceivedSequence = message.sequence;

            FrameInputPacket[] packets = message.packets;
            if (packets == null)
                continue;

            for (int i = 0; i < packets.Length; i++)
            {
                FrameInputPacket packet = packets[i];
                if (packet.playerId != remotePlayerId)
                    continue;

                output.Add(packet);
            }
        }

        SortByFrame(output);
        return output.Count;
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        client.Close();
    }

    private static int ClampPort(int port)
    {
        if (port < 1)
            return 1;

        if (port > 65535)
            return 65535;

        return port;
    }

    private static int ClampRecentInputCapacity(int capacity)
    {
        if (capacity < 1)
            return 1;

        if (capacity > NetInputMessageCodec.MaxPacketsPerMessage)
            return NetInputMessageCodec.MaxPacketsPerMessage;

        return capacity;
    }

    private static IPAddress ResolveAddress(string remoteAddress)
    {
        if (string.IsNullOrWhiteSpace(remoteAddress))
            return IPAddress.Loopback;

        IPAddress address;
        if (IPAddress.TryParse(remoteAddress, out address))
            return address;

        IPAddress[] addresses = Dns.GetHostAddresses(remoteAddress);
        if (addresses == null || addresses.Length == 0)
            throw new SocketException((int)SocketError.HostNotFound);

        return addresses[0];
    }

    private static void SortByFrame(List<FrameInputPacket> packets)
    {
        for (int i = 1; i < packets.Count; i++)
        {
            FrameInputPacket value = packets[i];
            int j = i - 1;
            while (j >= 0 && packets[j].frame > value.frame)
            {
                packets[j + 1] = packets[j];
                j--;
            }

            packets[j + 1] = value;
        }
    }
}
