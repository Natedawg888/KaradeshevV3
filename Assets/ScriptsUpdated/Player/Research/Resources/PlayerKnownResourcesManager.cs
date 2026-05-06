using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerKnownResourcesManager : MonoBehaviour
{
    public static PlayerKnownResourcesManager Instance { get; private set; }

    [Header("Starting Known Resources")]
    [Tooltip("Drag the ResourceDefinition assets the civilization knows from game start.")]
    [SerializeField] private List<ResourceDefinition> startingKnown = new();

    // Fast lookup by asset instance (and optional id string if you have one)
    private readonly HashSet<ResourceDefinition> _known = new();
    private readonly HashSet<string> _knownIds = new();

    public event Action OnKnownChanged;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        ResetToStarting();
    }

    // --- Public API ---
    private void MarkKnowledgeDirty()
    {
        SaveSystem.MarkSectionDirty(SaveSectionKeys.Knowledge);
    }

    public bool IsKnown(ResourceDefinition def)
        => def != null && (_known.Contains(def) || (!string.IsNullOrEmpty(def.resourceID) && _knownIds.Contains(def.resourceID)));

    public void Learn(ResourceDefinition def)
    {
        if (def == null) return;
        bool added = _known.Add(def);
        if (!string.IsNullOrEmpty(def.resourceID))
            added |= _knownIds.Add(def.resourceID);
        if (added) OnKnownChanged?.Invoke();
        MarkKnowledgeDirty();
    }

    public void LearnMany(IEnumerable<ResourceDefinition> defs)
    {
        bool any = false;
        if (defs != null)
        {
            foreach (var d in defs)
            {
                if (d == null) continue;
                if (_known.Add(d)) any = true;
                if (!string.IsNullOrEmpty(d.resourceID) && _knownIds.Add(d.resourceID)) any = true;
            }
        }
        if (any) OnKnownChanged?.Invoke();
        MarkKnowledgeDirty();
    }

    public void Forget(ResourceDefinition def)
    {
        if (def == null) return;
        bool removed = _known.Remove(def);
        if (!string.IsNullOrEmpty(def.resourceID))
            removed |= _knownIds.Remove(def.resourceID);
        if (removed) OnKnownChanged?.Invoke();
        MarkKnowledgeDirty();
    }

    public void ForgetMany(IEnumerable<ResourceDefinition> defs)
    {
        bool any = false;
        if (defs != null)
        {
            foreach (var d in defs)
            {
                if (d == null) continue;
                if (_known.Remove(d)) any = true;
                if (!string.IsNullOrEmpty(d.resourceID) && _knownIds.Remove(d.resourceID)) any = true;
            }
        }
        if (any) OnKnownChanged?.Invoke();
        MarkKnowledgeDirty();
    }

    [ContextMenu("Reset To Starting")]
    public void ResetToStarting()
    {
        _known.Clear();
        _knownIds.Clear();
        if (startingKnown != null)
        {
            foreach (var d in startingKnown)
            {
                if (d == null) continue;
                _known.Add(d);
                if (!string.IsNullOrEmpty(d.resourceID))
                    _knownIds.Add(d.resourceID);
            }
        }
        OnKnownChanged?.Invoke();
    }

    // Optional: expose a read-only snapshot (useful for UI/debug)
    public IReadOnlyCollection<ResourceDefinition> GetAllKnown() => _known;

    public PlayerKnownResourcesSaveData SaveState()
    {
        PlayerKnownResourcesSaveData data = new PlayerKnownResourcesSaveData();

        foreach (string id in _knownIds)
        {
            if (!string.IsNullOrWhiteSpace(id))
                data.knownResourceIDs.Add(id);
        }

        return data;
    }

    public void LoadState(PlayerKnownResourcesSaveData data)
    {
        if (data == null)
            return;

        _known.Clear();
        _knownIds.Clear();

        if (data.knownResourceIDs != null)
        {
            foreach (string rawId in data.knownResourceIDs)
            {
                string id = string.IsNullOrWhiteSpace(rawId) ? null : rawId.Trim();
                if (string.IsNullOrEmpty(id))
                    continue;

                _knownIds.Add(id);

                ResourceDefinition def = ResolveResourceByID(id);
                if (def != null)
                    _known.Add(def);
            }
        }

        OnKnownChanged?.Invoke();
    }

    private static Dictionary<string, ResourceDefinition> _resourceLookupById;

    private static ResourceDefinition ResolveResourceByID(string resourceID)
    {
        if (string.IsNullOrWhiteSpace(resourceID))
            return null;

        if (_resourceLookupById == null)
        {
            _resourceLookupById = new Dictionary<string, ResourceDefinition>(StringComparer.Ordinal);

            ResourceDefinition[] allDefs = Resources.LoadAll<ResourceDefinition>(string.Empty);
            foreach (ResourceDefinition def in allDefs)
            {
                if (def == null || string.IsNullOrWhiteSpace(def.resourceID))
                    continue;

                string id = def.resourceID.Trim();
                if (!_resourceLookupById.ContainsKey(id))
                    _resourceLookupById.Add(id, def);
            }
        }

        _resourceLookupById.TryGetValue(resourceID.Trim(), out ResourceDefinition result);
        return result;
    }
}
