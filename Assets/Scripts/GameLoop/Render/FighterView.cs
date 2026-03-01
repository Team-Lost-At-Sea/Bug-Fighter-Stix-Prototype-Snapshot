using System.Collections.Generic;
using UnityEngine;

// Fighterview is responsible for visual representation of a Fighter,
// including syncing Animator parameters with the Fighter's simulation state.
// It represents what is seen in the Render stage of the game loop for a single fighter character.
public class FighterView : MonoBehaviour
{
    private static Transform debugBoxRoot;

    [Header("Config")]
    [SerializeField]
    private FighterConfig config;

    [SerializeField]
    private float depth = 0f;

    [Header("References")]
    [SerializeField]
    private Animator animator;
    public Animator Animator => animator;
    private Fighter fighter;

    [Header("Box Visuals")]
    [SerializeField]
    private Color hurtboxColor = new Color(0f, 1f, 0f, 0.25f);

    [SerializeField]
    private Color hitboxColor = new Color(1f, 0f, 0f, 0.25f);

    private DebugBoxVisual hurtboxVisual;
    private DebugBoxVisual hitboxVisual;

    [SerializeField]
    private bool showBoxes = true;

    // Cache animator params by hash so updates can validate existence and type in O(1).
    private readonly Dictionary<int, AnimatorControllerParameterType> animatorParameterTypes =
        new Dictionary<int, AnimatorControllerParameterType>();

    // Tracks already-reported issues so missing/mismatched parameters only log once.
    private readonly HashSet<int> reportedAnimatorParameterIssues = new HashSet<int>();

    public FighterConfig Config => config;

    public void Initialize(Fighter fighter)
    {
        this.fighter = fighter;
        EnsureBoxVisuals();

        if (animator == null)
            Debug.LogWarning("FighterView is missing animator reference.");
        else
            CacheAnimatorParameters();
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

    void Update()
    {
        if (fighter == null)
        {
            Debug.LogWarning("FighterView is missing fighter reference.");
            return;
        }

        if (animator == null)
            Debug.LogWarning("FighterView is missing animator reference.");
        else
            UpdateAnimatorParameters();

        SetFacing(fighter.FacingRight);
        UpdateBoxVisuals();
    }

    private void UpdateAnimatorParameters()
    {
        // Keep Animator in sync with simulation state each frame.
        SetFloatIfExists("ForwardMoveSpeed", fighter.ForwardMoveSpeed);
        SetFloatIfExists("VerticalSpeed", fighter.VerticalSpeed);
        SetBoolIfExists("FacingRight", fighter.FacingRight);
        SetBoolIfExists("IsGrounded", fighter.IsGrounded);
        SetBoolIfExists("IsInPrejump", fighter.IsInPrejump);
        SetIntegerIfExists("PrejumpFramesRemaining", fighter.PrejumpFramesRemaining);
        SetBoolIfExists("JustJumped", fighter.JustJumped);
        SetBoolIfExists("JustBecameAirborne", fighter.JustBecameAirborne);
        SetBoolIfExists("IsInLandingRecovery", fighter.IsInLandingRecovery);
        SetIntegerIfExists("LandingRecoveryFramesRemaining", fighter.LandingRecoveryFramesRemaining);
        SetBoolIfExists("JustLanded", fighter.JustLanded);
        SetIntegerIfExists("ActiveAttackType", (int)fighter.CurrentAttack);
        SetBoolIfExists("AttackTriggered", fighter.AttackTriggered);
        SetIntegerIfExists("ControlState", (int)fighter.CurrentControlState);
    }

    private void CacheAnimatorParameters()
    {
        animatorParameterTypes.Clear();
        reportedAnimatorParameterIssues.Clear();

        // Snapshot current Animator parameter schema for validation during updates.
        foreach (AnimatorControllerParameter parameter in animator.parameters)
            animatorParameterTypes[parameter.nameHash] = parameter.type;
    }

    private bool TryValidateAnimatorParameter(
        string parameterName,
        AnimatorControllerParameterType expectedType
    )
    {
        int parameterHash = Animator.StringToHash(parameterName);

        // Re-cache once on miss/type mismatch to handle runtime controller swaps
        // and parameter edits while in play mode.
        if (!animatorParameterTypes.TryGetValue(parameterHash, out AnimatorControllerParameterType actualType))
        {
            CacheAnimatorParameters();
            if (!animatorParameterTypes.TryGetValue(parameterHash, out actualType))
            {
                ReportAnimatorParameterIssueOnce(
                    parameterName,
                    $"Missing Animator parameter '{parameterName}' (expected type: {expectedType}) on controller '{animator.runtimeAnimatorController?.name}'."
                );
                return false;
            }
        }

        if (actualType != expectedType)
        {
            CacheAnimatorParameters();
            if (
                animatorParameterTypes.TryGetValue(parameterHash, out actualType)
                && actualType != expectedType
            )
            {
                ReportAnimatorParameterIssueOnce(
                    parameterName,
                    $"Animator parameter '{parameterName}' has type {actualType}, expected {expectedType} on controller '{animator.runtimeAnimatorController?.name}'."
                );
                return false;
            }
        }

        return true;
    }

    private void ReportAnimatorParameterIssueOnce(string parameterName, string message)
    {
        // De-duplicate log entries to avoid frame-by-frame spam.
        int issueHash = Animator.StringToHash(parameterName + message);
        if (reportedAnimatorParameterIssues.Contains(issueHash))
            return;

        reportedAnimatorParameterIssues.Add(issueHash);
        Debug.LogError($"{name}: {message}", this);
    }

    private void SetBoolIfExists(string parameterName, bool value)
    {
        if (TryValidateAnimatorParameter(parameterName, AnimatorControllerParameterType.Bool))
            animator.SetBool(parameterName, value);
    }

    private void SetFloatIfExists(string parameterName, float value)
    {
        if (TryValidateAnimatorParameter(parameterName, AnimatorControllerParameterType.Float))
            animator.SetFloat(parameterName, value);
    }

    private void SetIntegerIfExists(string parameterName, int value)
    {
        if (TryValidateAnimatorParameter(parameterName, AnimatorControllerParameterType.Int))
            animator.SetInteger(parameterName, value);
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

        if (showBoxes)
        {
            hurtboxVisual.SetBox(hurtbox);
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
