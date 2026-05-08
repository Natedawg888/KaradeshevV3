using System.Collections.Generic;
using UnityEngine;

public class PlayerBuildingTechApplier : MonoBehaviour
{
    public static PlayerBuildingTechApplier Instance { get; private set; }

    private Dictionary<string, List<BuildingTechEffectSO>> _byTech;

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildCache();
    }

    private void BuildCache()
    {
        _byTech = new Dictionary<string, List<BuildingTechEffectSO>>();
        var all = Resources.LoadAll<BuildingTechEffectSO>("") ?? new BuildingTechEffectSO[0];
        foreach (var so in all)
        {
            if (!so || string.IsNullOrWhiteSpace(so.techID)) continue;
            if (!_byTech.TryGetValue(so.techID, out var list))
            {
                list = new List<BuildingTechEffectSO>();
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

        var rulebook = PlayerBuildingRulebook.Instance;
        //if (rulebook == null) { Debug.LogWarning("[PlayerBuildingTechApplier] Missing PlayerBuildingRulebook."); return; }
//
        // Aggregate per-tech; push to rulebook
        //for (int i = 0; i < list.Count; i++)
        //{
            //var so = list[i];
            var targets = (so.targetBuildingIDs != null && so.targetBuildingIDs.Count > 0)
                ? (IReadOnlyList<string>)so.targetBuildingIDs
                : new List<string>(0); // empty => wildcard

            var delta = new PlayerBuildingRulebook.Mod(
                so.maxHealthDelta,
                so.degenerationAmountDelta,
                so.degenerationIntervalDelta
            );

            rulebook.AddDeltasFor(targets, delta);
        }

        // Apply to existing instances now
        rulebook.ApplyToAllExisting();
    }

    public void RemoveFor(string techID)
    {
        if (string.IsNullOrWhiteSpace(techID)) return;
        if (_byTech == null) BuildCache();
        if (!_byTech.TryGetValue(techID, out var list) || list == null || list.Count == 0) return;

        var rulebook = PlayerBuildingRulebook.Instance;
        //if (rulebook == null) { Debug.LogWarning("[PlayerBuildingTechApplier] Missing PlayerBuildingRulebook."); return; }
//
        //for (int i = 0; i < list.Count; i++)
        //{
            //var so = list[i];
            var targets = (so.targetBuildingIDs != null && so.targetBuildingIDs.Count > 0)
                ? (IReadOnlyList<string>)so.targetBuildingIDs
                : new List<string>(0);

            // negate the deltas to remove this tech’s contribution
            var delta = new PlayerBuildingRulebook.Mod(
                -so.maxHealthDelta,
                -so.degenerationAmountDelta,
                -so.degenerationIntervalDelta
            );

            rulebook.AddDeltasFor(targets, delta);
        }

        rulebook.ApplyToAllExisting();
    }
}
