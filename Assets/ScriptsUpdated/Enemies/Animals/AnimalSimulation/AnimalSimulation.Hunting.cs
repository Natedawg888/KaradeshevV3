using System;
using UnityEngine;

public partial class AnimalSimulation
{
    private bool IsPreferredPrey(AnimalDefinition predator, AnimalDefinition prey)
    {
        if (predator == null || prey == null)
            return false;

        var preferred = predator.preferredPrey;
        if (preferred == null || preferred.Length == 0)
            return false;

        return Array.IndexOf(preferred, prey) >= 0;
    }

    private float GetHuntGroupHealthFraction01(AnimalGroupState group)
    {
        group.EnsureHealthValid();

        int max = group.MaxHealth;
        if (max <= 0)
            return 1f;

        return Mathf.Clamp01(group.currentHealth / (float)max);
    }

    private bool ShouldEngagePreyTarget(AnimalGroupState predator, AnimalGroupState prey, float hungerPct)
    {
        var predatorDef = predator.species;
        var preyDef = prey.species;

        if (predatorDef == null || preyDef == null)
            return false;

        float predatorPower = GetGroupCombatPower(predator, includeWeakness: true);
        float preyPower = GetGroupCombatPower(prey, includeWeakness: true);

        if (predatorPower <= 0f)
            return false;

        bool preyBiggerStrongerAndMore =
            (int)preyDef.sizeCategory > (int)predatorDef.sizeCategory &&
            prey.Strength > predator.Strength &&
            prey.size > predator.size;

        float powerRatio = preyPower / predatorPower;
        float preyWeakness = GetGroupWeakness01(prey);

        if (!preyBiggerStrongerAndMore &&
            powerRatio <= predatorDef.maxPreyPowerAdvantageToHunt)
            return true;

        bool veryHungry =
            hungerPct >= Mathf.Clamp01(Mathf.Max(
                predatorDef.huntingHungerThreshold,
                predatorDef.riskyHuntHungerThreshold));

        bool preyWeakEnough =
            preyWeakness >= predatorDef.weaknessThresholdToChallengeStrongerTarget;

        bool riskyEnough =
            predator.Aggression >= predatorDef.riskyConflictAggressionThreshold &&
            powerRatio <= predatorDef.maxPreyPowerAdvantageToHunt + 0.25f;

        return veryHungry && (preyWeakEnough || riskyEnough);
    }

    private float GetHuntingInitiativeScore01(AnimalGroupState group)
    {
        var def = group.species;
        if (def == null)
            return 0.5f;

        float speedScore = Mathf.Clamp01(group.Speed) * SizeSpeedMult(def.sizeCategory);
        float senseScore = Mathf.Clamp01(group.Sense);
        float strengthScore = Mathf.Clamp01(group.Strength);
        float weaknessPenalty = GetGroupWeakness01(group);

        float score =
            speedScore * 0.60f +
            senseScore * 0.25f +
            strengthScore * 0.15f;

        score -= weaknessPenalty * 0.30f;

        return Mathf.Clamp01(score);
    }

    private bool RollHuntingInitiative(AnimalGroupState predator, AnimalGroupState prey)
    {
        float predatorScore = GetHuntingInitiativeScore01(predator);
        float preyScore = GetHuntingInitiativeScore01(prey);

        float ambushBonus = GetPredatorAmbushBonus01(predator, prey);

        float diff = (predatorScore + ambushBonus) - preyScore;
        float predatorChance = Mathf.Clamp01(0.5f + diff * 0.5f);

        return _rng.NextDouble() < predatorChance;
    }

