using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Kardashev/Tech Effects/Buildings", fileName = "BuildingTechEffect")]
public class BuildingTechEffectSO : TechnologyEffectSO
{
    [Header("Targets")]
    public List<string> targetBuildingIDs = new();

    [Header("Additive Deltas")]
    public int maxHealthDelta = 0;

    public int degenerationAmountDelta = 0;

    public int degenerationIntervalDelta = 0;

    [Header("Production & Crafting Output Bonus")]
    [Tooltip("Production plan IDs that receive the output multiplier when this tech is researched.")]
    public List<string> productionPlanIDs = new();

    [Tooltip("Crafting recipe IDs that receive the output multiplier when this tech is researched.")]
    public List<string> craftingRecipeIDs = new();

    [Min(1f)]
    [Tooltip("Output multiplier for matching plans/recipes. 1.0 = no bonus, 1.25 = 25% more output.")]
    public float outputMultiplier = 1f;
}
