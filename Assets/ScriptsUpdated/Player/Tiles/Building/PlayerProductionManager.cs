using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class PlayerProductionManager : MonoBehaviour
{
    public static PlayerProductionManager Instance { get; private set; }

    [Header("Turn Tick Settings")]
    [SerializeField] private bool tickProductionOverMultipleFrames = true;

    [Tooltip("If enabled, production buildings are processed strictly one-by-one, with a frame gap between each building.")]
    [SerializeField] private bool tickOneBuildingPerFrame = true;

    [Tooltip("Only used when Tick One Building Per Frame is disabled.")]
    [SerializeField] private int buildingsPerFrame = 1;

    private readonly List<ProductionBuildingControl> _tracked = new();
    private readonly Queue<List<ProductionBuildingControl>> _pendingTurnTickBatches = new();

    private Coroutine _turnTickCoroutine;

    public bool IsProcessingTurnTick =>
        _turnTickCoroutine != null || _pendingTurnTickBatches.Count > 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        TurnSystem.SubscribeToEndOfTurn(OnTurnEnded);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            TurnSystem.UnsubscribeFromEndOfTurn(OnTurnEnded);
    }

    private void MarkJobsDirty()
    {
        SaveSystem.MarkSectionDirty(SaveSectionKeys.Jobs);
    }

    private void OnTurnEnded()
    {
        TickEndOfTurn();
    }

    #region Legacy registration API

    public void Register(ProductionBuildingControl building)
    {
        if (building == null) return;
        if (!_tracked.Contains(building))
            _tracked.Add(building);
    }

    public void Unregister(ProductionBuildingControl building)
    {
        if (building == null) return;
        _tracked.Remove(building);
    }

    #endregion

    public void TickEndOfTurn()
    {
        if (!tickProductionOverMultipleFrames)
        {
            TickEndOfTurnImmediate();
            MarkJobsDirty();
            return;
        }

        QueueEndOfTurnTick();
    }

    public void TickEndOfTurnImmediate()
    {
        List<ProductionBuildingControl> buildings = CollectProductionBuildings();

        for (int i = 0; i < buildings.Count; i++)
        {
            ProductionBuildingControl building = buildings[i];
            if (building == null)
                continue;

            building.RefreshReservationMetadataFromRuntime();
            building.TickProductionTurn();
        }
    }

    private void QueueEndOfTurnTick()
    {
        List<ProductionBuildingControl> buildings = CollectProductionBuildings();
        _pendingTurnTickBatches.Enqueue(buildings);

        if (_turnTickCoroutine == null)
            _turnTickCoroutine = StartCoroutine(ProcessQueuedTurnTicks());
    }

    private IEnumerator ProcessQueuedTurnTicks()
    {
        while (_pendingTurnTickBatches.Count > 0)
        {
            List<ProductionBuildingControl> batch = _pendingTurnTickBatches.Dequeue();

            if (tickOneBuildingPerFrame)
            {
                for (int i = 0; i < batch.Count; i++)
                {
                    ProductionBuildingControl building = batch[i];
                    if (building == null)
                        continue;

                    building.RefreshReservationMetadataFromRuntime();
                    building.TickProductionTurn();

                    if (i < batch.Count - 1)
                        yield return null;
                }
            }
            else
            {
                int processedThisFrame = 0;
                int maxPerFrame = Mathf.Max(1, buildingsPerFrame);

                for (int i = 0; i < batch.Count; i++)
                {
                    ProductionBuildingControl building = batch[i];
                    if (building == null)
                        continue;

                    building.RefreshReservationMetadataFromRuntime();
                    building.TickProductionTurn();
                    processedThisFrame++;

                    if (processedThisFrame >= maxPerFrame)
                    {
                        processedThisFrame = 0;

                        if (i < batch.Count - 1)
                            yield return null;
                    }
                }
            }

            MarkJobsDirty();

            if (_pendingTurnTickBatches.Count > 0)
                yield return null;
        }

        _turnTickCoroutine = null;
    }

    private List<ProductionBuildingControl> CollectProductionBuildings()
    {
        var results = new List<ProductionBuildingControl>();

        var pbm = PlayerBuildingManager.Instance;

        if (pbm != null)
        {
            var allRecords = pbm.GetAll();

            if (allRecords != null)
            {
                for (int i = 0; i < allRecords.Count; i++)
                {
                    var rec = allRecords[i];
                    if (rec == null || rec.instance == null)
                        continue;

                    var prod = rec.instance.GetComponent<ProductionBuildingControl>();
                    if (prod == null)
                        continue;

                    results.Add(prod);
                }
            }
        }
        else
        {
            for (int i = _tracked.Count - 1; i >= 0; i--)
            {
                var b = _tracked[i];
                if (b == null)
                {
                    _tracked.RemoveAt(i);
                    continue;
                }

                results.Add(b);
            }
        }

        return results;
    }

    public void RetagAllActiveProductionReservations()
    {
        List<ProductionBuildingControl> buildings = CollectProductionBuildings();

        for (int i = 0; i < buildings.Count; i++)
        {
            var building = buildings[i];
            if (building == null)
                continue;

            building.RefreshReservationMetadataFromRuntime();
        }
    }

    public bool OnProductionCycleCompleted(ProductionBuildingControl building, ProductionPlan plan)
    {
        if (!ApplyCompletedCycleResults(building, plan))
            return false;

        bool canContinue = TryConsumeRunningCostsForNextCycle(plan);

        if (!canContinue)
        {
            building.PauseForLackOfResources();
            return false;
        }

        return true;
    }

    public bool ApplyCompletedCycleResults(ProductionBuildingControl building, ProductionPlan plan)
    {
        if (building == null || plan == null)
            return false;

        building.RefreshReservationMetadataFromRuntime();

        var inv = PlayerInventoryManager.Instance;
        if (inv == null)
            return false;

        float buildingOutputMultiplier = building.GetCompletedCycleOutputMultiplier();
        var outs = GetFinalAdjustedOutputs(plan, buildingOutputMultiplier);
        if (outs != null)
        {
            foreach (var o in outs)
            {
                if (o == null || o.resource == null || o.amountPerCycle <= 0)
                    continue;

                inv.TryAdd(o.resource, o.amountPerCycle);
            }
        }

        int dmg = Mathf.Max(0, plan.environmentDamagePerCycle);
        if (dmg > 0)
        {
            var tiles = building.GetExtractionTilesForPlan(plan);
            foreach (var env in tiles)
            {
                if (env == null) continue;

                var node = env.GetComponent<EnvironmentResourceNode>();
                if (node != null)
                    node.ApplyEnvironmentDamage(dmg);
            }
        }

        if (plan.xpPerCompletedCycle > 0)
            PlayerLevel.Instance?.AddXP(plan.xpPerCompletedCycle);

        building.ApplyFatalitiesForCompletedCycle();
        return true;
    }

    public bool TryConsumeRunningCostsForNextCycle(ProductionPlan plan)
    {
        if (plan == null)
            return false;

        var inv = PlayerInventoryManager.Instance;
        if (inv == null)
            return false;

        return TryConsumeRunningCostsForNextCycle(inv, plan);
    }

    public bool CanFinalizeCompletedCycle(ProductionBuildingControl building, ProductionPlan plan)
    {
        if (building == null || plan == null)
            return false;

        var inv = PlayerInventoryManager.Instance;
        if (inv == null)
            return false;

        float buildingOutputMultiplier = building.GetCompletedCycleOutputMultiplier();
        var outs = GetFinalAdjustedOutputs(plan, buildingOutputMultiplier);
        if (outs == null || outs.Count == 0)
            return true;

        foreach (var o in outs)
        {
            if (o == null || o.resource == null || o.amountPerCycle <= 0)
                continue;

            if (!CanInventoryAcceptOutput(inv, o.resource, o.amountPerCycle))
                return false;
        }

        return true;
    }

    private IReadOnlyList<ProductionResourceAmount> GetFinalAdjustedOutputs(
    ProductionPlan plan,
    float buildingOutputMultiplier = 1f)
    {
        if (plan == null)
            return Array.Empty<ProductionResourceAmount>();

        var baseOutputs = plan.GetSeasonAdjustedOutputs();
        if (baseOutputs == null || baseOutputs.Count == 0)
            return baseOutputs;

        float religionMultiplier = GetReligionProductionOutputMultiplier();
        float finalMultiplier = religionMultiplier * Mathf.Max(0f, buildingOutputMultiplier);

        if (Mathf.Approximately(finalMultiplier, 1f))
            return baseOutputs;

        var adjusted = new List<ProductionResourceAmount>(baseOutputs.Count);

        for (int i = 0; i < baseOutputs.Count; i++)
        {
            var o = baseOutputs[i];
            if (o == null || o.resource == null)
                continue;

            adjusted.Add(new ProductionResourceAmount
            {
                resource = o.resource,
                amountPerCycle = Mathf.Max(0, Mathf.RoundToInt(o.amountPerCycle * finalMultiplier))
            });
        }

        return adjusted;
    }

    private float GetReligionProductionOutputMultiplier()
    {
        var religion = PlayerReligionManager.Instance;
        if (religion == null)
            return 1f;

        return Mathf.Max(0f, religion.GetMultiplierProduct(SpiritEffectType.ProductionOutputMultiplier));
    }

    private bool TryConsumeRunningCostsForNextCycle(PlayerInventoryManager inv, ProductionPlan plan)
    {
        var costs = plan.GetActiveRunningCosts();
        if (costs == null || costs.Count == 0)
            return true;

        foreach (var c in costs)
        {
            if (c == null || c.resource == null || c.amountPerCycle <= 0)
                continue;

            int owned = InventoryQuery.GetOwned(c.resource);
            if (owned < c.amountPerCycle)
                return false;
        }

        foreach (var c in costs)
        {
            if (c == null || c.resource == null || c.amountPerCycle <= 0)
                continue;

            bool removed;

            if (!c.resource.isGroup)
                removed = inv.TryRemove(c.resource, c.amountPerCycle);
            else
                removed = inv.TryRemoveGroup(c.resource, c.amountPerCycle);

            if (!removed)
                return false;
        }

        return true;
    }

    private bool CanInventoryAcceptOutput(PlayerInventoryManager inv, ResourceDefinition resource, int amount)
    {
        if (inv == null || resource == null || amount <= 0)
            return false;

        Type t = inv.GetType();

        string[] candidateMethodNames =
        {
            "CanAdd",
            "HasSpaceFor",
            "CanAccept",
            "CanFit"
        };

        for (int i = 0; i < candidateMethodNames.Length; i++)
        {
            MethodInfo mi = t.GetMethod(candidateMethodNames[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null)
                continue;

            ParameterInfo[] ps = mi.GetParameters();
            if (ps.Length == 2 &&
                ps[0].ParameterType == typeof(ResourceDefinition) &&
                ps[1].ParameterType == typeof(int) &&
                mi.ReturnType == typeof(bool))
            {
                try
                {
                    object result = mi.Invoke(inv, new object[] { resource, amount });
                    if (result is bool b)
                        return b;
                }
                catch { }
            }
        }

        return true;
    }
}