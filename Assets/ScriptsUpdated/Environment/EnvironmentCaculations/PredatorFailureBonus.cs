using System.Collections.Generic;
using UnityEngine;

public static class PredatorFailureBonus
{
    // Tuning (slight boost)
    private const float BONUS_PER_PREDATOR_ANIMAL = 0.25f; // +0.25% per predator animal on tile
    private const float MAX_BONUS_PERCENT = 10f;           // cap so it can't blow out

    private static readonly List<AnimalGroupState> _buf = new(32);

    public static float GetBonusPercent(TileControl tile)
    {
        if (tile == null) return 0f;

        var sim = AnimalSimulationAccess.Current;
        if (sim == null) return 0f;

        Vector2Int grid = tile.GetGridPosition();
        TileCoord coord = new TileCoord(grid.x, grid.y);

        _buf.Clear();
        sim.CollectGroupsOnTile(coord, _buf);

        int predatorAnimals = 0;

        for (int i = 0; i < _buf.Count; i++)
        {
            var g = _buf[i];
            if (g == null || !g.isAlive || g.species == null) continue;

            if (!IsPredator(g.species)) continue;

            predatorAnimals += Mathf.Max(0, g.size);
        }

        float bonus = predatorAnimals * BONUS_PER_PREDATOR_ANIMAL;
        return Mathf.Clamp(bonus, 0f, MAX_BONUS_PERCENT);
    }

    private static bool IsPredator(AnimalDefinition species)
    {
        if (species == null) return false;

        // Simple + safe default:
        // - Carnivores count
        // - Anything that huntsHumans counts
        return species.huntsHumans || species.diet == AnimalDiet.Carnivore;
    }
}
