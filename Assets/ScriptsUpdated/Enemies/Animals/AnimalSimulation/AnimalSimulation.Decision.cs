using System;
using System.Collections.Generic;
using UnityEngine;

public partial class AnimalSimulation
{
    private void DecideActionAndExecute(ref AnimalGroupState group, TileEnvironmentData currentTileEnv)
    {
        var species = group.species;
        if (species == null)
            return;

        if (TryBlockActionsWhileTargetedByHumanUnit(ref group))
            return;

        float hungerPct = species.maxHunger > 0f ? group.hunger / species.maxHunger : 0f;
        float thirstPct = species.maxThirst > 0f ? group.thirst / species.maxThirst : 0f;

        bool isCarnivoreLike = species.diet == AnimalDiet.Carnivore || species.diet == AnimalDiet.Omnivore;

        const float NEED_ACTION_THRESHOLD = 0.15f;


        if (TryHandleUrgentWaterNeed(ref group, currentTileEnv, hungerPct, thirstPct))
            return;

        if (TryHandleUrgentMateSeeking(ref group, hungerPct, thirstPct))
            return;

        if (TryHandlePredatorDetection(ref group))
            return;

        if (hungerPct >= NEED_ACTION_THRESHOLD)
        {
            if (isCarnivoreLike && HandleCarnivoreHunting(ref group, hungerPct))
                return;

            if (species.huntsHumans && HandleHumanRaiding(ref group, hungerPct, thirstPct))
                return;

            if (species.raidsStorageForFood && HandleStorageRaiding(ref group, hungerPct))
                return;

            if (TryEatHere(ref group, currentTileEnv))
                return;

            if (TryFindFoodTileNear(species, group.tile, out var foodTile))
            {
                TileCoord from = group.tile;
                group.tile = foodTile;
                group.lastAction = AnimalActionType.Move;

                LogAnimalEvent(
                    "MOVE-NEED",
                    group,
                    $"Moved {from} -> {foodTile} because HUNGER. hungerPct={hungerPct:F2}, thirstPct={thirstPct:F2}");

                return;
            }
        }

        if (thirstPct >= NEED_ACTION_THRESHOLD && thirstPct >= hungerPct)
        {
            if (TryDrinkHere(ref group, currentTileEnv))
                return;

            if (TryFindStepTowardWater(species, group.tile, out var nextStep, out var targetWaterTile))
            {
                TileCoord from = group.tile;
                group.tile = nextStep;
                group.lastAction = AnimalActionType.Move;

                LogAnimalEvent(
                    "MOVE-NEED",
                    group,
                    $"Moved {from} -> {nextStep} because THIRST, steering toward water at {targetWaterTile}. " +
                    $"hungerPct={hungerPct:F2}, thirstPct={thirstPct:F2}");

                return;
            }

            // New fallback: keep exploring tile-to-tile instead of idling.
            if (TryFindWaterSearchStep(ref group, thirstPct, out var searchStep))
            {
                TileCoord from = group.tile;
                group.tile = searchStep;
                group.lastAction = AnimalActionType.Move;
                RegisterWaterSearchStep(ref group, from);

                LogAnimalEvent(
                    "MOVE-WATER-SEARCH",
                    group,
                    $"Could not find a reachable drink tile right now, so moved {from} -> {searchStep} to continue searching. " +
                    $"hungerPct={hungerPct:F2}, thirstPct={thirstPct:F2}");

                return;
            }

            if (_env is MonoEnvironmentDataSource monoEnv)
            {
                monoEnv.DebugDumpCoord(group.tile, 2);
                monoEnv.DebugValidateRegistry();
            }

            LogAnimalEvent(
                "WATER-NOT-FOUND",
                group,
                $"Could not find reachable drink tile or a valid search step. hungerPct={hungerPct:F2}, thirstPct={thirstPct:F2}");
        }

        bool needsOk = hungerPct < 0.5f && thirstPct < 0.5f;
        if (needsOk && species.huntsHumans && group.Aggression >= 0.7f)
        {
            if (HandleHumanRaiding(ref group, hungerPct, thirstPct))
                return;
        }

        if (TryHandlePredatorConflict(ref group, hungerPct, thirstPct))
            return;

        if (needsOk &&
            species.useTemperaturePreference &&
            ClimateManager.Instance != null &&
            ClimateManager.Instance.HasValidInitialClimate)
        {
            if (IsTemperatureUncomfortable(species, group.tile, out float tempHere, out float distanceOutsideRange))
            {
                if (TryFindComfortableTemperatureTile(species, group.tile, tempHere, out var betterTile))
                {
                    if (betterTile != group.tile)
                    {
                        TileCoord from = group.tile;
                        group.tile = betterTile;
                        group.lastAction = AnimalActionType.Move;

                        LogAnimalEvent(
                            "MOVE-TEMP",
                            group,
                            $"Moved {from} -> {betterTile} for temperature. " +
                            $"tileTemp={tempHere:F1}C preferred=[{species.minPreferredTemperatureC:F1},{species.maxPreferredTemperatureC:F1}] " +
                            $"outsideBy={distanceOutsideRange:F1}C");

                        return;
                    }
                }
            }
        }

        if (TryHandleHerdingMovement(ref group))
            return;

        if (TryHandleMatingSeasonSpeciesSeeking(ref group))
            return;

        if (TryHandleOvercrowdingMovement(ref group))
            return;

        Wander(ref group);
    }

