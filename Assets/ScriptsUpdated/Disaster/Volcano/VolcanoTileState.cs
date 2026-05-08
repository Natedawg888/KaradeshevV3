using UnityEngine;

[DisallowMultipleComponent]
public class VolcanoTileState : MonoBehaviour
{
    [Header("Optional Links")]
    [SerializeField] private TileScript tile;
    [SerializeField] private EnvironmentControl environmentControl;
    [SerializeField] private VolcanoEnvironmentVisuals visuals;

    [Header("Start State")]
    public VolcanoActivityState startState = VolcanoActivityState.Mountain;

    [Tooltip("If true, this tile has already been seeded/initialized as a volcanic tile candidate.")]
    public bool startsAsSeeded = true;

    [Tooltip("Mountain-state tile can secretly gain volcanic energy and awaken into Dormant.")]
    public bool canBecomeVolcano = false;

    [Header("Energy")]
    [Range(0f, 1f)] public float energy01 = 0f;

    [Range(0f, 1f)] public float mountainEnergyGainPerTurn = 0.01f;
    [Range(0f, 1f)] public float dormantEnergyGainPerTurn = 0.04f;
    [Range(0f, 1f)] public float eruptionEnergyLossPerTurn = 0.18f;

    [Tooltip("Mountain reaches this energy before becoming Dormant.")]
    [Range(0f, 1f)] public float energyNeededToAwaken = 0.45f;

    [Tooltip("Dormant reaches this energy before entering Erupting.")]
    [Range(0f, 1f)] public float energyNeededToErupt = 0.90f;

    [Tooltip("Energy floor applied after a normal eruption ends and returns to Dormant.")]
    [Range(0f, 1f)] public float postEruptionEnergy = 0.25f;

    [Header("Low Energy Reversion")]
    [Range(0f, 1f)] public float lowEnergyThreshold = 0.10f;

    [Tooltip("0 disables low-energy reversion.")]
    [Min(0)] public int lowEnergyTurnsBeforeMountainRevert = 8;

    [Header("Eruption Duration")]
    [Min(1)] public int minEruptionTurns = 3;
    [Min(1)] public int maxEruptionTurns = 7;

    [Header("Volcanic Soot Output")]
    [Tooltip("How much soot this volcano releases each eruption turn. Smaller volcanoes should use lower values.")]
    [SerializeField, Range(0f, 10f)] private float sootEmissionPerEruptionTurn = 1.0f;

    [Tooltip("Lowest stamp radius this volcano can use when erupting.")]
    [SerializeField, Min(0)] private int minSootStampRadius = 0;

    [Tooltip("Largest stamp radius this volcano can use when soot output is high.")]
    [SerializeField, Min(0)] private int maxSootStampRadius = 3;

    [Tooltip("Soot amount that maps to the smallest stamp radius.")]
    [SerializeField, Range(0.01f, 10f)] private float sootAmountForMinStampRadius = 0.25f;

    [Tooltip("Soot amount that maps to the largest stamp radius.")]
    [SerializeField, Range(0.01f, 10f)] private float sootAmountForMaxStampRadius = 4.0f;

    [Tooltip("Maximum soot this volcano can add to one cell during one eruption turn.")]
    [SerializeField, Range(0.01f, 1f)] private float maxSootAddedPerCellPerTurn = 0.35f;

    [Tooltip("How much weaker soot gets for each ring away from the volcano footprint.")]
    [SerializeField, Range(0f, 1f)] private float sootStampFalloffPerCell = 0.35f;

    [Header("Behavior")]
    [Tooltip("If false, a mountain that becomes Dormant cannot also erupt in the same turn.")]
    public bool allowEruptSameTurnAsAwakening = false;

    [Tooltip("If true, an eruption ending at very low energy can revert to Mountain instead of Dormant.")]
    public bool allowMountainRevertAfterEruptionWhenLow = true;

    [Tooltip("If true, this state registers itself with VolcanoManager on enable/start.")]
    public bool autoRegisterWithManager = true;