    private bool ShouldPreyRetaliateAgainstPredator(AnimalGroupState prey, AnimalGroupState predator)
    {
        var preyDef = prey.species;
        var predatorDef = predator.species;

        if (preyDef == null || predatorDef == null)
            return false;

        if (prey.Aggression <= 0f)
            return false;

        float preyPower = GetGroupCombatPower(prey, includeWeakness: true);
        float predatorPower = GetGroupCombatPower(predator, includeWeakness: true);

        if (preyPower <= 0f)
            return false;

        bool sameOrLargerSize =
            (int)preyDef.sizeCategory >= (int)predatorDef.sizeCategory;

        bool closeStrength =
            prey.Strength >= predator.Strength - preyDef.preyRetaliationStrengthTolerance;

        bool closeDefense =
            prey.Defense >= predator.Defense - preyDef.preyRetaliationDefenseTolerance;

        bool strongNumbersAdvantage =
            prey.size >= Mathf.CeilToInt(predator.size * 1.5f);

        bool notBadlyOutmatched =
            preyPower >= predatorPower * 0.80f;

        bool wellArmoredAndCapable =
            sameOrLargerSize &&
            closeDefense &&
            preyPower >= predatorPower * 0.75f;

        bool desperateButCapable =
            GetHuntGroupHealthFraction01(prey) <= preyDef.preyLowHealthRetaliationThreshold &&
            closeDefense &&
            preyPower >= predatorPower * 0.65f;

        // Prey only retaliates if it is genuinely capable of standing and fighting.
        if ((sameOrLargerSize && closeStrength && closeDefense && notBadlyOutmatched) ||
            (strongNumbersAdvantage && closeDefense && notBadlyOutmatched) ||
            wellArmoredAndCapable ||
            desperateButCapable)
        {
            return true;
        }

        return false;
    }

    private float GetPreyEscapeAttemptChance01(AnimalGroupState prey, AnimalGroupState predator)
    {
        var preyDef = prey.species;
        var predatorDef = predator.species;

        if (preyDef == null || predatorDef == null)
            return 0.5f;

        float chance = Mathf.Clamp01(prey.Flightiness);

        bool smaller =
            (int)preyDef.sizeCategory < (int)predatorDef.sizeCategory ||
            prey.size < predator.size;

        bool weaker = prey.Strength < predator.Strength;
        float weakness = GetGroupWeakness01(prey);

        if (smaller && weaker)
            chance += 0.20f;

        chance += weakness * 0.25f;

        return Mathf.Clamp01(chance);
    }

    private int DealPreyRetaliationDamage(
    ref AnimalGroupState prey,
    ref AnimalGroupState predator)
    {
        int kills = CalculatePredatorConflictKills(prey, predator, 0.35f);
        if (kills <= 0)
            return 0;

        predator.size = Mathf.Max(0, predator.size - kills);

        predator.EnsureHealthValid();

        predator.currentHealth = Mathf.Clamp(
            predator.currentHealth - kills * Mathf.Max(1, predator.HealthPerAnimal),
            0,
            predator.MaxHealth);

        if (predator.size <= 0)
            predator.currentHealth = 0;
        else
            predator.EnsureHealthValid();

        return kills;
    }

    private bool TryHandlePreyEscape(
    ref AnimalGroupState predator,
    ref AnimalGroupState prey,
    int preyId)
    {
        float attempt = GetPreyEscapeAttemptChance01(prey, predator);

        if (!RollEscape(prey, predator, attempt))
            return false;

        TileCoord oldPreyTile = prey.tile;

        // Chance to leave 1-2 slower animals behind.
        TryLeaveEscapeStragglers(ref prey, oldPreyTile);

        prey.tile = StepAwayFrom(prey.tile, predator.tile);
        prey.lastAction = AnimalActionType.Flee;

        if (!oldPreyTile.Equals(prey.tile))
            MoveGroupInTileIndex(preyId, oldPreyTile, prey.tile);

        _groups[preyId] = prey;
        OnGroupUpdated?.Invoke(prey);

        predator.huntingEscapeCount++;
        predator.lastAction = AnimalActionType.AttackAnimal;

        var predatorDef = predator.species;
        if (predatorDef != null &&
            predator.huntingEscapeCount >= Mathf.Max(1, predatorDef.maxTargetEscapesBeforeGiveUp))
        {
            ClearHuntingTarget(ref predator);
        }

        return true;
    }

