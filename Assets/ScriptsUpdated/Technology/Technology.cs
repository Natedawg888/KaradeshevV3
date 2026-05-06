using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Technology
{
    [Header("Identity")]
    public string techID;
    public string techName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Research Requirements")]
    [Min(0)] public int turnsRequired = 1;
    [Min(0)] public int requiredKnowledge = 0;

    // Default level is 0 now
    [Min(0)] public int requiredPlayerLevel = 0;

    [Min(0)] public int requiredPopulation = 0;

    [Header("On Complete")]
    [Min(0)] public int knowledgeReward = 0;
    [Min(0)] public int xpReward = 0;

    [Tooltip("Resources spent up-front to start the research.")]
    public List<ResourceCost> researchCosts = new();

    [Tooltip("Which building IDs are allowed to perform this research. Empty = any building can research.")]
    public List<string> researchableByBuildingIDs = new();

    [Header("Effects")]
    [Tooltip("One technology can have multiple effect ScriptableObjects (Environment, Buildings, Civ, World, Resource Unlocks, etc.).")]
    public List<TechnologyEffectSO> effectSOs = new();

    // No clamping to 1 — allow level 0 requirements
    public bool IsEligibleForLevel(int playerLevel)
        => playerLevel >= Mathf.Max(0, requiredPlayerLevel);

    public bool IsEligibleForKnowledge(int currentKnowledge)
        => currentKnowledge >= Mathf.Max(0, requiredKnowledge);

    public bool IsResearchableBy(string buildingId)
    {
        if (researchableByBuildingIDs == null || researchableByBuildingIDs.Count == 0) return true;
        if (string.IsNullOrEmpty(buildingId)) return false;
        return researchableByBuildingIDs.Contains(buildingId);
    }
}