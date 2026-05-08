using System;
using System.Collections.Generic;
using UnityEngine;

public partial class KineticWarfareControl : MonoBehaviour
{
    [Serializable]
    public class TrainingOrder
    {
        public string orderID;   // unique id for this order (for save/debug)
        public MilitiaUnit unit;      // what to train
        public int multiplier = 1;   // how many batches
        public int totalTurns;       // total duration
        public int remainingTurns;   // countdown

        // Population reservation backing this order (if any)
        public string populationReservationId;
        public int reservedPopulation;

        // Absolute turn at which this order's people "age out" (Elder).
        // -1 means no expiry / not used.
        public int expiryTurn = -1;

        public int TotalUnits => unit != null ? unit.outputUnits * multiplier : 0;
        public int PopulationRequired => unit != null ? unit.populationToTrain * multiplier : 0;
        public bool HasExpiry => expiryTurn >= 0;
    }

    public struct TrainingCompletion
    {
        public KineticWarfareControl source;
        public string orderId;
        public MilitiaUnit unit;
        public int totalUnits;
        public TileUnitGroupControl tileGroupControl;

        public string populationReservationId;
        public int reservedPopulation;
        public int expiryTurn;

        public float startingHealthFraction;

        public int fatigueBonusPower;
        public int fatigueBonusDefense;
        public int fatigueBonusAgility;
        public int fatigueBonusAccuracy;
        public int fatigueBonusRange;
        public int fatigueBonusStealth;
        public float fatigueBonusMovementSpeed;
    }

    public event Action OnTrainingQueueChanged;

    [Header("Trainable Units")]
    [SerializeField] private List<MilitiaUnit> trainableUnits = new();

    [Header("Training Queue")]
    [Min(1)]
    public int maxTrainingSlots = 3;
    [SerializeField] private List<TrainingOrder> activeOrders = new();
    public IReadOnlyList<TrainingOrder> ActiveOrders => activeOrders;

    [Header("Ticking")]
    public bool tickedByManager = true;

    [Header("Order UI")]
    [Tooltip("Where to spawn the order widgets (e.g. in the Kinetic Warfare panel).")]
    public Transform ordersUIRoot;
    [Tooltip("Prefab that shows the countdown (same script as crafting order widget).")]
    public CraftOrderWidget orderWidgetPrefab;

    [Header("Skill Training Preview")]
    [Tooltip("Optional root to spawn unit-group skill training preview widgets. " +
             "If null, falls back to ordersUIRoot.")]
    public Transform skillTrainingPreviewRoot;

    private readonly Dictionary<string, CraftOrderWidget> _widgets = new();

    // Group skill training + advancement orders (logic lives in other partials)
    [SerializeField] private List<GroupSkillTrainingOrder> _groupTrainingOrders = new();
    private readonly Dictionary<string, CraftOrderWidget> _groupTrainingWidgets = new();

    private BuildingStatus _buildingStatus;

    private void Awake()
    {
        _buildingStatus = GetComponent<BuildingStatus>();
    }

    private void OnEnable()
    {
        if (!tickedByManager)
            TurnSystem.SubscribeToEndOfTurn(OnEndTurn_Internal);

        if (_buildingStatus != null)
            _buildingStatus.OnStateChanged += HandleBuildingStateChanged;
    }

    private void OnDisable()
    {
        if (!tickedByManager)
            TurnSystem.UnsubscribeFromEndOfTurn(OnEndTurn_Internal);

        if (_buildingStatus != null)
            _buildingStatus.OnStateChanged -= HandleBuildingStateChanged;
    }

    private void OnDestroy()
    {
        // release any pop tied to outstanding unit training orders
        ReleaseAllOrderPopulation();

        // Release any population used by group skill training if building is destroyed
        var popMgr = PlayersPopulationManager.Instance;
        if (popMgr != null)
        {
            for (int i = 0; i < _groupTrainingOrders.Count; i++)
            {
                var o = _groupTrainingOrders[i];
                if (o != null && !string.IsNullOrEmpty(o.populationReservationId))
                    popMgr.ReleaseReservation(o.populationReservationId);
            }
        }

        // Clean up widgets
        var keys = new List<string>(_widgets.Keys);
        foreach (var k in keys)
        {
            if (_widgets.TryGetValue(k, out var w) && w != null)
                Destroy(w.gameObject);
        }
        _widgets.Clear();

        var gKeys = new List<string>(_groupTrainingWidgets.Keys);
        foreach (var k in gKeys)
        {
            if (_groupTrainingWidgets.TryGetValue(k, out var w) && w != null)
                Destroy(w.gameObject);
        }
        _groupTrainingWidgets.Clear();

        activeOrders.Clear();
        _groupTrainingOrders.Clear();
        _temporarilyDisbandedGroups.Clear();
        _activeTornadoSourceIds.Clear();
        _trainingPauseReason = TrainingPauseReason.None;
    }

