using UnityEngine;
using System.Collections.Generic;
using System;

// Simulation.cs is the core of the game loop, responsible for updating the state of 
// all fighters and handling interactions between them.
// It encapsulates the main fighting match simulation logic for the game loop.
public class Simulation : ISimulationCore
{
    public readonly struct ProjectileSnapshot
    {
        public readonly int id;
        public readonly int ownerSlot;
        public readonly Vector2 position;
        public readonly Vector2 velocity;
        public readonly Vector2 halfSize;
        public readonly int lifetimeFramesRemaining;
        public readonly int damage;
        public readonly int hitstunFrames;
        public readonly int blockstunFrames;
        public readonly float blockPushback;
        public readonly int chipDamage;
        public readonly int attackerBlockstopFrames;
        public readonly HitLevel hitLevel;
        public readonly bool active;

        public ProjectileSnapshot(
            int id,
            int ownerSlot,
            Vector2 position,
            Vector2 velocity,
            Vector2 halfSize,
            int lifetimeFramesRemaining,
            int damage,
            int hitstunFrames,
            int blockstunFrames,
            float blockPushback,
            int chipDamage,
            int attackerBlockstopFrames,
            HitLevel hitLevel,
            bool active
        )
        {
            this.id = id;
            this.ownerSlot = ownerSlot;
            this.position = position;
            this.velocity = velocity;
            this.halfSize = halfSize;
            this.lifetimeFramesRemaining = lifetimeFramesRemaining;
            this.damage = damage;
            this.hitstunFrames = hitstunFrames;
            this.blockstunFrames = blockstunFrames;
            this.blockPushback = blockPushback;
            this.chipDamage = chipDamage;
            this.attackerBlockstopFrames = attackerBlockstopFrames;
            this.hitLevel = hitLevel;
            this.active = active;
        }
    }

    public readonly struct CombatInteractionSnapshot
    {
        public readonly int frame;
        public readonly int attackerPlayerId;
        public readonly int defenderPlayerId;
        public readonly HitLevel hitLevel;
        public readonly HitResultType resultType;
        public readonly int stunFrames;
        public readonly int chipDamage;
        public readonly float pushback;
        public readonly int attackerAdvantageEstimate;

        public CombatInteractionSnapshot(
            int frame,
            int attackerPlayerId,
            int defenderPlayerId,
            HitLevel hitLevel,
            HitResultType resultType,
            int stunFrames,
            int chipDamage,
            float pushback,
            int attackerAdvantageEstimate
        )
        {
            this.frame = frame;
            this.attackerPlayerId = attackerPlayerId;
            this.defenderPlayerId = defenderPlayerId;
            this.hitLevel = hitLevel;
            this.resultType = resultType;
            this.stunFrames = stunFrames;
            this.chipDamage = chipDamage;
            this.pushback = pushback;
            this.attackerAdvantageEstimate = attackerAdvantageEstimate;
        }
    }

    public readonly struct StateSnapshot
    {
        public readonly int simulationFrame;
        public readonly int nextProjectileId;
        public readonly int roundTimerFramesRemaining;
        public readonly bool roundTimerEnabled;
        public readonly RoundPhase roundPhase;
        public readonly RoundResult lastRoundResult;
        public readonly RoundEndType lastRoundEndType;
        public readonly MatchWinner matchWinner;
        public readonly int player1RoundWins;
        public readonly int player2RoundWins;
        public readonly int roundNumber;
        public readonly int phaseFramesRemaining;
        public readonly float previousPlayer1X;
        public readonly float previousPlayer2X;
        public readonly Fighter.Snapshot player1Snapshot;
        public readonly Fighter.Snapshot player2Snapshot;
        public readonly ProjectileSnapshot[] projectiles;

        public StateSnapshot(
            int simulationFrame,
            int nextProjectileId,
            int roundTimerFramesRemaining,
            bool roundTimerEnabled,
            RoundPhase roundPhase,
            RoundResult lastRoundResult,
            RoundEndType lastRoundEndType,
            MatchWinner matchWinner,
            int player1RoundWins,
            int player2RoundWins,
            int roundNumber,
            int phaseFramesRemaining,
            float previousPlayer1X,
            float previousPlayer2X,
            Fighter.Snapshot player1Snapshot,
            Fighter.Snapshot player2Snapshot,
            ProjectileSnapshot[] projectiles
        )
        {
            this.simulationFrame = simulationFrame;
            this.nextProjectileId = nextProjectileId;
            this.roundTimerFramesRemaining = roundTimerFramesRemaining;
            this.roundTimerEnabled = roundTimerEnabled;
            this.roundPhase = roundPhase;
            this.lastRoundResult = lastRoundResult;
            this.lastRoundEndType = lastRoundEndType;
            this.matchWinner = matchWinner;
            this.player1RoundWins = player1RoundWins;
            this.player2RoundWins = player2RoundWins;
            this.roundNumber = roundNumber;
            this.phaseFramesRemaining = phaseFramesRemaining;
            this.previousPlayer1X = previousPlayer1X;
            this.previousPlayer2X = previousPlayer2X;
            this.player1Snapshot = player1Snapshot;
            this.player2Snapshot = player2Snapshot;
            this.projectiles = projectiles;
        }
    }

    private enum PendingHitSource
    {
        FighterHitbox,
        Projectile
    }

    private struct PendingHitEvent
    {
        public PendingHitSource source;
        public Fighter attacker;
        public Fighter defender;
        public Hitbox hitbox;
        public Projectile projectile;
    }

    private Fighter player1;
    private Fighter player2;
    private readonly List<Projectile> projectiles = new List<Projectile>();
    private readonly List<PendingHitEvent> pendingHitEvents = new List<PendingHitEvent>(8);
    private int nextProjectileId = 1;
    private int simulationFrame;
    private readonly float fighterStartPositionOffset;
    private readonly float stageLeft;
    private readonly float stageRight;
    private readonly bool enableBackwalkTether;
    private readonly float maxBackwalkSeparation;
    private float previousPlayer1X;
    private float previousPlayer2X;
    private readonly Dictionary<int, InputFrame> pendingPlayer1Packets = new Dictionary<int, InputFrame>();
    private readonly Dictionary<int, InputFrame> pendingPlayer2Packets = new Dictionary<int, InputFrame>();
    private readonly bool enableAdvancedGuardRules;
    private readonly bool enableAirBlock;
    private readonly bool enableChipOnBlock;
    private readonly bool enableCounterHit;
    private readonly bool enableParry;
    private readonly int counterHitBonusHitstunFrames;
    private readonly int parryAttackerHitstopFrames;
    private readonly int parryDefenderHitstopFrames;
    private readonly int parryActiveWindowFrames;
    private readonly int parryWhiffLockoutFrames;
    private readonly bool verboseCombatDebugLogs;
    private readonly int roundStartHealth;
    private readonly bool enableGlobalDamageOverride;
    private readonly int lightOverrideDamage;
    private readonly int mediumOverrideDamage;
    private readonly int heavyOverrideDamage;
    private readonly bool useRoundTimer;
    private readonly int roundTimerDurationFrames;
    private readonly int roundOverFreezeFrames;
    private readonly int matchOverFreezeFrames;
    private const int RoundsToWin = 2;
    private FighterConfig player1Config;
    private FighterConfig player2Config;
    private string player1Name = "Player1";
    private string player2Name = "Player2";
    private int roundTimerFramesRemaining;
    private bool roundTimerEnabled;
    private RoundPhase roundPhase = RoundPhase.Fighting;
    private RoundResult lastRoundResult = RoundResult.None;
    private RoundEndType lastRoundEndType = RoundEndType.None;
    private MatchWinner matchWinner = MatchWinner.None;
    private int player1RoundWins;
    private int player2RoundWins;
    private int roundNumber = 1;
    private int phaseFramesRemaining;
    private CombatInteractionSnapshot latestCombatInteraction;
    
