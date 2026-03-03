using System.Collections.Generic;
using UnityEngine;

// FighterView is responsible for visual representation of a Fighter.
// Animator state playback is command-driven from deterministic simulation snapshots.
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

    [Header("Animator State Names")]
    [SerializeField]
    private string idleStateName = "Idle";

    [SerializeField]
    private string crouchStateName = "Crouch";

    [SerializeField]
    private string walkForwardStateName = "Walk Forward";

    [SerializeField]
    private string walkBackwardStateName = "Base Layer.Walk Backward";

    [SerializeField]
    private string jumpStartupStateName = "Jumpsquat";

    [SerializeField]
    private string airborneStateName = "Airborne";

    [SerializeField]
    private string landingStateName = "Landing";

    [SerializeField]
    private string hitstunStateName = "Hitstun";

    [SerializeField]
    private string blockstunStateName = "Blockstun";

    [SerializeField]
    private string knockdownStateName = "Knockdown";

    [Header("Attack State Names")]
    [SerializeField]
    private string standingLightAttackStateName = "s_LP";

    [SerializeField]
    private string standingMediumAttackStateName = "s_MP";

    [SerializeField]
    private string standingHeavyAttackStateName = "s_HP";

    [SerializeField]
    private string jumpingLightAttackStateName = "j_LP";

    [SerializeField]
    private string jumpingMediumAttackStateName = "j_MP";

    [SerializeField]
    private string jumpingHeavyAttackStateName = "j_HP";

    [Header("Box Visuals")]
    [SerializeField]
    private Color hurtboxColor = new Color(0f, 1f, 0f, 0.75f);

    [SerializeField]
    private Color hitboxColor = new Color(1f, 0f, 0f, 0.75f);

    [SerializeField]
    private bool showBoxes = true;

    private DebugBoxVisual hurtboxVisual;
    private DebugBoxVisual hitboxVisual;

    private readonly HashSet<string> reportedMissingStateNames = new HashSet<string>();

    private int idleHash;
    private int crouchHash;
    private int walkForwardHash;
    private int walkBackwardHash;
    private int jumpStartupHash;
    private int airborneHash;
    private int landingHash;
    private int hitstunHash;
    private int blockstunHash;
    private int knockdownHash;

    private int standingLightAttackHash;
    private int standingMediumAttackHash;
    private int standingHeavyAttackHash;
    private int jumpingLightAttackHash;
    private int jumpingMediumAttackHash;
    private int jumpingHeavyAttackHash;

    private int lastPlayedHash = -1;
    private uint lastAnimationSerial;

    public FighterConfig Config => config;

    public void Initialize(Fighter fighter)
    {
        this.fighter = fighter;
        EnsureBoxVisuals();

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

        SetFacing(fighter.FacingRight);
        UpdateBoxVisuals();
    }

    private void CacheAnimationHashes()
    {
        idleHash = CacheHash(idleStateName);
        crouchHash = CacheHash(crouchStateName);
        walkForwardHash = CacheHash(walkForwardStateName);
        walkBackwardHash = CacheHash(walkBackwardStateName);
        jumpStartupHash = CacheHash(jumpStartupStateName);
        airborneHash = CacheHash(airborneStateName);
        landingHash = CacheHash(landingStateName);
        hitstunHash = CacheHash(hitstunStateName);
        blockstunHash = CacheHash(blockstunStateName);
        knockdownHash = CacheHash(knockdownStateName);

        standingLightAttackHash = CacheHash(standingLightAttackStateName);
        standingMediumAttackHash = CacheHash(standingMediumAttackStateName);
        standingHeavyAttackHash = CacheHash(standingHeavyAttackStateName);
        jumpingLightAttackHash = CacheHash(jumpingLightAttackStateName);
        jumpingMediumAttackHash = CacheHash(jumpingMediumAttackStateName);
        jumpingHeavyAttackHash = CacheHash(jumpingHeavyAttackStateName);
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

    private int GetAnimationHash(FighterRenderSnapshot snapshot)
    {
        switch (snapshot.visualState)
        {
            case FighterVisualState.Idle:
                return idleHash;
            case FighterVisualState.Crouching:
                return crouchHash != 0 ? crouchHash : idleHash;
            case FighterVisualState.WalkForward:
                return walkForwardHash;
            case FighterVisualState.WalkBackward:
                return walkBackwardHash;
            case FighterVisualState.JumpStartup:
                return jumpStartupHash;
            case FighterVisualState.Airborne:
                return airborneHash;
            case FighterVisualState.Landing:
                return landingHash;
            case FighterVisualState.Hitstun:
                return hitstunHash;
            case FighterVisualState.Blockstun:
                return blockstunHash;
            case FighterVisualState.Knockdown:
                return knockdownHash;
            case FighterVisualState.AttackStartup:
                return GetAttackHash(snapshot.attackType, snapshot.attackIsAirborne);
            case FighterVisualState.AttackActive:
                return GetAttackHash(snapshot.attackType, snapshot.attackIsAirborne);
            case FighterVisualState.AttackRecovery:
                return GetAttackHash(snapshot.attackType, snapshot.attackIsAirborne);
            default:
                ReportMissingStateOnce($"No animation mapped for visual state '{snapshot.visualState}'.");
                return 0;
        }
    }

    private int GetAttackHash(AttackType attackType, bool isAirborne)
    {
        if (attackType == AttackType.Light)
            return isAirborne ? jumpingLightAttackHash : standingLightAttackHash;

        if (attackType == AttackType.Medium)
            return isAirborne ? jumpingMediumAttackHash : standingMediumAttackHash;

        if (attackType == AttackType.Heavy)
            return isAirborne ? jumpingHeavyAttackHash : standingHeavyAttackHash;

        ReportMissingStateOnce($"No attack animation mapped for attack type '{attackType}'.");
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
