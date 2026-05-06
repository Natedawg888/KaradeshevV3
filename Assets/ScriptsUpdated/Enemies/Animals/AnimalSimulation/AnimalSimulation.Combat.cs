using System;
using System.Collections.Generic;
using UnityEngine;

public partial class AnimalSimulation
{
    // Reused neighbour buffer to avoid per-call list allocations on OUR side.
    // (If your _env.GetNeighbourTiles allocates internally, we’ll fix that in the env next.)
    private readonly List<TileCoord> _neighbourBuf = new List<TileCoord>(128);

    private List<TileCoord> GetNeighbourTilesCached(TileCoord origin, int range)
    {
        _neighbourBuf.Clear();
        foreach (var c in _env.GetNeighbourTiles(origin, range))
            _neighbourBuf.Add(c);
        return _neighbourBuf;
    }

    private bool TryHandlePredatorConflict(ref AnimalGroupState group, float hungerPct, float thirstPct)
    {
        var species = group.species;
        if (species == null)
            return false;

        bool hasDislikedList = species.dislikedPredators != null && species.dislikedPredators.Length > 0;

        int range = species.predatorConflictRangeTiles > 0
            ? species.predatorConflictRangeTiles
            : Math.Max(1, species.huntingRangeTiles);

        bool ownSpeciesCrowded = CanUseOwnSpeciesConflict(
            group,
            range,
            out int nearbySameSpeciesGroups,
            out int nearbySameSpeciesAnimals);

        // If this species has neither disliked-predator conflict nor overcrowded same-species conflict, do nothing.
        if (!hasDislikedList && !ownSpeciesCrowded)
            return false;

        bool attackerIsPredatorLike = IsPredatorLike(species);

        float huntingThreshold = species.huntingHungerThreshold > 0f
            ? species.huntingHungerThreshold
            : 0.15f;

        bool hungryEnoughToTreatTargetAsPrey =
            attackerIsPredatorLike && hungerPct >= huntingThreshold;

        int nearbyConflictTargetCount = CountConflictTargetsNearby(group, range, ownSpeciesCrowded);

        float territoriality = Mathf.Clamp01(species.predatorTerritoriality);
        float densityPressure = nearbyConflictTargetCount * Mathf.Lerp(0.5f, 1.5f, territoriality);

        bool conflictPressureHigh =
            densityPressure >= Mathf.Max(1f, species.predatorDensityConflictThreshold) ||
            ownSpeciesCrowded;

        if (!conflictPressureHigh && !group.isInPredatorConflict)
            return false;

        AnimalGroupState existingTarget = default;
        bool hasExistingTarget = false;

        if (group.isInPredatorConflict &&
            group.predatorConflictTargetGroupId > 0 &&
            _groups.TryGetValue(group.predatorConflictTargetGroupId, out existingTarget) &&
            existingTarget.isAlive &&
            existingTarget.size > 0 &&
            IsConflictTargetForSpecies(group, existingTarget, ownSpeciesCrowded))
        {
            hasExistingTarget = true;
        }

        bool sameTileAsExisting = hasExistingTarget && existingTarget.tile.Equals(group.tile);

        float needThreshold = species.conflictNeedThreshold > 0f
            ? species.conflictNeedThreshold
            : 0.4f;

        if (!sameTileAsExisting && !hungryEnoughToTreatTargetAsPrey)
        {
            if (hungerPct > needThreshold || thirstPct > needThreshold)
            {
                SuspendPredatorConflictIcons(ref group);
                return false;
            }
        }

        if (!sameTileAsExisting)
        {
            float roll = (float)_rng.NextDouble();
            if (roll > group.Aggression)
            {
                SuspendPredatorConflictIcons(ref group);
                return false;
            }
        }

        int targetId;
        AnimalGroupState targetGroup;

        if (hasExistingTarget)
        {
            int dist = Math.Abs(existingTarget.tile.x - group.tile.x) +
                       Math.Abs(existingTarget.tile.y - group.tile.y);

            if (dist <= range && ShouldEngagePredatorTarget(group, existingTarget))
            {
                targetId = group.predatorConflictTargetGroupId;
                targetGroup = existingTarget;
            }
            else
            {
                ClearPredatorConflictVisuals(ref group);

                if (!TryAcquireConflictTarget(ref group, range, ownSpeciesCrowded, out targetId, out targetGroup))
                    return false;
            }
        }
        else
        {
            if (!TryAcquireConflictTarget(ref group, range, ownSpeciesCrowded, out targetId, out targetGroup))
                return false;
        }

        SetPredatorConflictVisuals(ref group, targetId, ref targetGroup);

        if (targetGroup.tile.Equals(group.tile))
        {
            ResolvePredatorConflict(ref group, targetId, ref targetGroup);
        }
        else
        {
            var next = StepTowards(group.tile, targetGroup.tile, false);
            group.tile = next;
            group.lastAction = AnimalActionType.Move;
        }

        return true;
    }

