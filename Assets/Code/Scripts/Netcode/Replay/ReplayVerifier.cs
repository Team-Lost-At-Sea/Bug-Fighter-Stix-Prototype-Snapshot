using System.Collections.Generic;
using UnityEngine;

public static class ReplayVerifier
{
    public static bool VerifyDeterministicHashes(
        FighterConfig player1Config,
        FighterConfig player2Config,
        MatchConfig matchConfig,
        ReplayData replay,
        out string report
    )
    {
        report = "Replay verification failed: missing setup.";
        if (player1Config == null || player2Config == null || replay == null)
            return false;

        Simulation runA = new Simulation(matchConfig);
        Simulation runB = new Simulation(matchConfig);
        runA.Initialize(player1Config, player2Config, "ReplayA_P1", "ReplayA_P2");
        runB.Initialize(player1Config, player2Config, "ReplayB_P1", "ReplayB_P2");

        Dictionary<int, List<FrameInputPacket>> packetsByFrame = BucketByFrame(replay);
        int maxFrame = Mathf.Max(replay.frameCount, FindMaxFrame(packetsByFrame));
        List<string> mismatches = new List<string>();
        for (int frame = 1; frame <= maxFrame; frame++)
        {
            if (packetsByFrame.TryGetValue(frame, out List<FrameInputPacket> framePackets))
            {
                for (int i = 0; i < framePackets.Count; i++)
                {
                    runA.Tick(framePackets[i]);
                    runB.Tick(framePackets[i]);
                }
            }
            else
            {
                runA.Tick(FrameInputPacket.Neutral(frame, 1));
                runA.Tick(FrameInputPacket.Neutral(frame, 2));
                runB.Tick(FrameInputPacket.Neutral(frame, 1));
                runB.Tick(FrameInputPacket.Neutral(frame, 2));
            }

            int hashA = runA.ComputeDeterminismHash();
            int hashB = runB.ComputeDeterminismHash();
            if (hashA != hashB)
            {
                mismatches.Add($"Frame {frame}: hashA={hashA} hashB={hashB}");
                if (mismatches.Count >= 8)
                    break;
            }
        }

        if (mismatches.Count > 0)
        {
            report = "[ReplayVerifier] FAIL\n" + string.Join("\n", mismatches);
            return false;
        }

        report = $"[ReplayVerifier] PASS. Frames={maxFrame} FinalHash={runA.ComputeDeterminismHash()}";
        return true;
    }

    private static Dictionary<int, List<FrameInputPacket>> BucketByFrame(ReplayData replay)
    {
        Dictionary<int, List<FrameInputPacket>> map = new Dictionary<int, List<FrameInputPacket>>();
        if (replay.packets == null)
            return map;

        for (int i = 0; i < replay.packets.Length; i++)
        {
            FrameInputPacket packet = replay.packets[i].packet;
            if (!map.TryGetValue(packet.frame, out List<FrameInputPacket> list))
            {
                list = new List<FrameInputPacket>(2);
                map.Add(packet.frame, list);
            }

            list.Add(packet);
        }

        return map;
    }

    private static int FindMaxFrame(Dictionary<int, List<FrameInputPacket>> packetsByFrame)
    {
        int max = 0;
        foreach (KeyValuePair<int, List<FrameInputPacket>> kv in packetsByFrame)
        {
            if (kv.Key > max)
                max = kv.Key;
        }

        return max;
    }
}