    public IReadOnlyList<Projectile> ActiveProjectiles => projectiles;
    public Fighter Player1 => player1;
    public Fighter Player2 => player2;
    public int CurrentFrame => simulationFrame;
    public CombatInteractionSnapshot LatestCombatInteraction => latestCombatInteraction;
    public int CurrentRoundNumber => roundNumber;
    public int Player1RoundWins => player1RoundWins;
    public int Player2RoundWins => player2RoundWins;
    public RoundPhase RoundPhase => roundPhase;
    public RoundResult LastRoundResult => lastRoundResult;
    public RoundEndType LastRoundEndType => lastRoundEndType;
    public MatchWinner MatchWinner => matchWinner;
    public int RoundTimerFramesRemaining => roundTimerFramesRemaining;
    public bool IsMatchOver => roundPhase == RoundPhase.MatchOver;
    public bool IsRoundTimerEnabled => roundTimerEnabled;
    public int RoundStartHealth => roundStartHealth;

    public Simulation(MatchConfig config = null)
    {
        if (config != null)
        {
            fighterStartPositionOffset = Mathf.Abs(config.fighterStartPositionOffset);
            stageLeft = config.stageLeft;
            stageRight = config.stageRight;
            enableBackwalkTether = config.enableBackwalkTether;
            maxBackwalkSeparation = Mathf.Max(0f, config.maxBackwalkSeparation);
            enableAdvancedGuardRules = config.enableAdvancedGuardRules;
            enableAirBlock = config.enableAirBlock;
            enableChipOnBlock = config.enableChipOnBlock;
            enableCounterHit = config.enableCounterHit;
            enableParry = config.enableParry;
            counterHitBonusHitstunFrames = Mathf.Max(0, config.counterHitBonusHitstunFrames);
            parryAttackerHitstopFrames = Mathf.Max(0, config.parryAttackerHitstopFrames);
            parryDefenderHitstopFrames = Mathf.Max(0, config.parryDefenderHitstopFrames);
            parryActiveWindowFrames = Mathf.Max(1, config.parryActiveWindowFrames);
            parryWhiffLockoutFrames = Mathf.Max(0, config.parryWhiffLockoutFrames);
            verboseCombatDebugLogs = config.verboseCombatDebugLogs;
            roundStartHealth = Mathf.Max(1, config.roundStartHealth);
            enableGlobalDamageOverride = config.enableGlobalDamageOverride;
            lightOverrideDamage = Mathf.Max(0, config.lightOverrideDamage);
            mediumOverrideDamage = Mathf.Max(0, config.mediumOverrideDamage);
            heavyOverrideDamage = Mathf.Max(0, config.heavyOverrideDamage);
            useRoundTimer = config.useRoundTimer;
            roundTimerDurationFrames = Mathf.Max(1, config.roundTimerSeconds) * SimulationTime.TicksPerSecond;
            roundOverFreezeFrames = config.roundOverFreezeFrames > 0 ? config.roundOverFreezeFrames : 90;
            matchOverFreezeFrames = config.matchOverFreezeFrames > 0 ? config.matchOverFreezeFrames : 180;
        }
        else
        {
            fighterStartPositionOffset = 10f;
            stageLeft = -80f;
            stageRight = 80f;
            enableBackwalkTether = false;
            maxBackwalkSeparation = 22f;
            enableAdvancedGuardRules = true;
            enableAirBlock = true;
            enableChipOnBlock = true;
            enableCounterHit = true;
            enableParry = false;
            counterHitBonusHitstunFrames = 2;
            parryAttackerHitstopFrames = 10;
            parryDefenderHitstopFrames = 3;
            parryActiveWindowFrames = 3;
            parryWhiffLockoutFrames = 12;
            verboseCombatDebugLogs = false;
            roundStartHealth = 1000;
            enableGlobalDamageOverride = false;
            lightOverrideDamage = 50;
            mediumOverrideDamage = 100;
            heavyOverrideDamage = 150;
            useRoundTimer = true;
            roundTimerDurationFrames = 99 * SimulationTime.TicksPerSecond;
            roundOverFreezeFrames = 90;
            matchOverFreezeFrames = 180;
        }
    }

    public void Initialize(
        FighterConfig player1Config,
        FighterConfig player2Config,
        string player1Name = "Player1",
        string player2Name = "Player2"
    )
    {
        this.player1Config = player1Config;
        this.player2Config = player2Config;
        this.player1Name = string.IsNullOrWhiteSpace(player1Name) ? "Player1" : player1Name;
        this.player2Name = string.IsNullOrWhiteSpace(player2Name) ? "Player2" : player2Name;
        Fighter.EnableParry = enableParry;
        Fighter.ParryActiveWindowFrames = parryActiveWindowFrames;
        Fighter.ParryWhiffLockoutFrames = parryWhiffLockoutFrames;
        ResetEntireMatch();
    }

    public void Tick(FrameInput frameInput)
    {
        if (player1 == null || player2 == null)
            return;

        previousPlayer1X = player1.Position.x;
        previousPlayer2X = player2.Position.x;

        if (roundPhase == RoundPhase.Fighting)
        {
            player1.Tick(frameInput.player1);
            player2.Tick(frameInput.player2);

            SpawnPendingProjectiles();
            UpdateProjectiles();
            ResolveHitDetection();
            ResolvePushboxes();
            ClampToStage(player1);
            ClampToStage(player2);
            ResolveBackwalkTether();

            if (roundTimerEnabled && roundTimerFramesRemaining > 0)
                roundTimerFramesRemaining--;

            EvaluateRoundEnd();
        }
        else
        {
            AdvanceRoundFlow();
        }

        simulationFrame = frameInput.frameIndex > simulationFrame
            ? frameInput.frameIndex
            : simulationFrame + 1;
    }

    public void Tick(FrameInputPacket frameInputPacket)
    {
        InputFrame decoded = InputPacketCodec.Decode(frameInputPacket);
        if (frameInputPacket.playerId == 2)
            pendingPlayer2Packets[frameInputPacket.frame] = decoded;
        else
            pendingPlayer1Packets[frameInputPacket.frame] = decoded;

        while (TryBuildNextFrameInput(out FrameInput frameInput))
            Tick(frameInput);
    }

