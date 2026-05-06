using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BuildingTechnology : MonoBehaviour
{
    private BuildingControl building;

    // Runtime cache of Technology objects for this building
    private List<Technology> allForThisBuilding = new List<Technology>();
    public IReadOnlyList<Technology> AllForThisBuilding => allForThisBuilding;

    // Inspector-visible: store ONLY tech IDs
    [SerializeField] private List<string> allForThisBuildingIds = new List<string>();
    public IReadOnlyList<string> AllForThisBuildingIds => allForThisBuildingIds;

    [SerializeField] private List<string> availableAtPlayerLevelIds = new List<string>();
    public IReadOnlyList<string> AvailableAtPlayerLevelIds => availableAtPlayerLevelIds;

    private void Awake()
    {
        building = GetComponent<BuildingControl>();
    }

    private void Start()
    {
        BuildCache();
        RefreshInspectorLists();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (building == null) building = GetComponent<BuildingControl>();

        if (!Application.isPlaying)
        {
            if (TechnologyManager.Instance && building && !string.IsNullOrWhiteSpace(building.buildingID))
            {
                BuildCache();
                RefreshInspectorLists();
            }
        }
    }
#endif

    [ContextMenu("Rebuild Tech Cache")]
    public void Menu_RebuildTechCache()
    {
        BuildCache();
        RefreshInspectorLists();
    }

    [ContextMenu("Refresh Inspector Lists")]
    public void Menu_RefreshInspectorLists()
    {
        RefreshInspectorLists();
    }

    private void OnEnable()
    {
        if (PlayerKnownTechnologyManager.Instance != null)
            PlayerKnownTechnologyManager.Instance.OnKnownTechnologyChanged += HandleKnownTechChanged;
    }

    private void OnDisable()
    {
        if (PlayerKnownTechnologyManager.Instance != null)
            PlayerKnownTechnologyManager.Instance.OnKnownTechnologyChanged -= HandleKnownTechChanged;
    }

    private void HandleKnownTechChanged()
    {
        BuildCache();
        RefreshInspectorLists();
    }

    /// Cache every Technology this building can research (by buildingID only).
    public void BuildCache()
    {
        allForThisBuilding.Clear();

        var tm = TechnologyManager.Instance;
        if (!tm || building == null || string.IsNullOrWhiteSpace(building.buildingID)) return;

        var all = tm.GetAll();
        if (all == null) return;

        var knownMgr = PlayerKnownTechnologyManager.Instance; // may be null in early boot

        foreach (var t in all)
        {
            if (t == null) continue;

            // NEW: skip if not known (when manager exists)
            if (knownMgr != null && !knownMgr.IsKnown(t.techID)) continue;

            if (t.IsResearchableBy(building.buildingID))
                allForThisBuilding.Add(t);
        }

        // Stable order for determinism (level then name/id)
        allForThisBuilding = allForThisBuilding
            .OrderBy(t => t.requiredPlayerLevel)
            .ThenBy(t => t.techName ?? t.techID)
            .ToList();
    }

    /// Techs visible by **player level** only (no cost/knowledge/pop checks).
    public List<Technology> GetAvailableAtPlayerLevel()
    {
        int playerLevel = PlayerLevel.Instance ? PlayerLevel.Instance.GetCurrentLevel() : 1;
        var knownMgr = PlayerKnownTechnologyManager.Instance;

        return allForThisBuilding
            .Where(t => t != null
                && (knownMgr == null || knownMgr.IsKnown(t.techID)) // NEW safety
                && t.IsEligibleForLevel(playerLevel))
            .ToList();
    }

    /// Mirror to inspector lists with **IDs only**.
    private void RefreshInspectorLists()
    {
        allForThisBuildingIds = allForThisBuilding
            .Where(t => t != null && !string.IsNullOrWhiteSpace(t.techID))
            .Select(t => t.techID)
            .ToList();

        var byLevel = GetAvailableAtPlayerLevel();
        availableAtPlayerLevelIds = byLevel
            .Where(t => t != null && !string.IsNullOrWhiteSpace(t.techID))
            .Select(t => t.techID)
            .ToList();
    }
}