    [Header("Lava Output")]
    [Tooltip("How many new lava cells this volcano can add per eruption turn. 0 = only seed the volcano footprint.")]
    [SerializeField, Min(0)] private int lavaCellsPerEruptionTurn = 2;

    [Tooltip("Maximum lava spread distance from this volcano's source footprint. 0 = unlimited.")]
    [SerializeField, Min(0)] private int maxLavaDistanceFromSource = 6;

    [Tooltip("Heat applied to lava emitted by this volcano. 1 = hot, 0 = already cooled.")]
    [SerializeField, Range(0f, 1f)] private float lavaHeatOnEmission = 1f;

    [Tooltip("How many turns lava stays hot after last being refreshed by eruption flow.")]
    [SerializeField, Min(0)] private int lavaCoolingDelayTurns = 1;

    [Tooltip("How many turns lava takes to cool from hot to black before removal.")]
    [SerializeField, Min(1)] private int lavaCoolingTurns = 4;

    [Header("Debug")]
    public bool debugLogging = false;

    private VolcanoActivityState activityState;
    private bool seeded;
    private int stateTurns;
    private int eruptionTurnsRemaining;
    private int lowEnergyTurns;
    private bool runtimeInitialized;

    public VolcanoActivityState ActivityState => activityState;
    public bool Seeded => seeded;
    public bool CanBecomeVolcano => canBecomeVolcano;
    public bool IsVolcano => activityState == VolcanoActivityState.Dormant || activityState == VolcanoActivityState.Erupting;
    public bool IsErupting => activityState == VolcanoActivityState.Erupting;
    public float Energy01 => energy01;
    public int StateTurns => stateTurns;
    public int EruptionTurnsRemaining => eruptionTurnsRemaining;
    public int LowEnergyTurns => lowEnergyTurns;

    public bool ShouldStayRegistered => canBecomeVolcano || IsVolcano || startState != VolcanoActivityState.Mountain;

    private void Awake()
    {
        EnsureLinks();
        InitializeRuntimeFromInspector(force: false);
    }

    private void OnEnable()
    {
        EnsureLinks();
        InitializeRuntimeFromInspector(force: false);

        if (autoRegisterWithManager && VolcanoManager.Instance != null)
            VolcanoManager.Instance.RegisterVolcano(this);
    }

    private void Start()
    {
        EnsureLinks();
        InitializeRuntimeFromInspector(force: false);

        if (autoRegisterWithManager && VolcanoManager.Instance != null)
            VolcanoManager.Instance.RegisterVolcano(this);
    }

    private void OnDisable()
    {
        if (autoRegisterWithManager && VolcanoManager.Instance != null)
            VolcanoManager.Instance.UnregisterVolcano(this);
    }

    private void OnValidate()
    {
        energy01 = Mathf.Clamp01(energy01);
        mountainEnergyGainPerTurn = Mathf.Clamp01(mountainEnergyGainPerTurn);
        dormantEnergyGainPerTurn = Mathf.Clamp01(dormantEnergyGainPerTurn);
        eruptionEnergyLossPerTurn = Mathf.Clamp01(eruptionEnergyLossPerTurn);
        energyNeededToAwaken = Mathf.Clamp01(energyNeededToAwaken);
        energyNeededToErupt = Mathf.Clamp01(energyNeededToErupt);
        postEruptionEnergy = Mathf.Clamp01(postEruptionEnergy);
        lowEnergyThreshold = Mathf.Clamp01(lowEnergyThreshold);

        if (maxEruptionTurns < minEruptionTurns)
            maxEruptionTurns = minEruptionTurns;

        if (!Application.isPlaying)
        {
            EnsureLinks();
            if (visuals != null)
                visuals.SetState(startState);
        }
    }

    public void Bind(TileScript targetTile)
    {
        tile = targetTile;
        EnsureLinks();
    }

