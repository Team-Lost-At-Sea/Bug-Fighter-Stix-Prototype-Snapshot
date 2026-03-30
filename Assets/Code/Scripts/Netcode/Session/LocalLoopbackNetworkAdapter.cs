using System;
using System.Collections.Generic;

public sealed class LocalLoopbackNetworkAdapter : INetworkAdapter
{
    private struct QueuedPacket
    {
        public FrameInputPacket packet;
        public int deliverFrame;
    }

    private readonly List<QueuedPacket> queue = new List<QueuedPacket>(128);
    private readonly Random rng;
    private readonly int mirrorTargetPlayerId;
    private readonly int baseLatencyFrames;
    private readonly int maxJitterFrames;
    private readonly float dropChance;
    private readonly float reorderChance;

    public LocalLoopbackNetworkAdapter(
        int mirrorTargetPlayerId = 2,
        int baseLatencyFrames = 2,
        int maxJitterFrames = 1,
        float dropChance = 0f,
        float reorderChance = 0f,
        int seed = 1337
    )
    {
        this.mirrorTargetPlayerId = mirrorTargetPlayerId;
        this.baseLatencyFrames = Math.Max(0, baseLatencyFrames);
        this.maxJitterFrames = Math.Max(0, maxJitterFrames);
        this.dropChance = Clamp01(dropChance);
        this.reorderChance = Clamp01(reorderChance);
        rng = new Random(seed);
    }

    public void SendLocalInput(FrameInputPacket packet, int currentFrame)
    {
        if (NextFloat() < dropChance)
            return;

        int jitter = maxJitterFrames > 0 ? rng.Next(0, maxJitterFrames + 1) : 0;
        int deliverFrame = currentFrame + baseLatencyFrames + jitter;
        FrameInputPacket mirrored = packet;
        mirrored.playerId = mirrorTargetPlayerId;
        queue.Add(new QueuedPacket
        {
            packet = mirrored,
            deliverFrame = deliverFrame
        });
    }

    public int PollRemoteInputs(int currentFrame, List<FrameInputPacket> output)
    {
        if (output == null)
            return 0;

        output.Clear();

        for (int i = queue.Count - 1; i >= 0; i--)
        {
            if (queue[i].deliverFrame > currentFrame)
                continue;

            output.Add(queue[i].packet);
            queue.RemoveAt(i);
        }

        if (output.Count > 1 && NextFloat() < reorderChance)
        {
            int a = rng.Next(0, output.Count);
            int b = rng.Next(0, output.Count);
            FrameInputPacket temp = output[a];
            output[a] = output[b];
            output[b] = temp;
        }

        return output.Count;
    }

    private static float Clamp01(float value)
    {
        if (value < 0f)
            return 0f;

        if (value > 1f)
            return 1f;

        return value;
    }

    private float NextFloat()
    {
        return (float)rng.NextDouble();
    }
}
