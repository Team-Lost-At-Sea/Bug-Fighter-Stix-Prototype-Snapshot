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

    void Start()
    {
        Fighter.HitstopFrames = Mathf.Max(0, hitstopFrames);
        ApplySelectedCharacters();
        Simulation.Instance.Initialize(player1View, player2View);
    }

    void Update()
    {
        accumulator += Time.deltaTime;

        int safety = 0;
        while (accumulator >= FIXED_DT && safety < 5)
        {
            // Tick the simulation with the next buffered input
            InputFrame p1Input = GameInput.Instance.ConsumeNextInput();

            Simulation.Instance.Tick(p1Input);

            accumulator -= FIXED_DT;
            safety++;
        }

        // Render after simulation updates
        Simulation.Instance.Render();
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
