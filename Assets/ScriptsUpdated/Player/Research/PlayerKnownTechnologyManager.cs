using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerKnownTechnologyManager : MonoBehaviour
{
    public static PlayerKnownTechnologyManager Instance { get; private set; }

    [Header("Starting Known Tech (IDs)")]
    [SerializeField] private List<string> startingKnownTechIDs = new();

    private readonly HashSet<string> _known = new(StringComparer.Ordinal);
    public event Action OnKnownTechnologyChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _known.Clear();
        foreach (var id in startingKnownTechIDs)
        {
            var trimmed = string.IsNullOrWhiteSpace(id) ? null : id.Trim();
            if (!string.IsNullOrEmpty(trimmed)) _known.Add(trimmed);
        }
    }

    private void MarkKnowledgeDirty()
    {
        SaveSystem.MarkSectionDirty(SaveSectionKeys.Knowledge);
    }

    // ---- Public API ----
    public bool IsKnown(string techID)
        => !string.IsNullOrWhiteSpace(techID) && _known.Contains(techID);

    public IReadOnlyCollection<string> GetKnownIDs() => _known;

    public bool Learn(string techID)
    {
        if (string.IsNullOrWhiteSpace(techID)) return false;
        if (_known.Add(techID))
        {
            OnKnownTechnologyChanged?.Invoke();
            MarkKnowledgeDirty();
            return true;
        }
        return false;
    }

    public int LearnMany(IEnumerable<string> techIDs)
    {
        if (techIDs == null) return 0;
        int added = 0;
        foreach (var id in techIDs)
        {
            var trimmed = string.IsNullOrWhiteSpace(id) ? null : id.Trim();
            if (!string.IsNullOrEmpty(trimmed) && _known.Add(trimmed))
                added++;
        }
        if (added > 0) OnKnownTechnologyChanged?.Invoke();
        MarkKnowledgeDirty();
        return added;
    }

    public bool Forget(string techID, bool revokeIfResearched = true)
    {
        if (string.IsNullOrWhiteSpace(techID)) return false;
        if (_known.Remove(techID))
        {
            // If this tech had been researched, revoke and remove buffs
            if (revokeIfResearched)
                PlayerResearchManager.Instance?.RevokeResearched(techID, undoBuffs: true);

            OnKnownTechnologyChanged?.Invoke();
            MarkKnowledgeDirty();
            return true;
        }
        return false;
    }

    public int ForgetMany(IEnumerable<string> techIDs, bool revokeIfResearched = true)
    {
        if (techIDs == null) return 0;
        int removed = 0;
        foreach (var id in techIDs)
        {
            var trimmed = string.IsNullOrWhiteSpace(id) ? null : id.Trim();
            if (!string.IsNullOrEmpty(trimmed) && _known.Remove(trimmed))
            {
                removed++;
                if (revokeIfResearched)
                    PlayerResearchManager.Instance?.RevokeResearched(trimmed, undoBuffs: true);
            }
        }
        if (removed > 0) OnKnownTechnologyChanged?.Invoke();
        MarkKnowledgeDirty();
        return removed;
    }

    public PlayerKnownTechnologySaveData SaveState()
    {
        PlayerKnownTechnologySaveData data = new PlayerKnownTechnologySaveData();

        foreach (string id in _known)
        {
            if (!string.IsNullOrWhiteSpace(id))
                data.knownTechnologyIDs.Add(id);
        }

        return data;
    }

    public void LoadState(PlayerKnownTechnologySaveData data)
    {
        if (data == null)
            return;

        _known.Clear();

        if (data.knownTechnologyIDs != null)
        {
            foreach (string rawId in data.knownTechnologyIDs)
            {
                string id = string.IsNullOrWhiteSpace(rawId) ? null : rawId.Trim();
                if (!string.IsNullOrEmpty(id))
                    _known.Add(id);
            }
        }

        OnKnownTechnologyChanged?.Invoke();
    }
}