    private bool TryBuildNextFrameInput(out FrameInput frameInput)
    {
        int nextFrame = simulationFrame + 1;
        if (!pendingPlayer1Packets.TryGetValue(nextFrame, out InputFrame player1Input))
        {
            frameInput = default;
            return false;
        }

        if (!pendingPlayer2Packets.TryGetValue(nextFrame, out InputFrame player2Input))
        {
            frameInput = default;
            return false;
        }

        pendingPlayer1Packets.Remove(nextFrame);
        pendingPlayer2Packets.Remove(nextFrame);
        frameInput = new FrameInput
        {
            frameIndex = nextFrame,
            player1 = player1Input,
            player2 = player2Input
        };
        return true;
    }

    private void EvaluateRoundEnd()
    {
        bool player1Dead = player1.IsDefeated || player1.Health <= 0;
        bool player2Dead = player2.IsDefeated || player2.Health <= 0;
        if (player1Dead || player2Dead)
        {
            if (player1Dead && player2Dead)
                ResolveRoundEnd(RoundResult.Draw, RoundEndType.Draw);
            else if (player1Dead)
                ResolveRoundEnd(RoundResult.Player2Win, RoundEndType.KO);
            else
                ResolveRoundEnd(RoundResult.Player1Win, RoundEndType.KO);

            return;
        }

        if (!roundTimerEnabled || roundTimerFramesRemaining > 0)
            return;

        if (player1.Health > player2.Health)
            ResolveRoundEnd(RoundResult.Player1Win, RoundEndType.TimeOut);
        else if (player2.Health > player1.Health)
            ResolveRoundEnd(RoundResult.Player2Win, RoundEndType.TimeOut);
        else
            ResolveRoundEnd(RoundResult.Draw, RoundEndType.Draw);
    }

    private void ResolveRoundEnd(RoundResult result, RoundEndType endType)
    {
        if (roundPhase != RoundPhase.Fighting)
            return;

        lastRoundResult = result;
        lastRoundEndType = endType;
        roundPhase = RoundPhase.RoundOverFreeze;
        phaseFramesRemaining = roundOverFreezeFrames;

        if (result == RoundResult.Player1Win)
            player1RoundWins++;
        else if (result == RoundResult.Player2Win)
            player2RoundWins++;

        if (player1RoundWins >= RoundsToWin)
            matchWinner = MatchWinner.Player1;
        else if (player2RoundWins >= RoundsToWin)
            matchWinner = MatchWinner.Player2;
        else
            matchWinner = MatchWinner.None;
    }

    private void AdvanceRoundFlow()
    {
        if (phaseFramesRemaining > 0)
        {
            phaseFramesRemaining--;
            return;
        }

        if (roundPhase == RoundPhase.RoundOverFreeze)
        {
            if (matchWinner == MatchWinner.None)
                StartNextRound();
            else
                EnterMatchOverPhase();
            return;
        }

        if (roundPhase == RoundPhase.MatchOver)
            ResetEntireMatch();
    }

    private void StartNextRound()
    {
        roundNumber = Mathf.Max(1, roundNumber + 1);
        BeginRound();
    }

    private void EnterMatchOverPhase()
    {
        roundPhase = RoundPhase.MatchOver;
        phaseFramesRemaining = matchOverFreezeFrames;
    }

    private void BeginRound()
    {
        if (player1Config == null || player2Config == null)
            return;

        player1 = new Fighter(
            player1Config,
            new Vector2(-fighterStartPositionOffset, 0f),
            player1Name,
            roundStartHealth,
            enableGlobalDamageOverride,
            lightOverrideDamage,
            mediumOverrideDamage,
            heavyOverrideDamage
        );
        player2 = new Fighter(
            player2Config,
            new Vector2(fighterStartPositionOffset, 0f),
            player2Name,
            roundStartHealth,
            enableGlobalDamageOverride,
            lightOverrideDamage,
            mediumOverrideDamage,
            heavyOverrideDamage
        );
        player1.SetOpponent(player2);
        player2.SetOpponent(player1);
        previousPlayer1X = player1.Position.x;
        previousPlayer2X = player2.Position.x;
        latestCombatInteraction = default;

        ClearTransientStateForRoundStart();
        lastRoundResult = RoundResult.None;
        lastRoundEndType = RoundEndType.None;
        roundTimerEnabled = useRoundTimer;
        roundTimerFramesRemaining = roundTimerEnabled ? roundTimerDurationFrames : 0;
        roundPhase = RoundPhase.Fighting;
        phaseFramesRemaining = 0;
    }

    private void ResetEntireMatch()
    {
        pendingPlayer1Packets.Clear();
        pendingPlayer2Packets.Clear();
        player1RoundWins = 0;
        player2RoundWins = 0;
        roundNumber = 1;
        matchWinner = MatchWinner.None;
        lastRoundResult = RoundResult.None;
        lastRoundEndType = RoundEndType.None;
        BeginRound();
    }

    public int ComputeDeterminismHash()
    {
        unchecked
        {
            int hash = 17;
            hash = HashInt(hash, simulationFrame);
            hash = HashInt(hash, (int)roundPhase);
            hash = HashInt(hash, (int)lastRoundResult);
            hash = HashInt(hash, (int)lastRoundEndType);
            hash = HashInt(hash, (int)matchWinner);
            hash = HashInt(hash, player1RoundWins);
            hash = HashInt(hash, player2RoundWins);
            hash = HashInt(hash, roundNumber);
            hash = HashInt(hash, phaseFramesRemaining);
            hash = HashInt(hash, roundTimerFramesRemaining);
            hash = HashInt(hash, roundTimerEnabled ? 1 : 0);
            hash = HashFighterState(hash, player1, 1);
            hash = HashFighterState(hash, player2, 2);
            hash = HashInt(hash, projectiles.Count);

            for (int i = 0; i < projectiles.Count; i++)
            {
                Projectile projectile = projectiles[i];
                hash = HashInt(hash, projectile.id);
                hash = HashInt(hash, projectile.active ? 1 : 0);
                hash = HashInt(hash, projectile.owner == player1 ? 1 : 2);
                hash = HashInt(hash, QuantizeFloat(projectile.position.x));
                hash = HashInt(hash, QuantizeFloat(projectile.position.y));
                hash = HashInt(hash, QuantizeFloat(projectile.velocity.x));
                hash = HashInt(hash, QuantizeFloat(projectile.velocity.y));
                hash = HashInt(hash, QuantizeFloat(projectile.halfSize.x));
                hash = HashInt(hash, QuantizeFloat(projectile.halfSize.y));
                hash = HashInt(hash, projectile.damage);
                hash = HashInt(hash, projectile.hitstunFrames);
                hash = HashInt(hash, projectile.blockstunFrames);
                hash = HashInt(hash, QuantizeFloat(projectile.blockPushback));
                hash = HashInt(hash, projectile.chipDamage);
                hash = HashInt(hash, projectile.attackerBlockstopFrames);
                hash = HashInt(hash, (int)projectile.hitLevel);
                hash = HashInt(hash, projectile.lifetimeFramesRemaining);
            }

            return hash;
        }
    }