    private bool TryHandleUrgentMateSeeking(
    ref AnimalGroupState group,
    float hungerPct,
    float thirstPct)
    {
        var species = group.species;
        if (species == null || _env == null)
            return false;

        if (!species.urgentMateSeekingInMatingSeason)
            return false;

        if (!IsInMatingSeason(species))
            return false;

        float maxNeed = Mathf.Clamp01(species.urgentMateSeekMaxNeedFraction);
        if (hungerPct > maxNeed || thirstPct > maxNeed)
            return false;

        // If we are already on a tile with a valid same-species merge candidate,
        // try to merge immediately instead of waiting for later reproduction logic.
        if (species.allowGroupMergeDuringMatingSeason)
        {
            int sizeBefore = group.size;
            TryMergeSameSpeciesGroupsForMating(0, ref group);

            if (!group.isAlive || group.size != sizeBefore)
            {
                group.lastAction = AnimalActionType.Breed;

                LogAnimalEvent(
                    "MERGE-MATING-URGENT",
                    group,
                    $"Urgent mating-season merge resolved immediately on tile {group.tile}. sizeNow={group.size}");

                return true;
            }

            // Even if size didn't change, if there is another same-species group on the same tile,
            // treat this as a mating/merge hold state and do not allow softer behaviors to pull us away.
            if (HasOtherSameSpeciesGroupOnTile(group.tile, group.id, species))
            {
                group.lastAction = AnimalActionType.Breed;
                return true;
            }
        }

        if (!species.seekOwnSpeciesDuringMatingSeason)
            return false;

        // Only low-herding species use this steering rule.
        if (group.Herding > species.matingSeekMaxHerding)
            return false;

        if (HasOtherSameSpeciesGroupOnTile(group.tile, group.id, species))
        {
            group.lastAction = AnimalActionType.Breed;
            return true;
        }

        if (!TryFindStepTowardOwnSpeciesGroupDuringMatingSeason(
                group,
                out TileCoord nextStep,
                out TileCoord targetTile,
                out int targetAnimals))
        {
            return false;
        }

        if (nextStep == group.tile)
        {
            group.lastAction = AnimalActionType.Breed;
            return true;
        }

        TileCoord from = group.tile;
        group.tile = nextStep;
        group.lastAction = AnimalActionType.Move;

        LogAnimalEvent(
            "MOVE-MATING-URGENT",
            group,
            $"Urgent mating-season steer moved {from} -> {nextStep} toward same-species group at {targetTile}. " +
            $"targetAnimals={targetAnimals}, hungerPct={hungerPct:F2}, thirstPct={thirstPct:F2}");

        return true;
    }

    private bool TryEatHere(ref AnimalGroupState group, TileEnvironmentData env)
    {
        var species = group.species;
        if (species == null) return false;

        var consumed = _env.ConsumeResourcesForAnimalGroup(
            group.tile,
            species,
            group.size,
            maxHungerToSatisfy: group.hunger,
            maxThirstToSatisfy: 0f
        );

        if (consumed.hungerSatisfied > 0f)
        {
            group.hunger = Mathf.Max(0f, group.hunger - consumed.hungerSatisfied);
            group.lastAction = AnimalActionType.Eat;
            return true;
        }

        bool herbLike = species.diet == AnimalDiet.Herbivore || species.diet == AnimalDiet.Omnivore;
        if (herbLike && env.plantFood > 0f)
        {
            group.hunger = 0f;
            group.lastAction = AnimalActionType.Eat;
            return true;
        }

        return false;
    }

    private void Wander(ref AnimalGroupState group)
    {
        if (_rng.NextDouble() >= 0.3)
        {
            group.lastAction = AnimalActionType.Idle;
            return;
        }

        var species = group.species;

        bool found = false;
        float bestScore = float.NegativeInfinity;
        TileCoord best = group.tile;

        var candidates = new List<TileCoord>(8);
        CollectAnimalMovementCandidates(species, group.tile, candidates);

        for (int i = 0; i < candidates.Count; i++)
        {
            var coord = candidates[i];
            var data = _env.GetTileData(coord);

            if (IsAvoidedHabitat(species, data))
                continue;

            float habitat = GetHabitatSuitability(species, data);
            float noise = (float)(_rng.NextDouble() * 0.1f);

            float score = habitat - data.dangerLevel * 0.1f + noise;

            if (!found || score > bestScore)
            {
                found = true;
                bestScore = score;
                best = coord;
            }
        }

        if (found && best != group.tile)
        {
            group.tile = best;
            group.lastAction = AnimalActionType.Wander;
            return;
        }

        group.lastAction = AnimalActionType.Idle;
    }

