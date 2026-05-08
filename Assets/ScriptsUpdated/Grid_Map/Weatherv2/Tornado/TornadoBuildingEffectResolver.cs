using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolves tornado effects against buildings using:
/// - WeatherGridManager building coverage
/// - TornadoSimulationSystem active cells
/// - second-based hit intervals
/// - max hits per turn per building
/// </summary>
public class TornadoBuildingEffectResolver : MonoBehaviour
{
    public static TornadoBuildingEffectResolver Instance { get; private set; }

    [Header("References")]
    [SerializeField] private TornadoSimulationSystem tornadoSimulationSystem;
    [SerializeField] private WeatherGridManager weatherGridManager;
    [SerializeField] private StormSimulationSystem stormSimulationSystem;

    [Header("Lifecycle")]
    [SerializeField] private bool processOnEnable = true;
    [SerializeField] private bool processOnTornadoStateChanged = true;
    [SerializeField] private bool processTimedHitsInUpdate = true;

    [Header("Building Tornado Effects")]
    [Min(0)][SerializeField] private int tornadoDamagePerHit = 20;

    [Header("Second-Based Hit Timing")]
    [Min(0.01f)][SerializeField] private float tornadoBuildingHitIntervalSeconds = 1f;
    [Min(1)][SerializeField] private int baseBuildingHitsPerInterval = 1;
    [Min(1)][SerializeField] private int maxBuildingHitsPerTurn = 3;

    [Header("Extra Hits From Storm Intensity")]
    [SerializeField] private bool extraBuildingHitsFromStormIntensity = true;
    [Range(0f, 1f)][SerializeField] private float stormIntensityForSecondHit = 0.85f;
    [Range(0f, 1f)][SerializeField] private float stormIntensityForThirdHit = 0.95f;

    [Header("Extra Hits From Remaining Lifetime")]
    [SerializeField] private bool extraBuildingHitsFromRemainingLifetime = false;
    [Min(1)][SerializeField] private int remainingLifetimeForSecondHit = 4;
    [Min(1)][SerializeField] private int remainingLifetimeForThirdHit = 6;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    private sealed class TornadoBuildingImpactState
    {
        public BuildingControl building;
        public float maxStormIntensity01;
        public int maxLifetimeRemaining;
        public int impactedCellCount;
    }

    private sealed class BuildingHitRuntimeState
    {
        public float nextAllowedHitTime;
        public int lastTurnWithBudgetReset = int.MinValue;
        public int hitsAppliedThisTurn;
    }

    private readonly Dictionary<BuildingControl, TornadoBuildingImpactState> _buildingImpactStates =
        new Dictionary<BuildingControl, TornadoBuildingImpactState>();

    private readonly Dictionary<BuildingControl, BuildingHitRuntimeState> _buildingHitRuntimeStates =
        new Dictionary<BuildingControl, BuildingHitRuntimeState>();

    private readonly HashSet<BuildingControl> _buildingsUnderTornadoThisStepRefs =
        new HashSet<BuildingControl>();

    private readonly HashSet<BuildingControl> _buildingsUnderTornadoLastStepRefs =
        new HashSet<BuildingControl>();

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

