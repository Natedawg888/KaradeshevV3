// InventoryHelpers.cs
using System.Collections.Generic;
using System.Linq;

public static class InventoryQuery
{
    public static int GetOwned(ResourceDefinition def)
    {
        var inv = PlayerInventoryManager.Instance;
        if (!inv || def == null) return 0;

        if (!def.isGroup)
        {
            return inv.GetAmount(def);
        }

        // GROUP: sum across all stacks of this type
        var stacks = inv.GetStacks(def.groupType);
        if (stacks == null) return 0;
        return stacks.Sum(s => s.amount);
    }

    public static bool CanAfford(IEnumerable<ResourceCost> costs)
    {
        if (costs == null) return true;

        foreach (var c in costs)
        {
            if (c == null || c.resource == null) continue;
            if (GetOwned(c.resource) < c.amount)
                return false;
        }
        return true;
    }

    // (optional) keep old signature for callers you haven’t updated yet
    public static bool CanAfford(List<ResourceCost> costs)
        => CanAfford(costs as IEnumerable<ResourceCost>);
}