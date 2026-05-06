using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolves tornado effects against unit groups after tornado state updates.
/// Uses PlayerUnitManager as the authoritative source of live player unit groups + owners,
/// while still using TileUnitGroupControl for tile-local operations and push targets.
/// Empty tornado unit tiles are cached and skipped until the tornado moves/spawns/expires.
/// </summary>
public class TornadoUnitEffectResolver : MonoBehaviour
{
    public static TornadoUnitEffectResolver Instance { get; private set; }

    [Header("References")]
    [SerializeField] private TornadoSimulationSystem tornadoSimulationSystem;
    [SerializeField] private WeatherGridManager weatherGridManager;

    [Header("Lifecycle")]
    [SerializeField] private bool processOnEnable = true;
    [SerializeField] private bool processOnTornadoStateChanged = true;

    [Header("Unit Tornado Effects")]
    [SerializeField] private bool tornadoAffectsUnitGroups = true;
    [Min(0)][SerializeField] private int tornadoUnitGroupDamagePerTurn = 20;
    [SerializeField] private bool tornadoCanThrowUnitGroups = true;
    [Range(0f, 1f)][SerializeField] private float tornadoUnitThrowChance = 0.5f;
    [SerializeField] private bool tornadoAvoidThrowingIntoOtherTornadoes = true;

    [Header("Batching")]
    [Min(1)][SerializeField] private int tornadoUnitGroupsProcessedPerFrame = 8;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    [Header("End Turn Recheck")]
    [SerializeField] private bool recheckActiveTornadoUnitTilesAtEndOfTurn = true;

    private Coroutine _endTurnRecheckCoroutine;

    private readonly Queue<Vector2Int> _pendingTornadoUnitTiles = new Queue<Vector2Int>(16);
    private readonly HashSet<long> _pendingTornadoUnitTileKeys = new HashSet<long>();
    private readonly HashSet<long> _knownEmptyActiveTornadoUnitTileKeys = new HashSet<long>();

    private readonly List<PlayerUnitManager.GroupInfo> _trackedGroupsScratch = new List<PlayerUnitManager.GroupInfo>(128);
    private readonly List<TileUnitGroupControl> _unitControlsAtTileScratch = new List<TileUnitGroupControl>(8);
    private readonly HashSet<TileUnitGroupControl> _uniqueUnitControlsAtTileScratch = new HashSet<TileUnitGroupControl>();

    private readonly List<TileUnitGroupData> _tmpUnitGroupSnapshot = new List<TileUnitGroupData>(16);
    private readonly HashSet<string> _processedUnitGroupsThisPass = new HashSet<string>();

    private Coroutine _processCoroutine;
    private TornadoSimulationSystem _subscribedTornadoSimulationSystem;

    private static readonly Vector2Int[] s_tornadoPushOffsets =
    {
        new Vector2Int( 0,  1),
        new Vector2Int( 1,  0),
        new Vector2Int( 0, -1),
        new Vector2Int(-1,  0),
        new Vector2Int( 1,  1),
        new Vector2Int( 1, -1),
        new Vector2Int(-1, -1),
        new Vector2Int(-1,  1)
    };

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

