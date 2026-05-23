using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ResourceValueEntry
{
    public ResourceDefinition resource;
    [Min(0f)] public float value = 1f;
}

public class ResourceValueManager : MonoBehaviour
{
    public static ResourceValueManager Instance { get; private set; }

    [SerializeField] private List<ResourceValueEntry> resourceValues = new List<ResourceValueEntry>();

    private Dictionary<ResourceDefinition, float> _lookup;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        BuildLookup();
    }

    private void BuildLookup()
    {
        _lookup = new Dictionary<ResourceDefinition, float>();
        foreach (var e in resourceValues)
            if (e?.resource != null)
                _lookup[e.resource] = Mathf.Max(0f, e.value);
    }

    public float GetValue(ResourceDefinition def)
    {
        if (def == null || _lookup == null) return 0f;
        return _lookup.TryGetValue(def, out float v) ? v : 0f;
    }
}