    public NetState CaptureNetState()
    {
        StateSnapshot snapshot = CaptureState();

        NetState state = NetState.CreateDefault();
        state.stateVersion = NetState.CurrentVersion;
        state.frame = snapshot.simulationFrame;
        state.nextProjectileId = snapshot.nextProjectileId;
        state.roundPhase = (int)snapshot.roundPhase;
        state.lastRoundResult = (int)snapshot.lastRoundResult;
        state.lastRoundEndType = (int)snapshot.lastRoundEndType;
        state.matchWinner = (int)snapshot.matchWinner;
        state.player1RoundWins = snapshot.player1RoundWins;
        state.player2RoundWins = snapshot.player2RoundWins;
        state.roundNumber = snapshot.roundNumber;
        state.phaseFramesRemaining = snapshot.phaseFramesRemaining;
        state.roundTimerFramesRemaining = snapshot.roundTimerFramesRemaining;
        state.roundTimerEnabled = snapshot.roundTimerEnabled;
        state.previousPlayer1X = snapshot.previousPlayer1X;
        state.previousPlayer2X = snapshot.previousPlayer2X;
        state.player1 = CaptureNetFighterState(snapshot.player1Snapshot, 1);
        state.player2 = CaptureNetFighterState(snapshot.player2Snapshot, 2);

        ProjectileSnapshot[] sourceProjectiles = snapshot.projectiles ?? Array.Empty<ProjectileSnapshot>();
        NetProjectileState[] netProjectiles = new NetProjectileState[sourceProjectiles.Length];
        for (int i = 0; i < sourceProjectiles.Length; i++)
        {
            ProjectileSnapshot source = sourceProjectiles[i];
            netProjectiles[i] = new NetProjectileState
            {
                id = source.id,
                ownerPlayerId = source.ownerSlot,
                positionX = source.position.x,
                positionY = source.position.y,
                velocityX = source.velocity.x,
                velocityY = source.velocity.y,
                halfSizeX = source.halfSize.x,
                halfSizeY = source.halfSize.y,
                lifetimeFramesRemaining = source.lifetimeFramesRemaining,
                damage = source.damage,
                hitstunFrames = source.hitstunFrames,
                blockstunFrames = source.blockstunFrames,
                blockPushback = source.blockPushback,
                chipDamage = source.chipDamage,
                attackerBlockstopFrames = source.attackerBlockstopFrames,
                hitLevel = (int)source.hitLevel,
                active = source.active
            };
        }

        state.projectiles = netProjectiles;
        return state;
    }

    public void RestoreNetState(NetState state)
    {
        state = NetStateMigrator.MigrateToCurrent(state);

        Fighter.Snapshot player1Snapshot = BuildFighterSnapshotFromNet(state.player1, player1);
        Fighter.Snapshot player2Snapshot = BuildFighterSnapshotFromNet(state.player2, player2);

        ProjectileSnapshot[] projectileSnapshots;
        if (state.projectiles == null)
        {
            projectileSnapshots = Array.Empty<ProjectileSnapshot>();
        }
        else
        {
            projectileSnapshots = new ProjectileSnapshot[state.projectiles.Length];
            for (int i = 0; i < state.projectiles.Length; i++)
            {
                NetProjectileState netProjectile = state.projectiles[i];
                projectileSnapshots[i] = new ProjectileSnapshot(
                    netProjectile.id,
                    netProjectile.ownerPlayerId,
                    new Vector2(netProjectile.positionX, netProjectile.positionY),
                    new Vector2(netProjectile.velocityX, netProjectile.velocityY),
                    new Vector2(netProjectile.halfSizeX, netProjectile.halfSizeY),
                    netProjectile.lifetimeFramesRemaining,
                    netProjectile.damage,
                    netProjectile.hitstunFrames,
                    netProjectile.blockstunFrames,
                    netProjectile.blockPushback,
                    netProjectile.chipDamage,
                    netProjectile.attackerBlockstopFrames,
                    (HitLevel)netProjectile.hitLevel,
                    netProjectile.active
                );
            }
        }

        RestoreState(new StateSnapshot(
            state.frame,
            Mathf.Max(1, state.nextProjectileId),
            Mathf.Max(0, state.roundTimerFramesRemaining),
            state.roundTimerEnabled,
            (RoundPhase)state.roundPhase,
            (RoundResult)state.lastRoundResult,
            (RoundEndType)state.lastRoundEndType,
            (MatchWinner)state.matchWinner,
            Mathf.Max(0, state.player1RoundWins),
            Mathf.Max(0, state.player2RoundWins),
            Mathf.Max(1, state.roundNumber),
            Mathf.Max(0, state.phaseFramesRemaining),
            state.previousPlayer1X,
            state.previousPlayer2X,
            player1Snapshot,
            player2Snapshot,
            projectileSnapshots
        ));
    }

    public StateSnapshot CaptureState()
    {
        ProjectileSnapshot[] projectileSnapshots = new ProjectileSnapshot[projectiles.Count];
        for (int i = 0; i < projectiles.Count; i++)
        {
            Projectile projectile = projectiles[i];
            int ownerSlot = projectile.owner == player1 ? 1 : 2;
            projectileSnapshots[i] = new ProjectileSnapshot(
                projectile.id,
                ownerSlot,
                projectile.position,
                projectile.velocity,
                projectile.halfSize,
                projectile.lifetimeFramesRemaining,
                projectile.damage,
                projectile.hitstunFrames,
                projectile.blockstunFrames,
                projectile.blockPushback,
                projectile.chipDamage,
                projectile.attackerBlockstopFrames,
                projectile.hitLevel,
                projectile.active
            );
        }

        return new StateSnapshot(
            simulationFrame,
            nextProjectileId,
            roundTimerFramesRemaining,
            roundTimerEnabled,
            roundPhase,
            lastRoundResult,
            lastRoundEndType,
            matchWinner,
            player1RoundWins,
            player2RoundWins,
            roundNumber,
            phaseFramesRemaining,
            previousPlayer1X,
            previousPlayer2X,
            player1.CaptureSnapshot(),
            player2.CaptureSnapshot(),
            projectileSnapshots
        );
    }

