using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PopulationValueEntry
{
    public AgeGroup ageGroup;
    public Gender gender;
    [Min(0f)] public float value = 1f;
}

public class PopulationValueManager : MonoBehaviour
{
    public static PopulationValueManager Instance { get; private set; }

    [SerializeField] private List<PopulationValueEntry> populationValues = new List<PopulationValueEntry>();

    private readonly Dictionary<(AgeGroup, Gender), float> _lookup = new Dictionary<(AgeGroup, Gender), float>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        BuildLookup();
    }

    private void BuildLookup()
    {
        _lookup.Clear();
        if (populationValues == null) return;
        foreach (var e in populationValues)
            if (e != null)
                _lookup[(e.ageGroup, e.gender)] = Mathf.Max(0f, e.value);
    }

    public float GetValue(AgeGroup age, Gender gender)
    {
        return _lookup.TryGetValue((age, gender), out float v) ? v : 0f;
    }
}
