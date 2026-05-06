using System;
using System.Collections.Generic;
using UnityEngine;

public class ProductionPlanManager : MonoBehaviour
{
    public static ProductionPlanManager Instance { get; private set; }

    [Tooltip("All production plans in the game (author in Inspector).")]
    [SerializeField]
    private List<ProductionPlan> allPlans = new();

    // ID → plan
    private readonly Dictionary<string, ProductionPlan> _byId =
        new Dictionary<string, ProductionPlan>(StringComparer.Ordinal);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        RebuildCache();
    }

    private void OnValidate()
    {
        // Keep cache fresh when editing in the inspector
        if (Application.isPlaying) return;
        RebuildCache();
    }

    private void RebuildCache()
    {
        _byId.Clear();

        foreach (var p in allPlans)
        {
            if (p == null || string.IsNullOrWhiteSpace(p.productionID))
                continue;

            // Last one wins if duplicate IDs exist – same pattern as CraftingRecipeManager
            _byId[p.productionID] = p;
        }
    }

    // -------- Query API (mirror CraftingRecipeManager) --------

    public IReadOnlyList<ProductionPlan> GetAll() => allPlans;

    public ProductionPlan GetByID(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        _byId.TryGetValue(id, out var plan);
        return plan;
    }

    public bool TryGet(string id, out ProductionPlan plan)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            plan = null;
            return false;
        }

        return _byId.TryGetValue(id, out plan);
    }
}
