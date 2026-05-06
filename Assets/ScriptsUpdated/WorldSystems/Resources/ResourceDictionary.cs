using System;
using System.Collections.Generic;
using UnityEngine;

public class ResourceDictionary : MonoBehaviour
{
    public static ResourceDictionary Instance { get; private set; }

    [Header("All resource definitions (populate via inspector or addressable/load at runtime)")]
    public List<ResourceDefinition> allResources = new();

    // Internal lookups for fast access
    private Dictionary<string, ResourceDefinition> byID;
    private Dictionary<ResourceType, List<ResourceDefinition>> byType;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        BuildLookup();
    }

    /// Recomputes internal lookup tables. Call if you mutate allResources at runtime.
    public void BuildLookup()
    {
        byID = new Dictionary<string, ResourceDefinition>(StringComparer.OrdinalIgnoreCase);
        byType = new Dictionary<ResourceType, List<ResourceDefinition>>();

        foreach (ResourceType rt in Enum.GetValues(typeof(ResourceType)))
            byType[rt] = new List<ResourceDefinition>();

        foreach (var def in allResources)
        {
            if (def == null)
            {
                Debug.LogWarning("ResourceDictionary: null entry in allResources, skipping.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(def.resourceID))
            {
                Debug.LogWarning($"ResourceDefinition '{def.name}' has empty resourceID; skipping registration.");
                continue;
            }

            if (byID.ContainsKey(def.resourceID))
            {
                Debug.LogWarning($"Duplicate ResourceDefinition ID '{def.resourceID}' in '{def.name}'; skipping.");
                continue;
            }

            byID[def.resourceID] = def;
            if (!byType.ContainsKey(def.resourceType))
                byType[def.resourceType] = new List<ResourceDefinition>();
            byType[def.resourceType].Add(def);
        }
    }

    public ResourceDefinition GetByID(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        byID.TryGetValue(id, out var def);
        return def;
    }

    public bool TryGetByID(string id, out ResourceDefinition def)
    {
        if (string.IsNullOrEmpty(id))
        {
            def = null;
            return false;
        }

        return byID.TryGetValue(id, out def);
    }

    public IReadOnlyList<ResourceDefinition> GetByType(ResourceType type)
    {
        if (byType.TryGetValue(type, out var list))
            return list;
        return Array.Empty<ResourceDefinition>();
    }
}