    public void InitializeRuntimeFromInspector(bool force)
    {
        if (runtimeInitialized && !force)
            return;

        runtimeInitialized = true;

        seeded = startsAsSeeded || startState != VolcanoActivityState.Mountain;
        activityState = startState;
        stateTurns = 0;
        lowEnergyTurns = 0;

        if (activityState != VolcanoActivityState.Mountain)
            canBecomeVolcano = true;

        if (activityState == VolcanoActivityState.Erupting && eruptionTurnsRemaining <= 0)
            eruptionTurnsRemaining = Random.Range(minEruptionTurns, maxEruptionTurns + 1);
        else if (activityState != VolcanoActivityState.Erupting)
            eruptionTurnsRemaining = 0;

        RefreshVisuals();

        if (debugLogging) {}
            //Debug.Log($"[VolcanoTileState] Initialized {name} as {activityState} energy={energy01:0.00}");
    }

    public void AdvanceOneTurn(VolcanoManager manager)
    {
        if (manager == null)
            return;

        EnsureLinks();
        InitializeRuntimeFromInspector(force: false);

        stateTurns++;

        switch (activityState)
        {
            case VolcanoActivityState.Mountain:
                AdvanceMountain(manager);
                break;

            case VolcanoActivityState.Dormant:
                AdvanceDormant(manager);
                break;

            case VolcanoActivityState.Erupting:
                AdvanceErupting(manager);
                break;
        }

        SaveSystem.MarkSectionDirty(SaveSectionKeys.WorldObjects);
    }

    private void AdvanceMountain(VolcanoManager manager)
    {
        if (!canBecomeVolcano)
            return;

        seeded = true;
        energy01 = Mathf.Clamp01(energy01 + mountainEnergyGainPerTurn);

        if (energy01 < energyNeededToAwaken)
            return;

        if (!manager.TryConsumeMountainAwakeningBudget())
            return;

        BecomeDormant(manager, createdFromMountain: true);

        if (allowEruptSameTurnAsAwakening)
            TryStartEruptionIfReady(manager);
    }

    private void AdvanceDormant(VolcanoManager manager)
    {
        energy01 = Mathf.Clamp01(energy01 + dormantEnergyGainPerTurn);

        UpdateLowEnergyCounter();

        if (ShouldRevertToMountainFromLowEnergy())
        {
            RevertToMountain(manager);
            return;
        }

        TryStartEruptionIfReady(manager);
    }

    private void AdvanceErupting(VolcanoManager manager)
    {
        if (eruptionTurnsRemaining <= 0)
            eruptionTurnsRemaining = Random.Range(minEruptionTurns, maxEruptionTurns + 1);

        energy01 = Mathf.Clamp01(energy01 - eruptionEnergyLossPerTurn);
        eruptionTurnsRemaining = Mathf.Max(0, eruptionTurnsRemaining - 1);

        UpdateLowEnergyCounter();

        manager.NotifyEruptingVolcanoAdvanced(this);

        if (eruptionTurnsRemaining <= 0)
            EndEruption(manager);
    }

    private void TryStartEruptionIfReady(VolcanoManager manager)
    {
        if (activityState != VolcanoActivityState.Dormant)
            return;

        if (energy01 < energyNeededToErupt)
            return;

        if (!manager.TryConsumeEruptionBudget())
            return;

        BeginEruption(manager);
    }

    public void ForceSetMountain()
    {
        VolcanoManager manager = VolcanoManager.Instance;
        RevertToMountain(manager, notify: manager != null);
    }

    public void ForceSetDormant()
    {
        VolcanoManager manager = VolcanoManager.Instance;
        BecomeDormant(manager, createdFromMountain: activityState == VolcanoActivityState.Mountain);
    }

    public void ForceSetErupting()
    {
        VolcanoManager manager = VolcanoManager.Instance;

        if (activityState == VolcanoActivityState.Mountain)
            BecomeDormant(manager, createdFromMountain: true);

        BeginEruption(manager);
    }

    public void SetEnergy01(float value)
    {
        energy01 = Mathf.Clamp01(value);
    }