    private int CountConflictTargetsNearby(AnimalGroupState attacker, int range)
    {
        var attackerDef = attacker.species;
        if (attackerDef == null)
            return 0;

        int count = 0;
        TileCoord origin = attacker.tile;

        var neighbours = GetNeighbourTilesCached(origin, range);
        for (int n = 0; n < neighbours.Count; n++)
        {
            var coord = neighbours[n];

            int dist = Math.Abs(coord.x - origin.x) + Math.Abs(coord.y - origin.y);
            if (dist == 0 || dist > range)
                continue;

            if (!_tileIndex.TryGetValue(coord, out var ids) || ids == null)
                continue;

            for (int i = 0; i < ids.Count; i++)
            {
                int id = ids[i];
                if (id == attacker.id)
                    continue;

                if (!_groups.TryGetValue(id, out var other) || !other.isAlive || other.size <= 0)
                    continue;

                if (other.species == null)
                    continue;

                if (!IsCandidateDislikedPredatorForConflict(attackerDef, other.species))
                    continue;

                if (!ShouldEngagePredatorTarget(attacker, other))
                    continue;

                count++;
            }
        }

        return count;
    }

    private bool TryAcquireDislikedPredatorTarget(
    ref AnimalGroupState attacker,
    int range,
    out int targetId,
    out AnimalGroupState targetGroup)
    {
        targetId = -1;
        targetGroup = default;

        var attackerDef = attacker.species;
        if (attackerDef == null)
            return false;

        TileCoord origin = attacker.tile;

        bool found = false;
        float bestScore = float.NegativeInfinity;
        int bestId = -1;
        AnimalGroupState best = default;

        var neighbours = GetNeighbourTilesCached(origin, range);
        for (int n = 0; n < neighbours.Count; n++)
        {
            var coord = neighbours[n];

            int dist = Math.Abs(coord.x - origin.x) + Math.Abs(coord.y - origin.y);
            if (dist == 0 || dist > range)
                continue;

            if (!_tileIndex.TryGetValue(coord, out var ids) || ids == null || ids.Count == 0)
                continue;

            for (int i = 0; i < ids.Count; i++)
            {
                int id = ids[i];
                if (id == attacker.id)
                    continue;

                if (!_groups.TryGetValue(id, out var candidate))
                    continue;
                if (!candidate.isAlive || candidate.size <= 0)
                    continue;

                var otherDef = candidate.species;
                if (otherDef == null)
                    continue;

                if (!IsCandidateDislikedPredatorForConflict(attackerDef, otherDef))
                    continue;

                if (!ShouldEngagePredatorTarget(attacker, candidate))
                    continue;

                float candidateWeakness = GetGroupWeakness01(candidate);
                float attackerPower = GetGroupCombatPower(attacker, includeWeakness: true);
                float candidatePower = GetGroupCombatPower(candidate, includeWeakness: true);

                float distanceScore = 1f / Mathf.Max(1, dist);
                float weaknessScore = candidateWeakness * 1.5f;
                float powerScore = attackerPower > 0f ? Mathf.Clamp((attackerPower - candidatePower) / attackerPower, -1f, 1f) : 0f;
                float ownSpeciesBias =
                    (attackerDef == otherDef && CanUseOwnSpeciesConflict(attackerDef))
                    ? attackerDef.ownSpeciesConflictBias
                    : 1f;

                float score = (distanceScore * 1.25f + weaknessScore + powerScore) * ownSpeciesBias;

                if (!found || score > bestScore)
                {
                    found = true;
                    bestScore = score;
                    bestId = id;
                    best = candidate;
                }
            }
        }

        if (!found)
            return false;

        targetId = bestId;
        targetGroup = best;
        return true;
    }

