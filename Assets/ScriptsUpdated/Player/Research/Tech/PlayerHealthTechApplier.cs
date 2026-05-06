using System.Collections.Generic;
using UnityEngine;

public class PlayerHealthTechApplier : MonoBehaviour
{
    public static PlayerHealthTechApplier Instance { get; private set; }

    private Dictionary<string, List<HealthTechEffectSO>> _byTech;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildCache();
    }

    private void BuildCache()
    {
        _byTech = new Dictionary<string, List<HealthTechEffectSO>>();
        var all = Resources.LoadAll<HealthTechEffectSO>("") ?? new HealthTechEffectSO[0];
        foreach (var so in all)
        {
            if (!so || string.IsNullOrWhiteSpace(so.techID)) continue;
            if (!_byTech.TryGetValue(so.techID, out var list))
            {
                list = new List<HealthTechEffectSO>();
                _byTech[so.techID] = list;
            }
            list.Add(so);
        }
    }

    public void ApplyFor(string techID)
    {
        if (string.IsNullOrWhiteSpace(techID)) return;
        if (_byTech == null) BuildCache();
        if (!_byTech.TryGetValue(techID, out var list) || list.Count == 0) return;

        var rules = PlayerHealthRulebook.Instance;
        if (rules == null) { Debug.LogWarning("[PlayerHealthTechApplier] Missing PlayerHealthRulebook."); return; }

        int dCH=0, dTH=0, dAH=0, dEH=0;
        int dC2T=0, dT2A=0, dA2E=0, dLife=0;
        float dCR=0, dTR=0, dAR=0, dER=0;

        float dResC=0f, dResT=0f, dResA=0f, dResE=0f;

        // NEW mortality deltas
        float dLowH=0f, dMort0=0f, dElderStart=0f, dElderLife=0f;

        FamilySimConfig replaceCfg = null;
        List<FamilySimConfigPatch> patches = new();

        foreach (var so in list)
        {
            dCH += so.baseChildHealthDelta; dTH += so.baseTeenHealthDelta;
            dAH += so.baseAdultHealthDelta; dEH += so.baseElderHealthDelta;

            dC2T += so.childToTeenAgeDelta; dT2A += so.teenToAdultAgeDelta;
            dA2E += so.adultToElderAgeDelta; dLife += so.lifespanDelta;

            dCR += so.childRecoveryDelta; dTR += so.teenRecoveryDelta;
            dAR += so.adultRecoveryDelta; dER += so.elderRecoveryDelta;

            dResC += so.childResistanceDelta; dResT += so.teenResistanceDelta;
            dResA += so.adultResistanceDelta; dResE += so.elderResistanceDelta;

            dLowH     += so.lowHealthMortalityThresholdDelta;
            dMort0    += so.mortalityChanceAtZeroHealthDelta;
            dElderStart += so.elderMortalityAtElderStartDelta;
            dElderLife  += so.elderMortalityAtLifespanDelta;

            if (so.replaceFamilyConfig != null) replaceCfg = so.replaceFamilyConfig;
            if (so.patchFamilyConfig != null)   patches.Add(so.patchFamilyConfig);
        }

        rules.ApplyDeltas(
            dCH,dTH,dAH,dEH,
            dC2T,dT2A,dA2E,dLife,
            dCR,dTR,dAR,dER,
            dResC,dResT,dResA,dResE,
            dLowH,dMort0,dElderStart,dElderLife
        );

        var fam = PlayerFamilySimulationManager.Instance;
        if (fam != null)
        {
            if (replaceCfg) fam.SetConfig(CloneConfig(replaceCfg));
            foreach (var p in patches) if (p != null) fam.ApplyPatch(p);
        }

        var pop = PlayersPopulationManager.Instance;
        if (pop != null)
        {
            foreach (var g in pop.AllPopulations)
                g.maxHealthPerIndividual = rules.GetBaseHealth(g.ageGroup);
            pop.MarkUIDirty();
        }
    }

    private static FamilySimConfig CloneConfig(FamilySimConfig src)
    {
        // shallow clone is fine if fields are primitives
        var c = ScriptableObject.CreateInstance<FamilySimConfig>();
        c.CopyFrom(src);
        return c;
    }

    public void RemoveFor(string techID)
    {
        if (string.IsNullOrWhiteSpace(techID)) return;
        if (_byTech == null) BuildCache();
        if (!_byTech.TryGetValue(techID, out var list) || list == null || list.Count == 0) return;

        var rules = PlayerHealthRulebook.Instance;
        if (rules == null) { Debug.LogWarning("[PlayerHealthTechApplier] Missing PlayerHealthRulebook."); return; }

        int dCH=0, dTH=0, dAH=0, dEH=0;
        int dC2T=0, dT2A=0, dA2E=0, dLife=0;
        float dCR=0, dTR=0, dAR=0, dER=0;
        float dResC=0f, dResT=0f, dResA=0f, dResE=0f;
        float dLowH=0f, dMort0=0f, dElderStart=0f, dElderLife=0f;

        bool hadReplaceOrPatch = false;

        foreach (var so in list)
        {
            // Negate all deltas
            dCH -= so.baseChildHealthDelta; dTH -= so.baseTeenHealthDelta;
            dAH -= so.baseAdultHealthDelta; dEH -= so.baseElderHealthDelta;

            dC2T -= so.childToTeenAgeDelta; dT2A -= so.teenToAdultAgeDelta;
            dA2E -= so.adultToElderAgeDelta; dLife -= so.lifespanDelta;

            dCR -= so.childRecoveryDelta; dTR -= so.teenRecoveryDelta;
            dAR -= so.adultRecoveryDelta; dER -= so.elderRecoveryDelta;

            dResC -= so.childResistanceDelta; dResT -= so.teenResistanceDelta;
            dResA -= so.adultResistanceDelta; dResE -= so.elderResistanceDelta;

            dLowH     -= so.lowHealthMortalityThresholdDelta;
            dMort0    -= so.mortalityChanceAtZeroHealthDelta;
            dElderStart -= so.elderMortalityAtElderStartDelta;
            dElderLife  -= so.elderMortalityAtLifespanDelta;

            if (so.replaceFamilyConfig != null || so.patchFamilyConfig != null)
            hadReplaceOrPatch = true;
        }

        rules.ApplyDeltas(
            dCH,dTH,dAH,dEH,
            dC2T,dT2A,dA2E,dLife,
            dCR,dTR,dAR,dER,
            dResC,dResT,dResA,dResE,
            dLowH,dMort0,dElderStart,dElderLife
        );

        if (hadReplaceOrPatch)
        {
            // If some tech replaced/patch family config, the safest is to re-derive config
            // from remaining researched techs. (Optional to implement depending on your rulebook API.)
            Debug.LogWarning("[PlayerHealthTechApplier] Tech removal touched family config. Consider reapplying remaining researched techs to rebuild config exactly.");
        }

        var pop = PlayersPopulationManager.Instance;
        if (pop != null)
        {
            foreach (var g in pop.AllPopulations)
                g.maxHealthPerIndividual = rules.GetBaseHealth(g.ageGroup);
            pop.MarkUIDirty();
        }
    }
}
