public interface IMatchSession
{
    RollbackMetrics Metrics { get; }
    void SubmitLocalInput(FrameInputPacket packet);
    int PollRemoteInputs();
    void AdvanceFrame();
}
