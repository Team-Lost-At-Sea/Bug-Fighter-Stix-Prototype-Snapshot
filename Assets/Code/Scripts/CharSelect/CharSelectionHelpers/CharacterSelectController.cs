using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class CharacterSelectController : MonoBehaviour
{
    private enum SelectionPhase
    {
        Player1,
        Player2,
        Done
    }

    [Header("Roster")]
    [SerializeField]
    private CharacterSelectRoster roster;

    [Header("Defaults")]
    [SerializeField]
    private int defaultPlayer1Index;

    [SerializeField]
    private int defaultPlayer2Index = 1;

    [Header("Selection Rules")]
    [SerializeField]
    private bool allowSameCharacter = true;

    [SerializeField]
    private bool wrapSelection = true;

    [Tooltip("If greater than 0, up/down moves by this many slots.")]
    [SerializeField]
    private int gridColumns;

    [Header("Input")]
    [SerializeField]
    private float navigationDeadzone = 0.5f;

    [SerializeField]
    private float navigationRepeatDelay = 0.2f;

    [Header("Scene")]
    [SerializeField]
    private string matchSceneName = "Training Room";

    [SerializeField]
    private string backSceneName = "";

    private InputSystem_Actions inputActions;
    private InputAction navigateAction;
    private InputAction submitAction;
    private InputAction cancelAction;

    private int player1Index;
    private int player2Index;
    private bool player1Confirmed;
    private bool player2Confirmed;
    private SelectionPhase phase;
    private float nextNavigateTime;

    public int Player1Index => player1Index;
    public int Player2Index => player2Index;
    public bool Player1Confirmed => player1Confirmed;
    public bool Player2Confirmed => player2Confirmed;
    public bool ReadyToStart => player1Confirmed && player2Confirmed;
    public bool IsPlayer1Active => phase == SelectionPhase.Player1;

    private void Awake()
    {
        inputActions = new InputSystem_Actions();
        navigateAction = inputActions.UI.Navigate;
        submitAction = inputActions.UI.Submit;
        cancelAction = inputActions.UI.Cancel;
    }

    private void OnEnable()
    {
        inputActions.UI.Enable();
        navigateAction.performed += OnNavigate;
        submitAction.performed += OnSubmit;
        cancelAction.performed += OnCancel;
    }

    private void OnDisable()
    {
        navigateAction.performed -= OnNavigate;
        submitAction.performed -= OnSubmit;
        cancelAction.performed -= OnCancel;
        inputActions.UI.Disable();
    }

    private void Start()
    {
        InitializeSelections();
    }

    private void InitializeSelections()
    {
        int count = GetRosterCount();
        if (count <= 0)
        {
            Debug.LogWarning("CharacterSelectController has no roster entries.", this);
            return;
        }

        player1Index = WrapOrClamp(defaultPlayer1Index, count);
        player2Index = WrapOrClamp(defaultPlayer2Index, count);

        if (!allowSameCharacter && count > 1 && player2Index == player1Index)
            player2Index = WrapOrClamp(player2Index + 1, count);

        player1Confirmed = false;
        player2Confirmed = false;
        phase = SelectionPhase.Player1;
        nextNavigateTime = 0f;
    }

    private void OnNavigate(InputAction.CallbackContext context)
    {
        if (phase == SelectionPhase.Done)
            return;

        if (Time.unscaledTime < nextNavigateTime)
            return;

        Vector2 value = context.ReadValue<Vector2>();
        if (value.sqrMagnitude < navigationDeadzone * navigationDeadzone)
            return;

        int delta = ResolveNavigationDelta(value);
        if (delta == 0)
            return;

        ApplyNavigation(delta);
        nextNavigateTime = Time.unscaledTime + navigationRepeatDelay;
    }

    private int ResolveNavigationDelta(Vector2 value)
    {
        if (gridColumns > 0 && Mathf.Abs(value.y) > Mathf.Abs(value.x))
            return value.y > 0f ? -gridColumns : gridColumns;

        if (value.x > 0f)
            return 1;
        if (value.x < 0f)
            return -1;

        return 0;
    }

    private void ApplyNavigation(int delta)
    {
        int count = GetRosterCount();
        if (count <= 0)
            return;

        if (phase == SelectionPhase.Player1)
        {
            player1Index = WrapOrClamp(player1Index + delta, count);
            if (!allowSameCharacter && count > 1 && player1Index == player2Index)
                player1Index = WrapOrClamp(player1Index + delta, count);
        }
        else if (phase == SelectionPhase.Player2)
        {
            player2Index = WrapOrClamp(player2Index + delta, count);
            if (!allowSameCharacter && count > 1 && player2Index == player1Index)
                player2Index = WrapOrClamp(player2Index + delta, count);
        }
    }

    private void OnSubmit(InputAction.CallbackContext context)
    {
        if (GetRosterCount() <= 0)
            return;

        if (phase == SelectionPhase.Player1)
        {
            player1Confirmed = true;
            phase = SelectionPhase.Player2;
            return;
        }

        if (phase == SelectionPhase.Player2)
        {
            player2Confirmed = true;
            phase = SelectionPhase.Done;
            StartMatchIfReady();
            return;
        }

        StartMatchIfReady();
    }

    private void OnCancel(InputAction.CallbackContext context)
    {
        if (phase == SelectionPhase.Player2 && !player2Confirmed)
        {
            player1Confirmed = false;
            phase = SelectionPhase.Player1;
            return;
        }

        if (player2Confirmed)
        {
            player2Confirmed = false;
            phase = SelectionPhase.Player2;
            return;
        }

        if (player1Confirmed)
        {
            player1Confirmed = false;
            phase = SelectionPhase.Player1;
            return;
        }

        if (!string.IsNullOrWhiteSpace(backSceneName))
            SceneManager.LoadScene(backSceneName);
    }

    private void StartMatchIfReady()
    {
        if (!ReadyToStart)
            return;

        CharacterDefinition player1 = GetCharacter(player1Index);
        CharacterDefinition player2 = GetCharacter(player2Index);
        if (player1 == null || player2 == null)
        {
            Debug.LogWarning("Cannot start match with missing character definitions.", this);
            return;
        }

        MatchSetup.SetSelections(player1, player2);

        if (!string.IsNullOrWhiteSpace(matchSceneName))
            SceneManager.LoadScene(matchSceneName);
    }

    private CharacterDefinition GetCharacter(int index)
    {
        if (roster == null)
            return null;

        return roster.GetCharacter(index);
    }

    private int GetRosterCount()
    {
        return roster != null ? roster.CharacterCount : 0;
    }

    private int WrapOrClamp(int index, int count)
    {
        if (count <= 0)
            return 0;

        if (wrapSelection)
            return ((index % count) + count) % count;

        return Mathf.Clamp(index, 0, count - 1);
    }
}
