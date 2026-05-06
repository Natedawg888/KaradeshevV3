using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerKnownCraftingManager : MonoBehaviour
{
    public static PlayerKnownCraftingManager Instance { get; private set; }

    [Header("Starting Known Crafting (IDs)")]
    [SerializeField] private List<string> startingKnownCraftingIDs = new();

    private readonly HashSet<string> _known = new(StringComparer.Ordinal);
    public event Action OnKnownCraftingChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _known.Clear();
        foreach (var id in startingKnownCraftingIDs)
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
    public bool IsKnown(string craftingID)
        => !string.IsNullOrWhiteSpace(craftingID) && _known.Contains(craftingID);

    public IReadOnlyCollection<string> GetKnownIDs() => _known;

    public List<CraftingRecipe> GetKnownRecipes()
    {
        var mgr = CraftingRecipeManager.Instance;
        if (!mgr) return new();
        return _known.Select(id => mgr.GetByID(id)).Where(r => r != null).ToList();
    }

    public bool Learn(string craftingID)
    {
        if (string.IsNullOrWhiteSpace(craftingID)) return false;
        if (_known.Add(craftingID))
        {
            MarkKnowledgeDirty();
            OnKnownCraftingChanged?.Invoke();
            return true;
        }
        return false;
    }

    public int LearnMany(IEnumerable<string> craftingIDs)
    {
        if (craftingIDs == null) return 0;
        int added = 0;
        foreach (var id in craftingIDs)
        {
            var trimmed = string.IsNullOrWhiteSpace(id) ? null : id.Trim();
            if (!string.IsNullOrEmpty(trimmed) && _known.Add(trimmed))
                added++;
        }
        if (added > 0) OnKnownCraftingChanged?.Invoke();
        MarkKnowledgeDirty();
        return added;
    }

    public bool Forget(string craftingID)
    {
        if (string.IsNullOrWhiteSpace(craftingID)) return false;
        if (_known.Remove(craftingID))
        {
            // Optional: cancel active crafting orders using this recipe (if you implement such tracking)
            OnKnownCraftingChanged?.Invoke();
            MarkKnowledgeDirty();
            return true;
        }
        return false;
    }

    public int ForgetMany(IEnumerable<string> craftingIDs)
    {
        if (craftingIDs == null) return 0;
        int removed = 0;
        foreach (var id in craftingIDs)
        {
            var trimmed = string.IsNullOrWhiteSpace(id) ? null : id.Trim();
            if (!string.IsNullOrEmpty(trimmed) && _known.Remove(trimmed))
                removed++;
        }
        if (removed > 0) OnKnownCraftingChanged?.Invoke();
        MarkKnowledgeDirty();
        return removed;
    }

    public PlayerKnownCraftingSaveData SaveState()
    {
        PlayerKnownCraftingSaveData data = new PlayerKnownCraftingSaveData();

        foreach (string id in _known)
        {
            if (!string.IsNullOrWhiteSpace(id))
                data.knownCraftingIDs.Add(id);
        }

        return data;
    }

    public void LoadState(PlayerKnownCraftingSaveData data)
    {
        if (data == null)
            return;

        _known.Clear();

        if (data.knownCraftingIDs != null)
        {
            foreach (string rawId in data.knownCraftingIDs)
            {
                string id = string.IsNullOrWhiteSpace(rawId) ? null : rawId.Trim();
                if (!string.IsNullOrEmpty(id))
                    _known.Add(id);
            }
        }

        OnKnownCraftingChanged?.Invoke();
    }
}
