using System.Collections.Generic;

public static class ResourceDeduction
{
    /// Returns true if all costs were successfully removed.
    public static bool Deduct(IEnumerable<ResourceCost> costs)
    {
        if (costs == null) return true;

        var inv = PlayerInventoryManager.Instance;
        if (!inv) return false;

        // Safety first — don’t partially deduct.
        if (!InventoryQuery.CanAfford(costs)) return false;

        foreach (var c in costs)
        {
            if (c == null || c.resource == null || c.amount <= 0) continue;

            if (!c.resource.isGroup)
            {
                inv.TryRemove(c.resource, c.amount);
            }
            else
            {
                int remaining = c.amount;
                var stacks = new List<InventoryStack>(inv.GetStacks(c.resource.groupType));
                foreach (var s in stacks)
                {
                    if (remaining <= 0) break;
                    int take = System.Math.Min(remaining, s.amount);
                    if (take > 0)
                    {
                        inv.TryRemove(s.definition, take);
                        remaining -= take;
                    }
                }
            }
        }

        inv.inventoryPanel?.Refresh();
        return true;
    }

    // (optional) keep old signature
    public static bool Deduct(List<ResourceCost> costs)
        => Deduct(costs as IEnumerable<ResourceCost>);
}