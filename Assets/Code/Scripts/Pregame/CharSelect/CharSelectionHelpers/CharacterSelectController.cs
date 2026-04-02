using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class CharacterSelectController : MonoBehaviour
{
    private enum SelectionPhase
    {
        Player1,
        Player2,
        Done
    }

    public enum CursorState
    {
        Idle,
        Moving,
        Selected
    }

    [Header("Roster")]
    [Tooltip("Roster data source used to resolve character definitions by slot index.")]
    [SerializeField]
    private CharacterSelectRoster roster;

    [Tooltip("UI/world anchors representing character slot positions for cursor snapping and hover checks.")]
    [SerializeField]
    private Transform[] slotAnchors;

    [Header("Defaults")]
    [Tooltip("Initial slot index for Player 1 when character select opens.")]
    [SerializeField]
    private int defaultPlayer1Index;

    [Tooltip("Initial slot index for Player 2 when character select opens.")]
    [SerializeField]
    private int defaultPlayer2Index = 1;

    [Header("Selection Rules")]
    [Tooltip("If disabled, Player 2 cannot lock in the same slot currently chosen by Player 1.")]
    [SerializeField]
    private bool allowSameCharacter = true;

    [Tooltip("Maximum world-space distance from a slot where submit can lock in.")]
    [SerializeField]
    private float selectionSnapRadius = 120f;

    [Tooltip("Small world-space radius used for hover highlighting. Keeps overlap checks precise.")]
    [SerializeField]
    private float hoverSnapRadius = 24f;

    [Header("Input")]
    [Tooltip("Minimum stick magnitude required before cursor movement input is accepted.")]
    [SerializeField]
    private float navigationDeadzone = 0.5f;

    [Tooltip("Cursor movement speed in world-space units per second.")]
    [SerializeField]
    private float cursorMoveSpeed = 70f;

    [Header("Scene")]
    [Tooltip("Scene to load after both players are locked in.")]
    [SerializeField]
    private string matchSceneName = "Training Room";

    [Tooltip("Optional scene to return to when cancel is pressed with no active confirmations.")]
    [SerializeField]
    private string backSceneName = "";

    [Header("Audio")]
    [Tooltip("Placeholder stage theme until stage-specific themes are added.")]
    [SerializeField]
    private AudioClip stageTheme;

    private InputSystem_Actions inputActions;
    private InputAction navigateAction;
    private InputAction submitAction;
    private InputAction cancelAction;

    private int player1Index;
    private int player2Index;
    private bool player1Confirmed;
    private bool player2Confirmed;
    private SelectionPhase phase;
    private Vector3 player1CursorPosition;
    private Vector3 player2CursorPosition;
    private CursorState player1CursorState;
    private CursorState player2CursorState;
    private bool isInitialized;

    public int Player1Index => player1Index;
    public int Player2Index => player2Index;
    public bool Player1Confirmed => player1Confirmed;
    public bool Player2Confirmed => player2Confirmed;
    public bool ReadyToStart => player1Confirmed && player2Confirmed;
    public bool IsPlayer1Active => phase == SelectionPhase.Player1;
    public Vector3 Player1CursorPosition => player1CursorPosition;
    public Vector3 Player2CursorPosition => player2CursorPosition;
    public CursorState Player1CursorVisualState => player1CursorState;
    public CursorState Player2CursorVisualState => player2CursorState;
    public int ActiveHoveredSlotIndex => GetActiveHoveredSlotIndex();

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
        submitAction.performed += OnSubmit;
        cancelAction.performed += OnCancel;
    }

    private void OnDisable()
    {
        submitAction.performed -= OnSubmit;
        cancelAction.performed -= OnCancel;
        inputActions.UI.Disable();
    }

    private void Start()
    {
        TryInitializeSelections();
    }

    private void Update()
    {
        if (!isInitialized)
            TryInitializeSelections();

        UpdateActiveCursor();
    }

    public void SetSlotAnchors(Transform[] anchors)
    {
        slotAnchors = anchors;
        if (!isInitialized)
            TryInitializeSelections();
    }

    private void TryInitializeSelections()
    {
        if (!HasAnySelectableSlot())
            return;

        player1Index = ResolveNearestValidIndex(defaultPlayer1Index, -1);
        player2Index = ResolveNearestValidIndex(defaultPlayer2Index, player1Index);

        if (player1Index < 0)
            player1Index = FindFirstSelectableIndex(-1);

        if (player2Index < 0)
            player2Index = FindFirstSelectableIndex(player1Index);

        if (player1Index < 0 || player2Index < 0)
            return;

        player1CursorPosition = GetSlotPosition(player1Index);
        player2CursorPosition = GetSlotPosition(player2Index);

        player1Confirmed = false;
        player2Confirmed = false;
        phase = SelectionPhase.Player1;

        player1CursorState = CursorState.Idle;
        player2CursorState = CursorState.Idle;
        isInitialized = true;
    }

    private void UpdateActiveCursor()
    {
        if (phase == SelectionPhase.Done)
            return;

        bool player1Active = phase == SelectionPhase.Player1;
        Vector2 navigation = navigateAction.ReadValue<Vector2>();
        bool isMoving = navigation.sqrMagnitude >= navigationDeadzone * navigationDeadzone;

        if (player1Active)
            UpdateCursorForPlayer(ref player1CursorPosition, ref player1Index, ref player1CursorState, navigation, isMoving, -1);
        else
            UpdateCursorForPlayer(ref player2CursorPosition, ref player2Index, ref player2CursorState, navigation, isMoving, !allowSameCharacter ? player1Index : -1);
    }

    private void UpdateCursorForPlayer(
        ref Vector3 cursorPosition,
        ref int selectedIndex,
        ref CursorState state,
        Vector2 navigation,
        bool isMoving,
        int blockedIndex)
    {
        if (state == CursorState.Selected)
            return;

        if (isMoving)
        {
            cursorPosition += new Vector3(navigation.x, navigation.y, 0f) * (cursorMoveSpeed * Time.unscaledDeltaTime);
            state = CursorState.Moving;
        }
        else
        {
            state = CursorState.Idle;
        }

        int nearestIndex = FindClosestSelectableSlotIndex(cursorPosition, blockedIndex);
        if (nearestIndex >= 0)
            selectedIndex = nearestIndex;
    }

    private int FindClosestSelectableSlotIndex(Vector3 cursorPosition, int blockedIndex)
    {
        return FindClosestSelectableSlotIndex(cursorPosition, blockedIndex, selectionSnapRadius);
    }

    private int FindClosestSelectableSlotIndex(Vector3 cursorPosition, int blockedIndex, float maxRadius)
    {
        if (slotAnchors == null || slotAnchors.Length == 0)
            return -1;

        int bestIndex = -1;
        float bestDistanceSqr = float.MaxValue;
        float radius = Mathf.Max(0f, maxRadius);
        float maxDistanceSqr = radius * radius;

        for (int i = 0; i < slotAnchors.Length; i++)
        {
            if (!IsSelectableSlot(i, blockedIndex))
                continue;

            Transform anchor = slotAnchors[i];
            if (anchor == null)
                continue;

            float distanceSqr = (cursorPosition - anchor.position).sqrMagnitude;
            if (distanceSqr > maxDistanceSqr)
                continue;

            if (distanceSqr >= bestDistanceSqr)
                continue;

            bestDistanceSqr = distanceSqr;
            bestIndex = i;
        }

        return bestIndex;
    }

    private void OnSubmit(InputAction.CallbackContext context)
    {
        if (!isInitialized)
            TryInitializeSelections();

        if (!HasAnySelectableSlot())
            return;

        if (phase == SelectionPhase.Player1)
        {
            if (!TryGetLockableSelectionIndex(true, out int lockIndex))
                return;

            player1Index = lockIndex;
            player1Confirmed = true;
            player1CursorState = CursorState.Selected;
            phase = SelectionPhase.Player2;
            player2CursorState = CursorState.Idle;
            return;
        }

        if (phase == SelectionPhase.Player2)
        {
            if (!TryGetLockableSelectionIndex(false, out int lockIndex))
                return;

            player2Index = lockIndex;
            player2Confirmed = true;
            player2CursorState = CursorState.Selected;
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
            player1CursorState = CursorState.Idle;
            phase = SelectionPhase.Player1;
            return;
        }

        if (player2Confirmed)
        {
            player2Confirmed = false;
            player2CursorState = CursorState.Idle;
            phase = SelectionPhase.Player2;
            return;
        }

        if (player1Confirmed)
        {
            player1Confirmed = false;
            player1CursorState = CursorState.Idle;
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

        AudioClip battleMusic = ResolveBattleMusicTrack(player1, player2);
        MatchSetup.SetSelections(player1, player2, battleMusic);

        if (!string.IsNullOrWhiteSpace(matchSceneName))
            SceneManager.LoadScene(matchSceneName);
    }

    private CharacterDefinition GetCharacter(int index)
    {
        if (roster == null)
            return null;

        return roster.GetCharacter(index);
    }

    private bool TryGetLockableSelectionIndex(bool forPlayer1, out int index)
    {
        Vector3 cursorPosition = forPlayer1 ? player1CursorPosition : player2CursorPosition;
        int blockedIndex = (!allowSameCharacter && !forPlayer1) ? player1Index : -1;

        index = FindClosestSelectableSlotIndex(cursorPosition, blockedIndex);
        return index >= 0;
    }

    private int GetActiveHoveredSlotIndex()
    {
        if (phase == SelectionPhase.Done)
            return -1;

        bool player1Active = phase == SelectionPhase.Player1;
        Vector3 cursorPosition = player1Active ? player1CursorPosition : player2CursorPosition;
        int blockedIndex = (!allowSameCharacter && !player1Active) ? player1Index : -1;
        return FindClosestSelectableSlotIndex(cursorPosition, blockedIndex, hoverSnapRadius);
    }

    private int ResolveNearestValidIndex(int desiredIndex, int blockedIndex)
    {
        if (slotAnchors == null || slotAnchors.Length == 0)
            return -1;

        if (IsSelectableSlot(desiredIndex, blockedIndex))
            return desiredIndex;

        int count = slotAnchors.Length;
        for (int offset = 1; offset < count; offset++)
        {
            int forward = desiredIndex + offset;
            if (forward >= 0 && forward < count && IsSelectableSlot(forward, blockedIndex))
                return forward;

            int backward = desiredIndex - offset;
            if (backward >= 0 && backward < count && IsSelectableSlot(backward, blockedIndex))
                return backward;
        }

        return FindFirstSelectableIndex(blockedIndex);
    }

    private int FindFirstSelectableIndex(int blockedIndex)
    {
        if (slotAnchors == null)
            return -1;

        for (int i = 0; i < slotAnchors.Length; i++)
        {
            if (IsSelectableSlot(i, blockedIndex))
                return i;
        }

        return -1;
    }

    private bool HasAnySelectableSlot()
    {
        return FindFirstSelectableIndex(-1) >= 0;
    }

    private bool IsSelectableSlot(int index, int blockedIndex)
    {
        if (index < 0 || slotAnchors == null || index >= slotAnchors.Length)
            return false;

        if (slotAnchors[index] == null)
            return false;

        if (index == blockedIndex)
            return false;

        return GetCharacter(index) != null;
    }

    private Vector3 GetSlotPosition(int index)
    {
        if (slotAnchors == null || index < 0 || index >= slotAnchors.Length || slotAnchors[index] == null)
            return Vector3.zero;

        return slotAnchors[index].position;
    }

    private AudioClip ResolveBattleMusicTrack(CharacterDefinition player1, CharacterDefinition player2)
    {
        List<AudioClip> candidates = new List<AudioClip>(3);
        AddClipIfAssigned(candidates, stageTheme);
        AddClipIfAssigned(candidates, player1 != null ? player1.characterTheme : null);
        AddClipIfAssigned(candidates, player2 != null ? player2.characterTheme : null);

        if (candidates.Count == 0)
            return null;

        int randomIndex = Random.Range(0, candidates.Count);
        return candidates[randomIndex];
    }

    private static void AddClipIfAssigned(List<AudioClip> clips, AudioClip clip)
    {
        if (clip == null)
            return;

        clips.Add(clip);
    }
}
