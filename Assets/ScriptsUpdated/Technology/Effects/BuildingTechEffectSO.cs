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
}
