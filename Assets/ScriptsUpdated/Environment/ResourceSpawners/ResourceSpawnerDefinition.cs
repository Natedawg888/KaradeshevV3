using System.Collections.Generic;
using UnityEngine;

// Evaluates a spawn chance multiplier from the current tile climate.
// temperatureCurve  X = °C,  Y = multiplier (0..2)
// humidityCurve     X = 0..1, Y = multiplier (0..2)
// Final multiplier = temp_mult × humidity_mult, clamped ≥ 0.
[System.Serializable]
public class ResourceSpawnerClimateSettings
{
    [Tooltip("If false, climate is ignored and the multiplier is always 1.")]
    public bool enabled = false;

    [Tooltip("Maps temperature (°C) to a spawn chance multiplier.")]
    public AnimationCurve temperatureCurve = AnimationCurve.Linear(-10f, 1f, 50f, 1f);

    [Tooltip("Maps humidity (0..1) to a spawn chance multiplier.")]
    public AnimationCurve humidityCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

    public float EvaluateMultiplier(float tempC, float humidity01)
    {
        if (!enabled) return 1f;
        float t = temperatureCurve != null ? temperatureCurve.Evaluate(tempC)      : 1f;
        float h = humidityCurve    != null ? humidityCurve.Evaluate(humidity01)    : 1f;
        return Mathf.Max(0f, t * h);
    }
}

public enum SpawnerCategory
{
    Plant,
    AnimalRemains,
    BurntRemains,
    WaterCoastal,
    GroundMaterial,
    WeatherCreated,
    EnvironmentBackground,
    Root,
    Bush,
    Tree,
    StoneDeposit,
    SmallAnimal
}

[System.Serializable]
public class ResourceSpawnerOutput
{
    [Tooltip("The resource this output produces.")]
    public ResourceDefinition resource;

    [Min(1)] public int minAmount = 1;
    [Min(1)] public int maxAmount = 3;

    [Tooltip("Relative weight when picking outputs. Higher = more likely.")]
    [Range(0f, 1f)] public float weight = 1f;

    [Tooltip("Chance this output is produced each spawn cycle (0-1).")]
    [Range(0f, 1f)] public float chance = 1f;

    [Tooltip("If true, adds to an existing stack of this resource instead of creating a new entry.")]
    public bool addToExistingStack = true;
}

[System.Serializable]
public class ResourceSpawnerConditionSettings
{
    [Header("Environment / Tile Filter")]
    [Tooltip("Leave empty to allow all environment types.")]
    public List<EnvironmentType> requiredEnvironmentTypes = new();

    [Tooltip("Leave empty to allow all tile types.")]
    public List<EnvironmentTileType> requiredTileTypes = new();

    [Header("Season Filter")]
    [Tooltip("Leave empty to allow all seasons. Match by seasonID string.")]
    public List<string> requiredSeasonIDs = new();

    [Header("Tile State Requirements")]
    public bool requiresHasBeenIgnited;
    public bool requiresIsCurrentlyWet;
    public bool requiresWasRecentlyFlooded;
    public bool requiresHasCarcass;
    public bool requiresHasVolcanicAsh;

    [Tooltip("If true, this spawner stops when the tile is flagged IsCurrentlyDry (drought / dry season).")]
    public bool requiresNotDry;
}

[System.Serializable]
public class SpawnerTriggerEntry
{
    [Tooltip("The spawner to add to this tile when the parent spawner successfully fires.")]
    public ResourceSpawnerDefinition triggeredSpawner;

    [Tooltip("Chance (0-1) this trigger fires each time the parent spawner completes an output cycle.")]
    [Range(0f, 1f)] public float triggerChance = 1f;

    [Tooltip("If > 0, the triggered spawner is temporary and expires after this many turns. 0 = permanent.")]
    [Min(0)] public int lifetimeTurns = 0;

    [Tooltip("If true, skips adding the triggered spawner if it is already active on this tile.")]
    public bool onlyIfNotAlreadyPresent = true;
}

[CreateAssetMenu(menuName = "Resources/ResourceSpawnerDefinition", fileName = "NewResourceSpawner")]
public class ResourceSpawnerDefinition : ScriptableObject
{
    [Header("Identity")]
    public string spawnerID;
    public string displayName;
    public SpawnerCategory category;

    [Header("Outputs")]
    public List<ResourceSpawnerOutput> outputs = new();

    [Header("Spawn Chance and Timing")]
    [Tooltip("Probability per spawn cycle that this spawner produces anything.")]
    [Range(0f, 1f)]
    public float baseSpawnChance = 0.8f;

    [Tooltip("How many turns between spawn attempts.")]
    [Min(1)]
    public int spawnIntervalTurns = 1;

    [Header("Lifetime / Uses")]
    [Tooltip("If true, this spawner never expires on its own.")]
    public bool isPermanent = true;

    [Tooltip("If true, this spawner can expire via maxUses or lifetimeTurns.")]
    public bool canExpire = false;

    [Tooltip("Maximum spawn cycles before expiry. 0 = unlimited.")]
    [Min(0)] public int maxUses = 0;

    [Tooltip("Maximum turns this spawner stays active. 0 = unlimited.")]
    [Min(0)] public int lifetimeTurns = 0;

    [Header("Climate Modifiers")]
    [Tooltip("Optional climate curves that scale baseSpawnChance by temperature and humidity.")]
    public ResourceSpawnerClimateSettings climate = new();

    [Header("Conditions")]
    public ResourceSpawnerConditionSettings conditions = new();

    [Header("Triggered Spawners")]
    [Tooltip("When this spawner fires an output cycle, these spawners are added to the tile. " +
             "Example: a Dung spawner that triggers a Mushroom spawner nearby.")]
    public List<SpawnerTriggerEntry> triggeredSpawners = new();

    [Header("Guaranteed Spawn")]
    [Tooltip("If true, this spawner always fires when its interval and conditions are met — no random roll against baseSpawnChance. The climate multiplier still applies, so drought (low humidity → multiplier 0) can stop it. Use for resources that must always be present, like water on ponds.")]
    public bool isGuaranteed = false;

    [Header("Debug / Design Notes")]
    [TextArea(2, 5)]
    [Tooltip("Designer notes: intended climate behaviour, trigger source, and purpose.")]
    public string debugNotes;
}