    private string GetTrainingReservationOwnerId()
    {
        Saveable saveable = GetComponent<Saveable>();
        if (saveable == null)
            saveable = GetComponentInParent<Saveable>();

        if (saveable != null && !string.IsNullOrWhiteSpace(saveable.uniqueID))
            return saveable.uniqueID;

        return gameObject.GetInstanceID().ToString();
    }

    private void TagTrainingReservation(string reservationId)
    {
        if (string.IsNullOrWhiteSpace(reservationId))
            return;

        PlayersPopulationManager.Instance?.UpdateReservationMetadata(
            reservationId,
            PopulationReservationKind.Training,
            GetTrainingReservationOwnerId(),
            nameof(KineticWarfareControl));
    }

    public void RetagTrainingReservationsFromRuntime()
    {
        for (int i = 0; i < activeOrders.Count; i++)
        {
            var order = activeOrders[i];
            if (order == null || string.IsNullOrWhiteSpace(order.populationReservationId))
                continue;

            TagTrainingReservation(order.populationReservationId);
        }
    }

    // -------------------------------------------------
    // Public API (base training)
    // -------------------------------------------------

    public IReadOnlyList<MilitiaUnit> GetAvailableTrainableUnits()
    {
        var result = new List<MilitiaUnit>();
        if (trainableUnits == null || trainableUnits.Count == 0) return result;

        var knownMgr = PlayerKnownUnitsManager.Instance;
        int playerLevel = PlayerLevel.Instance ? PlayerLevel.Instance.GetCurrentLevel() : int.MaxValue;

        foreach (var u in trainableUnits)
        {
            if (u == null) continue;

            // Player must know the unit
            if (knownMgr != null && !knownMgr.IsKnown(u)) continue;

            result.Add(u);
        }

        return result;
    }

    /// <summary>
    /// Returns true if there's at least one free slot across normal + group-skill/advancement orders.
    /// </summary>
    public bool HasFreeTrainingSlot()
    {
        return activeOrders.Count + _groupTrainingOrders.Count < maxTrainingSlots;
    }

    public bool TryStartTraining(MilitiaUnit unit, int multiplier, out string failReason)
    {
        failReason = string.Empty;

        if (_buildingStatus != null && _buildingStatus.CurrentState != BuildingState.Normal)
        {
            failReason = "Building is not operational.";
            return false;
        }

        if (unit == null)
        {
            failReason = "No unit selected.";
            return false;
        }

        if (!HasFreeTrainingSlot())
        {
            failReason = "All training slots are currently in use.";
            return false;
        }

        if (trainableUnits == null || !trainableUnits.Contains(unit))
        {
            failReason = "This building cannot train that unit type.";
            return false;
        }

        multiplier = Mathf.Max(1, multiplier);

        var popMgr = PlayersPopulationManager.Instance;
        if (popMgr == null)
        {
            failReason = "Population system not found.";
            return false;
        }

        int populationRequired = Mathf.Max(0, unit.populationToTrain * multiplier);
        string reservationId = null;
        int expiryTurn = -1;

        if (populationRequired > 0)
        {
            if (!popMgr.TryReservePopulation(
                    populationRequired,
                    PopulationReservationKind.Training,
                    GetTrainingReservationOwnerId(),
                    nameof(KineticWarfareControl),
                    out reservationId))
            {
                failReason = $"Not enough available population (needs {populationRequired}).";
                return false;
            }

            TagTrainingReservation(reservationId);

            // If this is a human unit, compute an expiry turn based on age → Elder.
            if (unit.isHuman && !string.IsNullOrEmpty(reservationId))
            {
                popMgr.TryComputeAndStoreReservationExpiryTurn(reservationId, out expiryTurn);
            }
        }

        int turns = Mathf.Max(1, unit.trainingTurns);

        var order = new TrainingOrder
        {
            orderID = Guid.NewGuid().ToString("N"),
            unit = unit,
            multiplier = multiplier,
            totalTurns = turns,
            remainingTurns = turns,
            populationReservationId = reservationId,
            reservedPopulation = populationRequired,
            expiryTurn = expiryTurn
        };

        activeOrders.Add(order);
        SpawnWidget(order);

        OnTrainingQueueChanged?.Invoke();

        //Debug.Log(
            //$"[KineticWarfare] Started training order {order.orderID}: " +
            //$"{order.TotalUnits} x {unit.unitName} (pop={populationRequired}, resId={reservationId}, expiry={expiryTurn})");

        return true;
    }

