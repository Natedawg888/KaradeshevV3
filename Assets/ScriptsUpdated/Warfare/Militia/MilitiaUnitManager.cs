using System;
using System.Collections.Generic;
using UnityEngine;

public class MilitiaUnitManager : MonoBehaviour
{
    public static MilitiaUnitManager Instance { get; private set; }

    [Header("All Militia Units")]
    [Tooltip("All possible militia unit types (ScriptableObjects).")]
    [SerializeField] private List<MilitiaUnit> allUnits = new();

    private readonly Dictionary<string, MilitiaUnit> _byId = new();

    public event Action OnUnitsChanged;
   
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        RebuildLookup();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            RebuildLookup();
        }
    }

    private void RebuildLookup()
    {
        _byId.Clear();
        if (allUnits == null) return;

        foreach (var u in allUnits)
        {
            if (u == null) continue;

            if (string.IsNullOrWhiteSpace(u.unitID))
            {
                Debug.LogWarning($"[MilitiaUnitManager] Unit '{u.unitName}' has empty unitID.");
                continue;
            }

            if (_byId.ContainsKey(u.unitID))
            {
                Debug.LogWarning($"[MilitiaUnitManager] Duplicate unitID '{u.unitID}'. Keeping the first.");
                continue;
            }

            _byId.Add(u.unitID, u);
        }
    }

    // ------------- Public API -------------

    public MilitiaUnit GetByID(string unitID)
    {
        if (string.IsNullOrWhiteSpace(unitID)) return null;
        _byId.TryGetValue(unitID, out var unit);
        return unit;
    }

    public IReadOnlyList<MilitiaUnit> GetAllUnits() => allUnits;

    public IReadOnlyList<MilitiaUnit> GetTrainableUnitsForPlayer(MilitiaUnitCategory? categoryFilter = null)
    {
        var list = new List<MilitiaUnit>();

        var knownMgr = PlayerKnownUnitsManager.Instance;
        int playerLevel = PlayerLevel.Instance ? PlayerLevel.Instance.GetCurrentLevel() : int.MaxValue;

        foreach (var u in allUnits)
        {
            if (u == null) continue;

            // Player must "know" the unit (through research, events, etc.)
            if (knownMgr != null && !knownMgr.IsKnown(u)) continue;

            // Optional land/sea/air filter
            if (categoryFilter.HasValue && u.category != categoryFilter.Value) continue;

            list.Add(u);
        }

        return list;
    }

    public IReadOnlyList<MilitiaUnit> GetUnitsByCategory(MilitiaUnitCategory category)
    {
        var list = new List<MilitiaUnit>();
        foreach (var u in allUnits)
        {
            if (u == null) continue;
            if (u.category == category)
                list.Add(u);
        }
        return list;
    }
}