    private static bool IsDislikedPredator(AnimalDefinition self, AnimalDefinition other)
    {
        return IsInDislikedList(self?.dislikedPredators, other);
    }

    private static bool IsInDislikedList(AnimalDefinition[] disliked, AnimalDefinition other)
    {
        if (disliked == null || other == null)
            return false;

        for (int i = 0; i < disliked.Length; i++)
        {
            if (disliked[i] == other)
                return true;
        }
        return false;
    }

    private void ResolvePredatorConflict(
    ref AnimalGroupState attacker,
    int defenderId,
    ref AnimalGroupState defender)
    {
        var attackerDef = attacker.species;
        var defenderDef = defender.species;

        if (attackerDef == null || defenderDef == null)
            return;

        attacker.EnsureHealthValid();
        defender.EnsureHealthValid();

        TileCoord attackerTile = attacker.tile;
        TileCoord defenderOriginalTile = defender.tile;

        float fleeBase = defender.Flightiness;
        if (defender.size < attacker.size)
            fleeBase = Mathf.Clamp01(fleeBase + 0.25f);

        bool attackerActsFirst = RollPredatorConflictInitiative(attacker, defender);

        // -------------------------------------------------
        // Defender wins initiative: reacts before attacker
        // -------------------------------------------------
        if (!attackerActsFirst)
        {
            // Defender can escape before the attack lands
            if (RollEscape(defender, attacker, fleeBase))
            {
                TryLeaveEscapeStragglers(ref defender, defenderOriginalTile);

                TileCoord newTile = StepAwayFrom(defender.tile, attackerTile);
                defender.tile = newTile;
                defender.lastAction = AnimalActionType.Flee;

                BreakPredatorConflictOnFlee(
                    ref defender, defenderId,
                    ref attacker, attacker.id);

                if (!defenderOriginalTile.Equals(defender.tile))
                    MoveGroupInTileIndex(defenderId, defenderOriginalTile, defender.tile);

                return;
            }

            // Defender strikes first
            bool defenderHit = RollCombatHit(defender, attacker, false);
            defender.lastAction = AnimalActionType.DefendAnimal;

            if (defenderHit)
            {
                DealPredatorCombatDamage(ref defender, ref attacker, 0.35f);

                if (!attacker.isAlive)
                {
                    LogAnimalVsAnimal(
                        "KILL-PRED-CONFLICT",
                        defender,
                        attacker,
                        "Defender killed attacker during predator conflict after winning initiative.");

                    attacker.currentHealth = 0;
                    attacker.size = 0;
                    ClearPredatorConflictVisuals(ref attacker);

                    _groups[defenderId] = defender;
                    OnGroupUpdated?.Invoke(defender);
                    return;
                }
            }
            else
            {
                LogAnimalVsAnimal(
                    "MISS-PRED-CONFLICT",
                    defender,
                    attacker,
                    "Defender won initiative in predator conflict but missed the opening strike.");
            }
        }

        // -------------------------------------------------
        // Attacker acts (either won initiative or survived it)
        // -------------------------------------------------
        bool attackerHit = RollCombatHit(attacker, defender, false);
        attacker.lastAction = AnimalActionType.AttackAnimal;

        if (attackerHit)
        {
            DealPredatorCombatDamage(ref attacker, ref defender, 0.45f);
        }
        else
        {
            LogAnimalVsAnimal(
                "MISS-PRED-CONFLICT",
                attacker,
                defender,
                "Attacker struck during predator conflict but missed.");
        }

        if (defender.isAlive)
        {
            if (attackerActsFirst)
            {
                // If attacker went first, defender still gets a reaction.
                if (RollEscape(defender, attacker, fleeBase))
                {
                    TryLeaveEscapeStragglers(ref defender, defenderOriginalTile);

                    TileCoord newTile = StepAwayFrom(defender.tile, attackerTile);
                    defender.tile = newTile;
                    defender.lastAction = AnimalActionType.Flee;

                    BreakPredatorConflictOnFlee(
                        ref defender, defenderId,
                        ref attacker, attacker.id);

                    if (!defenderOriginalTile.Equals(defender.tile))
                        MoveGroupInTileIndex(defenderId, defenderOriginalTile, defender.tile);

                    return;
                }
                else
                {
                    bool defenderHit = RollCombatHit(defender, attacker, false);
                    defender.lastAction = AnimalActionType.DefendAnimal;

                    if (defenderHit)
                    {
                        DealPredatorCombatDamage(ref defender, ref attacker, 0.35f);
                    }
                    else
                    {
                        LogAnimalVsAnimal(
                            "MISS-PRED-CONFLICT",
                            defender,
                            attacker,
                            "Defender reacted after the opening strike in predator conflict, but missed.");
                    }
                }
            }
        }
        else
        {
            defender.currentHealth = 0;
            defender.size = 0;
            defender.lastAction = AnimalActionType.Flee;
        }

        bool defenderFled = defender.lastAction == AnimalActionType.Flee &&
        !defender.isInPredatorConflict &&
        defender.predatorConflictTargetGroupId < 0;

        bool defenderDead = !defender.isAlive;
        bool attackerDead = !attacker.isAlive;

        if (defenderDead)
        {
            LogAnimalVsAnimal(
                "KILL-PRED-CONFLICT",
                attacker,
                defender,
                "Attacker killed defender during predator conflict.");

            defender.currentHealth = 0;
            defender.size = 0;
            RemoveGroup(defenderId, defenderOriginalTile);
        }
        else if (!defenderFled)
        {
            if (!defenderOriginalTile.Equals(defender.tile))
                MoveGroupInTileIndex(defenderId, defenderOriginalTile, defender.tile);

            _groups[defenderId] = defender;
            OnGroupUpdated?.Invoke(defender);
        }

        if (attackerDead)
        {
            LogAnimalVsAnimal(
                "KILL-PRED-CONFLICT",
                defender,
                attacker,
                "Defender killed attacker during predator conflict counterattack.");

            attacker.currentHealth = 0;
            attacker.size = 0;
        }

        if (attackerDead || defenderDead)
            ClearPredatorConflictVisuals(ref attacker);
    }

