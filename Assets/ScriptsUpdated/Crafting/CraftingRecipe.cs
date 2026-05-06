using System;
using System.Collections.Generic;
using UnityEngine;

public enum CraftingChanceOutputMode
{
    AddToNormalOutput,
    ReplaceNormalOutputForThisBatch
}

[Serializable]
public class CraftingChanceOutput
{
    [Header("Identity")]
    public string displayName;

    [Header("Chance")]
    [Range(0f, 1f)]
    public float chance01 = 0.1f;

    [Tooltip("Add this output on top of the normal output, or replace the normal output for this batch.")]
    public CraftingChanceOutputMode mode = CraftingChanceOutputMode.AddToNormalOutput;

    [Header("Output")]
    public List<ResourceAmount> outputs = new();
}

[CreateAssetMenu(
    fileName = "NewCraftingRecipe",
    menuName = "Kardashev/Crafting/Crafting Recipe")]
public class CraftingRecipe : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique ID used in save data / lookups. E.g. 'craft_basic_axe'")]
    public string craftingID;

    [Tooltip("Player-facing name, e.g. 'Basic Axe'")]
    public string craftingName;

    [Tooltip("Icon shown in UI for this recipe.")]
    public Sprite craftingIcon;

    [Header("Requirements")]
    [Min(1), Tooltip("How many turns the crafting job takes per batch.")]
    public int craftTurnsRequired = 1;

    [Min(0), Tooltip("Population required to work this recipe at full speed.")]
    public int requiredPopulation = 0;

    [Tooltip("If true, crafting multiple batches at once also multiplies required population.")]
    public bool scalePopulationWithMultiplier = false;

    [Header("Progression")]
    [Min(0), Tooltip("XP granted when this recipe completes once.")]
    public int xpReward = 0;

    [Tooltip("If true, crafting multiple batches at once also multiplies XP granted.")]
    public bool scaleXPWithMultiplier = true;

    [Header("Costs")]
    [Tooltip("Legacy/default costs (used when no cost set is selected).")]
    public List<ResourceCost> resourceCosts = new();

    [Tooltip("Optional alternate cost sets. If selected, overrides resourceCosts.")]
    public List<ResourceCostSet> costSets = new();

    [HideInInspector]
    public int activeCostSetIndex = -1;

    [Header("Output")]
    [Tooltip("Resources granted when the recipe completes once (before multiplier).")]
    public List<ResourceAmount> outputResources = new();

    [Header("Chance Outputs")]
    [Tooltip("Optional random outputs. Replacement outputs are checked per batch. First replacement roll that succeeds wins.")]
    public List<CraftingChanceOutput> chanceOutputs = new();

    [Header("Batching")]
    [Tooltip("How many multiples of this recipe can be ordered at once.")]
    [Min(1)]
    public int maxMultiplier = 10;

    public bool HasAlternateCostSets => costSets != null && costSets.Count > 0;

    public IReadOnlyList<ResourceCost> GetActiveCosts()
    {
        if (HasAlternateCostSets && activeCostSetIndex >= 0 && activeCostSetIndex < costSets.Count)
            return costSets[activeCostSetIndex].costs;
        return resourceCosts;
    }

    public string GetActiveCostSetLabel()
    {
        if (!HasAlternateCostSets) return "Default";
        if (activeCostSetIndex < 0 || activeCostSetIndex >= costSets.Count) return "Default";

        var set = costSets[activeCostSetIndex];
        return string.IsNullOrWhiteSpace(set.displayName)
            ? $"Set {activeCostSetIndex + 1}"
            : set.displayName;
    }

    public void SetActiveCostSet(int index)
    {
        if (!HasAlternateCostSets)
        {
            activeCostSetIndex = -1;
            return;
        }

        if (index < -1) index = -1;
        if (index >= costSets.Count) index = costSets.Count - 1;
        activeCostSetIndex = index;
    }

    public void CycleNextCostSet()
    {
        if (!HasAlternateCostSets) { activeCostSetIndex = -1; return; }
        if (activeCostSetIndex < 0) activeCostSetIndex = 0;
        else activeCostSetIndex = (activeCostSetIndex + 1) % costSets.Count;
    }

    public void CyclePrevCostSet()
    {
        if (!HasAlternateCostSets) { activeCostSetIndex = -1; return; }
        if (activeCostSetIndex < 0) activeCostSetIndex = costSets.Count - 1;
        else activeCostSetIndex = (activeCostSetIndex - 1 + costSets.Count) % costSets.Count;
    }

    public int ClampMultiplier(int requested)
    {
        int m = Math.Max(1, requested);
        if (maxMultiplier > 0) m = Math.Min(m, maxMultiplier);
        return m;
    }

    public int GetPopulationRequiredFor(int multiplier = 1)
    {
        int basePop = Mathf.Max(0, requiredPopulation);
        if (basePop <= 0) return 0;

        if (!scalePopulationWithMultiplier)
            return basePop;

        long total = (long)basePop * ClampMultiplier(multiplier);
        if (total > int.MaxValue) total = int.MaxValue;
        return (int)total;
    }

    public int GetXPRewardFor(int multiplier = 1)
    {
        int baseXP = Mathf.Max(0, xpReward);
        if (baseXP <= 0) return 0;

        if (!scaleXPWithMultiplier)
            return baseXP;

        long total = (long)baseXP * ClampMultiplier(multiplier);
        if (total > int.MaxValue) total = int.MaxValue;
        return (int)total;
    }

    public List<ResourceCost> GetCostsFor(int multiplier)
    {
        multiplier = ClampMultiplier(multiplier);
        var src = GetActiveCosts();
        var list = new List<ResourceCost>(src.Count);

        for (int i = 0; i < src.Count; i++)
        {
            var c = src[i];
            if (c == null || c.resource == null) continue;

            long amt = (long)c.amount * multiplier;
            list.Add(new ResourceCost
            {
                resource = c.resource,
                amount = amt > int.MaxValue ? int.MaxValue : (int)amt
            });
        }

        return list;
    }

    public List<ResourceAmount> GetOutputFor(int multiplier)
    {
        multiplier = ClampMultiplier(multiplier);

        var list = new List<ResourceAmount>(outputResources != null ? outputResources.Count : 0);
        if (outputResources == null) return list;

        for (int i = 0; i < outputResources.Count; i++)
        {
            var o = outputResources[i];
            if (o == null || o.resource == null) continue;

            long amt = (long)o.amount * multiplier;
            list.Add(new ResourceAmount
            {
                resource = o.resource,
                amount = amt > int.MaxValue ? int.MaxValue : (int)amt
            });
        }

        return list;
    }

    public bool CanAfford(int multiplier = 1)
        => InventoryQuery.CanAfford(GetCostsFor(multiplier));

    public int? GetFirstAffordableCostSetIndex(int multiplier = 1)
    {
        multiplier = ClampMultiplier(multiplier);

        if (!HasAlternateCostSets)
            return InventoryQuery.CanAfford(GetCostsFor(multiplier)) ? -1 : (int?)null;

        for (int i = 0; i < costSets.Count; i++)
        {
            var set = costSets[i];
            if (set == null) continue;

            int old = activeCostSetIndex;
            activeCostSetIndex = i;
            bool ok = InventoryQuery.CanAfford(GetCostsFor(multiplier));
            activeCostSetIndex = old;

            if (ok) return i;
        }

        int prev = activeCostSetIndex;
        activeCostSetIndex = -1;
        bool legacyOk = InventoryQuery.CanAfford(GetCostsFor(multiplier));
        activeCostSetIndex = prev;

        return legacyOk ? -1 : (int?)null;
    }

    public bool SpendFor(int multiplier = 1)
    {
        var costs = GetCostsFor(multiplier);
        return ResourceDeduction.Deduct(costs);
    }

    public List<ResourceAmount> GetRolledOutputFor(int multiplier)
    {
        multiplier = ClampMultiplier(multiplier);

        Dictionary<ResourceDefinition, int> totals = new Dictionary<ResourceDefinition, int>();

        for (int batch = 0; batch < multiplier; batch++)
        {
            List<ResourceAmount> batchOutput = GetSingleBatchBaseOutput();

            TryApplyReplacementChanceOutput(batchOutput);
            TryApplyBonusChanceOutputs(batchOutput);

            AddToTotals(totals, batchOutput);
        }

        return TotalsToList(totals);
    }

    private List<ResourceAmount> GetSingleBatchBaseOutput()
    {
        List<ResourceAmount> list = new List<ResourceAmount>(
            outputResources != null ? outputResources.Count : 0);

        if (outputResources == null)
            return list;

        for (int i = 0; i < outputResources.Count; i++)
        {
            ResourceAmount o = outputResources[i];
            if (o == null || o.resource == null || o.amount <= 0)
                continue;

            list.Add(new ResourceAmount
            {
                resource = o.resource,
                amount = o.amount
            });
        }

        return list;
    }

    private void TryApplyReplacementChanceOutput(List<ResourceAmount> batchOutput)
    {
        if (chanceOutputs == null || chanceOutputs.Count == 0)
            return;

        for (int i = 0; i < chanceOutputs.Count; i++)
        {
            CraftingChanceOutput roll = chanceOutputs[i];
            if (roll == null)
                continue;

            if (roll.mode != CraftingChanceOutputMode.ReplaceNormalOutputForThisBatch)
                continue;

            if (roll.outputs == null || roll.outputs.Count == 0)
                continue;

            float chance = Mathf.Clamp01(roll.chance01);
            if (chance <= 0f)
                continue;

            if (UnityEngine.Random.value > chance)
                continue;

            batchOutput.Clear();

            for (int j = 0; j < roll.outputs.Count; j++)
            {
                ResourceAmount o = roll.outputs[j];
                if (o == null || o.resource == null || o.amount <= 0)
                    continue;

                batchOutput.Add(new ResourceAmount
                {
                    resource = o.resource,
                    amount = o.amount
                });
            }

            // Only one replacement output should win per batch.
            return;
        }
    }

    private void TryApplyBonusChanceOutputs(List<ResourceAmount> batchOutput)
    {
        if (chanceOutputs == null || chanceOutputs.Count == 0)
            return;

        for (int i = 0; i < chanceOutputs.Count; i++)
        {
            CraftingChanceOutput roll = chanceOutputs[i];
            if (roll == null)
                continue;

            if (roll.mode != CraftingChanceOutputMode.AddToNormalOutput)
                continue;

            if (roll.outputs == null || roll.outputs.Count == 0)
                continue;

            float chance = Mathf.Clamp01(roll.chance01);
            if (chance <= 0f)
                continue;

            if (UnityEngine.Random.value > chance)
                continue;

            for (int j = 0; j < roll.outputs.Count; j++)
            {
                ResourceAmount o = roll.outputs[j];
                if (o == null || o.resource == null || o.amount <= 0)
                    continue;

                batchOutput.Add(new ResourceAmount
                {
                    resource = o.resource,
                    amount = o.amount
                });
            }
        }
    }

    private void AddToTotals(Dictionary<ResourceDefinition, int> totals, List<ResourceAmount> amounts)
    {
        if (totals == null || amounts == null)
            return;

        for (int i = 0; i < amounts.Count; i++)
        {
            ResourceAmount a = amounts[i];
            if (a == null || a.resource == null || a.amount <= 0)
                continue;

            if (!totals.ContainsKey(a.resource))
                totals[a.resource] = 0;

            long total = (long)totals[a.resource] + a.amount;
            totals[a.resource] = total > int.MaxValue ? int.MaxValue : (int)total;
        }
    }

    private List<ResourceAmount> TotalsToList(Dictionary<ResourceDefinition, int> totals)
    {
        List<ResourceAmount> list = new List<ResourceAmount>();

        if (totals == null || totals.Count == 0)
            return list;

        foreach (KeyValuePair<ResourceDefinition, int> pair in totals)
        {
            if (pair.Key == null || pair.Value <= 0)
                continue;

            list.Add(new ResourceAmount
            {
                resource = pair.Key,
                amount = pair.Value
            });
        }

        return list;
    }
}