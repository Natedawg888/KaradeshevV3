using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolves tornado effects against animal groups after tornado state updates.
/// Keeps animal gameplay consequences separate from TornadoSimulationSystem.
/// Processes affected groups over frames to avoid spikes on busy tiles.
///
//// Optimization:
/// If a tornado tile is checked and found to have no animal groups,
/// it is cached as empty and will not be checked again until the tornado
/// moves/spawns/expires and invalidates that cached-empty state.
/// </summary>
public class TornadoAnimalEffectResolver : MonoBehaviour
{
    public static TornadoAnimalEffectResolver Instance { get; private set; }

    [Header("References")]
    [SerializeField] private TornadoSimulationSystem tornadoSimulationSystem;
    [SerializeField] private WeatherGridManager weatherGridManager;

    [Header("Lifecycle")]
    [SerializeField] private bool processOnEnable = true;
    [SerializeField] private bool processOnTornadoStateChanged = true;

    [Header("Animal Tornado Effects")]
    [SerializeField] private bool tornadoAffectsAnimalGroups = true;
    [Min(0)][SerializeField] private int tornadoAnimalGroupDamagePerTurn = 15;
    [SerializeField] private bool tornadoCanThrowAnimalGroups = true;
    [Range(0f, 1f)][SerializeField] private float tornadoAnimalGroupThrowChance = 0.5f;

    [Header("Batching")]
    [Min(1)][SerializeField] private int tornadoAnimalGroupsProcessedPerFrame = 8;

    [Header("End Turn Recheck")]
    [SerializeField] private bool recheckActiveTornadoAnimalTilesAtEndOfTurn = true;

    private Coroutine _endTurnRecheckCoroutine;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    private readonly Queue<TileCoord> _pendingTornadoAnimalTiles = new Queue<TileCoord>(16);
    private readonly HashSet<int> _pendingTornadoAnimalTileKeys = new HashSet<int>();

    // Tiles that were checked while a tornado was on them and had no animals.
    // These are skipped until the tornado moves/spawns/expires and invalidates the entry.
    private readonly HashSet<int> _knownEmptyActiveTornadoTileKeys = new HashSet<int>();

    private readonly List<int> _groupIdsAtTileScratch = new List<int>(16);

    private Coroutine _processCoroutine;

