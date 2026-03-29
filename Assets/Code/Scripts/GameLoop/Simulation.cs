using UnityEngine;
using System.Collections.Generic;

// Simulation.cs is the core of the game loop, responsible for updating the state of 
// all fighters and handling interactions between them.
// It encapsulates the main fighting match simulation logic for the game loop.
public class Simulation
{
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
    
    public IReadOnlyList<Projectile> ActiveProjectiles => projectiles;
    public Fighter Player1 => player1;
    public Fighter Player2 => player2;
    public int CurrentFrame => simulationFrame;

    public Simulation(MatchConfig config = null)
    {
        if (config != null)
        {
            fighterStartPositionOffset = Mathf.Abs(config.fighterStartPositionOffset);
            stageLeft = config.stageLeft;
            stageRight = config.stageRight;
            enableBackwalkTether = config.enableBackwalkTether;
            maxBackwalkSeparation = Mathf.Max(0f, config.maxBackwalkSeparation);
        }
        else
        {
            fighterStartPositionOffset = 10f;
            stageLeft = -80f;
            stageRight = 80f;
            enableBackwalkTether = false;
            maxBackwalkSeparation = 22f;
        }
    }

    public void Initialize(
        FighterConfig player1Config,
        FighterConfig player2Config,
        string player1Name = "Player1",
        string player2Name = "Player2"
    )
    {
        player1 = new Fighter(player1Config, new Vector2(-fighterStartPositionOffset, 0f), player1Name);
        player2 = new Fighter(player2Config, new Vector2(fighterStartPositionOffset, 0f), player2Name);

        player1.SetOpponent(player2);
        player2.SetOpponent(player1);
        previousPlayer1X = player1.Position.x;
        previousPlayer2X = player2.Position.x;

        ClearProjectiles();
    }

    public void Tick(FrameInput frameInput)
    {
        previousPlayer1X = player1.Position.x;
        previousPlayer2X = player2.Position.x;

        // Update each fighter with their input
        player1.Tick(frameInput.player1);
        player2.Tick(frameInput.player2);

        SpawnPendingProjectiles();
        UpdateProjectiles();

        // Simulation-specific logic
        ResolveHitDetection();
        ResolvePushboxes(); // Prevent overlapping
        ClampToStage(player1);
        ClampToStage(player2);
        ResolveBackwalkTether();
        simulationFrame = frameInput.frameIndex > simulationFrame
            ? frameInput.frameIndex
            : simulationFrame + 1;
    }

    public int ComputeDeterminismHash()
    {
        unchecked
        {
            int hash = 17;
            hash = HashInt(hash, simulationFrame);
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
                hash = HashInt(hash, projectile.lifetimeFramesRemaining);
            }

            return hash;
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
            request.sprite,
            request.tint
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

            if (hitEvent.source == PendingHitSource.Projectile)
            {
                if (hitEvent.projectile == null || !hitEvent.projectile.active)
                    continue;

                hitEvent.defender.ApplyHit(hitEvent.hitbox);
                hitEvent.attacker.ApplySuccessfulHitstopAsAttacker();
                hitEvent.projectile.active = false;
                continue;
            }

            hitEvent.defender.ApplyHit(hitEvent.hitbox);
            hitEvent.attacker.ApplySuccessfulHitstopAsAttacker();
            hitEvent.attacker.MarkCurrentHitboxAsSpent();
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

    private void ClearProjectiles()
    {
        projectiles.Clear();
        nextProjectileId = 1;
        simulationFrame = 0;
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
        float leftRetreatDelta = Mathf.Max(0f, leftPreviousX - leftFighter.Position.x);
        float rightRetreatDelta = Mathf.Max(0f, rightFighter.Position.x - rightPreviousX);

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
