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
    private GameObject blockstunEffectInstance;
    private Transform blockstunEffectTransform;
    private Animator blockstunEffectAnimator;
    private Vector3 blockstunEffectBaseLocalScale = Vector3.one;
    private GameObject blockstunParticleInstance;
    private Transform blockstunParticleTransform;
    private Vector3 blockstunParticleBaseLocalScale = Vector3.one;
    private ParticleSystem[] blockstunParticleSystems;
    private bool wasInBlockstunLastFrame;
    private bool loggedMissingBlockstunAnimator;
    private bool loggedMissingBlockstunParticles;

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

        ResetBlockstunEffectInstance();
        ResetBlockstunParticleInstance();
    }

    public void Initialize(Fighter fighter)
    {
        this.fighter = fighter;
        EnsureBoxVisuals();
        EnsureBlockstunEffectInstance();
        EnsureBlockstunParticleInstance();

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
        EnsureBlockstunEffectInstance();
        EnsureBlockstunParticleInstance();
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
        UpdateBlockstunEffect();
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
        CacheVisualStateHash(FighterVisualState.Backdash);

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
        CacheMoveHash(MoveType.DragonPunchLight);
        CacheMoveHash(MoveType.DragonPunchMedium);
        CacheMoveHash(MoveType.DragonPunchHeavy);
        CacheMoveHash(MoveType.DownDownCharge);
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
            case FighterVisualState.Backdash:
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

    private void UpdateBlockstunEffect()
    {
        bool previewAlwaysVisible = config != null && config.previewBlockshieldAnim;
        bool inBlockstun = fighter.CurrentState == FighterState.Blockstun;
        bool showShieldEffect = previewAlwaysVisible || inBlockstun;
        if (!showShieldEffect)
        {
            if (blockstunEffectInstance != null && blockstunEffectInstance.activeSelf)
                blockstunEffectInstance.SetActive(false);
            if (blockstunParticleInstance != null && blockstunParticleInstance.activeSelf)
                blockstunParticleInstance.SetActive(false);
            wasInBlockstunLastFrame = false;
            return;
        }

        EnsureBlockstunEffectInstance();
        if (blockstunEffectInstance == null || blockstunEffectTransform == null)
            return;

        blockstunEffectTransform.localPosition = new Vector3(
            config.blockstunEffectOffsetFromFeet.x,
            config.blockstunEffectOffsetFromFeet.y,
            0f
        );
        blockstunEffectTransform.localScale = Vector3.Scale(
            blockstunEffectBaseLocalScale,
            config.blockstunEffectScaleMultiplier
        );

        if (!blockstunEffectInstance.activeSelf)
            blockstunEffectInstance.SetActive(true);

        UpdateBlockstunParticleTransform();
        // Particle supplement stays tied to real gameplay blockstun only; preview mode is for shield alignment.
        if (inBlockstun)
        {
            UpdateBlockstunParticleTransform();
            if (blockstunParticleInstance != null && !blockstunParticleInstance.activeSelf)
                blockstunParticleInstance.SetActive(true);
        }
        else if (blockstunParticleInstance != null && blockstunParticleInstance.activeSelf)
        {
            blockstunParticleInstance.SetActive(false);
        }

        if (!wasInBlockstunLastFrame)
            OnBlockstunEnter(previewAlwaysVisible);

        wasInBlockstunLastFrame = true;
    }

    private void OnBlockstunEnter(bool previewAlwaysVisible)
    {
        float speed;
        if (previewAlwaysVisible)
        {
            speed = 1f;
        }
        else
        {
            int stunFrames = Mathf.Max(1, fighter.LastReceivedStunFrames);
            float referenceFrames = Mathf.Max(1, config.blockstunEffectReferenceFrames);
            speed = referenceFrames / stunFrames;
            float minSpeed = Mathf.Max(0f, config.blockstunEffectMinSpeed);
            float maxSpeed = Mathf.Max(minSpeed, config.blockstunEffectMaxSpeed);
            speed = Mathf.Clamp(speed, minSpeed, maxSpeed);
        }

        if (blockstunEffectAnimator == null)
        {
            if (!loggedMissingBlockstunAnimator && blockstunEffectInstance != null)
            {
                loggedMissingBlockstunAnimator = true;
                Debug.LogWarning(
                    $"FighterView '{name}' blockstun effect prefab has no Animator. Assign an Animator-driven effect prefab.",
                    this
                );
            }
        }
        else
        {
            blockstunEffectAnimator.speed = speed;
            blockstunEffectAnimator.Play(0, 0, 0f);
        }

        if (!previewAlwaysVisible)
            ConfigureAndPlayBlockstunParticles(speed);
    }

    private void EnsureBlockstunEffectInstance()
    {
        if (blockstunEffectInstance != null)
            return;

        if (config == null || config.blockstunEffectPrefab == null)
            return;

        blockstunEffectInstance = Instantiate(config.blockstunEffectPrefab, transform);
        blockstunEffectInstance.name = $"{config.blockstunEffectPrefab.name}_Blockstun";
        blockstunEffectTransform = blockstunEffectInstance.transform;
        blockstunEffectBaseLocalScale = blockstunEffectTransform.localScale;
        blockstunEffectAnimator = blockstunEffectInstance.GetComponentInChildren<Animator>(true);
        loggedMissingBlockstunAnimator = false;
        blockstunEffectInstance.SetActive(false);
    }

    private void ResetBlockstunEffectInstance()
    {
        if (blockstunEffectInstance != null)
            Destroy(blockstunEffectInstance);

        blockstunEffectInstance = null;
        blockstunEffectTransform = null;
        blockstunEffectAnimator = null;
        blockstunEffectBaseLocalScale = Vector3.one;
        wasInBlockstunLastFrame = false;
        loggedMissingBlockstunAnimator = false;
    }

    private void UpdateBlockstunParticleTransform()
    {
        EnsureBlockstunParticleInstance();
        if (blockstunParticleInstance == null || blockstunParticleTransform == null)
            return;

        blockstunParticleTransform.localPosition = new Vector3(
            config.blockstunParticleOffsetFromFeet.x,
            config.blockstunParticleOffsetFromFeet.y,
            0f
        );
        blockstunParticleTransform.localScale = Vector3.Scale(
            blockstunParticleBaseLocalScale,
            config.blockstunParticleScaleMultiplier
        );
    }

    private void EnsureBlockstunParticleInstance()
    {
        if (blockstunParticleInstance != null)
            return;

        if (config == null || config.blockstunParticlePrefab == null)
            return;

        blockstunParticleInstance = Instantiate(config.blockstunParticlePrefab, transform);
        blockstunParticleInstance.name = $"{config.blockstunParticlePrefab.name}_BlockstunParticles";
        blockstunParticleTransform = blockstunParticleInstance.transform;
        blockstunParticleBaseLocalScale = blockstunParticleTransform.localScale;
        blockstunParticleSystems = blockstunParticleInstance.GetComponentsInChildren<ParticleSystem>(true);
        loggedMissingBlockstunParticles = false;
        blockstunParticleInstance.SetActive(false);
    }

    private void ResetBlockstunParticleInstance()
    {
        if (blockstunParticleInstance != null)
            Destroy(blockstunParticleInstance);

        blockstunParticleInstance = null;
        blockstunParticleTransform = null;
        blockstunParticleBaseLocalScale = Vector3.one;
        blockstunParticleSystems = null;
        loggedMissingBlockstunParticles = false;
    }

    private void ConfigureAndPlayBlockstunParticles(float baseBlockstunSpeed)
    {
        EnsureBlockstunParticleInstance();
        if (blockstunParticleInstance == null)
            return;

        if (blockstunParticleSystems == null || blockstunParticleSystems.Length == 0)
        {
            if (!loggedMissingBlockstunParticles)
            {
                loggedMissingBlockstunParticles = true;
                Debug.LogWarning(
                    $"FighterView '{name}' blockstun particle prefab has no ParticleSystem components.",
                    this
                );
            }
            return;
        }

        float speed = baseBlockstunSpeed * Mathf.Max(0f, config.blockstunParticleSpeedMultiplier);
        for (int i = 0; i < blockstunParticleSystems.Length; i++)
        {
            ParticleSystem ps = blockstunParticleSystems[i];
            if (ps == null)
                continue;

            ApplyBlockstunParticlePreset(ps, config.blockstunParticlePreset);
            ParticleSystem.MainModule main = ps.main;
            main.simulationSpeed = speed;
            ps.Clear(true);
            ps.Play(true);
        }
    }

    private static void ApplyBlockstunParticlePreset(ParticleSystem ps, BlockstunParticlePreset preset)
    {
        ParticleSystem.MainModule main = ps.main;
        ParticleSystem.EmissionModule emission = ps.emission;

        switch (preset)
        {
            case BlockstunParticlePreset.ShieldPulse:
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.35f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.65f);
                main.startColor = new Color(0.2f, 0.95f, 1f, 0.35f);
                emission.rateOverTime = 36f;
                break;
            case BlockstunParticlePreset.GuardSparks:
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 3.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
                main.startColor = new Color(1f, 0.92f, 0.45f, 0.8f);
                emission.rateOverTime = 70f;
                break;
            case BlockstunParticlePreset.HitMist:
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
                main.startColor = new Color(0.75f, 0.8f, 1f, 0.22f);
                emission.rateOverTime = 22f;
                break;
            case BlockstunParticlePreset.None:
            default:
                emission.rateOverTime = 0f;
                break;
        }
    }
}
