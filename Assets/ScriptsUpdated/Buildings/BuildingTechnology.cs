using System.Collections.Generic;
using UnityEngine;

public class BuildingTechnology : MonoBehaviour
{
    private BuildingControl building;

    private List<Technology> allForThisBuilding = new List<Technology>();
    public IReadOnlyList<Technology> AllForThisBuilding => allForThisBuilding;

    [SerializeField] private List<string> allForThisBuildingIds = new List<string>();
    public IReadOnlyList<string> AllForThisBuildingIds => allForThisBuildingIds;

    [SerializeField] private List<string> availableAtPlayerLevelIds = new List<string>();
    public IReadOnlyList<string> AvailableAtPlayerLevelIds => availableAtPlayerLevelIds;

    // Cached result — rebuilt only when tech list changes
    private readonly List<Technology> _availableAtPlayerLevelCache = new();
    private bool _availableCacheDirty = true;

    // Cached comparer — avoids allocation per sort
    private static readonly TechLevelComparer s_comparer = new TechLevelComparer();

    private sealed class TechLevelComparer : IComparer<Technology>
    {
        public int Compare(Technology x, Technology y)
        {
            if (x == null || y == null) return 0;
            int cmp = x.requiredPlayerLevel.CompareTo(y.requiredPlayerLevel);
            if (cmp != 0) return cmp;
            string nx = x.techName ?? x.techID ?? string.Empty;
            string ny = y.techName ?? y.techID ?? string.Empty;
            return string.Compare(nx, ny, System.StringComparison.Ordinal);
        }
    }

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
        if (PlayerLevel.Instance != null)
            PlayerLevel.Instance.OnLevelUp += HandlePlayerLevelUp;
    }

    private void OnDisable()
    {
        if (PlayerKnownTechnologyManager.Instance != null)
            PlayerKnownTechnologyManager.Instance.OnKnownTechnologyChanged -= HandleKnownTechChanged;
        if (PlayerLevel.Instance != null)
            PlayerLevel.Instance.OnLevelUp -= HandlePlayerLevelUp;
    }

    private void HandleKnownTechChanged()
    {
        BuildCache();
        RefreshInspectorLists();
    }

    private void HandlePlayerLevelUp(int newLevel)
    {
        _availableCacheDirty = true;
        RefreshInspectorLists();
    }

    public void BuildCache()
    {
        allForThisBuilding.Clear();
        _availableCacheDirty = true;

        var tm = TechnologyManager.Instance;
        if (!tm || building == null || string.IsNullOrWhiteSpace(building.buildingID)) return;

        var all = tm.GetAll();
        if (all == null) return;

        var knownMgr = PlayerKnownTechnologyManager.Instance;

        foreach (var t in all)
        {
            if (t == null) continue;
            if (knownMgr != null && !knownMgr.IsKnown(t.techID)) continue;
            if (t.IsResearchableBy(building.buildingID))
                allForThisBuilding.Add(t);
        }

        // Sort in-place — no LINQ allocation
        allForThisBuilding.Sort(s_comparer);
    }

    public List<Technology> GetAvailableAtPlayerLevel()
    {
        if (!_availableCacheDirty)
            return _availableAtPlayerLevelCache;

        int playerLevel = PlayerLevel.Instance ? PlayerLevel.Instance.GetCurrentLevel() : 1;
        var knownMgr = PlayerKnownTechnologyManager.Instance;

        _availableAtPlayerLevelCache.Clear();
        for (int i = 0; i < allForThisBuilding.Count; i++)
        {
            var t = allForThisBuilding[i];
            if (t == null) continue;
            if (knownMgr != null && !knownMgr.IsKnown(t.techID)) continue;
            if (t.IsEligibleForLevel(playerLevel))
                _availableAtPlayerLevelCache.Add(t);
        }

        _availableCacheDirty = false;
        return _availableAtPlayerLevelCache;
    }

    private void RefreshInspectorLists()
    {
        // Rebuild IDs without LINQ
        allForThisBuildingIds.Clear();
        for (int i = 0; i < allForThisBuilding.Count; i++)
        {
            var t = allForThisBuilding[i];
            if (t != null && !string.IsNullOrWhiteSpace(t.techID))
                allForThisBuildingIds.Add(t.techID);
        }

        var byLevel = GetAvailableAtPlayerLevel();
        availableAtPlayerLevelIds.Clear();
        for (int i = 0; i < byLevel.Count; i++)
        {
            var t = byLevel[i];
            if (t != null && !string.IsNullOrWhiteSpace(t.techID))
                availableAtPlayerLevelIds.Add(t.techID);
        }
    }
}
