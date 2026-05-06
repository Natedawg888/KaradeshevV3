using UnityEngine;

public partial class AnimalSimulation
{
    // Use when size changes for NON-COMBAT reasons and you want wounds to scale fairly.
    private void SetGroupSizePreserveHealthFraction(ref AnimalGroupState group, int newSize)
    {
        group.EnsureHealthValid();

        float healthFraction = GetGroupHealthFraction01(group);

        group.size = Mathf.Max(0, newSize);

        if (group.size <= 0)
        {
            group.currentHealth = 0;
            return;
        }

        int newMax = group.MaxHealth;
        group.currentHealth = Mathf.Clamp(
            Mathf.RoundToInt(newMax * healthFraction),
            1,
            newMax);
    }

    // Use when you split one group into two for NON-COMBAT reasons.
    private void SplitGroupHealthBySize(
        ref AnimalGroupState parent,
        ref AnimalGroupState child,
        int childSize)
    {
        parent.EnsureHealthValid();

        int oldSize = Mathf.Max(0, parent.size);
        if (oldSize <= 0 || childSize <= 0 || childSize >= oldSize)
            return;

        int totalHealth = parent.currentHealth;
        int parentNewSize = oldSize - childSize;

        int childHealth = Mathf.RoundToInt(totalHealth * (childSize / (float)oldSize));
        int parentHealth = totalHealth - childHealth;

        parent.size = parentNewSize;
        child.size = childSize;

        int parentMax = parent.MaxHealth;
        int childMax = child.MaxHealth;

        parent.currentHealth = parent.size <= 0 ? 0 : Mathf.Clamp(parentHealth, 1, parentMax);
        child.currentHealth = child.size <= 0 ? 0 : Mathf.Clamp(childHealth, 1, childMax);
    }

    // Use when two groups merge/rebalance for NON-COMBAT reasons.
    private void DistributeMergedHealthBySize(
        int totalHealth,
        ref AnimalGroupState a,
        int sizeA,
        ref AnimalGroupState b,
        int sizeB)
    {
        int totalSize = Mathf.Max(0, sizeA + sizeB);

        a.size = Mathf.Max(0, sizeA);
        b.size = Mathf.Max(0, sizeB);

        if (totalSize <= 0)
        {
            a.currentHealth = 0;
            b.currentHealth = 0;
            return;
        }

        int healthA = Mathf.RoundToInt(totalHealth * (sizeA / (float)totalSize));
        int healthB = totalHealth - healthA;

        int maxA = a.MaxHealth;
        int maxB = b.MaxHealth;

        a.currentHealth = a.size <= 0 ? 0 : Mathf.Clamp(healthA, 1, maxA);
        b.currentHealth = b.size <= 0 ? 0 : Mathf.Clamp(healthB, 1, maxB);
    }

    // Use when adding brand-new healthy animals to an existing group.
    private void AddAnimalsAtFullHealth(ref AnimalGroupState group, int amount)
    {
        if (amount <= 0)
            return;

        group.EnsureHealthValid();

        int added = Mathf.Max(0, amount);
        int hpPerAnimal = group.HealthPerAnimal;

        group.size += added;
        group.currentHealth = Mathf.Clamp(group.currentHealth + added * hpPerAnimal, 0, group.MaxHealth);
    }
}