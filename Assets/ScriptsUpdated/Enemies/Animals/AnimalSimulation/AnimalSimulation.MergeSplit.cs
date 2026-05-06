using UnityEngine;

public partial class AnimalSimulation
{
    private void TryMergeSameSpeciesGroupsForMating(int currentTurn, ref AnimalGroupState primary)
    {
        var species = primary.species;
        if (species == null || !primary.isAlive || primary.size <= 0)
            return;

        if (!species.allowGroupMergeDuringMatingSeason)
            return;

        if (!IsInMatingSeason(species))
            return;

        int bestCandidateId = -1;
        AnimalGroupState bestCandidate = default;
        bool found = false;
        int bestAgeGap = int.MaxValue;

        // Only consider groups that are actually on the same tile.
        var groupsOnSameTile = GetGroupsOnTile(primary.tile);
        for (int i = 0; i < groupsOnSameTile.Count; i++)
        {
            int otherId = groupsOnSameTile[i];

            if (otherId == primary.id)
                continue;

            if (!_groups.TryGetValue(otherId, out var other))
                continue;

            if (!ShouldMergeSameSpeciesGroupsForMating(primary, other))
                continue;

            int ageGap = Mathf.Abs(primary.ageInTurns - other.ageInTurns);

            if (!found || ageGap < bestAgeGap)
            {
                found = true;
                bestCandidateId = otherId;
                bestCandidate = other;
                bestAgeGap = ageGap;
            }
        }

        if (!found)
            return;

        MergeSameSpeciesGroupsForMating(ref primary, bestCandidateId, ref bestCandidate);
    }

    private bool ShouldMergeSameSpeciesGroupsForMating(AnimalGroupState a, AnimalGroupState b)
    {
        var defA = a.species;
        var defB = b.species;

        if (defA == null || defB == null)
            return false;

        if (defA != defB)
            return false;

        if (!a.isAlive || !b.isAlive)
            return false;

        if (a.size <= 0 || b.size <= 0)
            return false;

        if (a.tile != b.tile)
            return false;

        if (!defA.allowGroupMergeDuringMatingSeason)
            return false;

        if (!IsInMatingSeason(defA))
            return false;

        int minSize = Mathf.Max(1, defA.minGroupSize);

        if (defA.requireSmallGroupForMerge)
        {
            bool anySmall = a.size < minSize || b.size < minSize;
            if (!anySmall)
                return false;
        }

        if (IsTooOldToMerge(a, defA) || IsTooOldToMerge(b, defA))
            return false;

        int allowedAgeGap = Mathf.Max(0, defA.maxGroupMergeAgeDifferenceTurns);
        if (Mathf.Abs(a.ageInTurns - b.ageInTurns) > allowedAgeGap)
            return false;

        return true;
    }