    private TileCoord StepAwayFrom(TileCoord from, TileCoord threat)
    {
        TileCoord best = from;
        int bestDist = Math.Abs(from.x - threat.x) + Math.Abs(from.y - threat.y);

        var neighbours = GetNeighbourTilesCached(from, 1);
        for (int i = 0; i < neighbours.Count; i++)
        {
            var coord = neighbours[i];
            int dist = Math.Abs(coord.x - threat.x) + Math.Abs(coord.y - threat.y);
            if (dist > bestDist)
            {
                best = coord;
                bestDist = dist;
            }
        }

        return best;
    }

    private void SetPredatorConflictVisuals(
    ref AnimalGroupState attacker,
    int targetId,
    ref AnimalGroupState targetGroup)
    {
        attacker.isInPredatorConflict = true;
        attacker.predatorConflictTargetGroupId = targetId;

        // Only predator-like species should show hunting state during conflict.
        attacker.isHunting = IsPredatorLike(attacker.species);

        if (!targetGroup.isTargetedByPredator)
        {
            targetGroup.isTargetedByPredator = true;
            targetGroup.targetedByPredatorGroupId = attacker.id;
            _groups[targetId] = targetGroup;
            OnGroupUpdated?.Invoke(targetGroup);
        }
    }

    private void ClearPredatorConflictVisuals(ref AnimalGroupState attacker)
    {
        // Clear hunted flag on the old target, if any
        if (attacker.predatorConflictTargetGroupId > 0 &&
    _groups.TryGetValue(attacker.predatorConflictTargetGroupId, out var prevTarget))
        {
            if (prevTarget.targetedByPredatorGroupId == attacker.id)
            {
                prevTarget.isTargetedByPredator = false;
                prevTarget.targetedByPredatorGroupId = -1;

                if (prevTarget.fleeFromPredatorGroupId == attacker.id)
                    ClearThreatResponse(ref prevTarget, clearTargetedFlag: false);

                _groups[prevTarget.id] = prevTarget;
                OnGroupUpdated?.Invoke(prevTarget);
            }
        }

        attacker.isInPredatorConflict = false;
        attacker.predatorConflictTargetGroupId = -1;
        attacker.isHunting = false;
    }

