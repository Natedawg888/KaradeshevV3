using System.Collections.Generic;
using UnityEngine;

public class VolcanoLavaBridge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private VolcanoManager volcanoManager;
    [SerializeField] private LavaOverlayManager lavaOverlayManager;

    [Tooltip("Preferred source for volcano-owned grid/weather cells.")]
    [SerializeField] private WeatherGridManager weatherGridManager;

    [Tooltip("Fallback if WeatherGridManager ownership lookup misses.")]
    [SerializeField] private MonoEnvironmentDataSource environmentDataSource;

    [Header("Behavior")]
    [Tooltip("Seed lava when a volcano first starts erupting.")]
    public bool seedOnEruptionStarted = true;

    [Tooltip("If true, seed lava for volcanoes that are already erupting when this bridge starts.")]
    public bool seedExistingEruptionsOnStart = true;

    [Tooltip("If true, always include the volcano primary cell even if ownership lookup returns other cells.")]
    public bool includePrimaryCellAlways = true;

    [Tooltip("Use WeatherGridManager cached environment ownership first.")]
    public bool preferWeatherGridOwnership = true;

    [Tooltip("Prevents the same volcano from seeding lava more than once while lava is already active.")]
    public bool preventDuplicateSeedingPerVolcano = true;

    [Header("Debug")]
    public bool debugLogging = false;

    private readonly List<TileCoord> footprintScratch = new List<TileCoord>(16);
    private readonly HashSet<VolcanoTileState> seededVolcanoes = new HashSet<VolcanoTileState>();

    private VolcanoManager subscribedManager;

    private void Awake()
    {
        EnsureLinks();
    }

    private void OnEnable()
    {
        EnsureLinks();
        RebindManagerEvents();
    }

    private void Start()
    {
        EnsureLinks();
        RebindManagerEvents();

        if (seedExistingEruptionsOnStart)
            SeedExistingEruptions();
    }

    private void OnDisable()
    {
        UnbindManagerEvents();
    }

    private void EnsureLinks()
    {
        if (volcanoManager == null)
            volcanoManager = VolcanoManager.Instance;

        if (lavaOverlayManager == null)
            lavaOverlayManager = LavaOverlayManager.Instance;

        if (weatherGridManager == null)
            weatherGridManager = WeatherGridManager.Instance;

        if (environmentDataSource == null)
            environmentDataSource = MonoEnvironmentDataSource.Instance;
    }

    private void RebindManagerEvents()
    {
        if (subscribedManager == volcanoManager)
            return;

        UnbindManagerEvents();

        subscribedManager = volcanoManager;

        if (subscribedManager == null)
            return;

        subscribedManager.OnEruptionStarted += HandleEruptionStarted;
        subscribedManager.OnVolcanoRevertedToMountain += HandleVolcanoNoLongerActive;
        subscribedManager.OnEruptingVolcanoAdvanced += HandleEruptingVolcanoAdvanced;
    }

    private void UnbindManagerEvents()
    {
        if (subscribedManager == null)
            return;

        subscribedManager.OnEruptionStarted -= HandleEruptionStarted;
        subscribedManager.OnVolcanoRevertedToMountain -= HandleVolcanoNoLongerActive;
        subscribedManager.OnEruptingVolcanoAdvanced -= HandleEruptingVolcanoAdvanced;

        subscribedManager = null;
    }

    private void HandleEruptionStarted(VolcanoTileState volcano)
    {
        if (!seedOnEruptionStarted)
            return;

        SeedLavaForVolcano(volcano);
    }

    private void HandleVolcanoNoLongerActive(VolcanoTileState volcano)
    {
        // Allows this volcano to seed lava again in a future eruption.
        if (volcano != null)
            seededVolcanoes.Remove(volcano);
    }

    private void SeedExistingEruptions()
    {
        EnsureLinks();

        if (volcanoManager == null)
            return;

        IReadOnlyList<VolcanoTileState> erupting = volcanoManager.EruptingVolcanoes;

        for (int i = 0; i < erupting.Count; i++)
            SeedLavaForVolcano(erupting[i]);
    }

    public void SeedLavaForVolcano(VolcanoTileState volcano)
    {
        if (volcano == null)
            return;

        EnsureLinks();

        if (lavaOverlayManager == null)
        {
            if (debugLogging)
                Debug.LogWarning("[VolcanoLavaBridge] No LavaOverlayManager found.");

            return;
        }

        if (preventDuplicateSeedingPerVolcano && seededVolcanoes.Contains(volcano))
            return;

        if (!volcano.TryGetPrimaryCell(out TileCoord primaryCell))
        {
            if (debugLogging)
                Debug.LogWarning($"[VolcanoLavaBridge] Could not resolve primary cell for {volcano.name}");

            return;
        }

        footprintScratch.Clear();
        ResolveVolcanoOwnedCells(volcano, primaryCell, footprintScratch);

        if (footprintScratch.Count == 0)
            footprintScratch.Add(primaryCell);

        lavaOverlayManager.EmitLavaFromSourceCells(
            footprintScratch,
            maxNewCells: 0,
            maxDistanceFromSource: volcano.GetMaxLavaDistanceFromSource(),
            heat01: volcano.GetLavaHeatOnEmission(),
            coolingDelayTurns: volcano.GetLavaCoolingDelayTurns(),
            coolingTurns: volcano.GetLavaCoolingTurns(),
            ignoreEnvironmentBlockForSourceCells: true);

        if (preventDuplicateSeedingPerVolcano)
            seededVolcanoes.Add(volcano);

        if (debugLogging)
        {
            string cells = "";
            for (int i = 0; i < footprintScratch.Count; i++)
                cells += $"({footprintScratch[i].x},{footprintScratch[i].y}) ";

            Debug.Log(
                $"[VolcanoLavaBridge] Seeded lava for {volcano.name}. " +
                $"primary=({primaryCell.x},{primaryCell.y}) " +
                $"cells={footprintScratch.Count} {cells}");
        }
    }

    private void ResolveVolcanoOwnedCells(
        VolcanoTileState volcano,
        TileCoord primaryCell,
        List<TileCoord> results)
    {
        results.Clear();

        if (preferWeatherGridOwnership && weatherGridManager != null)
        {
            EnvironmentControl env = ResolveVolcanoEnvironment(volcano, primaryCell);

            if (env != null &&
                weatherGridManager.TryGetEnvironmentCoveredCells(env, results) &&
                results.Count > 0)
            {
                // Important:
                // Do NOT blindly add primaryCell here.
                // If VolcanoTileState returned default/stale (0,0), this causes lava in the map corner.
                if (includePrimaryCellAlways && CoordAlreadyInList(results, primaryCell))
                    AddUniqueCoord(results, primaryCell);

                return;
            }

            if (weatherGridManager.TryGetEnvironmentCoveredCells(primaryCell, results) &&
                results.Count > 0)
            {
                // Same rule: only use what the ownership lookup returned.
                // Do not add primaryCell again.
                return;
            }
        }

        if (environmentDataSource != null &&
            environmentDataSource.TryGetFootprintCoords(primaryCell, results) &&
            results.Count > 0)
        {
            // EnvironmentDataSource returns the footprint, so do not force-add primaryCell.
            return;
        }

        if (includePrimaryCellAlways)
            AddUniqueCoord(results, primaryCell);
    }

    private bool CoordAlreadyInList(List<TileCoord> list, TileCoord coord)
    {
        if (list == null)
            return false;

        for (int i = 0; i < list.Count; i++)
        {
            TileCoord existing = list[i];

            if (existing.x == coord.x && existing.y == coord.y)
                return true;
        }

        return false;
    }

    private EnvironmentControl ResolveVolcanoEnvironment(VolcanoTileState volcano, TileCoord primaryCell)
    {
        if (volcano == null)
            return null;

        EnvironmentControl env = volcano.GetComponent<EnvironmentControl>();

        if (env == null)
            env = volcano.GetComponentInParent<EnvironmentControl>(true);

        if (env == null)
            env = volcano.GetComponentInChildren<EnvironmentControl>(true);

        if (env != null)
            return env;

        if (weatherGridManager != null &&
            weatherGridManager.TryGetEnvironmentAtCell(primaryCell.x, primaryCell.y, out env))
        {
            return env;
        }

        return null;
    }

    private void HandleEruptingVolcanoAdvanced(VolcanoTileState volcano)
    {
        if (volcano == null)
            return;

        EnsureLinks();

        if (lavaOverlayManager == null)
            return;

        if (!volcano.TryGetPrimaryCell(out TileCoord primaryCell))
        {
            if (debugLogging)
                Debug.LogWarning($"[VolcanoLavaBridge] Could not resolve primary cell for erupting volcano {volcano.name}");

            return;
        }

        footprintScratch.Clear();
        ResolveVolcanoOwnedCells(volcano, primaryCell, footprintScratch);

        if (footprintScratch.Count == 0)
            footprintScratch.Add(primaryCell);

        int added = lavaOverlayManager.EmitLavaFromSourceCells(
            footprintScratch,
            volcano.GetLavaCellsPerEruptionTurn(),
            volcano.GetMaxLavaDistanceFromSource(),
            volcano.GetLavaHeatOnEmission(),
            volcano.GetLavaCoolingDelayTurns(),
            volcano.GetLavaCoolingTurns(),
            ignoreEnvironmentBlockForSourceCells: true);

        if (debugLogging)
        {
            Debug.Log(
                $"[VolcanoLavaBridge] {volcano.name} emitted lava this eruption turn. " +
                $"Added={added} PerTurn={volcano.GetLavaCellsPerEruptionTurn()} " +
                $"MaxDistance={volcano.GetMaxLavaDistanceFromSource()}");
        }
    }

    private void AddUniqueCoord(List<TileCoord> list, TileCoord coord)
    {
        if (list == null)
            return;

        for (int i = 0; i < list.Count; i++)
        {
            TileCoord existing = list[i];

            if (existing.x == coord.x && existing.y == coord.y)
                return;
        }

        list.Add(coord);
    }

    [ContextMenu("Seed Existing Eruptions Now")]
    private void ContextSeedExistingEruptions()
    {
        SeedExistingEruptions();
    }

    [ContextMenu("Clear Seeded Volcano Cache")]
    private void ContextClearSeededCache()
    {
        seededVolcanoes.Clear();
    }
}