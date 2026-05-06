using System.Collections.Generic;
using UnityEngine;

public class TechnologyManager : MonoBehaviour
{
    public static TechnologyManager Instance { get; private set; }

    [Tooltip("All technologies in the game (author in Inspector).")]
    public List<Technology> allTechnologies = new();

    private Dictionary<string, Technology> _byId = new();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        RebuildLookup();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RebuildLookup();
    }
#endif

    public void RebuildLookup()
    {
        if (_byId == null)
            _byId = new Dictionary<string, Technology>();
        else
            _byId.Clear();

        for (int i = 0; i < allTechnologies.Count; i++)
        {
            var t = allTechnologies[i];
            if (t == null || string.IsNullOrWhiteSpace(t.techID))
                continue;

            _byId[t.techID] = t;
        }
    }

    public Technology GetByID(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        if (_byId == null || _byId.Count == 0)
            RebuildLookup();

        _byId.TryGetValue(id, out var t);
        return t;
    }

    public IReadOnlyList<Technology> GetAll() => allTechnologies;
}