    private void SuspendPredatorConflictIcons(ref AnimalGroupState attacker)
    {
        // Turn OFF the hunted icon on the current conflict target,
        // but DO NOT clear isInPredatorConflict or predatorConflictTargetGroupId.
        if (attacker.predatorConflictTargetGroupId > 0 &&
    _groups.TryGetValue(attacker.predatorConflictTargetGroupId, out var target))
        {
            if (target.targetedByPredatorGroupId == attacker.id)
            {
                target.isTargetedByPredator = false;
                target.targetedByPredatorGroupId = -1;
                _groups[target.id] = target;
                OnGroupUpdated?.Invoke(target);
            }
        }

        // Turn OFF the hunting icon for the attacker for now.
        attacker.isHunting = false;
    }

    private float GetNeedPercent(float value, float max)
    {
        if (max <= 0f) return 0f;
        return Mathf.Clamp01(value / max);
    }

    private float GetGroupWeakness01(AnimalGroupState group)
    {
        var def = group.species;
        if (def == null)
            return 0f;

        float hungerPct = GetNeedPercent(group.hunger, def.maxHunger);
        float thirstPct = GetNeedPercent(group.thirst, def.maxThirst);

        // Needs weakness
        float worstNeed = Mathf.Max(hungerPct, thirstPct);
        float avgNeed = (hungerPct + thirstPct) * 0.5f;
        float needWeakness = Mathf.Clamp01(worstNeed * 0.65f + avgNeed * 0.35f);

        // Age weakness
        float ageWeakness01 = GetAgeWeakness01(group);
        float ageContribution = Mathf.Clamp01(def.maxAgeWeaknessContribution) * ageWeakness01;

        // Final combined weakness
        return Mathf.Clamp01(needWeakness + ageContribution);
    }

    private float GetAgeWeakness01(AnimalGroupState group)
    {
        var def = group.species;
        if (def == null)
            return 0f;

        if (def.maxAgeInTurns <= 0)
            return 0f;

        float startFrac = Mathf.Clamp01(def.ageWeaknessStartsFraction);
        int startAge = Mathf.RoundToInt(def.maxAgeInTurns * startFrac);

        if (group.ageInTurns <= startAge)
            return 0f;

        if (group.ageInTurns >= def.maxAgeInTurns)
            return 1f;

        return Mathf.InverseLerp(startAge, def.maxAgeInTurns, group.ageInTurns);
    }

    private float GetAgeEscapePenalty01(AnimalGroupState group)
    {
        // Reuse the same age weakness curve, but keep escape penalty a bit softer than combat weakness.
        float ageWeakness = GetAgeWeakness01(group);

        // 0 age weakness = no penalty
        // 1 age weakness = 0.35 penalty
        return Mathf.Clamp01(ageWeakness * 0.35f);
    }

    private float GetGroupCombatPower(AnimalGroupState group, bool includeWeakness = true)
    {
        var def = group.species;
        if (def == null || group.size <= 0)
            return 0f;

        float sizeMult = SizeCombatMult(def.sizeCategory);
        float strengthMult = StrengthCombatMult(group.Strength);
        float defenseMult = DefenseCombatMult(group.Defense);
        float speedMult = SpeedCombatMult(group.Speed, def.sizeCategory);

        // sizeCategory x groupSize x strength x defense x speed x weakness
        float basePower =
            Mathf.Max(1f, group.size) *
            sizeMult *
            strengthMult *
            defenseMult *
            speedMult;

        if (!includeWeakness)
            return basePower;

        float weakness = GetGroupWeakness01(group);

        // 0 weakness = 1.0x power
        // 1 weakness = 0.35x power
        float weaknessMult = Mathf.Lerp(1f, 0.35f, weakness);

        return basePower * weaknessMult;
    }

