using UnityEngine;
using System.Collections.Generic;

// Simulation.cs is the core of the game loop, responsible for updating the state of 
// all fighters and handling interactions between them.
// It encapsulates the main fighting match simulation logic for the game loop.
public class Simulation
{
    private static Simulation _instance;
    public static Simulation Instance
    {
        get
        {
            if (_instance == null)
                _instance = new Simulation();
            return _instance;
        }
    }

    private Fighter player1;
    private Fighter player2;
    private readonly List<Projectile> projectiles = new List<Projectile>();
    private readonly Dictionary<int, DebugBoxVisual> projectileVisuals = new Dictionary<int, DebugBoxVisual>();
    private readonly Dictionary<int, DebugBoxVisual> projectileHitboxVisuals = new Dictionary<int, DebugBoxVisual>();
    private Transform projectileVisualRoot;
    private Transform projectileHitboxVisualRoot;
    private int nextProjectileId = 1;
    const float FIGHTER_START_POSITION_OFFSET = 10f;

    public void Initialize(FighterView p1View, FighterView p2View)
    {
        player1 = new Fighter(p1View, new Vector2(-FIGHTER_START_POSITION_OFFSET, 0f));
        player2 = new Fighter(p2View, new Vector2(FIGHTER_START_POSITION_OFFSET, 0f));

        player1.SetOpponent(player2);
        player2.SetOpponent(player1);

        // Connect views to fighters
        p1View.Initialize(player1);
        p2View.Initialize(player2);

        ClearProjectilesAndVisuals();
    }

    public void Tick(InputFrame input_p1)
    {
        // Get input for the fighters on this simulation tick
        // Player 2 is still neutral for now
        InputFrame input_p2 = InputFrame.Neutral; // placeholder for P2

        // Update each fighter with their input
        player1.Tick(input_p1);
        player2.Tick(input_p2);

        SpawnPendingProjectiles();
        UpdateProjectiles();

        // Simulation-specific logic
        ResolveHitDetection();
        ResolvePushboxes(); // Prevent overlapping
        ClampToStage(player1);
        ClampToStage(player2);
    }