    // -------------------------------------------------
    // Manager-facing ticking
    // -------------------------------------------------

    public int AdvanceTurnAndCollectCompletions(List<TrainingCompletion> outList)
    {
        if (outList == null) return 0;
        if (_buildingStatus != null && _buildingStatus.CurrentState != BuildingState.Normal)
            return 0;
        if (IsPausedForTornadoImpact)
            return 0;
        if (IsPausedForFireImpact)
            return 0;

        int added = 0;
        bool anyRemoved = false;

        var tileGroupControl = GetComponentInParent<TileUnitGroupControl>();

        for (int i = activeOrders.Count - 1; i >= 0; i--)
        {
            var order = activeOrders[i];
            if (order == null)
            {
                activeOrders.RemoveAt(i);
                anyRemoved = true;
                continue;
            }

            if (order.unit == null)
            {
                // Unit no longer valid – free population and drop the order.
                ReleasePopulationForOrder(order);
                RemoveWidget(order.orderID);
                activeOrders.RemoveAt(i);
                anyRemoved = true;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(order.populationReservationId))
                TagTrainingReservation(order.populationReservationId);

            order.remainingTurns = Mathf.Max(0, order.remainingTurns - 1);
            UpdateWidget(order);

            if (order.remainingTurns <= 0)
            {
                int totalUnits = order.TotalUnits;

                activeOrders.RemoveAt(i);
                anyRemoved = true;

                if (totalUnits > 0)
                {
                    float healthFraction = trainingAppliesPostTrainingFatigue
    ? Mathf.Clamp01(trainedGroupStartingHealthFraction)
    : 1f;

                    outList.Add(new TrainingCompletion
                    {
                        source = this,
                        orderId = order.orderID,
                        unit = order.unit,
                        totalUnits = totalUnits,
                        tileGroupControl = tileGroupControl,
                        populationReservationId = order.populationReservationId,
                        reservedPopulation = order.reservedPopulation,
                        expiryTurn = order.expiryTurn,

                        startingHealthFraction = healthFraction,

                        fatigueBonusPower = trainingAppliesPostTrainingFatigue
                            ? ComputeTrainingStatDebuff(order.unit.power, trainingPowerDebuffFraction)
                            : 0,

                        fatigueBonusDefense = trainingAppliesPostTrainingFatigue
                            ? ComputeTrainingStatDebuff(order.unit.defense, trainingDefenseDebuffFraction)
                            : 0,

                        fatigueBonusAgility = trainingAppliesPostTrainingFatigue
                            ? ComputeTrainingStatDebuff(order.unit.agility, trainingAgilityDebuffFraction)
                            : 0,

                        fatigueBonusAccuracy = trainingAppliesPostTrainingFatigue
                            ? ComputeTrainingStatDebuff(order.unit.accuracy, trainingAccuracyDebuffFraction)
                            : 0,

                        fatigueBonusRange = trainingAppliesPostTrainingFatigue
                            ? ComputeTrainingStatDebuff(order.unit.range, trainingRangeDebuffFraction)
                            : 0,

                        fatigueBonusStealth = trainingAppliesPostTrainingFatigue
                            ? ComputeTrainingStatDebuff(order.unit.stealth, trainingStealthDebuffFraction)
                            : 0,

                        fatigueBonusMovementSpeed = trainingAppliesPostTrainingFatigue
                            ? ComputeTrainingMovementDebuff(order.unit.movementSpeed, trainingMovementDebuffFraction)
                            : 0f
                    });

                    // Do NOT release the population reservation here; the spawned
                    // unit group will own it and release it when disbanded/expired.
                    added++;
                }
                else
                {
                    //Debug.LogWarning("[KineticWarfare] Training order completed with zero units.");
                    // No group will be spawned; release the population now
                    ReleasePopulationForOrder(order);
                    RemoveWidget(order.orderID);
                }
            }
        }

        if (anyRemoved)
            OnTrainingQueueChanged?.Invoke();

        // Also tick any group-skill / advancement orders
        AdvanceGroupSkillTrainingOrders();

        return added;
    }

