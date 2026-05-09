using System.Collections.Generic;
using UnityEngine;

// Place one instance in the FinalSetup scene.
// Subscribes to flood and lava events → sets TileStateFlags on affected
// EnvironmentResourceNode tiles and adds temporary spawners.
//
// Inspector setup:
//   floodSystem     — assign FloodSimulationSystem
//   lavaManager     — assign LavaOverlayManager
//   floodSpawner    — assign FloodDebrisSpawner SO  (from Resources/ResourceSpawners/Weather/)
//   ashSpawner      — assign AshDepositSpawner SO   (from Resources/ResourceSpawners/BurntRemains/)
public class TileStateDisasterWirer : MonoBehaviour
{
    [Header("Disaster Systems")]
    public FloodSimulationSystem floodSystem;
    public LavaOverlayManager    lavaManager;

    [Header("Spawner Definitions")]
    [Tooltip("Temporary spawner added when a tile floods. Assign FloodDebrisSpawner SO.")]
    public ResourceSpawnerDefinition floodSpawner;

    [Header("Debug")]
    [SerializeField] private bool debugLogging;
    [Tooltip("Lifetime in turns for the flood spawner.")]
    [Min(1)] public int floodSpawnerLifetime = 4;

    [Tooltip("Temporary spawner added when lava covers a tile. Assign AshDepositSpawner SO.")]
    public ResourceSpawnerDefinition ashSpawner;
    [Tooltip("Lifetime in turns for the ash spawner.")]
    [Min(1)] public int ashSpawnerLifetime = 15;

    // ── Tile→Node cache ───────────────────────────────────────────────────────

    private readonly Dictionary<TileCoord, EnvironmentResourceNode> _nodeCache = new();

    private void BuildNodeCache()
    {
        _nodeCache.Clear();
        var envSource = MonoEnvironmentDataSource.Instance;
        if (envSource == null) return;
        foreach (var kvp in envSource.AllTiles)
        {
            var node = kvp.Value?.GetComponent<EnvironmentResourceNode>();
            if (node != null) _nodeCache[kvp.Key] = node;
        }
    }

    private EnvironmentResourceNode GetNode(TileCoord coord)
    {
        if (_nodeCache.Count == 0) BuildNodeCache();
        _nodeCache.TryGetValue(coord, out var n);
        return n;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        if (floodSystem == null)
            floodSystem = FindObjectOfType<FloodSimulationSystem>();
        if (lavaManager == null)
            lavaManager = FindObjectOfType<LavaOverlayManager>();

        if (MonoEnvironmentDataSource.Instance != null)
            MonoEnvironmentDataSource.Instance.OnEnvironmentRegisteredOrUpdated += OnTileRegistered;
    }

    private void OnEnable()
    {
        if (floodSystem != null)
        {
            floodSystem.OnFloodStarted += HandleFloodStarted;
            floodSystem.OnFloodDrained += HandleFloodDrained;
        }
        if (lavaManager != null)
            lavaManager.OnLavaCellActivated += HandleLavaCellActivated;
    }

    private void OnDisable()
    {
        if (floodSystem != null)
        {
            floodSystem.OnFloodStarted -= HandleFloodStarted;
            floodSystem.OnFloodDrained -= HandleFloodDrained;
        }
        if (lavaManager != null)
            lavaManager.OnLavaCellActivated -= HandleLavaCellActivated;

        if (MonoEnvironmentDataSource.Instance != null)
            MonoEnvironmentDataSource.Instance.OnEnvironmentRegisteredOrUpdated -= OnTileRegistered;
    }

    // ── Flood handlers ────────────────────────────────────────────────────────

    private void HandleFloodStarted(TileCoord coord, FloodCellState _)
    {
        var node = GetNode(coord);
        if (node == null) return;

        node.SetTileState(TileStateFlags.WasRecentlyFlooded, true);

        if (floodSpawner != null && !node.HasSpawner(floodSpawner.spawnerID))
        {
            node.AddTemporarySpawner(floodSpawner, floodSpawnerLifetime,
                                     SpawnerSourceReason.WeatherCreated, runImmediately: true);
            if (debugLogging)
                Debug.Log($"[DisasterWirer] Flood spawner added at {coord} (lifetime={floodSpawnerLifetime}t)");
        }
    }

    private void HandleFloodDrained(TileCoord coord, FloodCellState _)
    {
        var node = GetNode(coord);
        if (node == null) return;
        node.SetTileState(TileStateFlags.WasRecentlyFlooded, false);
        if (debugLogging)
            Debug.Log($"[DisasterWirer] Flood cleared at {coord} — WasRecentlyFlooded unset");
    }

    // ── Lava / Ash handler ────────────────────────────────────────────────────

    private void HandleLavaCellActivated(TileCoord coord)
    {
        var node = GetNode(coord);
        if (node == null) return;

        node.SetTileState(TileStateFlags.HasVolcanicAsh, true);

        if (ashSpawner != null && !node.HasSpawner(ashSpawner.spawnerID))
        {
            node.AddTemporarySpawner(ashSpawner, ashSpawnerLifetime,
                                     SpawnerSourceReason.WeatherCreated, runImmediately: false);
            if (debugLogging)
                Debug.Log($"[DisasterWirer] Ash spawner added at {coord} (lifetime={ashSpawnerLifetime}t)");
        }
    }

    // ── Cache invalidation ────────────────────────────────────────────────────

    private void OnTileRegistered(TileCoord coord, EnvironmentControl env)
    {
        if (env == null) return;
        var node = env.GetComponent<EnvironmentResourceNode>();
        if (node != null) _nodeCache[coord] = node;
        else _nodeCache.Remove(coord);
    }
}
