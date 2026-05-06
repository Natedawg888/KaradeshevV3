using System;
using System.Collections.Generic;
using UnityEngine;

public partial class AnimalSimulation
{
    private readonly IEnvironmentDataSource _env;

    private readonly Dictionary<int, AnimalGroupState> _groups =
        new Dictionary<int, AnimalGroupState>();

    private readonly Dictionary<TileCoord, List<int>> _tileIndex =
        new Dictionary<TileCoord, List<int>>();

    private int _nextGroupId = 1;
    private readonly System.Random _rng = new System.Random();

    private float _worldSpeciesGroupCapMultiplier = 1f;

    // Events for visuals / external systems
    public event Action<AnimalGroupState> OnGroupCreated;
    public event Action<AnimalGroupState> OnGroupUpdated;
    public event Action<int> OnGroupRemoved;

    // For predator attacks
    public event Action<int, TileCoord> OnGroupAttackedPlayerTile;
    public event Action<int, string, TileCoord> OnGroupAttackedPlayerUnitGroup;

    // Global group cap (default: unlimited)
    private int _maxTotalGroups = int.MaxValue;
    public int MaxTotalGroups
    {
        get => _maxTotalGroups;
        set => _maxTotalGroups = Math.Max(1, value);
    }

    public int TotalGroupCount => _groups.Count;

    // Pending spawns from reproduction
    private struct PendingSpawn
    {
        public AnimalGroupState template;
    }

    private struct PendingGroupSplit
    {
        public AnimalGroupState template;
    }

    private readonly Queue<PendingSpawn> _pendingSpawns = new Queue<PendingSpawn>();
    private readonly Queue<PendingGroupSplit> _pendingGroupSplits = new();

    private readonly HashSet<AnimalDefinition> _speciesCapSuppressedThisTurn = new();

    public bool HasPendingSpawns => _pendingSpawns.Count > 0;

    private bool HasReachedGroupCap()
    {
        // Cap applies only to actually spawned groups
        return _groups.Count >= _maxTotalGroups;
    }

    private void EnqueueSpawn(AnimalDefinition species, TileCoord tile, int size)
    {
        if (species == null || size <= 0)
            return;

        long safetyLimit = (long)_maxTotalGroups * 4L;
        if (safetyLimit > 0 && _pendingSpawns.Count >= safetyLimit)
            return;

        var group = new AnimalGroupState
        {
            id = -1,
            species = species,
            size = size,
            ageInTurns = 0,
            currentHealth = Mathf.Max(1, size * Mathf.Max(1, species.healthPerAnimal)),
            hunger = 0f,
            thirst = 0f,
            tile = tile,
            lastAction = AnimalActionType.Idle,
            nextUpdateTurn = 0,

            isLeader = false,
            herdId = 0,
            leaderGroupId = 0,

            isHunting = false,
            huntingTargetGroupId = -1,
            isTargetedByPredator = false,
            targetedByPredatorGroupId = -1,
            huntingEscapeCount = 0,

            nextReproductionTurn = 0,
            isOnReproductionCooldown = false,

            isInPredatorConflict = false,
            predatorConflictTargetGroupId = -1,

            isFleeingFromThreat = false,
            fleeFromPredatorGroupId = -1,
            fleeUntilDistanceTiles = 0,
            fleeThreatLastKnownTile = tile,
            fleeStepsRemaining = 0,

            hasWaterSearchMemory = false,
            lastWaterSearchPreviousTile = tile,
            secondLastWaterSearchPreviousTile = tile,
            waterSearchBacktrackAvoidanceTurns = 0,

            isRaidingPlayerTile = false,
            raidTargetTile = tile,

            isHuntingHumanUnits = false,
            huntingHumanUnitGroupId = null
        };

        group.EnsureHealthValid();

        _pendingSpawns.Enqueue(new PendingSpawn
        {
            template = group
        });
    }

