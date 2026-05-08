using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCraftingManager : MonoBehaviour
{
    public static PlayerCraftingManager Instance { get; private set; }

    [Header("Batching")]
    [Tooltip("How many completed craft orders to finalize per frame.")]
    [Min(1)] public int completionsPerFrame = 100;

    private readonly Queue<CraftingBuildingControl.CraftingCompletion> _pending = new();
    private Coroutine _processCo;

    private static Dictionary<string, ResourceDefinition> _resourceById;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        TurnSystem.SubscribeToEndOfTurn(OnEndTurn);
    }

    private void OnDisable()
    {
        TurnSystem.UnsubscribeFromEndOfTurn(OnEndTurn);
    }

    private void MarkJobsDirty()
    {
        SaveSystem.MarkSectionDirty(SaveSectionKeys.Jobs);
        SaveSystem.MarkSectionDirty(SaveSectionKeys.WorldObjects);
    }

    private string GetReservationOwnerId(CraftingBuildingControl cbc)
    {
        if (cbc == null)
            return null;

        Saveable saveable = cbc.GetComponent<Saveable>();
        if (saveable == null)
            saveable = cbc.GetComponentInParent<Saveable>();

        if (saveable != null && !string.IsNullOrWhiteSpace(saveable.uniqueID))
            return saveable.uniqueID;

        return cbc.gameObject.GetInstanceID().ToString();
    }

    private void TagCraftingReservation(CraftingBuildingControl cbc, string reservationId)
    {
        if (string.IsNullOrWhiteSpace(reservationId))
            return;

        var pop = PlayersPopulationManager.Instance;
        if (pop == null)
            return;

        pop.UpdateReservationMetadata(
            reservationId,
            PopulationReservationKind.Crafting,
            GetReservationOwnerId(cbc),
            nameof(CraftingBuildingControl));
    }

    private void OnEndTurn()
    {
        EnqueueCompletionsFromAllBuildings();

        if (_processCo == null && _pending.Count > 0)
            _processCo = StartCoroutine(ProcessCompletions());

        MarkJobsDirty();
    }

    private void EnqueueCompletionsFromAllBuildings()
    {
        var pbm = PlayerBuildingManager.Instance;
        if (pbm == null)
            return;

        var all = pbm.GetAll();
        if (all == null || all.Count == 0)
            return;

        for (int i = 0; i < all.Count; i++)
        {
            var rec = all[i];
            if (rec == null || !rec.instance)
                continue;

            var cbc = rec.instance.GetComponent<CraftingBuildingControl>();
            if (cbc == null || !cbc.isActiveAndEnabled)
                continue;

            var tmp = ListPool<CraftingBuildingControl.CraftingCompletion>.Get();
            try
            {
                int count = cbc.AdvanceTurnAndCollectCompletions(tmp);
                for (int k = 0; k < count; k++)
                {
                    var cc = tmp[k];

                    if (!string.IsNullOrWhiteSpace(cc.reservationId))
                        TagCraftingReservation(cbc, cc.reservationId);

                    _pending.Enqueue(cc);
                }
            }
            finally
            {
                ListPool<CraftingBuildingControl.CraftingCompletion>.Release(tmp);
            }
        }
    }

    private IEnumerator ProcessCompletions()
    {
        var inv = PlayerInventoryManager.Instance;
        var pop = PlayersPopulationManager.Instance;
        var level = PlayerLevel.Instance;

        while (_pending.Count > 0)
        {
            int toDo = Mathf.Min(completionsPerFrame, _pending.Count);
            bool refreshedInvThisFrame = false;

            for (int i = 0; i < toDo; i++)
            {
                var cc = _pending.Dequeue();

                if (!string.IsNullOrWhiteSpace(cc.reservationId))
                    TagCraftingReservation(cc.source, cc.reservationId);

                if (inv != null && cc.payout != null)
                {
                    float orderOutputMultiplier = cc.outputMultiplier > 0f ? cc.outputMultiplier : 1f;
                    var adjustedPayout = GetAdjustedCraftingPayout(cc.payout, orderOutputMultiplier);

                    for (int j = 0; j < adjustedPayout.Count; j++)
                    {
                        var a = adjustedPayout[j];
                        if (a != null && a.resource != null && a.amount > 0)
                            inv.TryAdd(a.resource, a.amount);
                    }
                     
                    refreshedInvThisFrame = true;
                }

                if (cc.xpAward > 0)
                    level?.AddXP(cc.xpAward);

                if (!string.IsNullOrEmpty(cc.reservationId))
                    pop?.ReleaseReservation(cc.reservationId);

                if (cc.source != null)
                    cc.source.OnOrderFinalizedExternally(cc.orderId);
            }

            if (refreshedInvThisFrame)
                inv?.inventoryPanel?.Refresh();

            yield return null;
        }

        MarkJobsDirty();
        _processCo = null;
    }

    private List<ResourceAmount> GetAdjustedCraftingPayout(List<ResourceAmount> basePayout, float orderOutputMultiplier = 1f)
    {
        var result = new List<ResourceAmount>(basePayout != null ? basePayout.Count : 0);
        if (basePayout == null || basePayout.Count == 0)
            return result;

        float multiplier = GetReligionCraftingOutputMultiplier() * Mathf.Max(0f, orderOutputMultiplier);

        for (int i = 0; i < basePayout.Count; i++)
        {
            var a = basePayout[i];
            if (a == null || a.resource == null || a.amount <= 0)
                continue;

            result.Add(new ResourceAmount
            {
                resource = a.resource,
                amount = Mathf.Max(0, Mathf.RoundToInt(a.amount * multiplier))
            });
        }

        return result;
    }

    private float GetReligionCraftingOutputMultiplier()
    {
        var religion = PlayerReligionManager.Instance;
        if (religion == null)
            return 1f;

        return Mathf.Max(0f, religion.GetMultiplierProduct(SpiritEffectType.CraftingOutputMultiplier));
    }

    public PlayerCraftingSaveData SaveState()
    {
        PlayerCraftingSaveData data = new PlayerCraftingSaveData();

        CraftingBuildingControl[] buildings = FindObjectsOfType<CraftingBuildingControl>(true);
        for (int i = 0; i < buildings.Length; i++)
        {
            CraftingBuildingControl cbc = buildings[i];
            if (cbc == null)
                continue;

            Saveable saveable = cbc.GetComponent<Saveable>();
            if (saveable == null)
                saveable = cbc.GetComponentInParent<Saveable>();

            if (saveable == null || string.IsNullOrWhiteSpace(saveable.uniqueID))
                continue;

            List<ActiveCraftOrderSaveData> orders = cbc.CaptureActiveOrders(saveable.uniqueID);
            if (orders != null && orders.Count > 0)
                data.activeOrders.AddRange(orders);
        }

        CraftingBuildingControl.CraftingCompletion[] queued = _pending.ToArray();
        for (int i = 0; i < queued.Length; i++)
        {
            var cc = queued[i];

            Saveable saveable = cc.source != null ? cc.source.GetComponent<Saveable>() : null;
            if (saveable == null && cc.source != null)
                saveable = cc.source.GetComponentInParent<Saveable>();

            PendingCraftCompletionSaveData saved = new PendingCraftCompletionSaveData
            {
                sourceBuildingSaveableID = saveable != null ? saveable.uniqueID : null,
                orderId = cc.orderId,
                reservationId = cc.reservationId,
                xpAward = cc.xpAward
            };

            if (cc.payout != null)
            {
                for (int j = 0; j < cc.payout.Count; j++)
                {
                    var a = cc.payout[j];
                    if (a == null || a.resource == null || a.amount <= 0)
                        continue;

                    saved.payout.Add(new CraftingPayoutSaveData
                    {
                        resourceID = a.resource.resourceID,
                        amount = a.amount
                    });
                }
            }

            data.pendingCompletions.Add(saved);
        }

        return data;
    }

    public void LoadState(PlayerCraftingSaveData data)
    {
        if (_processCo != null)
        {
            StopCoroutine(_processCo);
            _processCo = null;
        }

        _pending.Clear();

        CraftingBuildingControl[] buildings = FindObjectsOfType<CraftingBuildingControl>(true);
        Dictionary<string, CraftingBuildingControl> bySaveableId = new Dictionary<string, CraftingBuildingControl>(StringComparer.Ordinal);

        for (int i = 0; i < buildings.Length; i++)
        {
            CraftingBuildingControl cbc = buildings[i];
            if (cbc == null)
                continue;

            cbc.ClearOrdersForLoad();

            Saveable saveable = cbc.GetComponent<Saveable>();
            if (saveable == null)
                saveable = cbc.GetComponentInParent<Saveable>();

            if (saveable != null &&
                !string.IsNullOrWhiteSpace(saveable.uniqueID) &&
                !bySaveableId.ContainsKey(saveable.uniqueID))
            {
                bySaveableId.Add(saveable.uniqueID, cbc);
            }
        }

        var pop = PlayersPopulationManager.Instance;

        if (data != null && data.activeOrders != null)
        {
            for (int i = 0; i < data.activeOrders.Count; i++)
            {
                ActiveCraftOrderSaveData saved = data.activeOrders[i];
                if (saved == null || string.IsNullOrWhiteSpace(saved.buildingSaveableID))
                    continue;

                if (!bySaveableId.TryGetValue(saved.buildingSaveableID, out CraftingBuildingControl cbc) || cbc == null)
                {
                    //Debug.LogWarning($"[Crafting] Could not resolve building '{saved.buildingSaveableID}' for active craft order '{saved.orderId}'.");
                    continue;
                }

                cbc.AddLoadedOrder(saved, ResolveRecipeByID, ResolveResourceByID);

                if (pop != null && !string.IsNullOrWhiteSpace(saved.reservationId))
                {
                    pop.UpdateReservationMetadata(
                        saved.reservationId,
                        PopulationReservationKind.Crafting,
                        GetReservationOwnerId(cbc),
                        nameof(CraftingBuildingControl));
                }
            }
        }

        if (data != null && data.pendingCompletions != null)
        {
            for (int i = 0; i < data.pendingCompletions.Count; i++)
            {
                PendingCraftCompletionSaveData saved = data.pendingCompletions[i];
                if (saved == null)
                    continue;

                bySaveableId.TryGetValue(saved.sourceBuildingSaveableID ?? string.Empty, out CraftingBuildingControl source);

                List<ResourceAmount> payout = new List<ResourceAmount>();
                if (saved.payout != null)
                {
                    for (int j = 0; j < saved.payout.Count; j++)
                    {
                        CraftingPayoutSaveData p = saved.payout[j];
                        if (p == null || string.IsNullOrWhiteSpace(p.resourceID) || p.amount <= 0)
                            continue;

                        ResourceDefinition def = ResolveResourceByID(p.resourceID);
                        if (def == null)
                            continue;

                        payout.Add(new ResourceAmount
                        {
                            resource = def,
                            amount = p.amount
                        });
                    }
                }

                if (pop != null && !string.IsNullOrWhiteSpace(saved.reservationId))
                {
                    pop.UpdateReservationMetadata(
                        saved.reservationId,
                        PopulationReservationKind.Crafting,
                        source != null ? GetReservationOwnerId(source) : saved.sourceBuildingSaveableID,
                        nameof(CraftingBuildingControl));
                }

                _pending.Enqueue(new CraftingBuildingControl.CraftingCompletion
                {
                    source = source,
                    orderId = saved.orderId,
                    reservationId = saved.reservationId,
                    payout = payout,
                    xpAward = saved.xpAward
                });
            }
        }

        if (_processCo == null && _pending.Count > 0)
            _processCo = StartCoroutine(ProcessCompletions());
    }

    private static CraftingRecipe ResolveRecipeByID(string craftingID)
    {
        if (string.IsNullOrWhiteSpace(craftingID))
            return null;

        return CraftingRecipeManager.Instance != null
            ? CraftingRecipeManager.Instance.GetByID(craftingID.Trim())
            : null;
    }

    private static ResourceDefinition ResolveResourceByID(string resourceID)
    {
        if (string.IsNullOrWhiteSpace(resourceID))
            return null;

        if (_resourceById == null)
        {
            _resourceById = new Dictionary<string, ResourceDefinition>(StringComparer.Ordinal);
            ResourceDefinition[] defs = Resources.LoadAll<ResourceDefinition>(string.Empty);

            for (int i = 0; i < defs.Length; i++)
            {
                ResourceDefinition def = defs[i];
                if (def == null || string.IsNullOrWhiteSpace(def.resourceID))
                    continue;

                string id = def.resourceID.Trim();
                if (!_resourceById.ContainsKey(id))
                    _resourceById.Add(id, def);
            }
        }

        _resourceById.TryGetValue(resourceID.Trim(), out ResourceDefinition result);
        return result;
    }
}

static class ListPool<T>
{
    static readonly Stack<List<T>> _pool = new();

    public static List<T> Get()
    {
        return _pool.Count > 0 ? _pool.Pop() : new List<T>(8);
    }

    public static void Release(List<T> list)
    {
        list.Clear();
        _pool.Push(list);
    }
}