    public void Render()
    {
        player1.Render();
        player2.Render();
        RenderProjectiles();
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

    private void ResolveHitDetection()
    {
        ResolveHitForPair(player1, player2);
        ResolveHitForPair(player2, player1);
        ResolveProjectileHits();
    }

    private void ResolveHitForPair(Fighter attacker, Fighter defender)
    {
        if (!attacker.HasActiveUnspentHitbox)
            return;

        if (!attacker.CurrentHitbox.box.Overlaps(defender.CurrentHurtbox))
            return;

        defender.ApplyHit(attacker.CurrentHitbox);
        attacker.ApplySuccessfulHitstopAsAttacker();
        attacker.MarkCurrentHitboxAsSpent();
    }

    private float stageLeft = -80f;
    private float stageRight = 80f;

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
                RemoveProjectileVisual(projectile.id);
                projectiles.RemoveAt(i);
            }
        }
    }

    private bool IsProjectileOutOfStage(Projectile projectile)
    {
        return projectile.position.x + projectile.halfSize.x < stageLeft
            || projectile.position.x - projectile.halfSize.x > stageRight;
    }

    private void ResolveProjectileHits()
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

            defender.ApplyHit(projectile.ToHitbox());
            projectile.owner.ApplySuccessfulHitstopAsAttacker();
            projectile.active = false;
            RemoveProjectileVisual(projectile.id);
            projectiles.RemoveAt(i);
        }
    }

    private void RenderProjectiles()
    {
        for (int i = 0; i < projectiles.Count; i++)
        {
            Projectile projectile = projectiles[i];
            DebugBoxVisual visual = GetOrCreateProjectileVisual(projectile.id);
            visual.SetBox(projectile.CurrentBox);
            visual.SetVisible(projectile.active);

            DebugBoxVisual hitboxVisual = GetOrCreateProjectileHitboxVisual(projectile.id);
            hitboxVisual.SetBox(projectile.CurrentBox);
            hitboxVisual.SetVisible(projectile.active && FighterView.GlobalShowBoxes);
        }
    }

    private DebugBoxVisual GetOrCreateProjectileVisual(int projectileId)
    {
        if (projectileVisuals.TryGetValue(projectileId, out DebugBoxVisual existing))
            return existing;

        GameObject visualObject = new GameObject($"ProjectileVisual_{projectileId}");
        visualObject.transform.SetParent(GetOrCreateProjectileVisualRoot(), false);
        DebugBoxVisual visual = visualObject.AddComponent<DebugBoxVisual>();
        Projectile projectile = GetProjectileById(projectileId);
        Color tint = projectile != null ? projectile.tint : new Color(1f, 0.6f, 0.1f, 0.9f);
        visual.Initialize(tint);
        visual.SetSortingOrder(RenderOrder.World.Projectiles);
        if (projectile != null)
            visual.SetSprite(projectile.sprite);
        visual.SetVisible(false);
        projectileVisuals[projectileId] = visual;
        return visual;
    }

    private Transform GetOrCreateProjectileVisualRoot()
    {
        if (projectileVisualRoot != null)
            return projectileVisualRoot;

        GameObject rootObject = GameObject.Find("ProjectileDebugRoot");
        if (rootObject == null)
            rootObject = new GameObject("ProjectileDebugRoot");

        projectileVisualRoot = rootObject.transform;
        return projectileVisualRoot;
    }

    private void RemoveProjectileVisual(int projectileId)
    {
        if (projectileVisuals.TryGetValue(projectileId, out DebugBoxVisual visual))
        {
            projectileVisuals.Remove(projectileId);
            if (visual != null)
                Object.Destroy(visual.gameObject);
        }

        if (projectileHitboxVisuals.TryGetValue(projectileId, out DebugBoxVisual hitboxVisual))
        {
            projectileHitboxVisuals.Remove(projectileId);
            if (hitboxVisual != null)
                Object.Destroy(hitboxVisual.gameObject);
        }
    }

    private void ClearProjectilesAndVisuals()
    {
        for (int i = 0; i < projectiles.Count; i++)
            RemoveProjectileVisual(projectiles[i].id);

        projectiles.Clear();
    }

    private DebugBoxVisual GetOrCreateProjectileHitboxVisual(int projectileId)
    {
        if (projectileHitboxVisuals.TryGetValue(projectileId, out DebugBoxVisual existing))
            return existing;

        GameObject visualObject = new GameObject($"ProjectileHitboxVisual_{projectileId}");
        visualObject.transform.SetParent(GetOrCreateProjectileHitboxVisualRoot(), false);
        DebugBoxVisual visual = visualObject.AddComponent<DebugBoxVisual>();
        visual.Initialize(new Color(1f, 0f, 0f, 0.75f));
        visual.SetSortingOrder(RenderOrder.World.DebugBoxes);
        visual.SetVisible(false);
        projectileHitboxVisuals[projectileId] = visual;
        return visual;
    }

    private Transform GetOrCreateProjectileHitboxVisualRoot()
    {
        if (projectileHitboxVisualRoot != null)
            return projectileHitboxVisualRoot;

        GameObject rootObject = GameObject.Find("ProjectileHitboxDebugRoot");
        if (rootObject == null)
            rootObject = new GameObject("ProjectileHitboxDebugRoot");

        projectileHitboxVisualRoot = rootObject.transform;
        return projectileHitboxVisualRoot;
    }

    private Projectile GetProjectileById(int projectileId)
    {
        for (int i = 0; i < projectiles.Count; i++)
        {
            if (projectiles[i].id == projectileId)
                return projectiles[i];
        }

        return null;
    }

    private void ClampToStage(Fighter fighter)
    {
        float half = fighter.PushboxHalfWidth;

        if (fighter.Position.x - half < stageLeft)
            fighter.SetHorizontal(stageLeft + half);

        if (fighter.Position.x + half > stageRight)
            fighter.SetHorizontal(stageRight - half);
    }

}
