using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerEnvironmentTechApplier : MonoBehaviour
{
    public static PlayerEnvironmentTechApplier Instance { get; private set; }

    private Dictionary<string, List<EnvironmentTechEffectSO>> _byTech;

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildCache();
    }

    private void BuildCache()
    {
        _byTech = new Dictionary<string, List<EnvironmentTechEffectSO>>();
        var all = Resources.LoadAll<EnvironmentTechEffectSO>("") ?? new EnvironmentTechEffectSO[0];
        foreach (var so in all)
        {
            if (!so || string.IsNullOrWhiteSpace(so.techID)) continue;
            if (!_byTech.TryGetValue(so.techID, out var list))
            {
                list = new List<EnvironmentTechEffectSO>();
                _byTech[so.techID] = list;
            }
            list.Add(so);
        }
    }

    public void ApplyFor(string techID)
    {
        if (string.IsNullOrWhiteSpace(techID)) return;
        if (_byTech == null) BuildCache();
        if (!_byTech.TryGetValue(techID, out var list) || list == null || list.Count == 0) return;

        // Gather all tiles once
        var envs = FindObjectsOfType<EnvironmentControl>(includeInactive: true);
        if (envs == null || envs.Length == 0) return;

        int unlocked = 0;

        foreach (var so in list)
        {
            if (so == null || so.environmentEffects == null) continue;

            foreach (var eff in so.environmentEffects)
            {
                if (!eff.unlockExplore) continue;

                for (int i = 0; i < envs.Length; i++)
                {
                    var env = envs[i];
                    if (!env) continue;
                    if (!eff.Matches(env)) continue;

                    if (!env.canExplore)
                    {
                        env.canExplore = true;
                        unlocked++;
                    }
                }
            }
        }

        if (unlocked > 0)
            //Debug.Log($"[EnvTech] Unlocked exploration on {unlocked} environment tiles for tech '{techID}'.");
    }

    public void RemoveFor(string techID)
    {
        if (string.IsNullOrWhiteSpace(techID)) return;
        if (_byTech == null) BuildCache();
        if (!_byTech.TryGetValue(techID, out var list) || list == null || list.Count == 0) return;

        // We only handle unlockExplore toggles here.
        var envs = FindObjectsOfType<EnvironmentControl>(includeInactive: true);
        if (envs == null || envs.Length == 0) return;

        // Build a fast set of (remaining) unlocking matchers from the *still researched* techs
        var researched = PlayerResearchManager.Instance?.GetResearchedIDs() ?? Array.Empty<string>();
        var unlockers = new List<EnvironmentTechEffectSO.EnvironmentEffect>();
        foreach (var kv in _byTech)
        {
            if (kv.Key == techID) continue; // skip the one we’re removing
            if (!researched.Contains(kv.Key)) continue;
            foreach (var effSO in kv.Value)
            {
                if (effSO == null || effSO.environmentEffects == null) continue;
                foreach (var eff in effSO.environmentEffects)
                    if (eff.unlockExplore) unlockers.Add(eff);
            }
        }

        foreach (var so in list)
        {
            if (so == null || so.environmentEffects == null) continue;

            foreach (var eff in so.environmentEffects)
            {
                if (!eff.unlockExplore) continue;

                for (int i = 0; i < envs.Length; i++)
                {
                    var env = envs[i];
                    if (!env || !eff.Matches(env)) continue;

                    // Only relock if *no other researched tech* still unlocks this tile
                    bool someoneElseUnlocks = false;
                    for (int u = 0; u < unlockers.Count; u++)
                    {
                        if (unlockers[u].Matches(env)) { someoneElseUnlocks = true; break; }
                    }
                    if (!someoneElseUnlocks)
                        env.canExplore = false;
                }
            }
        }
    }
}
