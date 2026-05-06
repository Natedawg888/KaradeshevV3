using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AnimalSpeciesDebugSummary
{
    public string speciesName;
    public AnimalDiet diet;
    public int groupCount;
    public int individualCount;
}

public class AnimalSimulationDebugInspector : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private AnimalSimulationController controller;

    [Header("Refresh")]
    [SerializeField] private bool autoRefreshInPlayMode = true;
    [SerializeField] private float refreshInterval = 0.25f;

    [Header("Summary")]
    [SerializeField] private int totalGroups;
    [SerializeField] private int aliveGroups;
    [SerializeField] private int huntingGroups;
    [SerializeField] private int fleeingGroups;
    [SerializeField] private int conflictGroups;
    [SerializeField] private int targetedGroups;

    [Header("Diet Totals - Groups")]
    [SerializeField] private int totalHerbivoreGroups;
    [SerializeField] private int totalCarnivoreGroups;
    [SerializeField] private int totalOmnivoreGroups;

    [Header("Diet Totals - Individuals")]
    [SerializeField] private int totalHerbivoreIndividuals;
    [SerializeField] private int totalCarnivoreIndividuals;
    [SerializeField] private int totalOmnivoreIndividuals;

    [Header("Species Totals")]
    [SerializeField] private List<AnimalSpeciesDebugSummary> speciesTotals = new();

    [Header("Live Group States")]
    [SerializeField] private List<AnimalGroupDebugSnapshot> groups = new();

    private float _nextRefreshTime;

    private void Reset()
    {
        if (controller == null)
            controller = GetComponent<AnimalSimulationController>();
    }

    private void Awake()
    {
        if (controller == null)
            controller = GetComponent<AnimalSimulationController>();
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        if (!autoRefreshInPlayMode)
            return;

        if (Time.unscaledTime < _nextRefreshTime)
            return;

        RefreshNow();
        _nextRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
    }

    [ContextMenu("Refresh Debug List Now")]
    public void RefreshNow()
    {
        AnimalSimulation sim = null;

        if (controller != null)
            sim = controller.Simulation;

        if (sim == null)
        {
            ClearDebugList();
            return;
        }

        groups = sim.GetDebugSnapshots();
        groups.Sort((a, b) =>
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            return a.id.CompareTo(b.id);
        });

        totalGroups = groups.Count;
        aliveGroups = 0;
        huntingGroups = 0;
        fleeingGroups = 0;
        conflictGroups = 0;
        targetedGroups = 0;

        totalHerbivoreGroups = 0;
        totalCarnivoreGroups = 0;
        totalOmnivoreGroups = 0;

        totalHerbivoreIndividuals = 0;
        totalCarnivoreIndividuals = 0;
        totalOmnivoreIndividuals = 0;

        speciesTotals.Clear();

        var speciesMap = new Dictionary<string, AnimalSpeciesDebugSummary>(StringComparer.Ordinal);

        for (int i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            if (g == null)
                continue;

            if (g.isAlive) aliveGroups++;
            if (g.isHunting) huntingGroups++;
            if (g.isFleeingFromThreat) fleeingGroups++;
            if (g.isInPredatorConflict) conflictGroups++;
            if (g.isTargetedByPredator) targetedGroups++;

            AnimalDiet parsedDiet = ParseDietFromSnapshot(g);

            switch (parsedDiet)
            {
                case AnimalDiet.Herbivore:
                    totalHerbivoreGroups++;
                    totalHerbivoreIndividuals += Mathf.Max(0, g.size);
                    break;

                case AnimalDiet.Carnivore:
                    totalCarnivoreGroups++;
                    totalCarnivoreIndividuals += Mathf.Max(0, g.size);
                    break;

                case AnimalDiet.Omnivore:
                    totalOmnivoreGroups++;
                    totalOmnivoreIndividuals += Mathf.Max(0, g.size);
                    break;
            }

            string speciesName = string.IsNullOrWhiteSpace(g.speciesName) ? "Unknown" : g.speciesName;

            if (!speciesMap.TryGetValue(speciesName, out var summary))
            {
                summary = new AnimalSpeciesDebugSummary
                {
                    speciesName = speciesName,
                    diet = parsedDiet,
                    groupCount = 0,
                    individualCount = 0
                };

                speciesMap.Add(speciesName, summary);
            }

            summary.groupCount++;
            summary.individualCount += Mathf.Max(0, g.size);
        }

        speciesTotals.AddRange(speciesMap.Values);

        speciesTotals.Sort((a, b) =>
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            int individualsCompare = b.individualCount.CompareTo(a.individualCount);
            if (individualsCompare != 0) return individualsCompare;

            int groupsCompare = b.groupCount.CompareTo(a.groupCount);
            if (groupsCompare != 0) return groupsCompare;

            int dietCompare = a.diet.CompareTo(b.diet);
            if (dietCompare != 0) return dietCompare;

            return string.Compare(a.speciesName, b.speciesName, StringComparison.Ordinal);
        });
    }

    private AnimalDiet ParseDietFromSnapshot(AnimalGroupDebugSnapshot snapshot)
    {
        if (snapshot == null)
            return AnimalDiet.Herbivore;

        if (!string.IsNullOrWhiteSpace(snapshot.dietName) &&
            Enum.TryParse(snapshot.dietName, out AnimalDiet parsed))
        {
            return parsed;
        }

        return AnimalDiet.Herbivore;
    }

    [ContextMenu("Clear Debug List")]
    public void ClearDebugList()
    {
        totalGroups = 0;
        aliveGroups = 0;
        huntingGroups = 0;
        fleeingGroups = 0;
        conflictGroups = 0;
        targetedGroups = 0;

        totalHerbivoreGroups = 0;
        totalCarnivoreGroups = 0;
        totalOmnivoreGroups = 0;

        totalHerbivoreIndividuals = 0;
        totalCarnivoreIndividuals = 0;
        totalOmnivoreIndividuals = 0;

        speciesTotals.Clear();
        groups.Clear();
    }
}