using System.Collections.Generic;
using UnityEngine;

public class PlayerResourceKnowledgeManager : MonoBehaviour
{
    public static PlayerResourceKnowledgeManager Instance { get; private set; }

    private readonly HashSet<string> _knownResourceIDs = new HashSet<string>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    public void Learn(ResourceDefinition def)
    {
        if (def == null || string.IsNullOrEmpty(def.resourceID)) return;
        _knownResourceIDs.Add(def.resourceID);
    }

    public bool IsKnown(ResourceDefinition def)
    {
        if (def == null || string.IsNullOrEmpty(def.resourceID)) return false;
        return _knownResourceIDs.Contains(def.resourceID);
    }
}
