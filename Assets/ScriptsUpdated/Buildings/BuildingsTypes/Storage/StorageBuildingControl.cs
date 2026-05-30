using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class StorageItem
{
    [Header("Resource")]
    public ResourceDefinition definition;

    public string resourceID;

    [Header("Amount")]
    public int amount;

    [Header("Spoilage (storage-side, AFTER modifier)")]
    public int spoilageInterval;
    public int currentInterval;

    public void SyncLegacyFields()
    {
        if (definition != null)
            resourceID = definition.resourceID;
    }
}

public class StorageBuildingControl : MonoBehaviour, IBuildingTurnTickable
{
    [Header("Storage Settings")]
    public int maxStorageCapacity = 100;
    public List<StorageItem> storedResources = new();
    public float spoilageModifier = 1f;

    [Header("Supported Resources (leave empty = allow all)")]
    public List<ResourceDefinition> supportedResources = new();

    [Header("Spoiled Food (optional)")]
    [Tooltip("If set, spoiled FOOD will be converted into this definition (like your inventory does).")]
    public ResourceDefinition spoiledFoodDefinition;

    [Header("Inventory Capacity Bonus")]
    [SerializeField] private bool increasesFoodStorage = false;
    [SerializeField] private float foodStorageIncreaseAmount = 0f;

    [SerializeField] private bool increasesMaterialStorage = false;
    [SerializeField] private float materialStorageIncreaseAmount = 0f;

    [SerializeField] private bool increasesWaterStorage = false;
    [SerializeField] private float waterStorageIncreaseAmount = 0f;

    [Header("Storage Status Icon")]
    public Image storageStatusIcon;
    public Sprite storageMediumIcon;
    public Sprite storageFullIcon;

    [SerializeField] private GameObject spoiledFoodIndicator;

    private int totalStoredAmount;

    private static Dictionary<string, ResourceDefinition> _defByIdCache;

    private bool _capacityApplied;
    private int _capKey;

    private void Awake()
    {
        RecalculateTotalStoredAmount();
        // Best effort: if spoiledFoodDefinition not set but PlayerInventoryManager has an id, try resolve it.
        if (spoiledFoodDefinition == null && PlayerInventoryManager.Instance != null)
        {
            spoiledFoodDefinition = FindDefById(PlayerInventoryManager.Instance.spoiledFoodResourceId);
        }
    }

    private void Start()
    {
        RecalculateTotalStoredAmount();
        UpdateStorageIcon();
    }

    private void OnDestroy()
    {
        RemoveInventoryCapacityBonus();
    }

    private void OnEnable()
    {
        BuildingTickManager.Instance?.Register(this);
        ApplyInventoryCapacityBonus();
    }

    private void OnDisable()
    {
        BuildingTickManager.Instance?.Unregister(this);
        RemoveInventoryCapacityBonus();
    }

    public void TurnTick()
    {
        HandleSpoilageTick();
        RecalculateTotalStoredAmount();
        UpdateWorldCanvasIndicators();
        UpdateStorageIcon();
    }

    private void OnValidate()
    {
        // Keep legacy id synced so older code/saves don't explode.
        if (storedResources != null)
        {
            foreach (var s in storedResources)
                if (s != null) s.SyncLegacyFields();
        }
    }

    // ----------------------------
    // Public API (Definition-first)
    // ----------------------------

    public bool CanStoreResource(ResourceDefinition def)
    {
        if (def == null) return false;
        if (supportedResources == null || supportedResources.Count == 0) return true;
        return supportedResources.Contains(def);
    }

    public int GetTotalStoredAmount()
    {
        int total = 0;
        if (storedResources == null) return 0;
        foreach (var item in storedResources)
            total += item != null ? Mathf.Max(0, item.amount) : 0;
        return total;
    }

    public int GetAvailableSpace()
    {
        RecalculateTotalStoredAmount();
        return Mathf.Max(0, maxStorageCapacity - totalStoredAmount);
    }

    // shared capacity (kept for compatibility)
    public int GetAvailableSpaceForResource(ResourceDefinition _)
        => GetAvailableSpace();