    public void ProcessPendingSpawns(int maxToProcess)
    {
        if (maxToProcess <= 0)
            return;

        int processed = 0;

        while (_pendingSpawns.Count > 0 && processed < maxToProcess)
        {
            if (HasReachedGroupCap())
                break;

            var pending = _pendingSpawns.Dequeue();
            var group = pending.template;

            if (group.species == null || group.size <= 0)
                continue;

            group.id = _nextGroupId++;
            group.EnsureHealthValid();

            _groups[group.id] = group;
            AddToTileIndex(group.id, group.tile);

            if (!IsSpeciesCapSuppressedThisTurn(group.species))
                EnforceSpeciesGroupCapFor(group.species);

            OnGroupCreated?.Invoke(group);

            processed++;
        }
    }

    private void SuppressSpeciesCapForThisTurn(AnimalDefinition species)
    {
        if (species != null)
            _speciesCapSuppressedThisTurn.Add(species);
    }

    private bool IsSpeciesCapSuppressedThisTurn(AnimalDefinition species)
    {
        return species != null && _speciesCapSuppressedThisTurn.Contains(species);
    }

    // Helper to hard-reset reproduction backlog
    public void ClearPendingSpawns()
    {
        _pendingSpawns.Clear();
    }

    public AnimalSimulation(IEnvironmentDataSource envDataSource)
    {
        _env = envDataSource ?? throw new ArgumentNullException(nameof(envDataSource));
    }

