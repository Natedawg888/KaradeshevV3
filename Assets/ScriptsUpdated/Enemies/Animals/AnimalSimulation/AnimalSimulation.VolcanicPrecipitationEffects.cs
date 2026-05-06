using UnityEngine;

public partial class AnimalSimulation
{
    public bool TryApplyVolcanicPrecipitationDamageToGroup(
        int groupId,
        RainSimulationSystem.RainVisualKind kind,
        int baseDamage,
        float severity01,
        bool debugLogging)
    {
        if (!_groups.TryGetValue(groupId, out AnimalGroupState group) || group == null)
            return false;

        if (!group.isAlive || group.size <= 0)
            return false;

        severity01 = Mathf.Clamp01(severity01);
        int finalDamage = Mathf.Max(0, Mathf.RoundToInt(baseDamage * severity01));

        if (finalDamage <= 0)
            return false;

        TileCoord originalTile = group.tile;

        group.currentHealth = Mathf.Max(0, group.currentHealth - finalDamage);

        int healthPerAnimal = Mathf.Max(1, GetResolvedHealthPerAnimalFor(group));
        int newSize = Mathf.Max(0, Mathf.CeilToInt(group.currentHealth / (float)healthPerAnimal));

        if (newSize != group.size)
            group.size = newSize;

        if (group.currentHealth <= 0 || group.size <= 0)
        {
            if (debugLogging)
            {
                Debug.Log(
                    $"[AnimalSimulation] {kind} killed animal group {groupId} at {originalTile}. " +
                    $"Damage={finalDamage}");
            }

            CleanupTargetsOnDeath(ref group);
            RemoveGroup(groupId, originalTile);
            OnSimulationStateChanged?.Invoke();
            return true;
        }

        group.EnsureHealthValid();
        _groups[groupId] = group;

        OnGroupUpdated?.Invoke(group);
        OnSimulationStateChanged?.Invoke();

        if (debugLogging)
        {
            Debug.Log(
                $"[AnimalSimulation] {kind} damaged animal group {groupId} at {originalTile}. " +
                $"Damage={finalDamage} RemainingSize={group.size}");
        }

        return true;
    }
}