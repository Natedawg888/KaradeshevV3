using System.Collections.Generic;
using UnityEngine;

public partial class EnvironmentResourceNode : MonoBehaviour
{
    public void GenerateResources()
    {
        spawnedResources.Clear();
        if (environmentControl == null) return;
        RunSpawnersNow();
    }

    // -------- LIFECYCLE / BARREN TICK --------
    public void TickResourceLifecycle()
    {
        if (environmentControl == null)
            return;

        if (!isBarren && currentEnvironmentHealth <= 0)
        {
            StartBarren();
            return;
        }

        if (!isBarren && currentEnvironmentHealth < maxEnvironmentHealth && environmentRecoveryPerTick > 0)
        {
            currentEnvironmentHealth = Mathf.Min(
                maxEnvironmentHealth,
                currentEnvironmentHealth + environmentRecoveryPerTick
            );
        }

        if (isBarren)
        {
            if (barrenTurnsLeft > 0)
            {
                barrenTurnsLeft = Mathf.Max(0, barrenTurnsLeft - 1);

                if (barrenTimerUI != null)
                    barrenTimerUI.UpdateTimer(barrenTurnsLeft);

                if (barrenTurnsLeft <= 0)
                    ExitBarrenState();
            }
            return;
        }

        SeasonDefinition currentSeason = SeasonManager.Instance != null
            ? SeasonManager.Instance.CurrentSeason
            : null;

        // --- SPOILAGE & REGENERATION ---
        for (int i = spawnedResources.Count - 1; i >= 0; i--)
        {
            var entry = spawnedResources[i];
            var def = entry.definition;

            bool allowedThisSeason = def.IsAllowedInSeason(currentSeason);

            int effectiveSpoilageInterval = def.spoilageInterval;
            float effectiveSpoilageRate = def.spoilageRate;
            if (!allowedThisSeason)
            {
                effectiveSpoilageInterval = Mathf.Max(1, def.spoilageInterval * 2);
                effectiveSpoilageRate = Mathf.Min(1f, def.spoilageRate * 2f);
            }

            if (!def.nonPerishable)
            {
                entry.turnsSinceLastSpoilage++;
                if (entry.turnsSinceLastSpoilage >= effectiveSpoilageInterval)
                {
                    entry.turnsSinceLastSpoilage = 0;

                    int lost = Mathf.FloorToInt(entry.amount * effectiveSpoilageRate);
                    entry.amount = Mathf.Max(0, entry.amount - lost);
                }
            }

            if (def.ShouldRegenerate && allowedThisSeason)
            {
                entry.turnsSinceLastRegeneration++;
                if (entry.turnsSinceLastRegeneration >= def.recoveryInterval)
                {
                    entry.turnsSinceLastRegeneration = 0;

                    int regenAmount = Mathf.CeilToInt(def.recoveryRate * entry.maxAmount);
                    entry.amount = Mathf.Min(entry.maxAmount, entry.amount + regenAmount);
                }
            }
            else
            {
                entry.turnsSinceLastRegeneration = 0;
            }
        }

        turnsSinceLastExtraSpawn++;
        if (turnsSinceLastExtraSpawn >= ExtraSpawnInterval)
        {
            turnsSinceLastExtraSpawn = 0;
            TickSpawners();
            CleanupZeroAmountEntries();
        }
    }

    public int Consume(ResourceSpawnEntry entry, int amount)
    {
        if (entry == null || amount <= 0) return 0;
        int i = spawnedResources.IndexOf(entry);
        if (i < 0) return 0;

        int take = Mathf.Clamp(amount, 0, spawnedResources[i].amount);
        spawnedResources[i].amount -= take;

        if (spawnedResources[i].amount <= 0)
            spawnedResources.RemoveAt(i);

        return take;
    }

    public int Consume(ResourceDefinition def, int amount)
    {
        if (def == null || amount <= 0) return 0;
        for (int i = 0; i < spawnedResources.Count; i++)
        {
            if (spawnedResources[i].definition == def)
            {
                int take = Mathf.Clamp(amount, 0, spawnedResources[i].amount);
                spawnedResources[i].amount -= take;
                if (spawnedResources[i].amount <= 0)
                    spawnedResources.RemoveAt(i);
                return take;
            }
        }
        return 0;
    }

    public void CleanupZeroAmountEntries()
    {
        for (int i = spawnedResources.Count - 1; i >= 0; i--)
        {
            if (spawnedResources[i].amount <= 0)
                spawnedResources.RemoveAt(i);
        }
    }

    public int GetAmount(ResourceDefinition resourceDef)
    {
        if (resourceDef == null || spawnedResources == null)
            return 0;

        int total = 0;
        for (int i = 0; i < spawnedResources.Count; i++)
        {
            var entry = spawnedResources[i];
            if (entry == null || entry.definition == null) continue;
            if (entry.definition == resourceDef)
                total += Mathf.Max(0, entry.amount);
        }
        return total;
    }
}