    public void RestoreState(StateSnapshot snapshot)
    {
        simulationFrame = snapshot.simulationFrame;
        nextProjectileId = snapshot.nextProjectileId;
        roundTimerFramesRemaining = snapshot.roundTimerFramesRemaining;
        roundTimerEnabled = snapshot.roundTimerEnabled;
        roundPhase = snapshot.roundPhase;
        lastRoundResult = snapshot.lastRoundResult;
        lastRoundEndType = snapshot.lastRoundEndType;
        matchWinner = snapshot.matchWinner;
        player1RoundWins = snapshot.player1RoundWins;
        player2RoundWins = snapshot.player2RoundWins;
        roundNumber = snapshot.roundNumber;
        phaseFramesRemaining = snapshot.phaseFramesRemaining;
        previousPlayer1X = snapshot.previousPlayer1X;
        previousPlayer2X = snapshot.previousPlayer2X;
        pendingPlayer1Packets.Clear();
        pendingPlayer2Packets.Clear();

        player1.RestoreSnapshot(snapshot.player1Snapshot);
        player2.RestoreSnapshot(snapshot.player2Snapshot);

        pendingHitEvents.Clear();
        projectiles.Clear();
        if (snapshot.projectiles == null)
            return;

        for (int i = 0; i < snapshot.projectiles.Length; i++)
        {
            ProjectileSnapshot projectileSnapshot = snapshot.projectiles[i];
            Fighter owner = projectileSnapshot.ownerSlot == 1 ? player1 : player2;
            Projectile projectile = new Projectile(
                projectileSnapshot.id,
                owner,
                projectileSnapshot.position,
                projectileSnapshot.velocity,
                projectileSnapshot.halfSize,
                projectileSnapshot.lifetimeFramesRemaining,
                projectileSnapshot.damage,
                projectileSnapshot.hitstunFrames,
                projectileSnapshot.blockstunFrames,
                projectileSnapshot.blockPushback,
                projectileSnapshot.chipDamage,
                projectileSnapshot.attackerBlockstopFrames,
                projectileSnapshot.hitLevel
            )
            {
                active = projectileSnapshot.active,
                lifetimeFramesRemaining = projectileSnapshot.lifetimeFramesRemaining
            };
            projectiles.Add(projectile);
        }
    }

    public string GetPlayer1InputHistoryDebugString(int maxEntries = 30)
    {
        if (player1 == null)
            return "Player 1 not initialized";

        return player1.GetInputHistoryDebugString(maxEntries);
    }

    public void ClearDebugInputHistories()
    {
        player1?.ClearInputHistory();
        player2?.ClearInputHistory();
    }

    private void ResolvePushboxes()
    {
        if (!ShouldResolvePushboxCollision(player1, player2))
            return;

        // Compute horizontal overlap
        float distance = Mathf.Abs(player2.Position.x - player1.Position.x);
        float minDistance = player1.PushboxHalfWidth + player2.PushboxHalfWidth;

        if (distance < minDistance)
        {
            float overlap = minDistance - distance;
            float separation = overlap * 0.5f;

            if (player1.Position.x < player2.Position.x)
            {
                player1.MoveHorizontal(-separation);
                player2.MoveHorizontal(separation);
            }
            else
            {
                player1.MoveHorizontal(separation);
                player2.MoveHorizontal(-separation);
            }
        }
    }

    private static bool ShouldResolvePushboxCollision(Fighter a, Fighter b)
    {
        if (a == null || b == null)
            return false;

        // Keep grounded body-blocking behavior, but allow air crossovers by requiring
        // vertical overlap between hurtboxes before applying horizontal push separation.
        if (a.IsGrounded && b.IsGrounded)
            return true;

        Box hurtboxA = a.CurrentHurtbox;
        Box hurtboxB = b.CurrentHurtbox;
        return Mathf.Abs(hurtboxA.center.y - hurtboxB.center.y) <= (hurtboxA.halfSize.y + hurtboxB.halfSize.y);
    }

    private void ResolveHitDetection()
    {
        pendingHitEvents.Clear();
        CollectFighterHitEvent(player1, player2);
        CollectFighterHitEvent(player2, player1);
        CollectProjectileHitEvents();
        ResolvePendingHitEvents();
        PruneInactiveProjectiles();
    }

    private void CollectFighterHitEvent(Fighter attacker, Fighter defender)
    {
        if (attacker == null || defender == null)
            return;

        if (attacker.IsDefeated || defender.IsDefeated)
            return;

        if (!attacker.HasActiveUnspentHitbox)
            return;

        Hitbox hitbox = attacker.CurrentHitbox;
        if (!hitbox.box.Overlaps(defender.CurrentHurtbox))
            return;

        pendingHitEvents.Add(new PendingHitEvent
        {
            source = PendingHitSource.FighterHitbox,
            attacker = attacker,
            defender = defender,
            hitbox = hitbox,
            projectile = null
        });
    }

    private void SpawnPendingProjectiles()
    {
        TrySpawnProjectileForFighter(player1);
        TrySpawnProjectileForFighter(player2);
    }

    private void TrySpawnProjectileForFighter(Fighter fighter)
    {
        if (fighter == null)
            return;

        if (!fighter.TryConsumeProjectileSpawnRequest(out ProjectileSpawnRequest request))
            return;

        Projectile projectile = new Projectile(
            nextProjectileId++,
            request.owner,
            request.position,
            request.velocity,
            request.halfSize,
            request.lifetimeFrames,
            request.damage,
            request.hitstunFrames,
            request.blockstunFrames,
            request.blockPushback,
            request.chipDamage,
            request.attackerBlockstopFrames,
            request.hitLevel
        );
        projectiles.Add(projectile);
    }

    private void UpdateProjectiles()
    {
        for (int i = projectiles.Count - 1; i >= 0; i--)
        {
            Projectile projectile = projectiles[i];
            projectile.Tick();

            if (!projectile.active || IsProjectileOutOfStage(projectile))
            {
                projectile.active = false;
                projectiles.RemoveAt(i);
            }
        }
    }

    private bool IsProjectileOutOfStage(Projectile projectile)
    {
        return projectile.position.x + projectile.halfSize.x < stageLeft
            || projectile.position.x - projectile.halfSize.x > stageRight;
    }

    private void CollectProjectileHitEvents()
    {
        for (int i = projectiles.Count - 1; i >= 0; i--)
        {
            Projectile projectile = projectiles[i];
            if (!projectile.active)
                continue;

            Fighter defender = projectile.owner == player1 ? player2 : player1;
            if (defender == null)
                continue;

            if (defender.IsDefeated)
                continue;

            if (!projectile.CurrentBox.Overlaps(defender.CurrentHurtbox))
                continue;

            pendingHitEvents.Add(new PendingHitEvent
            {
                source = PendingHitSource.Projectile,
                attacker = projectile.owner,
                defender = defender,
                hitbox = projectile.ToHitbox(),
                projectile = projectile
            });
        }
    }

    private void ResolvePendingHitEvents()
    {
        for (int i = 0; i < pendingHitEvents.Count; i++)
        {
            PendingHitEvent hitEvent = pendingHitEvents[i];
            if (hitEvent.attacker == null || hitEvent.defender == null)
                continue;

            if (hitEvent.attacker.IsDefeated || hitEvent.defender.IsDefeated)
                continue;

            if (!hitEvent.hitbox.active)
                continue;

            bool isProjectile = hitEvent.source == PendingHitSource.Projectile;
            if (isProjectile && (hitEvent.projectile == null || !hitEvent.projectile.active))
                continue;

            ResolveHitEvent(hitEvent);

            if (hitEvent.source == PendingHitSource.Projectile)
            {
                hitEvent.projectile.active = false;
            }
            else
            {
                hitEvent.attacker.MarkCurrentHitboxAsSpent();
            }
        }
    }