    private int CountPredatorGroupsNearby(TileCoord origin, int range, int excludeGroupId)
    {
        int count = 0;
        var neighbours = GetNeighbourTilesCached(origin, range);

        for (int n = 0; n < neighbours.Count; n++)
        {
            var coord = neighbours[n];

            if (!_tileIndex.TryGetValue(coord, out var ids) || ids == null)
                continue;

            for (int i = 0; i < ids.Count; i++)
            {
                int id = ids[i];
                if (id == excludeGroupId)
                    continue;

                if (!_groups.TryGetValue(id, out var other) || !other.isAlive || other.size <= 0)
                    continue;

                var otherDef = other.species;
                if (otherDef == null)
                    continue;

                if (otherDef.diet == AnimalDiet.Carnivore || otherDef.diet == AnimalDiet.Omnivore)
                    count++;
            }
        }

        return count;
    }

    private bool CanUseOwnSpeciesConflict(AnimalDefinition species)
    {
        if (species == null || !species.allowOwnSpeciesConflictOutOfMatingSeason)
            return false;

        return !IsInMatingSeason(species);
    }

    private bool IsCandidateDislikedPredatorForConflict(AnimalDefinition attackerDef, AnimalDefinition otherDef)
    {
        if (attackerDef == null || otherDef == null)
            return false;

        if (IsInDislikedList(attackerDef.dislikedPredators, otherDef))
            return true;

        if (attackerDef == otherDef && CanUseOwnSpeciesConflict(attackerDef))
            return true;

        return false;
    }

    private bool ShouldEngagePredatorTarget(AnimalGroupState attacker, AnimalGroupState candidate)
    {
        var attackerDef = attacker.species;
        var targetDef = candidate.species;

        if (attackerDef == null || targetDef == null)
            return false;

        float attackerPower = GetGroupCombatPower(attacker, includeWeakness: true);
        float targetPower = GetGroupCombatPower(candidate, includeWeakness: true);

        if (attackerPower <= 0f)
            return false;

        bool targetBigger =
            (int)targetDef.sizeCategory > (int)attackerDef.sizeCategory &&
            candidate.Strength > attacker.Strength &&
            candidate.size > attacker.size;

        float powerRatio = targetPower / attackerPower;
        float targetWeakness = GetGroupWeakness01(candidate);

        // Default behavior: avoid clearly stronger targets
        if (!targetBigger && powerRatio <= attackerDef.maxTargetPowerAdvantageToEngage)
            return true;

        // Risky engage is allowed only if attacker is aggressive enough
        // and target weakness is high enough to make the fight plausible.
        bool riskyEnough = attacker.Aggression >= attackerDef.riskyConflictAggressionThreshold;
        bool targetWeakEnough = targetWeakness >= attackerDef.weaknessThresholdToChallengeStrongerTarget;

        if (riskyEnough && targetWeakEnough)
            return true;

        return false;
    }

    private bool CanUseOwnSpeciesConflict(
    AnimalGroupState attacker,
    int range,
    out int nearbySameSpeciesGroups,
    out int nearbySameSpeciesAnimals)
    {
        nearbySameSpeciesGroups = 0;
        nearbySameSpeciesAnimals = 0;

        var species = attacker.species;
        if (species == null)
            return false;

        // This rule is for predator-like / solitary territorial species.
        if (!IsPredatorLike(species))
            return false;

        if (!species.allowOwnSpeciesConflict)
            return false;

        if (attacker.Herding > species.ownSpeciesConflictMaxHerding)
            return false;

        bool inMatingSeason = IsInMatingSeason(species);

        if (inMatingSeason && !species.allowOwnSpeciesConflictInMatingSeason)
            return false;

        if (!inMatingSeason && !species.allowOwnSpeciesConflictOutOfMatingSeason)
            return false;

        CountNearbySameSpecies(attacker, range, out nearbySameSpeciesGroups, out nearbySameSpeciesAnimals);

        return nearbySameSpeciesGroups >= Mathf.Max(1, species.ownSpeciesConflictMinNearbyGroups) ||
               nearbySameSpeciesAnimals >= Mathf.Max(1, species.ownSpeciesConflictMinNearbyAnimals);
    }

