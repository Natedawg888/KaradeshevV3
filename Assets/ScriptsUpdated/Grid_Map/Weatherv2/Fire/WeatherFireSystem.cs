using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeatherFireSystem : MonoBehaviour
{

    private struct FireCellTargets
    {
        public EnvironmentControl environment;
        public PlayerBuildingManager.Record buildingRecord;

        public bool HasEnvironment => environment != null;
        public bool HasBuilding => buildingRecord != null && buildingRecord.instance != null;
        public bool HasAny => HasEnvironment || HasBuilding;
    }

    public static WeatherFireSystem Instance { get; private set; }

    [Header("References")]
    [SerializeField] private WeatherGridManager weatherGridManager;
    [SerializeField] private LightningSimulationSystem lightningSimulationSystem;
    [SerializeField] private RainSimulationSystem rainSimulationSystem;
    [SerializeField] private StormSimulationSystem stormSimulationSystem;
    [SerializeField] private CloudSimulationSystem cloudSimulationSystem;
    [SerializeField] private FloodSimulationSystem floodSimulationSystem;

    [Header("Lifecycle")]
    [SerializeField] private bool processOnStartOfTurn = true;

    [Header("Lightning -> Fire")]
    [SerializeField] private bool lightningCanStartFires = true;
    [Range(0f, 1f)][SerializeField] private float lightningFireStartChance = 0.25f;
    [SerializeField] private bool lightningCanIgniteAlreadyDiscoveredOnly = false;

    [Header("Rain / Storm Dampening")]
    [Tooltip("At full rain, ignition chance is multiplied by this value.")]
    [Range(0f, 1f)][SerializeField] private float minIgnitionMultiplierAtFullRain = 0.20f;

    [Tooltip("Storm intensity counts as partial dampness even before full rain.")]
    [Range(0f, 1f)][SerializeField] private float stormDampeningStrength = 0.35f;

    [Header("Environment Fire")]
    [SerializeField] private bool autoAddMissingEnvironmentFireState = true;
    [Min(1)][SerializeField] private int environmentBurnTurns = 3;
    [Range(0f, 1f)][SerializeField] private float environmentDrynessIgnitionBonus = 0.35f;
    [Range(0f, 1f)][SerializeField] private float environmentHeatIgnitionBonus = 0.20f;
    [Range(0f, 1f)][SerializeField] private float environmentRainExtinguishChanceAtFullRain = 0.25f;

    [Header("Building Fire")]
    [SerializeField] private bool fireCanIgniteBuildings = true;
    [SerializeField] private bool autoAddMissingBuildingFireState = true;
    [SerializeField] private bool autoAddBuildingSecondaryEffects = true;
    [Min(1)][SerializeField] private int buildingBurnTurns = 3;
    [Min(0)][SerializeField] private int buildingDamagePerStep = 8;
    [Range(0f, 1f)][SerializeField] private float buildingDrynessIgnitionBonus = 0.35f;
    [Range(0f, 1f)][SerializeField] private float buildingHeatIgnitionBonus = 0.25f;
    [Range(0f, 1f)][SerializeField] private float buildingRainExtinguishChanceAtFullRain = 0.35f;

    [Header("Fire Spread")]
    [SerializeField] private bool fireCanSpread = true;
    [SerializeField] private bool fireSpreadIncludesDiagonals = true;
    [Range(0f, 1f)][SerializeField] private float fireSpreadChanceOrthogonal = 0.20f;
    [Range(0f, 1f)][SerializeField] private float fireSpreadChanceDiagonal = 0.10f;
    [Range(0f, 1f)][SerializeField] private float fireSpreadRainPenaltyStrength = 0.75f;
    [Range(0f, 1f)][SerializeField] private float fireSpreadSourceDrynessBonus = 0.40f;
    [Range(0f, 1f)][SerializeField] private float fireSpreadSourceHeatBonus = 0.30f;

    [Header("Fire Spread Wind Bias")]
    [Range(0f, 2f)][SerializeField] private float fireSpreadWindBiasStrength = 0.75f;
    [Range(0.01f, 1f)][SerializeField] private float fireSpreadWindMinMultiplier = 0.20f;

    [Header("Flood Fire Blocking")]
    [SerializeField] private bool floodBlocksEnvironmentIgnition = true;
    [SerializeField] private bool floodBlocksBuildingIgnition = true;

    [Tooltip("Flood depth needed before it blocks fire ignition.")]
    [Range(0f, 1f)]
    [SerializeField] private float minFloodDepthToBlockIgnition = 0.01f;

    [Header("Flood Extinguishing")]
    [SerializeField] private bool floodExtinguishesEnvironmentFire = true;
    [SerializeField] private bool floodExtinguishesBuildingFire = true;

    [Tooltip("Flood depth needed before it extinguishes fire.")]
    [Range(0f, 1f)]
    [SerializeField] private float minFloodDepthToExtinguishFire = 0.01f;

    [Header("Performance")]
    [Min(0)][SerializeField] private int maxFireSpreadIgnitionsPerTurn = 24;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    private LightningSimulationSystem _subscribedLightning;
    private Coroutine _lateBindRoutine;

    private readonly List<BurningEnvironmentEntry> _burningEnvironments = new();
    private readonly HashSet<int> _burningEnvironmentKeys = new();

    private readonly List<BurningBuildingEntry> _burningBuildings = new();
    private readonly HashSet<int> _burningBuildingKeys = new();

    private readonly List<PendingFireIgnition> _pendingFireIgnitions = new();

    private readonly List<TileCoord> _floodCheckScratch = new();

    public event Action<TileCoord> OnFireCellIgnited;
    public event Action OnFireCellsChanged;

    private static readonly Vector2Int[] FireSpreadOrthogonalOffsets =
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0)
    };

    private static readonly Vector2Int[] FireSpreadDiagonalOffsets =
    {
        new Vector2Int(1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, -1),
        new Vector2Int(-1, 1)
    };

    private struct BurningEnvironmentEntry
    {
        public EnvironmentControl environment;
        public EnvironmentFireState state;
        public Vector2Int cell;
    }

    private struct BurningBuildingEntry
    {
        public GameObject buildingRoot;
        public BuildingFireState state;
        public Vector2Int cell;
    }

    private struct PendingFireIgnition
    {
        public int x;
        public int y;
        public float chance01;
        public float sourceDryness01;
        public float sourceHeat01;
    }

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
        RebindLightningEvents();

        TurnSystem.SubscribeToStartOfTurn(HandleStartOfTurn);

        if (_lateBindRoutine == null)
            _lateBindRoutine = StartCoroutine(LateBindRoutine());
    }

    private void OnDisable()
    {
        TurnSystem.UnsubscribeFromStartOfTurn(HandleStartOfTurn);
        UnbindLightningEvents();

        if (_lateBindRoutine != null)
        {
            StopCoroutine(_lateBindRoutine);
            _lateBindRoutine = null;
        }
    }

    private void OnDestroy()
    {
        UnbindLightningEvents();

        if (Instance == this)
            Instance = null;
    }

    private IEnumerator LateBindRoutine()
    {
        while (isActiveAndEnabled)
        {
            EnsureLinks();
            RebindLightningEvents();

            if (_subscribedLightning != null)
            {
                _lateBindRoutine = null;
                yield break;
            }

            yield return null;
        }
    }

    public void InstallRuntimeRefs(
    WeatherGridManager newWeatherGridManager,
    LightningSimulationSystem newLightningSimulationSystem,
    RainSimulationSystem newRainSimulationSystem,
    StormSimulationSystem newStormSimulationSystem,
    CloudSimulationSystem newCloudSimulationSystem,
    FloodSimulationSystem newFloodSimulationSystem = null)
    {
        if (newWeatherGridManager != null)
            weatherGridManager = newWeatherGridManager;

        if (newLightningSimulationSystem != null)
            lightningSimulationSystem = newLightningSimulationSystem;

        if (newRainSimulationSystem != null)
            rainSimulationSystem = newRainSimulationSystem;

        if (newStormSimulationSystem != null)
            stormSimulationSystem = newStormSimulationSystem;

        if (newCloudSimulationSystem != null)
            cloudSimulationSystem = newCloudSimulationSystem;

        if (newFloodSimulationSystem != null)
            floodSimulationSystem = newFloodSimulationSystem;

        RebindLightningEvents();
    }

    private void EnsureLinks()
    {
        if (weatherGridManager == null)
            weatherGridManager = WeatherGridManager.Instance;

        if (lightningSimulationSystem == null)
            lightningSimulationSystem = LightningSimulationSystem.Instance;

        if (rainSimulationSystem == null)
            rainSimulationSystem = RainSimulationSystem.Instance;

        if (stormSimulationSystem == null)
            stormSimulationSystem = StormSimulationSystem.Instance;

        if (cloudSimulationSystem == null)
            cloudSimulationSystem = CloudSimulationSystem.Instance;

        if (floodSimulationSystem == null)
            floodSimulationSystem = FindFirstObjectByType<FloodSimulationSystem>();
    }

    private void RebindLightningEvents()
    {
        if (_subscribedLightning == lightningSimulationSystem)
            return;

        UnbindLightningEvents();

        _subscribedLightning = lightningSimulationSystem;

        if (_subscribedLightning != null)
            _subscribedLightning.OnLightningStrikeResolved += HandleLightningStrikeResolved;
    }

    private void UnbindLightningEvents()
    {
        if (_subscribedLightning != null)
            _subscribedLightning.OnLightningStrikeResolved -= HandleLightningStrikeResolved;

        _subscribedLightning = null;
    }

    private void HandleStartOfTurn()
    {
        if (!processOnStartOfTurn)
            return;

        ProcessFireStep();
    }

    private void HandleLightningStrikeResolved(LightningStrikePayload payload)
    {
        if (!lightningCanStartFires)
            return;

        TryIgniteFromLightningStrike(payload);
    }

    public bool TryIgniteFromLightningStrike(LightningStrikePayload payload)
    {
        int x = payload.resolvedCellX;
        int y = payload.resolvedCellY;

        if (!IsInBounds(x, y))
            return false;

        if (!TryResolveFireTargetsAtCell(x, y, out FireCellTargets targets))
        {
            if (debugLogging)
            {
                Debug.Log(
                    $"[WeatherFireSystem] Lightning hit ({x},{y}) but no environment/building footprint owner was cached there.");
            }

            return false;
        }

        float sourceDryness01 = 0.5f;
        float sourceHeat01 = Mathf.Clamp01(
            0.75f +
            payload.sourceStormIntensity01 * 0.15f +
            payload.sourceCharge * 0.10f);

        if (targets.environment != null)
        {
            EnvironmentFireState envFire = GetEnvironmentFireState(
                targets.environment,
                createIfMissing: false);

            if (envFire != null)
                sourceDryness01 = envFire.CurrentDryness01;
        }

        bool ignited = TryIgniteFireAtCell(
            x,
            y,
            lightningFireStartChance,
            sourceDryness01,
            sourceHeat01,
            ignitionEvent: true);

        if (debugLogging)
        {
            string envName = targets.environment != null ? targets.environment.name : "none";
            string buildingName = targets.HasBuilding ? targets.buildingRecord.instance.name : "none";

            Debug.Log(
                $"[WeatherFireSystem] Lightning fire attempt at ({x},{y}) " +
                $"Env={envName} Building={buildingName} Ignited={ignited}");
        }

        return ignited;
    }

    public bool TryIgniteFireAtCell(
        int x,
        int y,
        float chance01,
        float sourceDryness01,
        float sourceHeat01,
        bool ignitionEvent)
    {
        if (!IsInBounds(x, y))
            return false;

        chance01 = Mathf.Clamp01(chance01);
        sourceDryness01 = Mathf.Clamp01(sourceDryness01);
        sourceHeat01 = Mathf.Clamp01(sourceHeat01);

        if (chance01 <= 0f)
            return false;

        float rain01 = GetRainIntensity01AtCell(x, y);
        float rainPenalty = Mathf.Lerp(1f, minIgnitionMultiplierAtFullRain, rain01);
        float adjustedChance = Mathf.Clamp01(chance01 * rainPenalty);

        if (adjustedChance <= 0f)
            return false;

        bool envIgnited = TryIgniteEnvironmentAtCell(
            x,
            y,
            adjustedChance,
            sourceDryness01,
            sourceHeat01,
            rain01,
            ignitionEvent);

        bool buildingIgnited = TryIgniteBuildingAtCell(
            x,
            y,
            adjustedChance,
            sourceDryness01,
            sourceHeat01);

        bool anyIgnited = envIgnited || buildingIgnited;

        if (anyIgnited)
        {
            OnFireCellIgnited?.Invoke(new TileCoord(x, y));
            OnFireCellsChanged?.Invoke();
            MarkFireSaveDirty();
        }

        return anyIgnited;
    }

    public bool CopyActiveFireCells(List<TileCoord> results)
    {
        if (results == null)
            return false;

        results.Clear();

        for (int i = 0; i < _burningEnvironments.Count; i++)
        {
            BurningEnvironmentEntry entry = _burningEnvironments[i];

            if (entry.environment == null || entry.state == null || !entry.state.IsOnFire)
                continue;

            AddUniqueFireCell(results, entry.cell.x, entry.cell.y);
        }

        for (int i = 0; i < _burningBuildings.Count; i++)
        {
            BurningBuildingEntry entry = _burningBuildings[i];

            if (entry.buildingRoot == null || entry.state == null || !entry.state.IsOnFire)
                continue;

            AddUniqueFireCell(results, entry.cell.x, entry.cell.y);
        }

        return results.Count > 0;
    }

    private void AddUniqueFireCell(List<TileCoord> results, int x, int y)
    {
        TileCoord coord = new TileCoord(x, y);

        for (int i = 0; i < results.Count; i++)
        {
            if (results[i].x == coord.x && results[i].y == coord.y)
                return;
        }

        results.Add(coord);
    }

    private bool TryIgniteEnvironmentAtCell(
    int x,
    int y,
    float incomingChance01,
    float sourceDryness01,
    float sourceHeat01,
    float rain01,
    bool ignitionEvent)
    {
        if (!TryGetEnvironmentAtIgnitionCell(x, y, out EnvironmentControl env) || env == null)
            return false;

        if (floodBlocksEnvironmentIgnition && IsEnvironmentFootprintFlooded(env))
        {
            return false;
        }

        if (ignitionEvent && lightningCanIgniteAlreadyDiscoveredOnly && !env.IsDiscovered)
            return false;

        if (!env.canCatchFire)
            return false;

        EnvironmentFireState fireState = GetEnvironmentFireState(env, autoAddMissingEnvironmentFireState);
        if (fireState == null || !fireState.CanCatchFire || fireState.IsOnFire)
            return false;

        fireState.RefreshDrynessFromWeather(rain01);

        float drynessFactor = Mathf.Lerp(
            0.85f,
            1f + environmentDrynessIgnitionBonus,
            fireState.CurrentDryness01);

        float heatFactor = Mathf.Lerp(
            0.90f,
            1f + environmentHeatIgnitionBonus,
            Mathf.Clamp01(sourceHeat01));

        float finalChance = Mathf.Clamp01(incomingChance01 * drynessFactor * heatFactor);

        if (!fireState.TryIgnite(finalChance, environmentBurnTurns))
            return false;

        // Store the struck cell, not necessarily the env primary cell.
        // This means spread/rain checks happen from the lightning-hit footprint cell.
        RegisterBurningEnvironment(env, fireState, new Vector2Int(x, y));

        if (debugLogging)
        {
            Debug.Log(
                $"[WeatherFireSystem] Environment '{env.name}' ignited from footprint cell ({x},{y}).");
        }

        return true;
    }

    private bool TryIgniteBuildingAtCell(
    int x,
    int y,
    float incomingChance01,
    float sourceDryness01,
    float sourceHeat01)
    {
        if (!fireCanIgniteBuildings)
            return false;

        if (!TryGetBuildingAtIgnitionCell(x, y, out PlayerBuildingManager.Record record) || record == null)
            return false;

        GameObject buildingRoot = record.instance;
        if (buildingRoot == null)
            return false;

        if (floodBlocksBuildingIgnition && IsBuildingFootprintFlooded(record))
        {
            return false;
        }

        BuildingFireState fireState = GetBuildingFireState(buildingRoot, autoAddMissingBuildingFireState);
        if (fireState == null || !fireState.CanCatchFire || fireState.IsOnFire)
            return false;

        float igniteChance = Mathf.Clamp01(incomingChance01);

        igniteChance *= Mathf.Lerp(
            0.75f,
            1f + buildingDrynessIgnitionBonus,
            Mathf.Clamp01(sourceDryness01));

        igniteChance *= Mathf.Lerp(
            0.80f,
            1f + buildingHeatIgnitionBonus,
            Mathf.Clamp01(sourceHeat01));

        igniteChance = Mathf.Clamp01(igniteChance);

        int burnTurns = buildingBurnTurns;

        BuildingFireResistance resistance = buildingRoot.GetComponent<BuildingFireResistance>();
        if (resistance == null)
            resistance = buildingRoot.GetComponentInChildren<BuildingFireResistance>(true);

        if (resistance != null)
        {
            igniteChance = resistance.ModifyFireIgnitionChance(igniteChance);
            burnTurns = resistance.ModifyFireBurnTurns(burnTurns);
        }

        if (igniteChance <= 0f || burnTurns <= 0)
            return false;

        if (!fireState.TryIgnite(igniteChance, burnTurns))
            return false;

        if (autoAddBuildingSecondaryEffects)
            EnsureBuildingSecondaryEffects(buildingRoot);

        // Store the struck footprint cell.
        RegisterBurningBuilding(buildingRoot, fireState, new Vector2Int(x, y));

        if (debugLogging)
        {
            Debug.Log(
                $"[WeatherFireSystem] Building '{buildingRoot.name}' ignited from footprint cell ({x},{y}).");
        }

        return true;
    }

    public void ProcessFireStep()
    {
        int beforeEnvironmentCount = _burningEnvironments.Count;
        int beforeBuildingCount = _burningBuildings.Count;

        int remainingSpreadBudget = Mathf.Max(0, maxFireSpreadIgnitionsPerTurn);

        _pendingFireIgnitions.Clear();

        ProcessBurningBuildingsStep(ref remainingSpreadBudget);
        ProcessBurningEnvironmentsStep(ref remainingSpreadBudget);

        ApplyPendingFireIgnitions();

        if (beforeEnvironmentCount != _burningEnvironments.Count ||
            beforeBuildingCount != _burningBuildings.Count ||
            _burningEnvironments.Count > 0 ||
            _burningBuildings.Count > 0)
        {
            MarkFireSaveDirty();
        }
    }

    private bool TryResolveFireTargetsAtCell(int x, int y, out FireCellTargets targets)
    {
        targets = default;

        if (!IsInBounds(x, y) || weatherGridManager == null)
            return false;

        // Exact cached footprint lookup.
        if (weatherGridManager.TryGetEnvironmentAtCell(x, y, out EnvironmentControl env) && env != null)
            targets.environment = env;

        if (weatherGridManager.TryGetBuildingAtCell(x, y, out PlayerBuildingManager.Record building) &&
            building != null &&
            building.instance != null)
        {
            targets.buildingRecord = building;
        }

        if (targets.HasAny)
            return true;

        // If lightning happened right after placement/spawn, caches may be stale.
        // Rebuild once and retry. Still no physics overlap.
        weatherGridManager.RebuildEnvironmentFootprintCoverage();
        weatherGridManager.RebuildBuildingFootprintCoverage();

        if (weatherGridManager.TryGetEnvironmentAtCell(x, y, out env) && env != null)
            targets.environment = env;

        if (weatherGridManager.TryGetBuildingAtCell(x, y, out building) &&
            building != null &&
            building.instance != null)
        {
            targets.buildingRecord = building;
        }

        return targets.HasAny;
    }

    private bool TryGetEnvironmentAtIgnitionCell(int x, int y, out EnvironmentControl env)
    {
        env = null;

        if (!TryResolveFireTargetsAtCell(x, y, out FireCellTargets targets))
            return false;

        env = targets.environment;
        return env != null;
    }

    private bool TryGetBuildingAtIgnitionCell(int x, int y, out PlayerBuildingManager.Record building)
    {
        building = null;

        if (!TryResolveFireTargetsAtCell(x, y, out FireCellTargets targets))
            return false;

        building = targets.buildingRecord;
        return building != null && building.instance != null;
    }

    private void ProcessBurningEnvironmentsStep(ref int remainingSpreadBudget)
    {
        for (int i = _burningEnvironments.Count - 1; i >= 0; i--)
        {
            BurningEnvironmentEntry entry = _burningEnvironments[i];

            if (entry.environment == null || entry.state == null)
            {
                RemoveBurningEnvironmentAt(i);
                continue;
            }

            if (!entry.state.IsOnFire)
            {
                RemoveBurningEnvironmentAt(i);
                continue;
            }

            float rain01 = GetRainIntensity01AtCell(entry.cell.x, entry.cell.y);
            bool stillBurning = entry.state.AdvanceBurnStep(
                rain01,
                environmentRainExtinguishChanceAtFullRain);

            if (!stillBurning)
            {
                RemoveBurningEnvironmentAt(i);
                continue;
            }

            if (fireCanSpread && remainingSpreadBudget > 0)
            {
                float sourceHeat01 = entry.state.BaseBurnTurns > 0
                    ? Mathf.Clamp01((float)entry.state.BurnTurnsRemaining / entry.state.BaseBurnTurns)
                    : 1f;

                int queued = 0;

                queued += TryQueueSpreadOffsets(
                    entry.cell,
                    FireSpreadOrthogonalOffsets,
                    fireSpreadChanceOrthogonal,
                    entry.state.CurrentDryness01,
                    sourceHeat01,
                    remainingSpreadBudget - queued);

                if (fireSpreadIncludesDiagonals && queued < remainingSpreadBudget)
                {
                    queued += TryQueueSpreadOffsets(
                        entry.cell,
                        FireSpreadDiagonalOffsets,
                        fireSpreadChanceDiagonal,
                        entry.state.CurrentDryness01,
                        sourceHeat01,
                        remainingSpreadBudget - queued);
                }

                remainingSpreadBudget -= queued;
            }
        }
    }

    private void ProcessBurningBuildingsStep(ref int remainingSpreadBudget)
    {
        for (int i = _burningBuildings.Count - 1; i >= 0; i--)
        {
            BurningBuildingEntry entry = _burningBuildings[i];

            if (entry.buildingRoot == null || entry.state == null)
            {
                RemoveBurningBuildingAt(i);
                continue;
            }

            if (!entry.state.IsOnFire)
            {
                RemoveBurningBuildingAt(i);
                continue;
            }

            float rain01 = GetRainIntensity01AtCell(entry.cell.x, entry.cell.y);

            int finalDamage = buildingDamagePerStep;

            BuildingFireResistance resistance = entry.buildingRoot.GetComponent<BuildingFireResistance>();
            if (resistance == null)
                resistance = entry.buildingRoot.GetComponentInChildren<BuildingFireResistance>(true);

            if (resistance != null)
                finalDamage = resistance.ModifyFireDamage(finalDamage);

            float extinguishChance = Mathf.Clamp01(buildingRainExtinguishChanceAtFullRain * rain01);
            bool stillBurning = entry.state.AdvanceBurnStep(finalDamage, extinguishChance);

            if (!stillBurning)
            {
                RemoveBurningBuildingAt(i);
                continue;
            }

            if (fireCanSpread && remainingSpreadBudget > 0)
            {
                float sourceDryness01 = Mathf.Clamp01(1f - rain01);
                float sourceHeat01 = entry.state.BaseBurnTurns > 0
                    ? Mathf.Clamp01((float)entry.state.BurnTurnsRemaining / entry.state.BaseBurnTurns)
                    : 1f;

                int queued = 0;

                queued += TryQueueSpreadOffsets(
                    entry.cell,
                    FireSpreadOrthogonalOffsets,
                    fireSpreadChanceOrthogonal,
                    sourceDryness01,
                    sourceHeat01,
                    remainingSpreadBudget - queued);

                if (fireSpreadIncludesDiagonals && queued < remainingSpreadBudget)
                {
                    queued += TryQueueSpreadOffsets(
                        entry.cell,
                        FireSpreadDiagonalOffsets,
                        fireSpreadChanceDiagonal,
                        sourceDryness01,
                        sourceHeat01,
                        remainingSpreadBudget - queued);
                }

                remainingSpreadBudget -= queued;
            }
        }
    }

    private int TryQueueSpreadOffsets(
        Vector2Int sourceCell,
        Vector2Int[] offsets,
        float baseChance01,
        float sourceDryness01,
        float sourceHeat01,
        int remainingBudget)
    {
        if (offsets == null || remainingBudget <= 0 || baseChance01 <= 0f)
            return 0;

        sourceDryness01 = Mathf.Clamp01(sourceDryness01);
        sourceHeat01 = Mathf.Clamp01(sourceHeat01);

        float sourceDrynessFactor = Mathf.Lerp(
            0.85f,
            1f + fireSpreadSourceDrynessBonus,
            sourceDryness01);

        float sourceHeatFactor = Mathf.Lerp(
            0.75f,
            1f + fireSpreadSourceHeatBonus,
            sourceHeat01);

        int queued = 0;

        for (int i = 0; i < offsets.Length && queued < remainingBudget; i++)
        {
            Vector2Int offset = offsets[i];

            int nx = sourceCell.x + offset.x;
            int ny = sourceCell.y + offset.y;

            if (!IsInBounds(nx, ny))
                continue;

            if (!CanAnythingIgniteAtCell(nx, ny))
                continue;

            if (IsAnythingOnFireAtCell(nx, ny))
                continue;

            float targetRain01 = GetRainIntensity01AtCell(nx, ny);
            float rainPenalty = Mathf.Lerp(1f, 1f - fireSpreadRainPenaltyStrength, targetRain01);
            float windMultiplier = GetFireSpreadWindMultiplier(sourceCell, offset);

            float spreadChance01 =
                baseChance01 *
                sourceDrynessFactor *
                sourceHeatFactor *
                rainPenalty *
                windMultiplier;

            spreadChance01 = Mathf.Clamp01(spreadChance01);

            if (spreadChance01 <= 0f)
                continue;

            if (UnityEngine.Random.value > spreadChance01)
                continue;

            if (QueuePendingFireIgnition(nx, ny, spreadChance01, sourceDryness01, sourceHeat01))
                queued++;
        }

        return queued;
    }

    private float GetFireSpreadWindMultiplier(Vector2Int sourceCell, Vector2Int spreadOffset)
    {
        if (cloudSimulationSystem == null)
            return 1f;

        Vector2Int windTarget = cloudSimulationSystem.GetWindTargetForCell(sourceCell.x, sourceCell.y);
        Vector2Int windOffset = windTarget - sourceCell;

        if (windOffset == Vector2Int.zero)
            return 1f;

        Vector2 spreadDir = new Vector2(spreadOffset.x, spreadOffset.y).normalized;
        Vector2 windDir = new Vector2(windOffset.x, windOffset.y).normalized;

        float alignment = Vector2.Dot(spreadDir, windDir);
        float multiplier = 1f + alignment * fireSpreadWindBiasStrength;

        return Mathf.Max(fireSpreadWindMinMultiplier, multiplier);
    }

    private bool QueuePendingFireIgnition(
        int x,
        int y,
        float chance01,
        float sourceDryness01,
        float sourceHeat01)
    {
        if (!IsInBounds(x, y))
            return false;

        chance01 = Mathf.Clamp01(chance01);
        sourceDryness01 = Mathf.Clamp01(sourceDryness01);
        sourceHeat01 = Mathf.Clamp01(sourceHeat01);

        if (chance01 <= 0f)
            return false;

        for (int i = 0; i < _pendingFireIgnitions.Count; i++)
        {
            PendingFireIgnition pending = _pendingFireIgnitions[i];

            if (pending.x == x && pending.y == y)
            {
                if (chance01 > pending.chance01)
                    pending.chance01 = chance01;

                if (sourceDryness01 > pending.sourceDryness01)
                    pending.sourceDryness01 = sourceDryness01;

                if (sourceHeat01 > pending.sourceHeat01)
                    pending.sourceHeat01 = sourceHeat01;

                _pendingFireIgnitions[i] = pending;
                return false;
            }
        }

        _pendingFireIgnitions.Add(new PendingFireIgnition
        {
            x = x,
            y = y,
            chance01 = chance01,
            sourceDryness01 = sourceDryness01,
            sourceHeat01 = sourceHeat01
        });

        return true;
    }

    private void ApplyPendingFireIgnitions()
    {
        if (_pendingFireIgnitions.Count == 0)
            return;

        for (int i = 0; i < _pendingFireIgnitions.Count; i++)
        {
            PendingFireIgnition pending = _pendingFireIgnitions[i];

            if (IsAnythingOnFireAtCell(pending.x, pending.y))
                continue;

            TryIgniteFireAtCell(
                pending.x,
                pending.y,
                pending.chance01,
                pending.sourceDryness01,
                pending.sourceHeat01,
                ignitionEvent: false);
        }

        _pendingFireIgnitions.Clear();
    }

    public bool IsAnythingOnFireAtCell(int x, int y)
    {
        if (!TryResolveFireTargetsAtCell(x, y, out FireCellTargets targets))
            return false;

        if (targets.environment != null)
        {
            EnvironmentFireState envFire = GetEnvironmentFireState(
                targets.environment,
                createIfMissing: false);

            if (envFire != null && envFire.IsOnFire)
                return true;
        }

        if (targets.HasBuilding)
        {
            BuildingFireState buildingFire = GetBuildingFireState(
                targets.buildingRecord.instance,
                createIfMissing: false);

            if (buildingFire != null && buildingFire.IsOnFire)
                return true;
        }

        return false;
    }

    public bool CanAnythingIgniteAtCell(int x, int y)
    {
        if (!TryResolveFireTargetsAtCell(x, y, out FireCellTargets targets))
            return false;

        if (targets.environment != null)
        {
            bool blockedByFlood =
                floodBlocksEnvironmentIgnition &&
                IsEnvironmentFootprintFlooded(targets.environment);

            if (!blockedByFlood)
            {
                EnvironmentFireState envFire = GetEnvironmentFireState(
                    targets.environment,
                    createIfMissing: false);

                bool alreadyBurning = envFire != null && envFire.IsOnFire;

                if (targets.environment.canCatchFire && !alreadyBurning)
                    return true;
            }
        }

        if (targets.HasBuilding)
        {
            GameObject buildingRoot = targets.buildingRecord.instance;

            bool blockedByFlood =
                floodBlocksBuildingIgnition &&
                IsBuildingFootprintFlooded(targets.buildingRecord);

            if (!blockedByFlood)
            {
                BuildingFireResistance resistance = buildingRoot.GetComponent<BuildingFireResistance>();
                if (resistance == null)
                    resistance = buildingRoot.GetComponentInChildren<BuildingFireResistance>(true);

                BuildingFireState buildingFire = GetBuildingFireState(
                    buildingRoot,
                    createIfMissing: false);

                bool immune = resistance != null && resistance.fireImmune;
                bool alreadyBurning = buildingFire != null && buildingFire.IsOnFire;

                if (!immune && !alreadyBurning)
                    return true;
            }
        }

        return false;
    }

    private bool IsEnvironmentFootprintFlooded(EnvironmentControl targetEnvironment)
    {
        if (targetEnvironment == null)
            return false;

        if (floodSimulationSystem == null || weatherGridManager == null)
            return false;

        if (floodSimulationSystem.ActiveFloodCellCount <= 0)
            return false;

        SnapshotFloodedCellsForFireCheck();

        for (int i = 0; i < _floodCheckScratch.Count; i++)
        {
            TileCoord floodCoord = _floodCheckScratch[i];

            if (!weatherGridManager.TryGetEnvironmentAtCell(
                    floodCoord.x,
                    floodCoord.y,
                    out EnvironmentControl floodedEnvironment))
            {
                continue;
            }

            if (floodedEnvironment == targetEnvironment)
                return true;
        }

        return false;
    }

    private bool IsBuildingFootprintFlooded(PlayerBuildingManager.Record targetBuildingRecord)
    {
        if (targetBuildingRecord == null || targetBuildingRecord.instance == null)
            return false;

        if (floodSimulationSystem == null || weatherGridManager == null)
            return false;

        if (floodSimulationSystem.ActiveFloodCellCount <= 0)
            return false;

        GameObject targetBuildingRoot = targetBuildingRecord.instance;

        SnapshotFloodedCellsForFireCheck();

        for (int i = 0; i < _floodCheckScratch.Count; i++)
        {
            TileCoord floodCoord = _floodCheckScratch[i];

            if (!weatherGridManager.TryGetBuildingAtCell(
                    floodCoord.x,
                    floodCoord.y,
                    out PlayerBuildingManager.Record floodedBuildingRecord))
            {
                continue;
            }

            if (floodedBuildingRecord == null || floodedBuildingRecord.instance == null)
                continue;

            if (floodedBuildingRecord.instance == targetBuildingRoot)
                return true;
        }

        return false;
    }

    private void SnapshotFloodedCellsForFireCheck()
    {
        _floodCheckScratch.Clear();

        if (floodSimulationSystem == null)
            return;

        foreach (KeyValuePair<TileCoord, FloodCellState> pair in floodSimulationSystem.ActiveFloodCells)
        {
            FloodCellState state = pair.Value;

            if (state == null)
                continue;

            if (state.floodDepth01 < minFloodDepthToBlockIgnition)
                continue;

            _floodCheckScratch.Add(pair.Key);
        }
    }

    private EnvironmentFireState GetEnvironmentFireState(EnvironmentControl env, bool createIfMissing)
    {
        if (env == null)
            return null;

        EnvironmentFireState state = env.GetComponent<EnvironmentFireState>();
        if (state == null)
            state = env.GetComponentInChildren<EnvironmentFireState>(true);

        if (state == null && createIfMissing)
            state = env.gameObject.AddComponent<EnvironmentFireState>();

        return state;
    }

    private BuildingFireState GetBuildingFireState(GameObject buildingRoot, bool createIfMissing)
    {
        if (buildingRoot == null)
            return null;

        BuildingFireState state = buildingRoot.GetComponent<BuildingFireState>();
        if (state == null)
            state = buildingRoot.GetComponentInChildren<BuildingFireState>(true);

        if (state == null && createIfMissing)
            state = buildingRoot.AddComponent<BuildingFireState>();

        return state;
    }

    private void EnsureBuildingSecondaryEffects(GameObject buildingRoot)
    {
        if (buildingRoot == null)
            return;

        BuildingFireSecondaryEffects secondary =
            buildingRoot.GetComponent<BuildingFireSecondaryEffects>();

        if (secondary == null)
            secondary = buildingRoot.GetComponentInChildren<BuildingFireSecondaryEffects>(true);

        if (secondary == null)
            secondary = buildingRoot.AddComponent<BuildingFireSecondaryEffects>();

        secondary.SetBaseDamagePerStep(buildingDamagePerStep);
    }

    private void RegisterBurningEnvironment(
        EnvironmentControl env,
        EnvironmentFireState state,
        Vector2Int cell)
    {
        if (env == null || state == null || !state.IsOnFire)
            return;

        int key = env.GetInstanceID();

        if (_burningEnvironmentKeys.Add(key))
        {
            _burningEnvironments.Add(new BurningEnvironmentEntry
            {
                environment = env,
                state = state,
                cell = cell
            });
        }
    }

    private void RegisterBurningBuilding(
        GameObject buildingRoot,
        BuildingFireState state,
        Vector2Int cell)
    {
        if (buildingRoot == null || state == null || !state.IsOnFire)
            return;

        int key = buildingRoot.GetInstanceID();

        if (_burningBuildingKeys.Add(key))
        {
            _burningBuildings.Add(new BurningBuildingEntry
            {
                buildingRoot = buildingRoot,
                state = state,
                cell = cell
            });
        }
    }

    private void RemoveBurningEnvironmentAt(int index)
    {
        if (index < 0 || index >= _burningEnvironments.Count)
            return;

        EnvironmentControl env = _burningEnvironments[index].environment;
        if (env != null)
            _burningEnvironmentKeys.Remove(env.GetInstanceID());

        _burningEnvironments.RemoveAt(index);
    }

    private void RemoveBurningBuildingAt(int index)
    {
        if (index < 0 || index >= _burningBuildings.Count)
            return;

        GameObject root = _burningBuildings[index].buildingRoot;
        if (root != null)
            _burningBuildingKeys.Remove(root.GetInstanceID());

        _burningBuildings.RemoveAt(index);
    }

    private float GetRainIntensity01AtCell(int x, int y)
    {
        if (!IsInBounds(x, y))
            return 0f;

        float rain01 = 0f;

        if (rainSimulationSystem != null)
        {
            if (rainSimulationSystem.IsRainingAtCell(x, y))
                rain01 = 1f;
            else
                rain01 = Mathf.Max(rain01, rainSimulationSystem.GetRainCharge01AtCell(x, y));
        }

        if (stormSimulationSystem != null)
        {
            float storm01 = stormSimulationSystem.GetStormIntensity01AtCell(x, y);
            rain01 = Mathf.Max(rain01, Mathf.Clamp01(storm01 * stormDampeningStrength));
        }

        return Mathf.Clamp01(rain01);
    }

    private bool IsInBounds(int x, int y)
    {
        return weatherGridManager != null &&
               weatherGridManager.IsInitialized &&
               x >= 0 &&
               y >= 0 &&
               x < weatherGridManager.Columns &&
               y < weatherGridManager.Rows;
    }

    public bool TryExtinguishFireAtCellFromFlood(TileCoord coord, float floodDepth01)
    {
        return TryExtinguishFireAtCellFromFlood(coord.x, coord.y, floodDepth01);
    }

    public bool TryExtinguishFireAtCellFromFlood(int x, int y, float floodDepth01)
    {
        if (floodDepth01 < minFloodDepthToExtinguishFire)
            return false;

        if (!IsInBounds(x, y))
            return false;

        if (!TryResolveFireTargetsAtCell(x, y, out FireCellTargets targets))
            return false;

        bool extinguishedAny = false;

        if (floodExtinguishesEnvironmentFire && targets.environment != null)
            extinguishedAny |= TryExtinguishEnvironmentFireFromFlood(targets.environment);

        if (floodExtinguishesBuildingFire && targets.HasBuilding)
            extinguishedAny |= TryExtinguishBuildingFireFromFlood(targets.buildingRecord.instance);

        if (extinguishedAny)
        {
            OnFireCellsChanged?.Invoke();
            MarkFireSaveDirty();

            if (debugLogging)
            {
                Debug.Log(
                    $"[WeatherFireSystem] Flood extinguished fire at ({x},{y}). " +
                    $"Depth={floodDepth01:0.00}");
            }
        }

        return extinguishedAny;
    }

    private bool TryExtinguishEnvironmentFireFromFlood(EnvironmentControl env)
    {
        if (env == null)
            return false;

        EnvironmentFireState fireState = GetEnvironmentFireState(env, createIfMissing: false);

        if (fireState == null || !fireState.IsOnFire)
            return false;

        // Use the existing burn-step logic as a guaranteed full-rain extinguish.
        // rain01 = 1 and extinguish chance = 1 should force it off.
        bool stillBurning = fireState.AdvanceBurnStep(
            rain01: 1f,
            extinguishChanceAtFullRain: 1f);

        if (stillBurning)
            return false;

        RemoveBurningEnvironmentOwner(env);

        if (debugLogging)
            Debug.Log($"[WeatherFireSystem] Flood extinguished environment fire on '{env.name}'.");

        return true;
    }

    private bool TryExtinguishBuildingFireFromFlood(GameObject buildingRoot)
    {
        if (buildingRoot == null)
            return false;

        BuildingFireState fireState = GetBuildingFireState(buildingRoot, createIfMissing: false);

        if (fireState == null || !fireState.IsOnFire)
            return false;

        // 0 damage because flood extinguishing should not apply fire tick damage.
        // 1f extinguish chance should force it off.
        bool stillBurning = fireState.AdvanceBurnStep(0, 1f);

        if (stillBurning)
            return false;

        RemoveBurningBuildingOwner(buildingRoot);

        if (debugLogging)
            Debug.Log($"[WeatherFireSystem] Flood extinguished building fire on '{buildingRoot.name}'.");

        return true;
    }

    private void RemoveBurningEnvironmentOwner(EnvironmentControl env)
    {
        if (env == null)
            return;

        int key = env.GetInstanceID();
        _burningEnvironmentKeys.Remove(key);

        for (int i = _burningEnvironments.Count - 1; i >= 0; i--)
        {
            BurningEnvironmentEntry entry = _burningEnvironments[i];

            if (entry.environment == env)
                _burningEnvironments.RemoveAt(i);
        }
    }

    private void RemoveBurningBuildingOwner(GameObject buildingRoot)
    {
        if (buildingRoot == null)
            return;

        int key = buildingRoot.GetInstanceID();
        _burningBuildingKeys.Remove(key);

        for (int i = _burningBuildings.Count - 1; i >= 0; i--)
        {
            BurningBuildingEntry entry = _burningBuildings[i];

            if (entry.buildingRoot == buildingRoot)
                _burningBuildings.RemoveAt(i);
        }
    }

    public FireSimulationSaveData SaveState()
    {
        FireSimulationSaveData data = new FireSimulationSaveData();

        for (int i = 0; i < _burningEnvironments.Count; i++)
        {
            BurningEnvironmentEntry entry = _burningEnvironments[i];

            if (entry.environment == null || entry.state == null)
                continue;

            if (!entry.state.IsOnFire)
                continue;

            data.burningEnvironments.Add(new EnvironmentFireSaveData
            {
                x = entry.cell.x,
                y = entry.cell.y,

                burnTurnsRemaining = Mathf.Max(1, entry.state.BurnTurnsRemaining),
                baseBurnTurns = Mathf.Max(1, entry.state.BaseBurnTurns),

                currentDryness01 = Mathf.Clamp01(entry.state.CurrentDryness01)
            });
        }

        for (int i = 0; i < _burningBuildings.Count; i++)
        {
            BurningBuildingEntry entry = _burningBuildings[i];

            if (entry.buildingRoot == null || entry.state == null)
                continue;

            if (!entry.state.IsOnFire)
                continue;

            data.burningBuildings.Add(new BuildingFireSaveData
            {
                x = entry.cell.x,
                y = entry.cell.y,

                burnTurnsRemaining = Mathf.Max(1, entry.state.BurnTurnsRemaining),
                baseBurnTurns = Mathf.Max(1, entry.state.BaseBurnTurns)
            });
        }

        return data;
    }

    public void LoadState(FireSimulationSaveData data)
    {
        EnsureLinks();
        RebindLightningEvents();

        ClearRuntimeFireTrackingForLoad();

        if (data == null)
            return;

        int restoredEnvironmentFires = 0;
        int restoredBuildingFires = 0;

        if (data.burningEnvironments != null)
        {
            for (int i = 0; i < data.burningEnvironments.Count; i++)
            {
                EnvironmentFireSaveData saved = data.burningEnvironments[i];

                if (saved == null)
                    continue;

                if (RestoreEnvironmentFireFromSave(saved))
                    restoredEnvironmentFires++;
            }
        }

        if (data.burningBuildings != null)
        {
            for (int i = 0; i < data.burningBuildings.Count; i++)
            {
                BuildingFireSaveData saved = data.burningBuildings[i];

                if (saved == null)
                    continue;

                if (RestoreBuildingFireFromSave(saved))
                    restoredBuildingFires++;
            }
        }

        if (restoredEnvironmentFires > 0 || restoredBuildingFires > 0)
            OnFireCellsChanged?.Invoke();

        if (debugLogging)
        {
            Debug.Log(
                $"[WeatherFireSystem] Loaded fire state. " +
                $"EnvironmentFires={restoredEnvironmentFires}, BuildingFires={restoredBuildingFires}");
        }
    }

    private void ClearRuntimeFireTrackingForLoad()
    {
        _burningEnvironments.Clear();
        _burningEnvironmentKeys.Clear();

        _burningBuildings.Clear();
        _burningBuildingKeys.Clear();

        _pendingFireIgnitions.Clear();
    }

    private bool RestoreEnvironmentFireFromSave(EnvironmentFireSaveData saved)
    {
        if (saved == null)
            return false;

        if (!IsInBounds(saved.x, saved.y))
            return false;

        if (!TryGetEnvironmentAtIgnitionCell(saved.x, saved.y, out EnvironmentControl env) || env == null)
            return false;

        if (!env.canCatchFire)
            return false;

        if (floodBlocksEnvironmentIgnition && IsEnvironmentFootprintFlooded(env))
            return false;

        EnvironmentFireState fireState = GetEnvironmentFireState(
            env,
            autoAddMissingEnvironmentFireState);

        if (fireState == null)
            return false;

        int burnTurns = Mathf.Max(1, saved.burnTurnsRemaining);

        if (!fireState.IsOnFire)
        {
            if (!fireState.TryIgnite(1f, burnTurns))
                return false;
        }

        RegisterBurningEnvironment(
            env,
            fireState,
            new Vector2Int(saved.x, saved.y));

        OnFireCellIgnited?.Invoke(new TileCoord(saved.x, saved.y));

        return true;
    }

    private bool RestoreBuildingFireFromSave(BuildingFireSaveData saved)
    {
        if (saved == null)
            return false;

        if (!IsInBounds(saved.x, saved.y))
            return false;

        if (!fireCanIgniteBuildings)
            return false;

        if (!TryGetBuildingAtIgnitionCell(saved.x, saved.y, out PlayerBuildingManager.Record record) ||
            record == null ||
            record.instance == null)
        {
            return false;
        }

        if (floodBlocksBuildingIgnition && IsBuildingFootprintFlooded(record))
            return false;

        GameObject buildingRoot = record.instance;

        BuildingFireResistance resistance = buildingRoot.GetComponent<BuildingFireResistance>();
        if (resistance == null)
            resistance = buildingRoot.GetComponentInChildren<BuildingFireResistance>(true);

        if (resistance != null && resistance.fireImmune)
            return false;

        BuildingFireState fireState = GetBuildingFireState(
            buildingRoot,
            autoAddMissingBuildingFireState);

        if (fireState == null)
            return false;

        int burnTurns = Mathf.Max(1, saved.burnTurnsRemaining);

        if (!fireState.IsOnFire)
        {
            if (!fireState.TryIgnite(1f, burnTurns))
                return false;
        }

        if (autoAddBuildingSecondaryEffects)
            EnsureBuildingSecondaryEffects(buildingRoot);

        RegisterBurningBuilding(
            buildingRoot,
            fireState,
            new Vector2Int(saved.x, saved.y));

        OnFireCellIgnited?.Invoke(new TileCoord(saved.x, saved.y));

        return true;
    }

    private void MarkFireSaveDirty()
    {
        SaveSystem.MarkSectionDirty(SaveSectionKeys.WorldSim);
    }

    public void ApplyPresetSettings(FirePresetSettings settings)
    {
        if (settings == null || !settings.overrideFire)
            return;

        lightningCanStartFires = settings.lightningCanStartFires;
        lightningFireStartChance = settings.lightningFireStartChance;

        minIgnitionMultiplierAtFullRain = settings.minIgnitionMultiplierAtFullRain;
        stormDampeningStrength = settings.stormDampeningStrength;

        environmentBurnTurns = settings.environmentBurnTurns;
        environmentDrynessIgnitionBonus = settings.environmentDrynessIgnitionBonus;
        environmentHeatIgnitionBonus = settings.environmentHeatIgnitionBonus;
        environmentRainExtinguishChanceAtFullRain = settings.environmentRainExtinguishChanceAtFullRain;

        fireCanIgniteBuildings = settings.fireCanIgniteBuildings;
        buildingBurnTurns = settings.buildingBurnTurns;
        buildingDamagePerStep = settings.buildingDamagePerStep;

        fireCanSpread = settings.fireCanSpread;
        fireSpreadIncludesDiagonals = settings.fireSpreadIncludesDiagonals;
        fireSpreadChanceOrthogonal = settings.fireSpreadChanceOrthogonal;
        fireSpreadChanceDiagonal = settings.fireSpreadChanceDiagonal;
        fireSpreadRainPenaltyStrength = settings.fireSpreadRainPenaltyStrength;
        fireSpreadWindBiasStrength = settings.fireSpreadWindBiasStrength;

        if (debugLogging)
            Debug.Log("[WeatherFireSystem] Applied fire preset settings.");
    }
}