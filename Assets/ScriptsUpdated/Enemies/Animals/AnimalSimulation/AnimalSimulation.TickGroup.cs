using System;
using UnityEngine;

public partial class AnimalSimulation
{
    /// <summary>
    /// Runs one simulation tick for a single animal group.
    /// Called by AnimalSimulation.Ticking.cs
    /// </summary>
    private void TickGroup(int id, ref AnimalGroupState group, int currentTurn)
    {
        // Hard-cull anything already invalid before it gets another turn.
        if (group.size <= 0 || group.currentHealth <= 0 || group.species == null)
        {
            LogAnimalEvent(
                "DESPAWN",
                group,
                $"Turn {currentTurn}: invalid at tick start. size={group.size}, hp={group.currentHealth}, speciesNull={group.species == null}");

            FinalizeDeadGroup(ref group);
            return;
        }

        var species = group.species;

        group.ageInTurns++;
        ApplyNeedsPerTurn(species, ref group);

        DecayWaterSearchMemory(ref group);

        TileEnvironmentData tileData = _env != null
            ? _env.GetTileData(group.tile)
            : default;

        DecideActionAndExecute(ref group, tileData);

        // NEW: recover some HP each turn before mortality is checked.
        ApplyHealthRecoveryPerTurn(species, ref group);

        int sizeBeforeMortality = group.size;
        float hungerBeforeMortality = group.hunger;
        float thirstBeforeMortality = group.thirst;

        ApplyMortality(ref group);

        if (group.size <= 0 || group.currentHealth <= 0)
        {
            LogAnimalEvent(
                "DESPAWN",
                group,
                $"Turn {currentTurn}: died after ApplyMortality. " +
                $"sizeBeforeMortality={sizeBeforeMortality}, " +
                $"hunger={hungerBeforeMortality:F2}/{species.maxHunger:F2}, " +
                $"thirst={thirstBeforeMortality:F2}/{species.maxThirst:F2}, " +
                $"age={group.ageInTurns}/{species.maxAgeInTurns}, " +
                $"hp={group.currentHealth}");

            FinalizeDeadGroup(ref group);
            return;
        }

        HandleReproduction(currentTurn, ref group);

        // Safety: reproduction / merge / split side effects should not leave a dead shell behind.
        if (group.size <= 0 || group.currentHealth <= 0 || group.species == null)
        {
            LogAnimalEvent(
                "DESPAWN",
                group,
                $"Turn {currentTurn}: invalid after reproduction. size={group.size}, hp={group.currentHealth}, speciesNull={group.species == null}");

            FinalizeDeadGroup(ref group);
            return;
        }

        group.nextUpdateTurn = currentTurn + 1;
        group.EnsureHealthValid();
    }

    /// <summary>
    /// Default hunger/thirst increase each turn.
    /// Uses only fields we already know exist (maxHunger/maxThirst).
    /// </summary>
    private void ApplyNeedsPerTurn(AnimalDefinition species, ref AnimalGroupState group)
    {
        if (species == null)
            return;

        if (species.maxHunger > 0f)
        {
            float hungerGain = Mathf.Max(0f, species.hungerPerTurn);
            group.hunger = Mathf.Clamp(group.hunger + hungerGain, 0f, species.maxHunger);
        }

        if (species.maxThirst > 0f)
        {
            float thirstGain = Mathf.Max(0f, species.thirstPerTurn);
            group.thirst = Mathf.Clamp(group.thirst + thirstGain, 0f, species.maxThirst);
        }
    }

    /// <summary>
    /// Clear any "targeted" flags/icons on other groups when this group dies.
    /// </summary>
    private void CleanupTargetsOnDeath(ref AnimalGroupState group)
    {
        // Clear prey targeting icon
        if (group.isHunting && group.huntingTargetGroupId > 0)
        {
            ClearHuntingTarget(ref group);
        }

        // Clear predator conflict link/icon
        if (group.isInPredatorConflict || group.predatorConflictTargetGroupId > 0)
        {
            ClearPredatorConflictVisuals(ref group);
        }

        // Clear human raid targets (if the HumanRaids partial is in your project)
        if (group.isRaidingPlayerTile)
        {
            ClearHumanRaidTarget(ref group);
        }

        if (group.isHuntingHumanUnits)
        {
            ClearHumanUnitTarget(ref group);
        }
    }

    private void ApplyHealthRecoveryPerTurn(AnimalDefinition species, ref AnimalGroupState group)
    {
        if (species == null || !group.isAlive)
            return;

        int recovery = Mathf.Max(0, species.healthRecoveryPerTurn);
        if (recovery <= 0)
            return;

        group.EnsureHealthValid();

        if (group.currentHealth >= group.MaxHealth)
            return;

        if (species.blockHealthRecoveryWhenCriticalNeeds)
        {
            float hungerPct = species.maxHunger > 0f ? group.hunger / species.maxHunger : 0f;
            float thirstPct = species.maxThirst > 0f ? group.thirst / species.maxThirst : 0f;

            if (hungerPct >= species.starvationThreshold)
                return;

            if (thirstPct >= species.dehydrationThreshold)
                return;
        }

        group.currentHealth = Mathf.Min(group.currentHealth + recovery, group.MaxHealth);
    }

    private void FinalizeDeadGroup(ref AnimalGroupState group)
    {
        CleanupTargetsOnDeath(ref group);
        group.size = 0;
        group.currentHealth = 0;
        group.EnsureHealthValid();
    }
}