using System.Collections.Generic;
using UnityEngine;

// FighterView is responsible for visual representation of a Fighter.
// Animator state playback is command-driven from deterministic simulation snapshots.
public class FighterView : MonoBehaviour
{
    private static Transform debugBoxRoot;
    public static bool GlobalShowBoxes { get; set; } = false;

    [Header("Config")]
    [SerializeField]
    private FighterConfig config;

    [SerializeField]
    private float depth = 0f;

    [Header("References")]
    [SerializeField]
    private Animator animator;

    [SerializeField]
    private FighterShadow fighterShadow;

    public Animator Animator => animator;
    private Fighter fighter;

    [Header("Box Visuals")]
    [SerializeField]
    private Color hurtboxColor = new Color(0f, 1f, 0f, 0.75f);

    [SerializeField]
    private Color blockingHurtboxColor = new Color(0.05f, 0.2f, 0.6f, 0.75f);

    [SerializeField]
    private Color hitboxColor = new Color(1f, 0f, 0f, 0.75f);

    [SerializeField]
    private bool showBoxes = true;

    [Header("Debug")]
    [SerializeField]
    private bool logVisualStateChanges;

    private DebugBoxVisual hurtboxVisual;
    private DebugBoxVisual hitboxVisual;

    private readonly HashSet<string> reportedMissingStateNames = new HashSet<string>();

    private readonly Dictionary<FighterVisualState, int> visualStateHashes = new Dictionary<FighterVisualState, int>();
    private readonly Dictionary<MoveType, int> moveHashes = new Dictionary<MoveType, int>();

    private int lastPlayedHash = -1;
    private uint lastAnimationSerial;
    private FighterVisualState lastLoggedVisualState = FighterVisualState.None;
    private int lastLoggedVisualHash = -1;

    public FighterConfig Config => config;

    public void ApplyCharacterDefinition(CharacterDefinition characterDefinition)
    {
        if (characterDefinition == null)
        {
            Debug.LogWarning("FighterView received a null character definition.", this);
            return;
        }

        config = characterDefinition.fighterConfig;

        if (animator != null)
            animator.runtimeAnimatorController = characterDefinition.animatorController;

        if (fighterShadow == null)
            fighterShadow = GetComponentInChildren<FighterShadow>();

        if (fighterShadow != null)
            fighterShadow.ApplyPresentationConfig(characterDefinition.presentationConfig);
    }

    public void Initialize(Fighter fighter)
    {
        this.fighter = fighter;
        EnsureBoxVisuals();

        if (config == null)
        {
            Debug.LogWarning("FighterView is missing fighter config.");
            return;
        }

        if (animator == null)
        {
            Debug.LogWarning("FighterView is missing animator reference.");
            return;
        }

        CacheAnimationHashes();
        lastPlayedHash = -1;
        lastAnimationSerial = 0;
    }

    public void SetPosition(Vector2 simPosition)
    {
        transform.position = new Vector3(simPosition.x, simPosition.y, depth);
    }

    public void SetFacing(bool facingRight)
    {
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (facingRight ? 1 : -1);
        transform.localScale = scale;
    }

    private void Start()
    {
        EnsureBoxVisuals();
        hurtboxVisual.SetVisible(false);
        hitboxVisual.SetVisible(false);
    }

    private void Update()
    {
        if (fighter == null)
        {
            Debug.LogWarning("FighterView is missing fighter reference.");
            return;
        }

        if (animator == null)
            Debug.LogWarning("FighterView is missing animator reference.");
        else
            UpdateAnimationPlayback();

        SetPosition(fighter.Position);
        SetFacing(fighter.FacingRight);
        UpdateBoxVisuals();
    }

    private void CacheAnimationHashes()
    {
        visualStateHashes.Clear();
        moveHashes.Clear();

        CacheVisualStateHash(FighterVisualState.Idle);
        CacheVisualStateHash(FighterVisualState.CrouchTransition);
        CacheVisualStateHash(FighterVisualState.Crouching);
        CacheVisualStateHash(FighterVisualState.WalkForward);
        CacheVisualStateHash(FighterVisualState.WalkBackward);
        CacheVisualStateHash(FighterVisualState.JumpStartup);
        CacheVisualStateHash(FighterVisualState.Airborne);
        CacheVisualStateHash(FighterVisualState.Landing);
        CacheVisualStateHash(FighterVisualState.Hitstun);
        CacheVisualStateHash(FighterVisualState.Blockstun);
        CacheVisualStateHash(FighterVisualState.Knockdown);

        CacheMoveHash(MoveType.StandingLight);
        CacheMoveHash(MoveType.StandingMedium);
        CacheMoveHash(MoveType.StandingHeavy);
        CacheMoveHash(MoveType.CrouchingLight);
        CacheMoveHash(MoveType.CrouchingMedium);
        CacheMoveHash(MoveType.CrouchingHeavy);
        CacheMoveHash(MoveType.JumpingLight);
        CacheMoveHash(MoveType.JumpingMedium);
        CacheMoveHash(MoveType.JumpingHeavy);
        CacheMoveHash(MoveType.FireballLight);
        CacheMoveHash(MoveType.FireballMedium);
        CacheMoveHash(MoveType.FireballHeavy);
    }

    private void CacheVisualStateHash(FighterVisualState visualState)
    {
        visualStateHashes[visualState] = CacheHash(config.GetAnimationStateName(visualState));
    }

    private void CacheMoveHash(MoveType moveType)
    {
        moveHashes[moveType] = CacheHash(config.GetAnimationStateName(moveType));
    }