    private bool TryFindStepTowardWater(
    AnimalDefinition species,
    TileCoord origin,
    out TileCoord nextStep,
    out TileCoord targetWaterTile)
    {
        nextStep = origin;
        targetWaterTile = origin;

        if (species == null || _env == null)
            return false;

        const int SEARCH_RADIUS = 40;

        if (CanAnimalDrinkAtTile(species, origin))
        {
            targetWaterTile = origin;
            return false;
        }

        var frontier = new Queue<TileCoord>();
        var visited = new HashSet<TileCoord>();
        var cameFrom = new Dictionary<TileCoord, TileCoord>();
        var distanceFromOrigin = new Dictionary<TileCoord, int>();
        var candidates = new List<TileCoord>(8);

        frontier.Enqueue(origin);
        visited.Add(origin);
        distanceFromOrigin[origin] = 0;

        bool found = false;
        TileCoord foundWater = origin;
        float bestWaterScore = float.NegativeInfinity;

        while (frontier.Count > 0)
        {
            TileCoord current = frontier.Dequeue();
            int currentDistance = distanceFromOrigin[current];

            if (currentDistance >= SEARCH_RADIUS)
                continue;

            CollectAnimalMovementCandidates(species, current, candidates);

            for (int i = 0; i < candidates.Count; i++)
            {
                TileCoord neighbour = candidates[i];

                if (visited.Contains(neighbour))
                    continue;

                TileEnvironmentData data = _env.GetTileData(neighbour);
                bool canDrinkHere = CanAnimalDrinkAtTile(species, neighbour);

                if (!canDrinkHere)
                {
                    if (IsAvoidedHabitat(species, data))
                        continue;
                }

                visited.Add(neighbour);
                cameFrom[neighbour] = current;
                distanceFromOrigin[neighbour] = currentDistance + 1;

                if (canDrinkHere)
                {
                    float habitat = GetHabitatSuitability(species, data);
                    float score =
                        habitat * 0.75f -
                        data.dangerLevel * 0.5f -
                        distanceFromOrigin[neighbour] * 0.35f +
                        (float)(_rng.NextDouble() * 0.05f);

                    if (!found || score > bestWaterScore)
                    {
                        found = true;
                        bestWaterScore = score;
                        foundWater = neighbour;
                    }

                    continue;
                }

                frontier.Enqueue(neighbour);
            }
        }

        if (!found)
            return false;

        targetWaterTile = foundWater;

        TileCoord step = foundWater;
        while (cameFrom.TryGetValue(step, out TileCoord parent) && parent != origin)
            step = parent;

        if (cameFrom.TryGetValue(foundWater, out TileCoord immediateParent) && immediateParent == origin)
            nextStep = foundWater;
        else
            nextStep = step;

        return nextStep != origin;
    }

    private bool TryFindFoodTileNear(AnimalDefinition species, TileCoord origin, out TileCoord bestTile)
    {
        bestTile = origin;
        bool found = false;
        float bestScore = float.NegativeInfinity;

        var candidates = new List<TileCoord>(8);
        CollectAnimalMovementCandidates(species, origin, candidates);

        for (int i = 0; i < candidates.Count; i++)
        {
            var coord = candidates[i];
            var data = _env.GetTileData(coord);

            if (IsAvoidedHabitat(species, data))
                continue;

            float foodScore = 0f;

            if (species.diet == AnimalDiet.Herbivore || species.diet == AnimalDiet.Omnivore)
                foodScore = data.plantFood;

            if (species.diet == AnimalDiet.Carnivore || species.diet == AnimalDiet.Omnivore)
            {
                float preyBiomass = GetPreyBiomassOnTile(coord, species);
                foodScore = Math.Max(foodScore, preyBiomass);
            }

            if (foodScore <= 0f)
                continue;

            float habitat = GetHabitatSuitability(species, data);
            float score = foodScore + habitat * 0.75f - data.dangerLevel * 0.5f;

            if (!found || score > bestScore)
            {
                found = true;
                bestScore = score;
                bestTile = coord;
            }
        }

        return found;
    }

    private bool TryDrinkHere(ref AnimalGroupState group, TileEnvironmentData currentTileEnv)
    {
        var species = group.species;
        if (species == null)
            return false;

        float thirstToSatisfy = group.thirst;
        if (thirstToSatisfy <= 0f)
            return false;

        bool hasHydrationResources =
            species.hydrationResources != null &&
            species.hydrationResources.Length > 0;

        if (hasHydrationResources)
        {
            ResourceConsumptionResult result = _env.ConsumeResourcesForAnimalGroup(
                group.tile,
                species,
                group.size,
                maxHungerToSatisfy: 0f,
                maxThirstToSatisfy: thirstToSatisfy);

            if (result.thirstSatisfied > 0f)
            {
                group.thirst = Clamp(group.thirst - result.thirstSatisfied, 0f, species.maxThirst);
                group.lastAction = AnimalActionType.Drink;
                ClearWaterSearchMemory(ref group);
                return true;
            }
        }

        // Keep this definition in sync with the search logic.
        if (_env.GetTileData(group.tile).hasWater)
        {
            group.thirst = 0f;
            group.lastAction = AnimalActionType.Drink;
            ClearWaterSearchMemory(ref group);
            return true;
        }

        return false;
    }

    private float GetHabitatSuitability(AnimalDefinition species, TileEnvironmentData data)
    {
        float score = 0f;

        var prefEnv = species.preferredEnvironments;
        if (prefEnv != null && prefEnv.Length > 0)
        {
            for (int i = 0; i < prefEnv.Length; i++)
            {
                if (prefEnv[i] == data.environmentType) { score += 1f; break; }
            }
        }

        var prefTiles = species.preferredTileTypes;
        if (prefTiles != null && prefTiles.Length > 0)
        {
            for (int i = 0; i < prefTiles.Length; i++)
            {
                if (prefTiles[i] == data.tileType) { score += 1f; break; }
            }
        }

        var avoidEnv = species.avoidedEnvironments;
        if (avoidEnv != null && avoidEnv.Length > 0)
        {
            for (int i = 0; i < avoidEnv.Length; i++)
            {
                if (avoidEnv[i] == data.environmentType) { score -= 1f; break; }
            }
        }

        var avoidTiles = species.avoidedTileTypes;
        if (avoidTiles != null && avoidTiles.Length > 0)
        {
            for (int i = 0; i < avoidTiles.Length; i++)
            {
                if (avoidTiles[i] == data.tileType) { score -= 1f; break; }
            }
        }

        return score;
    }