    private bool HandleCarnivoreHunting(ref AnimalGroupState predator, float hungerPct)
    {
        var species = predator.species;
        if (species == null)
            return false;

        if (predator.isFleeingFromThreat || predator.lastAction == AnimalActionType.Flee)
        {
            ClearHuntingTarget(ref predator);
            return false;
        }

        float thirstPct = species.maxThirst > 0f
            ? predator.thirst / species.maxThirst
            : 0f;

        if (thirstPct >= Mathf.Clamp01(species.abandonHuntForWaterNeedThreshold))
        {
            ClearHuntingTarget(ref predator);
            return false;
        }

        float huntingThreshold = (species.huntingHungerThreshold > 0f)
            ? species.huntingHungerThreshold
            : 0.15f;

        int range = Mathf.Max(1, species.huntingRangeTiles);
        int maxChaseDistance = range * 2;

        if (hungerPct < huntingThreshold)
            return false;

        // 1) Continue existing target if possible
        if (predator.isHunting && predator.huntingTargetGroupId > 0)
        {
            if (_groups.TryGetValue(predator.huntingTargetGroupId, out var prey) &&
                prey.isAlive && prey.size > 0)
            {
                int dist = Mathf.Abs(prey.tile.x - predator.tile.x) +
                           Mathf.Abs(prey.tile.y - predator.tile.y);

                if (dist > maxChaseDistance ||
                    predator.huntingEscapeCount >= Mathf.Max(1, species.maxTargetEscapesBeforeGiveUp) ||
                    !ShouldEngagePreyTarget(predator, prey, hungerPct))
                {
                    ClearHuntingTarget(ref predator);
                }
                else
                {
                    if (prey.tile.Equals(predator.tile))
                    {
                        CarnivoreEat(ref predator);
                        return true;
                    }

                    predator.tile = StepTowards(predator.tile, prey.tile, false);
                    predator.lastAction = AnimalActionType.Move;
                    return true;
                }
            }

            ClearHuntingTarget(ref predator);
        }

        // 2) Acquire a new target
        if (TryAcquireHuntingTarget(ref predator, hungerPct, out var newPrey))
        {
            if (newPrey.tile.Equals(predator.tile))
                CarnivoreEat(ref predator);
            else
            {
                predator.tile = StepTowards(predator.tile, newPrey.tile, false);
                predator.lastAction = AnimalActionType.Move;
            }

            return true;
        }

        return false;
    }

    private bool TryAcquireHuntingTarget(
        ref AnimalGroupState predator,
        float hungerPct,
        out AnimalGroupState preyGroup)
    {
        preyGroup = default;

        var predatorDef = predator.species;
        if (predatorDef == null)
            return false;

        int range = Mathf.Max(1, predatorDef.huntingRangeTiles);
        TileCoord origin = predator.tile;

        bool found = false;
        float bestScore = float.NegativeInfinity;
        int bestPreyId = -1;
        AnimalGroupState bestPrey = default;

        var neighbours = GetNeighbourTilesCached(origin, range);
        for (int n = 0; n < neighbours.Count; n++)
        {
            var coord = neighbours[n];

            int dist = Mathf.Abs(coord.x - origin.x) + Mathf.Abs(coord.y - origin.y);
            if (dist == 0 || dist > range)
                continue;

            if (!_tileIndex.TryGetValue(coord, out var ids) || ids == null || ids.Count == 0)
                continue;

            for (int i = 0; i < ids.Count; i++)
            {
                int id = ids[i];
                if (id == predator.id)
                    continue;

                if (!_groups.TryGetValue(id, out var candidate))
                    continue;

                if (!candidate.isAlive || candidate.size <= 0)
                    continue;

                var preyDef = candidate.species;
                if (preyDef == null)
                    continue;

                if (!IsPreferredPrey(predatorDef, preyDef))
                    continue;

                if (!ShouldEngagePreyTarget(predator, candidate, hungerPct))
                    continue;

                float preyWeakness = GetGroupWeakness01(candidate);
                float predatorPower = GetGroupCombatPower(predator, includeWeakness: true);
                float preyPower = GetGroupCombatPower(candidate, includeWeakness: true);

                float distanceScore = 1f / Mathf.Max(1, dist);
                float weaknessScore = preyWeakness * 1.5f;
                float powerScore = predatorPower > 0f
                    ? Mathf.Clamp((predatorPower - preyPower) / predatorPower, -1f, 1f)
                    : 0f;

                float score = distanceScore * 1.25f + weaknessScore + powerScore;

                if (!found || score > bestScore)
                {
                    found = true;
                    bestScore = score;
                    bestPreyId = id;
                    bestPrey = candidate;
                }
            }
        }

        if (!found)
            return false;

        if (predator.isHunting &&
            predator.huntingTargetGroupId > 0 &&
            predator.huntingTargetGroupId != bestPreyId)
        {
            ClearHuntingTarget(ref predator);
        }

        predator.isHunting = true;
        predator.huntingTargetGroupId = bestPreyId;
        predator.huntingEscapeCount = 0;

        bestPrey.isTargetedByPredator = true;
        bestPrey.targetedByPredatorGroupId = predator.id;
        _groups[bestPrey.id] = bestPrey;
        OnGroupUpdated?.Invoke(bestPrey);

        preyGroup = bestPrey;
        return true;
    }

