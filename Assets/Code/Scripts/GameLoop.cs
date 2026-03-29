using UnityEngine;

public class GameLoop : MonoBehaviour
{
    public const int TICKS_PER_SECOND = 60;
    public const float FIXED_DT = 1f / TICKS_PER_SECOND;

    private float accumulator;

    [Header("Views")]
    public FighterView player1View;
    public FighterView player2View;

    [Header("Character Selection")]
    [SerializeField]
    private CharacterDefinition player1Character;

    [SerializeField]
    private CharacterDefinition player2Character;

    [Header("Debug")]
    [SerializeField]
    private int hitstopFrames = 8;
    
    [Header("Match")]
    [SerializeField]
    private MatchConfig matchConfig;

    private Simulation simulation;
    private readonly MatchPresenter presenter = new MatchPresenter();

    public Simulation ActiveSimulation => simulation;

    void Start()
    {
        int configuredTicksPerSecond = matchConfig != null
            ? matchConfig.ticksPerSecond
            : TICKS_PER_SECOND;
        SimulationTime.Configure(configuredTicksPerSecond);

        simulation = new Simulation(matchConfig);
        Fighter.HitstopFrames = matchConfig != null
            ? Mathf.Max(0, matchConfig.hitstopFrames)
            : Mathf.Max(0, hitstopFrames);
        ApplySelectedCharacters();

        FighterConfig player1Config = player1View != null ? player1View.Config : null;
        FighterConfig player2Config = player2View != null ? player2View.Config : null;
        if (player1Config == null || player2Config == null)
        {
            Debug.LogError(
                "GameLoop: Missing fighter config(s) on player views. Ensure character definitions are applied before match start."
            );
            simulation = null;
            return;
        }

        string player1Name = player1View != null ? player1View.name : "Player1";
        string player2Name = player2View != null ? player2View.name : "Player2";
        simulation.Initialize(player1Config, player2Config, player1Name, player2Name);
        presenter.Initialize(simulation, player1View, player2View);
    }

    void Update()
    {
        if (simulation == null)
            return;

        accumulator += Time.deltaTime;

        int safety = 0;
        float fixedDt = SimulationTime.FixedDt;
        while (accumulator >= fixedDt && safety < 5)
        {
            // Tick the simulation with the next buffered input
            int nextFrameIndex = simulation.CurrentFrame + 1;
            FrameInput frameInput = GameInput.Instance.ConsumeNextFrameInput(nextFrameIndex);

            simulation.Tick(frameInput);

            accumulator -= fixedDt;
            safety++;
        }

        // Render after simulation updates
        presenter.Render(simulation);
    }

    private void OnDisable()
    {
        presenter.Dispose();
    }

    private void ApplySelectedCharacters()
    {
        CharacterDefinition resolvedPlayer1 = player1Character;
        CharacterDefinition resolvedPlayer2 = player2Character;

        if (MatchSetup.HasSelections)
        {
            resolvedPlayer1 = MatchSetup.Player1Character;
            resolvedPlayer2 = MatchSetup.Player2Character;
        }

        if (player1View != null && resolvedPlayer1 != null)
            player1View.ApplyCharacterDefinition(resolvedPlayer1);

        if (player2View != null && resolvedPlayer2 != null)
            player2View.ApplyCharacterDefinition(resolvedPlayer2);
    }
}