    // Shared helper
    private static float Clamp(float value, float min, float max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    public int EjectGroupsFromDestroyedTile(TileCoord destroyedTile)
    {
        if (!_tileIndex.TryGetValue(destroyedTile, out var list) || list == null || list.Count == 0)
            return 0;

        // Copy first so we do not modify the same list while iterating.
        List<int> groupIds = new List<int>(list);
        int movedCount = 0;

        for (int i = 0; i < groupIds.Count; i++)
        {
            int groupId = groupIds[i];

            if (!_groups.TryGetValue(groupId, out var group))
                continue;

            if (!group.isAlive || group.size <= 0)
                continue;

            if (TryFindEjectionTile(group, destroyedTile, out TileCoord safeTile))
            {
                TileCoord oldTile = group.tile;
                group.tile = safeTile;
                group.lastAction = AnimalActionType.Move;

                // Clear invalid target states caused by forced displacement
                group.isHunting = false;
                group.huntingTargetGroupId = -1;
                group.isTargetedByPredator = false;

                group.isInPredatorConflict = false;
                group.predatorConflictTargetGroupId = -1;

                group.isRaidingPlayerTile = false;
                group.isHuntingHumanUnits = false;

                _groups[groupId] = group;
                MoveGroupInTileIndex(groupId, oldTile, safeTile);
                OnGroupUpdated?.Invoke(group);

                movedCount++;
            }
            else
            {
                CleanupTargetsOnDeath(ref group);
                RemoveGroup(groupId, destroyedTile);
            }
        }

        return movedCount;
    }

    private bool TryFindEjectionTile(AnimalGroupState group, TileCoord blockedTile, out TileCoord bestTile)
    {
        bestTile = blockedTile;

        var species = group.species;
        if (species == null || _env == null)
            return false;

        bool found = false;
        float bestScore = float.NegativeInfinity;

        var neighbours = GetNeighbourTilesCached(blockedTile, 1);
        for (int i = 0; i < neighbours.Count; i++)
        {
            TileCoord coord = neighbours[i];

            if (coord.Equals(blockedTile))
                continue;

            // Do not eject onto another player building tile
            if (IsPlayerBuildingTile(coord))
                continue;

            // Species that avoid humans should not be forced onto human tiles
            if (ShouldAvoidHumans(species) && IsPlayerBuildingTile(coord))
                continue;

            TileEnvironmentData data = _env.GetTileData(coord);

            // Reject strongly unsuitable tiles
            if (!IsTileSuitableForForcedRelocation(species, data))
                continue;

            float habitatScore = GetHabitatSuitability(species, data);

            // Slight bonus for water/food access if needs are high
            float hungerPct = species.maxHunger > 0f ? group.hunger / species.maxHunger : 0f;
            float thirstPct = species.maxThirst > 0f ? group.thirst / species.maxThirst : 0f;

            float waterBonus = data.hasWater ? thirstPct * 2f : 0f;
            float foodBonus = data.plantFood > 0f ? hungerPct * 1.25f : 0f;

            // Mild crowding penalty so they spread out a bit when possible
            int occupantCount = _tileIndex.TryGetValue(coord, out var occupants) && occupants != null
                ? occupants.Count
                : 0;

            float crowdPenalty = occupantCount * 0.2f;

            float score = habitatScore
                        + waterBonus
                        + foodBonus
                        - data.dangerLevel * 0.5f
                        - crowdPenalty;

            if (!found || score > bestScore)
            {
                found = true;
                bestScore = score;
                bestTile = coord;
            }
        }

        return found;
    }

    private bool IsTileSuitableForForcedRelocation(AnimalDefinition species, TileEnvironmentData data)
    {
        if (species == null)
            return false;

        bool matchesPreferredEnvironment =
            species.preferredEnvironments == null ||
            species.preferredEnvironments.Length == 0 ||
            Array.IndexOf(species.preferredEnvironments, data.environmentType) >= 0;

        bool matchesPreferredTileType =
            species.preferredTileTypes == null ||
            species.preferredTileTypes.Length == 0 ||
            Array.IndexOf(species.preferredTileTypes, data.tileType) >= 0;

        bool isAvoidedEnvironment =
            species.avoidedEnvironments != null &&
            species.avoidedEnvironments.Length > 0 &&
            Array.IndexOf(species.avoidedEnvironments, data.environmentType) >= 0;

        bool isAvoidedTileType =
            species.avoidedTileTypes != null &&
            species.avoidedTileTypes.Length > 0 &&
            Array.IndexOf(species.avoidedTileTypes, data.tileType) >= 0;

        if (isAvoidedEnvironment || isAvoidedTileType)
            return false;

        return matchesPreferredEnvironment && matchesPreferredTileType;
    }

    private void RollGroupCoreStats(ref AnimalGroupState group)
    {
        var def = group.species;
        if (def == null)
            return;

        group.resolvedHealthPerAnimal = RollIntPlusMinus(def.healthPerAnimal, def.healthPerAnimalVariation);

        group.resolvedAggression = RollFloatPlusMinus(def.aggression, def.aggressionVariation);
        group.resolvedFlightiness = RollFloatPlusMinus(def.flightiness, def.flightinessVariation);
        group.resolvedHerding = RollFloatPlusMinus(def.herding, def.herdingVariation);
        group.resolvedStrength = RollFloatPlusMinus(def.strength, def.strengthVariation);
        group.resolvedDefense = RollFloatPlusMinus(def.defense, def.defenseVariation);
        group.resolvedSpeed = RollFloatPlusMinus(def.speed, def.speedVariation);
        group.resolvedSense = RollFloatPlusMinus(def.sense, def.senseVariation);
        group.resolvedStealth = RollFloatPlusMinus(def.stealth, def.stealthVariation);

        group.resolvedBreedingFemaleFraction =
            RollFloatPlusMinus(def.breedingFemaleFraction, def.breedingFemaleFractionVariation);
    }

    private int RollIntPlusMinus(int baseValue, int variation)
    {
        if (variation <= 0)
            return Mathf.Max(1, baseValue);

        return Mathf.Max(1, baseValue + UnityEngine.Random.Range(-variation, variation + 1));
    }

    private float RollFloatPlusMinus(float baseValue, float variation)
    {
        if (variation <= 0f)
            return Mathf.Clamp01(baseValue);

        return Mathf.Clamp01(baseValue + UnityEngine.Random.Range(-variation, variation));
    }

    public void SetPlayerTargetedOnAnimal(int animalGroupId, bool targeted)
    {
        if (animalGroupId < 0)
            return;

#if UNITY_2023_1_OR_NEWER
    var markers = UnityEngine.Object.FindObjectsByType<AnimalGroupMarkerView>(
        FindObjectsInactive.Include,
        FindObjectsSortMode.None);
#else
        var markers = UnityEngine.Object.FindObjectsOfType<AnimalGroupMarkerView>(true);
#endif

        for (int i = 0; i < markers.Length; i++)
        {
            var marker = markers[i];
            if (marker == null)
                continue;

            if (marker.GroupId != animalGroupId)
                continue;

            marker.SetPlayerTargeted(targeted);
            return;
        }
    }
}