    private void ResolveHitEvent(PendingHitEvent hitEvent)
    {
        Fighter attacker = hitEvent.attacker;
        Fighter defender = hitEvent.defender;
        Hitbox hitbox = hitEvent.hitbox;

        if (enableParry && defender.CanPerformParryNow() && IsParryEligible(hitbox))
        {
            defender.ApplyParry(parryDefenderHitstopFrames, parryWhiffLockoutFrames);
            attacker.ApplySuccessfulHitstopAsAttacker(parryAttackerHitstopFrames);
            EmitCombatInteraction(attacker, defender, hitbox, HitResultType.Parry, 0, 0, 0f);
            return;
        }

        bool blocked = enableAdvancedGuardRules
            ? IsLegalBlock(defender, hitbox)
            : defender.IsHoldingValidBlockDirection;

        int counterHitBonus = 0;
        bool isCounterHit = false;
        if (enableCounterHit && !blocked && defender.IsInAttackStartup())
        {
            isCounterHit = true;
            counterHitBonus = counterHitBonusHitstunFrames;
        }

        if (blocked)
        {
            bool allowChip = enableChipOnBlock && (hitbox.isProjectile || attacker.CurrentMoveType.IsFireball() || attacker.CurrentMoveType.IsDragonPunch());
            HitResultType resultType = isCounterHit ? HitResultType.CounterHitBlocked : HitResultType.Blocked;
            defender.ApplyBlockedHit(hitbox, allowChip, resultType);
            int attackerStop = hitbox.attackerBlockstopFrames >= 0 ? hitbox.attackerBlockstopFrames : Fighter.HitstopFrames;
            attacker.ApplySuccessfulHitstopAsAttacker(attackerStop);
            ApplyBlockPushback(attacker, defender, hitbox.blockPushback);
            EmitCombatInteraction(attacker, defender, hitbox, resultType, defender.BlockstunFramesRemaining, defender.LastReceivedChipDamage, hitbox.blockPushback);
            return;
        }

        HitResultType onHitResult = isCounterHit ? HitResultType.CounterHit : HitResultType.Hit;
        defender.ApplyHit(hitbox, counterHitBonus, onHitResult);
        attacker.ApplySuccessfulHitstopAsAttacker();
        EmitCombatInteraction(attacker, defender, hitbox, onHitResult, defender.HitstunFramesRemaining, 0, 0f);
    }

    private bool IsParryEligible(Hitbox hitbox)
    {
        if (hitbox.hitLevel == HitLevel.Unblockable || hitbox.hitLevel == HitLevel.AirUnblockable)
            return false;

        return true;
    }

    private bool IsLegalBlock(Fighter defender, Hitbox hitbox)
    {
        if (!defender.IsHoldingBlockInput)
            return false;

        if (!defender.CanCurrentlyBlock)
            return false;

        if (hitbox.hitLevel == HitLevel.Unblockable)
            return false;

        if (!defender.IsGrounded)
        {
            if (!enableAirBlock)
                return false;

            return hitbox.hitLevel != HitLevel.AirUnblockable;
        }

        switch (hitbox.hitLevel)
        {
            case HitLevel.High:
                return defender.CurrentState != FighterState.Crouching;
            case HitLevel.Low:
                return defender.CurrentState == FighterState.Crouching;
            case HitLevel.Mid:
                return true;
            case HitLevel.AirUnblockable:
                return true;
            default:
                return false;
        }
    }

    private void ApplyBlockPushback(Fighter attacker, Fighter defender, float amount)
    {
        float pushback = Mathf.Max(0f, amount);
        if (pushback <= 0f)
            return;

        float direction = attacker.Position.x < defender.Position.x ? 1f : -1f;
        attacker.MoveHorizontal(-direction * (pushback * 0.35f));
        defender.MoveHorizontal(direction * (pushback * 0.65f));

        ClampToStage(attacker);
        ClampToStage(defender);
        ResolvePushboxes();
    }

    private void EmitCombatInteraction(
        Fighter attacker,
        Fighter defender,
        Hitbox hitbox,
        HitResultType resultType,
        int stunFrames,
        int chipDamage,
        float pushback
    )
    {
        int attackerId = attacker == player1 ? 1 : 2;
        int defenderId = defender == player1 ? 1 : 2;
        int attackerRecovery = attacker.GetEstimatedRemainingRecoveryFrames();
        int attackerAdvantageEstimate = stunFrames - attackerRecovery;

        latestCombatInteraction = new CombatInteractionSnapshot(
            simulationFrame,
            attackerId,
            defenderId,
            hitbox.hitLevel,
            resultType,
            stunFrames,
            chipDamage,
            pushback,
            attackerAdvantageEstimate
        );

        if (verboseCombatDebugLogs)
        {
            Debug.Log(
                $"[Combat] f={simulationFrame} a={attackerId} d={defenderId} result={resultType} " +
                $"lvl={hitbox.hitLevel} stun={stunFrames} chip={chipDamage} push={pushback:F2} adv={attackerAdvantageEstimate}"
            );
        }
    }

    private void PruneInactiveProjectiles()
    {
        for (int i = projectiles.Count - 1; i >= 0; i--)
        {
            if (!projectiles[i].active)
                projectiles.RemoveAt(i);
        }
    }

    private void ClearTransientStateForRoundStart()
    {
        pendingHitEvents.Clear();
        projectiles.Clear();
        nextProjectileId = 1;
    }

    private void ClampToStage(Fighter fighter)
    {
        float half = fighter.PushboxHalfWidth;

        if (fighter.Position.x - half < stageLeft)
            fighter.SetHorizontal(stageLeft + half);

        if (fighter.Position.x + half > stageRight)
            fighter.SetHorizontal(stageRight - half);
    }

    private void ResolveBackwalkTether()
    {
        if (!enableBackwalkTether || maxBackwalkSeparation <= 0f)
            return;

        if (player1 == null || player2 == null)
            return;

        // Apply this as a grounded spacing rule to mimic an artificial corner when both
        // fighters keep walking away and camera zoom has reached its limit.
        if (!player1.IsGrounded || !player2.IsGrounded)
            return;

        bool player1IsLeft = player1.Position.x <= player2.Position.x;
        Fighter leftFighter = player1IsLeft ? player1 : player2;
        Fighter rightFighter = player1IsLeft ? player2 : player1;
        float leftPreviousX = player1IsLeft ? previousPlayer1X : previousPlayer2X;
        float rightPreviousX = player1IsLeft ? previousPlayer2X : previousPlayer1X;
        float distance = rightFighter.Position.x - leftFighter.Position.x;
        float minimumSpacing = player1.PushboxHalfWidth + player2.PushboxHalfWidth;
        float effectiveMaxSeparation = Mathf.Max(maxBackwalkSeparation, minimumSpacing);
        if (distance <= effectiveMaxSeparation)
            return;

        float separationExcess = distance - effectiveMaxSeparation;

        // Retreating means increasing separation: left fighter moving left, right fighter
        // moving right. Undo only that retreat delta so the opponent is not dragged.
        float leftRetreatDelta = leftFighter.CurrentState == FighterState.Backdash
            ? 0f
            : Mathf.Max(0f, leftPreviousX - leftFighter.Position.x);
        float rightRetreatDelta = rightFighter.CurrentState == FighterState.Backdash
            ? 0f
            : Mathf.Max(0f, rightFighter.Position.x - rightPreviousX);

        if (leftRetreatDelta > 0f)
        {
            float correction = Mathf.Min(separationExcess, leftRetreatDelta);
            leftFighter.SetHorizontal(leftFighter.Position.x + correction);
            separationExcess -= correction;
        }

        if (separationExcess > 0f && rightRetreatDelta > 0f)
        {
            float correction = Mathf.Min(separationExcess, rightRetreatDelta);
            rightFighter.SetHorizontal(rightFighter.Position.x - correction);
            separationExcess -= correction;
        }

        ClampToStage(player1);
        ClampToStage(player2);
    }