    private TornadoSimulationSystem _subscribedTornadoSimulationSystem;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureLinks();
    }

    private void OnEnable()
    {
        EnsureLinks();
        RebindSourceEvents();

        TurnSystem.SubscribeToEndOfTurn(HandleEndTurn);

        if (processOnEnable)
            RequestQueueActiveTornadoTiles();
    }

    private void OnDisable()
    {
        UnbindSourceEvents();
        TurnSystem.UnsubscribeFromEndOfTurn(HandleEndTurn);

        if (_processCoroutine != null)
        {
            StopCoroutine(_processCoroutine);
            _processCoroutine = null;
        }

        if (_endTurnRecheckCoroutine != null)
        {
            StopCoroutine(_endTurnRecheckCoroutine);
            _endTurnRecheckCoroutine = null;
        }
    }

    private void HandleEndTurn()
    {
        if (!recheckActiveTornadoAnimalTilesAtEndOfTurn || !isActiveAndEnabled)
            return;

        if (_endTurnRecheckCoroutine != null)
            StopCoroutine(_endTurnRecheckCoroutine);

        _endTurnRecheckCoroutine = StartCoroutine(EndTurnRecheckAnimalTilesNextFrame());
    }

    private IEnumerator EndTurnRecheckAnimalTilesNextFrame()
    {
        yield return null;

        _endTurnRecheckCoroutine = null;
        RequestQueueActiveTornadoTiles();
    }

    private void OnDestroy()
    {
        UnbindSourceEvents();

        if (Instance == this)
            Instance = null;
    }

    public void InstallRuntimeRefs(
        TornadoSimulationSystem newTornadoSimulationSystem = null,
        WeatherGridManager newWeatherGridManager = null,
        bool processNow = true)
    {
        if (newTornadoSimulationSystem != null)
            tornadoSimulationSystem = newTornadoSimulationSystem;

        if (newWeatherGridManager != null)
            weatherGridManager = newWeatherGridManager;

        RebindSourceEvents();

        if (processNow)
            RequestQueueActiveTornadoTiles();
    }

    private void HandleTornadoStateChanged()
    {
        if (!processOnTornadoStateChanged)
            return;

        RequestQueueActiveTornadoTiles();
    }

    private void HandleTornadoSpawned(TornadoSpawnEventData data)
    {
        ClearKnownEmptyForTile(data.cell);
        QueueTileForProcessing(data.cell);
    }

    private void HandleTornadoMoved(TornadoMoveEventData data)
    {
        ClearKnownEmptyForTile(data.fromCell);
        ClearKnownEmptyForTile(data.toCell);

        QueueTileForProcessing(data.toCell);
    }

    private void HandleTornadoExpired(TornadoExpireEventData data)
    {
        ClearKnownEmptyForTile(data.cell);
    }

    private void RequestQueueActiveTornadoTiles()
    {
        if (!tornadoAffectsAnimalGroups)
            return;

        if (tornadoAnimalGroupDamagePerTurn <= 0 && !tornadoCanThrowAnimalGroups)
            return;

        AnimalSimulation sim = GetAnimalSimulationForTornadoes();
        if (sim == null)
            return;

        if (tornadoSimulationSystem == null || !tornadoSimulationSystem.IsInitialized)
            return;

        IReadOnlyList<Vector2Int> activeCells = tornadoSimulationSystem.GetActiveTornadoCells();
        if (activeCells == null || activeCells.Count == 0)
            return;

        for (int i = 0; i < activeCells.Count; i++)
        {
            Vector2Int cell = activeCells[i];
            int key = GetCellKey(cell.x, cell.y);

            // Skip tiles already proven empty while the tornado remains there.
            if (_knownEmptyActiveTornadoTileKeys.Contains(key))
                continue;

            QueueTileForProcessing(new TileCoord(cell.x, cell.y));
        }

        EnsureProcessingRoutine();
    }

    private void QueueTileForProcessing(Vector2Int cell)
    {
        QueueTileForProcessing(new TileCoord(cell.x, cell.y));
    }

    private void QueueTileForProcessing(TileCoord tile)
    {
        if (tornadoSimulationSystem == null || !tornadoSimulationSystem.IsInitialized)
            return;

        if (tile.x < 0 || tile.x >= tornadoSimulationSystem.Columns ||
            tile.y < 0 || tile.y >= tornadoSimulationSystem.Rows)
        {
            return;
        }

        int key = GetCellKey(tile.x, tile.y);

        if (_knownEmptyActiveTornadoTileKeys.Contains(key))
            return;

        if (!_pendingTornadoAnimalTileKeys.Add(key))
            return;

        _pendingTornadoAnimalTiles.Enqueue(tile);
    }

    private void EnsureProcessingRoutine()
    {
        if (_processCoroutine != null || !isActiveAndEnabled || _pendingTornadoAnimalTiles.Count == 0)
            return;

        _processCoroutine = StartCoroutine(ProcessQueuedTornadoAnimalTilesRoutine());
    }

    private IEnumerator ProcessQueuedTornadoAnimalTilesRoutine()
    {
        AnimalSimulation sim = GetAnimalSimulationForTornadoes();
        if (sim == null)
        {
            _processCoroutine = null;
            yield break;
        }

        int maxPerFrame = Mathf.Max(1, tornadoAnimalGroupsProcessedPerFrame);
        int processedThisFrame = 0;

        while (_pendingTornadoAnimalTiles.Count > 0)
        {
            TileCoord tile = _pendingTornadoAnimalTiles.Dequeue();
            int tileKey = GetCellKey(tile.x, tile.y);
            _pendingTornadoAnimalTileKeys.Remove(tileKey);

            if (!IsTornadoAtAnimalTile(tile))
            {
                // Tornado is no longer here, so this empty-state no longer matters.
                _knownEmptyActiveTornadoTileKeys.Remove(tileKey);
                continue;
            }

            _groupIdsAtTileScratch.Clear();
            sim.GetGroupIdsAtTileNonAlloc(tile, _groupIdsAtTileScratch);

            if (_groupIdsAtTileScratch.Count == 0)
            {
                // Mark as empty and do not check again until tornado moves/spawns/expires.
                _knownEmptyActiveTornadoTileKeys.Add(tileKey);

                if (debugLogging) {}
                    //Debug.Log($"[TornadoAnimalEffectResolver] Cached empty tornado animal tile at {tile}.");

                continue;
            }

            // Tile has animals, so ensure it stays eligible for future turns.
            _knownEmptyActiveTornadoTileKeys.Remove(tileKey);

            for (int i = 0; i < _groupIdsAtTileScratch.Count; i++)
            {
                if (!IsTornadoAtAnimalTile(tile))
                    break;

                int groupId = _groupIdsAtTileScratch[i];

                sim.TryApplyTornadoEffectToGroup(
                    groupId,
                    tornadoAnimalGroupDamagePerTurn,
                    tornadoCanThrowAnimalGroups,
                    tornadoAnimalGroupThrowChance,
                    IsTornadoAtAnimalTile,
                    IsAnimalTileValidForTornadoPush,
                    debugLogging);

                processedThisFrame++;

                if (processedThisFrame >= maxPerFrame)
                {
                    processedThisFrame = 0;
                    yield return null;
                }
            }

            // After damage/throws, the tile may now be empty.
            if (!IsTornadoAtAnimalTile(tile) || !sim.HasGroupsAtTile(tile))
            {
                _knownEmptyActiveTornadoTileKeys.Add(tileKey);

                if (debugLogging) {}
                    //Debug.Log($"[TornadoAnimalEffectResolver] Tile became empty after tornado processing at {tile}.");
            }
            else
            {
                _knownEmptyActiveTornadoTileKeys.Remove(tileKey);
            }
        }

        _processCoroutine = null;

        // Catch anything queued while the coroutine was winding down.
        if (isActiveAndEnabled && _pendingTornadoAnimalTiles.Count > 0)
            EnsureProcessingRoutine();
    }

    private AnimalSimulation GetAnimalSimulationForTornadoes()
    {
        return AnimalSimulationAccess.Current;
    }

    private bool IsTornadoAtAnimalTile(TileCoord tile)
    {
        if (tornadoSimulationSystem == null || !tornadoSimulationSystem.IsInitialized)
            return false;

        return tornadoSimulationSystem.IsTornadoActiveAtCell(tile.x, tile.y);
    }

    private bool IsAnimalTileValidForTornadoPush(TileCoord tile)
    {
        if (tornadoSimulationSystem == null || !tornadoSimulationSystem.IsInitialized)
            return false;

        if (tile.x < 0 || tile.x >= tornadoSimulationSystem.Columns)
            return false;

        if (tile.y < 0 || tile.y >= tornadoSimulationSystem.Rows)
            return false;

        // Do not push into another currently active tornado cell.
        return !IsTornadoAtAnimalTile(tile);
    }

    private void ClearKnownEmptyForTile(Vector2Int cell)
    {
        ClearKnownEmptyForTile(new TileCoord(cell.x, cell.y));
    }

    private void ClearKnownEmptyForTile(TileCoord tile)
    {
        if (tornadoSimulationSystem == null || !tornadoSimulationSystem.IsInitialized)
            return;

        if (tile.x < 0 || tile.x >= tornadoSimulationSystem.Columns ||
            tile.y < 0 || tile.y >= tornadoSimulationSystem.Rows)
        {
            return;
        }

        _knownEmptyActiveTornadoTileKeys.Remove(GetCellKey(tile.x, tile.y));
    }

    private int GetCellKey(int x, int y)
    {
        int cols = tornadoSimulationSystem != null
            ? Mathf.Max(1, tornadoSimulationSystem.Columns)
            : 1;

        return x + (y * cols);
    }

    private void EnsureLinks()
    {
        if (tornadoSimulationSystem == null)
            tornadoSimulationSystem = TornadoSimulationSystem.Instance;

        if (weatherGridManager == null)
            weatherGridManager = WeatherGridManager.Instance;
    }

    private void RebindSourceEvents()
    {
        if (_subscribedTornadoSimulationSystem == tornadoSimulationSystem)
            return;

        UnbindSourceEvents();
        _subscribedTornadoSimulationSystem = tornadoSimulationSystem;

        if (_subscribedTornadoSimulationSystem != null)
        {
            _subscribedTornadoSimulationSystem.OnTornadoStateChanged += HandleTornadoStateChanged;
            _subscribedTornadoSimulationSystem.OnTornadoSpawned += HandleTornadoSpawned;
            _subscribedTornadoSimulationSystem.OnTornadoMoved += HandleTornadoMoved;
            _subscribedTornadoSimulationSystem.OnTornadoExpired += HandleTornadoExpired;
        }
    }

    private void UnbindSourceEvents()
    {
        if (_subscribedTornadoSimulationSystem == null)
            return;

        _subscribedTornadoSimulationSystem.OnTornadoStateChanged -= HandleTornadoStateChanged;
        _subscribedTornadoSimulationSystem.OnTornadoSpawned -= HandleTornadoSpawned;
        _subscribedTornadoSimulationSystem.OnTornadoMoved -= HandleTornadoMoved;
        _subscribedTornadoSimulationSystem.OnTornadoExpired -= HandleTornadoExpired;
        _subscribedTornadoSimulationSystem = null;
    }
}
