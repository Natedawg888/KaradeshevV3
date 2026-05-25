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

    public float GetProductionPlanOutputMultiplier(string planID)
    {
        if (string.IsNullOrWhiteSpace(planID)) return 1f;
        var research = PlayerResearchManager.Instance;
        if (research == null) return 1f;
        float result = 1f;
        for (int i = 0; i < allTechnologies.Count; i++)
        {
            var tech = allTechnologies[i];
            if (tech == null || !research.IsResearched(tech.techID)) continue;
            for (int j = 0; j < tech.effectSOs.Count; j++)
            {
                if (tech.effectSOs[j] is BuildingTechEffectSO effect &&
                    effect.productionPlanIDs != null &&
                    effect.productionPlanIDs.Contains(planID))
                    result *= Mathf.Max(1f, effect.outputMultiplier);
            }
        }
        return result;
    }

    public float GetCraftingRecipeOutputMultiplier(string recipeID)
    {
        if (string.IsNullOrWhiteSpace(recipeID)) return 1f;
        var research = PlayerResearchManager.Instance;
        if (research == null) return 1f;
        float result = 1f;
        for (int i = 0; i < allTechnologies.Count; i++)
        {
            var tech = allTechnologies[i];
            if (tech == null || !research.IsResearched(tech.techID)) continue;
            for (int j = 0; j < tech.effectSOs.Count; j++)
            {
                if (tech.effectSOs[j] is BuildingTechEffectSO effect &&
                    effect.craftingRecipeIDs != null &&
                    effect.craftingRecipeIDs.Contains(recipeID))
                    result *= Mathf.Max(1f, effect.outputMultiplier);
            }
        }
        return result;
    }
}