    private static NetFighterState CaptureNetFighterState(Fighter.Snapshot snapshot, int ownerPlayerId)
    {
        InputHistoryBuffer.Snapshot historySnapshot = snapshot.inputHistorySnapshot;
        InputHistoryBuffer.HistoryEntry[] sourceEntries = historySnapshot.entries;
        NetInputHistoryEntry[] netEntries;
        if (sourceEntries == null)
        {
            netEntries = Array.Empty<NetInputHistoryEntry>();
        }
        else
        {
            netEntries = new NetInputHistoryEntry[sourceEntries.Length];
            for (int i = 0; i < sourceEntries.Length; i++)
            {
                netEntries[i] = new NetInputHistoryEntry
                {
                    input = sourceEntries[i].input,
                    relativeDirection = sourceEntries[i].relativeDirection
                };
            }
        }

        ProjectileSpawnRequest pendingRequest = snapshot.pendingProjectileSpawn;
        NetPendingProjectileRequestState pendingProjectileRequest = new NetPendingProjectileRequestState
        {
            hasPending = snapshot.hasPendingProjectileSpawn,
            ownerPlayerId = ownerPlayerId,
            positionX = pendingRequest.position.x,
            positionY = pendingRequest.position.y,
            velocityX = pendingRequest.velocity.x,
            velocityY = pendingRequest.velocity.y,
            halfSizeX = pendingRequest.halfSize.x,
            halfSizeY = pendingRequest.halfSize.y,
            lifetimeFrames = pendingRequest.lifetimeFrames,
            damage = pendingRequest.damage,
            hitstunFrames = pendingRequest.hitstunFrames,
            blockstunFrames = pendingRequest.blockstunFrames,
            blockPushback = pendingRequest.blockPushback,
            chipDamage = pendingRequest.chipDamage,
            attackerBlockstopFrames = pendingRequest.attackerBlockstopFrames,
            hitLevel = (int)pendingRequest.hitLevel
        };

        Hitbox hitbox = snapshot.attackSnapshot.hitbox;
        return new NetFighterState
        {
            positionX = snapshot.position.x,
            positionY = snapshot.position.y,
            velocityX = snapshot.velocity.x,
            velocityY = snapshot.velocity.y,
            isGrounded = snapshot.isGrounded,
            facingRight = snapshot.facingRight,
            fighterState = (int)snapshot.state,
            stateFrame = snapshot.stateFrame,
            transitionedThisTick = snapshot.transitionedThisTick,
            stateFrameFrozenThisTick = snapshot.stateFrameFrozenThisTick,
            hitstopFramesRemaining = snapshot.hitstopFramesRemaining,
            hitstunFramesRemaining = snapshot.hitstunFramesRemaining,
            blockstunFramesRemaining = snapshot.blockstunFramesRemaining,
            isHoldingBlockInput = snapshot.isHoldingBlockInput,
            canCurrentlyBlock = snapshot.canCurrentlyBlock,
            isHoldingValidBlockDirection = snapshot.isHoldingValidBlockDirection,
            parryInputPressedThisTick = snapshot.parryInputPressedThisTick,
            parryLastMoveXSign = snapshot.lastMoveXSign,
            parryWindowFramesRemaining = snapshot.parryWindowFramesRemaining,
            parryLockoutFramesRemaining = snapshot.parryLockoutFramesRemaining,
            health = snapshot.health,
            totalDamageTaken = snapshot.totalDamageTaken,
            totalChipDamageTaken = snapshot.totalChipDamageTaken,
            lastReceivedHitResult = (int)snapshot.lastReceivedHitResult,
            lastReceivedHitLevel = (int)snapshot.lastReceivedHitLevel,
            lastReceivedStunFrames = snapshot.lastReceivedStunFrames,
            lastReceivedChipDamage = snapshot.lastReceivedChipDamage,
            hadAttackInputThisTick = snapshot.hadAttackInputThisTick,
            lightPressBufferFramesRemaining = snapshot.lightPressBufferFramesRemaining,
            mediumPressBufferFramesRemaining = snapshot.mediumPressBufferFramesRemaining,
            heavyPressBufferFramesRemaining = snapshot.heavyPressBufferFramesRemaining,
            isDefeated = snapshot.isDefeated,
            pendingProjectileRequest = pendingProjectileRequest,

            renderVisualState = (int)snapshot.renderSnapshot.visualState,
            renderMoveType = (int)snapshot.renderSnapshot.moveType,
            renderVisualStateFrame = snapshot.renderSnapshot.visualStateFrame,
            renderAnimationSerial = snapshot.renderSnapshot.animationSerial,
            renderRestartAnimation = snapshot.renderSnapshot.restartAnimation,
            renderFreezeAnimation = snapshot.renderSnapshot.freezeAnimation,

            attackMoveType = (int)snapshot.attackSnapshot.moveType,
            attackFrame = snapshot.attackSnapshot.attackFrame,
            attackHasData = snapshot.attackSnapshot.attackData != null,
            hitboxCenterX = hitbox.box.center.x,
            hitboxCenterY = hitbox.box.center.y,
            hitboxHalfX = hitbox.box.halfSize.x,
            hitboxHalfY = hitbox.box.halfSize.y,
            hitboxDamage = hitbox.damage,
            hitboxHitstunFrames = hitbox.hitstunFrames,
            hitboxBlockstunFrames = hitbox.blockstunFrames,
            hitboxBlockPushback = hitbox.blockPushback,
            hitboxChipDamage = hitbox.chipDamage,
            hitboxAttackerBlockstopFrames = hitbox.attackerBlockstopFrames,
            hitboxHitLevel = (int)hitbox.hitLevel,
            hitboxIsProjectile = hitbox.isProjectile,
            hitboxActive = hitbox.active,
            hitboxHasHit = hitbox.hasHit,

            landingRecoveryTicksRemaining = snapshot.movementSnapshot.landingRecoveryTicksRemaining,
            queuedJumpMoveX = snapshot.movementSnapshot.queuedJumpMoveX,
            usedAirNormalThisJump = snapshot.movementSnapshot.usedAirNormalThisJump,

            builderLastVisualState = (int)snapshot.renderStateBuilderSnapshot.lastVisualState,
            builderLastVisualMoveType = (int)snapshot.renderStateBuilderSnapshot.lastVisualMoveType,
            builderVisualStateFrame = snapshot.renderStateBuilderSnapshot.visualStateFrame,
            builderAnimationSerial = snapshot.renderStateBuilderSnapshot.animationSerial,
            builderLastSimulationState = (int)snapshot.renderStateBuilderSnapshot.lastSimulationState,
            builderHasLastSimulationState = snapshot.renderStateBuilderSnapshot.hasLastSimulationState,
            builderCrouchTransitionFramesRemaining = snapshot.renderStateBuilderSnapshot.crouchTransitionFramesRemaining,

            inputHistoryEntries = netEntries,
            inputHistoryNextWriteIndex = historySnapshot.nextWriteIndex,
            inputHistoryCount = historySnapshot.count
        };
    }