    public bool TryGetPrimaryCell(out TileCoord cell)
    {
        cell = default;

        EnsureLinks();

        GridManager gm = GridManager.Instance;

        TileControl tileControl = GetComponent<TileControl>();
        if (tileControl == null)
            tileControl = GetComponentInParent<TileControl>(true);
        if (tileControl == null)
            tileControl = GetComponentInChildren<TileControl>(true);

        if (tileControl != null)
        {
            Vector2Int pos = tileControl.GetGridPosition();

            if (IsGridPositionValid(pos, gm))
            {
                cell = new TileCoord(pos.x, pos.y);
                return true;
            }
        }

        if (gm != null)
        {
            Vector2Int pos = gm.GetGridPosition(transform.position);

            if (IsGridPositionValid(pos, gm))
            {
                cell = new TileCoord(pos.x, pos.y);
                return true;
            }

            if (environmentControl != null)
            {
                pos = gm.GetGridPosition(environmentControl.transform.position);

                if (IsGridPositionValid(pos, gm))
                {
                    cell = new TileCoord(pos.x, pos.y);
                    return true;
                }
            }

            if (tile != null)
            {
                pos = gm.GetGridPosition(tile.transform.position);

                if (IsGridPositionValid(pos, gm))
                {
                    cell = new TileCoord(pos.x, pos.y);
                    return true;
                }
            }
        }

        // Last fallback only.
        // This can be stale/default, so do not use it before world/grid lookups.
        if (environmentControl != null)
        {
            Vector2Int pos = environmentControl.gridPosition;

            if (IsGridPositionValid(pos, gm))
            {
                cell = new TileCoord(pos.x, pos.y);
                return true;
            }
        }

        return false;
    }

    private bool IsGridPositionValid(Vector2Int pos, GridManager gm)
    {
        if (gm == null)
            return true;

        return pos.x >= 0 &&
               pos.y >= 0 &&
               pos.x < gm.columns &&
               pos.y < gm.rows;
    }

    public bool TryGetPrimaryCell(out Vector2Int cell)
    {
        cell = default;

        if (TryGetPrimaryCell(out TileCoord coord))
        {
            cell = new Vector2Int(coord.x, coord.y);
            return true;
        }

        return false;
    }

    private void BecomeDormant(VolcanoManager manager, bool createdFromMountain)
    {
        VolcanoActivityState previous = activityState;

        seeded = true;
        canBecomeVolcano = true;
        activityState = VolcanoActivityState.Dormant;
        stateTurns = 0;
        eruptionTurnsRemaining = 0;
        lowEnergyTurns = 0;

        RefreshVisuals();

        if (manager != null)
        {
            if (createdFromMountain || previous == VolcanoActivityState.Mountain)
                manager.NotifyVolcanoCreated(this);

            manager.NotifyVolcanoBecameDormant(this);
        }

        if (debugLogging) {}
            //Debug.Log($"[VolcanoTileState] {name} became Dormant.");
    }

    private void BeginEruption(VolcanoManager manager)
    {
        seeded = true;
        canBecomeVolcano = true;
        activityState = VolcanoActivityState.Erupting;
        stateTurns = 0;
        lowEnergyTurns = 0;
        eruptionTurnsRemaining = Random.Range(minEruptionTurns, maxEruptionTurns + 1);

        RefreshVisuals();

        if (manager != null)
            manager.NotifyEruptionStarted(this);

        if (debugLogging) {}
            //Debug.Log($"[VolcanoTileState] {name} began Erupting for {eruptionTurnsRemaining} turns.");
    }

    private void EndEruption(VolcanoManager manager)
    {
        bool shouldRevert =
            allowMountainRevertAfterEruptionWhenLow &&
            ShouldRevertToMountainFromLowEnergy();

        if (shouldRevert)
        {
            RevertToMountain(manager);
            return;
        }

        activityState = VolcanoActivityState.Dormant;
        stateTurns = 0;
        eruptionTurnsRemaining = 0;
        lowEnergyTurns = 0;
        energy01 = Mathf.Clamp01(Mathf.Max(energy01, postEruptionEnergy));

        RefreshVisuals();

        if (manager != null)
        {
            manager.NotifyEruptionEnded(this);
            manager.NotifyVolcanoBecameDormant(this);
        }

        if (debugLogging) {}
            //Debug.Log($"[VolcanoTileState] {name} ended eruption and returned Dormant.");
    }

