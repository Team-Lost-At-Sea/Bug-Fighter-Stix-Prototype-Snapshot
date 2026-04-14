public interface IGameLoopFeature
{
    void Initialize(GameLoop gameLoop);
    void OnUpdate(GameLoop gameLoop);
    void OnPostRenderGui(GameLoop gameLoop);
    void OnDispose(GameLoop gameLoop);
    void RewriteLocalInputs(GameLoop gameLoop, InputFrame livePlayer1, InputFrame livePlayer2, ref InputFrame finalPlayer1, ref InputFrame finalPlayer2);
}