    private void CountNearbySameSpecies(
        AnimalGroupState attacker,
        int range,
        out int groupCount,
        out int animalCount)
    {
        groupCount = 0;
        animalCount = 0;

        var species = attacker.species;
        if (species == null)
            return;

        TileCoord origin = attacker.tile;
        var neighbours = GetNeighbourTilesCached(origin, range);

        for (int n = 0; n < neighbours.Count; n++)
        {
            var coord = neighbours[n];

            int dist = Math.Abs(coord.x - origin.x) + Math.Abs(coord.y - origin.y);
            if (dist == 0 || dist > range)
                continue;

            if (!_tileIndex.TryGetValue(coord, out var ids) || ids == null)
                continue;

            for (int i = 0; i < ids.Count; i++)
            {
                int id = ids[i];
                if (id == attacker.id)
                    continue;

                if (!_groups.TryGetValue(id, out var other) || !other.isAlive || other.size <= 0)
                    continue;

                if (other.species != species)
                    continue;

                groupCount++;
                animalCount += other.size;
            }
        }
    }

    private bool IsConflictTargetForSpecies(
        AnimalGroupState attacker,
        AnimalGroupState other,
        bool allowOwnSpeciesConflict)
    {
        if (attacker.species == null || other.species == null)
            return false;

        if (!other.isAlive || other.size <= 0)
            return false;

        if (other.id == attacker.id)
            return false;

        if (other.species == attacker.species)
            return allowOwnSpeciesConflict;

        return IsCandidateDislikedPredatorForConflict(attacker.species, other.species);
    }

    private int CountConflictTargetsNearby(AnimalGroupState attacker, int range, bool allowOwnSpeciesConflict)
    {
        int count = 0;
        TileCoord origin = attacker.tile;
        var neighbours = GetNeighbourTilesCached(origin, range);

        for (int n = 0; n < neighbours.Count; n++)
        {
            var coord = neighbours[n];

            int dist = Math.Abs(coord.x - origin.x) + Math.Abs(coord.y - origin.y);
            if (dist == 0 || dist > range)
                continue;

            if (!_tileIndex.TryGetValue(coord, out var ids) || ids == null)
                continue;

            for (int i = 0; i < ids.Count; i++)
            {
                int id = ids[i];
                if (id == attacker.id)
                    continue;

                if (!_groups.TryGetValue(id, out var other) || !other.isAlive || other.size <= 0)
                    continue;

                if (!IsConflictTargetForSpecies(attacker, other, allowOwnSpeciesConflict))
                    continue;

                if (!ShouldEngagePredatorTarget(attacker, other))
                    continue;

                count++;
            }
        }

        return count;
    }

