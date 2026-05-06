using UnityEngine;

public class AnimalGroupState
{
    public int id;
    public AnimalDefinition species;

    public int size;
    public int ageInTurns;

    public int currentHealth = -1;

    public float hunger;
    public float thirst;

    public TileCoord tile;

    public AnimalActionType lastAction;
    public int nextUpdateTurn;

    public bool isLeader;
    public int herdId;
    public int leaderGroupId;

    // NEW: hunting state
    public bool isHunting;              // this group is actively hunting
    public int huntingTargetGroupId;    // prey group id it is chasing (-1 = none)
    public bool isTargetedByPredator;   // this group is being hunted

    public int huntingEscapeCount;

    public int nextReproductionTurn;     // turn when this group *may* breed again
    public bool isOnReproductionCooldown;

    // 🔹 NEW: predator vs predator conflict state
    public bool isInPredatorConflict;
    public int predatorConflictTargetGroupId = -1;

    public int targetedByPredatorGroupId = -1;

    public bool isFleeingFromThreat;
    public int fleeFromPredatorGroupId = -1;
    public int fleeUntilDistanceTiles = 0;
    public TileCoord fleeThreatLastKnownTile;
    public int fleeStepsRemaining = 0;

    public bool hasWaterSearchMemory;
    public TileCoord lastWaterSearchPreviousTile;
    public TileCoord secondLastWaterSearchPreviousTile;
    public int waterSearchBacktrackAvoidanceTurns;

    public bool isAlive => size > 0 && currentHealth > 0 && species != null;

    public bool isRaidingPlayerTile;
    public TileCoord raidTargetTile;

    public bool isHuntingHumanUnits;
    public string huntingHumanUnitGroupId;

    public int resolvedHealthPerAnimal = -1;
    public float resolvedAggression = -1f;
    public float resolvedFlightiness = -1f;
    public float resolvedHerding = -1f;
    public float resolvedStrength = -1f;
    public float resolvedDefense = -1f;
    public float resolvedSpeed = -1f;
    public float resolvedSense = -1f;
    public float resolvedStealth = -1f;

    public float resolvedBreedingFemaleFraction = -1f;

    public bool isTargetedByHumanUnits;

    public int HealthPerAnimal
        => species != null
            ? Mathf.Max(1, resolvedHealthPerAnimal > 0 ? resolvedHealthPerAnimal : species.healthPerAnimal)
            : 1;

    public int MaxHealth => Mathf.Max(0, size) * HealthPerAnimal;

    public float Aggression
        => species != null ? Mathf.Clamp01(resolvedAggression >= 0f ? resolvedAggression : species.aggression) : 0f;

    public float Flightiness
        => species != null ? Mathf.Clamp01(resolvedFlightiness >= 0f ? resolvedFlightiness : species.flightiness) : 0f;

    public float Herding
        => species != null ? Mathf.Clamp01(resolvedHerding >= 0f ? resolvedHerding : species.herding) : 0f;

    public float Strength
        => species != null ? Mathf.Clamp01(resolvedStrength >= 0f ? resolvedStrength : species.strength) : 0f;

    public float Defense
        => species != null ? Mathf.Clamp01(resolvedDefense >= 0f ? resolvedDefense : species.defense) : 0f;

    public float Speed
        => species != null ? Mathf.Clamp01(resolvedSpeed >= 0f ? resolvedSpeed : species.speed) : 0f;

    public float Sense
        => species != null ? Mathf.Clamp01(resolvedSense >= 0f ? resolvedSense : species.sense) : 0f;

    public float Stealth
        => species != null ? Mathf.Clamp01(resolvedStealth >= 0f ? resolvedStealth : species.stealth) : 0f;

    public float BreedingFemaleFraction
    => species != null
        ? Mathf.Clamp01(
            resolvedBreedingFemaleFraction >= 0f
                ? resolvedBreedingFemaleFraction
                : species.breedingFemaleFraction)
        : 0f;

    public void EnsureHealthValid()
    {
        int max = MaxHealth;

        if (currentHealth < 0)
            currentHealth = max;

        int before = currentHealth;
        int oldMax = before > max ? before : max;

        currentHealth = Mathf.Clamp(currentHealth, 0, max);

        if (before != currentHealth)
        {
            Debug.Log(
                $"[AnimalHealthClamp] {species?.displayName} group {id} HP clamped {before}->{currentHealth} | size={size} | maxHP={max}");
        }
    }
}
