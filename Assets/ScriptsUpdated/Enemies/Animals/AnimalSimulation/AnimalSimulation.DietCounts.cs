using System;

public partial class AnimalSimulation
{
    public struct DietGroupCounts
    {
        public int herbivore;
        public int carnivore;
        public int omnivore;
        public int unknown;

        public int total => herbivore + carnivore + omnivore + unknown;

        // Common convenience rollups (useful for predator balancing)
        public int predatorLike => carnivore + omnivore; // carn + omni
        public int preyLike => herbivore + omnivore; // herb + omni (if you treat omni as prey sometimes)
    }

    /// <summary>
    /// Returns group counts by diet. Defaults to alive groups only.
    /// </summary>
    public DietGroupCounts GetDietGroupCounts(bool aliveOnly = true)
    {
        DietGroupCounts c = default;

        foreach (var kvp in _groups)
        {
            var g = kvp.Value;

            if (aliveOnly && (!g.isAlive || g.size <= 0))
                continue;

            var def = g.species;
            if (def == null)
            {
                c.unknown++;
                continue;
            }

            switch (def.diet)
            {
                case AnimalDiet.Herbivore: c.herbivore++; break;
                case AnimalDiet.Carnivore: c.carnivore++; break;
                case AnimalDiet.Omnivore: c.omnivore++; break;
                default: c.unknown++; break;
            }
        }

        return c;
    }

    // --- Overloads to match whatever your controller expects ---

    public void GetDietGroupCounts(out int herbivoreGroups, out int carnivoreGroups, out int omnivoreGroups)
    {
        var c = GetDietGroupCounts(aliveOnly: true);
        herbivoreGroups = c.herbivore;
        carnivoreGroups = c.carnivore;
        omnivoreGroups = c.omnivore;
    }

    public void GetDietGroupCounts(out int herbivoreGroups, out int carnivoreGroups, out int omnivoreGroups, out int totalGroups)
    {
        var c = GetDietGroupCounts(aliveOnly: true);
        herbivoreGroups = c.herbivore;
        carnivoreGroups = c.carnivore;
        omnivoreGroups = c.omnivore;
        totalGroups = c.total;
    }

    public void GetDietGroupCounts(out int herbivoreGroups, out int carnivoreGroups, out int omnivoreGroups, out int unknownGroups, out int totalGroups)
    {
        var c = GetDietGroupCounts(aliveOnly: true);
        herbivoreGroups = c.herbivore;
        carnivoreGroups = c.carnivore;
        omnivoreGroups = c.omnivore;
        unknownGroups = c.unknown;
        totalGroups = c.total;
    }

    public void GetDietGroupCounts(out int herbivoreGroups, out int carnivoreLikeGroups)
    {
        var c = GetDietGroupCounts(aliveOnly: true);

        // In your PredatorBalancing you treat "carnGroups" as predator groups.
        // Since you spawn Carnivore OR Omnivore as predators, count both here.
        herbivoreGroups = c.herbivore;
        carnivoreLikeGroups = c.carnivore + c.omnivore;
    }
}
