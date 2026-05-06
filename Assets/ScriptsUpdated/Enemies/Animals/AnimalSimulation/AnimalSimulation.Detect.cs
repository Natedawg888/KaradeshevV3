using System;
using UnityEngine;

public partial class AnimalSimulation
{
    private bool TryHandlePredatorDetection(ref AnimalGroupState group)
    {
        var species = group.species;
        if (species == null)
            return false;

        // Continue an already-started flee response first.
        if (TryContinueThreatResponse(ref group))
            return true;

        // Proactive nearby predator/prey sensing (1-2 tiles).
        if (TryHandleNearbyPredatorPreyDetection(ref group))
            return true;

        // Existing targeted-predator response.
        if (!group.isTargetedByPredator || group.targetedByPredatorGroupId <= 0)
            return false;

        if (!_groups.TryGetValue(group.targetedByPredatorGroupId, out var predator) ||
            !predator.isAlive || predator.size <= 0)
        {
            ClearThreatResponse(ref group, clearTargetedFlag: true);
            return false;
        }

        group.fleeThreatLastKnownTile = predator.tile;

        if (!RollPredatorDetection(group, predator))
            return false;

        return ReactToDetectedPredator(ref group, predator);
    }

    private bool TryHandleNearbyPredatorPreyDetection(ref AnimalGroupState group)
    {
        var species = group.species;
        if (species == null || _env == null)
            return false;

        float hungerPct = species.maxHunger > 0f ? group.hunger / species.maxHunger : 0f;
        float thirstPct = species.maxThirst > 0f ? group.thirst / species.maxThirst : 0f;

        bool groupIsPredatorLike =
            species.diet == AnimalDiet.Carnivore || species.diet == AnimalDiet.Omnivore;

        bool foundThreat = false;
        AnimalGroupState bestThreat = default;
        float bestThreatScore = float.NegativeInfinity;

        bool foundPrey = false;
        AnimalGroupState bestPrey = default;
        float bestPreyScore = float.NegativeInfinity;

        var nearbyTiles = GetNeighbourTilesCached(group.tile, 2);
        for (int n = 0; n < nearbyTiles.Count; n++)
        {
            TileCoord coord = nearbyTiles[n];

            if (coord == group.tile)
                continue;

            if (!_tileIndex.TryGetValue(coord, out var ids) || ids == null || ids.Count == 0)
                continue;

            int distance = Manhattan(group.tile, coord);
            if (distance <= 0 || distance > 2)
                continue;

            for (int i = 0; i < ids.Count; i++)
            {
                int otherId = ids[i];
                if (otherId == group.id)
                    continue;

                if (!_groups.TryGetValue(otherId, out var other))
                    continue;

                if (!other.isAlive || other.size <= 0 || other.species == null)
                    continue;

                // Threat detection: the other species sees this one as preferred prey.
                if (IsSpeciesOnPreferredPreyList(other.species, species) &&
                    RollGeneralDetection(group, other, distance))
                {
                    float threatScore =
                        GetGroupCombatPower(other, includeWeakness: true) +
                        other.size * 0.35f +
                        other.species.aggression * 1.5f -
                        distance * 0.25f;

                    if (!foundThreat || threatScore > bestThreatScore)
                    {
                        foundThreat = true;
                        bestThreat = other;
                        bestThreatScore = threatScore;
                    }
                }

                // Prey opportunity: this group sees the other as preferred prey.
                if (groupIsPredatorLike &&
                    IsSpeciesOnPreferredPreyList(species, other.species) &&
                    RollGeneralDetection(group, other, distance) &&
                    ShouldEngageNearbyDetectedPrey(group, other, hungerPct, thirstPct))
                {
                    float preyWeakness = GetGroupWeakness01(other);
                    float predatorPower = GetGroupCombatPower(group, includeWeakness: true);
                    float preyPower = GetGroupCombatPower(other, includeWeakness: true);

                    float safetyScore = predatorPower > 0f
                        ? Mathf.Clamp((predatorPower - preyPower) / predatorPower, -1f, 1f)
                        : 0f;

                    float preyScore =
                        other.size * Mathf.Lerp(0.75f, 1.50f, preyWeakness) *
                        Mathf.Lerp(0.35f, 1.25f, Mathf.Clamp01(0.5f + safetyScore * 0.5f))
                        - distance * 0.25f;

                    if (!foundPrey || preyScore > bestPreyScore)
                    {
                        foundPrey = true;
                        bestPrey = other;
                        bestPreyScore = preyScore;
                    }
                }
            }
        }

        // Threat takes priority over pursuit.
        if (foundThreat)
            return ReactToDetectedPredator(ref group, bestThreat);

        if (foundPrey)
            return ReactToDetectedPrey(ref group, ref bestPrey);

        return false;
    }

