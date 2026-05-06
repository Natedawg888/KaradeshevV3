using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class PlayerInventoryManager : MonoBehaviour
{
    public static PlayerInventoryManager Instance { get; private set; }

    [Header("Capacities (space units)")]
    [Tooltip("Max space for Materials (sum of amount * weightPerUnit * sizePerUnit).")]
    public float maxMaterialsSpace = 200f;

    [Tooltip("Max space for Food (same formula).")]
    public float maxFoodSpace = 150f;

    [Tooltip("Max space for Water (same formula).")]
    public float maxWaterSpace = 150f;

    [System.Serializable]
    public struct StarterResourceEntry
    {
        public ResourceDefinition def;
        public int amount;
    }

    [Header("Starting Resources")]
    public bool grantStartingResourcesOnStart = true;

    [Tooltip("If true, only grant if inventory is empty (prevents re-granting when reloading a scene).")]
    public bool grantOnlyIfInventoryEmpty = true;

    public List<StarterResourceEntry> startingResources = new();

    private bool _starterGranted = false;

    [Header("UI")]
    public InventoryPanelControl inventoryPanel;

    [Header("Spoiled Food Resource")]
    [Tooltip("Assign directly in the Inspector so startup does not need a Resources scan.")]
    [SerializeField] private ResourceDefinition spoiledFoodDefinition;

    [Tooltip("Fallback ID only used if spoiledFoodDefinition is not assigned.")]
    public string spoiledFoodResourceId = "spd";

    [Header("Consumed Resource Health / Recovery")]
    public bool enableConsumedResourceHealthEffects = true;
    public bool enableConsumedResourceDiseaseRecoveryEffects = true;
    public bool debugConsumedResourceRecoveryEffects = false;

    // Cached spoiled definition
    private bool _turnSubscribed = false;

    [Header("Food Variety Tracking")]
    public bool trackFoodVarietyForHappiness = true;

    private readonly HashSet<string> _foodIdsConsumedThisTurn = new();
    private int _foodUnitsConsumedThisTurn = 0;
    private float _nutritionPointsConsumedThisTurn = 0f;

    private int _spoiledFoodUnitsConsumedThisTurn = 0;
    private float _spoiledNutritionPointsConsumedThisTurn = 0f;

    private float _nonSpoiledGradeWeightedSumThisTurn = 0f;   // Σ(grade * nutritionPoints)
    private float _nonSpoiledNutritionPointsThisTurn = 0f;    // Σ(nutritionPoints)
    private int _maxNonSpoiledGradeConsumedThisTurn = 0;

    [Header("Debug")]
    public bool enableConsumeDebug = false; 

    // Internal stacks
    private readonly List<InventoryStack> _materials = new();
    private readonly List<InventoryStack> _food = new();
    private readonly List<InventoryStack> _water = new();

    // Cached spoiled definition
    private ResourceDefinition _spoiledDef;
    private static Dictionary<string, ResourceDefinition> _resourceDefinitionCache;
    private static bool _resourceDefinitionCacheBuilt = false;
    private readonly Dictionary<ResourceDefinition, int> _spoiledAmountsThisTurn = new();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        CacheBaseCapacities();

        // Do NOT scan Resources here anymore.
        _spoiledDef = spoiledFoodDefinition;
    }

    private void OnEnable()
    {
        if (!_turnSubscribed)
        {
            TurnSystem.SubscribeToEndOfTurn(OnTurnEnded);
            _turnSubscribed = true;
        }
    }

    private void Start()
    {
        TryGrantStarterResources();
    }

    private void OnDisable()
    {
        if (_turnSubscribed)
        {
            TurnSystem.UnsubscribeFromEndOfTurn(OnTurnEnded);
            _turnSubscribed = false;
        }
    }

    private void OnDestroy()
    {
        if (_turnSubscribed)
        {
            TurnSystem.UnsubscribeFromEndOfTurn(OnTurnEnded);
            _turnSubscribed = false;
        }
    }

    private void MarkKnowledgeDirty()
    {
        SaveSystem.MarkSectionDirty(SaveSectionKeys.Knowledge);
    }

    #region Public API

    private void TryGrantStarterResources()
    {
        if (!grantStartingResourcesOnStart) return;
        if (_starterGranted) return;

        if (grantOnlyIfInventoryEmpty && !IsInventoryEmpty())
            return;

        if (startingResources == null || startingResources.Count == 0)
            return;

        int addedCount = 0;

        foreach (var e in startingResources)
        {
            if (e.def == null || e.amount <= 0) continue;

            // Don’t allow “group defs” as starter items
            if (e.def.isGroup) continue;

            bool ok = TryAdd(e.def, e.amount); // uses your existing capacity + merge logic :contentReference[oaicite:3]{index=3}
            if (!ok)
                Debug.LogWarning($"[INV] Starter add failed (capacity?) {e.amount}x {e.def.resourceName} ({e.def.resourceID})");
            else
                addedCount++;
        }

        if (addedCount > 0)
            inventoryPanel?.Refresh();

        _starterGranted = true;
    }

    private bool IsInventoryEmpty()
    {
        // internal lists already exist in this class :contentReference[oaicite:4]{index=4}
        return (_materials.Count == 0 && _food.Count == 0 && _water.Count == 0);
    }

    public bool TryAdd(ResourceDefinition def, int amount)
    {
        if (def == null || amount <= 0) return false;

        var targetList = GetListFor(def.resourceType);
        if (targetList == null) return false;

        // capacity check
        float incomingSpace = SpaceFor(def, amount);
        if (!HasSpaceFor(def, incomingSpace)) return false;

        // merge with existing stack (by resourceID)
        var stack = targetList.FirstOrDefault(s => s.definition.resourceID == def.resourceID);
        if (stack == null)
        {
            stack = new InventoryStack(def, amount);
            // initialize spoilage timer (nonPerishable => -1)
            if (def.nonPerishable || def.spoilageInterval <= 0) stack.remainingSpoilageTurns = def.nonPerishable ? -1 : def.spoilageInterval;
            else stack.remainingSpoilageTurns = def.spoilageInterval;
            targetList.Add(stack);
        }
        else
        {
            // Weighted average remaining spoilage turns (only if it spoils and stack tracks it)
            if (!def.nonPerishable)
            {
                if (stack.remainingSpoilageTurns < 0)
                    stack.remainingSpoilageTurns = def.spoilageInterval; // was non-perishable marker inconsistently? normalize

                int a = Mathf.Max(0, stack.amount);
                int b = Mathf.Max(0, amount);

                // if the def.spoilageInterval <= 0 (meaning spoil every turn by rate),
                // use "1 turn" notion for averaging; it’ll tick down anyway.
                int incTurns = def.spoilageInterval > 0 ? def.spoilageInterval : 1;

                // weighted average
                stack.remainingSpoilageTurns = Mathf.RoundToInt(((a * stack.remainingSpoilageTurns) + (b * incTurns)) / Mathf.Max(1, (a + b)));
            }

            stack.amount += amount;
        }

        MarkKnowledgeDirty();

        return true;
    }

    public bool TryRemove(ResourceDefinition def, int removeAmount)
    {
        if (def == null || removeAmount <= 0) return false;

        // Group resource (like GFD) => remove from all stacks of this resource type
        if (def.isGroup)
        {
            return TryRemoveGroup(def, removeAmount);
        }

        // Normal single resource
        var targetList = GetListFor(def.resourceType);
        var stack = targetList?.FirstOrDefault(s => s.definition.resourceID == def.resourceID);
        if (stack == null) return false;

        int actual = Mathf.Min(removeAmount, stack.amount);
        stack.amount -= actual;

        if (stack.amount <= 0)
            targetList.Remove(stack);

        MarkKnowledgeDirty();

        return actual > 0;
    }

    public bool TryRemoveHalf(ResourceDefinition def)
        => TryRemove(def, Mathf.CeilToInt(GetAmount(def) / 2f));

    public bool TryRemoveAll(ResourceDefinition def)
        => TryRemove(def, GetAmount(def));

    public int GetAmount(ResourceDefinition def)
    {
        if (def == null) return 0;

        // Group resource (e.g. GFD = "all Food")
        if (def.isGroup)
        {
            var list = GetListFor(def.groupType);
            if (list == null) return 0;

            int total = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                if (s?.definition == null) continue;
                if (s.definition.resourceType != def.groupType) continue;

                total += Mathf.Max(0, s.amount);
            }

            return total;
        }

        // Normal single resource
        var targetList = GetListFor(def.resourceType);
        var stack = targetList?.FirstOrDefault(s => s.definition.resourceID == def.resourceID);
        return stack?.amount ?? 0;
    }

    public float GetUsedSpace(ResourceType type)
    {
        var list = GetList(type);
        if (list == null) return 0f;

        float total = 0f;
        foreach (var s in list)
            total += SpaceFor(s.definition, s.amount);

        return total;
    }

    public float GetMaxSpace(ResourceType type)
    {
        return type switch
        {
            ResourceType.Material => maxMaterialsSpace,
            ResourceType.Food => maxFoodSpace,
            ResourceType.Water => maxWaterSpace,
            _ => 0f
        };
    }

    public IReadOnlyList<InventoryStack> GetStacks(ResourceType type)
        => GetList(type) ?? (IReadOnlyList<InventoryStack>)Array.Empty<InventoryStack>();

    #endregion

    #region Spoilage & Turns

    private void OnTurnEnded()
    {
        float spoilageMultiplier = GetInventorySpoilageMultiplier();

        _spoiledAmountsThisTurn.Clear();

        TickSpoilage(_materials, _spoiledAmountsThisTurn, spoilToSpoiledFood: false, spoilageMultiplier: spoilageMultiplier);
        TickSpoilage(_food, _spoiledAmountsThisTurn, spoilToSpoiledFood: true, spoilageMultiplier: spoilageMultiplier);
        TickSpoilage(_water, _spoiledAmountsThisTurn, spoilToSpoiledFood: false, spoilageMultiplier: spoilageMultiplier);

        if (_spoiledAmountsThisTurn.Count > 0 && PlayerReligionManager.Instance != null)
            PlayerReligionManager.Instance.NotifyResourcesSpoiled(_spoiledAmountsThisTurn);

        MarkKnowledgeDirty();
    }

    private void RegisterSpoilage(ResourceDefinition def, int amount, Dictionary<ResourceDefinition, int> spoiledAmounts)
    {
        if (def == null || amount <= 0 || spoiledAmounts == null)
            return;

        if (spoiledAmounts.TryGetValue(def, out int current))
            spoiledAmounts[def] = current + amount;
        else
            spoiledAmounts.Add(def, amount);
    }

    private float GetInventorySpoilageMultiplier()
    {
        var religion = PlayerReligionManager.Instance;
        if (religion == null)
            return 1f;

        float mult = religion.GetMultiplierProduct(SpiritEffectType.InventorySpoilageRateMultiplier);
        return Mathf.Max(0f, mult);
    }

    private void TickSpoilage(
    List<InventoryStack> list,
    Dictionary<ResourceDefinition, int> spoiledAmounts,
    bool spoilToSpoiledFood = false,
    float spoilageMultiplier = 1f)
    {
        if (list == null || list.Count == 0) return;

        var toRemove = new List<InventoryStack>();
        int spoiledUnitsToAdd = 0;

        spoilageMultiplier = Mathf.Max(0f, spoilageMultiplier);

        foreach (var s in list.ToArray())
        {
            var def = s.definition;
            if (def == null) continue;
            if (def.nonPerishable) continue;

            float effectiveSpoilageRate = Mathf.Clamp01(def.spoilageRate * spoilageMultiplier);

            if (def.spoilageInterval <= 0)
            {
                if (effectiveSpoilageRate <= 0f) continue;

                int spoilCount = Mathf.Clamp(Mathf.CeilToInt(s.amount * effectiveSpoilageRate), 0, s.amount);
                if (spoilCount > 0)
                {
                    RegisterSpoilage(def, spoilCount, spoiledAmounts);

                    if (def.resourceType == ResourceType.Material)
                    {
                        s.amount -= spoilCount;
                    }
                    else if (def.resourceType == ResourceType.Food && spoilToSpoiledFood)
                    {
                        s.amount -= spoilCount;
                        spoiledUnitsToAdd += spoilCount;
                    }
                    else
                    {
                        s.amount -= spoilCount;
                    }
                }
            }
            else
            {
                if (s.remainingSpoilageTurns < 0)
                    s.remainingSpoilageTurns = def.spoilageInterval;

                s.remainingSpoilageTurns = Mathf.Max(0, s.remainingSpoilageTurns - 1);

                if (s.remainingSpoilageTurns == 0)
                {
                    int spoilCount = Mathf.Clamp(Mathf.CeilToInt(s.amount * effectiveSpoilageRate), 0, s.amount);

                    if (spoilCount > 0)
                    {
                        if (def.resourceType == ResourceType.Material)
                        {
                            int fullStackSpoiled = s.amount;
                            RegisterSpoilage(def, fullStackSpoiled, spoiledAmounts);
                            s.amount = 0;
                        }
                        else if (def.resourceType == ResourceType.Food && spoilToSpoiledFood)
                        {
                            RegisterSpoilage(def, spoilCount, spoiledAmounts);
                            s.amount -= spoilCount;
                            spoiledUnitsToAdd += spoilCount;
                        }
                        else
                        {
                            RegisterSpoilage(def, spoilCount, spoiledAmounts);
                            s.amount -= spoilCount;
                        }
                    }

                    if (s.amount > 0)
                        s.remainingSpoilageTurns = def.spoilageInterval;
                }
            }

            if (s.amount <= 0)
                toRemove.Add(s);
        }

        if (toRemove.Count > 0)
        {
            foreach (var dead in toRemove)
                list.Remove(dead);
        }

        if (spoilToSpoiledFood && spoiledUnitsToAdd > 0)
        {
            var spoiledDef = GetSpoiledDefinition();
            if (spoiledDef != null)
                TryAdd(spoiledDef, spoiledUnitsToAdd);
        }
    }

    #endregion

    #region Helpers

    private List<InventoryStack> GetListFor(ResourceType type)
        => type switch
        {
            ResourceType.Material => _materials,
            ResourceType.Food => _food,
            ResourceType.Water => _water,
            _ => null
        };

    private List<InventoryStack> GetList(ResourceType type) => GetListFor(type);

    private float SpaceFor(ResourceDefinition def, int amount)
    {
        // spec: weight x size per unit
        return Mathf.Max(0, amount) * def.weightPerUnit * def.sizePerUnit;
    }

    private bool HasSpaceFor(ResourceDefinition def, float incomingSpace)
    {
        var type = def.resourceType;
        float used = GetUsedSpace(type);
        float cap = GetMaxSpace(type);
        return used + incomingSpace <= cap + 1e-4f;
    }

    private ResourceDefinition GetSpoiledDefinition()
    {
        if (_spoiledDef != null)
            return _spoiledDef;

        if (spoiledFoodDefinition != null)
        {
            _spoiledDef = spoiledFoodDefinition;
            return _spoiledDef;
        }

        if (string.IsNullOrEmpty(spoiledFoodResourceId))
            return null;

        var all = Resources.LoadAll<ResourceDefinition>("");
        for (int i = 0; i < all.Length; i++)
        {
            var def = all[i];
            if (def != null && string.Equals(def.resourceID, spoiledFoodResourceId, StringComparison.OrdinalIgnoreCase))
            {
                _spoiledDef = def;
                break;
            }
        }

        return _spoiledDef;
    }

    private ResourceDefinition FindResourceById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        EnsureResourceDefinitionCache();

        _resourceDefinitionCache.TryGetValue(id, out ResourceDefinition def);
        return def;
    }

    private void EnsureResourceDefinitionCache()
    {
        if (_resourceDefinitionCacheBuilt && _resourceDefinitionCache != null)
            return;

        _resourceDefinitionCacheBuilt = true;
        _resourceDefinitionCache = new Dictionary<string, ResourceDefinition>(StringComparer.OrdinalIgnoreCase);

        ResourceDefinition[] all = Resources.LoadAll<ResourceDefinition>("");
        for (int i = 0; i < all.Length; i++)
        {
            ResourceDefinition def = all[i];
            if (def == null || string.IsNullOrWhiteSpace(def.resourceID))
                continue;

            if (!_resourceDefinitionCache.ContainsKey(def.resourceID))
                _resourceDefinitionCache.Add(def.resourceID, def);
        }
    }

    #endregion

    // Public: consume nutrition points from FOOD stacks
    public float ConsumeNutrition(float pointsNeeded)
    {
        SortFoodForNutrition();
        return ConsumePointsFromStacks(_food, Mathf.Max(0f, pointsNeeded), useNutrition: true);
    }

    // Public: consume hydration points (water first, then hydrating foods)
    public float ConsumeHydration(float pointsNeeded)
    {
        float need = Mathf.Max(0f, pointsNeeded);
        if (need <= 1e-6f) return 0f;

        // 1) Water first
        float provided = ConsumePointsFromStacks(_water, need, useNutrition: false);

        // 2) If still thirsty, use hydrating foods (hydrationPerUnit on FOOD defs)
        if (provided + 1e-6f < need)
        {
            float remaining = need - provided;
            provided += ConsumePointsFromStacks(_food, remaining, useNutrition: false);
        }

        return provided;
    }

    private float ComputeAvailablePoints(List<InventoryStack> list, bool useNutrition)
    {
        if (list == null || list.Count == 0) return 0f;

        float sum = 0f;
        for (int i = 0; i < list.Count; i++)
        {
            var s = list[i];
            if (s?.definition == null || s.amount <= 0) continue;
            if (!s.allowPopulationConsumption) continue;

            float perUnit = useNutrition ? s.definition.GetNutritionPerUnit() : s.definition.GetHydrationPerUnit();
            if (perUnit <= 0f) continue;

            sum += perUnit * s.amount;
        }
        return sum;
    }

    private string ShortRes(ResourceDefinition def)
    {
        if (def == null) return "<null>";
        // prefer a friendly name if you have one; fall back to resourceID
        return string.IsNullOrEmpty(def.resourceName) ? def.resourceID : def.resourceName;
    }

    private void SortFoodForNutrition()
    {
        if (_food == null || _food.Count <= 1) return;

        _food.Sort((a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            var da = a.definition;
            var db = b.definition;

            // Spoiled always last
            bool aSpoiled = da != null && !string.IsNullOrEmpty(spoiledFoodResourceId) &&
                            string.Equals(da.resourceID, spoiledFoodResourceId, StringComparison.OrdinalIgnoreCase);
            bool bSpoiled = db != null && !string.IsNullOrEmpty(spoiledFoodResourceId) &&
                            string.Equals(db.resourceID, spoiledFoodResourceId, StringComparison.OrdinalIgnoreCase);
            if (aSpoiled != bSpoiled) return aSpoiled ? 1 : -1;

            // Higher grade first
            int ga = da != null ? da.GetFoodGradeValue() : 0;
            int gb = db != null ? db.GetFoodGradeValue() : 0;
            int cmp = gb.CompareTo(ga);
            if (cmp != 0) return cmp;

            // Higher nutrition per unit first (tie-break)
            float na = da != null ? da.GetNutritionPerUnit() : 0f;
            float nb = db != null ? db.GetNutritionPerUnit() : 0f;
            cmp = nb.CompareTo(na);
            if (cmp != 0) return cmp;

            // Prefer expiring sooner (optional waste-reduction)
            int ta = a.remainingSpoilageTurns < 0 ? int.MaxValue : a.remainingSpoilageTurns;
            int tb = b.remainingSpoilageTurns < 0 ? int.MaxValue : b.remainingSpoilageTurns;
            cmp = ta.CompareTo(tb);
            if (cmp != 0) return cmp;

            return 0;
        });
    }

    // Helper: walk stacks and remove as many units as needed to deliver points
    private float ConsumePointsFromStacks(List<InventoryStack> list, float need, bool useNutrition)
    {
        if (list == null || list.Count == 0 || need <= 1e-6f) return 0f;

        string mode = useNutrition ? "NUTRITION" : "HYDRATION";

        float availableBefore = enableConsumeDebug ? ComputeAvailablePoints(list, useNutrition) : 0f;

        if (enableConsumeDebug)
        {
            Debug.Log($"[INV][{mode}] Need={need:F2} pts  |  Available(before)={availableBefore:F2} pts  |  Stacks={list.Count}");
        }

        float providedPoints = 0f;
        float poisonSumDamage01 = 0f;
        int stacksUsed = 0;

        for (int i = 0; i < list.Count && providedPoints < need; i++)
        {
            var s = list[i];
            var def = s.definition;
            if (def == null || s.amount <= 0) continue;
            if (!s.allowPopulationConsumption) continue;

            float perUnit = useNutrition ? def.GetNutritionPerUnit() : def.GetHydrationPerUnit();
            if (perUnit <= 0f) continue;

            float remainingPoints = need - providedPoints;
            int unitsToTake = Mathf.Min(s.amount, Mathf.CeilToInt(remainingPoints / perUnit));
            if (unitsToTake <= 0) continue;

            float subtotal = unitsToTake * perUnit;
            providedPoints += subtotal;
            s.amount -= unitsToTake;

            if (trackFoodVarietyForHappiness && useNutrition && ReferenceEquals(list, _food))
            {
                if (!string.IsNullOrEmpty(spoiledFoodResourceId) &&
                    def != null &&
                    string.Equals(def.resourceID, spoiledFoodResourceId, StringComparison.OrdinalIgnoreCase))
                {
                    _spoiledFoodUnitsConsumedThisTurn += unitsToTake;
                    _spoiledNutritionPointsConsumedThisTurn += subtotal;
                }
                else
                {
                    if (def != null && !string.IsNullOrEmpty(def.resourceID))
                        _foodIdsConsumedThisTurn.Add(def.resourceID);

                    int grade = def != null ? def.GetFoodGradeValue() : 0;

                    _nonSpoiledGradeWeightedSumThisTurn += grade * subtotal;
                    _nonSpoiledNutritionPointsThisTurn += subtotal;
                    _maxNonSpoiledGradeConsumedThisTurn = Mathf.Max(_maxNonSpoiledGradeConsumedThisTurn, grade);
                }

                _foodUnitsConsumedThisTurn += unitsToTake;
                _nutritionPointsConsumedThisTurn += subtotal;
            }

            if (def.HasPoison)
                poisonSumDamage01 += unitsToTake * def.poisonDamagePerUnit01;

            TryApplyDiseaseRiskFromConsumedResource(def, unitsToTake, subtotal, useNutrition);

            TryApplyHealthAndRecoveryEffectsFromConsumedResource(def, unitsToTake);

            stacksUsed++;

            if (enableConsumeDebug)
            {
                float afterRemaining = Mathf.Max(0f, need - providedPoints);
                Debug.Log($"[INV][{mode}]  • Took {unitsToTake} × {ShortRes(def)} @ {perUnit:F2} => {subtotal:F2} pts  |  Remaining need ≈ {afterRemaining:F2}  |  Stack left={s.amount}");
            }

            if (s.amount <= 0)
            {
                list.RemoveAt(i);
                i--;
            }
        }

        if (poisonSumDamage01 > 0f && providedPoints > 0f)
        {
            var gen = GeneralPopulationManager.Instance;
            var pop = PlayersPopulationManager.Instance;
            if (gen != null && pop != null)
            {
                int peopleSatisfied = Mathf.FloorToInt(providedPoints / Mathf.Max(1f, gen.pointsPerPersonScale));
                if (peopleSatisfied > 0)
                    pop.ApplyPoisonToPeople(peopleSatisfied, poisonSumDamage01);
            }
        }

        inventoryPanel?.Refresh();

        if (enableConsumeDebug)
        {
            float overfill = Mathf.Max(0f, providedPoints - need);
            float availableAfter = ComputeAvailablePoints(list, useNutrition);
            Debug.Log($"[INV][{mode}] Provided={providedPoints:F2} / Need={need:F2}  (Overfill={overfill:F2})  |  StacksUsed={stacksUsed}  |  Available(after)={availableAfter:F2}");
        }

        return providedPoints;
    }

    // ===== Points helpers (public) =====
    public float CalcCurrentNutritionNeedPoints(PlayersPopulationManager pop)
    {
        if (pop == null) return 0f;
        float sum = 0f;
        var groups = pop.AllPopulations;
        for (int i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            if (g == null || g.count <= 0) continue;
            // hungerLevel (0..1) × scale × people
            sum += Mathf.Max(0f, g.hungerLevel * GeneralPopulationManager.Instance.pointsPerPersonScale) * g.count;
        }
        return sum;
    }

    public float CalcCurrentHydrationNeedPoints(PlayersPopulationManager pop)
    {
        if (pop == null) return 0f;
        float sum = 0f;
        var groups = pop.AllPopulations;
        for (int i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            if (g == null || g.count <= 0) continue;
            sum += Mathf.Max(0f, g.thirstLevel * GeneralPopulationManager.Instance.pointsPerPersonScale) * g.count;
        }
        return sum;
    }

    public float CalcNextCycleNutritionIncreasePoints(PlayersPopulationManager pop, GeneralPopulationManager general)
    {
        if (pop == null || general == null) return 0f;

        var groups = pop.AllPopulations;
        int totalPeople = 0;
        for (int i = 0; i < groups.Count; i++)
            totalPeople += Mathf.Max(0, groups[i]?.count ?? 0);

        // per-person nutrition points × people
        return Mathf.Max(0f, general.nutritionPointsPerPersonPerCycle) * totalPeople;
    }

    public float CalcNextCycleHydrationIncreasePoints(PlayersPopulationManager pop, GeneralPopulationManager general)
    {
        if (pop == null || general == null) return 0f;

        var groups = pop.AllPopulations;
        int totalPeople = 0;
        for (int i = 0; i < groups.Count; i++)
            totalPeople += Mathf.Max(0, groups[i]?.count ?? 0);

        // per-person hydration points × people
        return Mathf.Max(0f, general.hydrationPointsPerPersonPerCycle) * totalPeople;
    }

    public bool TryRemoveGroup(ResourceDefinition groupDef, int amount)
    {
        if (!groupDef || !groupDef.isGroup || amount <= 0) return false;

        var list = GetListFor(groupDef.groupType);
        if (list == null) return false;

        int remaining = amount;

        // Walk stacks of the same group type, consume until satisfied
        for (int i = 0; i < list.Count && remaining > 0; i++)
        {
            var s = list[i];
            if (s?.definition == null) continue;
            if (s.definition.resourceType != groupDef.groupType) continue;

            int take = Mathf.Min(remaining, s.amount);
            s.amount -= take;
            remaining -= take;

            if (s.amount <= 0)
            {
                list.RemoveAt(i);
                i--;
            }
        }

        return remaining <= 0;
    }

    private void ClearFoodVarietyThisTurn()
    {
        _foodIdsConsumedThisTurn.Clear();

        _foodUnitsConsumedThisTurn = 0;
        _nutritionPointsConsumedThisTurn = 0f;

        _spoiledFoodUnitsConsumedThisTurn = 0;
        _spoiledNutritionPointsConsumedThisTurn = 0f;

        _nonSpoiledGradeWeightedSumThisTurn = 0f;
        _nonSpoiledNutritionPointsThisTurn = 0f;
        _maxNonSpoiledGradeConsumedThisTurn = 0;
    }

    // ---- Backwards compatible wrappers ----

    /// Old call sites can keep using this.
    public void GetAndClearFoodVarietyThisTurn(out int distinctFoods, out int totalUnits, out float nutritionPoints)
    {
        GetAndClearFoodHappinessMetrics(out distinctFoods, out totalUnits, out nutritionPoints,
                                       out _, out _);
    }

    /// 5-out version (for variety + spoiled)
    public void GetAndClearFoodHappinessMetrics(
        out int distinctFoodsNonSpoiled,
        out int totalUnits,
        out float nutritionPoints,
        out int spoiledUnits,
        out float spoiledNutritionPoints)
    {
        GetAndClearFoodHappinessMetrics(
            out distinctFoodsNonSpoiled,
            out totalUnits,
            out nutritionPoints,
            out spoiledUnits,
            out spoiledNutritionPoints,
            out _,   // avgNonSpoiledGradeByNutrition
            out _);  // maxNonSpoiledGradeConsumed
    }

    /// 7-out version (adds grade info)
    public void GetAndClearFoodHappinessMetrics(
        out int distinctFoodsNonSpoiled,
        out int totalUnits,
        out float nutritionPoints,
        out int spoiledUnits,
        out float spoiledNutritionPoints,
        out float avgNonSpoiledGradeByNutrition,
        out int maxNonSpoiledGradeConsumed)
    {
        distinctFoodsNonSpoiled = _foodIdsConsumedThisTurn.Count;
        totalUnits = _foodUnitsConsumedThisTurn;
        nutritionPoints = _nutritionPointsConsumedThisTurn;

        spoiledUnits = _spoiledFoodUnitsConsumedThisTurn;
        spoiledNutritionPoints = _spoiledNutritionPointsConsumedThisTurn;

        maxNonSpoiledGradeConsumed = _maxNonSpoiledGradeConsumedThisTurn;

        avgNonSpoiledGradeByNutrition =
            (_nonSpoiledNutritionPointsThisTurn > 1e-6f)
                ? (_nonSpoiledGradeWeightedSumThisTurn / _nonSpoiledNutritionPointsThisTurn)
                : 0f;

        ClearFoodVarietyThisTurn();
    }

    public void SetInventoryPanel(InventoryPanelControl newPanel, bool refreshIfOpen = true)
    {
        if (newPanel == null)
            return;

        inventoryPanel = newPanel;

        if (refreshIfOpen && inventoryPanel.root != null && inventoryPanel.root.activeSelf)
            inventoryPanel.Refresh();
    }

    public void SetPopulationConsumptionAllowed(InventoryStack stack, bool allowed)
    {
        if (stack == null)
            return;

        stack.allowPopulationConsumption = allowed;
        MarkKnowledgeDirty();
    }

    private void TryApplyHealthAndRecoveryEffectsFromConsumedResource(ResourceDefinition def, int unitsConsumed)
    {
        if (def == null || unitsConsumed <= 0)
            return;

        DiseaseManager diseaseManager = DiseaseManager.Instance;
        if (diseaseManager == null)
            return;

        if (enableConsumedResourceHealthEffects && def.HasHealthRestore)
        {
            int maxTargets =
                def.maxHealthTargetsPerUnit <= 0
                    ? 0
                    : unitsConsumed * def.maxHealthTargetsPerUnit;

            diseaseManager.TryApplyResourceHealthRestore(
                def,
                unitsConsumed * def.healthRestorePerUnit01,
                maxTargets,
                def.prioritizeLowestHealth);
        }

        if (enableConsumedResourceDiseaseRecoveryEffects && def.HasDiseaseRecoveryBoost)
        {
            int maxTargets =
                def.maxDiseaseRecoveryTargetsPerUnit <= 0
                    ? 0
                    : unitsConsumed * def.maxDiseaseRecoveryTargetsPerUnit;

            diseaseManager.TryApplyConsumedResourceRecoveryBoost(
                def,
                unitsConsumed * def.diseaseRecoveryBoostPerUnit01,
                maxTargets);
        }
    }

    private int TryApplyHealthRestoreFromConsumedResource(ResourceDefinition def, int unitsConsumed)
    {
        if (def == null || unitsConsumed <= 0)
            return 0;

        float healBudget = unitsConsumed * Mathf.Max(0f, def.healthRestorePerUnit01);
        if (healBudget <= 0f)
            return 0;

        PlayerFamilySimulationManager familySim = PlayerFamilySimulationManager.Instance;
        PlayersPopulationManager pop = PlayersPopulationManager.Instance;

        if (familySim == null)
            return 0;

        IReadOnlyList<Individual> allPeople = familySim.GetIndividuals();
        if (allPeople == null || allPeople.Count == 0)
            return 0;

        List<Individual> targets = new();

        for (int i = 0; i < allPeople.Count; i++)
        {
            Individual person = allPeople[i];

            if (person == null || !person.IsAlive)
                continue;

            if (person.Health01 >= 0.999f)
                continue;

            targets.Add(person);
        }

        if (targets.Count == 0)
            return 0;

        if (def.prioritizeLowestHealth)
            targets.Sort((a, b) => a.Health01.CompareTo(b.Health01));

        int maxTargets =
            def.maxHealthTargetsPerUnit <= 0
                ? targets.Count
                : Mathf.Min(targets.Count, unitsConsumed * def.maxHealthTargetsPerUnit);

        int healedPeople = 0;
        float totalHealthRestored = 0f;

        for (int i = 0; i < maxTargets && healBudget > 0.0001f; i++)
        {
            Individual person = targets[i];
            if (person == null || !person.IsAlive)
                continue;

            float missingHealth = Mathf.Clamp01(1f - person.Health01);
            if (missingHealth <= 0f)
                continue;

            float restore = Mathf.Min(missingHealth, healBudget);
            if (restore <= 0f)
                continue;

            float oldHealth = person.Health01;
            person.Health01 = Mathf.Clamp01(person.Health01 + restore);

            float actualRestored = person.Health01 - oldHealth;
            if (actualRestored <= 0f)
                continue;

            healBudget -= actualRestored;
            totalHealthRestored += actualRestored;
            healedPeople++;
        }

        if (healedPeople > 0)
        {
            pop?.MarkUIDirty();

            SaveSystem.MarkSectionDirty(SaveSectionKeys.Population);

            if (debugConsumedResourceRecoveryEffects)
            {
                Debug.Log(
                    $"[INV][RECOVERY] {def.resourceName} restored health. " +
                    $"Units={unitsConsumed}, " +
                    $"PeopleHealed={healedPeople}, " +
                    $"TotalHealthRestored={totalHealthRestored:F3}");
            }
        }

        return healedPeople;
    }

    private int TryApplyDiseaseRecoveryBoostFromConsumedResource(ResourceDefinition def, int unitsConsumed)
    {
        if (def == null || unitsConsumed <= 0)
            return 0;

        if (DiseaseManager.Instance == null)
            return 0;

        float recoveryBoostBudget = unitsConsumed * Mathf.Max(0f, def.diseaseRecoveryBoostPerUnit01);
        if (recoveryBoostBudget <= 0f)
            return 0;

        int maxTargets =
            def.maxDiseaseRecoveryTargetsPerUnit <= 0
                ? 0
                : unitsConsumed * def.maxDiseaseRecoveryTargetsPerUnit;

        int affected = DiseaseManager.Instance.TryApplyConsumedResourceRecoveryBoost(
            def,
            recoveryBoostBudget,
            maxTargets);

        if (debugConsumedResourceRecoveryEffects && affected > 0)
        {
            Debug.Log(
                $"[INV][RECOVERY] {def.resourceName} boosted disease recovery. " +
                $"Units={unitsConsumed}, " +
                $"RecoveryBudget={recoveryBoostBudget:F3}, " +
                $"Affected={affected}");
        }

        return affected;
    }
}

[Serializable]
public class InventoryStack
{
    public ResourceDefinition definition;
    public int amount;

    /// Remaining turns until next spoil tick (for interval-based items).
    public int remainingSpoilageTurns = -1;

    /// If false, population will not consume this stack for hunger/thirst.
    public bool allowPopulationConsumption = true;

    public InventoryStack(ResourceDefinition def, int amount)
    {
        this.definition = def;
        this.amount = amount;
        this.allowPopulationConsumption = true;
    }
}