    private void CarnivoreEat(ref AnimalGroupState predator)
    {
        var predatorSpecies = predator.species;
        if (predatorSpecies == null)
            return;

        var tile = predator.tile;
        float hungerPct = predatorSpecies.maxHunger > 0f
            ? predator.hunger / predatorSpecies.maxHunger
            : 0f;

        if (!_tileIndex.TryGetValue(tile, out var ids) || ids == null || ids.Count == 0)
        {
            predator.lastAction = AnimalActionType.Idle;
            ClearHuntingTarget(ref predator);
            return;
        }

        int chosenPreyId = -1;
        AnimalGroupState chosenPrey = default;
        bool hasChosenPrey = false;

        // Prefer explicit target if still valid and on this tile
        if (predator.isHunting &&
            predator.huntingTargetGroupId > 0 &&
            _groups.TryGetValue(predator.huntingTargetGroupId, out var targeted) &&
            targeted.isAlive &&
            targeted.size > 0 &&
            targeted.tile.Equals(tile) &&
            ShouldEngagePreyTarget(predator, targeted, hungerPct))
        {
            chosenPreyId = targeted.id;
            chosenPrey = targeted;
            hasChosenPrey = true;
        }
        else
        {
            bool found = false;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < ids.Count; i++)
            {
                int id = ids[i];
                if (id == predator.id)
                    continue;

                if (!_groups.TryGetValue(id, out var prey))
                    continue;

                if (!prey.isAlive || prey.size <= 0)
                    continue;

                if (!IsPreferredPrey(predatorSpecies, prey.species))
                    continue;

                if (!ShouldEngagePreyTarget(predator, prey, hungerPct))
                    continue;

                float preyWeakness = GetGroupWeakness01(prey);
                float predatorPower = GetGroupCombatPower(predator, includeWeakness: true);
                float preyPower = GetGroupCombatPower(prey, includeWeakness: true);

                float powerScore = predatorPower > 0f
                    ? Mathf.Clamp((predatorPower - preyPower) / predatorPower, -1f, 1f)
                    : 0f;

                float score = preyWeakness * 1.5f + powerScore;

                if (!found || score > bestScore)
                {
                    found = true;
                    bestScore = score;
                    chosenPreyId = id;
                    chosenPrey = prey;
                    hasChosenPrey = true;
                }
            }
        }

        if (!hasChosenPrey || chosenPreyId == -1)
        {
            predator.lastAction = AnimalActionType.Idle;
            ClearHuntingTarget(ref predator);
            return;
        }

        bool predatorActsFirst = RollHuntingInitiative(predator, chosenPrey);