    private static Fighter.Snapshot BuildFighterSnapshotFromNet(NetFighterState state, Fighter fighter)
    {
        AttackData attackData = null;
        MoveType attackMoveType = (MoveType)state.attackMoveType;
        if (state.attackHasData && attackMoveType != MoveType.None && fighter != null)
            attackData = fighter.ResolveAttackDataForNetState(attackMoveType, state.facingRight);

        Hitbox hitbox = new Hitbox
        {
            box = new Box(
                new Vector2(state.hitboxCenterX, state.hitboxCenterY),
                new Vector2(state.hitboxHalfX, state.hitboxHalfY)
            ),
            damage = state.hitboxDamage,
            hitstunFrames = state.hitboxHitstunFrames,
            blockstunFrames = state.hitboxBlockstunFrames,
            blockPushback = state.hitboxBlockPushback,
            chipDamage = state.hitboxChipDamage,
            attackerBlockstopFrames = state.hitboxAttackerBlockstopFrames,
            hitLevel = (HitLevel)state.hitboxHitLevel,
            isProjectile = state.hitboxIsProjectile,
            active = state.hitboxActive,
            hasHit = state.hitboxHasHit
        };

        NetPendingProjectileRequestState pending = state.pendingProjectileRequest;
        ProjectileSpawnRequest pendingProjectile = new ProjectileSpawnRequest(
            owner: fighter,
            position: new Vector2(pending.positionX, pending.positionY),
            velocity: new Vector2(pending.velocityX, pending.velocityY),
            halfSize: new Vector2(pending.halfSizeX, pending.halfSizeY),
            lifetimeFrames: Mathf.Max(1, pending.lifetimeFrames),
            damage: pending.damage,
            hitstunFrames: Mathf.Max(1, pending.hitstunFrames),
            blockstunFrames: Mathf.Max(1, pending.blockstunFrames),
            blockPushback: Mathf.Max(0f, pending.blockPushback),
            chipDamage: Mathf.Max(0, pending.chipDamage),
            attackerBlockstopFrames: pending.attackerBlockstopFrames,
            hitLevel: (HitLevel)pending.hitLevel
        );

        NetInputHistoryEntry[] sourceEntries = state.inputHistoryEntries ?? Array.Empty<NetInputHistoryEntry>();
        InputHistoryBuffer.HistoryEntry[] historyEntries = new InputHistoryBuffer.HistoryEntry[sourceEntries.Length];
        for (int i = 0; i < sourceEntries.Length; i++)
        {
            historyEntries[i] = new InputHistoryBuffer.HistoryEntry(
                sourceEntries[i].input,
                sourceEntries[i].relativeDirection
            );
        }

        return new Fighter.Snapshot(
            new Vector2(state.positionX, state.positionY),
            new Vector2(state.velocityX, state.velocityY),
            state.isGrounded,
            state.facingRight,
            (FighterState)state.fighterState,
            state.stateFrame,
            state.transitionedThisTick,
            state.stateFrameFrozenThisTick,
            state.hitstopFramesRemaining,
            state.hitstunFramesRemaining,
            state.blockstunFramesRemaining,
            state.isHoldingBlockInput,
            state.canCurrentlyBlock,
            state.isHoldingValidBlockDirection,
            state.parryInputPressedThisTick,
            state.parryLastMoveXSign,
            state.parryWindowFramesRemaining,
            state.parryLockoutFramesRemaining,
            state.health,
            state.totalDamageTaken,
            state.totalChipDamageTaken,
            (HitResultType)state.lastReceivedHitResult,
            (HitLevel)state.lastReceivedHitLevel,
            state.lastReceivedStunFrames,
            state.lastReceivedChipDamage,
            state.hadAttackInputThisTick,
            0,
            "No input history yet",
            pending.hasPending,
            pendingProjectile,
            state.lightPressBufferFramesRemaining,
            state.mediumPressBufferFramesRemaining,
            state.heavyPressBufferFramesRemaining,
            state.isDefeated,
            new FighterRenderSnapshot(
                (FighterVisualState)state.renderVisualState,
                (MoveType)state.renderMoveType,
                state.renderVisualStateFrame,
                state.renderAnimationSerial,
                state.renderRestartAnimation,
                state.renderFreezeAnimation
            ),
            new FighterAttackController.Snapshot(
                attackMoveType,
                attackData,
                state.attackFrame,
                hitbox
            ),
            new FighterMovementController.Snapshot(
                state.landingRecoveryTicksRemaining,
                state.queuedJumpMoveX,
                state.usedAirNormalThisJump
            ),
            new FighterRenderStateBuilder.Snapshot(
                (FighterVisualState)state.builderLastVisualState,
                (MoveType)state.builderLastVisualMoveType,
                state.builderVisualStateFrame,
                state.builderAnimationSerial,
                (FighterState)state.builderLastSimulationState,
                state.builderHasLastSimulationState,
                state.builderCrouchTransitionFramesRemaining
            ),
            new InputHistoryBuffer.Snapshot(
                historyEntries,
                state.inputHistoryNextWriteIndex,
                state.inputHistoryCount
            )
        );
    }

    private static int HashFighterState(int seed, Fighter fighter, int slot)
    {
        if (fighter == null)
            return HashInt(seed, -slot);

        int hash = seed;
        hash = HashInt(hash, slot);
        hash = HashInt(hash, QuantizeFloat(fighter.Position.x));
        hash = HashInt(hash, QuantizeFloat(fighter.Position.y));
        hash = HashInt(hash, QuantizeFloat(fighter.Velocity.x));
        hash = HashInt(hash, QuantizeFloat(fighter.Velocity.y));
        hash = HashInt(hash, fighter.FacingRight ? 1 : 0);
        hash = HashInt(hash, fighter.IsGrounded ? 1 : 0);
        hash = HashInt(hash, fighter.IsInHitstop ? 1 : 0);
        hash = HashInt(hash, (int)fighter.CurrentState);
        hash = HashInt(hash, fighter.StateFrame);
        hash = HashInt(hash, (int)fighter.CurrentMoveType);
        hash = HashInt(hash, fighter.HitstunFramesRemaining);
        hash = HashInt(hash, fighter.BlockstunFramesRemaining);
        hash = HashInt(hash, fighter.Health);
        hash = HashInt(hash, fighter.IsDefeated ? 1 : 0);
        hash = HashInt(hash, (int)fighter.LastReceivedHitResult);
        hash = HashInt(hash, (int)fighter.LastReceivedHitLevel);
        return hash;
    }

    private static int QuantizeFloat(float value)
    {
        return Mathf.RoundToInt(value * 1000f);
    }

    private static int HashInt(int seed, int value)
    {
        unchecked
        {
            return (seed * 31) + value;
        }
    }

}
