using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static cache mapping each ResourceDefinition to every way it can be obtained:
/// world spawners (base-environment and event-triggered), crafting recipes, production plans.
/// No scene object required — call Invalidate() then GetSources() to rebuild on demand.
/// </summary>
public static class ResourceSourceCache
{
    public class SpawnerSource
    {
        public ResourceSpawnerDefinition spawner;
        public List<EnvironmentType> environmentTypes;
        public List<EnvironmentTileType> tileTypes;
        public int minAmount;
        public int maxAmount;
        // True when the spawner requires a special tile state rather than a base environment.
        public bool isExternal;
        public string externalSourceLabel;
    }

    public class ResourceSources
    {
        public ResourceDefinition resource;
        public readonly List<SpawnerSource> spawnerSources = new();
        public readonly List<CraftingRecipe> craftingRecipes = new();
        public readonly List<ProductionPlan> productionPlans = new();

        public bool HasAnySources =>
            spawnerSources.Count > 0 ||
            craftingRecipes.Count > 0 ||
            productionPlans.Count > 0;
    }

    private static readonly Dictionary<string, ResourceSources> _cache = new();
    private static readonly HashSet<string> _dynamicIds = new();
    private static readonly List<ResourceSpawnerDefinition> _dynamicDefs = new();
    private static bool _built;

    // ── Dynamic spawner registration ──────────────────────────────────────────

    /// <summary>
    /// Call from any handler that adds spawners dynamically at runtime
    /// (e.g. TileStateResourceSpawnerHandler, animal death handlers).
    /// Safe to call multiple times with the same definition.
    /// </summary>
    public static void RegisterDynamicSpawner(ResourceSpawnerDefinition def)
    {
        if (def == null || !_dynamicIds.Add(def.spawnerID)) return;
        _dynamicDefs.Add(def);
        _built = false; // invalidate so next GetSources re-builds with the new def
    }

    public static void UnregisterDynamicSpawner(ResourceSpawnerDefinition def)
    {
        if (def == null || !_dynamicIds.Remove(def.spawnerID)) return;
        _dynamicDefs.Remove(def);
        _built = false;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public static void Invalidate() => _built = false;

    public static ResourceSources GetSources(ResourceDefinition resource)
    {
        if (resource == null) return null;
        if (!_built) BuildCache();
        _cache.TryGetValue(resource.resourceID, out var src);
        return src;
    }

    public static void BuildCache()
    {
        _cache.Clear();
        ScanSpawners();
        ScanCrafting();
        ScanProduction();
        _built = true;
    }

    // ── Spawners ──────────────────────────────────────────────────────────────

    private static void ScanSpawners()
    {
        var seenIds = new HashSet<string>();
        var allDefs = new List<ResourceSpawnerDefinition>();

        // Base + active-runtime spawners from every registered node
        var nodeMgr = ResourceNodeManager.Instance;
        if (nodeMgr != null)
            foreach (var node in nodeMgr.GetAllNodes())
                node?.CollectAllSpawnerDefinitions(seenIds, allDefs);

        // Dynamic spawner definitions pre-registered by event-driven handlers
        foreach (var def in _dynamicDefs)
            AddDefIfNew(def, seenIds, allDefs);

        // Register each definition's outputs into the cache
        foreach (var def in allDefs)
        {
            if (def?.outputs == null) continue;

            string externalLabel = GetExternalSourceLabel(def);

            foreach (var output in def.outputs)
            {
                if (output?.resource == null) continue;

                var sources = GetOrCreate(output.resource);
                sources.spawnerSources.Add(new SpawnerSource
                {
                    spawner          = def,
                    environmentTypes = def.conditions?.requiredEnvironmentTypes != null
                        ? new List<EnvironmentType>(def.conditions.requiredEnvironmentTypes)
                        : new List<EnvironmentType>(),
                    tileTypes        = def.conditions?.requiredTileTypes != null
                        ? new List<EnvironmentTileType>(def.conditions.requiredTileTypes)
                        : new List<EnvironmentTileType>(),
                    minAmount           = output.minAmount,
                    maxAmount           = output.maxAmount,
                    isExternal          = externalLabel != null,
                    externalSourceLabel = externalLabel,
                });
            }
        }
    }

    private static void AddDefIfNew(
        ResourceSpawnerDefinition def,
        HashSet<string> seenIds,
        List<ResourceSpawnerDefinition> results)
    {
        if (def != null && seenIds.Add(def.spawnerID))
            results.Add(def);
    }

    // ── Crafting ──────────────────────────────────────────────────────────────

    private static void ScanCrafting()
    {
        var mgr = CraftingRecipeManager.Instance;
        if (mgr == null) return;

        foreach (var recipe in mgr.GetAll())
        {
            if (recipe?.outputResources == null) continue;
            foreach (var output in recipe.outputResources)
            {
                if (output?.resource == null) continue;
                var sources = GetOrCreate(output.resource);
                if (!sources.craftingRecipes.Contains(recipe))
                    sources.craftingRecipes.Add(recipe);
            }
        }
    }

    // ── Production ────────────────────────────────────────────────────────────

    private static void ScanProduction()
    {
        var mgr = ProductionPlanManager.Instance;
        if (mgr == null) return;

        foreach (var plan in mgr.GetAll())
        {
            if (plan?.outputs == null) continue;
            foreach (var output in plan.outputs)
            {
                if (output?.resource == null) continue;
                var sources = GetOrCreate(output.resource);
                if (!sources.productionPlans.Contains(plan))
                    sources.productionPlans.Add(plan);
            }
        }
    }

    // ── External label ────────────────────────────────────────────────────────

    public static string GetExternalSourceLabel(ResourceSpawnerDefinition def)
    {
        if (def == null) return null;

        var c = def.conditions;
        if (c != null)
        {
            if (c.requiresHasCarcass)         return "Animal Remains";
            if (c.requiresHasBeenIgnited)     return "Burnt Tiles";
            if (c.requiresWasRecentlyFlooded) return "Flooding";
            if (c.requiresIsCurrentlyWet)     return "Rain / Wet";
            if (c.requiresHasVolcanicAsh)     return "Volcanic Ash";
        }

        return def.category switch
        {
            SpawnerCategory.AnimalRemains => "Animal Remains",
            SpawnerCategory.BurntRemains  => "Burnt Tiles",
            _                             => null
        };
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static ResourceSources GetOrCreate(ResourceDefinition resource)
    {
        if (!_cache.TryGetValue(resource.resourceID, out var src))
        {
            src = new ResourceSources { resource = resource };
            _cache[resource.resourceID] = src;
        }
        return src;
    }
}