        // -------------------------------------------------
        // Prey wins initiative: retaliate first or escape first
        // -------------------------------------------------
        if (!predatorActsFirst)
        {
            if (ShouldPreyRetaliateAgainstPredator(chosenPrey, predator))
            {
                bool preyHit = RollCombatHit(chosenPrey, predator, false);

                chosenPrey.lastAction = AnimalActionType.DefendAnimal;

                if (preyHit)
                {
                    int retaliationDamage = DealPreyRetaliationDamage(ref chosenPrey, ref predator);

                    if (!predator.isAlive)
                    {
                        LogAnimalVsAnimal(
                            "KILL-PREY-DEFENSE",
                            chosenPrey,
                            predator,
                            $"Prey killed predator during hunt retaliation. retaliationDamage={retaliationDamage}");

                        predator.currentHealth = 0;
                        predator.size = 0;
                        predator.lastAction = AnimalActionType.Flee;

                        ClearHuntingTarget(ref predator);

                        _groups[chosenPreyId] = chosenPrey;
                        OnGroupUpdated?.Invoke(chosenPrey);
                        return;
                    }
                }
                else
                {
                    LogAnimalVsAnimal(
                        "MISS-HUNT",
                        chosenPrey,
                        predator,
                        "Prey won initiative and retaliated first during hunt, but missed.");
                }
            }
            else
            {
                if (TryHandlePreyEscape(ref predator, ref chosenPrey, chosenPreyId))
                    return;
            }
        }

        // -------------------------------------------------
        // Predator attacks
        // -------------------------------------------------
        bool predatorHit = RollCombatHit(predator, chosenPrey, true);
        predator.lastAction = AnimalActionType.AttackAnimal;

        if (predatorHit)
        {
            DealPredatorCombatDamage(ref predator, ref chosenPrey, 0.45f);

            if (!chosenPrey.isAlive)
            {
                LogAnimalVsAnimal(
                    "KILL-HUNT",
                    predator,
                    chosenPrey,
                    $"Predator killed prey during hunt on tile {tile}");

                chosenPrey.currentHealth = 0;
                chosenPrey.size = 0;
                RemoveGroup(chosenPreyId, tile);

                ApplySuccessfulHuntNeedRestore(ref predator);
                predator.lastAction = AnimalActionType.Eat;
                ClearHuntingTarget(ref predator);
                return;
            }
        }
        else
        {
            LogAnimalVsAnimal(
                "MISS-HUNT",
                predator,
                chosenPrey,
                "Predator attacked during hunt but missed.");
        }

        // -------------------------------------------------
        // Predator went first: prey still gets a reaction if alive,
        // especially important on a miss.
        // -------------------------------------------------
        if (predatorActsFirst)
        {
            if (ShouldPreyRetaliateAgainstPredator(chosenPrey, predator))
            {
                bool preyHit = RollCombatHit(chosenPrey, predator, false);
                chosenPrey.lastAction = AnimalActionType.DefendAnimal;

                if (preyHit)
                {
                    DealPreyRetaliationDamage(ref chosenPrey, ref predator);
                }
                else
                {
                    LogAnimalVsAnimal(
                        "MISS-HUNT",
                        chosenPrey,
                        predator,
                        "Prey reacted after predator's opening attack, but missed retaliation.");
                }
            }
            else
            {
                if (TryHandlePreyEscape(ref predator, ref chosenPrey, chosenPreyId))
                    return;
            }
        }

        _groups[chosenPreyId] = chosenPrey;
        OnGroupUpdated?.Invoke(chosenPrey);

        if (!predator.isAlive)
        {
            predator.currentHealth = 0;
            predator.size = 0;
            predator.lastAction = AnimalActionType.Flee;
            ClearHuntingTarget(ref predator);
            return;
        }

        // Predator only eats if it actually secured the prey.
        if (!predatorHit)
            predator.lastAction = AnimalActionType.AttackAnimal;

