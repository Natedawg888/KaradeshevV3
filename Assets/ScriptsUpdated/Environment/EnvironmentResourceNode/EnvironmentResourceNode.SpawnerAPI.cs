using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

public partial class EnvironmentResourceNode : MonoBehaviour
{
    [Header("Debug (spawner system)")]
    [Tooltip("Log per-spawner tick detail. Keep off in production — very spammy.")]
    [SerializeField] private bool debugSpawnerLogging;
    [Tooltip("Log per-resource add/remove. Keep off in production.")]
    [SerializeField] private bool debugResourceLogging;

    private static readonly ProfilerMarker s_tickSpawnersMarker    = new("ERN.TickSpawners");
    private static readonly ProfilerMarker s_runSpawnersNowMarker  = new("ERN.RunSpawnersNow");
    private static readonly ProfilerMarker s_runOutputsMarker      = new("ERN.RunSpawnerOutputs");

    // ── Initialization ───────────────────────────────────────────────────────

    public void InitializeSpawners()
    {
        if (activeSpawners == null) activeSpawners = new List<ResourceSpawnerRuntime>();
        activeSpawners.Clear();
        if (baseSpawners == null || baseSpawners.Count == 0) return;

        var envType = environmentControl?.environmentType ?? default;
        var tileType = environmentControl?.environmentTileType ?? default;
        var season   = SeasonManager.Instance?.CurrentSeason;
        string normalizedSeason = season != null ? NormalizeID(season.seasonID) : string.Empty;

        for (int i = 0; i < baseSpawners.Count; i++)
        {
            var def = baseSpawners[i];
            if (def == null) continue;
            if (!SpawnerConditionsMet(def, envType, tileType, normalizedSeason)) continue;
            var runtime = BuildRuntime(def, SpawnerSourceReason.BaseEnvironment);
            activeSpawners.Add(runtime);
            if (debugSpawnerLogging)
                Debug.Log($"[Spawner] [{name}] Added base spawner '{def.displayName}' ({def.category})");
        }
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void AddSpawner(ResourceSpawnerDefinition def,
                           SpawnerSourceReason reason = SpawnerSourceReason.BaseEnvironment)
    {
        if (def == null) return;
        if (HasSpawner(def.spawnerID)) return;
        activeSpawners.Add(BuildRuntime(def, reason));
        if (debugSpawnerLogging)
            Debug.Log($"[Spawner] [{name}] Added spawner '{def.displayName}' reason={reason}");
    }

    public void AddTemporarySpawner(ResourceSpawnerDefinition def, int lifetimeTurns,
                                    SpawnerSourceReason reason = SpawnerSourceReason.BaseEnvironment,
                                    bool runImmediately = false)
    {
        if (def == null) return;
        var runtime = BuildRuntime(def, reason);
        runtime.remainingLifetimeTurns = Mathf.Max(1, lifetimeTurns);
        activeSpawners.Add(runtime);
        if (debugSpawnerLogging)
            Debug.Log($"[Spawner] [{name}] Added TEMPORARY spawner '{def.displayName}' " +
                      $"lifetime={lifetimeTurns}t reason={reason}");
        if (runImmediately)
        {
            int remaining = totalCapacity > 0 ? totalCapacity - SpawnedAmountTotal() : int.MaxValue;
            float sizeMult = environmentControl != null
                ? EnvironmentResourceAmountCalculator.GetSizeMultiplier(environmentControl.tileSize)
                : 1f;
            RunSpawnerOutputs(runtime, remaining, sizeMult);
        }
    }

    public void RemoveSpawner(string spawnerID)
    {
        if (string.IsNullOrEmpty(spawnerID)) return;
        for (int i = activeSpawners.Count - 1; i >= 0; i--)
        {
            if (activeSpawners[i]?.definition?.spawnerID == spawnerID)
            {
                if (debugSpawnerLogging)
                    Debug.Log($"[Spawner] [{name}] Removed spawner '{activeSpawners[i].definition.displayName}'");
                activeSpawners.RemoveAt(i);
            }
        }
    }

    public void RemoveSpawnersByReason(SpawnerSourceReason reason)
    {
        for (int i = activeSpawners.Count - 1; i >= 0; i--)
        {
            if (activeSpawners[i]?.sourceReason == reason)
            {
                if (debugSpawnerLogging)
                    Debug.Log($"[Spawner] [{name}] Removed spawner " +
                              $"'{activeSpawners[i].definition?.displayName}' (reason={reason})");
                activeSpawners.RemoveAt(i);
            }
        }
    }

    public bool HasSpawner(string spawnerID)
    {
        if (string.IsNullOrEmpty(spawnerID)) return false;
        for (int i = 0; i < activeSpawners.Count; i++)
        {
            var r = activeSpawners[i];
            if (r != null && r.isActive && r.definition?.spawnerID == spawnerID)
                return true;
        }
        return false;
    }

    public void AddResource(ResourceDefinition def, int amount)
    {
        if (def == null || amount <= 0) return;
        int remaining = totalCapacity > 0 ? totalCapacity - SpawnedAmountTotal() : amount;
        int toAdd     = Mathf.Min(amount, remaining);
        if (toAdd <= 0) return;
        AddResourceToNode(def, toAdd, addToExisting: true);
    }

    public void SetTileState(TileStateFlags flag, bool active)
    {
        if (active) currentTileState |= flag;
        else        currentTileState &= ~flag;
    }

    public bool HasTileState(TileStateFlags flag) => (currentTileState & flag) == flag;

    // ── Tick (called from TickResourceLifecycle each interval) ───────────────

    public void TickSpawners()
    {
        if (activeSpawners == null || activeSpawners.Count == 0) return;

        using (s_tickSpawnersMarker.Auto())
        {
            int remaining = totalCapacity > 0 ? totalCapacity - SpawnedAmountTotal() : int.MaxValue;

            var envType  = environmentControl?.environmentType ?? default;
            var tileType = environmentControl?.environmentTileType ?? default;
            float sizeMultiplier = environmentControl != null
                ? EnvironmentResourceAmountCalculator.GetSizeMultiplier(environmentControl.tileSize)
                : 1f;
            var season   = SeasonManager.Instance?.CurrentSeason;
            string normalizedSeason = season != null ? NormalizeID(season.seasonID) : string.Empty;

            // Live-update IsCurrentlyWet from rain simulation before checking conditions
            if (gridManager != null && RainSimulationSystem.Instance != null &&
                gridManager.TryGetCell(transform.position, out int rainX, out int rainY))
            {
                SetTileState(TileStateFlags.IsCurrentlyWet,
                             RainSimulationSystem.Instance.IsRainingAtCell(rainX, rainY));
            }

            // Sample climate once per tile per tick
            float tileTemp = 0f, tileHum = 0f;
            bool hasClimate = ClimateManager.Instance != null
                && ClimateManager.Instance.TryGetClimateAtWorldPos(transform.position, out tileTemp, out tileHum);

            for (int i = activeSpawners.Count - 1; i >= 0; i--)
            {
                var runtime = activeSpawners[i];
                if (runtime == null || !runtime.isActive) continue;

                // Tick lifetime first
                if (runtime.remainingLifetimeTurns > 0)
                    runtime.remainingLifetimeTurns--;

                if (runtime.IsExpired())
                {
                    if (debugSpawnerLogging)
                        Debug.Log($"[Spawner] [{name}] Spawner EXPIRED: '{runtime.definition?.displayName}'");
                    activeSpawners.RemoveAt(i);
                    continue;
                }

                var def = runtime.definition;
                runtime.turnsSinceLastSpawn++;

                if (debugSpawnerLogging)
                    Debug.Log($"[Spawner] [{name}] Checking '{def.displayName}' " +
                              $"turns={runtime.turnsSinceLastSpawn}/{def.spawnIntervalTurns}");

                if (runtime.turnsSinceLastSpawn < def.spawnIntervalTurns) continue;
                if (!SpawnerConditionsMet(def, envType, tileType, normalizedSeason)) continue;

                float climateMult = (hasClimate && def.climate != null)
                    ? Mathf.Clamp(def.climate.EvaluateMultiplier(tileTemp, tileHum), 0f, 3f)
                    : 1f;
                // Guaranteed spawners skip the chance roll; only climate can stop them.
                float effectiveChance = (def.isGuaranteed ? 1f : def.baseSpawnChance) * climateMult;
                float roll = def.isGuaranteed ? 0f : Random.value;

                if (debugSpawnerLogging)
                    Debug.Log($"[Spawner] [{name}] '{def.displayName}' guaranteed:{def.isGuaranteed} " +
                              $"roll:{roll:F2} effective:{effectiveChance:F2} (base:{def.baseSpawnChance:F2} climate×{climateMult:F2})");

                if (effectiveChance <= 0f || roll > effectiveChance)
                {
                    runtime.turnsSinceLastSpawn = 0;
                    continue;
                }

                remaining = RunSpawnerOutputs(runtime, remaining, sizeMultiplier);

                if (def.canExpire && def.maxUses > 0 && runtime.remainingUses > 0)
                    runtime.remainingUses--;

                runtime.turnsSinceLastSpawn = 0;
            }
        }
    }

    // ── Immediate run (called from GenerateResources on new game) ────────────

    public void RunSpawnersNow()
    {
        if (activeSpawners == null || activeSpawners.Count == 0) return;

        using (s_runSpawnersNowMarker.Auto())
        {
            int remaining = totalCapacity > 0 ? totalCapacity - SpawnedAmountTotal() : int.MaxValue;

            var envType  = environmentControl?.environmentType ?? default;
            var tileType = environmentControl?.environmentTileType ?? default;
            float sizeMultiplier = environmentControl != null
                ? EnvironmentResourceAmountCalculator.GetSizeMultiplier(environmentControl.tileSize)
                : 1f;
            var season   = SeasonManager.Instance?.CurrentSeason;
            string normalizedSeason = season != null ? NormalizeID(season.seasonID) : string.Empty;

            float tileTemp = 0f, tileHum = 0f;
            bool hasClimate = ClimateManager.Instance != null
                && ClimateManager.Instance.TryGetClimateAtWorldPos(transform.position, out tileTemp, out tileHum);

            for (int i = 0; i < activeSpawners.Count; i++)
            {
                var runtime = activeSpawners[i];
                if (runtime == null || !runtime.isActive) continue;
                var def = runtime.definition;
                if (!SpawnerConditionsMet(def, envType, tileType, normalizedSeason)) continue;

                float climateMult = (hasClimate && def.climate != null)
                    ? Mathf.Clamp(def.climate.EvaluateMultiplier(tileTemp, tileHum), 0f, 3f)
                    : 1f;

                float effectiveChance = (def.isGuaranteed ? 1f : def.baseSpawnChance) * climateMult;
                if (effectiveChance <= 0f) continue;
                if (!def.isGuaranteed && Random.value > effectiveChance) continue;

                remaining = RunSpawnerOutputs(runtime, remaining, sizeMultiplier);
            }
        }
    }

    // ── Internal helpers ─────────────────────────────────────────────────────

    private int RunSpawnerOutputs(ResourceSpawnerRuntime runtime, int remaining, float sizeMultiplier = 1f)
    {
        using (s_runOutputsMarker.Auto())
        {
            var def = runtime.definition;
            for (int i = 0; i < def.outputs.Count; i++)
            {
                var output = def.outputs[i];
                if (output?.resource == null) continue;

                float outputRoll = Random.value;
                if (!def.isGuaranteed && outputRoll > output.chance)
                {
                    if (debugSpawnerLogging)
                        Debug.Log($"[Spawner] [{name}] Output '{output.resource.resourceID}' skipped " +
                                  $"(roll {outputRoll:F2} > chance {output.chance:F2})");
                    continue;
                }

                int rawAmount = Random.Range(output.minAmount, output.maxAmount + 1);
                int amount    = Mathf.Max(1, Mathf.RoundToInt(rawAmount * sizeMultiplier));
                int toAdd     = Mathf.Min(amount, remaining);
                if (toAdd <= 0)
                {
                    if (debugSpawnerLogging)
                        Debug.Log($"[Spawner] [{name}] Capacity BLOCKED " +
                                  $"'{output.resource.resourceID}' (remaining={remaining})");
                    continue;
                }

                if (debugSpawnerLogging)
                    Debug.Log($"[Spawner] [{name}] Output: '{output.resource.resourceID}' x{toAdd}");

                AddResourceToNode(output.resource, toAdd, output.addToExistingStack);
                remaining -= toAdd;
            }
            return remaining;
        }
    }

    private void AddResourceToNode(ResourceDefinition def, int amount, bool addToExisting)
    {
        if (def == null || amount <= 0) return;

        if (addToExisting)
        {
            for (int i = 0; i < spawnedResources.Count; i++)
            {
                if (spawnedResources[i].definition == def)
                {
                    int space = spawnedResources[i].maxAmount - spawnedResources[i].amount;
                    if (space <= 0)
                    {
                        if (debugResourceLogging)
                            Debug.Log($"[Spawner] [{name}] Stack full for '{def.resourceID}' — skipped");
                        return;
                    }
                    int add = Mathf.Min(amount, space);
                    spawnedResources[i].amount += add;
                    if (debugResourceLogging)
                        Debug.Log($"[Spawner] [{name}] Spawned {add}x '{def.resourceID}' " +
                                  $"(stack now {spawnedResources[i].amount}/{spawnedResources[i].maxAmount})");
                    return;
                }
            }
        }

        var entry = new ResourceSpawnEntry { definition = def };
        entry.Initialize(amount);
        spawnedResources.Add(entry);
        if (debugResourceLogging)
            Debug.Log($"[Spawner] [{name}] Spawned {amount}x '{def.resourceID}' (new stack)");
    }

    private int SpawnedAmountTotal()
    {
        int total = 0;
        for (int i = 0; i < spawnedResources.Count; i++)
            total += spawnedResources[i].amount;
        return total;
    }

    // normalizedSeasonID: pre-computed by caller to avoid per-spawner NormalizeID allocation
    private bool SpawnerConditionsMet(ResourceSpawnerDefinition def,
                                      EnvironmentType env,
                                      EnvironmentTileType tile,
                                      string normalizedSeasonID)
    {
        var c = def?.conditions;
        if (c == null) return true;

        if (c.requiredEnvironmentTypes != null && c.requiredEnvironmentTypes.Count > 0
            && !c.requiredEnvironmentTypes.Contains(env))
            return false;

        if (c.requiredTileTypes != null && c.requiredTileTypes.Count > 0
            && !c.requiredTileTypes.Contains(tile))
            return false;

        if (c.requiredSeasonIDs != null && c.requiredSeasonIDs.Count > 0)
        {
            if (string.IsNullOrEmpty(normalizedSeasonID)) return false;
            bool found = false;
            for (int i = 0; i < c.requiredSeasonIDs.Count; i++)
            {
                if (NormalizeID(c.requiredSeasonIDs[i]) == normalizedSeasonID) { found = true; break; }
            }
            if (!found) return false;
        }

        if (c.requiresHasBeenIgnited     && !HasTileState(TileStateFlags.HasBeenIgnited))     return false;
        if (c.requiresIsCurrentlyWet     && !HasTileState(TileStateFlags.IsCurrentlyWet))     return false;
        if (c.requiresWasRecentlyFlooded && !HasTileState(TileStateFlags.WasRecentlyFlooded)) return false;
        if (c.requiresHasCarcass         && !HasTileState(TileStateFlags.HasCarcass))         return false;
        if (c.requiresHasVolcanicAsh     && !HasTileState(TileStateFlags.HasVolcanicAsh))     return false;
        if (c.requiresNotDry             &&  HasTileState(TileStateFlags.IsCurrentlyDry))      return false;

        return true;
    }

    // Overload for callers that still have a SeasonDefinition (InitializeSpawners, etc.)
    private bool SpawnerConditionsMet(ResourceSpawnerDefinition def,
                                      EnvironmentType env,
                                      EnvironmentTileType tile,
                                      SeasonDefinition season)
        => SpawnerConditionsMet(def, env, tile,
               season != null ? NormalizeID(season.seasonID) : string.Empty);

    private static ResourceSpawnerRuntime BuildRuntime(ResourceSpawnerDefinition def,
                                                        SpawnerSourceReason reason)
    {
        return new ResourceSpawnerRuntime
        {
            definition             = def,
            isActive               = true,
            sourceReason           = reason,
            remainingUses          = (def.canExpire && def.maxUses > 0) ? def.maxUses : -1,
            remainingLifetimeTurns = (def.canExpire && def.lifetimeTurns > 0) ? def.lifetimeTurns : -1
        };
    }

    private static string NormalizeID(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return value.Trim().ToLowerInvariant()
                    .Replace(" ", "").Replace("_", "").Replace("-", "");
    }
}
