using System;
using UnityEngine;

public partial class AnimalSimulation
{
    private bool TryHandleHerdingMovement(ref AnimalGroupState group)
    {
        var species = group.species;
        if (species == null || _env == null)
            return false;

        // If this group is already committed to a target/chase/conflict,
        // do not let social steering override that.
        if (ShouldSuppressHerdingMovement(group))
            return false;

        float herding01 = Mathf.Clamp01(species.herding);
        bool matingSeason = IsInMatingSeason(species);

        // High herding pulls toward groups.
        // Low herding pushes away from groups.
        // During mating season, don't push away, and allow own-species pull.
        bool wantsAttraction = herding01 >= 0.55f || matingSeason;
        bool wantsRepulsion = herding01 <= 0.35f && !matingSeason;

        if (!wantsAttraction && !wantsRepulsion)
            return false;

        TileCoord current = group.tile;
        TileCoord bestTile = current;

        float bestScore = EvaluateHerdingTileScore(
            ref group,
            current,
            wantsAttraction,
            wantsRepulsion,
            matingSeason);

        bool foundBetter = false;

        var neighbours = GetNeighbourTilesCached(current, 1);
        for (int i = 0; i < neighbours.Count; i++)
        {
            var coord = neighbours[i];

            if (coord.Equals(current))
                continue;

            if (ShouldAvoidHumans(species) && IsPlayerBuildingTile(coord))
                continue;

            float score = EvaluateHerdingTileScore(
                ref group,
                coord,
                wantsAttraction,
                wantsRepulsion,
                matingSeason);

            // small threshold so groups do not jitter constantly
            if (score > bestScore + 0.05f)
            {
                bestScore = score;
                bestTile = coord;
                foundBetter = true;
            }
        }

        if (!foundBetter || bestTile.Equals(current))
            return false;

        group.tile = bestTile;
        group.lastAction = AnimalActionType.Move;
        return true;
    }

    private float EvaluateHerdingTileScore(
        ref AnimalGroupState group,
        TileCoord candidate,
        bool wantsAttraction,
        bool wantsRepulsion,
        bool matingSeason)
    {
        var species = group.species;
        if (species == null)
            return float.NegativeInfinity;

        var data = _env.GetTileData(candidate);

        float herding01 = Mathf.Clamp01(species.herding);

        // Keep habitat relevant so animals don't herd into terrible tiles.
        float score =
            GetHabitatSuitability(species, data) * 0.35f
            - data.dangerLevel * 0.10f;

        float attraction = 0f;
        float repulsion = 0f;

        // Scan a small local area around the candidate tile.
        var nearby = GetNeighbourTilesCached(candidate, 2);
        for (int n = 0; n < nearby.Count; n++)
        {
            var otherTile = nearby[n];

            if (!_tileIndex.TryGetValue(otherTile, out var ids) || ids == null || ids.Count == 0)
                continue;

            int dist = Mathf.Abs(otherTile.x - candidate.x) + Mathf.Abs(otherTile.y - candidate.y);
            float proximity = 1f / Mathf.Max(1, dist);

            for (int i = 0; i < ids.Count; i++)
            {
                int otherId = ids[i];
                if (otherId == group.id)
                    continue;

                if (!_groups.TryGetValue(otherId, out var other))
                    continue;

                if (!other.isAlive || other.size <= 0 || other.species == null)
                    continue;

                float sizeFactor = Mathf.Lerp(
                    0.75f,
                    1.25f,
                    Mathf.Clamp01(other.size / (float)Mathf.Max(1, species.maxGroupSize)));

                if (wantsAttraction)
                {
                    float affinity = GetHerdingAffinity(species, other.species, matingSeason);
                    if (affinity > 0f)
                        attraction += affinity * proximity * sizeFactor;
                }

                if (wantsRepulsion)
                {
                    float repel = GetHerdingRepulsion(species, other.species);
                    if (repel > 0f)
                        repulsion += repel * proximity * sizeFactor;
                }
            }
        }

        if (wantsAttraction)
        {
            float attractionScale = Mathf.Lerp(0.40f, 1.50f, herding01);

            // During mating season, pull harder toward own species.
            if (matingSeason)
                attractionScale += 0.25f;

            score += attraction * attractionScale;
        }

        if (wantsRepulsion)
        {
            // Lower herding = stronger push away from groups.
            float repulsionScale = Mathf.Lerp(1.50f, 0.40f, herding01);
            score -= repulsion * repulsionScale;
        }

        return score;
    }

    private float GetHerdingAffinity(
        AnimalDefinition self,
        AnimalDefinition other,
        bool matingSeason)
    {
        if (self == null || other == null)
            return 0f;

        float affinity = 0f;

        // Strongest attraction: own species
        if (self == other)
        {
            affinity = matingSeason ? 2.5f : 2.0f;
        }
        // Second strongest: explicitly liked species
        else if (IsLikedAnimal(self, other))
        {
            affinity = 1.25f;
        }

        // Predators can also cluster with nearby predator groups
        // (especially pack-style behavior like wolves).
        if (IsPredatorLikeForHerding(self) &&
            IsPredatorLikeForHerding(other) &&
            !IsInDislikedList(self.dislikedPredators, other))
        {
            affinity = Mathf.Max(affinity, self == other ? 2.0f : 0.75f);
        }

        return affinity;
    }

    private float GetHerdingRepulsion(AnimalDefinition self, AnimalDefinition other)
    {
        if (self == null || other == null)
            return 0f;

        // Low-herding species avoid groups in general,
        // but repel a bit less from their own species / liked species.
        if (self == other)
            return 0.75f;

        if (IsLikedAnimal(self, other))
            return 0.50f;

        return 1.0f;
    }

    private bool IsLikedAnimal(AnimalDefinition self, AnimalDefinition other)
    {
        if (self == null || other == null)
            return false;

        var liked = self.likedAnimals;
        if (liked == null || liked.Length == 0)
            return false;

        return Array.IndexOf(liked, other) >= 0;
    }

    private bool IsPredatorLikeForHerding(AnimalDefinition def)
    {
        if (def == null)
            return false;

        return def.diet == AnimalDiet.Carnivore || def.diet == AnimalDiet.Omnivore;
    }

    private bool ShouldSuppressHerdingMovement(AnimalGroupState group)
    {
        if ((group.isHunting && group.huntingTargetGroupId > 0) ||
            group.isTargetedByPredator ||
            (group.isInPredatorConflict && group.predatorConflictTargetGroupId > 0) ||
            group.isRaidingPlayerTile ||
            group.isHuntingHumanUnits)
        {
            return true;
        }

        return false;
    }
}