        ClearHuntingTarget(ref predator);
    }

    private void ClearHuntingTarget(ref AnimalGroupState predator)
    {
        if (predator.huntingTargetGroupId > 0 &&
            _groups.TryGetValue(predator.huntingTargetGroupId, out var prey))
        {
            if (prey.targetedByPredatorGroupId == predator.id)
            {
                prey.isTargetedByPredator = false;
                prey.targetedByPredatorGroupId = -1;

                if (prey.fleeFromPredatorGroupId == predator.id)
                    ClearThreatResponse(ref prey, clearTargetedFlag: false);

                if (prey.lastAction == AnimalActionType.DefendAnimal &&
                    !prey.isInPredatorConflict &&
                    !prey.isFleeingFromThreat)
                {
                    prey.lastAction = AnimalActionType.Idle;
                }

                _groups[prey.id] = prey;
                OnGroupUpdated?.Invoke(prey);
            }
        }

        predator.isHunting = false;
        predator.huntingTargetGroupId = -1;
        predator.huntingEscapeCount = 0;

        if (predator.lastAction == AnimalActionType.AttackAnimal)
            predator.lastAction = AnimalActionType.Idle;
    }

    private void ApplySuccessfulHuntNeedRestore(ref AnimalGroupState predator)
    {
        var species = predator.species;
        if (species == null)
            return;

        float hungerSatisfied =
            species.maxHunger * Mathf.Clamp01(species.hungerSatisfiedOnSuccessfulHunt);

        float thirstSatisfied =
            species.maxThirst * Mathf.Clamp01(species.thirstSatisfiedOnSuccessfulHunt);

        predator.hunger = Mathf.Clamp(predator.hunger - hungerSatisfied, 0f, species.maxHunger);
        predator.thirst = Mathf.Clamp(predator.thirst - thirstSatisfied, 0f, species.maxThirst);
    }

    private AnimalGroupState CreateCopiedGroupState(AnimalGroupState source)
    {
        if (source == null)
            return null;

        return new AnimalGroupState
        {
            id = source.id,
            species = source.species,

            size = source.size,
            ageInTurns = source.ageInTurns,

            currentHealth = source.currentHealth,

            hunger = source.hunger,
            thirst = source.thirst,

            tile = source.tile,

            lastAction = source.lastAction,
            nextUpdateTurn = source.nextUpdateTurn,

            isLeader = source.isLeader,
            herdId = source.herdId,
            leaderGroupId = source.leaderGroupId,

            // Resolved per-group core stats
            resolvedHealthPerAnimal = source.resolvedHealthPerAnimal,
            resolvedAggression = source.resolvedAggression,
            resolvedFlightiness = source.resolvedFlightiness,
            resolvedHerding = source.resolvedHerding,
            resolvedStrength = source.resolvedStrength,
            resolvedDefense = source.resolvedDefense,
            resolvedSpeed = source.resolvedSpeed,
            resolvedSense = source.resolvedSense,
            resolvedStealth = source.resolvedStealth,

            resolvedBreedingFemaleFraction = source.resolvedBreedingFemaleFraction,

            // Hunting / prey targeting
            isHunting = source.isHunting,
            huntingTargetGroupId = source.huntingTargetGroupId,
            isTargetedByPredator = source.isTargetedByPredator,
            huntingEscapeCount = source.huntingEscapeCount,

            // Reproduction
            nextReproductionTurn = source.nextReproductionTurn,
            isOnReproductionCooldown = source.isOnReproductionCooldown,

            // Predator conflict
            isInPredatorConflict = source.isInPredatorConflict,
            predatorConflictTargetGroupId = source.predatorConflictTargetGroupId,

            targetedByPredatorGroupId = source.targetedByPredatorGroupId,

            // Flee / threat memory
            isFleeingFromThreat = source.isFleeingFromThreat,
            fleeFromPredatorGroupId = source.fleeFromPredatorGroupId,
            fleeUntilDistanceTiles = source.fleeUntilDistanceTiles,
            fleeThreatLastKnownTile = source.fleeThreatLastKnownTile,
            fleeStepsRemaining = source.fleeStepsRemaining,

            // Water search memory
            hasWaterSearchMemory = source.hasWaterSearchMemory,
            lastWaterSearchPreviousTile = source.lastWaterSearchPreviousTile,
            secondLastWaterSearchPreviousTile = source.secondLastWaterSearchPreviousTile,
            waterSearchBacktrackAvoidanceTurns = source.waterSearchBacktrackAvoidanceTurns,

            // Human interaction
            isRaidingPlayerTile = source.isRaidingPlayerTile,
            raidTargetTile = source.raidTargetTile,

            isHuntingHumanUnits = source.isHuntingHumanUnits,
            huntingHumanUnitGroupId = source.huntingHumanUnitGroupId
        };
    }
}