    private bool ReactToDetectedPredator(ref AnimalGroupState group, AnimalGroupState predator)
    {
        group.isTargetedByPredator = true;
        group.targetedByPredatorGroupId = predator.id;
        group.fleeThreatLastKnownTile = predator.tile;

        bool advanceOnThreat = ShouldTargetedGroupAdvanceOnThreat(group, predator);

        if (advanceOnThreat)
        {
            ClearFleeState(ref group);

            if (group.tile.Equals(predator.tile))
                return false;

            group.tile = StepTowards(group.tile, predator.tile, false);
            group.lastAction = AnimalActionType.Move;

            LogAnimalEvent(
                "DETECT-THREAT",
                group,
                $"Detected nearby predator {predator.species.name} from {predator.tile} and advanced to confront it.");

            return true;
        }

        int desiredDistance = GetDesiredFleeDistanceTiles(group);
        StartFleeFromThreat(ref group, predator, desiredDistance);
        TryTriggerNearbyHerdFlee(group, predator, desiredDistance);

        LogAnimalEvent(
            "DETECT-THREAT",
            group,
            $"Detected nearby predator {predator.species.name} from {predator.tile} and started fleeing.");

        return TryContinueThreatResponse(ref group);
    }

    private bool ReactToDetectedPrey(ref AnimalGroupState predator, ref AnimalGroupState prey)
    {
        predator.isHunting = true;
        predator.huntingTargetGroupId = prey.id;
        predator.huntingEscapeCount = 0;

        prey.isTargetedByPredator = true;
        prey.targetedByPredatorGroupId = predator.id;
        prey.fleeThreatLastKnownTile = predator.tile;

        _groups[prey.id] = prey;
        OnGroupUpdated?.Invoke(prey);

        if (predator.tile.Equals(prey.tile))
            return false;

        TileCoord next = StepTowards(predator.tile, prey.tile, false);
        if (next.Equals(predator.tile))
            return false;

        TileCoord from = predator.tile;
        predator.tile = next;
        predator.lastAction = AnimalActionType.Move;

        LogAnimalEvent(
            "DETECT-PREY",
            predator,
            $"Detected preferred prey {prey.species.name} at {prey.tile} and moved {from} -> {next} to engage.");

        return true;
    }

    private float GetAnimalDetectionChance01(AnimalGroupState observer, AnimalGroupState observed, int tileDistance)
    {
        var observerDef = observer.species;
        var observedDef = observed.species;

        if (observerDef == null || observedDef == null)
            return 0.5f;

        float sense = Mathf.Clamp01(observer.Sense);
        float stealth = Mathf.Clamp01(observed.Stealth);

        float sizeBonus = observedDef.sizeCategory switch
        {
            AnimalSizeCategory.Small => -0.05f,
            AnimalSizeCategory.Medium => 0f,
            AnimalSizeCategory.Large => 0.05f,
            AnimalSizeCategory.Giant => 0.10f,
            _ => 0f
        };

        float observerPenalty = GetGroupWeakness01(observer) * 0.15f;
        float observedPenalty = GetGroupWeakness01(observed) * 0.10f;

        float distancePenalty = tileDistance switch
        {
            1 => 0f,
            2 => 0.30f,
            _ => 0.45f
        };

        float diff = (sense + sizeBonus + observedPenalty) - (stealth + observerPenalty + distancePenalty);
        return Mathf.Clamp01(0.5f + diff * 0.5f);
    }

    private float GetPredatorDetectionChance01(AnimalGroupState target, AnimalGroupState predator)
    {
        int distance = Mathf.Max(1, Manhattan(target.tile, predator.tile));
        return GetAnimalDetectionChance01(target, predator, distance);
    }

    private bool RollPredatorDetection(AnimalGroupState target, AnimalGroupState predator)
    {
        float chance = GetPredatorDetectionChance01(target, predator);
        return _rng.NextDouble() < chance;
    }