    /// <summary>
    /// Called by PlayerTrainingManager after it has spawned units.
    /// Cleans up the order UI/widget mapping on this building.
    /// </summary>
    public void OnOrderFinalizedExternally(string orderId)
    {
        if (string.IsNullOrEmpty(orderId)) return;
        RemoveWidget(orderId);
    }

    // -------------------------------------------------
    // Legacy self-tick (used only if tickedByManager == false)
    // -------------------------------------------------

    private void OnEndTurn_Internal()
    {
        var tmp = ListPool<TrainingCompletion>.Get();
        try
        {
            int count = AdvanceTurnAndCollectCompletions(tmp);

            for (int i = 0; i < count; i++)
            {
                var tc = tmp[i];

                bool spawned = false;

                if (tc.tileGroupControl != null && tc.unit != null && tc.totalUnits > 0)
                {
                    var group = tc.tileGroupControl.AddGroup(
                        tc.unit,
                        tc.totalUnits,
                        tc.populationReservationId,
                        tc.reservedPopulation,
                        tc.expiryTurn);

                    spawned = (group != null);
                }
                else
                {
                    //Debug.LogWarning("[KineticWarfare] TrainingCompletion missing data; skipping spawn.");
                }

                if (!spawned && !string.IsNullOrEmpty(tc.populationReservationId))
                {
                    PlayersPopulationManager.Instance?.ReleaseReservation(tc.populationReservationId);
                }

                if (tc.source != null)
                    tc.source.OnOrderFinalizedExternally(tc.orderId);
            }
        }
        finally
        {
            ListPool<TrainingCompletion>.Release(tmp);
        }
    }

    // -------------------------------------------------
    // Order widget UI (reuses CraftOrderWidget)
    // -------------------------------------------------

    private void SpawnWidget(TrainingOrder order)
    {
        if (!ordersUIRoot || !orderWidgetPrefab) return;

        var w = Instantiate(orderWidgetPrefab, ordersUIRoot);

        var icon = order.unit != null ? order.unit.unitIcon : null;
        w.Bind(order.orderID, order.totalTurns, icon);
        w.UpdateTurns(order.remainingTurns);

        _widgets[order.orderID] = w;
    }

    private void UpdateWidget(TrainingOrder order)
    {
        if (_widgets.TryGetValue(order.orderID, out var w) && w != null)
            w.UpdateTurns(order.remainingTurns);
    }

    private void RemoveWidget(string orderID)
    {
        if (string.IsNullOrEmpty(orderID)) return;

        if (_widgets.TryGetValue(orderID, out var w) && w != null)
            Destroy(w.gameObject);

        _widgets.Remove(orderID);
    }

    private void ReleasePopulationForOrder(TrainingOrder order)
    {
        if (order == null) return;
        if (string.IsNullOrEmpty(order.populationReservationId)) return;

        var popMgr = PlayersPopulationManager.Instance;
        if (popMgr != null)
        {
            popMgr.ReleaseReservation(order.populationReservationId);
            //Debug.Log($"[KineticWarfare] Released pop reservation {order.populationReservationId} (order {order.orderID}).");
        }

        order.populationReservationId = null;
        order.reservedPopulation = 0;
    }

    private void ReleaseAllOrderPopulation()
    {
        var popMgr = PlayersPopulationManager.Instance;
        if (popMgr == null) return;

        for (int i = 0; i < activeOrders.Count; i++)
        {
            var order = activeOrders[i];
            if (order == null) continue;
            if (string.IsNullOrEmpty(order.populationReservationId)) continue;

            popMgr.ReleaseReservation(order.populationReservationId);
            order.populationReservationId = null;
            order.reservedPopulation = 0;
        }
    }

    // -------------------------------------------------
    // Building state
    // -------------------------------------------------

