using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class AnimalSimulation
{
    public AnimalSimulationSaveData SaveState()
    {
        if (!_cachedSaveStateValid || _cachedSaveState == null)
            RebuildCachedSaveState();

        return _cachedSaveState ?? new AnimalSimulationSaveData
        {
            nextGroupId = _nextGroupId
        };
    }

    public void ClearAllGroups(bool raiseRemovedEvents = true)
    {
        if (raiseRemovedEvents)
        {
            List<int> ids = _groups.Keys.ToList();
            for (int i = 0; i < ids.Count; i++)
            {
                int id = ids[i];
                if (_groups.TryGetValue(id, out AnimalGroupState group) && group != null)
                    RemoveGroup(id, group.tile, "LOAD-CLEAR", "Clearing existing animal groups before save load.");
            }
        }
        else
        {
            _groups.Clear();
            _tileIndex.Clear();
            _liveGroupIdsBySpecies.Clear();
            _liveSpeciesByGroupId.Clear();
        }

        _turnIncomingAnimalsByTile.Clear();
        _turnIncomingGroupsByTile.Clear();
        _pendingSpawns.Clear();
    }

    public void LoadState(AnimalSimulationSaveData data)
    {
        ClearAllGroups(true);

        if (data == null)
        {
            _nextGroupId = 1;
            RebuildCachedSaveState();
            return;
        }

        _nextGroupId = Mathf.Max(1, data.nextGroupId);

        if (data.groups != null)
        {
            for (int i = 0; i < data.groups.Count; i++)
            {
                AnimalGroupSaveData saved = data.groups[i];
                AnimalGroupState group = BuildGroupFromSaveData(saved);
                if (group == null)
                    continue;

                _groups[group.id] = group;
                AddToTileIndex(group.id, group.tile);
                RegisterGroupInSpeciesRegistry(group.id, group.species);

                if (_nextGroupId <= group.id)
                    _nextGroupId = group.id + 1;

                OnGroupCreated?.Invoke(group);
            }
        }

        if (data.pendingSpawnTemplates != null)
        {
            for (int i = 0; i < data.pendingSpawnTemplates.Count; i++)
            {
                AnimalGroupSaveData saved = data.pendingSpawnTemplates[i];
                AnimalGroupState template = BuildGroupFromSaveData(saved);
                if (template == null)
                    continue;

                template.id = -1;
                template.EnsureHealthValid();

                _pendingSpawns.Enqueue(new PendingSpawn
                {
                    template = template
                });
            }
        }

        RebuildCachedSaveState();
    }

    private AnimalGroupSaveData BuildGroupSaveData(AnimalGroupState g)
    {
        return new AnimalGroupSaveData
        {
            id = g.id,
            speciesAssetName = g.species != null ? g.species.name : string.Empty,

            size = g.size,
            ageInTurns = g.ageInTurns,
            currentHealth = g.currentHealth,

            hunger = g.hunger,
            thirst = g.thirst,

            tile = g.tile,

            lastAction = g.lastAction,
            nextUpdateTurn = g.nextUpdateTurn,

            isLeader = g.isLeader,
            herdId = g.herdId,
            leaderGroupId = g.leaderGroupId,

            isHunting = g.isHunting,
            huntingTargetGroupId = g.huntingTargetGroupId,
            isTargetedByPredator = g.isTargetedByPredator,
            huntingEscapeCount = g.huntingEscapeCount,

            nextReproductionTurn = g.nextReproductionTurn,
            isOnReproductionCooldown = g.isOnReproductionCooldown,

            isInPredatorConflict = g.isInPredatorConflict,
            predatorConflictTargetGroupId = g.predatorConflictTargetGroupId,

            targetedByPredatorGroupId = g.targetedByPredatorGroupId,

            isFleeingFromThreat = g.isFleeingFromThreat,
            fleeFromPredatorGroupId = g.fleeFromPredatorGroupId,
            fleeUntilDistanceTiles = g.fleeUntilDistanceTiles,
            fleeThreatLastKnownTile = g.fleeThreatLastKnownTile,
            fleeStepsRemaining = g.fleeStepsRemaining,

            hasWaterSearchMemory = g.hasWaterSearchMemory,
            lastWaterSearchPreviousTile = g.lastWaterSearchPreviousTile,
            secondLastWaterSearchPreviousTile = g.secondLastWaterSearchPreviousTile,
            waterSearchBacktrackAvoidanceTurns = g.waterSearchBacktrackAvoidanceTurns,

            isRaidingPlayerTile = g.isRaidingPlayerTile,
            raidTargetTile = g.raidTargetTile,

            isHuntingHumanUnits = g.isHuntingHumanUnits,
            huntingHumanUnitGroupId = g.huntingHumanUnitGroupId,

            resolvedHealthPerAnimal = g.resolvedHealthPerAnimal,
            resolvedAggression = g.resolvedAggression,
            resolvedFlightiness = g.resolvedFlightiness,
            resolvedHerding = g.resolvedHerding,
            resolvedStrength = g.resolvedStrength,
            resolvedDefense = g.resolvedDefense,
            resolvedSpeed = g.resolvedSpeed,
            resolvedSense = g.resolvedSense,
            resolvedStealth = g.resolvedStealth,
            resolvedBreedingFemaleFraction = g.resolvedBreedingFemaleFraction,

            isTargetedByHumanUnits = g.isTargetedByHumanUnits
        };
    }

    private AnimalGroupState BuildGroupFromSaveData(AnimalGroupSaveData saved)
    {
        if (saved == null || string.IsNullOrWhiteSpace(saved.speciesAssetName))
            return null;

        AnimalDefinition species = ResolveSpecies(saved.speciesAssetName);
        if (species == null)
        {
            //Debug.LogWarning($"[AnimalSimulation] Could not resolve species '{saved.speciesAssetName}' while loading animal state.");
            return null;
        }

        AnimalGroupState group = new AnimalGroupState
        {
            id = saved.id,
            species = species,

            size = saved.size,
            ageInTurns = saved.ageInTurns,
            currentHealth = saved.currentHealth,

            hunger = saved.hunger,
            thirst = saved.thirst,

            tile = saved.tile,

            lastAction = saved.lastAction,
            nextUpdateTurn = saved.nextUpdateTurn,

            isLeader = saved.isLeader,
            herdId = saved.herdId,
            leaderGroupId = saved.leaderGroupId,

            isHunting = saved.isHunting,
            huntingTargetGroupId = saved.huntingTargetGroupId,
            isTargetedByPredator = saved.isTargetedByPredator,
            huntingEscapeCount = saved.huntingEscapeCount,

            nextReproductionTurn = saved.nextReproductionTurn,
            isOnReproductionCooldown = saved.isOnReproductionCooldown,

            isInPredatorConflict = saved.isInPredatorConflict,
            predatorConflictTargetGroupId = saved.predatorConflictTargetGroupId,

            targetedByPredatorGroupId = saved.targetedByPredatorGroupId,

            isFleeingFromThreat = saved.isFleeingFromThreat,
            fleeFromPredatorGroupId = saved.fleeFromPredatorGroupId,
            fleeUntilDistanceTiles = saved.fleeUntilDistanceTiles,
            fleeThreatLastKnownTile = saved.fleeThreatLastKnownTile,
            fleeStepsRemaining = saved.fleeStepsRemaining,

            hasWaterSearchMemory = saved.hasWaterSearchMemory,
            lastWaterSearchPreviousTile = saved.lastWaterSearchPreviousTile,
            secondLastWaterSearchPreviousTile = saved.secondLastWaterSearchPreviousTile,
            waterSearchBacktrackAvoidanceTurns = saved.waterSearchBacktrackAvoidanceTurns,

            isRaidingPlayerTile = saved.isRaidingPlayerTile,
            raidTargetTile = saved.raidTargetTile,

            isHuntingHumanUnits = saved.isHuntingHumanUnits,
            huntingHumanUnitGroupId = saved.huntingHumanUnitGroupId,

            resolvedHealthPerAnimal = saved.resolvedHealthPerAnimal,
            resolvedAggression = saved.resolvedAggression,
            resolvedFlightiness = saved.resolvedFlightiness,
            resolvedHerding = saved.resolvedHerding,
            resolvedStrength = saved.resolvedStrength,
            resolvedDefense = saved.resolvedDefense,
            resolvedSpeed = saved.resolvedSpeed,
            resolvedSense = saved.resolvedSense,
            resolvedStealth = saved.resolvedStealth,
            resolvedBreedingFemaleFraction = saved.resolvedBreedingFemaleFraction,

            isTargetedByHumanUnits = saved.isTargetedByHumanUnits
        };

        group.EnsureHealthValid();
        return group;
    }

    private static Dictionary<string, AnimalDefinition> _speciesByAssetName;

    private AnimalDefinition ResolveSpecies(string assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName))
            return null;

        if (_speciesByAssetName == null)
        {
            _speciesByAssetName = new Dictionary<string, AnimalDefinition>(System.StringComparer.Ordinal);
            AnimalDefinition[] all = Resources.LoadAll<AnimalDefinition>(string.Empty);

            for (int i = 0; i < all.Length; i++)
            {
                AnimalDefinition def = all[i];
                if (def == null || string.IsNullOrWhiteSpace(def.name))
                    continue;

                if (!_speciesByAssetName.ContainsKey(def.name))
                    _speciesByAssetName.Add(def.name, def);
            }
        }

        _speciesByAssetName.TryGetValue(assetName.Trim(), out AnimalDefinition result);
        return result;
    }

    public event Action OnSimulationStateChanged;

    private void NotifySimulationStateChanged()
    {
        InvalidateSaveCache();
        OnSimulationStateChanged?.Invoke();
    }
}