    private bool RollGeneralDetection(AnimalGroupState observer, AnimalGroupState observed, int tileDistance)
    {
        float chance = GetAnimalDetectionChance01(observer, observed, tileDistance);
        return _rng.NextDouble() < chance;
    }

    private bool IsSpeciesOnPreferredPreyList(AnimalDefinition predator, AnimalDefinition prey)
    {
        if (predator == null || prey == null || predator.preferredPrey == null || predator.preferredPrey.Length == 0)
            return false;

        for (int i = 0; i < predator.preferredPrey.Length; i++)
        {
            if (predator.preferredPrey[i] == prey)
                return true;
        }

        return false;
    }

    private bool ShouldEngageNearbyDetectedPrey(
        AnimalGroupState predator,
        AnimalGroupState prey,
        float hungerPct,
        float thirstPct)
    {
        var predatorDef = predator.species;
        if (predatorDef == null || prey.species == null)
            return false;

        bool predatorLike =
            predatorDef.diet == AnimalDiet.Carnivore || predatorDef.diet == AnimalDiet.Omnivore;

        if (!predatorLike)
            return false;

        if (!IsSpeciesOnPreferredPreyList(predatorDef, prey.species))
            return false;

        // If badly thirsty, do not abandon water needs for a chase.
        if (thirstPct > hungerPct && thirstPct >= 0.75f)
            return false;

        if (ShouldEngagePreyTarget(predator, prey, hungerPct))
            return true;

        float predatorPower = GetGroupCombatPower(predator, includeWeakness: true);
        float preyPower = GetGroupCombatPower(prey, includeWeakness: true);

        if (predator.Aggression >= 0.70f &&
            predatorPower >= preyPower * 0.85f)
        {
            return true;
        }

        return false;
    }

    private bool TryContinueThreatResponse(ref AnimalGroupState group)
    {
        if (!group.isFleeingFromThreat || group.fleeFromPredatorGroupId <= 0)
            return false;

        bool hasThreat =
            _groups.TryGetValue(group.fleeFromPredatorGroupId, out var threat) &&
            threat.isAlive &&
            threat.size > 0;

        TileCoord threatTile = hasThreat ? threat.tile : group.fleeThreatLastKnownTile;

        if (hasThreat)
            group.fleeThreatLastKnownTile = threat.tile;

        if (group.fleeStepsRemaining <= 0)
        {
            ClearFleeState(ref group);
            return false;
        }

        if (hasThreat && ShouldTargetedGroupAdvanceOnThreat(group, threat))
        {
            ClearFleeState(ref group);

            if (group.tile.Equals(threat.tile))
                return false;

            group.tile = StepTowards(group.tile, threat.tile, false);
            group.lastAction = AnimalActionType.Move;
            return true;
        }

        if (!TryGetBestFleeStep(group.species, group.tile, threatTile, out TileCoord next))
        {
            // If blocked, stop trying rather than getting stuck forever.
            ClearFleeState(ref group);
            return false;
        }

        if (next.Equals(group.tile))
        {
            ClearFleeState(ref group);
            return false;
        }

        group.tile = next;
        group.fleeStepsRemaining--;
        group.lastAction = AnimalActionType.Flee;

        if (group.fleeStepsRemaining <= 0)
            ClearFleeState(ref group);

        return true;
    }

    private bool TryGetBestFleeStep(
    AnimalDefinition species,
    TileCoord origin,
    TileCoord threatTile,
    out TileCoord bestStep)
    {
        bestStep = origin;

        if (_env == null)
            return false;

        var neighbours = GetNeighbourTilesCached(origin, 1);

        bool found = false;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < neighbours.Count; i++)
        {
            TileCoord coord = neighbours[i];

            if (coord.Equals(origin))
                continue;

            TileEnvironmentData data = _env.GetTileData(coord);

            if (species != null && IsAvoidedHabitat(species, data))
                continue;

            if (species != null && ShouldAvoidHumans(species) && IsPlayerBuildingTile(coord))
                continue;

            int distFromThreat = Manhattan(coord, threatTile);
            int currentDist = Manhattan(origin, threatTile);

            if (distFromThreat < currentDist)
                continue;

            float habitatScore = species != null ? GetHabitatSuitability(species, data) : 0f;

            float score =
                distFromThreat * 3.0f +
                habitatScore * 0.75f -
                data.dangerLevel * 0.75f +
                (float)(_rng.NextDouble() * 0.05f);

            if (!found || score > bestScore)
            {
                found = true;
                bestScore = score;
                bestStep = coord;
            }
        }