        if (processOnEnable)
        {
            RefreshImpactedBuildings();
            ProcessTimedBuildingHits();
        }
    }

    private void Update()
    {
        if (!processTimedHitsInUpdate)
            return;

        ProcessTimedBuildingHits();
    }

    private void OnDisable()
    {
        UnbindSourceEvents();

        _buildingImpactStates.Clear();
        _buildingsUnderTornadoThisStepRefs.Clear();

        NotifyTornadoClearedForBuildingsNoLongerAffected();
        RemoveRuntimeStatesForClearedBuildings();

        _buildingsUnderTornadoLastStepRefs.Clear();
        _buildingHitRuntimeStates.Clear();
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
        StormSimulationSystem newStormSimulationSystem = null,
        bool processNow = true)
    {
        if (newTornadoSimulationSystem != null)
            tornadoSimulationSystem = newTornadoSimulationSystem;

        if (newWeatherGridManager != null)
            weatherGridManager = newWeatherGridManager;

        if (newStormSimulationSystem != null)
            stormSimulationSystem = newStormSimulationSystem;

        RebindSourceEvents();

        if (processNow)
        {
            RefreshImpactedBuildings();
            ProcessTimedBuildingHits();
        }
    }

    private void HandleTornadoStateChanged()
    {
        if (!processOnTornadoStateChanged)
            return;

        RefreshImpactedBuildings();
        ProcessTimedBuildingHits();
    }

    private void RefreshImpactedBuildings()
    {
        _buildingImpactStates.Clear();
        _buildingsUnderTornadoThisStepRefs.Clear();

        if (tornadoSimulationSystem == null || !tornadoSimulationSystem.IsInitialized ||
            weatherGridManager == null || !weatherGridManager.IsInitialized)
        {
            NotifyTornadoClearedForBuildingsNoLongerAffected();
            RemoveRuntimeStatesForClearedBuildings();
            _buildingsUnderTornadoLastStepRefs.Clear();
            return;
        }

        IReadOnlyList<Vector2Int> activeCells = tornadoSimulationSystem.GetActiveTornadoCells();
        if (activeCells == null || activeCells.Count == 0)
        {
            NotifyTornadoClearedForBuildingsNoLongerAffected();
            RemoveRuntimeStatesForClearedBuildings();
            _buildingsUnderTornadoLastStepRefs.Clear();
            return;
        }

        for (int i = 0; i < activeCells.Count; i++)
        {
            Vector2Int cell = activeCells[i];

            if (!weatherGridManager.TryGetBuildingAtCell(cell.x, cell.y, out WorldBuildingManager.Record record) ||
                record == null)
            {
                if (debugLogging)
                    //Debug.Log($"[TornadoBuildingEffectResolver] Cell {cell} has no building record.");

                continue;
            }

            BuildingControl building = GetBuildingControlFromRecord(record);
            if (building == null)
            {
                if (debugLogging)
                {
                    //Debug.Log(
                        //$"[TornadoBuildingEffectResolver] Cell {cell} matched building record '{record.instanceId}', " +
                        //$"but no BuildingControl was found on instance '{(record.instance != null ? record.instance.name : "null")}'.");
                }

                continue;
            }

            _buildingsUnderTornadoThisStepRefs.Add(building);

            if (!_buildingImpactStates.TryGetValue(building, out TornadoBuildingImpactState state))
            {
                state = new TornadoBuildingImpactState
                {
                    building = building,
                    maxStormIntensity01 = 0f,
                    maxLifetimeRemaining = 0,
                    impactedCellCount = 0
                };

                _buildingImpactStates.Add(building, state);
            }

            state.impactedCellCount++;

            int remainingLifetime = tornadoSimulationSystem.GetTornadoLifetimeAtCell(cell.x, cell.y);
            if (remainingLifetime > state.maxLifetimeRemaining)
                state.maxLifetimeRemaining = remainingLifetime;

            if (stormSimulationSystem != null && stormSimulationSystem.IsInitialized)
            {
                float stormIntensity = stormSimulationSystem.GetStormIntensity01AtCell(cell.x, cell.y);
                if (stormIntensity > state.maxStormIntensity01)
                    state.maxStormIntensity01 = stormIntensity;
            }

            if (!_buildingHitRuntimeStates.ContainsKey(building))
            {
                _buildingHitRuntimeStates.Add(building, new BuildingHitRuntimeState
                {
                    nextAllowedHitTime = 0f,
                    lastTurnWithBudgetReset = int.MinValue,
                    hitsAppliedThisTurn = 0
                });
            }

            if (debugLogging)
            {
                //Debug.Log(
                    //$"[TornadoBuildingEffectResolver] Cell {cell} matched building '{building.name}' " +
                    //$"from record '{record.instanceId}'.");
            }
        }

        NotifyTornadoClearedForBuildingsNoLongerAffected();
        RemoveRuntimeStatesForClearedBuildings();

        _buildingsUnderTornadoLastStepRefs.Clear();
        foreach (BuildingControl building in _buildingsUnderTornadoThisStepRefs)
        {
            if (building != null)
                _buildingsUnderTornadoLastStepRefs.Add(building);
        }
    }

    private void ProcessTimedBuildingHits()
    {
        if (_buildingImpactStates.Count == 0)
            return;

        float now = Time.time;
        float intervalSeconds = Mathf.Max(0.01f, tornadoBuildingHitIntervalSeconds);

        int currentTurn = TurnSystem.Instance != null ? TurnSystem.GetCurrentTurn() : -1;
        bool hasTurnBudget = currentTurn >= 0;

        foreach (KeyValuePair<BuildingControl, TornadoBuildingImpactState> kv in _buildingImpactStates)
        {
            TornadoBuildingImpactState state = kv.Value;
            if (state == null || state.building == null)
                continue;

            BuildingControl building = state.building;

            if (!_buildingHitRuntimeStates.TryGetValue(building, out BuildingHitRuntimeState runtime))
            {
                runtime = new BuildingHitRuntimeState
                {
                    nextAllowedHitTime = 0f,
                    lastTurnWithBudgetReset = int.MinValue,
                    hitsAppliedThisTurn = 0
                };

                _buildingHitRuntimeStates.Add(building, runtime);
            }

            if (hasTurnBudget && runtime.lastTurnWithBudgetReset != currentTurn)
            {
                runtime.lastTurnWithBudgetReset = currentTurn;
                runtime.hitsAppliedThisTurn = 0;
            }

            if (now < runtime.nextAllowedHitTime)
                continue;

            int desiredHitsThisInterval = CalculateBuildingHitsThisInterval(state);

            if (desiredHitsThisInterval <= 0)
                continue;

            int hitsToApply = desiredHitsThisInterval;

            if (hasTurnBudget)
            {
                int remainingTurnBudget = Mathf.Max(0, Mathf.Max(1, maxBuildingHitsPerTurn) - runtime.hitsAppliedThisTurn);
                if (remainingTurnBudget <= 0)
                {
                    if (debugLogging)
                    {
                        //Debug.Log(
                            //$"[TornadoBuildingEffectResolver] Building '{building.name}' reached max hits for turn {currentTurn}.");
                    }

                    continue;
                }

                hitsToApply = Mathf.Min(hitsToApply, remainingTurnBudget);
            }

            if (hitsToApply <= 0)
                continue;

            for (int hitIndex = 0; hitIndex < hitsToApply; hitIndex++)
            {
                if (building == null)
                    break;

                ApplyTileBasedTornadoImpactToBuilding(
                    building,
                    hitIndex + 1,
                    hitsToApply,
                    state.maxStormIntensity01,
                    state.maxLifetimeRemaining,
                    state.impactedCellCount);
            }

            if (hasTurnBudget)
                runtime.hitsAppliedThisTurn += hitsToApply;

            runtime.nextAllowedHitTime = now + intervalSeconds;

            if (debugLogging)
            {
                //Debug.Log(
                    //$"[TornadoBuildingEffectResolver] Applied {hitsToApply} timed tornado hit(s) to '{building.name}'. " +
                    //$"NextAllowed={runtime.nextAllowedHitTime:F2} Turn={currentTurn} HitsThisTurn={runtime.hitsAppliedThisTurn}");
            }
        }
    }

    private int CalculateBuildingHitsThisInterval(TornadoBuildingImpactState state)
    {
        if (state == null || state.building == null)
            return 0;

        int hits = Mathf.Max(1, baseBuildingHitsPerInterval);

        if (extraBuildingHitsFromStormIntensity)
        {
            if (state.maxStormIntensity01 >= stormIntensityForSecondHit)
                hits++;

            if (state.maxStormIntensity01 >= stormIntensityForThirdHit)
                hits++;
        }

        if (extraBuildingHitsFromRemainingLifetime)
        {
            if (state.maxLifetimeRemaining >= remainingLifetimeForSecondHit)
                hits++;

            if (state.maxLifetimeRemaining >= remainingLifetimeForThirdHit)
                hits++;
        }

        return Mathf.Max(0, hits);
    }

    private BuildingControl GetBuildingControlFromRecord(WorldBuildingManager.Record record)
    {
        if (record == null || record.instance == null)
            return null;

        return record.instance.GetComponent<BuildingControl>();
    }

    private void NotifyTornadoClearedForBuildingsNoLongerAffected()
    {
        if (_buildingsUnderTornadoLastStepRefs.Count == 0)
            return;

        foreach (BuildingControl building in _buildingsUnderTornadoLastStepRefs)
        {
            if (building == null)
                continue;

            if (_buildingsUnderTornadoThisStepRefs.Contains(building))
                continue;

            NotifyBuildingTornadoCleared(building);
        }
    }

    private void RemoveRuntimeStatesForClearedBuildings()
    {
        if (_buildingsUnderTornadoLastStepRefs.Count == 0)
            return;

        List<BuildingControl> toRemove = null;

        foreach (BuildingControl building in _buildingsUnderTornadoLastStepRefs)
        {
            if (building == null)
                continue;

            if (_buildingsUnderTornadoThisStepRefs.Contains(building))
                continue;

            if (toRemove == null)
                toRemove = new List<BuildingControl>();

            toRemove.Add(building);
        }

        if (toRemove == null)
            return;

        for (int i = 0; i < toRemove.Count; i++)
            _buildingHitRuntimeStates.Remove(toRemove[i]);
    }

    private void NotifyBuildingTornadoCleared(BuildingControl building)
    {
        if (building == null)
            return;

        ProductionBuildingControl production = building.GetComponent<ProductionBuildingControl>();
        if (production == null)
            production = building.GetComponentInChildren<ProductionBuildingControl>(true);

        if (production != null)
            production.NotifyTornadoCleared(building.GetInstanceID(), debugLogging);

        KineticWarfareControl training = building.GetComponent<KineticWarfareControl>();
        if (training == null)
            training = building.GetComponentInChildren<KineticWarfareControl>(true);

        if (training != null)
            training.NotifyTornadoCleared(building.GetInstanceID(), debugLogging);
    }

    private void ApplyTileBasedTornadoImpactToBuilding(
        BuildingControl building,
        int hitNumberThisInterval,
        int totalHitsThisInterval,
        float maxStormIntensity01,
        int maxLifetimeRemaining,
        int impactedCellCount)
    {
        if (building == null)
            return;

        int finalDamage = tornadoDamagePerHit;

        BuildingTornadoResistance resistance = building.GetComponent<BuildingTornadoResistance>();
        if (resistance == null)
            resistance = building.GetComponentInChildren<BuildingTornadoResistance>(true);

        if (resistance != null)
            finalDamage = resistance.ModifyTornadoDamage(finalDamage);

        if (finalDamage <= 0)
        {
            if (debugLogging)
            {
                //Debug.Log(
                    //$"[TornadoBuildingEffectResolver] {building.name} resisted tornado hit " +
                    //$"{hitNumberThisInterval}/{totalHitsThisInterval}.");
            }

            return;
        }

        building.ApplyDamage(finalDamage);

        float casualtyMultiplier = tornadoDamagePerHit > 0
            ? finalDamage / (float)tornadoDamagePerHit
            : 1f;

        int killedInShelter = 0;
        int cancelledCraftOrders = 0;
        int killedCrafters = 0;
        int killedProductionWorkers = 0;
        bool productionPaused = false;
        int killedTrainees = 0;
        bool trainingPaused = false;

        ShelterControl shelter = building.GetComponent<ShelterControl>();
        if (shelter == null)
            shelter = building.GetComponentInChildren<ShelterControl>(true);

        if (shelter != null)
            killedInShelter = shelter.TryApplyTornadoCasualties(casualtyMultiplier, debugLogging);

        CraftingBuildingControl crafting = building.GetComponent<CraftingBuildingControl>();
        if (crafting == null)
            crafting = building.GetComponentInChildren<CraftingBuildingControl>(true);

        if (crafting != null)
        {
            var craftingImpact = crafting.TryApplyTornadoCraftingImpact(casualtyMultiplier, debugLogging);
            cancelledCraftOrders = craftingImpact.cancelledOrders;
            killedCrafters = craftingImpact.workersKilled;
        }

        ProductionBuildingControl production = building.GetComponent<ProductionBuildingControl>();
        if (production == null)
            production = building.GetComponentInChildren<ProductionBuildingControl>(true);

        if (production != null)
        {
            var productionImpact = production.RegisterTornadoImpact(
                building.GetInstanceID(),
                casualtyMultiplier,
                debugLogging);

            killedProductionWorkers = productionImpact.workersKilled;
            productionPaused = productionImpact.paused;
        }

        KineticWarfareControl training = building.GetComponent<KineticWarfareControl>();
        if (training == null)
            training = building.GetComponentInChildren<KineticWarfareControl>(true);

        if (training != null)
        {
            var trainingImpact = training.RegisterTornadoImpact(
                building.GetInstanceID(),
                casualtyMultiplier,
                debugLogging);

            killedTrainees = trainingImpact.traineesKilled;
            trainingPaused = trainingImpact.paused;
        }

        if (debugLogging)
        {
            //Debug.Log(
                //$"[TornadoBuildingEffectResolver] Tornado hit {building.name} " +
                //$"({hitNumberThisInterval}/{totalHitsThisInterval}) for {finalDamage}. " +
                //$"Storm={maxStormIntensity01:F2} Lifetime={maxLifetimeRemaining} Cells={impactedCellCount} | " +
                //$"ShelterDeaths={killedInShelter} | " +
                //$"CancelledCraftOrders={cancelledCraftOrders} | " +
                //$"CrafterDeaths={killedCrafters} | " +
                //$"ProductionPaused={productionPaused} | " +
                //$"ProductionWorkerDeaths={killedProductionWorkers} | " +
                //$"TrainingPaused={trainingPaused} | " +
                //$"TrainingDeaths={killedTrainees}");
        }
    }

    private void EnsureLinks()
    {
        if (tornadoSimulationSystem == null)
            tornadoSimulationSystem = TornadoSimulationSystem.Instance;

        if (weatherGridManager == null)
            weatherGridManager = WeatherGridManager.Instance;

        if (stormSimulationSystem == null)
            stormSimulationSystem = StormSimulationSystem.Instance;
    }

    private void RebindSourceEvents()
    {
        if (_subscribedTornadoSimulationSystem == tornadoSimulationSystem)
            return;

        UnbindSourceEvents();
        _subscribedTornadoSimulationSystem = tornadoSimulationSystem;

        if (_subscribedTornadoSimulationSystem != null)
            _subscribedTornadoSimulationSystem.OnTornadoStateChanged += HandleTornadoStateChanged;
    }

    private void UnbindSourceEvents()
    {
        if (_subscribedTornadoSimulationSystem == null)
            return;

        _subscribedTornadoSimulationSystem.OnTornadoStateChanged -= HandleTornadoStateChanged;
        _subscribedTornadoSimulationSystem = null;
    }
}