    private bool TryAcquireConflictTarget(
        ref AnimalGroupState attacker,
        int range,
        bool allowOwnSpeciesConflict,
        out int targetId,
        out AnimalGroupState targetGroup)
    {
        targetId = -1;
        targetGroup = default;

        var attackerDef = attacker.species;
        if (attackerDef == null)
            return false;

        bool found = false;
        float bestScore = float.NegativeInfinity;
        TileCoord origin = attacker.tile;

        var neighbours = GetNeighbourTilesCached(origin, range);
        for (int n = 0; n < neighbours.Count; n++)
        {
            var coord = neighbours[n];

            int dist = Math.Abs(coord.x - origin.x) + Math.Abs(coord.y - origin.y);
            if (dist == 0 || dist > range)
                continue;

            if (!_tileIndex.TryGetValue(coord, out var ids) || ids == null)
                continue;

            for (int i = 0; i < ids.Count; i++)
            {
                int id = ids[i];
                if (id == attacker.id)
                    continue;

                if (!_groups.TryGetValue(id, out var other) || !other.isAlive || other.size <= 0)
                    continue;

                if (!IsConflictTargetForSpecies(attacker, other, allowOwnSpeciesConflict))
                    continue;

                if (!ShouldEngagePredatorTarget(attacker, other))
                    continue;

                float otherPower = GetGroupCombatPower(other, includeWeakness: true);
                float selfPower = GetGroupCombatPower(attacker, includeWeakness: true);

                float powerAdvantage = selfPower > 0f
                    ? Mathf.Clamp((selfPower - otherPower) / selfPower, -1f, 1f)
                    : 0f;

                bool sameSpecies = other.species == attackerDef;

                float score =
                    powerAdvantage * 2.0f +
                    (sameSpecies ? 1.5f : 0f) +
                    attackerDef.predatorTerritoriality * 1.5f -
                    dist * 0.35f +
                    (float)(_rng.NextDouble() * 0.05f);

                if (!found || score > bestScore)
                {
                    found = true;
                    bestScore = score;
                    targetId = id;
                    targetGroup = other;
                }
            }
        }

        return found;
    }

    private void BreakPredatorConflictOnFlee(
    ref AnimalGroupState fleeingGroup,
    int fleeingGroupId,
    ref AnimalGroupState otherGroup,
    int otherGroupId)
    {
        // Clear the pursuer / attacker side first.
        ClearPredatorConflictVisuals(ref otherGroup);

        // Clear the fleeing group's direct link back to the other side.
        if (fleeingGroup.predatorConflictTargetGroupId == otherGroupId)
        {
            fleeingGroup.isInPredatorConflict = false;
            fleeingGroup.predatorConflictTargetGroupId = -1;
        }

        // If the fleeing group was showing hunting for any reason, turn it off.
        fleeingGroup.isHunting = false;

        // If the fleeing group was still marked as targeted by this other group, clear it.
        if (fleeingGroup.isTargetedByPredator &&
            fleeingGroup.targetedByPredatorGroupId == otherGroupId)
        {
            fleeingGroup.isTargetedByPredator = false;
            fleeingGroup.targetedByPredatorGroupId = -1;
        }

        // Flee should win visually.
        fleeingGroup.lastAction = AnimalActionType.Flee;

        _groups[otherGroupId] = otherGroup;
        OnGroupUpdated?.Invoke(otherGroup);

        _groups[fleeingGroupId] = fleeingGroup;
        OnGroupUpdated?.Invoke(fleeingGroup);
    }

    public void SetTargetedByHumanUnit(int animalGroupId, bool targeted)
    {
        if (animalGroupId < 0)
            return;

        if (!_groups.TryGetValue(animalGroupId, out var group))
            return;

        group.isTargetedByHumanUnits = targeted;

        if (targeted)
        {
            ClearPlannedActionBecauseHumanTargeted(ref group);
            group.lastAction = AnimalActionType.Idle;
        }

        _groups[animalGroupId] = group;
        OnGroupUpdated?.Invoke(group);

        SetPlayerTargetedOnAnimal(animalGroupId, targeted);
    }

    private void ClearPlannedActionBecauseHumanTargeted(ref AnimalGroupState group)
    {
        // Clear hunting plan / chase
        if (group.isHunting || group.huntingTargetGroupId > 0)
            ClearHuntingTarget(ref group);

        group.huntingEscapeCount = 0;

        // Clear human raid / hunt intent
        group.isRaidingPlayerTile = false;
        group.raidTargetTile = group.tile;

        group.isHuntingHumanUnits = false;
        group.huntingHumanUnitGroupId = null;

        // Clear predator-conflict intent
        group.isInPredatorConflict = false;
        group.predatorConflictTargetGroupId = -1;

        // Clear active flee / threat-movement intent
        group.isFleeingFromThreat = false;
        group.fleeFromPredatorGroupId = -1;
        group.fleeUntilDistanceTiles = 0;
        group.fleeThreatLastKnownTile = group.tile;
        group.fleeStepsRemaining = 0;

        // Clear water-search path memory so it does not resume an old search plan later
        ClearWaterSearchMemory(ref group);
    }
}