        return found;
    }

    private bool ShouldTargetedGroupAdvanceOnThreat(AnimalGroupState target, AnimalGroupState predator)
    {
        var targetDef = target.species;
        var predatorDef = predator.species;

        if (targetDef == null || predatorDef == null)
            return false;

        bool targetIsPredatorLike =
            targetDef.diet == AnimalDiet.Carnivore || targetDef.diet == AnimalDiet.Omnivore;

        float targetPower = GetGroupCombatPower(target, includeWeakness: true);
        float predatorPower = GetGroupCombatPower(predator, includeWeakness: true);

        if (targetIsPredatorLike)
        {
            if (targetPower >= predatorPower * 0.90f)
                return true;

            if (target.Aggression >= 0.70f &&
                GetGroupWeakness01(predator) >= 0.45f)
                return true;
        }

        return ShouldPreyRetaliateAgainstPredator(target, predator);
    }

    private int GetDesiredFleeDistanceTiles(AnimalGroupState group)
    {
        var def = group.species;
        if (def == null)
            return 2;

        int baseDistance = Mathf.Max(1, def.fleeDistanceTiles);

        if (GetGroupWeakness01(group) >= 0.65f)
            baseDistance += 1;

        return baseDistance;
    }

    private void StartFleeFromThreat(
    ref AnimalGroupState group,
    AnimalGroupState predator,
    int desiredDistance)
    {
        group.isFleeingFromThreat = true;
        group.fleeFromPredatorGroupId = predator.id;

        // Keep this if you still want it visible/debuggable,
        // but the actual movement will now use fleeStepsRemaining.
        group.fleeUntilDistanceTiles = Mathf.Max(1, desiredDistance);

        group.fleeStepsRemaining = Mathf.Max(1, desiredDistance);
        group.fleeThreatLastKnownTile = predator.tile;
    }

    private void ClearFleeState(ref AnimalGroupState group)
    {
        group.isFleeingFromThreat = false;
        group.fleeFromPredatorGroupId = -1;
        group.fleeUntilDistanceTiles = 0;
        group.fleeStepsRemaining = 0;
    }

    private void ClearThreatResponse(ref AnimalGroupState group, bool clearTargetedFlag)
    {
        if (clearTargetedFlag)
        {
            group.isTargetedByPredator = false;
            group.targetedByPredatorGroupId = -1;
        }

        ClearFleeState(ref group);
    }

    private void TryTriggerNearbyHerdFlee(
        AnimalGroupState source,
        AnimalGroupState predator,
        int desiredDistance)
    {
        var sourceDef = source.species;
        if (sourceDef == null)
            return;

        if (source.Herding < sourceDef.herdFleeTriggerThreshold)
            return;

        int range = Mathf.Max(1, sourceDef.herdFleeSignalRangeTiles);
        var nearby = GetNeighbourTilesCached(source.tile, range);

        for (int n = 0; n < nearby.Count; n++)
        {
            var coord = nearby[n];

            if (!_tileIndex.TryGetValue(coord, out var ids) || ids == null || ids.Count == 0)
                continue;

            for (int i = 0; i < ids.Count; i++)
            {
                int otherId = ids[i];
                if (otherId == source.id || otherId == predator.id)
                    continue;

                if (!_groups.TryGetValue(otherId, out var other))
                    continue;

                if (!other.isAlive || other.size <= 0 || other.species == null)
                    continue;

                bool otherIsPredatorLike =
                    other.species.diet == AnimalDiet.Carnivore || other.species.diet == AnimalDiet.Omnivore;

                if (otherIsPredatorLike)
                    continue;

                if (ShouldSuppressHerdingMovement(other))
                    continue;

                bool sociallyLinked =
                    other.species == sourceDef ||
                    IsLikedAnimal(sourceDef, other.species) ||
                    IsLikedAnimal(other.species, sourceDef);

                if (!sociallyLinked)
                    continue;

                other.isFleeingFromThreat = true;
                other.fleeFromPredatorGroupId = predator.id;
                other.fleeUntilDistanceTiles = Mathf.Max(other.fleeUntilDistanceTiles, desiredDistance);
                other.fleeThreatLastKnownTile = predator.tile;

                _groups[other.id] = other;
                OnGroupUpdated?.Invoke(other);
            }
        }
    }
}