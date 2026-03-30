using System.Collections.Generic;

public interface INetworkAdapter
{
    void SendLocalInput(FrameInputPacket packet, int currentFrame);
    int PollRemoteInputs(int currentFrame, List<FrameInputPacket> output);
}