    private void RevertToMountain(VolcanoManager manager)
    {
        RevertToMountain(manager, notify: true);
    }

    private void RevertToMountain(VolcanoManager manager, bool notify)
    {
        activityState = VolcanoActivityState.Mountain;
        stateTurns = 0;
        eruptionTurnsRemaining = 0;
        lowEnergyTurns = 0;

        // Keep canBecomeVolcano true so it can build energy again later.
        seeded = true;

        RefreshVisuals();

        if (notify && manager != null)
            manager.NotifyVolcanoRevertedToMountain(this);

        if (debugLogging) {}
            //Debug.Log($"[VolcanoTileState] {name} reverted to Mountain.");
    }

    private void UpdateLowEnergyCounter()
    {
        if (lowEnergyTurnsBeforeMountainRevert <= 0)
        {
            lowEnergyTurns = 0;
            return;
        }

        if (energy01 <= lowEnergyThreshold)
            lowEnergyTurns++;
        else
            lowEnergyTurns = 0;
    }

    private bool ShouldRevertToMountainFromLowEnergy()
    {
        if (lowEnergyTurnsBeforeMountainRevert <= 0)
            return false;

        return lowEnergyTurns >= lowEnergyTurnsBeforeMountainRevert;
    }

    private void RefreshVisuals()
    {
        EnsureLinks();

        if (visuals != null)
            visuals.SetState(activityState);
    }

    private void EnsureLinks()
    {
        if (tile == null)
            tile = GetComponent<TileScript>();
        if (tile == null)
            tile = GetComponentInParent<TileScript>(true);

        if (environmentControl == null)
            environmentControl = GetComponent<EnvironmentControl>();
        if (environmentControl == null)
            environmentControl = GetComponentInParent<EnvironmentControl>(true);
        if (environmentControl == null)
            environmentControl = GetComponentInChildren<EnvironmentControl>(true);

        if (visuals == null)
            visuals = GetComponent<VolcanoEnvironmentVisuals>();
        if (visuals == null)
            visuals = GetComponentInChildren<VolcanoEnvironmentVisuals>(true);
        if (visuals == null)
            visuals = GetComponentInParent<VolcanoEnvironmentVisuals>(true);
    }

    public float GetSootEmissionThisTurn()
    {
        if (activityState != VolcanoActivityState.Erupting)
            return 0f;

        return Mathf.Max(0f, sootEmissionPerEruptionTurn);
    }

    public int GetSootStampRadius()
    {
        float amount = GetSootEmissionThisTurn();

        if (amount <= 0f)
            return 0;

        int minRadius = Mathf.Max(0, minSootStampRadius);
        int maxRadius = Mathf.Max(minRadius, maxSootStampRadius);

        if (maxRadius <= minRadius)
            return minRadius;

        float minAmount = Mathf.Max(0.01f, sootAmountForMinStampRadius);
        float maxAmount = Mathf.Max(minAmount + 0.01f, sootAmountForMaxStampRadius);

        float t = Mathf.InverseLerp(minAmount, maxAmount, amount);
        return Mathf.RoundToInt(Mathf.Lerp(minRadius, maxRadius, t));
    }

    public float GetMaxSootAddedPerCellThisTurn()
    {
        return Mathf.Clamp01(maxSootAddedPerCellPerTurn);
    }

    public float GetSootStampFalloffPerCell()
    {
        return Mathf.Clamp01(sootStampFalloffPerCell);
    }

    public int GetLavaCellsPerEruptionTurn()
    {
        if (activityState != VolcanoActivityState.Erupting)
            return 0;

        return Mathf.Max(0, lavaCellsPerEruptionTurn);
    }

    public int GetMaxLavaDistanceFromSource()
    {
        return Mathf.Max(0, maxLavaDistanceFromSource);
    }

    public float GetLavaHeatOnEmission()
    {
        return Mathf.Clamp01(lavaHeatOnEmission);
    }

