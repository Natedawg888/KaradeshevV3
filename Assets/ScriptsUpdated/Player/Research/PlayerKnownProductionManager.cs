using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerKnownProductionManager : MonoBehaviour
{
    public static PlayerKnownProductionManager Instance { get; private set; }

    [Header("Starting Known Production Plans (IDs)")]
    [SerializeField] private List<string> startingKnownProductionIDs = new();

    private readonly HashSet<string> _known = new(StringComparer.Ordinal);

    public event Action OnKnownProductionChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _known.Clear();
        foreach (var id in startingKnownProductionIDs)
        {
            var trimmed = string.IsNullOrWhiteSpace(id) ? null : id.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                _known.Add(trimmed);
        }
    }

    private void MarkKnowledgeDirty()
    {
        SaveSystem.MarkSectionDirty(SaveSectionKeys.Knowledge);
        SaveSystem.MarkSectionDirty(SaveSectionKeys.WorldObjects);
    }

    // ---- Public API ----

    public bool IsKnown(string productionID)
        => !string.IsNullOrWhiteSpace(productionID) && _known.Contains(productionID);

    public IReadOnlyCollection<string> GetKnownIDs() => _known;

    public List<ProductionPlan> GetKnownPlans()
    {
        var mgr = ProductionPlanManager.Instance;
        if (!mgr) return new List<ProductionPlan>();

        return _known
            .Select(id => mgr.GetByID(id))
            .Where(p => p != null)
            .ToList();
    }

    public bool Learn(string productionID)
    {
        var trimmed = string.IsNullOrWhiteSpace(productionID) ? null : productionID.Trim();
        if (string.IsNullOrEmpty(trimmed)) return false;

        if (_known.Add(trimmed))
        {
            OnKnownProductionChanged?.Invoke();
            MarkKnowledgeDirty();
            return true;
        }
        return false;
    }

    public int LearnMany(IEnumerable<string> productionIDs)
    {
        if (productionIDs == null) return 0;

        int added = 0;
        foreach (var id in productionIDs)
        {
            var trimmed = string.IsNullOrWhiteSpace(id) ? null : id.Trim();
            if (!string.IsNullOrEmpty(trimmed) && _known.Add(trimmed))
                added++;
        }

        if (added > 0)
            OnKnownProductionChanged?.Invoke();

        MarkKnowledgeDirty();
        return added;
    }

    public bool Forget(string productionID)
    {
        var trimmed = string.IsNullOrWhiteSpace(productionID) ? null : productionID.Trim();
        if (string.IsNullOrEmpty(trimmed)) return false;

        if (_known.Remove(trimmed))
        {
            OnKnownProductionChanged?.Invoke();
            MarkKnowledgeDirty();
            return true;
        }
        return false;
    }

    public int ForgetMany(IEnumerable<string> productionIDs)
    {
        if (productionIDs == null) return 0;

        int removed = 0;
        foreach (var id in productionIDs)
        {
            var trimmed = string.IsNullOrWhiteSpace(id) ? null : id.Trim();
            if (!string.IsNullOrEmpty(trimmed) && _known.Remove(trimmed))
                removed++;
        }

        if (removed > 0)
            OnKnownProductionChanged?.Invoke();
        MarkKnowledgeDirty();
        return removed;
    }

    public PlayerKnownProductionSaveData SaveState()
    {
        PlayerKnownProductionSaveData data = new PlayerKnownProductionSaveData();

        foreach (string id in _known)
        {
            if (!string.IsNullOrWhiteSpace(id))
                data.knownProductionIDs.Add(id);
        }

        return data;
    }

    public void LoadState(PlayerKnownProductionSaveData data)
    {
        if (data == null)
            return;

        _known.Clear();

        if (data.knownProductionIDs != null)
        {
            foreach (string rawId in data.knownProductionIDs)
            {
                string id = string.IsNullOrWhiteSpace(rawId) ? null : rawId.Trim();
                if (!string.IsNullOrEmpty(id))
                    _known.Add(id);
            }
        }

        OnKnownProductionChanged?.Invoke();
    }
}
