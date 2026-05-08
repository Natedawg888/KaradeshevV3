using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public class CraftingBuildingControl : MonoBehaviour
{
    [Header("What this building can craft")]
    public List<string> allowedRecipeIDs = new();

    public bool autoBuildRecipeCacheOnStart = true;

    [Header("Order/Throughput")]
    [Min(1)] public int maxConcurrentOrders = 4;

    [Header("Ticking")]
    public bool tickedByManager = true;

    [Header("UI")]
    public Transform ordersUIRoot;
    public CraftOrderWidget orderWidgetPrefab;

    public event Action OnOrdersChanged;

    private BuildingStatus _status;
    private readonly List<ActiveOrder> _active = new();
    private readonly Dictionary<string, CraftOrderWidget> _widgets = new();
    private readonly Dictionary<string, CraftingRecipe> _allowedById = new(StringComparer.Ordinal);

    public bool HasActiveOrders => _active.Count > 0;
    public int ActiveOrderCount => _active.Count;

    [Header("Tornado Crafting Casualties")]
    public bool tornadoCanInterruptCrafting = true;

    [Range(0f, 1f)] public float tornadoTeenCrafterDeathChance = 0.08f;
    [Range(0f, 1f)] public float tornadoAdultCrafterDeathChance = 0.06f;

    [Tooltip("Extra multiplier applied to tornado crafter death chance.")]
    [Min(0f)] public float tornadoCrafterDeathChanceMultiplier = 1f;

    [Header("Fire Crafting Casualties")]
    public bool fireCanInterruptCrafting = true;

    [Range(0f, 1f)] public float fireTeenCrafterDeathChance = 0.05f;
    [Range(0f, 1f)] public float fireAdultCrafterDeathChance = 0.03f;

    [Tooltip("Extra multiplier applied to fire crafter death chance.")]
    [Min(0f)] public float fireCrafterDeathChanceMultiplier = 1f;

    [Header("Crafting Health Wear")]
    public bool craftingCanLowerWorkerHealth = true;

    [Range(0f, 1f)] public float craftingHealthLossMinPerTurn = 0.003f;
    [Range(0f, 1f)] public float craftingHealthLossMaxPerTurn = 0.010f;

    [Tooltip("Extra multiplier applied to crafting health wear.")]
    [Min(0f)] public float craftingHealthLossMultiplier = 1f;

    [Tooltip("Crafting wear lowers health, but does not kill.")]
    [Range(0f, 1f)] public float minimumHealthAfterCraftingWear = 0.05f;

    [Header("Crafting Output Wear")]
    public bool craftingHealthAffectsOutput = true;

    [Tooltip("No output penalty while average crafter health is at or above this.")]
    [Range(0f, 1f)] public float craftingOutputPenaltyStartsBelowHealth = 0.85f;

    [Tooltip("Lowest output multiplier allowed when crafters are badly worn down.")]
    [Range(0f, 1f)] public float craftingMinimumOutputMultiplier = 0.55f;

    [Tooltip("Extra multiplier on the size of the output penalty.")]
    [Min(0f)] public float craftingOutputPenaltyStrength = 1f;

    [Header("Environmental Disease Exposure")]
    public bool craftingWeatherDiseaseExposure = true;

    [Tooltip("Extra global multiplier for building weather disease chance. Keep below 1 because workers are partly covered.")]
    [Range(0f, 1f)]
    public float craftingWeatherDiseaseChanceMultiplier = 0.75f;

    [Tooltip("Extra global multiplier for building weather exposure strength.")]
    [Range(0f, 1f)]
    public float craftingWeatherDiseaseExposureMultiplier = 0.85f;

    [Tooltip("0 means let each EnvironmentalDiseaseRisk decide.")]
    [Min(0)]
    public int maxCraftingWeatherDiseaseTargetsPerTurn = 0;

    public bool debugCraftingWeatherDiseaseExposure = false;

    private BuildingDiseaseExposureSource _buildingDiseaseExposure;
    private readonly List<string> _tmpBuildingDiseaseWorkerIds = new();

    private readonly List<string> _tmpCraftingWeatherDiseaseIds = new();

    [Header("Disease Crafting Output")]
    public bool craftingDiseaseAffectsOutput = true;
    public bool debugDiseaseCraftingOutput = false;

    private class ActiveOrder
    {
        public string orderId;
        public CraftingRecipe recipe;
        public int multiplier;
        public int totalTurns;
        public int turnsLeft;
        public string reservationId;
        public List<ResourceAmount> payoutSnapshot;
    }

    public struct ActiveOrderView
    {
        public string orderId;
        public CraftingRecipe recipe;
        public Sprite icon;
        public int multiplier;
        public int totalTurns;
        public int turnsLeft;
    }

    public struct CraftingCompletion
    {
        public CraftingBuildingControl source;
        public string orderId;
        public string reservationId;
        public List<ResourceAmount> payout;
        public int xpAward;
        public float outputMultiplier;
    }

    public struct FireCraftingImpact
    {
        public int cancelledOrders;
        public int workersRolled;
        public int workersKilled;
    }

    private float GetFireCrafterDeathChance(AgeGroup ageGroup)
    {
        return ageGroup switch
        {
            AgeGroup.Teen => fireTeenCrafterDeathChance,
            AgeGroup.Adult => fireAdultCrafterDeathChance,
            _ => 0f
        };
    }

    private void OnEnable()
    {
        if (!tickedByManager)
            TurnSystem.SubscribeToEndOfTurn(OnEndTurn_Internal);
    }

    private void OnDisable()
    {
        if (!tickedByManager)
            TurnSystem.UnsubscribeFromEndOfTurn(OnEndTurn_Internal);
    }

    private void Start()
    {
        _status = GetComponent<BuildingStatus>();
        if (_status) _status.OnStateChanged += HandleBuildingStateChanged;

        _buildingDiseaseExposure = GetComponent<BuildingDiseaseExposureSource>();

        if (autoBuildRecipeCacheOnStart)
            RebuildAllowedCache();
    }

    private void OnDestroy()
    {
        if (_status) _status.OnStateChanged -= HandleBuildingStateChanged;

        for (int i = 0; i < _active.Count; i++)
        {
            var ar = _active[i];
            if (!string.IsNullOrEmpty(ar?.reservationId))
                PlayersPopulationManager.Instance?.ReleaseReservation(ar.reservationId);
        }
        _active.Clear();

        foreach (var w in _widgets.Values)
            if (w) Destroy(w.gameObject);
        _widgets.Clear();
    }

    public List<ActiveOrderView> GetActiveOrdersSnapshot()
    {
        var result = new List<ActiveOrderView>(_active.Count);

        for (int i = 0; i < _active.Count; i++)
        {
            var ar = _active[i];
            if (ar == null) continue;

            result.Add(new ActiveOrderView
            {
                orderId = ar.orderId,
                recipe = ar.recipe,
                icon = ar.recipe != null ? ar.recipe.craftingIcon : null,
                multiplier = ar.multiplier,
                totalTurns = ar.totalTurns,
                turnsLeft = ar.turnsLeft
            });
        }

        return result;
    }

    private void NotifyOrdersChanged()
    {
        OnOrdersChanged?.Invoke();
    }

    public void RebuildAllowedCache()
    {
        _allowedById.Clear();

        var mgr = CraftingRecipeManager.Instance;
        if (!mgr) return;

        for (int i = 0; i < allowedRecipeIDs.Count; i++)
        {
            var id = allowedRecipeIDs[i];
            if (string.IsNullOrWhiteSpace(id)) continue;
            var recipe = mgr.GetByID(id);
            if (recipe != null) _allowedById[id] = recipe;
        }
    }

    public IReadOnlyList<CraftingRecipe> GetAllowedRecipes()
        => _allowedById.Values.ToList();

    public bool IsRecipeAllowed(string craftingID)
        => !string.IsNullOrWhiteSpace(craftingID) && _allowedById.ContainsKey(craftingID);

    public bool StartCrafting(string craftingID, int multiplier = 1)
    {
        if (!IsRecipeAllowed(craftingID)) return false;
        return StartCrafting(_allowedById[craftingID], multiplier);
    }

    public bool StartCrafting(CraftingRecipe recipe, int multiplier = 1)
    {
        if (recipe == null) return false;
        if (_status != null && _status.CurrentState == BuildingState.Destroyed) return false;
        if (_active.Count >= Mathf.Max(1, maxConcurrentOrders)) return false;

        multiplier = recipe.ClampMultiplier(multiplier);
        var costs = recipe.GetCostsFor(multiplier);
        var output = recipe.GetRolledOutputFor(multiplier);

        var ppm = PlayersPopulationManager.Instance;
        string reservationId = null;

        int popRequired = recipe.GetPopulationRequiredFor(multiplier);

        if (popRequired > 0)
        {
            if (ppm == null) return false;
            if (!ppm.TryPickRandomNonBusyTaskIndividuals(popRequired, out var picked, out reservationId))
                return false;
        }

        if (!ResourceDeduction.Deduct(costs))
        {
            if (!string.IsNullOrEmpty(reservationId))
                ppm?.ReleaseReservation(reservationId);
            return false;
        }

        var ar = new ActiveOrder
        {
            orderId = Guid.NewGuid().ToString("N"),
            recipe = recipe,
            multiplier = multiplier,
            totalTurns = Mathf.Max(1, recipe.craftTurnsRequired),
            turnsLeft = Mathf.Max(1, recipe.craftTurnsRequired),
            reservationId = reservationId,
            payoutSnapshot = output
        };

        _active.Add(ar);
        SpawnWidget(ar);

        ppm?.ForceSyncUI();

        NotifyOrdersChanged();
        return true;
    }

    public int AdvanceTurnAndCollectCompletions(List<CraftingCompletion> outList)
    {
        if (outList == null) return 0;
        if (_status != null && _status.CurrentState == BuildingState.Destroyed) return 0;

        TryApplyCraftingHealthWear(debugLogging: false);
        TryApplyCraftingWeatherDiseaseExposure(debugLogging: false);
        TryApplyBuildingDiseaseToActiveCraftingWorkers();

        int added = 0;
        bool changed = false;

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var ar = _active[i];
            if (ar == null)
            {
                _active.RemoveAt(i);
                changed = true;
                continue;
            }

            ar.turnsLeft = Mathf.Max(0, ar.turnsLeft - 1);
            UpdateWidget(ar);
            changed = true;

            if (ar.turnsLeft <= 0)
            {
                _active.RemoveAt(i);

                int xpAward = (ar.recipe != null) ? ar.recipe.GetXPRewardFor(ar.multiplier) : 0;

                float outputMultiplier = GetCraftingOutputMultiplierForReservation(ar.reservationId);

                outList.Add(new CraftingCompletion
                {
                    source = this,
                    orderId = ar.orderId,
                    reservationId = ar.reservationId,
                    payout = ar.payoutSnapshot,
                    xpAward = xpAward,
                    outputMultiplier = outputMultiplier
                });

                added++;
            }
        }

        if (changed)
            NotifyOrdersChanged();

        return added;
    }

    public void OnOrderFinalizedExternally(string orderId)
    {
        if (string.IsNullOrEmpty(orderId)) return;

        if (_widgets.TryGetValue(orderId, out var w) && w)
            Destroy(w.gameObject);

        _widgets.Remove(orderId);
        NotifyOrdersChanged();
    }

    private void OnEndTurn_Internal()
    {
        var tmp = new List<CraftingCompletion>(4);
        AdvanceTurnAndCollectCompletions(tmp);

        var inv = PlayerInventoryManager.Instance;
        var pop = PlayersPopulationManager.Instance;
        var level = PlayerLevel.Instance;

        for (int i = 0; i < tmp.Count; i++)
        {
            var cc = tmp[i];

            if (inv != null && cc.payout != null)
            {
                for (int j = 0; j < cc.payout.Count; j++)
                {
                    var a = cc.payout[j];
                    if (a != null && a.resource != null && a.amount > 0)
                        inv.TryAdd(a.resource, a.amount);
                }
                inv.inventoryPanel?.Refresh();
            }

            if (cc.xpAward > 0)
                level?.AddXP(cc.xpAward);

            if (!string.IsNullOrEmpty(cc.reservationId))
                pop?.ReleaseReservation(cc.reservationId);

            OnOrderFinalizedExternally(cc.orderId);
        }
    }

    private void SpawnWidget(ActiveOrder ar)
    {
        if (!ordersUIRoot || !orderWidgetPrefab) return;

        var w = Instantiate(orderWidgetPrefab, ordersUIRoot);
        w.Bind(ar.orderId, ar.totalTurns, ar.recipe != null ? ar.recipe.craftingIcon : null);
        w.UpdateTurns(ar.turnsLeft);

        _widgets[ar.orderId] = w;
    }

    private void UpdateWidget(ActiveOrder ar)
    {
        if (_widgets.TryGetValue(ar.orderId, out var w) && w)
            w.UpdateTurns(ar.turnsLeft);
    }

    private void HandleBuildingStateChanged(BuildingState s)
    {
        switch (s)
        {
            case BuildingState.Normal:
                break;

            case BuildingState.Damaged:
                break;

            case BuildingState.Destroyed:
                AbortAllOrders("Building destroyed");
                break;
        }
    }

    public void AbortAllOrders(string reason = null)
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var ar = _active[i];
            if (!string.IsNullOrEmpty(ar?.reservationId))
                PlayersPopulationManager.Instance?.ReleaseReservation(ar.reservationId);
            _active.RemoveAt(i);
        }

        var keys = new List<string>(_widgets.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            var k = keys[i];
            if (_widgets.TryGetValue(k, out var w) && w) Destroy(w.gameObject);
            _widgets.Remove(k);
        }

        NotifyOrdersChanged();

        if (!string.IsNullOrEmpty(reason)) {}
            //Debug.Log($"[Crafting] All orders aborted: {reason}");
    }

    public List<ActiveCraftOrderSaveData> CaptureActiveOrders(string buildingSaveableID)
    {
        List<ActiveCraftOrderSaveData> result = new List<ActiveCraftOrderSaveData>();

        for (int i = 0; i < _active.Count; i++)
        {
            var ar = _active[i];
            if (ar == null || ar.recipe == null || string.IsNullOrWhiteSpace(ar.recipe.craftingID))
                continue;

            ActiveCraftOrderSaveData saved = new ActiveCraftOrderSaveData
            {
                buildingSaveableID = buildingSaveableID,
                orderId = ar.orderId,
                craftingID = ar.recipe.craftingID,
                multiplier = ar.multiplier,
                totalTurns = ar.totalTurns,
                turnsLeft = ar.turnsLeft,
                reservationId = ar.reservationId
            };

            if (ar.payoutSnapshot != null)
            {
                for (int j = 0; j < ar.payoutSnapshot.Count; j++)
                {
                    var payout = ar.payoutSnapshot[j];
                    if (payout == null || payout.resource == null || payout.amount <= 0)
                        continue;

                    saved.payout.Add(new CraftingPayoutSaveData
                    {
                        resourceID = payout.resource.resourceID,
                        amount = payout.amount
                    });
                }
            }

            result.Add(saved);
        }

        return result;
    }

    public void ClearOrdersForLoad()
    {
        _active.Clear();

        foreach (var w in _widgets.Values)
            if (w) Destroy(w.gameObject);

        _widgets.Clear();
        NotifyOrdersChanged();
    }

    public void AddLoadedOrder(
        ActiveCraftOrderSaveData saved,
        Func<string, CraftingRecipe> recipeResolver,
        Func<string, ResourceDefinition> resourceResolver)
    {
        if (saved == null || string.IsNullOrWhiteSpace(saved.craftingID))
            return;

        CraftingRecipe recipe = recipeResolver != null ? recipeResolver(saved.craftingID) : null;
        if (recipe == null)
        {
            //Debug.LogWarning($"[CraftingBuildingControl] Could not resolve recipe '{saved.craftingID}' while loading.");
            return;
        }

        List<ResourceAmount> payoutSnapshot = new List<ResourceAmount>();

        if (saved.payout != null)
        {
            for (int i = 0; i < saved.payout.Count; i++)
            {
                CraftingPayoutSaveData p = saved.payout[i];
                if (p == null || string.IsNullOrWhiteSpace(p.resourceID) || p.amount <= 0)
                    continue;

                ResourceDefinition def = resourceResolver != null ? resourceResolver(p.resourceID) : null;
                if (def == null)
                    continue;

                payoutSnapshot.Add(new ResourceAmount
                {
                    resource = def,
                    amount = p.amount
                });
            }
        }

        var ar = new ActiveOrder
        {
            orderId = string.IsNullOrWhiteSpace(saved.orderId) ? Guid.NewGuid().ToString("N") : saved.orderId,
            recipe = recipe,
            multiplier = Mathf.Max(1, saved.multiplier),
            totalTurns = Mathf.Max(1, saved.totalTurns),
            turnsLeft = Mathf.Clamp(saved.turnsLeft, 0, Mathf.Max(1, saved.totalTurns)),
            reservationId = saved.reservationId,
            payoutSnapshot = payoutSnapshot
        };

        _active.Add(ar);
        SpawnWidget(ar);
        NotifyOrdersChanged();
    }

    public struct TornadoCraftingImpact
    {
        public int cancelledOrders;
        public int workersRolled;
        public int workersKilled;
    }

    private float GetTornadoCrafterDeathChance(AgeGroup ageGroup)
    {
        return ageGroup switch
        {
            AgeGroup.Teen => tornadoTeenCrafterDeathChance,
            AgeGroup.Adult => tornadoAdultCrafterDeathChance,
            _ => 0f
        };
    }

    private Individual FindIndividualById(string individualId)
    {
        if (string.IsNullOrWhiteSpace(individualId))
            return null;

        var sim = PlayerFamilySimulationManager.Instance;
        if (sim == null)
            return null;

        var people = sim.GetIndividuals();
        if (people == null)
            return null;

        for (int i = 0; i < people.Count; i++)
        {
            var person = people[i];
            if (person != null && person.Id == individualId)
                return person;
        }

        return null;
    }

    private bool AbortOrderByIdInternal(string orderId, bool releaseReservation)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            return false;

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var ar = _active[i];
            if (ar == null)
            {
                _active.RemoveAt(i);
                continue;
            }

            if (!string.Equals(ar.orderId, orderId, StringComparison.Ordinal))
                continue;

            if (releaseReservation && !string.IsNullOrEmpty(ar.reservationId))
                PlayersPopulationManager.Instance?.ReleaseReservation(ar.reservationId);

            _active.RemoveAt(i);

            if (_widgets.TryGetValue(orderId, out var w) && w)
                Destroy(w.gameObject);

            _widgets.Remove(orderId);
            return true;
        }

        return false;
    }

    public TornadoCraftingImpact TryApplyTornadoCraftingImpact(float externalChanceMultiplier = 1f, bool debugLogging = false)
    {
        TornadoCraftingImpact result = default;

        if (!tornadoCanInterruptCrafting)
            return result;

        if (_active == null || _active.Count == 0)
            return result;

        var pop = PlayersPopulationManager.Instance;
        var familySim = PlayerFamilySimulationManager.Instance;

        if (pop == null)
            return result;

        HashSet<string> killIds = new HashSet<string>(StringComparer.Ordinal);
        List<string> orderIdsToCancel = new List<string>();

        for (int i = 0; i < _active.Count; i++)
        {
            var ar = _active[i];
            if (ar == null)
                continue;

            // Only crafting orders that are actually using population
            if (string.IsNullOrWhiteSpace(ar.reservationId))
                continue;

            orderIdsToCancel.Add(ar.orderId);

            if (familySim == null)
                continue;

            if (!pop.TryGetReservedIndividualIds(ar.reservationId, out var reservedIds) || reservedIds == null)
                continue;

            for (int j = 0; j < reservedIds.Count; j++)
            {
                string individualId = reservedIds[j];
                Individual person = FindIndividualById(individualId);

                if (person == null || !person.IsAlive)
                    continue;

                result.workersRolled++;

                float chance = GetTornadoCrafterDeathChance(person.AggregatedAgeGroup);
                if (chance <= 0f)
                    continue;

                chance *= tornadoCrafterDeathChanceMultiplier;
                chance *= Mathf.Max(0f, externalChanceMultiplier);
                chance = Mathf.Clamp01(chance);

                if (UnityEngine.Random.value <= chance)
                    killIds.Add(person.Id);
            }
        }

        for (int i = 0; i < orderIdsToCancel.Count; i++)
        {
            if (AbortOrderByIdInternal(orderIdsToCancel[i], releaseReservation: true))
                result.cancelledOrders++;
        }

        if (result.cancelledOrders > 0)
            NotifyOrdersChanged();

        if (killIds.Count > 0 && familySim != null)
            familySim.TryKillIndividualsById(killIds, out result.workersKilled);

        if (debugLogging && (result.cancelledOrders > 0 || result.workersKilled > 0))
        {
            //Debug.Log(
                //$"[CraftingBuildingControl] Tornado interrupted crafting at '{name}' | " +
                //$"CancelledOrders={result.cancelledOrders} | " +
                //$"WorkersRolled={result.workersRolled} | " +
                //$"WorkersKilled={result.workersKilled}"
            //);
        }

        return result;
    }

    public FireCraftingImpact TryApplyFireCraftingImpact(float externalChanceMultiplier = 1f, bool debugLogging = false)
    {
        FireCraftingImpact result = default;

        if (!fireCanInterruptCrafting)
            return result;

        if (_active == null || _active.Count == 0)
            return result;

        var pop = PlayersPopulationManager.Instance;
        var familySim = PlayerFamilySimulationManager.Instance;

        if (pop == null)
            return result;

        HashSet<string> killIds = new HashSet<string>(StringComparer.Ordinal);
        List<string> orderIdsToCancel = new List<string>();

        for (int i = 0; i < _active.Count; i++)
        {
            var ar = _active[i];
            if (ar == null)
                continue;

            // Only crafting orders that are actually using population
            if (string.IsNullOrWhiteSpace(ar.reservationId))
                continue;

            orderIdsToCancel.Add(ar.orderId);

            if (familySim == null)
                continue;

            if (!pop.TryGetReservedIndividualIds(ar.reservationId, out var reservedIds) || reservedIds == null)
                continue;

            for (int j = 0; j < reservedIds.Count; j++)
            {
                string individualId = reservedIds[j];
                Individual person = FindIndividualById(individualId);

                if (person == null || !person.IsAlive)
                    continue;

                result.workersRolled++;

                float chance = GetFireCrafterDeathChance(person.AggregatedAgeGroup);
                if (chance <= 0f)
                    continue;

                chance *= fireCrafterDeathChanceMultiplier;
                chance *= Mathf.Max(0f, externalChanceMultiplier);
                chance = Mathf.Clamp01(chance);

                if (UnityEngine.Random.value <= chance)
                    killIds.Add(person.Id);
            }
        }

        for (int i = 0; i < orderIdsToCancel.Count; i++)
        {
            if (AbortOrderByIdInternal(orderIdsToCancel[i], releaseReservation: true))
                result.cancelledOrders++;
        }

        if (result.cancelledOrders > 0)
            NotifyOrdersChanged();

        if (killIds.Count > 0 && familySim != null)
            familySim.TryKillIndividualsById(killIds, out result.workersKilled);

        if (debugLogging && (result.cancelledOrders > 0 || result.workersKilled > 0))
        {
            //Debug.Log(
                //$"[CraftingBuildingControl] Fire interrupted crafting at '{name}' | " +
                //$"CancelledOrders={result.cancelledOrders} | " +
                //$"WorkersRolled={result.workersRolled} | " +
                //$"WorkersKilled={result.workersKilled}"
            //);
        }

        return result;
    }

    private float GetCrafterAgeResistance01(Individual person)
    {
        if (person == null)
            return 0f;

        if (PlayerHealthRulebook.Instance != null)
            return Mathf.Clamp01(PlayerHealthRulebook.Instance.GetResistance(person.AggregatedAgeGroup));

        if (GeneralPopulationManager.Instance != null)
            return Mathf.Clamp01(GeneralPopulationManager.Instance.GetResistance(person.AggregatedAgeGroup));

        return 0f;
    }

    public int TryApplyCraftingHealthWear(float externalMultiplier = 1f, bool debugLogging = false)
    {
        if (!craftingCanLowerWorkerHealth)
            return 0;

        if (_active == null || _active.Count == 0)
            return 0;

        var pop = PlayersPopulationManager.Instance;
        var familySim = PlayerFamilySimulationManager.Instance;

        if (pop == null || familySim == null)
            return 0;

        HashSet<string> processedIds = new HashSet<string>(StringComparer.Ordinal);

        int affectedWorkers = 0;
        float totalHealthLoss = 0f;

        for (int i = 0; i < _active.Count; i++)
        {
            var ar = _active[i];
            if (ar == null)
                continue;

            if (string.IsNullOrWhiteSpace(ar.reservationId))
                continue;

            if (!pop.TryGetReservedIndividualIds(ar.reservationId, out var reservedIds) || reservedIds == null)
                continue;

            for (int j = 0; j < reservedIds.Count; j++)
            {
                string individualId = reservedIds[j];
                if (string.IsNullOrWhiteSpace(individualId))
                    continue;

                if (!processedIds.Add(individualId))
                    continue;

                Individual person = FindIndividualById(individualId);
                if (person == null || !person.IsAlive)
                    continue;

                float loss = UnityEngine.Random.Range(craftingHealthLossMinPerTurn, craftingHealthLossMaxPerTurn);
                loss *= Mathf.Max(0f, craftingHealthLossMultiplier);
                loss *= Mathf.Max(0f, externalMultiplier);

                float ageResistance01 = GetCrafterAgeResistance01(person);
                loss *= (1f - ageResistance01);

                loss = Mathf.Max(0f, loss);
                if (loss <= 0f)
                    continue;

                float oldHealth = person.Health01;
                float newHealth = Mathf.Clamp(person.Health01 - loss, minimumHealthAfterCraftingWear, 1f);

                if (newHealth >= oldHealth - 0.0001f)
                    continue;

                person.Health01 = newHealth;
                affectedWorkers++;
                totalHealthLoss += (oldHealth - newHealth);

                if (debugLogging)
                {
                    //Debug.Log(
                        //$"[CraftingBuildingControl] Crafter wear | Building='{name}' | " +
                        //$"Person={person.Id} | Age={person.AggregatedAgeGroup} | " +
                        //$"Resistance01={ageResistance01:F2} | " +
                        //$"Health {oldHealth:F3}->{newHealth:F3}");
                }
            }
        }

        if (affectedWorkers > 0)
        {
            pop.MarkUIDirty();
            SaveSystem.MarkSectionDirty(SaveSectionKeys.Population);
        }

        if (debugLogging && affectedWorkers > 0)
        {
            //Debug.Log(
                //$"[CraftingBuildingControl] Crafting wear at '{name}' | " +
                //$"AffectedWorkers={affectedWorkers} | " +
                //$"TotalHealthLoss={totalHealthLoss:F3}");
        }

        return affectedWorkers;
    }

    private float GetCraftingOutputMultiplierForReservation(string reservationId)
    {
        if (string.IsNullOrWhiteSpace(reservationId))
            return 1f;

        var pop = PlayersPopulationManager.Instance;
        if (pop == null)
            return 1f;

        if (!pop.TryGetReservedIndividualIds(reservationId, out var reservedIds) ||
            reservedIds == null ||
            reservedIds.Count == 0)
        {
            return 1f;
        }

        float healthMultiplier = 1f;

        if (craftingHealthAffectsOutput)
            healthMultiplier = GetHealthCraftingOutputMultiplierForReservedIds(reservedIds);

        float diseaseMultiplier = 1f;

        if (craftingDiseaseAffectsOutput && DiseaseManager.Instance != null)
        {
            diseaseMultiplier = DiseaseManager.Instance.GetWorkEfficiencyMultiplierForIndividuals(
                reservedIds,
                "Crafting",
                name);
        }

        float finalMultiplier = Mathf.Clamp01(healthMultiplier * diseaseMultiplier);

        if (debugDiseaseCraftingOutput && diseaseMultiplier < 0.999f)
        {
            //Debug.Log(
                //$"[CraftingBuildingControl] Disease lowered crafting output. " +
                //$"Building={name}, " +
                //$"HealthMultiplier={healthMultiplier:F3}, " +
                //$"DiseaseMultiplier={diseaseMultiplier:F3}, " +
                //$"FinalMultiplier={finalMultiplier:F3}");
        }

        return Mathf.Clamp(finalMultiplier, craftingMinimumOutputMultiplier, 1f);
    }

    private float GetHealthCraftingOutputMultiplierForReservedIds(IReadOnlyList<string> reservedIds)
    {
        if (reservedIds == null || reservedIds.Count == 0)
            return 1f;

        float totalHealth = 0f;
        int liveCount = 0;

        for (int i = 0; i < reservedIds.Count; i++)
        {
            string individualId = reservedIds[i];

            if (string.IsNullOrWhiteSpace(individualId))
                continue;

            Individual person = FindIndividualById(individualId);

            if (person == null || !person.IsAlive)
                continue;

            totalHealth += Mathf.Clamp01(person.Health01);
            liveCount++;
        }

        if (liveCount <= 0)
            return 1f;

        float averageHealth = totalHealth / liveCount;

        if (averageHealth >= craftingOutputPenaltyStartsBelowHealth)
            return 1f;

        float healthT = Mathf.InverseLerp(
            minimumHealthAfterCraftingWear,
            craftingOutputPenaltyStartsBelowHealth,
            averageHealth);

        float multiplier = Mathf.Lerp(
            craftingMinimumOutputMultiplier,
            1f,
            healthT);

        multiplier = Mathf.Lerp(
            1f,
            multiplier,
            Mathf.Max(0f, craftingOutputPenaltyStrength));

        return Mathf.Clamp(multiplier, craftingMinimumOutputMultiplier, 1f);
    }

    private int TryApplyCraftingWeatherDiseaseExposure(bool debugLogging = false)
    {
        if (!craftingWeatherDiseaseExposure)
            return 0;

        if (_status != null && _status.CurrentState == BuildingState.Destroyed)
            return 0;

        if (_active == null || _active.Count == 0)
            return 0;

        if (DiseaseManager.Instance == null)
            return 0;

        PlayersPopulationManager pop = PlayersPopulationManager.Instance;
        if (pop == null)
            return 0;

        _tmpCraftingWeatherDiseaseIds.Clear();

        for (int i = 0; i < _active.Count; i++)
        {
            ActiveOrder order = _active[i];

            if (order == null)
                continue;

            if (string.IsNullOrWhiteSpace(order.reservationId))
                continue;

            if (!pop.TryGetReservedIndividualIds(order.reservationId, out var reservedIds) ||
                reservedIds == null ||
                reservedIds.Count == 0)
            {
                continue;
            }

            for (int j = 0; j < reservedIds.Count; j++)
            {
                string id = reservedIds[j];

                if (string.IsNullOrWhiteSpace(id))
                    continue;

                if (_tmpCraftingWeatherDiseaseIds.Contains(id))
                    continue;

                _tmpCraftingWeatherDiseaseIds.Add(id);
            }
        }

        if (_tmpCraftingWeatherDiseaseIds.Count == 0)
            return 0;

        int infections = DiseaseManager.Instance.TryApplyEnvironmentalDiseaseRiskForBuildingComponent(
            this,
            _tmpCraftingWeatherDiseaseIds,
            DiseaseTaskResultType.CraftingBuildingWeatherExposure,
            craftingWeatherDiseaseChanceMultiplier,
            craftingWeatherDiseaseExposureMultiplier,
            maxCraftingWeatherDiseaseTargetsPerTurn);

        DiseaseManager.Instance?.TrySpreadContagiousVirusesWithinGroup(
            _tmpCraftingWeatherDiseaseIds,
            "Crafting",
            name,
            1f);

        if (debugLogging || debugCraftingWeatherDiseaseExposure)
        {
            if (infections > 0)
            {
                //Debug.Log(
                    //$"[CraftingBuildingControl] Weather disease exposure at '{name}'. " +
                    //$"Workers={_tmpCraftingWeatherDiseaseIds.Count}, Infections={infections}");
            }
        }

        return infections;
    }

    private int TryApplyBuildingDiseaseToActiveCraftingWorkers()
    {
        if (_buildingDiseaseExposure == null)
            return 0;

        if (_active == null || _active.Count == 0)
            return 0;

        PlayersPopulationManager pop = PlayersPopulationManager.Instance;
        if (pop == null)
            return 0;

        _tmpBuildingDiseaseWorkerIds.Clear();

        for (int i = 0; i < _active.Count; i++)
        {
            ActiveOrder order = _active[i];

            if (order == null)
                continue;

            if (string.IsNullOrWhiteSpace(order.reservationId))
                continue;

            if (!pop.TryGetReservedIndividualIds(order.reservationId, out var reservedIds) ||
                reservedIds == null)
            {
                continue;
            }

            for (int j = 0; j < reservedIds.Count; j++)
            {
                string id = reservedIds[j];

                if (string.IsNullOrWhiteSpace(id))
                    continue;

                if (_tmpBuildingDiseaseWorkerIds.Contains(id))
                    continue;

                _tmpBuildingDiseaseWorkerIds.Add(id);
            }
        }

        if (_tmpBuildingDiseaseWorkerIds.Count == 0)
            return 0;

        return _buildingDiseaseExposure.TryApplyToActiveCraftingWorkers(
            _tmpBuildingDiseaseWorkerIds,
            BuildingDiseaseTriggerTiming.EveryTurn,
            name);
    }
}