    private int CacheHash(string stateName)
    {
        if (string.IsNullOrWhiteSpace(stateName))
            return 0;

        string trimmedName = stateName.Trim();
        string[] candidates;
        if (trimmedName.StartsWith("Base Layer."))
        {
            candidates = new string[] { trimmedName };
        }
        else
        {
            candidates = new string[]
            {
                trimmedName,
                $"Base Layer.{trimmedName}",
                $"Base Layer.Grounded.{trimmedName}",
                $"Base Layer.Airborne.{trimmedName}",
            };
        }

        for (int i = 0; i < candidates.Length; i++)
        {
            int candidateHash = Animator.StringToHash(candidates[i]);
            if (animator.HasState(0, candidateHash))
                return candidateHash;
        }

        ReportMissingStateOnce(
            $"Animator state '{trimmedName}' was not found in layer 0. Tried: {string.Join(", ", candidates)}"
        );
        return 0;
    }

    private void UpdateAnimationPlayback()
    {
        FighterRenderSnapshot snapshot = fighter.RenderSnapshot;
        animator.speed = snapshot.freezeAnimation ? 0f : 1f;

        int targetHash = GetAnimationHash(snapshot);
        LogVisualStateChangeIfNeeded(snapshot, targetHash);
        if (targetHash == 0)
            return;

        bool shouldPlay =
            snapshot.restartAnimation
            || snapshot.animationSerial != lastAnimationSerial
            || targetHash != lastPlayedHash;

        if (!shouldPlay)
            return;

        animator.Play(targetHash, 0, 0f);
        lastPlayedHash = targetHash;
        lastAnimationSerial = snapshot.animationSerial;
    }

    private void LogVisualStateChangeIfNeeded(FighterRenderSnapshot snapshot, int targetHash)
    {
        if (!logVisualStateChanges)
            return;

        if (snapshot.visualState == lastLoggedVisualState && targetHash == lastLoggedVisualHash)
            return;

        Debug.Log(
            $"[FighterView] {name} visualState={snapshot.visualState} hash={targetHash} " +
            $"moveType={snapshot.moveType}",
            this
        );
        lastLoggedVisualState = snapshot.visualState;
        lastLoggedVisualHash = targetHash;
    }

    private int GetAnimationHash(FighterRenderSnapshot snapshot)
    {
        switch (snapshot.visualState)
        {
            case FighterVisualState.Idle:
            case FighterVisualState.CrouchTransition:
            case FighterVisualState.Crouching:
            case FighterVisualState.WalkForward:
            case FighterVisualState.WalkBackward:
            case FighterVisualState.JumpStartup:
            case FighterVisualState.Airborne:
            case FighterVisualState.Landing:
            case FighterVisualState.Hitstun:
            case FighterVisualState.Blockstun:
            case FighterVisualState.Knockdown:
                return GetVisualStateHash(snapshot.visualState);
            case FighterVisualState.Attacking:
                return GetMoveHash(snapshot.moveType);
            default:
                ReportMissingStateOnce($"No animation mapped for visual state '{snapshot.visualState}'.");
                return 0;
        }
    }

    private int GetVisualStateHash(FighterVisualState visualState)
    {
        if (visualStateHashes.TryGetValue(visualState, out int hash))
            return hash;

        ReportMissingStateOnce($"No animation mapped for visual state '{visualState}'.");
        return 0;
    }

    private int GetMoveHash(MoveType moveType)
    {
        if (moveHashes.TryGetValue(moveType, out int hash))
            return hash;

        ReportMissingStateOnce($"No animation mapped for move type '{moveType}'.");
        return 0;
    }

    private void ReportMissingStateOnce(string message)
    {
        if (reportedMissingStateNames.Contains(message))
            return;

        reportedMissingStateNames.Add(message);
        Debug.LogError($"{name}: {message}", this);
    }

    private void EnsureBoxVisuals()
    {
        if (hurtboxVisual == null)
            hurtboxVisual = CreateBoxVisual("HurtboxVisual", hurtboxColor);
        if (hitboxVisual == null)
            hitboxVisual = CreateBoxVisual("HitboxVisual", hitboxColor);
    }

    private DebugBoxVisual CreateBoxVisual(string visualName, Color color)
    {
        GameObject visualObject = new GameObject(visualName);
        visualObject.transform.SetParent(GetOrCreateDebugBoxRoot(), false);

        DebugBoxVisual visual = visualObject.AddComponent<DebugBoxVisual>();
        visual.Initialize(color);
        visual.SetVisible(false);
        return visual;
    }

    private Transform GetOrCreateDebugBoxRoot()
    {
        if (debugBoxRoot != null)
            return debugBoxRoot;

        GameObject rootObject = GameObject.Find("DebugBoxRoot");
        if (rootObject == null)
            rootObject = new GameObject("DebugBoxRoot");

        debugBoxRoot = rootObject.transform;
        return debugBoxRoot;
    }

    private void UpdateBoxVisuals()
    {
        EnsureBoxVisuals();
        Box hurtbox = fighter.CurrentHurtbox;
        Hitbox hitbox = fighter.CurrentHitbox;

        if (showBoxes && GlobalShowBoxes)
        {
            hurtboxVisual.SetBox(hurtbox);
            bool showBlockingHurtbox = fighter.IsHoldingBlockInput && fighter.CanCurrentlyBlock;
            hurtboxVisual.SetColor(showBlockingHurtbox ? blockingHurtboxColor : hurtboxColor);
            hurtboxVisual.SetVisible(true);

            if (hitbox.active)
            {
                hitboxVisual.SetBox(hitbox.box);
                hitboxVisual.SetVisible(true);
            }
            else
            {
                hitboxVisual.SetVisible(false);
            }
        }
        else
        {
            hurtboxVisual.SetVisible(false);
            hitboxVisual.SetVisible(false);
        }
    }
}