    private void HerbivoreEat(ref AnimalGroupState group)
    {
        var species = group.species;
        if (species == null)
            return;

        bool hasResourceDiet = species.edibleResources != null &&
                               species.edibleResources.Length > 0;

        if (hasResourceDiet)
        {
            float hungerToSatisfy = group.hunger;

            bool canHydrateFromFood = species.hydrationResources != null &&
                                      species.hydrationResources.Length > 0;

            float thirstFromFood = canHydrateFromFood ? group.thirst * 0.5f : 0f;

            ResourceConsumptionResult result = _env.ConsumeResourcesForAnimalGroup(
                group.tile,
                species,
                group.size,
                maxHungerToSatisfy: hungerToSatisfy,
                maxThirstToSatisfy: thirstFromFood);

            if (result.hungerSatisfied > 0f || result.thirstSatisfied > 0f)
            {
                group.hunger = Clamp(group.hunger - result.hungerSatisfied, 0f, species.maxHunger);
                group.thirst = Clamp(group.thirst - result.thirstSatisfied, 0f, species.maxThirst);
                group.lastAction = AnimalActionType.Eat;
                return;
            }
        }

        group.hunger = 0f;
        group.lastAction = AnimalActionType.Eat;
    }

    private bool IsTemperatureUncomfortable(
    AnimalDefinition species,
    TileCoord tile,
    out float tileTemp,
    out float distanceOutsideRange)
    {
        tileTemp = 0f;
        distanceOutsideRange = 0f;

        if (species == null || !species.useTemperaturePreference)
            return false;

        var climate = ClimateManager.Instance;
        if (climate == null || !climate.HasValidInitialClimate)
            return false;

        if (!climate.TryGetTemperatureAtCell(tile.x, tile.y, out tileTemp))
            return false;

        float minTemp = Mathf.Min(species.minPreferredTemperatureC, species.maxPreferredTemperatureC);
        float maxTemp = Mathf.Max(species.minPreferredTemperatureC, species.maxPreferredTemperatureC);

        if (tileTemp < minTemp)
        {
            distanceOutsideRange = minTemp - tileTemp;
            return true;
        }

        if (tileTemp > maxTemp)
        {
            distanceOutsideRange = tileTemp - maxTemp;
            return true;
        }

        return false;
    }

    private bool TryFindComfortableTemperatureTile(
    AnimalDefinition species,
    TileCoord currentTile,
    float currentTemp,
    out TileCoord bestTile)
    {
        bestTile = currentTile;

        if (species == null || !species.useTemperaturePreference || _env == null)
            return false;

        var climate = ClimateManager.Instance;
        if (climate == null || !climate.HasValidInitialClimate)
            return false;

        float minTemp = Mathf.Min(species.minPreferredTemperatureC, species.maxPreferredTemperatureC);
        float maxTemp = Mathf.Max(species.minPreferredTemperatureC, species.maxPreferredTemperatureC);
        float midTemp = (minTemp + maxTemp) * 0.5f;

        float currentOutside = GetTemperatureDistanceOutsideRange(currentTemp, minTemp, maxTemp);

        var neighbours = GetNeighbourTilesCached(currentTile, 2);

        bool found = false;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < neighbours.Count; i++)
        {
            TileCoord coord = neighbours[i];
            TileEnvironmentData data = _env.GetTileData(coord);

            if (ShouldAvoidHumans(species) && IsPlayerBuildingTile(coord))
                continue;

            if (IsAvoidedHabitat(species, data))
                continue;

            if (!climate.TryGetTemperatureAtCell(coord.x, coord.y, out float temp))
                continue;

            float outsideAmount = GetTemperatureDistanceOutsideRange(temp, minTemp, maxTemp);
            if (outsideAmount > currentOutside)
                continue;

            float distanceFromMid = Mathf.Abs(temp - midTemp);
            float tempScore = -outsideAmount * 10f - distanceFromMid * 0.05f;
            float habitatScore = GetHabitatSuitability(species, data);

            float score = tempScore
                        + habitatScore * 0.25f
                        - data.dangerLevel * 0.1f;

            if (!found || score > bestScore)
            {
                found = true;
                bestScore = score;
                bestTile = coord;
            }
        }

