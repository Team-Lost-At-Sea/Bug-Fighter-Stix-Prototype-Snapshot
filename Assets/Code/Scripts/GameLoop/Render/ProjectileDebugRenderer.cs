using System.Collections.Generic;
using UnityEngine;

public sealed class ProjectileDebugRenderer
{
    private readonly Dictionary<int, DebugBoxVisual> projectileVisuals = new Dictionary<int, DebugBoxVisual>();
    private readonly Dictionary<int, DebugBoxVisual> projectileHitboxVisuals = new Dictionary<int, DebugBoxVisual>();
    private readonly HashSet<int> renderActiveIds = new HashSet<int>();
    private readonly List<int> staleProjectileIds = new List<int>();
    private Transform projectileVisualRoot;
    private Transform projectileHitboxVisualRoot;

    public void Render(IReadOnlyList<Projectile> projectiles)
    {
        renderActiveIds.Clear();

        for (int i = 0; i < projectiles.Count; i++)
        {
            Projectile projectile = projectiles[i];
            renderActiveIds.Add(projectile.id);

            DebugBoxVisual visual = GetOrCreateProjectileVisual(projectile);
            visual.SetBox(projectile.CurrentBox);
            visual.SetVisible(projectile.active);

            DebugBoxVisual hitboxVisual = GetOrCreateProjectileHitboxVisual(projectile.id);
            hitboxVisual.SetBox(projectile.CurrentBox);
            hitboxVisual.SetVisible(projectile.active && FighterView.GlobalShowBoxes);
        }

        staleProjectileIds.Clear();
        foreach (int projectileId in projectileVisuals.Keys)
        {
            if (!renderActiveIds.Contains(projectileId))
                staleProjectileIds.Add(projectileId);
        }

        for (int i = 0; i < staleProjectileIds.Count; i++)
            Remove(staleProjectileIds[i]);
    }

    public void Remove(int projectileId)
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

    public void ClearAll()
    {
        foreach (KeyValuePair<int, DebugBoxVisual> pair in projectileVisuals)
        {
            if (pair.Value != null)
                Object.Destroy(pair.Value.gameObject);
        }

        foreach (KeyValuePair<int, DebugBoxVisual> pair in projectileHitboxVisuals)
        {
            if (pair.Value != null)
                Object.Destroy(pair.Value.gameObject);
        }

        projectileVisuals.Clear();
        projectileHitboxVisuals.Clear();
        projectileVisualRoot = null;
        projectileHitboxVisualRoot = null;
    }

    private DebugBoxVisual GetOrCreateProjectileVisual(Projectile projectile)
    {
        if (projectileVisuals.TryGetValue(projectile.id, out DebugBoxVisual existing))
            return existing;

        GameObject visualObject = new GameObject($"ProjectileVisual_{projectile.id}");
        visualObject.transform.SetParent(GetOrCreateProjectileVisualRoot(), false);
        DebugBoxVisual visual = visualObject.AddComponent<DebugBoxVisual>();
        visual.Initialize(projectile.tint);
        visual.SetSortingOrder(RenderOrder.World.Projectiles);
        visual.SetSprite(projectile.sprite);
        visual.SetVisible(false);
        projectileVisuals[projectile.id] = visual;
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
}
