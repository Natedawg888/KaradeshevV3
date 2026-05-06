using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class EnvironmentResourceNode : MonoBehaviour
{
    public void GenerateResources()
    {
        spawnedResources.Clear();
        if (environmentControl == null) return;

        var envType = environmentControl.environmentType;
        var tileType = environmentControl.environmentTileType;
        var size = environmentControl.tileSize;

        int varietyLimit = ResourceVarietyCalculator.CalculateVarietyLimit(
            envType,
            size,
            GetAllowedCandidates().Count(),
            maxVarietyCap);

        SeasonDefinition currentSeason = SeasonManager.Instance != null
            ? SeasonManager.Instance.CurrentSeason
            : null;

        var candidates = GetAllowedCandidates(currentSeason, envType, tileType).ToList();
        if (candidates.Count == 0)
            return;

        int remainingCapacity = totalCapacity;

        // ---------- GUARANTEED SPAWNS FIRST ----------
        var alreadySpawnedDefs = new HashSet<ResourceDefinition>();
        foreach (var g in guaranteedSpawns)
        {
            var def = FindDefById(g.resourceId);
            if (def == null) continue;

            if (g.respectSeason && !def.IsAvailableIn(envType, tileType, currentSeason))
                continue;

            if (remainingCapacity <= 0) break;

            float baseAmount = def.spawnRate
                * EnvironmentResourceAmountCalculator.GetEnvironmentModifier(envType)
                * EnvironmentResourceAmountCalculator.GetTileModifier(tileType);

            float jitter = Random.Range(0.75f, 1.25f);
            int desired = Mathf.Max(1, Mathf.CeilToInt(baseAmount * jitter));
            int assigned = Mathf.Min(desired, remainingCapacity);
            if (assigned <= 0) continue;

            var entry = new ResourceSpawnEntry { definition = def };
            entry.Initialize(assigned);
            spawnedResources.Add(entry);
            alreadySpawnedDefs.Add(def);

            remainingCapacity -= assigned;
            varietyLimit = Mathf.Max(0, varietyLimit - 1);
        }

        if (remainingCapacity <= 0 || varietyLimit <= 0)
            return;

        ShuffleList(candidates);
        candidates.RemoveAll(d => alreadySpawnedDefs.Contains(d));

        int takeCount = Mathf.Min(varietyLimit, candidates.Count);

        for (int i = 0; i < takeCount && remainingCapacity > 0; i++)
        {
            var def = candidates[i];

            float baseAmount = def.spawnRate
                * EnvironmentResourceAmountCalculator.GetEnvironmentModifier(envType)
                * EnvironmentResourceAmountCalculator.GetTileModifier(tileType);

            float jitter = Random.Range(0.75f, 1.25f);
            float raw = baseAmount * jitter;

            int desired = Mathf.Max(1, Mathf.CeilToInt(raw));
            int assigned = Mathf.Min(desired, remainingCapacity);

            var entry = new ResourceSpawnEntry { definition = def };
            entry.Initialize(assigned);
            spawnedResources.Add(entry);

            remainingCapacity -= assigned;
        }
    }

    private IEnumerable<ResourceDefinition> GetAllowedCandidates(
        SeasonDefinition season,
        EnvironmentType envType,
        EnvironmentTileType tileType)
    {
        return resourceDefinitions.Where(def => def.IsAvailableIn(envType, tileType, season));
    }

    private IEnumerable<ResourceDefinition> GetAllowedCandidates()
    {
        SeasonDefinition currentSeason = SeasonManager.Instance != null
            ? SeasonManager.Instance.CurrentSeason
            : null;

        return GetAllowedCandidates(
            currentSeason,
            environmentControl.environmentType,
            environmentControl.environmentTileType
        );
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = Random.Range(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
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
            TryExtraSpawn(currentSeason);
            CleanupZeroAmountEntries();
        }
    }

    private void TryExtraSpawn(SeasonDefinition currentSeason)
    {
        if (environmentControl == null) return;

        var envType = environmentControl.environmentType;
        var tileType = environmentControl.environmentTileType;
        var size = environmentControl.tileSize;

        int varietyLimit = ResourceVarietyCalculator.CalculateVarietyLimit(
            envType,
            size,
            GetAllowedCandidates(currentSeason, envType, tileType).Count(),
            maxVarietyCap);

        var candidates = GetAllowedCandidates(currentSeason, envType, tileType).ToList();
        if (candidates.Count == 0) return;

        const float outOfSeasonRemovalBias = 3f;

        int currentTotal = spawnedResources.Sum(e => e.amount);
        int remainingCapacity = totalCapacity - currentTotal;

        foreach (var g in guaranteedSpawns)
        {
            var def = FindDefById(g.resourceId);
            if (def == null) continue;
            if (g.respectSeason && !def.IsAllowedInSeason(currentSeason)) continue;
            if (spawnedResources.Any(e => e.definition == def && e.amount > 0)) continue;

            if (remainingCapacity <= 0) break;

            float baseAmount = def.spawnRate
                * EnvironmentResourceAmountCalculator.GetEnvironmentModifier(envType)
                * EnvironmentResourceAmountCalculator.GetTileModifier(tileType);

            float jitter = Random.Range(0.75f, 1.25f);
            int desired = Mathf.Max(1, Mathf.CeilToInt(baseAmount * jitter));
            int add = Mathf.Min(desired, remainingCapacity);
            if (add <= 0) continue;

            var entry = new ResourceSpawnEntry { definition = def };
            entry.Initialize(add);
            spawnedResources.Add(entry);

            remainingCapacity -= add;
            currentTotal += add;
        }

        if (spawnedResources.Count > 0)
        {
            float totalWeight = 0f;
            var weights = new float[spawnedResources.Count];
            for (int i = 0; i < spawnedResources.Count; i++)
            {
                bool allowed = spawnedResources[i].definition.IsAllowedInSeason(currentSeason);
                weights[i] = allowed ? 1f : outOfSeasonRemovalBias;
                totalWeight += weights[i];
            }

            float r = Random.Range(0f, totalWeight);
            float acc = 0f;
            int idx = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                acc += weights[i];
                if (r <= acc) { idx = i; break; }
            }

            var entryToTrim = spawnedResources[idx];
            int removeAmount = Random.Range(1, entryToTrim.amount + 1);
            entryToTrim.amount = Mathf.Max(0, entryToTrim.amount - removeAmount);
            if (entryToTrim.amount <= 0) spawnedResources.RemoveAt(idx);

            currentTotal = spawnedResources.Sum(e => e.amount);
            remainingCapacity = totalCapacity - currentTotal;
        }

        var weightList = new List<(ResourceDefinition def, float weight)>();
        foreach (var def in candidates)
            weightList.Add((def, Mathf.Max(0f, def.spawnRate)));

        if (remainingCapacity <= 0) return;

        for (int pick = 0; pick < varietyLimit && remainingCapacity > 0 && weightList.Count > 0; pick++)
        {
            var chosen = WeightedPickAndRemove(weightList);
            if (chosen.def == null) break;

            int amountToAdd = Mathf.Max(1, Mathf.CeilToInt(chosen.def.spawnRate));
            amountToAdd = Mathf.Min(amountToAdd, remainingCapacity);

            var existing = spawnedResources.FirstOrDefault(e => e.definition == chosen.def);
            if (existing != null)
            {
                int space = existing.maxAmount - existing.amount;
                if (space <= 0) continue;

                int add = Mathf.Min(amountToAdd, space);
                existing.amount += add;
                remainingCapacity -= add;
            }
            else
            {
                var entry = new ResourceSpawnEntry { definition = chosen.def };
                entry.Initialize(amountToAdd);
                spawnedResources.Add(entry);
                remainingCapacity -= amountToAdd;
            }
        }

        for (int i = spawnedResources.Count - 1; i >= 0; i--)
            if (spawnedResources[i].amount <= 0) spawnedResources.RemoveAt(i);
    }

    private (ResourceDefinition def, float weight) WeightedPickAndRemove(List<(ResourceDefinition def, float weight)> list)
    {
        float total = list.Sum(t => t.weight);
        if (total <= 0f)
        {
            int idx = Random.Range(0, list.Count);
            var item = list[idx];
            list.RemoveAt(idx);
            return item;
        }

        float r = Random.Range(0f, total);
        float accum = 0f;
        for (int i = 0; i < list.Count; i++)
        {
            accum += list[i].weight;
            if (r <= accum)
            {
                var picked = list[i];
                list.RemoveAt(i);
                return picked;
            }
        }

        var last = list[list.Count - 1];
        list.RemoveAt(list.Count - 1);
        return last;
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
            if (entry == null || entry.definition == null)
                continue;

            if (entry.definition == resourceDef)
                total += Mathf.Max(0, entry.amount);
        }

        return total;
    }

    private ResourceDefinition FindDefById(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        if (ResourceDictionary.Instance != null)
        {
            var def = ResourceDictionary.Instance.GetByID(id);
            if (def != null)
                return def;
        }

        if (baseResourceDefinitions != null && baseResourceDefinitions.Count > 0)
        {
            return baseResourceDefinitions
                .FirstOrDefault(d =>
                    d != null &&
                    string.Equals(d.resourceID, id, System.StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }
}