    public int GetLavaCoolingDelayTurns()
    {
        return Mathf.Max(0, lavaCoolingDelayTurns);
    }

    public int GetLavaCoolingTurns()
    {
        return Mathf.Max(1, lavaCoolingTurns);
    }

    public VolcanoTileRuntimeSaveData CaptureRuntimeSaveData()
    {
        EnsureLinks();
        InitializeRuntimeFromInspector(force: false);

        VolcanoTileRuntimeSaveData data = new VolcanoTileRuntimeSaveData
        {
            activityStateValue = (int)activityState,

            seeded = seeded,
            canBecomeVolcano = canBecomeVolcano,
            runtimeInitialized = runtimeInitialized,

            energy01 = Mathf.Clamp01(energy01),

            stateTurns = stateTurns,
            eruptionTurnsRemaining = eruptionTurnsRemaining,
            lowEnergyTurns = lowEnergyTurns
        };

        if (TryGetPrimaryCell(out TileCoord cell))
        {
            data.hasPrimaryCell = true;
            data.primaryCellX = cell.x;
            data.primaryCellY = cell.y;
        }

        return data;
    }

    public void ApplyRuntimeSaveData(VolcanoTileRuntimeSaveData data)
    {
        if (data == null)
            return;

        EnsureLinks();

        runtimeInitialized = true;

        activityState = RestoreActivityState(data.activityStateValue);
        startState = activityState;

        seeded = data.seeded;
        canBecomeVolcano = data.canBecomeVolcano;

        energy01 = Mathf.Clamp01(data.energy01);

        stateTurns = Mathf.Max(0, data.stateTurns);
        eruptionTurnsRemaining = Mathf.Max(0, data.eruptionTurnsRemaining);
        lowEnergyTurns = Mathf.Max(0, data.lowEnergyTurns);

        // Saved non-mountain states must remain valid volcanoes.
        if (activityState != VolcanoActivityState.Mountain)
        {
            seeded = true;
            canBecomeVolcano = true;
        }

        // Saved erupting volcanoes must stay erupting.
        if (activityState == VolcanoActivityState.Erupting)
        {
            seeded = true;
            canBecomeVolcano = true;

            if (eruptionTurnsRemaining <= 0)
                eruptionTurnsRemaining = Mathf.Max(1, minEruptionTurns);
        }
        else
        {
            eruptionTurnsRemaining = 0;
        }

        if (activityState != VolcanoActivityState.Mountain)
        {
            seeded = true;
            canBecomeVolcano = true;
        }

        if (activityState == VolcanoActivityState.Erupting && eruptionTurnsRemaining <= 0)
            eruptionTurnsRemaining = Mathf.Max(1, minEruptionTurns);

        if (activityState != VolcanoActivityState.Erupting)
            eruptionTurnsRemaining = 0;

        RefreshVisuals();

        VolcanoManager manager = VolcanoManager.Instance;
        if (manager != null && autoRegisterWithManager)
        {
            if (ShouldStayRegistered)
                manager.RegisterVolcano(this);
            else
                manager.UnregisterVolcano(this);
        }

        if (debugLogging)
        {
            //Debug.Log(
                //$"[VolcanoTileState] Loaded state for {name}. " +
                //$"State={activityState}, Energy={energy01:0.00}, " +
                //$"EruptionTurnsRemaining={eruptionTurnsRemaining}");
        }
    }

    private VolcanoActivityState RestoreActivityState(int value)
    {
        if (value < (int)VolcanoActivityState.Mountain ||
            value > (int)VolcanoActivityState.Erupting)
        {
            return VolcanoActivityState.Mountain;
        }

        return (VolcanoActivityState)value;
    }

    [ContextMenu("Volcano/Set Mountain")]
    private void ContextSetMountain()
    {
        ForceSetMountain();
    }

    [ContextMenu("Volcano/Set Dormant")]
    private void ContextSetDormant()
    {
        ForceSetDormant();
    }

    [ContextMenu("Volcano/Set Erupting")]
    private void ContextSetErupting()
    {
        ForceSetErupting();
    }
}