        return found && bestTile != currentTile;
    }

    private float GetTemperatureDistanceOutsideRange(float temp, float minTemp, float maxTemp)
    {
        if (temp < minTemp) return minTemp - temp;
        if (temp > maxTemp) return temp - maxTemp;
        return 0f;
    }

    private float GetPreyBiomassOnTile(TileCoord tile, AnimalDefinition predatorSpecies)
    {
        if (predatorSpecies == null)
            return 0f;

        if (!_tileIndex.TryGetValue(tile, out var ids) || ids == null || ids.Count == 0)
            return 0f;

        float score = 0f;

        // Build a lightweight virtual predator group for evaluation.
        AnimalGroupState virtualPredator = new AnimalGroupState
        {
            species = predatorSpecies,
            size = Mathf.Max(1, predatorSpecies.minGroupSize),
            ageInTurns = Mathf.RoundToInt(predatorSpecies.maxAgeInTurns * 0.35f),
            hunger = predatorSpecies.maxHunger * Mathf.Clamp01(predatorSpecies.huntingHungerThreshold),
            thirst = predatorSpecies.maxThirst * 0.20f,
            currentHealth = Mathf.Max(1, Mathf.Max(1, predatorSpecies.minGroupSize) * Mathf.Max(1, predatorSpecies.healthPerAnimal)),
            tile = tile
        };
        virtualPredator.EnsureHealthValid();

        float hungerPct = predatorSpecies.maxHunger > 0f
            ? virtualPredator.hunger / predatorSpecies.maxHunger
            : 0f;

        bool hasPreferredList = predatorSpecies.preferredPrey != null &&
                                predatorSpecies.preferredPrey.Length > 0;

        for (int i = 0; i < ids.Count; i++)
        {
            int id = ids[i];

            if (!_groups.TryGetValue(id, out var prey))
                continue;

            if (!prey.isAlive || prey.size <= 0)
                continue;

            if (prey.species == null || prey.species == predatorSpecies)
                continue;

            var preyDef = prey.species;

            if (hasPreferredList)
            {
                if (!IsPreferredPrey(predatorSpecies, preyDef))
                    continue;
            }
            else
            {
                if (preyDef.diet != AnimalDiet.Herbivore && preyDef.diet != AnimalDiet.Omnivore)
                    continue;
            }

            // If current hunting logic says this is not a realistic target,
            // don't count it as useful prey biomass.
            if (!ShouldEngagePreyTarget(virtualPredator, prey, hungerPct))
                continue;

            float preyWeakness = GetGroupWeakness01(prey);
            float preyPower = GetGroupCombatPower(prey, includeWeakness: true);
            float predatorPower = GetGroupCombatPower(virtualPredator, includeWeakness: true);

            float safetyScore = predatorPower > 0f
                ? Mathf.Clamp((predatorPower - preyPower) / predatorPower, -1f, 1f)
                : 0f;

            float preyValue = prey.size;

            // Weak prey is more attractive.
            preyValue *= Mathf.Lerp(0.75f, 1.50f, preyWeakness);

            // Dangerous prey is less attractive.
            preyValue *= Mathf.Lerp(0.35f, 1.25f, Mathf.Clamp01(0.5f + safetyScore * 0.5f));

            score += preyValue;
        }

        return score;
    }

    private bool TryHandleOvercrowdingMovement(ref AnimalGroupState group)
    {
        var species = group.species;
        if (species == null || _env == null)
            return false;

        int totalAnimalsHere = GetTotalAnimalsOnTile(group.tile);
        int sameSpeciesAnimalsHere = GetAnimalsOfSpeciesOnTile(group.tile, species);
        int livingGroupsHere = GetLivingGroupCountOnTile(group.tile);

        int comfortableSameSpecies = Mathf.Max(4, group.size * 2);
        int comfortableTotalAnimals = Mathf.Max(6, group.size * 3);
        int comfortableGroupCount = 3;

        bool overcrowded =
            sameSpeciesAnimalsHere > comfortableSameSpecies ||
            totalAnimalsHere > comfortableTotalAnimals ||
            livingGroupsHere > comfortableGroupCount;

        if (!overcrowded)
            return false;

        bool found = false;
        float bestScore = float.NegativeInfinity;
        TileCoord bestTile = group.tile;

        var neighbours = GetNeighbourTilesCached(group.tile, 2);

        for (int i = 0; i < neighbours.Count; i++)
        {
            var coord = neighbours[i];

            if (coord == group.tile)
                continue;

            if (ShouldAvoidHumans(species) && IsPlayerBuildingTile(coord))
                continue;

            var data = _env.GetTileData(coord);

            if (IsAvoidedHabitat(species, data))
                continue;

            int totalAnimalsThere = GetTotalAnimalsOnTile(coord);
            int sameSpeciesAnimalsThere = GetAnimalsOfSpeciesOnTile(coord, species);
            int livingGroupsThere = GetLivingGroupCountOnTile(coord);

            // New: count how many animals/groups have already chosen this tile earlier this turn.
            int incomingAnimalsThere = GetTurnIncomingAnimalsOnTile(coord);
            int incomingGroupsThere = GetTurnIncomingGroupsOnTile(coord);

            int projectedTotalAnimalsThere = totalAnimalsThere + incomingAnimalsThere;
            int projectedLivingGroupsThere = livingGroupsThere + incomingGroupsThere;

            // Use projected counts when deciding if a tile is actually better.
            if (sameSpeciesAnimalsThere > sameSpeciesAnimalsHere &&
                projectedTotalAnimalsThere >= totalAnimalsHere)
            {
                continue;
            }

            float habitatScore = GetHabitatSuitability(species, data);

            float crowdReliefScore =
                (sameSpeciesAnimalsHere - sameSpeciesAnimalsThere) * 1.5f +
                (totalAnimalsHere - projectedTotalAnimalsThere) * 0.35f +
                (livingGroupsHere - projectedLivingGroupsThere) * 0.75f;

            // New: strong penalty if many groups already selected this tile this turn.
            float incomingPenalty =
                incomingAnimalsThere * 0.40f +
                incomingGroupsThere * 1.25f;

            float score =
                crowdReliefScore +
                habitatScore * 0.85f -
                data.dangerLevel * 0.5f -
                incomingPenalty +
                (float)(_rng.NextDouble() * 0.25f);

            if (!found || score > bestScore)
            {
                found = true;
                bestScore = score;
                bestTile = coord;
            }
        }

        if (found && bestTile != group.tile)
        {
            TileCoord from = group.tile;
            group.tile = bestTile;
            group.lastAction = AnimalActionType.Move;

            LogAnimalEvent(
                "MOVE-CROWD",
                group,
                $"Moved {from} -> {bestTile} due to overcrowding. " +
                $"sameSpeciesHere={sameSpeciesAnimalsHere}, totalAnimalsHere={totalAnimalsHere}, livingGroupsHere={livingGroupsHere}, " +
                $"incomingThere={GetTurnIncomingAnimalsOnTile(bestTile)}, incomingGroupsThere={GetTurnIncomingGroupsOnTile(bestTile)}");

            return true;
        }

        return false;
    }

    private int GetTotalAnimalsOnTile(TileCoord tile)
    {
        if (!_tileIndex.TryGetValue(tile, out var ids) || ids == null || ids.Count == 0)
            return 0;

        int total = 0;

        for (int i = 0; i < ids.Count; i++)
        {
            int id = ids[i];

            if (!_groups.TryGetValue(id, out var other))
                continue;

            if (!other.isAlive || other.size <= 0)
                continue;

            total += other.size;
        }

        return total;
    }

    private int GetAnimalsOfSpeciesOnTile(TileCoord tile, AnimalDefinition species)
    {
        if (species == null)
            return 0;

        if (!_tileIndex.TryGetValue(tile, out var ids) || ids == null || ids.Count == 0)
            return 0;

        int total = 0;

        for (int i = 0; i < ids.Count; i++)
        {
            int id = ids[i];

            if (!_groups.TryGetValue(id, out var other))
                continue;

            if (!other.isAlive || other.size <= 0 || other.species != species)
                continue;

            total += other.size;
        }

        return total;
    }

    private int GetLivingGroupCountOnTile(TileCoord tile)
    {
        if (!_tileIndex.TryGetValue(tile, out var ids) || ids == null || ids.Count == 0)
            return 0;

        int count = 0;

        for (int i = 0; i < ids.Count; i++)
        {
            int id = ids[i];

            if (!_groups.TryGetValue(id, out var other))
                continue;

            if (!other.isAlive || other.size <= 0)
                continue;

            count++;
        }

        return count;
    }

    private bool IsEnvironmentAvoided(AnimalDefinition species, EnvironmentType envType)
    {
        if (species == null || species.avoidedEnvironments == null || species.avoidedEnvironments.Length == 0)
            return false;

        for (int i = 0; i < species.avoidedEnvironments.Length; i++)
        {
            if (species.avoidedEnvironments[i] == envType)
                return true;
        }

        return false;
    }

    private bool IsTileTypeAvoided(AnimalDefinition species, EnvironmentTileType tileType)
    {
        if (species == null || species.avoidedTileTypes == null || species.avoidedTileTypes.Length == 0)
            return false;

        for (int i = 0; i < species.avoidedTileTypes.Length; i++)
        {
            if (species.avoidedTileTypes[i] == tileType)
                return true;
        }

        return false;
    }

    private bool IsAvoidedHabitat(AnimalDefinition species, TileEnvironmentData data)
    {
        if (species == null)
            return false;

        if (IsEnvironmentAvoided(species, data.environmentType))
            return true;

        if (IsTileTypeAvoided(species, data.tileType))
            return true;

        return false;
    }

    private bool TryHandleMatingSeasonSpeciesSeeking(ref AnimalGroupState group)
    {
        var species = group.species;
        if (species == null || _env == null)
            return false;

        if (!species.seekOwnSpeciesDuringMatingSeason)
            return false;

        if (!IsInMatingSeason(species))
            return false;

        // Only low-herding species use this rule.
        if (group.Herding > species.matingSeekMaxHerding)
            return false;

        // If we already share the tile with another same-species group, no need to seek.
        if (HasOtherSameSpeciesGroupOnTile(group.tile, group.id, species))
            return false;

        if (!TryFindStepTowardOwnSpeciesGroupDuringMatingSeason(
                group,
                out TileCoord nextStep,
                out TileCoord targetTile,
                out int targetAnimals))
        {
            return false;
        }

        if (nextStep == group.tile)
            return false;

        TileCoord from = group.tile;
        group.tile = nextStep;
        group.lastAction = AnimalActionType.Move;

        LogAnimalEvent(
            "MOVE-MATING",
            group,
            $"Moved {from} -> {nextStep} toward same-species group at {targetTile} during mating season. " +
            $"targetAnimals={targetAnimals}, herding={group.Herding:F2}");

        return true;
    }

    private bool TryFindStepTowardOwnSpeciesGroupDuringMatingSeason(
    AnimalGroupState seeker,
    out TileCoord nextStep,
    out TileCoord targetTile,
    out int targetAnimals)
    {
        nextStep = seeker.tile;
        targetTile = seeker.tile;
        targetAnimals = 0;

        var species = seeker.species;
        if (species == null || _env == null)
            return false;

        int searchRadius = Mathf.Max(1, species.matingSeekRangeTiles);

        var frontier = new Queue<TileCoord>();
        var visited = new HashSet<TileCoord>();
        var cameFrom = new Dictionary<TileCoord, TileCoord>();
        var distanceFromOrigin = new Dictionary<TileCoord, int>();
        var candidates = new List<TileCoord>(8);

        frontier.Enqueue(seeker.tile);
        visited.Add(seeker.tile);
        distanceFromOrigin[seeker.tile] = 0;

        bool found = false;
        float bestScore = float.NegativeInfinity;
        TileCoord bestTarget = seeker.tile;
        int bestTargetAnimals = 0;

        while (frontier.Count > 0)
        {
            TileCoord current = frontier.Dequeue();
            int currentDistance = distanceFromOrigin[current];

            if (currentDistance >= searchRadius)
                continue;

            CollectAnimalMovementCandidates(species, current, candidates);

            for (int i = 0; i < candidates.Count; i++)
            {
                TileCoord neighbour = candidates[i];

                if (visited.Contains(neighbour))
                    continue;

                TileEnvironmentData data = _env.GetTileData(neighbour);

                if (IsAvoidedHabitat(species, data))
                    continue;

                visited.Add(neighbour);
                cameFrom[neighbour] = current;
                distanceFromOrigin[neighbour] = currentDistance + 1;

                int sameSpeciesAnimals = GetOtherAnimalsOfSpeciesOnTile(neighbour, seeker.id, species);
                if (sameSpeciesAnimals >= Mathf.Max(1, species.matingSeekMinTargetGroupSize))
                {
                    float habitatScore = GetHabitatSuitability(species, data);
                    float score =
                        sameSpeciesAnimals * 1.5f +
                        habitatScore * 0.75f -
                        data.dangerLevel * 0.5f -
                        distanceFromOrigin[neighbour] * 0.4f +
                        (float)(_rng.NextDouble() * 0.05f);

                    if (!found || score > bestScore)
                    {
                        found = true;
                        bestScore = score;
                        bestTarget = neighbour;
                        bestTargetAnimals = sameSpeciesAnimals;
                    }
                }

                frontier.Enqueue(neighbour);
            }
        }

        if (!found)
            return false;

        targetTile = bestTarget;
        targetAnimals = bestTargetAnimals;

        TileCoord step = bestTarget;
        while (cameFrom.TryGetValue(step, out TileCoord parent) && parent != seeker.tile)
            step = parent;

        if (cameFrom.TryGetValue(bestTarget, out TileCoord immediateParent) && immediateParent == seeker.tile)
            nextStep = bestTarget;
        else
            nextStep = step;

        return nextStep != seeker.tile;
    }

    private bool HasOtherSameSpeciesGroupOnTile(TileCoord tile, int selfId, AnimalDefinition species)
    {
        if (species == null)
            return false;

        if (!_tileIndex.TryGetValue(tile, out var ids) || ids == null || ids.Count == 0)
            return false;

        for (int i = 0; i < ids.Count; i++)
        {
            int id = ids[i];
            if (id == selfId)
                continue;

            if (!_groups.TryGetValue(id, out var other))
                continue;

            if (!other.isAlive || other.size <= 0)
                continue;

            if (other.species == species)
                return true;
        }

        return false;
    }

    private int GetOtherAnimalsOfSpeciesOnTile(TileCoord tile, int selfId, AnimalDefinition species)
    {
        if (species == null)
            return 0;

        if (!_tileIndex.TryGetValue(tile, out var ids) || ids == null || ids.Count == 0)
            return 0;

        int total = 0;

        for (int i = 0; i < ids.Count; i++)
        {
            int id = ids[i];
            if (id == selfId)
                continue;

            if (!_groups.TryGetValue(id, out var other))
                continue;

            if (!other.isAlive || other.size <= 0)
                continue;

            if (other.species != species)
                continue;

            total += other.size;
        }

        return total;
    }

    private bool TryHandleUrgentWaterNeed(
    ref AnimalGroupState group,
    TileEnvironmentData currentTileEnv,
    float hungerPct,
    float thirstPct)
    {
        var species = group.species;
        if (species == null || species.maxThirst <= 0f)
            return false;

        float threshold = Mathf.Clamp01(species.abandonHuntForWaterNeedThreshold);
        if (thirstPct < threshold)
            return false;

        bool wasHunting = group.isHunting || group.huntingTargetGroupId > 0;
        if (wasHunting)
            ClearHuntingTarget(ref group);

        if (TryDrinkHere(ref group, currentTileEnv))
        {
            LogAnimalEvent(
                "DRINK-URGENT",
                group,
                $"Urgent thirst handled immediately. " +
                $"wasHunting={wasHunting}, hungerPct={hungerPct:F2}, thirstPct={thirstPct:F2}, threshold={threshold:F2}");

            return true;
        }

        if (TryFindStepTowardWater(species, group.tile, out var nextStep, out var targetWaterTile))
        {
            TileCoord from = group.tile;
            group.tile = nextStep;
            group.lastAction = AnimalActionType.Move;

            LogAnimalEvent(
                "MOVE-WATER-URGENT",
                group,
                $"Urgent thirst forced water-seeking. " +
                $"wasHunting={wasHunting}, moved {from} -> {nextStep}, targetWaterTile={targetWaterTile}, " +
                $"hungerPct={hungerPct:F2}, thirstPct={thirstPct:F2}, threshold={threshold:F2}");

            return true;
        }

        // New fallback: keep searching tile-to-tile instead of giving up.
        if (TryFindWaterSearchStep(ref group, thirstPct, out var searchStep))
        {
            TileCoord from = group.tile;
            group.tile = searchStep;
            group.lastAction = AnimalActionType.Move;
            RegisterWaterSearchStep(ref group, from);

            LogAnimalEvent(
                "MOVE-WATER-SEARCH-URGENT",
                group,
                $"Urgent thirst could not find reachable water yet, so moved {from} -> {searchStep} to continue searching. " +
                $"wasHunting={wasHunting}, hungerPct={hungerPct:F2}, thirstPct={thirstPct:F2}, threshold={threshold:F2}");

            return true;
        }

        return false;
    }

    private bool TryFindWaterSearchStep(
    ref AnimalGroupState group,
    float thirstPct,
    out TileCoord bestStep)
    {
        bestStep = group.tile;

        var species = group.species;
        if (species == null || _env == null)
            return false;

        TileCoord origin = group.tile;
        bool found = false;
        float bestScore = float.NegativeInfinity;

        float focusedThreshold = Mathf.Clamp01(species.focusedWaterSearchThreshold);
        bool useFocusedMemory = thirstPct >= focusedThreshold;

        var candidates = new List<TileCoord>(8);
        CollectAnimalMovementCandidates(species, origin, candidates);

        for (int i = 0; i < candidates.Count; i++)
        {
            TileCoord coord = candidates[i];

            if (coord == origin)
                continue;

            TileEnvironmentData data = _env.GetTileData(coord);
            bool canDrinkHere = CanAnimalDrinkAtTile(species, coord);

            if (!canDrinkHere)
            {
                if (IsAvoidedHabitat(species, data))
                    continue;
            }

            float score = 0f;

            if (canDrinkHere)
                score += 1000f;

            score += GetHabitatSuitability(species, data) * 1.25f;
            score -= data.dangerLevel * 0.75f;

            score -= GetTurnIncomingAnimalsOnTile(coord) * 0.15f;
            score -= GetTurnIncomingGroupsOnTile(coord) * 0.75f;

            if (useFocusedMemory &&
                group.hasWaterSearchMemory &&
                group.waterSearchBacktrackAvoidanceTurns > 0)
            {
                if (coord == group.lastWaterSearchPreviousTile)
                    score -= 25f;

                if (coord == group.secondLastWaterSearchPreviousTile)
                    score -= 12f;
            }

            score += (float)(_rng.NextDouble() * 0.1f);

            if (!found || score > bestScore)
            {
                found = true;
                bestScore = score;
                bestStep = coord;
            }
        }

        return found && bestStep != origin;
    }

    private void ClearWaterSearchMemory(ref AnimalGroupState group)
    {
        group.hasWaterSearchMemory = false;
        group.lastWaterSearchPreviousTile = group.tile;
        group.secondLastWaterSearchPreviousTile = group.tile;
        group.waterSearchBacktrackAvoidanceTurns = 0;
    }

    private void RegisterWaterSearchStep(ref AnimalGroupState group, TileCoord fromTile)
    {
        if (!group.hasWaterSearchMemory)
        {
            group.hasWaterSearchMemory = true;
            group.lastWaterSearchPreviousTile = fromTile;
            group.secondLastWaterSearchPreviousTile = fromTile;
        }
        else
        {
            group.secondLastWaterSearchPreviousTile = group.lastWaterSearchPreviousTile;
            group.lastWaterSearchPreviousTile = fromTile;
        }

        // Keep memory alive a little longer now that we track 2 tiles.
        group.waterSearchBacktrackAvoidanceTurns = 3;
    }

    private void DecayWaterSearchMemory(ref AnimalGroupState group)
    {
        if (!group.hasWaterSearchMemory)
            return;

        group.waterSearchBacktrackAvoidanceTurns--;

        if (group.waterSearchBacktrackAvoidanceTurns <= 0)
            ClearWaterSearchMemory(ref group);
    }

    private bool CanAnimalDrinkAtTile(AnimalDefinition species, TileCoord coord)
    {
        if (species == null || _env == null)
            return false;

        TileEnvironmentData data = _env.GetTileData(coord);

        if (data.hasWater)
            return true;

        if (_env is MonoEnvironmentDataSource monoEnv &&
            monoEnv.HasHydrationResourcesForSpecies(coord, species))
        {
            return true;
        }

        return false;
    }

    private bool TryBlockActionsWhileTargetedByHumanUnit(ref AnimalGroupState group)
    {
        if (!group.isTargetedByHumanUnits)
            return false;

        ClearPlannedActionBecauseHumanTargeted(ref group);
        group.lastAction = AnimalActionType.Idle;
        return true;
    }

    private void CollectAnimalMovementCandidates(
    AnimalDefinition species,
    TileCoord origin,
    List<TileCoord> results)
    {
        results.Clear();

        var neighbours = GetNeighbourTilesCached(origin, 1);
        for (int i = 0; i < neighbours.Count; i++)
        {
            TileCoord coord = neighbours[i];

            if (coord == origin)
                continue;

            // If this neighbour is a building tile the animal should avoid,
            // try to "hop" over it to the next tile in the same direction.
            if (ShouldAvoidHumans(species) && IsPlayerBuildingTile(coord))
            {
                if (TryGetHopLandingOverBuilding(origin, coord, out TileCoord landingTile))
                    AddUniqueMovementCandidate(results, landingTile);

                continue;
            }

            AddUniqueMovementCandidate(results, coord);
        }
    }

    private bool TryGetHopLandingOverBuilding(
        TileCoord origin,
        TileCoord buildingTile,
        out TileCoord landingTile)
    {
        landingTile = origin;

        int dx = buildingTile.x - origin.x;
        int dy = buildingTile.y - origin.y;

        if (dx == 0 && dy == 0)
            return false;

        TileCoord candidate = new TileCoord(buildingTile.x + dx, buildingTile.y + dy);

        // Make sure the candidate is actually a valid neighbour of the building tile.
        var buildingNeighbours = GetNeighbourTilesCached(buildingTile, 1);
        bool isValidContinuation = false;

        for (int i = 0; i < buildingNeighbours.Count; i++)
        {
            if (buildingNeighbours[i] == candidate)
            {
                isValidContinuation = true;
                break;
            }
        }

        if (!isValidContinuation)
            return false;

        // Do not hop onto another building tile.
        if (IsPlayerBuildingTile(candidate))
            return false;

        landingTile = candidate;
        return true;
    }

    private void AddUniqueMovementCandidate(List<TileCoord> results, TileCoord coord)
    {
        for (int i = 0; i < results.Count; i++)
        {
            if (results[i] == coord)
                return;
        }

        results.Add(coord);
    }
}