using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Kardashev/Tech Effects/World", fileName = "WorldTechEffect")]
public class WorldTechEffectSO : TechnologyEffectSO
{
    [Header("Known Resources")]
    public List<ResourceDefinition> addKnownResources = new();
    public List<ResourceDefinition> removeKnownResources = new();

    [Header("Known Buildings")]
    public List<string> addKnownBuildingIDs = new();
    public List<string> removeKnownBuildingIDs = new();

    [Header("Known Technology (IDs)")]
    public List<string> addKnownTechnologyIDs = new();
    public List<string> removeKnownTechnologyIDs = new();

    [Header("Known Crafting Recipes (IDs)")]
    public List<string> addKnownCraftingIDs = new();
    public List<string> removeKnownCraftingIDs = new();

    [Header("Known Production Plans (IDs)")]
    public List<string> addKnownProductionIDs = new();
    public List<string> removeKnownProductionIDs = new();

    // ✅ NEW
    [Header("Known Units")]
    public List<MilitiaUnit> addKnownUnits = new();
    public List<MilitiaUnit> removeKnownUnits = new();

    [Header("Known Spirits")]
    public List<SpiritDefinitionSO> addKnownSpirits = new();
    public List<SpiritDefinitionSO> removeKnownSpirits = new();

    [Header("Known Rituals")]
    public List<ReligionRitualDefinitionSO> addKnownRituals = new();
    public List<ReligionRitualDefinitionSO> removeKnownRituals = new();
}