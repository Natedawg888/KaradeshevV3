using System;
using UnityEngine;

public partial class AnimalSimulation
{
    private void HandleReproduction(int currentTurn, ref AnimalGroupState group)
    {
        var species = group.species;
        if (species == null)
            return;

        if (!group.isAlive || group.size <= 1)
            return;

        TryMergeSameSpeciesGroupsForMating(currentTurn, ref group);

        species = group.species;
        if (species == null || !group.isAlive || group.size <= 1)
            return;

        if (TryEnqueueOffSeasonSplit(ref group))
        {
            _groups[group.id] = group;
            OnGroupUpdated?.Invoke(group);
            return;
        }

        if (HasReachedGroupCap())
            return;

        if (!CanGroupReproduce(currentTurn, ref group, species))
            return;

        int breedingUnits = GetBreedingUnits(group);
        if (breedingUnits <= 0)
            return;

        int minLitter = Mathf.Max(1, species.minLitterSize);
        int maxLitter = Mathf.Max(minLitter, species.maxLitterSize);

        int totalOffspring = 0;
        for (int i = 0; i < breedingUnits; i++)
        {
            int litter = _rng.Next(minLitter, maxLitter + 1);
            totalOffspring += litter;
        }

        if (totalOffspring <= 0)
            return;

        int speciesMaxGroupSize = Mathf.Max(1, species.maxGroupSize);

        int remaining = totalOffspring;
        bool enqueuedAny = false;

        long cap = _maxTotalGroups;
        long used = (long)_groups.Count + _pendingSpawns.Count;
        long availableGroupSlots = cap - used;

        if (availableGroupSlots <= 0)
            return;

        while (remaining > 0 && availableGroupSlots > 0)
        {
            int spawnSize = Mathf.Min(remaining, speciesMaxGroupSize);

            if (TryEnqueueReproductionSpawn(species, group.tile, spawnSize, allowBelowMinGroupSize: true))
            {
                remaining -= spawnSize;
                availableGroupSlots--;
                enqueuedAny = true;
            }
            else
            {
                break;
            }
        }

        if (!enqueuedAny)
            return;

        group.nextReproductionTurn = currentTurn + Mathf.Max(1, species.reproduceCooldownTurns);
        group.isOnReproductionCooldown = true;
    }

    private int GetActiveGroupSizeCap(AnimalDefinition species)
    {
        if (species == null)
            return 1;

        int inSeasonCap = Mathf.Max(1, species.maxGroupSize);

        if (IsInMatingSeason(species))
            return inSeasonCap;

        if (species.splitOutsideMatingSeason)
            return Mathf.Max(1, species.offSeasonMaxGroupSize);

        return inSeasonCap;
    }

    private bool CanGroupReproduce(int currentTurn, ref AnimalGroupState group, AnimalDefinition species)
    {
        if (group.isOnReproductionCooldown && currentTurn < group.nextReproductionTurn)
            return false;

        if (group.isOnReproductionCooldown && currentTurn >= group.nextReproductionTurn)
            group.isOnReproductionCooldown = false;

        if (group.ageInTurns < Mathf.Max(0, species.minReproductiveAgeTurns))
            return false;

        if (species.maxAgeInTurns > 0 && group.ageInTurns >= species.maxAgeInTurns)
            return false;

        if (!IsInMatingSeason(species))
            return false;

        if (group.size < Mathf.Max(1, species.minGroupSize))
            return false;

        float hungerPct = species.maxHunger > 0f ? group.hunger / species.maxHunger : 0f;
        float thirstPct = species.maxThirst > 0f ? group.thirst / species.maxThirst : 0f;

        float needLimit = Mathf.Clamp01(species.maxNeedFractionForMating);
        if (hungerPct > needLimit) return false;
        if (thirstPct > needLimit) return false;

        return true;
    }

    private bool IsInMatingSeason(AnimalDefinition species)
    {
        if (species == null)
            return false;

        if (SeasonManager.Instance == null)
            return true;

        SeasonDefinition currentSeason = SeasonManager.Instance.CurrentSeason;
        return species.IsMatingSeason(currentSeason);
    }

    private int GetBreedingUnits(AnimalGroupState group)
    {
        var species = group.species;
        if (species == null || group.size <= 1)
            return 0;

        float frac = group.BreedingFemaleFraction;
        if (frac <= 0f)
            return 0;

        int breedingFemales = Mathf.FloorToInt(group.size * frac);
        if (breedingFemales <= 0)
            return 0;

        switch (species.matingSystem)
        {
            case MatingSystem.MonogamousPair:
                {
                    int availablePartners = Mathf.Max(0, group.size - breedingFemales);
                    return Mathf.Min(breedingFemales, availablePartners);
                }

            case MatingSystem.OneMaleMultiFemale:
                {
                    return group.size >= 2 ? breedingFemales : 0;
                }

            case MatingSystem.Polygamous:
                {
                    return breedingFemales;
                }

            default:
                return breedingFemales;
        }
    }

    private bool TryEnqueueReproductionSpawn(
        AnimalDefinition species,
        TileCoord tile,
        int size,
        bool allowBelowMinGroupSize = false)
    {
        if (species == null || size <= 0)
            return false;

        int minGroup = Mathf.Max(1, species.minGroupSize);
        int maxGroup = Mathf.Max(minGroup, species.maxGroupSize);

        if (!allowBelowMinGroupSize && size < minGroup)
            return false;

        if (size > maxGroup)
            return false;

        if (_pendingSpawns.Count >= _maxTotalGroups * 4)
            return false;

        SuppressSpeciesCapForThisTurn(species);

        var newborn = new AnimalGroupState
        {
            id = -1,
            species = species,
            size = size,
            ageInTurns = 0,
            currentHealth = -1,
            hunger = 0f,
            thirst = 0f,
            tile = tile,
            lastAction = AnimalActionType.Idle,
            nextUpdateTurn = 0,

            isLeader = false,
            herdId = 0,
            leaderGroupId = 0,

            isHunting = false,
            huntingTargetGroupId = -1,
            isTargetedByPredator = false,
            targetedByPredatorGroupId = -1,
            huntingEscapeCount = 0,

            nextReproductionTurn = 0,
            isOnReproductionCooldown = false,

            isInPredatorConflict = false,
            predatorConflictTargetGroupId = -1,

            isFleeingFromThreat = false,
            fleeFromPredatorGroupId = -1,
            fleeUntilDistanceTiles = 0,
            fleeThreatLastKnownTile = tile,
            fleeStepsRemaining = 0,

            hasWaterSearchMemory = false,
            lastWaterSearchPreviousTile = tile,
            secondLastWaterSearchPreviousTile = tile,
            waterSearchBacktrackAvoidanceTurns = 0,

            isRaidingPlayerTile = false,
            raidTargetTile = tile,

            isHuntingHumanUnits = false,
            huntingHumanUnitGroupId = null
        };

        RollGroupCoreStats(ref newborn);
        newborn.EnsureHealthValid();

        _pendingSpawns.Enqueue(new PendingSpawn
        {
            template = newborn
        });

        return true;
    }
}