    public void RecalculateTotalStoredAmount()
    {
        totalStoredAmount = 0;
        if (storedResources == null) return;

        for (int i = 0; i < storedResources.Count; i++)
        {
            var s = storedResources[i];
            if (s == null) continue;
            totalStoredAmount += Mathf.Max(0, s.amount);
        }

        totalStoredAmount = Mathf.Clamp(totalStoredAmount, 0, maxStorageCapacity);
    }

    public void UpdateStorageIcon()
    {
        if (storageStatusIcon == null) return;

        RecalculateTotalStoredAmount();

        float pct = maxStorageCapacity > 0 ? (float)totalStoredAmount / maxStorageCapacity : 0f;

        if (pct >= 1f)
        {
            storageStatusIcon.sprite = storageFullIcon;
            storageStatusIcon.gameObject.SetActive(true);
        }
        else if (pct >= 0.5f)
        {
            storageStatusIcon.sprite = storageMediumIcon;
            storageStatusIcon.gameObject.SetActive(true);
        }
        else
        {
            storageStatusIcon.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Adds a resource into storage with spoilage timing in "player/base turns" (pre-modifier).
    /// We convert that timing into storage-side timing using spoilageModifier.
    /// </summary>
    public bool AddResourceWithSpoilage(ResourceDefinition def, int amount, int baseSpoilageInterval, int baseCurrentInterval)
    {
        if (def == null || amount <= 0) return false;
        if (!CanStoreResource(def)) return false;

        RecalculateTotalStoredAmount();
        if (totalStoredAmount + amount > maxStorageCapacity) return false;

        int storageInterval = ConvertIntervalToStorage(baseSpoilageInterval);
        int storageCurrent = ConvertCurrentToStorage(baseCurrentInterval, baseSpoilageInterval);

        AddOrUpdate(def, amount, storageInterval, storageCurrent);

        totalStoredAmount += amount;
        UpdateStorageIcon();
        UpdateWorldCanvasIndicators();
        return true;
    }

    /// <summary>
    /// Withdraw from storage into PlayerInventoryManager. Returns taken amount (can be partial).
    /// Preserves spoilage progress by weighted-averaging remaining turns.
    /// </summary>
    public bool TryWithdraw(ResourceDefinition def, int requestAmount, out int taken)
    {
        taken = 0;
        if (def == null || requestAmount <= 0) return false;
        if (PlayerInventoryManager.Instance == null) return false;

        var item = FindStored(def);
        if (item == null || item.amount <= 0) return false;

        int removable = Mathf.Min(requestAmount, item.amount);
        if (removable <= 0) return false;

        float unitSpace = Mathf.Max(0.0001f, def.weightPerUnit * def.sizePerUnit);
        float remainingSpace = PlayerInventoryManager.Instance.GetMaxSpace(def.resourceType) - PlayerInventoryManager.Instance.GetUsedSpace(def.resourceType);
        int maxFittable = Mathf.FloorToInt(remainingSpace / unitSpace);
        removable = Mathf.Min(removable, maxFittable);

        if (removable <= 0) return false;

        var invList = PlayerInventoryManager.Instance.GetStacks(def.resourceType);
        var stackBefore = invList.FirstOrDefault(s => s != null && s.definition != null && s.definition.resourceID == def.resourceID);
        int a = stackBefore != null ? Mathf.Max(0, stackBefore.amount) : 0;
        int ra = stackBefore != null ? stackBefore.remainingSpoilageTurns : def.spoilageInterval;

        int incomingRemaining = ConvertCurrentToBase(def, item);

        if (!PlayerInventoryManager.Instance.TryAdd(def, removable))
            return false;

        if (!def.nonPerishable)
        {
            var stackAfter = PlayerInventoryManager.Instance.GetStacks(def.resourceType)
                .FirstOrDefault(s => s != null && s.definition != null && s.definition.resourceID == def.resourceID);

            if (stackAfter != null)
            {
                int b = removable;
                int rb = incomingRemaining;

                if (a <= 0) stackAfter.remainingSpoilageTurns = rb;
                else
                    stackAfter.remainingSpoilageTurns = Mathf.RoundToInt(((a * ra) + (b * rb)) / Mathf.Max(1, (a + b)));

                if (def.spoilageInterval > 0)
                    stackAfter.remainingSpoilageTurns = Mathf.Clamp(stackAfter.remainingSpoilageTurns, 0, def.spoilageInterval);
            }
        }

        item.amount -= removable;
        if (item.amount <= 0)
            storedResources.Remove(item);

        RecalculateTotalStoredAmount();
        UpdateStorageIcon();
        UpdateWorldCanvasIndicators();

        taken = removable;
        return true;
    }

    // ----------------------------
    // Compatibility wrappers (string-based callers won’t break)
    // ----------------------------

    public bool CanStoreResource(string resourceID) => CanStoreResource(FindDefById(resourceID));

    public int GetAvailableSpaceForResource(string _) => GetAvailableSpace();

    public bool AddResourceWithSpoilage(string resourceID, int amount, int spoilageInterval, int currentInterval)
        => AddResourceWithSpoilage(FindDefById(resourceID), amount, spoilageInterval, currentInterval);

    public bool RemoveResource(string resourceID, int amount)
        => TryWithdraw(FindDefById(resourceID), amount, out _);

    // ----------------------------
    // Internals
    // ----------------------------

    private StorageItem FindStored(ResourceDefinition def)
    {
        if (storedResources == null) return null;

        // primary: reference match; fallback: resourceID match
        for (int i = 0; i < storedResources.Count; i++)
        {
            var s = storedResources[i];
            if (s == null) continue;
            if (s.definition == def) return s;
            if (s.definition != null && s.definition.resourceID == def.resourceID) return s;
            if (!string.IsNullOrEmpty(s.resourceID) && s.resourceID == def.resourceID) return s;
        }

        return null;
    }

    private void AddOrUpdate(ResourceDefinition def, int amount, int storageInterval, int storageCurrent)
    {
        var existing = FindStored(def);

        if (existing != null)
        {
            int a = Mathf.Max(0, existing.amount);
            int b = Mathf.Max(0, amount);

            // Weighted average remaining turns (storage-side)
            if (!def.nonPerishable && a > 0 && b > 0 && storageInterval > 0)
            {
                existing.currentInterval = Mathf.RoundToInt(((a * existing.currentInterval) + (b * storageCurrent)) / Mathf.Max(1, (a + b)));
                existing.currentInterval = Mathf.Clamp(existing.currentInterval, 0, storageInterval);
            }
            else
            {
                existing.currentInterval = storageCurrent;
            }

            existing.spoilageInterval = storageInterval;
            existing.amount += amount;
            existing.definition = def;
            existing.SyncLegacyFields();
        }
        else
        {
            var s = new StorageItem
            {
                definition = def,
                amount = amount,
                spoilageInterval = storageInterval,
                currentInterval = storageCurrent,
            };
            s.SyncLegacyFields();
            storedResources.Add(s);
        }
    }

    private void HandleSpoilageTick()
    {
        if (storedResources == null || storedResources.Count == 0) return;

        // stage conversions to avoid list issues
        int spoiledFoodToAdd = 0;

        for (int i = 0; i < storedResources.Count; i++)
        {
            var item = storedResources[i];
            if (item == null || item.amount <= 0) continue;

            var def = item.definition != null ? item.definition : FindDefById(item.resourceID);
            if (def == null) continue;

            // Keep synced
            item.definition = def;
            item.SyncLegacyFields();

            if (def.nonPerishable) continue;

            float rate = NormalizeSpoilageRate(def.spoilageRate);
            if (rate <= 0f) continue;

            // per-turn spoilage by rate
            if (def.spoilageInterval <= 0)
            {
                int spoilCount = Mathf.Clamp(Mathf.CeilToInt(item.amount * rate), 0, item.amount);
                if (spoilCount <= 0) continue;

                ApplySpoilage(def, item, spoilCount, ref spoiledFoodToAdd);
            }
            else
            {
                item.spoilageInterval = Mathf.Max(1, item.spoilageInterval);
                item.currentInterval--;

                if (item.currentInterval > 0) continue;

                int spoilCount = Mathf.Clamp(Mathf.CeilToInt(item.amount * rate), 0, item.amount);
                if (spoilCount > 0)
                    ApplySpoilage(def, item, spoilCount, ref spoiledFoodToAdd);

                if (item.amount > 0)
                    item.currentInterval = item.spoilageInterval;
            }

            if (item.amount <= 0)
            {
                storedResources.RemoveAt(i);
                i--;
            }
        }

        // Apply staged spoiled food conversion
        if (spoiledFoodToAdd > 0 && spoiledFoodDefinition != null)
        {
            // Use "base timing" for spoiled food as full interval remaining
            int baseInterval = Mathf.Max(1, spoiledFoodDefinition.spoilageInterval <= 0 ? 1 : spoiledFoodDefinition.spoilageInterval);
            AddResourceWithSpoilage(spoiledFoodDefinition, spoiledFoodToAdd, baseInterval, baseInterval);
        }
    }

    private void ApplySpoilage(ResourceDefinition def, StorageItem item, int spoilCount, ref int spoiledFoodToAdd)
    {
        // Materials: follow your inventory behavior: interval spoil wipes stack; per-turn reduces portion.
        if (def.resourceType == ResourceType.Material)
        {
            if (def.spoilageInterval > 0) item.amount = 0;
            else item.amount -= spoilCount;
            return;
        }

        // Food: convert to spoiledFoodDefinition if provided
        if (def.resourceType == ResourceType.Food && spoiledFoodDefinition != null)
        {
            item.amount -= spoilCount;
            spoiledFoodToAdd += spoilCount;
            return;
        }

        // Water/other: just reduce
        item.amount -= spoilCount;
    }

    private float NormalizeSpoilageRate(float r)
    {
        // Supports either 0..1 or 0..100 authoring
        if (r <= 0f) return 0f;
        if (r > 1f) return r / 100f;
        return r;
    }

    private int ConvertIntervalToStorage(int baseInterval)
    {
        if (baseInterval <= 0) return 0; // per-turn mode
        return Mathf.Max(1, Mathf.CeilToInt(baseInterval * Mathf.Max(0.0001f, spoilageModifier)));
    }

    private int ConvertCurrentToStorage(int baseCurrent, int baseInterval)
    {
        if (baseInterval <= 0) return 0; // per-turn mode

        int interval = ConvertIntervalToStorage(baseInterval);
        int cur = Mathf.CeilToInt(baseCurrent * Mathf.Max(0.0001f, spoilageModifier));
        return Mathf.Clamp(cur, 0, interval);
    }

    private int ConvertCurrentToBase(ResourceDefinition def, StorageItem item)
    {
        if (def == null) return 0;
        if (def.nonPerishable) return -1;

        if (def.spoilageInterval <= 0) return 1; // per-turn mode: remaining doesn't matter much

        int storageInterval = Mathf.Max(1, item.spoilageInterval);
        float progress = storageInterval > 0 ? (float)item.currentInterval / storageInterval : 1f;

        int baseInterval = Mathf.Max(1, def.spoilageInterval);
        int baseCurrent = Mathf.RoundToInt(baseInterval * progress);
        return Mathf.Clamp(baseCurrent, 0, baseInterval);
    }

    private void ApplyInventoryCapacityBonus()
    {
        if (_capacityApplied) return;

        var inv = PlayerInventoryManager.Instance;
        if (inv == null) return;

        _capKey = GetInstanceID();

        float food = increasesFoodStorage ? Mathf.Max(0f, foodStorageIncreaseAmount) : 0f;
        float mats = increasesMaterialStorage ? Mathf.Max(0f, materialStorageIncreaseAmount) : 0f;
        float water = increasesWaterStorage ? Mathf.Max(0f, waterStorageIncreaseAmount) : 0f;

        inv.SetCapacityBonus(_capKey, materialsDelta: mats, foodDelta: food, waterDelta: water);

        _capacityApplied = true;
    }

    private void RemoveInventoryCapacityBonus()
    {
        if (!_capacityApplied) return;

        var inv = PlayerInventoryManager.Instance;
        if (inv != null)
            inv.RemoveCapacityBonus(_capKey);

        _capacityApplied = false;
        _capKey = 0;
    }

    private static ResourceDefinition FindDefById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        if (_defByIdCache == null)
        {
            _defByIdCache = new Dictionary<string, ResourceDefinition>();
            var all = Resources.LoadAll<ResourceDefinition>("");
            foreach (var d in all)
            {
                if (d == null || string.IsNullOrEmpty(d.resourceID)) continue;
                if (!_defByIdCache.ContainsKey(d.resourceID))
                    _defByIdCache.Add(d.resourceID, d);
            }
        }

        _defByIdCache.TryGetValue(id, out var def);
        return def;
    }

    public bool HasSpoiledFoodInStorage()
    {
        if (spoiledFoodDefinition == null || storedResources == null || storedResources.Count == 0)
            return false;

        for (int i = 0; i < storedResources.Count; i++)
        {
            var item = storedResources[i];
            if (item == null || item.amount <= 0)
                continue;

            var def = item.definition != null ? item.definition : FindDefById(item.resourceID);
            if (def == null)
                continue;

            if (def == spoiledFoodDefinition || def.resourceID == spoiledFoodDefinition.resourceID)
                return true;
        }

        return false;
    }

    private void UpdateWorldCanvasIndicators()
    {
        if (spoiledFoodIndicator != null)
            spoiledFoodIndicator.SetActive(HasSpoiledFoodInStorage());
    }
    public StorageBuildingSaveData CaptureRuntimeSaveData(string buildingSaveableID)
    {
        StorageBuildingSaveData data = new StorageBuildingSaveData
        {
            buildingSaveableID = buildingSaveableID,
            maxStorageCapacity = maxStorageCapacity,
            spoilageModifier = spoilageModifier,
            spoiledFoodResourceID = spoiledFoodDefinition != null ? spoiledFoodDefinition.resourceID : string.Empty
        };

        if (storedResources != null)
        {
            for (int i = 0; i < storedResources.Count; i++)
            {
                StorageItem item = storedResources[i];
                if (item == null || item.amount <= 0)
                    continue;

                string resourceID = !string.IsNullOrWhiteSpace(item.resourceID)
                    ? item.resourceID
                    : (item.definition != null ? item.definition.resourceID : string.Empty);

                if (string.IsNullOrWhiteSpace(resourceID))
                    continue;

                data.storedResources.Add(new StorageItemSaveData
                {
                    resourceID = resourceID,
                    amount = item.amount,
                    spoilageInterval = item.spoilageInterval,
                    currentInterval = item.currentInterval
                });
            }
        }

        return data;
    }

    public void ClearStorageForLoad()
    {
        if (storedResources == null)
            storedResources = new List<StorageItem>();
        else
            storedResources.Clear();

        RecalculateTotalStoredAmount();
        UpdateWorldCanvasIndicators();
        UpdateStorageIcon();
    }

    public void ApplyRuntimeSaveData(StorageBuildingSaveData data, Func<string, ResourceDefinition> resourceResolver)
    {
        if (data == null)
        {
            ClearStorageForLoad();
            return;
        }

        maxStorageCapacity = Mathf.Max(0, data.maxStorageCapacity);
        spoilageModifier = Mathf.Max(0.0001f, data.spoilageModifier);

        if (!string.IsNullOrWhiteSpace(data.spoiledFoodResourceID) && resourceResolver != null)
        {
            ResourceDefinition spoiled = resourceResolver(data.spoiledFoodResourceID);
            if (spoiled != null)
                spoiledFoodDefinition = spoiled;
        }

        if (storedResources == null)
            storedResources = new List<StorageItem>();
        else
            storedResources.Clear();

        if (data.storedResources != null)
        {
            for (int i = 0; i < data.storedResources.Count; i++)
            {
                StorageItemSaveData saved = data.storedResources[i];
                if (saved == null || string.IsNullOrWhiteSpace(saved.resourceID) || saved.amount <= 0)
                    continue;

                ResourceDefinition def = resourceResolver != null ? resourceResolver(saved.resourceID) : null;
                if (def == null)
                    continue;

                StorageItem item = new StorageItem
                {
                    definition = def,
                    resourceID = def.resourceID,
                    amount = Mathf.Max(0, saved.amount),
                    spoilageInterval = Mathf.Max(0, saved.spoilageInterval),
                    currentInterval = Mathf.Max(0, saved.currentInterval)
                };

                item.SyncLegacyFields();
                storedResources.Add(item);
            }
        }

        RecalculateTotalStoredAmount();
        UpdateWorldCanvasIndicators();
        UpdateStorageIcon();
    }

    public int TryApplyFireStorageLoss(
    float severity01,
    int maxDestroyedThisStep,
    bool allowDestroySpoiledFood,
    bool debugLogging)
    {
        severity01 = Mathf.Clamp01(severity01);
        maxDestroyedThisStep = Mathf.Max(0, maxDestroyedThisStep);

        if (severity01 <= 0f)
            return 0;

        if (storedResources == null || storedResources.Count == 0)
            return 0;

        RecalculateTotalStoredAmount();

        int totalBefore = GetTotalStoredAmount();
        if (totalBefore <= 0)
            return 0;

        int targetLoss = Mathf.CeilToInt(totalBefore * severity01);

        if (maxDestroyedThisStep > 0)
            targetLoss = Mathf.Min(targetLoss, maxDestroyedThisStep);

        targetLoss = Mathf.Clamp(targetLoss, 0, totalBefore);

        if (targetLoss <= 0)
            return 0;

        int destroyedTotal = 0;
        int remainingLoss = targetLoss;
        int remainingAmountPool = totalBefore;

        for (int i = storedResources.Count - 1; i >= 0 && remainingLoss > 0; i--)
        {
            StorageItem item = storedResources[i];

            if (item == null || item.amount <= 0)
            {
                storedResources.RemoveAt(i);
                continue;
            }

            ResourceDefinition def = item.definition != null
                ? item.definition
                : FindDefById(item.resourceID);

            if (def == null)
                continue;

            item.definition = def;
            item.SyncLegacyFields();

            if (!allowDestroySpoiledFood &&
                spoiledFoodDefinition != null &&
                (def == spoiledFoodDefinition || def.resourceID == spoiledFoodDefinition.resourceID))
            {
                remainingAmountPool -= Mathf.Max(0, item.amount);
                continue;
            }

            if (remainingAmountPool <= 0)
                break;

            float share01 = item.amount / (float)remainingAmountPool;

            int amountToDestroy = Mathf.CeilToInt(remainingLoss * share01);
            amountToDestroy = Mathf.Clamp(amountToDestroy, 0, item.amount);
            amountToDestroy = Mathf.Min(amountToDestroy, remainingLoss);

            if (amountToDestroy <= 0 && remainingLoss > 0)
                amountToDestroy = 1;

            item.amount -= amountToDestroy;
            destroyedTotal += amountToDestroy;
            remainingLoss -= amountToDestroy;
            remainingAmountPool -= Mathf.Max(0, item.amount + amountToDestroy);

            if (debugLogging)
            {
                string resourceName = def != null ? def.name : item.resourceID;
                //Debug.Log(
                    //$"[StorageBuildingControl] Fire destroyed {amountToDestroy}x {resourceName} " +
                    //$"from '{name}'. Remaining stack={Mathf.Max(0, item.amount)}");
            }

            if (item.amount <= 0)
                storedResources.RemoveAt(i);
        }

        if (destroyedTotal <= 0)
            return 0;

        RecalculateTotalStoredAmount();
        UpdateWorldCanvasIndicators();
        UpdateStorageIcon();

        if (debugLogging)
        {
            //Debug.Log(
                //$"[StorageBuildingControl] Fire destroyed {destroyedTotal} total resources " +
                //$"from '{name}'. Before={totalBefore}, After={GetTotalStoredAmount()}");
        }

        return destroyedTotal;
    }
}
