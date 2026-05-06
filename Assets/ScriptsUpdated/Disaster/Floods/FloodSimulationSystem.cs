using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class FloodSimulationSystem : MonoBehaviour
{
    [Header("References")]
    public GridManager gridManager;
    public MapGenerator mapGenerator;
    public MonoEnvironmentDataSource environmentDataSource;
    public LavaOverlayManager lavaOverlayManager;
    public WeatherFireSystem weatherFireSystem;

    [Tooltip("Optional fallback only. Flood will not ask WeatherGridManager to rebuild anything.")]
    public WeatherGridManager weatherGridManager;

    [Header("Environment Lookup")]
    [Tooltip("Preferred terrain lookup uses MonoEnvironmentDataSource.GetTileData like tornadoes do.")]
    public bool useMonoEnvironmentDataSource = true;

    [Tooltip("Optional fallback if MonoEnvironmentDataSource has no tile at the cell.")]
    public bool useWeatherGridEnvironmentFallback = true;

    [Tooltip("If false, flood cannot exist on cells with no registered environment tile.")]
    public bool allowFloodOnCellsWithoutEnvironment = false;

    [Header("Main Settings")]
    public bool enableFlooding = true;
    public bool enableRainFlooding = true;
    public bool enableTsunamiFlooding = true;
    public bool floodUpdateOnEndTurn = true;

    [Header("Limits")]
    [Min(1)] public int maxActiveFloodCells = 2500;
    [Min(1)] public int cellsProcessedPerFrame = 128;

    [Header("Spread")]
    [Range(0f, 1f)] public float floodSpreadThreshold = 0.28f;
    [Range(0f, 1f)] public float floodSpreadAmount = 0.16f;

    [Tooltip("0 = all spread water is lost, 1 = all spread water reaches neighbour.")]
    [Range(0f, 1f)] public float floodSpreadLossMultiplier = 0.65f;

    [Tooltip("0 = no absorption, 1 = land absorbs all incoming spread water.")]
    [Range(0f, 1f)] public float landAbsorptionMultiplier = 0.25f;

    [Tooltip("0 = no absorption, 1 = beach/coast absorbs all incoming spread water.")]
    [Range(0f, 1f)] public float beachAbsorptionMultiplier = 0.1f;

    [Tooltip("Higher values make river/lake/ocean source cells drain slower.")]
    [Min(0.01f)] public float waterSourceRetentionMultiplier = 2f;

    public bool allowDiagonalSpread = false;

    [Header("Drainage")]
    [Range(0f, 1f)] public float baseDrainPerTurn = 0.04f;
    [Range(0f, 1f)] public float evaporationPerTurn = 0.015f;

    [Header("Rain Flooding")]
    [Range(0f, 1f)] public float rainfallAccumulationPerRain01 = 0.2f;
    [Range(0f, 5f)] public float rainFloodThreshold = 0.8f;
    [Min(0)] public int rainFloodSourceRadius = 2;
    [Range(0f, 1f)] public float maxRainFloodInputPerTurn = 0.22f;
    [Range(0f, 1f)] public float floodRainDecayPerTurn = 0.15f;

    [Header("Tsunami Flooding")]
    [Range(0f, 2f)] public float tsunamiFloodInputMultiplier = 0.55f;
    [Range(0f, 1f)] public float tsunamiMinEnergyToFlood = 0.2f;
    [Range(0f, 1f)] public float tsunamiMaxFloodDepth = 0.85f;
    [Range(0f, 4f)] public float tsunamiFloodDrainMultiplier = 1.35f;

    [Header("Flood / Fire Interaction")]
    public bool floodExtinguishesFire = true;

    [Range(0f, 1f)]
    public float minFloodDepthToExtinguishFire = 0.01f;

    [Header("Source Rules")]
    public bool allowSaltLakeAsFloodSource = false;

    [Header("Lava/Flood Blocking")]
    [Tooltip("If true, flood cannot start, spread, or be added to cells that currently contain lava.")]
    public bool lavaBlocksFloodAdvance = true;

    [Tooltip("If true, flood will not even top up an existing flood cell if lava is now on that cell.")]
    public bool lavaBlocksFloodTopUp = true;

    [Header("Debug")]
    public bool debugLogging = false;
    public bool drawGizmos = true;
    public KeyCode debugForceFloodKey = KeyCode.F;
    public Vector2Int debugSelectedCell;
    [Range(0f, 1f)] public float debugForceFloodAmount = 0.75f;

    public event Action<FloodCellChangedEvent> OnFloodCellChanged;
    public event Action<IReadOnlyList<TileCoord>> OnFloodCellsChanged;
    public event Action<TileCoord, FloodCellState> OnFloodStarted;
    public event Action<TileCoord, FloodCellState> OnFloodExpanded;
    public event Action<TileCoord, FloodCellState> OnFloodDrained;
    public event Action OnFloodCleared;

    private readonly Dictionary<TileCoord, FloodCellState> activeFloodCells =
        new Dictionary<TileCoord, FloodCellState>();

    private readonly Dictionary<TileCoord, float> rainfallAccumulator =
        new Dictionary<TileCoord, float>();

    private readonly HashSet<TileCoord> dirtyCells = new HashSet<TileCoord>();
    private readonly List<TileCoord> dirtyCellBuffer = new List<TileCoord>();

    private readonly List<TileCoord> activeSnapshot = new List<TileCoord>();

    private readonly Dictionary<TileCoord, PendingFloodInput> pendingSpreadInput =
        new Dictionary<TileCoord, PendingFloodInput>();

    private int currentTurn;
    private bool isAdvancing;

    private static readonly Vector2Int[] OrthogonalDirs =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1)
    };

    private static readonly Vector2Int[] DiagonalDirs =
    {
        new Vector2Int(1, 1),
        new Vector2Int(-1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, -1)
    };

    private struct PendingFloodInput
    {
        public float amount;
        public FloodSourceType sourceType;
        public FloodCellChangeReason reason;

        public PendingFloodInput(float amount, FloodSourceType sourceType, FloodCellChangeReason reason)
        {
            this.amount = amount;
            this.sourceType = sourceType;
            this.reason = reason;
        }
    }

    private struct FloodTerrainInfo
    {
        public bool hasEnvironment;
        public bool isFloodSource;
        public bool isBlocked;
        public bool isBeachLike;
        public FloodSourceType sourceType;
        public string environmentName;
    }

    public IReadOnlyDictionary<TileCoord, FloodCellState> ActiveFloodCells => activeFloodCells;
    public int ActiveFloodCellCount => activeFloodCells.Count;

    private void Reset()
    {
        TryAutoAssignReferences();
    }

    private void Awake()
    {
        TryAutoAssignReferences();
    }

    private void OnEnable()
    {
        TryAutoAssignReferences();
    }

    private void Update()
    {
        if (Input.GetKeyDown(debugForceFloodKey))
        {
            TileCoord coord = new TileCoord(debugSelectedCell.x, debugSelectedCell.y);
            ForceFloodAtCell(coord, debugForceFloodAmount);
        }
    }

    private void TryAutoAssignReferences()
    {
        if (gridManager == null)
            gridManager = GridManager.Instance;

        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (mapGenerator == null)
            mapGenerator = FindFirstObjectByType<MapGenerator>();

        if (environmentDataSource == null)
            environmentDataSource = MonoEnvironmentDataSource.Instance;

        if (environmentDataSource == null)
            environmentDataSource = FindFirstObjectByType<MonoEnvironmentDataSource>();

        if (weatherGridManager == null)
            weatherGridManager = WeatherGridManager.Instance;

        if (weatherGridManager == null)
            weatherGridManager = FindFirstObjectByType<WeatherGridManager>();

        if (lavaOverlayManager == null)
            lavaOverlayManager = LavaOverlayManager.Instance;

        if (lavaOverlayManager == null)
            lavaOverlayManager = FindFirstObjectByType<LavaOverlayManager>();

        if (weatherFireSystem == null)
            weatherFireSystem = WeatherFireSystem.Instance;

        if (weatherFireSystem == null)
            weatherFireSystem = FindFirstObjectByType<WeatherFireSystem>();
    }

    public bool TryGetFloodCell(TileCoord coord, out FloodCellState state)
    {
        return activeFloodCells.TryGetValue(coord, out state);
    }

    public bool IsFlooded(TileCoord coord)
    {
        return activeFloodCells.ContainsKey(coord);
    }

    public float GetFloodDepth01(TileCoord coord)
    {
        return activeFloodCells.TryGetValue(coord, out FloodCellState state)
            ? state.floodDepth01
            : 0f;
    }

    public void ForceFloodAtCell(TileCoord coord, float amount)
    {
        AddFloodWater(coord, amount, FloodSourceType.Mixed, false, FloodCellChangeReason.DebugInput);
    }

    public void AddFloodWater(
        TileCoord coord,
        float amount,
        FloodSourceType sourceType,
        bool forceSourceFed = false,
        FloodCellChangeReason reason = FloodCellChangeReason.DepthChanged)
    {
        if (!enableFlooding)
            return;

        if (amount <= 0f)
            return;

        if (!IsValidGridCell(coord))
            return;

        if (!CanFloodOccupyCell(coord))
            return;

        bool sourceFed = forceSourceFed || IsValidFloodSourceCell(coord);

        AddOrUpdateFloodCell(coord, amount, sourceType, sourceFed, reason);
        FlushDirtyCells();
    }

    public void AddTsunamiFloodWater(TileCoord coord, float tsunamiEnergy01)
    {
        if (!enableFlooding || !enableTsunamiFlooding)
            return;

        tsunamiEnergy01 = Mathf.Clamp01(tsunamiEnergy01);

        if (tsunamiEnergy01 < tsunamiMinEnergyToFlood)
            return;

        if (!IsValidGridCell(coord))
            return;

        if (!CanFloodOccupyCell(coord))
            return;

        float input = tsunamiEnergy01 * tsunamiFloodInputMultiplier;
        input = Mathf.Min(input, tsunamiMaxFloodDepth);

        AddOrUpdateFloodCell(
            coord,
            input,
            FloodSourceType.Tsunami,
            false,
            FloodCellChangeReason.TsunamiInput
        );

        FlushDirtyCells();
    }

    public void AddRainfallAtCell(TileCoord coord, float rain01)
    {
        if (!enableFlooding || !enableRainFlooding)
            return;

        if (!IsValidGridCell(coord))
            return;

        if (!CanFloodOccupyCell(coord))
            return;

        rain01 = Mathf.Clamp01(rain01);
        if (rain01 <= 0f)
            return;

        float add = rain01 * rainfallAccumulationPerRain01;

        if (!rainfallAccumulator.TryGetValue(coord, out float existing))
            existing = 0f;

        rainfallAccumulator[coord] = existing + add;
    }

    public void AddRainfallAtCells(IEnumerable<TileCoord> coords, float rain01)
    {
        if (coords == null)
            return;

        foreach (TileCoord coord in coords)
            AddRainfallAtCell(coord, rain01);
    }

    public void ProcessRainAccumulation()
    {
        if (!enableFlooding || !enableRainFlooding)
            return;

        if (rainfallAccumulator.Count == 0)
            return;

        List<TileCoord> keys = new List<TileCoord>(rainfallAccumulator.Keys);

        for (int i = 0; i < keys.Count; i++)
        {
            TileCoord coord = keys[i];

            if (!rainfallAccumulator.TryGetValue(coord, out float accumulation))
                continue;

            bool canStartOrFeed =
                IsValidFloodSourceCell(coord) ||
                IsFlooded(coord) ||
                IsNearValidFloodSource(coord, rainFloodSourceRadius);

            if (canStartOrFeed && accumulation >= rainFloodThreshold)
            {
                float excess = accumulation - rainFloodThreshold;
                float input = Mathf.Min(excess, maxRainFloodInputPerTurn);

                FloodSourceType rainFloodSourceType = ResolveRainFloodSourceType(coord);

                AddOrUpdateFloodCell(
                    coord,
                    input,
                    rainFloodSourceType,
                    IsValidFloodSourceCell(coord),
                    FloodCellChangeReason.RainInput
                );

                accumulation -= input;
            }

            accumulation = Mathf.Max(0f, accumulation - floodRainDecayPerTurn);

            if (accumulation <= 0.001f)
                rainfallAccumulator.Remove(coord);
            else
                rainfallAccumulator[coord] = accumulation;
        }

        FlushDirtyCells();
        MarkFloodSaveDirty();
    }

    public void AdvanceFloodOneTurn(int turnIndex = -1)
    {
        if (!enableFlooding)
            return;

        if (isAdvancing)
            return;

        if (turnIndex >= 0)
            currentTurn = turnIndex;
        else
            currentTurn++;

        StartCoroutine(AdvanceFloodOneTurnRoutine());
    }

    private IEnumerator AdvanceFloodOneTurnRoutine()
    {
        isAdvancing = true;

        ProcessRainAccumulation();

        activeSnapshot.Clear();
        activeSnapshot.AddRange(activeFloodCells.Keys);

        pendingSpreadInput.Clear();

        int processed = 0;

        for (int i = 0; i < activeSnapshot.Count; i++)
        {
            TileCoord coord = activeSnapshot[i];

            if (!activeFloodCells.TryGetValue(coord, out FloodCellState state))
                continue;

            state.ageTurns++;

            ApplyDrainToCell(coord, state);

            if (state.waterAmount <= 0f)
            {
                RemoveFloodCell(coord, FloodCellChangeReason.Cleared);
            }
            else
            {
                TryQueueSpreadFromCell(coord, state);
                MarkDirty(coord, FloodCellChangeReason.Drained);

                if (debugLogging)
                    OnFloodDrained?.Invoke(coord, state);
            }

            processed++;

            if (processed >= cellsProcessedPerFrame)
            {
                processed = 0;
                FlushDirtyCells();
                yield return null;
            }
        }

        ApplyPendingSpreadInputBatched();

        FlushDirtyCells();

        if (debugLogging)
        {
            Debug.Log(
                $"[FloodSimulationSystem] Turn {currentTurn} finished. " +
                $"Active={activeFloodCells.Count}, AvgDepth={GetAverageFloodDepth():0.00}"
            );
        }

        isAdvancing = false;
    }

    private void ApplyDrainToCell(TileCoord coord, FloodCellState state)
    {
        float drain = baseDrainPerTurn + evaporationPerTurn;

        if (state.sourceFed || IsValidFloodSourceCell(coord))
            drain /= Mathf.Max(0.01f, waterSourceRetentionMultiplier);

        if (state.sourceType == FloodSourceType.Tsunami || state.sourceType == FloodSourceType.Mixed)
            drain *= tsunamiFloodDrainMultiplier;

        state.RemoveWater(drain, currentTurn);
    }

    private void TryQueueSpreadFromCell(TileCoord coord, FloodCellState state)
    {
        if (state.floodDepth01 < floodSpreadThreshold)
            return;

        Vector2Int baseCell = new Vector2Int(coord.x, coord.y);

        for (int i = 0; i < OrthogonalDirs.Length; i++)
        {
            Vector2Int n = baseCell + OrthogonalDirs[i];
            TryQueueSpreadToNeighbour(coord, state, new TileCoord(n.x, n.y), false);
        }

        if (!allowDiagonalSpread)
            return;

        for (int i = 0; i < DiagonalDirs.Length; i++)
        {
            Vector2Int n = baseCell + DiagonalDirs[i];
            TryQueueSpreadToNeighbour(coord, state, new TileCoord(n.x, n.y), true);
        }
    }

    private void TryQueueSpreadToNeighbour(
        TileCoord fromCoord,
        FloodCellState fromState,
        TileCoord neighbour,
        bool diagonal)
    {
        if (!IsValidGridCell(neighbour))
            return;

        if (!CanFloodOccupyCell(neighbour))
            return;

        if (!activeFloodCells.ContainsKey(neighbour) &&
            activeFloodCells.Count + pendingSpreadInput.Count >= maxActiveFloodCells)
        {
            return;
        }

        float amount = floodSpreadAmount * fromState.floodDepth01;
        amount *= floodSpreadLossMultiplier;

        if (diagonal)
            amount *= 0.7f;

        if (IsBeachLikeFloodCell(neighbour))
            amount *= 1f - beachAbsorptionMultiplier;
        else if (!IsValidFloodSourceCell(neighbour))
            amount *= 1f - landAbsorptionMultiplier;

        if (amount <= 0.001f)
            return;

        if (pendingSpreadInput.TryGetValue(neighbour, out PendingFloodInput existing))
        {
            existing.amount += amount;
            existing.sourceType = MergeSource(existing.sourceType, fromState.sourceType);
            pendingSpreadInput[neighbour] = existing;
        }
        else
        {
            pendingSpreadInput[neighbour] = new PendingFloodInput(
                amount,
                fromState.sourceType,
                FloodCellChangeReason.Expanded
            );
        }

        fromState.RemoveWater(amount * 0.35f, currentTurn);
        MarkDirty(fromCoord, FloodCellChangeReason.DepthChanged);
    }

    private void ApplyPendingSpreadInputBatched()
    {
        foreach (KeyValuePair<TileCoord, PendingFloodInput> pair in pendingSpreadInput)
        {
            TileCoord coord = pair.Key;
            PendingFloodInput input = pair.Value;

            AddOrUpdateFloodCell(
                coord,
                input.amount,
                input.sourceType,
                IsValidFloodSourceCell(coord),
                input.reason
            );
        }

        pendingSpreadInput.Clear();
    }

    private void AddOrUpdateFloodCell(
    TileCoord coord,
    float amount,
    FloodSourceType sourceType,
    bool sourceFed,
    FloodCellChangeReason reason)
    {
        if (amount <= 0f)
            return;

        if (IsLavaCell(coord))
            return;

        if (activeFloodCells.TryGetValue(coord, out FloodCellState state))
        {
            state.sourceFed |= sourceFed;
            state.AddWater(amount, sourceType, currentTurn);

            TryExtinguishFireAtFloodCell(coord, state);

            MarkDirty(coord, reason == FloodCellChangeReason.Started
                ? FloodCellChangeReason.DepthChanged
                : reason);

            MarkFloodSaveDirty();
            return;
        }

        if (activeFloodCells.Count >= maxActiveFloodCells)
            return;

        FloodCellState newState = new FloodCellState(coord, amount, sourceType, sourceFed, currentTurn);
        activeFloodCells.Add(coord, newState);

        TryExtinguishFireAtFloodCell(coord, newState);

        MarkDirty(coord, FloodCellChangeReason.Started);

        if (debugLogging)
        {
            Debug.Log(
                $"[FloodSimulationSystem] Flood started at {coord.x},{coord.y}. " +
                $"Source={sourceType}, Amount={amount:0.00}");
        }

        OnFloodStarted?.Invoke(coord, newState);

        if (reason == FloodCellChangeReason.Expanded)
            OnFloodExpanded?.Invoke(coord, newState);

        MarkFloodSaveDirty();
    }

    private void RemoveFloodCell(TileCoord coord, FloodCellChangeReason reason)
    {
        if (!activeFloodCells.TryGetValue(coord, out FloodCellState oldState))
            return;

        activeFloodCells.Remove(coord);

        MarkDirty(coord, reason);

        if (debugLogging)
            Debug.Log($"[FloodSimulationSystem] Flood cleared at {coord.x},{coord.y}");

        OnFloodCellChanged?.Invoke(new FloodCellChangedEvent(coord, oldState, reason));

        MarkFloodSaveDirty();

        if (activeFloodCells.Count == 0)
            OnFloodCleared?.Invoke();
    }

    private void MarkDirty(TileCoord coord, FloodCellChangeReason reason)
    {
        dirtyCells.Add(coord);

        activeFloodCells.TryGetValue(coord, out FloodCellState state);

        OnFloodCellChanged?.Invoke(new FloodCellChangedEvent(coord, state, reason));
    }

    private void FlushDirtyCells()
    {
        if (dirtyCells.Count == 0)
            return;

        dirtyCellBuffer.Clear();
        dirtyCellBuffer.AddRange(dirtyCells);
        dirtyCells.Clear();

        OnFloodCellsChanged?.Invoke(dirtyCellBuffer);
    }

    public void ClearAllFloods()
    {
        List<TileCoord> oldCells = new List<TileCoord>(activeFloodCells.Keys);

        activeFloodCells.Clear();
        rainfallAccumulator.Clear();

        for (int i = 0; i < oldCells.Count; i++)
            dirtyCells.Add(oldCells[i]);

        FlushDirtyCells();
        OnFloodCleared?.Invoke();

        MarkFloodSaveDirty();

        if (debugLogging)
            Debug.Log("[FloodSimulationSystem] Cleared all floods.");
    }

    public float GetAverageFloodDepth()
    {
        if (activeFloodCells.Count == 0)
            return 0f;

        float total = 0f;

        foreach (FloodCellState state in activeFloodCells.Values)
            total += state.floodDepth01;

        return total / activeFloodCells.Count;
    }

    private bool CanFloodOccupyCell(TileCoord coord)
    {
        if (!IsValidGridCell(coord))
            return false;

        if (IsLavaCell(coord))
            return false;

        if (!TryGetFloodTerrainInfo(coord, out FloodTerrainInfo info))
            return allowFloodOnCellsWithoutEnvironment;

        return !info.isBlocked;
    }

    private bool IsLavaCell(TileCoord coord)
    {
        if (!lavaBlocksFloodAdvance)
            return false;

        if (lavaOverlayManager == null)
            TryAutoAssignReferences();

        if (lavaOverlayManager == null)
            return false;

        return lavaOverlayManager.HasLavaAt(coord);
    }

    public bool IsValidFloodSourceCell(TileCoord coord)
    {
        return TryGetFloodTerrainInfo(coord, out FloodTerrainInfo info) &&
               info.isFloodSource;
    }

    public bool IsFloodBlockedCell(TileCoord coord)
    {
        return TryGetFloodTerrainInfo(coord, out FloodTerrainInfo info) &&
               info.isBlocked;
    }

    private bool IsBeachLikeFloodCell(TileCoord coord)
    {
        return TryGetFloodTerrainInfo(coord, out FloodTerrainInfo info) &&
               info.isBeachLike;
    }

    private bool IsNearValidFloodSource(TileCoord coord, int radius)
    {
        if (radius <= 0)
            return IsValidFloodSourceCell(coord);

        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                TileCoord check = new TileCoord(coord.x + dx, coord.y + dy);

                if (IsValidFloodSourceCell(check))
                    return true;
            }
        }

        return false;
    }

    private FloodSourceType GetNearbyWaterSourceType(TileCoord coord, int radius)
    {
        if (TryGetFloodTerrainInfo(coord, out FloodTerrainInfo directInfo) &&
            directInfo.isFloodSource)
        {
            return directInfo.sourceType;
        }

        FloodSourceType found = FloodSourceType.None;
        int safeRadius = Mathf.Max(0, radius);

        for (int dx = -safeRadius; dx <= safeRadius; dx++)
        {
            for (int dy = -safeRadius; dy <= safeRadius; dy++)
            {
                TileCoord check = new TileCoord(coord.x + dx, coord.y + dy);

                if (!TryGetFloodTerrainInfo(check, out FloodTerrainInfo info))
                    continue;

                if (!info.isFloodSource)
                    continue;

                FloodSourceType sourceType = info.sourceType;

                if (found == FloodSourceType.None)
                {
                    found = sourceType;
                    continue;
                }

                if (found != sourceType)
                    return FloodSourceType.Mixed;
            }
        }

        return found;
    }

    private FloodSourceType ResolveRainFloodSourceType(TileCoord coord)
    {
        FloodSourceType nearby = GetNearbyWaterSourceType(coord, rainFloodSourceRadius);

        if (nearby != FloodSourceType.None)
            return nearby;

        if (activeFloodCells.TryGetValue(coord, out FloodCellState existing) && existing != null)
            return existing.sourceType;

        return FloodSourceType.Rain;
    }

    private FloodSourceType MergeSource(FloodSourceType a, FloodSourceType b)
    {
        if (a == FloodSourceType.None)
            return b;

        if (b == FloodSourceType.None)
            return a;

        if (a == b)
            return a;

        return FloodSourceType.Mixed;
    }

    private bool TryGetFloodTerrainInfo(TileCoord coord, out FloodTerrainInfo info)
    {
        info = default;
        info.environmentName = "No environment";

        if (!IsValidGridCell(coord))
            return false;

        TryAutoAssignReferences();

        if (useMonoEnvironmentDataSource &&
            environmentDataSource != null &&
            environmentDataSource.HasLiveEnvironmentTile(coord))
        {
            TileEnvironmentData data = environmentDataSource.GetTileData(coord);

            info.hasEnvironment = true;
            info.environmentName = $"{data.tileType}|{data.environmentType}";
            ClassifyFloodTerrainFromNames(
                data.tileType.ToString(),
                data.environmentType.ToString(),
                ref info);

            return true;
        }

        if (useWeatherGridEnvironmentFallback &&
            weatherGridManager != null &&
            weatherGridManager.IsInitialized &&
            weatherGridManager.TryGetEnvironmentAtCell(coord.x, coord.y, out EnvironmentControl env) &&
            env != null)
        {
            string tileName = env.environmentTileType.ToString();
            string envName = env.environmentType.ToString();

            info.hasEnvironment = true;
            info.environmentName = $"{tileName}|{envName}|{env.name}";
            ClassifyFloodTerrainFromNames(tileName, envName, ref info);

            return true;
        }

        return false;
    }

    private void ClassifyFloodTerrainFromNames(
        string tileTypeName,
        string environmentTypeName,
        ref FloodTerrainInfo info)
    {
        tileTypeName = string.IsNullOrWhiteSpace(tileTypeName) ? "Unknown" : tileTypeName;
        environmentTypeName = string.IsNullOrWhiteSpace(environmentTypeName) ? "Unknown" : environmentTypeName;

        string combined = $"{tileTypeName}|{environmentTypeName}";

        if (ContainsAny(combined, "Mountain", "Volcano"))
        {
            info.isBlocked = true;
            info.isFloodSource = false;
            info.sourceType = FloodSourceType.None;
            return;
        }

        bool isSaltLake = ContainsAny(combined, "SaltLake");

        if (isSaltLake && !allowSaltLakeAsFloodSource)
        {
            info.isFloodSource = false;
            info.sourceType = FloodSourceType.None;
            return;
        }

        if (ContainsAny(
                combined,
                "River",
                "RiverCorner",
                "RiverSplit",
                "RiverMouth",
                "RiverCross",
                "RiverEnd"))
        {
            info.isFloodSource = true;
            info.sourceType = FloodSourceType.River;
            return;
        }

        if (ContainsAny(
                combined,
                "Lake",
                "LakeEdge",
                "LakeCorner",
                "LakeMouth",
                "Water"))
        {
            info.isFloodSource = true;
            info.sourceType = FloodSourceType.Lake;

            if (ContainsAny(combined, "LakeEdge", "LakeEdgeEnd"))
                info.isBeachLike = true;

            return;
        }

        if (ContainsAny(
                combined,
                "Ocean",
                "Coastline",
                "CoastlineCorner",
                "Beach",
                "BeachEnd"))
        {
            info.isFloodSource = true;
            info.sourceType = FloodSourceType.Ocean;

            if (ContainsAny(combined, "Coastline", "CoastlineCorner", "Beach", "BeachEnd"))
                info.isBeachLike = true;

            return;
        }

        info.isFloodSource = false;
        info.sourceType = FloodSourceType.None;
    }

    private bool ContainsAny(string value, params string[] needles)
    {
        if (string.IsNullOrWhiteSpace(value) || needles == null)
            return false;

        for (int i = 0; i < needles.Length; i++)
        {
            string needle = needles[i];

            if (string.IsNullOrWhiteSpace(needle))
                continue;

            if (value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    public FloodWaterMaterialKind GetFloodWaterMaterialKindForCell(TileCoord coord)
    {
        FloodSourceType sourceType = FloodSourceType.None;

        if (activeFloodCells.TryGetValue(coord, out FloodCellState state) && state != null)
        {
            sourceType = state.sourceType;
        }
        else
        {
            float bestDepth = -1f;

            for (int i = 0; i < 9; i++)
            {
                int ox = (i % 3) - 1;
                int oy = (i / 3) - 1;

                TileCoord nearby = new TileCoord(coord.x + ox, coord.y + oy);

                if (!activeFloodCells.TryGetValue(nearby, out FloodCellState nearbyState) ||
                    nearbyState == null)
                {
                    continue;
                }

                if (nearbyState.floodDepth01 > bestDepth)
                {
                    bestDepth = nearbyState.floodDepth01;
                    sourceType = nearbyState.sourceType;
                }
            }
        }

        switch (sourceType)
        {
            case FloodSourceType.River:
            case FloodSourceType.Lake:
            case FloodSourceType.Rain:
                return FloodWaterMaterialKind.FreshWater;

            case FloodSourceType.Ocean:
            case FloodSourceType.Tsunami:
                return FloodWaterMaterialKind.OceanWater;

            case FloodSourceType.Mixed:
                return FloodWaterMaterialKind.Mixed;

            default:
                return FloodWaterMaterialKind.FreshWater;
        }
    }

    private bool IsValidGridCell(TileCoord coord)
    {
        if (gridManager == null)
            TryAutoAssignReferences();

        if (gridManager == null)
            return coord.x >= 0 && coord.y >= 0;

        return coord.x >= 0 &&
               coord.y >= 0 &&
               coord.x < gridManager.columns &&
               coord.y < gridManager.rows;
    }

    public bool CanRainContributeToFloodingAtCell(TileCoord coord)
    {
        if (!enableFlooding || !enableRainFlooding)
            return false;

        if (!IsValidGridCell(coord))
            return false;

        if (!CanFloodOccupyCell(coord))
            return false;

        return IsValidFloodSourceCell(coord) ||
               IsFlooded(coord) ||
               IsNearValidFloodSource(coord, rainFloodSourceRadius);
    }

    // Flood no longer keeps source/block caches.
    // These are kept so RainFloodBridge debug logs still compile.
    public int DebugValidFloodSourceCellCount => -1;
    public int DebugBlockedFloodCellCount => -1;

    public string GetDebugEnvironmentName(TileCoord coord)
    {
        if (TryGetFloodTerrainInfo(coord, out FloodTerrainInfo info))
            return info.environmentName;

        return "No live environment";
    }

    public string GetRainFloodContributionDebugReason(TileCoord coord)
    {
        if (!enableFlooding)
            return "Flooding disabled.";

        if (!enableRainFlooding)
            return "Rain flooding disabled.";

        if (!IsValidGridCell(coord))
            return $"Outside grid. Coord=({coord.x},{coord.y})";

        bool hasInfo = TryGetFloodTerrainInfo(coord, out FloodTerrainInfo info);
        string environmentName = hasInfo ? info.environmentName : "No live environment";

        if (!hasInfo)
        {
            return
                $"Cannot contribute: no live environment found at cell. " +
                $"Coord=({coord.x},{coord.y}), Environment={environmentName}. " +
                $"Check MonoEnvironmentDataSource registration or WeatherGridManager environment coverage.";
        }

        if (info.isBlocked)
        {
            return
                $"Blocked flood cell. Coord=({coord.x},{coord.y}), " +
                $"Environment={environmentName}";
        }

        bool isSource = info.isFloodSource;
        bool isFlooded = IsFlooded(coord);
        bool nearSource = IsNearValidFloodSource(coord, rainFloodSourceRadius);

        if (isSource)
        {
            return
                $"Can contribute: direct flood source. Coord=({coord.x},{coord.y}), " +
                $"Environment={environmentName}, SourceType={info.sourceType}";
        }

        if (isFlooded)
        {
            return
                $"Can contribute: already flooded. Coord=({coord.x},{coord.y}), " +
                $"Environment={environmentName}";
        }

        if (nearSource)
        {
            return
                $"Can contribute: near valid flood source within radius {rainFloodSourceRadius}. " +
                $"Coord=({coord.x},{coord.y}), Environment={environmentName}";
        }

        if (TryFindNearestFloodSourceDebug(
                coord,
                Mathf.Max(8, rainFloodSourceRadius * 4),
                out TileCoord nearest,
                out FloodSourceType nearestSourceType,
                out int nearestDistance,
                out string nearestEnvironment))
        {
            return
                $"Cannot contribute: not a source, not already flooded, and not within rainFloodSourceRadius. " +
                $"Coord=({coord.x},{coord.y}), Environment={environmentName}, " +
                $"rainFloodSourceRadius={rainFloodSourceRadius}, " +
                $"NearestSource=({nearest.x},{nearest.y}), " +
                $"NearestSourceType={nearestSourceType}, " +
                $"NearestEnvironment={nearestEnvironment}, " +
                $"NearestManhattanDistance={nearestDistance}";
        }

        return
            $"Cannot contribute: not a source, not already flooded, and no source found nearby. " +
            $"Coord=({coord.x},{coord.y}), Environment={environmentName}, " +
            $"rainFloodSourceRadius={rainFloodSourceRadius}";
    }

    private bool TryFindNearestFloodSourceDebug(
        TileCoord from,
        int maxSearchRadius,
        out TileCoord nearest,
        out FloodSourceType sourceType,
        out int manhattanDistance,
        out string environmentName)
    {
        nearest = default;
        sourceType = FloodSourceType.None;
        manhattanDistance = int.MaxValue;
        environmentName = "None";

        if (gridManager == null)
            TryAutoAssignReferences();

        if (gridManager == null)
            return false;

        int maxRadius = Mathf.Max(1, maxSearchRadius);

        int minX = Mathf.Max(0, from.x - maxRadius);
        int maxX = Mathf.Min(gridManager.columns - 1, from.x + maxRadius);
        int minY = Mathf.Max(0, from.y - maxRadius);
        int maxY = Mathf.Min(gridManager.rows - 1, from.y + maxRadius);

        bool found = false;

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                TileCoord check = new TileCoord(x, y);

                if (!TryGetFloodTerrainInfo(check, out FloodTerrainInfo info))
                    continue;

                if (!info.isFloodSource)
                    continue;

                int distance =
                    Mathf.Abs(check.x - from.x) +
                    Mathf.Abs(check.y - from.y);

                if (distance >= manhattanDistance)
                    continue;

                nearest = check;
                sourceType = info.sourceType;
                manhattanDistance = distance;
                environmentName = info.environmentName;
                found = true;
            }
        }

        return found;
    }

    private Vector3 GetWorldPosition(TileCoord coord)
    {
        if (gridManager == null)
            return new Vector3(coord.x, 0f, coord.y);

        Vector3 corner = gridManager.GetWorldPosition(coord.x, coord.y);

        return new Vector3(
            corner.x + gridManager.cellSize * 0.5f,
            corner.y,
            corner.z + gridManager.cellSize * 0.5f);
    }

    [ContextMenu("Debug/Force Flood At Selected Cell")]
    private void ContextForceFloodAtSelectedCell()
    {
        ForceFloodAtCell(new TileCoord(debugSelectedCell.x, debugSelectedCell.y), debugForceFloodAmount);
    }

    [ContextMenu("Debug/Flood From All Water Sources")]
    private void ContextFloodFromAllWaterSources()
    {
        TryAutoAssignReferences();

        if (gridManager == null)
            return;

        int added = 0;

        for (int x = 0; x < gridManager.columns; x++)
        {
            for (int y = 0; y < gridManager.rows; y++)
            {
                TileCoord coord = new TileCoord(x, y);

                if (!TryGetFloodTerrainInfo(coord, out FloodTerrainInfo info))
                    continue;

                if (!info.isFloodSource)
                    continue;

                AddOrUpdateFloodCell(
                    coord,
                    debugForceFloodAmount,
                    info.sourceType,
                    true,
                    FloodCellChangeReason.DebugInput
                );

                added++;
            }
        }

        FlushDirtyCells();

        if (debugLogging)
            Debug.Log($"[FloodSimulationSystem] Debug flooded all water sources. Added={added}");
    }

    private void TryExtinguishFireAtFloodCell(TileCoord coord, FloodCellState state)
    {
        if (!floodExtinguishesFire)
            return;

        if (state == null)
            return;

        if (state.floodDepth01 < minFloodDepthToExtinguishFire)
            return;

        if (weatherFireSystem == null)
            TryAutoAssignReferences();

        if (weatherFireSystem == null)
            return;

        weatherFireSystem.TryExtinguishFireAtCellFromFlood(
            coord,
            state.floodDepth01);
    }

    [ContextMenu("Debug/Clear All Floods")]
    private void ContextClearAllFloods()
    {
        ClearAllFloods();
    }

    [ContextMenu("Debug/Print Selected Flood Terrain Info")]
    private void ContextPrintSelectedFloodTerrainInfo()
    {
        TileCoord coord = new TileCoord(debugSelectedCell.x, debugSelectedCell.y);

        if (TryGetFloodTerrainInfo(coord, out FloodTerrainInfo info))
        {
            Debug.Log(
                $"[FloodSimulationSystem] Selected terrain info. " +
                $"Coord=({coord.x},{coord.y}), Env={info.environmentName}, " +
                $"Source={info.isFloodSource}, SourceType={info.sourceType}, " +
                $"Blocked={info.isBlocked}, BeachLike={info.isBeachLike}");
        }
        else
        {
            Debug.Log(
                $"[FloodSimulationSystem] Selected terrain info. " +
                $"Coord=({coord.x},{coord.y}), No environment found.");
        }
    }

    // Kept as a safe no-op so older inspector buttons or scripts do not break.
    [ContextMenu("Debug/Rebuild Environment Caches - No Op")]
    public void RebuildEnvironmentCaches()
    {
        if (debugLogging)
        {
            Debug.Log(
                "[FloodSimulationSystem] RebuildEnvironmentCaches called, but flood now uses on-demand terrain lookup. No cache rebuild needed.");
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        if (gridManager == null)
            gridManager = GridManager.Instance;

        if (gridManager == null)
            return;

        TileCoord selected = new TileCoord(debugSelectedCell.x, debugSelectedCell.y);

        if (TryGetFloodTerrainInfo(selected, out FloodTerrainInfo selectedInfo))
        {
            if (selectedInfo.isBlocked)
                Gizmos.color = Color.red;
            else if (selectedInfo.isFloodSource)
                Gizmos.color = Color.cyan;
            else
                Gizmos.color = Color.yellow;

            Gizmos.DrawWireCube(
                GetWorldPosition(selected) + Vector3.up * 0.15f,
                Vector3.one * 0.45f);
        }

        foreach (KeyValuePair<TileCoord, FloodCellState> pair in activeFloodCells)
        {
            float depth = pair.Value.floodDepth01;
            Gizmos.color = Color.Lerp(Color.clear, Color.blue, depth);
            Vector3 size = new Vector3(0.8f, 0.05f + depth * 0.4f, 0.8f);

            Gizmos.DrawCube(
                GetWorldPosition(pair.Key) + Vector3.up * (0.15f + depth * 0.2f),
                size);
        }
    }

    public FloodSimulationSaveData SaveState()
    {
        FloodSimulationSaveData data = new FloodSimulationSaveData
        {
            currentTurn = currentTurn
        };

        foreach (KeyValuePair<TileCoord, FloodCellState> pair in activeFloodCells)
        {
            TileCoord coord = pair.Key;
            FloodCellState state = pair.Value;

            if (state == null)
                continue;

            data.floodCells.Add(new FloodCellSaveData
            {
                x = coord.x,
                y = coord.y,

                waterAmount = Mathf.Max(0f, state.waterAmount),
                floodDepth01 = Mathf.Clamp01(state.floodDepth01),

                sourceTypeValue = (int)state.sourceType,
                sourceFed = state.sourceFed,

                ageTurns = Mathf.Max(0, state.ageTurns),
                lastUpdatedTurn = state.lastUpdatedTurn
            });
        }

        foreach (KeyValuePair<TileCoord, float> pair in rainfallAccumulator)
        {
            if (pair.Value <= 0.001f)
                continue;

            data.rainfallAccumulators.Add(new RainfallAccumulatorSaveData
            {
                x = pair.Key.x,
                y = pair.Key.y,
                amount = Mathf.Max(0f, pair.Value)
            });
        }

        return data;
    }

    public void LoadState(FloodSimulationSaveData data)
    {
        TryAutoAssignReferences();

        StopFloodLoadSensitiveState();

        activeFloodCells.Clear();
        rainfallAccumulator.Clear();
        dirtyCells.Clear();
        dirtyCellBuffer.Clear();
        activeSnapshot.Clear();
        pendingSpreadInput.Clear();

        if (data == null)
        {
            FlushDirtyCells();
            OnFloodCleared?.Invoke();
            return;
        }

        currentTurn = Mathf.Max(0, data.currentTurn);

        int restoredFloodCells = 0;

        if (data.floodCells != null)
        {
            for (int i = 0; i < data.floodCells.Count; i++)
            {
                FloodCellSaveData saved = data.floodCells[i];

                if (saved == null)
                    continue;

                TileCoord coord = new TileCoord(saved.x, saved.y);

                if (!IsValidGridCell(coord))
                    continue;

                // Keep your existing lava/flood rule: lava wins if both somehow exist in save.
                if (IsLavaCell(coord))
                    continue;

                float waterAmount = Mathf.Max(0f, saved.waterAmount);

                if (waterAmount <= 0.001f && saved.floodDepth01 > 0.001f)
                    waterAmount = saved.floodDepth01;

                if (waterAmount <= 0.001f)
                    continue;

                FloodSourceType sourceType = RestoreFloodSourceType(saved.sourceTypeValue);

                FloodCellState state = new FloodCellState(
                    coord,
                    waterAmount,
                    sourceType,
                    saved.sourceFed,
                    saved.lastUpdatedTurn);

                state.ageTurns = Mathf.Max(0, saved.ageTurns);

                activeFloodCells[coord] = state;
                dirtyCells.Add(coord);

                TryExtinguishFireAtFloodCell(coord, state);

                restoredFloodCells++;
            }
        }

        if (data.rainfallAccumulators != null)
        {
            for (int i = 0; i < data.rainfallAccumulators.Count; i++)
            {
                RainfallAccumulatorSaveData saved = data.rainfallAccumulators[i];

                if (saved == null)
                    continue;

                TileCoord coord = new TileCoord(saved.x, saved.y);

                if (!IsValidGridCell(coord))
                    continue;

                if (saved.amount <= 0.001f)
                    continue;

                rainfallAccumulator[coord] = Mathf.Max(0f, saved.amount);
            }
        }

        FlushDirtyCells();

        if (activeFloodCells.Count == 0)
            OnFloodCleared?.Invoke();
        else
            OnFloodCellsChanged?.Invoke(new List<TileCoord>(activeFloodCells.Keys));

        FloodOverlayManager overlay = FindFirstObjectByType<FloodOverlayManager>();
        if (overlay != null)
            overlay.RebuildAllVisuals();

        SaveSystem.MarkSectionDirty(SaveSectionKeys.WorldSim);

        if (debugLogging)
        {
            Debug.Log(
                $"[FloodSimulationSystem] Loaded flood state. " +
                $"FloodCells={restoredFloodCells}, RainAccumulators={rainfallAccumulator.Count}");
        }
    }

    private void StopFloodLoadSensitiveState()
    {
        isAdvancing = false;
    }

    private FloodSourceType RestoreFloodSourceType(int value)
    {
        if (value < (int)FloodSourceType.None ||
            value > (int)FloodSourceType.Mixed)
        {
            return FloodSourceType.None;
        }

        return (FloodSourceType)value;
    }

    private void MarkFloodSaveDirty()
    {
        SaveSystem.MarkSectionDirty(SaveSectionKeys.WorldSim);
    }

    public void ApplyPresetSettings(FloodPresetSettings settings)
    {
        if (settings == null || !settings.overrideFloods)
            return;

        enableFlooding = settings.enableFlooding;
        enableRainFlooding = settings.enableRainFlooding;
        enableTsunamiFlooding = settings.enableTsunamiFlooding;

        maxActiveFloodCells = settings.maxActiveFloodCells;
        floodSpreadThreshold = settings.floodSpreadThreshold;
        floodSpreadAmount = settings.floodSpreadAmount;
        floodSpreadLossMultiplier = settings.floodSpreadLossMultiplier;
        landAbsorptionMultiplier = settings.landAbsorptionMultiplier;
        beachAbsorptionMultiplier = settings.beachAbsorptionMultiplier;

        baseDrainPerTurn = settings.baseDrainPerTurn;
        evaporationPerTurn = settings.evaporationPerTurn;

        rainfallAccumulationPerRain01 = settings.rainfallAccumulationPerRain01;
        rainFloodThreshold = settings.rainFloodThreshold;
        maxRainFloodInputPerTurn = settings.maxRainFloodInputPerTurn;

        tsunamiFloodInputMultiplier = settings.tsunamiFloodInputMultiplier;
        tsunamiMaxFloodDepth = settings.tsunamiMaxFloodDepth;

        if (debugLogging)
            Debug.Log("[FloodSimulationSystem] Applied flood preset settings.");
    }
}