    private bool IsTooOldToMerge(AnimalGroupState group, AnimalDefinition species)
    {
        if (species == null)
            return true;

        if (species.maxAgeInTurns > 0 && group.ageInTurns >= species.maxAgeInTurns)
            return true;

        float frac = Mathf.Clamp01(species.maxMergeAgeFraction);
        if (species.maxAgeInTurns > 0 && frac > 0f)
        {
            int oldestAllowed = Mathf.FloorToInt(species.maxAgeInTurns * frac);
            if (group.ageInTurns > oldestAllowed)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Non-combat helper:
    /// Split one group's existing health pool between parent and child by size.
    /// </summary>
    private void SplitGroupHealthBySizeNonCombat(
        ref AnimalGroupState parent,
        ref AnimalGroupState child,
        int childSize)
    {
        parent.EnsureHealthValid();

        int oldSize = Mathf.Max(0, parent.size);
        childSize = Mathf.Clamp(childSize, 0, oldSize);

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

    /// <summary>
    /// Non-combat helper:
    /// Distribute a merged health pool across two groups by final size.
    /// </summary>
    private void DistributeMergedHealthBySizeNonCombat(
        int totalHealth,
        ref AnimalGroupState a,
        int sizeA,
        ref AnimalGroupState b,
        int sizeB)
    {
        sizeA = Mathf.Max(0, sizeA);
        sizeB = Mathf.Max(0, sizeB);

        int totalSize = sizeA + sizeB;

        a.size = sizeA;
        b.size = sizeB;

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

    private void MergeSameSpeciesGroupsForMating(ref AnimalGroupState primary, int secondaryId, ref AnimalGroupState secondary)
    {
        if (primary.species == null || secondary.species == null || primary.species != secondary.species)
            return;

        if (primary.tile != secondary.tile)
        {
            LogAnimalVsAnimal(
                "MERGE-BLOCKED",
                primary,
                secondary,
                "Blocked same-species mating merge because groups were not on the same tile.");
            return;
        }

        primary.EnsureHealthValid();
        secondary.EnsureHealthValid();

        int primarySize = Mathf.Max(0, primary.size);
        int secondarySize = Mathf.Max(0, secondary.size);
        int totalSize = primarySize + secondarySize;

        if (totalSize <= 0)
            return;

        var species = primary.species;
        int maxGroupSize = Mathf.Max(1, species.maxGroupSize);

        ClearPredatorConflictVisuals(ref primary);
        ClearPredatorConflictVisuals(ref secondary);
        SuppressSpeciesCapForThisTurn(species);

        int keptPrimaryId = primary.id;
        TileCoord mergeTile = primary.tile;

        // Build a fresh merged group object, but KEEP the currently processed primary id.
        AnimalGroupState merged = CreateMergedGroupState(primary, secondary, keptPrimaryId);

        if (totalSize <= maxGroupSize)
        {
            // Only remove the secondary. Keep the primary id alive so TickSomeAnimals
            // can safely write back using the same id it started with.
            RemoveGroup(
                secondaryId,
                secondary.tile,
                "MERGE-REMOVE",
                "Removing secondary group for mating merge replacement.");

            // Replace the active primary state with the merged result.
            primary = merged;

            LogAnimalVsAnimal(
                "MERGE-MATING",
                primary,
                secondary,
                $"Merged same-species groups into NEW averaged state on tile {mergeTile}. " +
                $"keptPrimaryId={primary.id}, finalSize={primary.size}, secondaryRemovedId={secondaryId}");

            // Do NOT add to _groups here; TickSomeAnimals will write back _groups[id] = group
            // using the same primary id it is already processing.
            return;
        }

        // Oversized merged result: keep primary id for one half, create one new sibling group.
        int splitA = totalSize / 2;
        int splitB = totalSize - splitA;

        AnimalGroupState mergedA = CreateCopiedGroupState(merged);
        AnimalGroupState mergedB = CreateCopiedGroupState(merged);

        mergedA.id = keptPrimaryId;
        mergedB.id = _nextGroupId++;

        mergedA.tile = mergeTile;
        mergedB.tile = mergeTile;

        mergedA.lastAction = AnimalActionType.Idle;
        mergedB.lastAction = AnimalActionType.Idle;

        DistributeMergedHealthBySizeNonCombat(
            merged.currentHealth,
            ref mergedA, splitA,
            ref mergedB, splitB);

        // Remove only the secondary; keep primary id alive for the current tick.
        RemoveGroup(
            secondaryId,
            secondary.tile,
            "MERGE-REMOVE",
            "Removing secondary group for mating merge replacement.");

        // Primary half stays in the ref variable and will be written back by TickSomeAnimals.
        primary = mergedA;

        // Secondary half is truly new and must be inserted now.
        _groups[mergedB.id] = mergedB;
        AddToTileIndex(mergedB.id, mergedB.tile);

        if (_groups.ContainsKey(mergedB.id))
            OnGroupCreated?.Invoke(mergedB);

        LogAnimalVsAnimal(
            "MERGE-MATING",
            mergedA,
            mergedB,
            $"Merged same-species groups into NEW averaged groups on tile {mergeTile}, then split due to max group size. " +
            $"keptPrimaryId={mergedA.id}, sizeA={mergedA.size}, newGroupId={mergedB.id}, sizeB={mergedB.size}");
    }

    private int GetCurrentSeasonalGroupCap(AnimalDefinition species)
    {
        if (species == null)
            return 1;

        int matingCap = Mathf.Max(1, species.maxGroupSize);

        if (!species.splitOutsideMatingSeason)
            return matingCap;

        if (IsInMatingSeason(species))
            return matingCap;

        return Mathf.Clamp(species.offSeasonMaxGroupSize, 1, matingCap);
    }

    private bool TryEnqueueOffSeasonSplit(ref AnimalGroupState group)
    {
        var species = group.species;
        if (species == null || !group.isAlive || group.size <= 1)
            return false;

        if (!species.splitOutsideMatingSeason)
            return false;

        if (IsInMatingSeason(species))
            return false;

        int offSeasonCap = GetCurrentSeasonalGroupCap(species);

        int splitSize = group.size - offSeasonCap;
        int minNewGroupSize = Mathf.Max(1, species.offSeasonMinNewGroupSize);

        if (splitSize < minNewGroupSize)
            return false;

        long cap = _maxTotalGroups;
        long used = (long)_groups.Count + _pendingSpawns.Count + _pendingGroupSplits.Count;
        if (used >= cap)
            return false;

        // Structural replacement, not migration.
        SuppressSpeciesCapForThisTurn(species);

        group.EnsureHealthValid();

        AnimalGroupState splitGroup = CreateCopiedGroupState(group);
        splitGroup.lastAction = AnimalActionType.Idle;

        // Clear active combat / chase / flee state on the new split-off group.
        splitGroup.isHunting = false;
        splitGroup.huntingTargetGroupId = -1;
        splitGroup.isTargetedByPredator = false;
        splitGroup.targetedByPredatorGroupId = -1;
        splitGroup.huntingEscapeCount = 0;

        splitGroup.isInPredatorConflict = false;
        splitGroup.predatorConflictTargetGroupId = -1;

        splitGroup.isFleeingFromThreat = false;
        splitGroup.fleeFromPredatorGroupId = -1;
        splitGroup.fleeUntilDistanceTiles = 0;

        splitGroup.isRaidingPlayerTile = false;
        splitGroup.isHuntingHumanUnits = false;

        SplitGroupHealthBySizeNonCombat(ref group, ref splitGroup, splitSize);

        group.lastAction = AnimalActionType.Idle;
        group.isInPredatorConflict = false;
        group.predatorConflictTargetGroupId = -1;

        _pendingGroupSplits.Enqueue(new PendingGroupSplit
        {
            template = splitGroup
        });

        return true;
    }

    private void ProcessPendingGroupSplits()
    {
        while (_pendingGroupSplits.Count > 0)
        {
            if (HasReachedGroupCap())
                break;

            var pending = _pendingGroupSplits.Dequeue();
            var splitGroup = pending.template;

            if (splitGroup.species == null || splitGroup.size <= 0)
                continue;

            splitGroup.id = _nextGroupId++;

            // Keep whatever redistributed health it already got, just clamp to its final max.
            splitGroup.EnsureHealthValid();

            _groups[splitGroup.id] = splitGroup;
            AddToTileIndex(splitGroup.id, splitGroup.tile);

            // NEW: enforce per-species live-group cap for split-created groups too.
            if (!IsSpeciesCapSuppressedThisTurn(splitGroup.species))
                EnforceSpeciesGroupCapFor(splitGroup.species);

            // Only fire created if this split group survived the cap enforcement.
            if (_groups.ContainsKey(splitGroup.id))
                OnGroupCreated?.Invoke(splitGroup);
        }
    }

    private float WeightedAverageFloat(float a, int weightA, float b, int weightB)
    {
        int total = Mathf.Max(0, weightA) + Mathf.Max(0, weightB);
        if (total <= 0)
            return 0f;

        return ((a * Mathf.Max(0, weightA)) + (b * Mathf.Max(0, weightB))) / total;
    }

    private int WeightedAverageInt(int a, int weightA, int b, int weightB)
    {
        int total = Mathf.Max(0, weightA) + Mathf.Max(0, weightB);
        if (total <= 0)
            return 0;

        return Mathf.RoundToInt(((a * Mathf.Max(0, weightA)) + (b * Mathf.Max(0, weightB))) / (float)total);
    }

    private AnimalGroupState CreateMergedGroupState(AnimalGroupState a, AnimalGroupState b, int idToKeep)
    {
        var species = a.species;
        int sizeA = Mathf.Max(0, a.size);
        int sizeB = Mathf.Max(0, b.size);
        int totalSize = sizeA + sizeB;

        var merged = new AnimalGroupState
        {
            id = idToKeep,
            species = species,
            size = totalSize,

            ageInTurns = WeightedAverageInt(a.ageInTurns, sizeA, b.ageInTurns, sizeB),

            currentHealth = 0, // assigned below

            hunger = WeightedAverageFloat(a.hunger, sizeA, b.hunger, sizeB),
            thirst = WeightedAverageFloat(a.thirst, sizeA, b.thirst, sizeB),

            tile = a.tile,

            lastAction = AnimalActionType.Idle,
            nextUpdateTurn = Mathf.Min(a.nextUpdateTurn, b.nextUpdateTurn),

            isLeader = false,
            herdId = 0,
            leaderGroupId = 0,

            isHunting = false,
            huntingTargetGroupId = -1,
            isTargetedByPredator = false,
            huntingEscapeCount = 0,

            nextReproductionTurn = Mathf.Max(a.nextReproductionTurn, b.nextReproductionTurn),
            isOnReproductionCooldown = a.isOnReproductionCooldown || b.isOnReproductionCooldown,

            isInPredatorConflict = false,
            predatorConflictTargetGroupId = -1,

            targetedByPredatorGroupId = -1,

            isFleeingFromThreat = false,
            fleeFromPredatorGroupId = -1,
            fleeUntilDistanceTiles = 0,
            fleeThreatLastKnownTile = a.tile,
            fleeStepsRemaining = 0,

            hasWaterSearchMemory = false,
            lastWaterSearchPreviousTile = a.tile,
            secondLastWaterSearchPreviousTile = a.tile,
            waterSearchBacktrackAvoidanceTurns = 0,

            isRaidingPlayerTile = false,
            raidTargetTile = a.tile,

            isHuntingHumanUnits = false,
            huntingHumanUnitGroupId = null,

            resolvedHealthPerAnimal = WeightedAverageInt(a.HealthPerAnimal, sizeA, b.HealthPerAnimal, sizeB),
            resolvedAggression = WeightedAverageFloat(a.Aggression, sizeA, b.Aggression, sizeB),
            resolvedFlightiness = WeightedAverageFloat(a.Flightiness, sizeA, b.Flightiness, sizeB),
            resolvedHerding = WeightedAverageFloat(a.Herding, sizeA, b.Herding, sizeB),
            resolvedStrength = WeightedAverageFloat(a.Strength, sizeA, b.Strength, sizeB),
            resolvedDefense = WeightedAverageFloat(a.Defense, sizeA, b.Defense, sizeB),
            resolvedSpeed = WeightedAverageFloat(a.Speed, sizeA, b.Speed, sizeB),
            resolvedSense = WeightedAverageFloat(a.Sense, sizeA, b.Sense, sizeB),
            resolvedStealth = WeightedAverageFloat(a.Stealth, sizeA, b.Stealth, sizeB),

            resolvedBreedingFemaleFraction =
                WeightedAverageFloat(a.BreedingFemaleFraction, sizeA, b.BreedingFemaleFraction, sizeB)
        };

        int totalHealth = Mathf.Max(0, a.currentHealth) + Mathf.Max(0, b.currentHealth);
        merged.EnsureHealthValid();
        merged.currentHealth = Mathf.Clamp(totalHealth, 0, merged.MaxHealth);

        return merged;
    }
}