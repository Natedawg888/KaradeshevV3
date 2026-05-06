using System;

public partial class AnimalSimulation
{
    private void ApplyMortality(ref AnimalGroupState group)
    {
        if (group.size <= 0)
            return;

        var species = group.species;
        if (species == null)
            return;

        float hungerPct = species.maxHunger > 0f ? (group.hunger / species.maxHunger) : 0f;
        float thirstPct = species.maxThirst > 0f ? (group.thirst / species.maxThirst) : 0f;

        int deaths = 0;

        // --- Starvation ---
        if (species.starvationThreshold > 0f && hungerPct >= species.starvationThreshold)
        {
            float fraction = species.starvationDeathFractionPerTurn;
            if (fraction > 0f)
            {
                int lost = (int)Math.Floor(group.size * fraction);
                if (lost <= 0) lost = 1;
                deaths += lost;
            }
        }

        // --- Dehydration ---
        if (species.dehydrationThreshold > 0f && thirstPct >= species.dehydrationThreshold)
        {
            float fraction = species.dehydrationDeathFractionPerTurn;
            if (fraction > 0f)
            {
                int lost = (int)Math.Floor(group.size * fraction);
                if (lost <= 0) lost = 1;
                deaths += lost;
            }
        }

        // --- Old age ---
        if (species.maxAgeInTurns > 0 && group.ageInTurns >= species.maxAgeInTurns)
        {
            float fraction = species.oldAgeDeathFractionPerTurn;
            if (fraction > 0f)
            {
                int lost = (int)Math.Floor(group.size * fraction);
                if (lost <= 0) lost = 1;
                deaths += lost;
            }
        }

        if (deaths > 0)
        {
            if (deaths > group.size)
                deaths = group.size;

            group.size -= deaths;
            if (group.size < 0)
                group.size = 0;
        }
    }
}