using System;
using System.Collections.Generic;
using UnityEngine;

public static class ProductionSelectionController
{
    public static event Action<bool>     OnSelectionModeChanged; // true/false
    public static event Action<int, int> OnSelectionProgress;    // picked, max

    // NEW: fired when a selection is successfully completed and
    // the plan has been started on a building.
    public static event Action<ProductionBuildingControl, ProductionPlan> OnSelectionCompleted;

    private static bool                      s_active;
    private static ProductionBuildingControl s_building;
    private static ProductionPlan            s_plan;

    private static readonly HashSet<EnvironmentControl> s_inRangeTiles  = new();
    private static readonly HashSet<EnvironmentControl> s_selectedTiles = new();

    private static int s_maxSelectableTiles;

    public static bool   IsSelectionActive => s_active;
    public static int    SelectedCount     => s_selectedTiles.Count;
    public static int    MaxTiles          => s_maxSelectableTiles;
    public static ProductionPlan            ActivePlan     => s_plan;
    public static ProductionBuildingControl ActiveBuilding => s_building;

    public static IReadOnlyCollection<EnvironmentControl> GetSelectedTiles()
        => s_selectedTiles;

    // -------- COMPLETE SELECTION --------
    public static void CompleteSelection()
    {
        if (!s_active) return;

        int picked = SelectedCount;
        int max    = MaxTiles;

        // Cache before we clear anything, so the event gets valid references.
        var completedBuilding = s_building;
        var completedPlan     = s_plan;

        if (completedBuilding != null && completedPlan != null)
        {
            // Store tiles on the building
            completedBuilding.StoreExtractionTilesForPlan(completedPlan, s_selectedTiles);

            // Actually start the extraction production
            completedBuilding.StartProduction(completedPlan);
        }

        s_active = false;

        //Debug.Log("[ProductionSelection] Selection complete; exiting selection mode.");

        OnSelectionModeChanged?.Invoke(false);
        OnSelectionProgress?.Invoke(picked, max);

        // NEW: notify listeners that we finished selection & started the plan
        if (completedBuilding != null && completedPlan != null)
        {
            OnSelectionCompleted?.Invoke(completedBuilding, completedPlan);
        }

        s_selectedTiles.Clear();
        s_inRangeTiles.Clear();
        s_building           = null;
        s_plan               = null;
        s_maxSelectableTiles = 0;
    }

    // -------- BEGIN SELECTION --------
    public static void BeginSelection(ProductionBuildingControl building, ProductionPlan plan)
    {
        if (building == null || plan == null)
        {
            //Debug.LogWarning("[ProductionSelection] BeginSelection with null building/plan.");
            return;
        }

        if (!plan.isExternalExtractor)
        {
            //Debug.LogWarning("[ProductionSelection] Plan is not an external extractor; no tile picking needed.");
            return;
        }

        s_building = building;
        s_plan     = plan;

        s_selectedTiles.Clear();
        s_inRangeTiles.Clear();

        // 🔥 NEW: force a fresh BFS scan every time selection starts
        building.RefreshEnvironmentTiles();

        var nodes = building.GetResourceNodesInRange();
        if (nodes != null)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null) continue;

                var env = node.GetComponent<EnvironmentControl>()
                        ?? node.GetComponentInParent<EnvironmentControl>();

                if (env == null)
                    continue;

                if (plan.CanExtractFrom(env.environmentType, env.environmentTileType))
                {
                    s_inRangeTiles.Add(env);
                }
            }
        }

        s_maxSelectableTiles = Mathf.Max(0, plan.GetMaxExtractionTiles());

        s_active = true;

        //Debug.Log($"[ProductionSelection] Started selection for plan {plan.productionID}. " +
                //$"BFS tiles={s_inRangeTiles.Count}, Max picks={s_maxSelectableTiles}");

        OnSelectionModeChanged?.Invoke(true);
        OnSelectionProgress?.Invoke(SelectedCount, MaxTiles);
    }

    // -------- CANCEL --------
    public static void CancelSelection(bool keepSelection)
    {
        if (!s_active) return;

        if (!keepSelection)
            s_selectedTiles.Clear();

        s_active             = false;
        s_building           = null;
        s_plan               = null;
        s_inRangeTiles.Clear();
        s_maxSelectableTiles = 0;

        OnSelectionModeChanged?.Invoke(false);
        OnSelectionProgress?.Invoke(0, 0);
    }

    // -------- QUERY: CAN SHOW BUTTON --------
    public static bool CanShowButtonFor(EnvironmentControl env)
    {
        if (!s_active || s_plan == null || env == null) return false;
        if (MaxTiles <= 0) return false;

        // Must be in BFS range (already filtered by plan in BeginSelection)
        if (!s_inRangeTiles.Contains(env)) return false;
        if (!env.IsDiscovered)             return false;

        // Double-check against the plan’s conditions (safe & explicit)
        return s_plan.CanExtractFrom(env.environmentType, env.environmentTileType);
    }

    // -------- TOGGLE TILE --------
    public static bool ToggleTile(EnvironmentControl env)
    {
        if (!s_active || s_plan == null || env == null)
            return false;

        if (!s_inRangeTiles.Contains(env))
            return false;

        if (s_selectedTiles.Contains(env))
        {
            s_selectedTiles.Remove(env);
        }
        else
        {
            if (s_selectedTiles.Count >= MaxTiles)
            {
                //Debug.Log("[ProductionSelection] Already at plan tile cap.");
                return false;
            }

            s_selectedTiles.Add(env);
        }

        OnSelectionProgress?.Invoke(SelectedCount, MaxTiles);

        // Auto-complete when we hit the cap
        if (MaxTiles > 0 && SelectedCount == MaxTiles)
        {
            //Debug.Log("[ProductionSelection] Reached max tiles; auto-completing selection.");
            CompleteSelection();
        }

        return s_selectedTiles.Contains(env);
    }

    public static bool IsTileSelected(EnvironmentControl env)
        => env != null && s_selectedTiles.Contains(env);
}