    private void HandleBuildingStateChanged(BuildingState s)
    {
        switch (s)
        {
            case BuildingState.Normal:
                break;

            case BuildingState.Damaged:
                // (optional) pause new orders only; existing orders still tick.
                break;

            case BuildingState.Destroyed:
                AbortAllOrders("Building destroyed");
                break;
        }
    }

    public void AbortAllOrders(string reason = null)
    {
        ReleaseAllOrderPopulation();

        _activeTornadoSourceIds.Clear();
        _trainingPauseReason = TrainingPauseReason.None;

        activeOrders.Clear();

        var keys = new List<string>(_widgets.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            var k = keys[i];
            if (_widgets.TryGetValue(k, out var w) && w) Destroy(w.gameObject);
            _widgets.Remove(k);
        }

        if (!string.IsNullOrEmpty(reason))
            //Debug.Log($"[KineticWarfare] All training orders aborted: {reason}");
    }

    public List<ActiveTrainingOrderSaveData> CaptureActiveOrders(string buildingSaveableID)
    {
        List<ActiveTrainingOrderSaveData> result = new List<ActiveTrainingOrderSaveData>();

        for (int i = 0; i < activeOrders.Count; i++)
        {
            TrainingOrder order = activeOrders[i];
            if (order == null || order.unit == null || string.IsNullOrWhiteSpace(order.unit.unitID))
                continue;

            result.Add(new ActiveTrainingOrderSaveData
            {
                buildingSaveableID = buildingSaveableID,

                orderID = order.orderID,
                unitID = order.unit.unitID,
                multiplier = Mathf.Max(1, order.multiplier),
                totalTurns = Mathf.Max(1, order.totalTurns),
                remainingTurns = Mathf.Clamp(order.remainingTurns, 0, Mathf.Max(1, order.totalTurns)),

                populationReservationId = order.populationReservationId,
                reservedPopulation = Mathf.Max(0, order.reservedPopulation),
                expiryTurn = order.expiryTurn
            });
        }

        return result;
    }

    public void ClearTrainingOrdersForLoad()
    {
        ReleaseAllOrderPopulation();

        activeOrders.Clear();

        var keys = new List<string>(_widgets.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            string k = keys[i];
            if (_widgets.TryGetValue(k, out var w) && w != null)
                Destroy(w.gameObject);
            _widgets.Remove(k);
        }

        OnTrainingQueueChanged?.Invoke();
    }

    public void AddLoadedTrainingOrder(ActiveTrainingOrderSaveData saved, Func<string, MilitiaUnit> unitResolver)
    {
        if (saved == null || string.IsNullOrWhiteSpace(saved.unitID))
            return;

        MilitiaUnit unit = unitResolver != null ? unitResolver(saved.unitID) : null;
        if (unit == null)
        {
            //Debug.LogWarning($"[KineticWarfare] Could not resolve unit '{saved.unitID}' while loading training order '{saved.orderID}'.");
            return;
        }

        TrainingOrder order = new TrainingOrder
        {
            orderID = string.IsNullOrWhiteSpace(saved.orderID) ? Guid.NewGuid().ToString("N") : saved.orderID,
            unit = unit,
            multiplier = Mathf.Max(1, saved.multiplier),
            totalTurns = Mathf.Max(1, saved.totalTurns),
            remainingTurns = Mathf.Clamp(saved.remainingTurns, 0, Mathf.Max(1, saved.totalTurns)),
            populationReservationId = saved.populationReservationId,
            reservedPopulation = Mathf.Max(0, saved.reservedPopulation),
            expiryTurn = saved.expiryTurn
        };

        activeOrders.Add(order);

        if (!string.IsNullOrWhiteSpace(order.populationReservationId))
            TagTrainingReservation(order.populationReservationId);

        SpawnWidget(order);
        OnTrainingQueueChanged?.Invoke();
    }

    private int ComputeTrainingStatDebuff(int baseValue, float debuffFraction)
    {
        if (baseValue <= 0 || debuffFraction <= 0f)
            return 0;

        return -Mathf.Max(1, Mathf.RoundToInt(baseValue * debuffFraction));
    }

    private float ComputeTrainingMovementDebuff(float baseValue, float debuffFraction)
    {
        if (baseValue <= 0f || debuffFraction <= 0f)
            return 0f;

        return -Mathf.Max(0.05f, baseValue * debuffFraction);
    }
}