        _pendingTornadoUnitTiles.Clear();
        _pendingTornadoUnitTileKeys.Clear();
        _knownEmptyActiveTornadoUnitTileKeys.Clear();
        _processedUnitGroupsThisPass.Clear();
        _tmpUnitGroupSnapshot.Clear();
        _trackedGroupsScratch.Clear();
        _unitControlsAtTileScratch.Clear();
        _uniqueUnitControlsAtTileScratch.Clear();
    }

    private void HandleEndTurn()
    {
        if (!recheckActiveTornadoUnitTilesAtEndOfTurn || !isActiveAndEnabled)
            return;

        if (_endTurnRecheckCoroutine != null)
            StopCoroutine(_endTurnRecheckCoroutine);

        _endTurnRecheckCoroutine = StartCoroutine(EndTurnRecheckUnitTilesNextFrame());
    }

    private IEnumerator EndTurnRecheckUnitTilesNextFrame()
    {
        // Let other end-turn systems finish first this frame.
        yield return null;

        _endTurnRecheckCoroutine = null;
        ForceQueueActiveTornadoUnitTilesForEndTurnRecheck();
    }

    private void ForceQueueActiveTornadoUnitTilesForEndTurnRecheck()
    {
        if (!tornadoAffectsUnitGroups)
            return;

        if (tornadoUnitGroupDamagePerTurn <= 0 && !tornadoCanThrowUnitGroups)
            return;

        if (tornadoSimulationSystem == null || !tornadoSimulationSystem.IsInitialized)
            return;

        IReadOnlyList<Vector2Int> activeCells = tornadoSimulationSystem.GetActiveTornadoCells();
        if (activeCells == null || activeCells.Count == 0)
            return;

        for (int i = 0; i < activeCells.Count; i++)
        {
            Vector2Int cell = activeCells[i];
            long key = MakeGridKey(cell.x, cell.y);

            // Force a recheck even if this tile was cached empty earlier in the turn.
            _knownEmptyActiveTornadoUnitTileKeys.Remove(key);

            if (_pendingTornadoUnitTileKeys.Add(key))
                _pendingTornadoUnitTiles.Enqueue(cell);
        }

        EnsureProcessingRoutine();
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
        if (!tornadoAffectsUnitGroups)
            return;

        if (tornadoUnitGroupDamagePerTurn <= 0 && !tornadoCanThrowUnitGroups)
            return;

        if (tornadoSimulationSystem == null || !tornadoSimulationSystem.IsInitialized)
            return;

        IReadOnlyList<Vector2Int> activeCells = tornadoSimulationSystem.GetActiveTornadoCells();
        if (activeCells == null || activeCells.Count == 0)
            return;

        for (int i = 0; i < activeCells.Count; i++)
        {
            Vector2Int cell = activeCells[i];
            long key = MakeGridKey(cell.x, cell.y);

            if (_knownEmptyActiveTornadoUnitTileKeys.Contains(key))
                continue;

            QueueTileForProcessing(cell);
        }

        EnsureProcessingRoutine();
    }

    private void QueueTileForProcessing(Vector2Int tile)
    {
        if (tornadoSimulationSystem == null || !tornadoSimulationSystem.IsInitialized)
            return;

        if (!IsInBounds(tile.x, tile.y))
            return;

        long key = MakeGridKey(tile.x, tile.y);

        if (_knownEmptyActiveTornadoUnitTileKeys.Contains(key))
            return;

        if (!_pendingTornadoUnitTileKeys.Add(key))
            return;

        _pendingTornadoUnitTiles.Enqueue(tile);
    }

    private void EnsureProcessingRoutine()
    {
        if (_processCoroutine != null || !isActiveAndEnabled || _pendingTornadoUnitTiles.Count == 0)
            return;

        _processCoroutine = StartCoroutine(ProcessQueuedTornadoUnitTilesRoutine());
    }

    private IEnumerator ProcessQueuedTornadoUnitTilesRoutine()
    {
        int maxPerFrame = Mathf.Max(1, tornadoUnitGroupsProcessedPerFrame);
        int processedThisFrame = 0;

        _processedUnitGroupsThisPass.Clear();

        while (_pendingTornadoUnitTiles.Count > 0)
        {
            Vector2Int tile = _pendingTornadoUnitTiles.Dequeue();
            long tileKey = MakeGridKey(tile.x, tile.y);
            _pendingTornadoUnitTileKeys.Remove(tileKey);

            if (!IsTornadoAtUnitTile(tile))
            {
                _knownEmptyActiveTornadoUnitTileKeys.Remove(tileKey);
                continue;
            }

            if (!CollectUnitControlsAtTile(tile, _unitControlsAtTileScratch))
            {
                _knownEmptyActiveTornadoUnitTileKeys.Add(tileKey);

                if (debugLogging)
                    Debug.Log($"[TornadoUnitEffectResolver] Cached empty tornado unit tile at {tile.x},{tile.y}.");

                continue;
            }

            _knownEmptyActiveTornadoUnitTileKeys.Remove(tileKey);

            for (int controlIndex = 0; controlIndex < _unitControlsAtTileScratch.Count; controlIndex++)
            {
                if (!IsTornadoAtUnitTile(tile))
                    break;

                TileUnitGroupControl unitControl = _unitControlsAtTileScratch[controlIndex];
                if (unitControl == null || !unitControl.HasAnyGroups)
                    continue;

                _tmpUnitGroupSnapshot.Clear();

                IReadOnlyList<TileUnitGroupData> groups = unitControl.Groups;
                if (groups == null || groups.Count == 0)
                    continue;

                for (int i = 0; i < groups.Count; i++)
                {
                    TileUnitGroupData g = groups[i];
                    if (g != null)
                        _tmpUnitGroupSnapshot.Add(g);
                }

                for (int i = 0; i < _tmpUnitGroupSnapshot.Count; i++)
                {
                    if (!IsTornadoAtUnitTile(tile))
                        break;

                    TileUnitGroupData group = _tmpUnitGroupSnapshot[i];
                    if (group == null || string.IsNullOrWhiteSpace(group.groupId))
                        continue;

                    if (!_processedUnitGroupsThisPass.Add(group.groupId))
                        continue;

                    group.ClearMovementAndActionStateForTornado();

                    int oldUnitCount = Mathf.Max(0, group.unitCount);
                    int unitsLost = 0;

                    if (tornadoUnitGroupDamagePerTurn > 0)
                        unitsLost = group.ApplyDamageAndReturnUnitsLost(tornadoUnitGroupDamagePerTurn);

                    if (group.unitCount <= 0 || group.currentHealth <= 0)
                    {
                        if (debugLogging)
                        {
                            Debug.Log(
                                $"[TornadoUnitEffectResolver] Tornado destroyed unit group {group.groupId} at {tile.x},{tile.y}.");
                        }

                        unitControl.RemoveGroupDueToFatalities(group);
                        processedThisFrame++;
                    }
                    else
                    {
                        if (unitsLost > 0)
                            ApplyPopulationLossFromUnitLoss(group, oldUnitCount, unitsLost);

                        unitControl.RefreshMarker(group);

                        if (tornadoCanThrowUnitGroups &&
                            Random.value <= tornadoUnitThrowChance)
                        {
                            TileUnitGroupControl target = FindAdjacentTornadoPushTarget(tile, unitControl);
                            if (target != null)
                            {
                                unitControl.MoveGroupTo(group, target);
                                target.RefreshMarker(group);

                                if (debugLogging && target.TryGetOwningGridPosition(out Vector2Int targetGrid))
                                {
                                    Debug.Log(
                                        $"[TornadoUnitEffectResolver] Tornado pushed unit group {group.groupId} " +
                                        $"from {tile.x},{tile.y} to {targetGrid.x},{targetGrid.y}.");
                                }
                            }
                        }

                        processedThisFrame++;
                    }

                    if (processedThisFrame >= maxPerFrame)
                    {
                        processedThisFrame = 0;
                        yield return null;
                    }
                }

                _tmpUnitGroupSnapshot.Clear();
            }

            if (!IsTornadoAtUnitTile(tile) || !CollectUnitControlsAtTile(tile, _unitControlsAtTileScratch))
            {
                _knownEmptyActiveTornadoUnitTileKeys.Add(tileKey);

                if (debugLogging)
                    Debug.Log($"[TornadoUnitEffectResolver] Tile became empty after unit tornado processing at {tile.x},{tile.y}.");
            }
            else
            {
                _knownEmptyActiveTornadoUnitTileKeys.Remove(tileKey);
            }
        }

        _processedUnitGroupsThisPass.Clear();
        _processCoroutine = null;

        if (isActiveAndEnabled && _pendingTornadoUnitTiles.Count > 0)
            EnsureProcessingRoutine();
    }

    private bool CollectUnitControlsAtTile(Vector2Int tile, List<TileUnitGroupControl> results)
    {
        if (results == null)
            return false;

        results.Clear();
        _uniqueUnitControlsAtTileScratch.Clear();
        _trackedGroupsScratch.Clear();

        PlayerUnitManager unitManager = PlayerUnitManager.Instance;
        if (unitManager == null)
            return false;

        unitManager.GetAllGroups(_trackedGroupsScratch);
        if (_trackedGroupsScratch.Count == 0)
            return false;

        for (int i = 0; i < _trackedGroupsScratch.Count; i++)
        {
            PlayerUnitManager.GroupInfo info = _trackedGroupsScratch[i];
            TileUnitGroupControl owner = info.owner;
            TileUnitGroupData data = info.data;

            if (owner == null || data == null)
                continue;

            if (!owner.HasAnyGroups)
                continue;

            if (!owner.TryGetOwningGridPosition(out Vector2Int ownerGrid))
                continue;

            if (ownerGrid != tile)
                continue;

            if (_uniqueUnitControlsAtTileScratch.Add(owner))
                results.Add(owner);
        }

        return results.Count > 0;
    }

    private void ApplyPopulationLossFromUnitLoss(TileUnitGroupData group, int oldUnitCount, int unitsLost)
    {
        if (group == null)
            return;

        if (unitsLost <= 0 || oldUnitCount <= 0)
            return;

        if (string.IsNullOrWhiteSpace(group.populationReservationId) || group.reservedPopulation <= 0)
            return;

        PlayersPopulationManager pop = PlayersPopulationManager.Instance;
        if (pop == null)
            return;

        int populationLoss = Mathf.Clamp(
            Mathf.RoundToInt(group.reservedPopulation * (unitsLost / (float)oldUnitCount)),
            0,
            group.reservedPopulation
        );

        if (populationLoss <= 0)
            return;

        pop.ApplyPenaltyFromReservation(group.populationReservationId, populationLoss);
        group.reservedPopulation = Mathf.Max(0, group.reservedPopulation - populationLoss);

        if (group.reservedPopulation <= 0)
        {
            pop.ReleaseReservation(group.populationReservationId);
            group.populationReservationId = null;
            group.reservedPopulation = 0;
        }
    }

    private TileUnitGroupControl FindAdjacentTornadoPushTarget(Vector2Int origin, TileUnitGroupControl currentControl)
    {
        int startIndex = Random.Range(0, s_tornadoPushOffsets.Length);

        for (int i = 0; i < s_tornadoPushOffsets.Length; i++)
        {
            Vector2Int offset = s_tornadoPushOffsets[(startIndex + i) % s_tornadoPushOffsets.Length];
            Vector2Int candidateGrid = origin + offset;

            if (!IsInBounds(candidateGrid.x, candidateGrid.y))
                continue;

            if (tornadoAvoidThrowingIntoOtherTornadoes && IsTornadoAtUnitTile(candidateGrid))
                continue;

            TileUnitGroupControl target = FindUnitControlAtGrid(candidateGrid);
            if (target == null || target == currentControl)
                continue;

            return target;
        }

        return null;
    }

    private TileUnitGroupControl FindUnitControlAtGrid(Vector2Int grid)
    {
        PlayerUnitManager unitManager = PlayerUnitManager.Instance;
        if (unitManager == null)
            return null;

        _trackedGroupsScratch.Clear();
        unitManager.GetAllGroups(_trackedGroupsScratch);

        for (int i = 0; i < _trackedGroupsScratch.Count; i++)
        {
            PlayerUnitManager.GroupInfo info = _trackedGroupsScratch[i];
            TileUnitGroupControl owner = info.owner;

            if (owner == null || !owner.HasAnyGroups)
                continue;

            if (!owner.TryGetOwningGridPosition(out Vector2Int ownerGrid))
                continue;

            if (ownerGrid == grid)
                return owner;
        }

        return null;
    }

    private bool IsTornadoAtUnitTile(Vector2Int tile)
    {
        if (tornadoSimulationSystem == null || !tornadoSimulationSystem.IsInitialized)
            return false;

        return tornadoSimulationSystem.IsTornadoActiveAtCell(tile.x, tile.y);
    }

    private bool IsInBounds(int x, int y)
    {
        if (tornadoSimulationSystem == null || !tornadoSimulationSystem.IsInitialized)
            return false;

        return x >= 0 && x < tornadoSimulationSystem.Columns &&
               y >= 0 && y < tornadoSimulationSystem.Rows;
    }

    private void ClearKnownEmptyForTile(Vector2Int tile)
    {
        _knownEmptyActiveTornadoUnitTileKeys.Remove(MakeGridKey(tile.x, tile.y));
    }

    private static long MakeGridKey(int x, int y)
    {
        return ((long)x << 32) ^ (uint)y;
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