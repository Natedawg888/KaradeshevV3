using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class AnimalSimulation
{
    // ---- Debug toggles (controller can set these) ----
    public bool DebugHumanTargeting { get; set; } = false;
    public bool DebugHumanStepping { get; set; } = false;

    public struct HumanUnitGroupInfo
    {
        public string groupId;
        public TileCoord tile;
        public int unitCount;

        // If false -> animals must attack from an adjacent env tile (never stand on the tile).
        // If true  -> animals must be on the same tile to attack (UNLESS the unit sits on a building tile).
        public bool isOnEnvironmentTile;
    }

    private readonly HashSet<TileCoord> _playerBuildingTiles = new();
    private readonly Dictionary<string, HumanUnitGroupInfo> _humanUnitGroups = new();

    public bool HumanHuntersAvoidPlayerBuildings { get; set; } = true;

    // ✅ NON-ALLOC neighbour buffer for human raid logic
    private readonly List<TileCoord> _humanRaidNeighboursBuffer = new List<TileCoord>(32);

    public void SetPlayerBuildingTiles(IEnumerable<TileCoord> tiles)
    {
        _playerBuildingTiles.Clear();
        if (tiles == null) return;

        foreach (var t in tiles)
            _playerBuildingTiles.Add(t);
    }

    public void SetHumanUnitGroups(IEnumerable<HumanUnitGroupInfo> groups)
    {
        _humanUnitGroups.Clear();
        if (groups == null) return;

        foreach (var g in groups)
        {
            if (string.IsNullOrEmpty(g.groupId)) continue;
            if (g.unitCount <= 0) continue;
            _humanUnitGroups[g.groupId] = g;
        }
    }

    private bool IsPlayerBuildingTile(TileCoord tile) => _playerBuildingTiles.Contains(tile);

    private bool ShouldAvoidHumans(AnimalDefinition species)
    {
        if (species == null) return false;
        if (species.avoidsHumans) return true;
        if (HumanHuntersAvoidPlayerBuildings && species.huntsHumans) return true;
        return false;
    }

    private static int Manhattan(TileCoord a, TileCoord b)
        => Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);

    private static bool IsAdjacent(TileCoord a, TileCoord b)
        => Manhattan(a, b) == 1;

    private void ClearHumanRaidTarget(ref AnimalGroupState predator)
    {
        predator.isRaidingPlayerTile = false;
        predator.raidTargetTile = default;
    }

    private void ClearHumanUnitTarget(ref AnimalGroupState predator)
    {
        predator.isHuntingHumanUnits = false;
        predator.huntingHumanUnitGroupId = null;
    }

    private bool TryAcquirePlayerBuildingTarget(TileCoord from, int range, out TileCoord best)
    {
        best = default;
        bool found = false;
        int bestDist = int.MaxValue;

        foreach (var t in _playerBuildingTiles)
        {
            int dist = Manhattan(from, t);
            if (dist == 0 || dist > range) continue;

            if (!found || dist < bestDist)
            {
                found = true;
                best = t;
                bestDist = dist;
            }
        }

        return found;
    }

    // ✅ Scan ALL human groups (not env neighbours), because units may be on non-env tiles.
    private bool TryAcquireHumanUnitTarget(TileCoord from, int range, out HumanUnitGroupInfo best)
    {
        best = default;
        bool found = false;
        int bestDist = int.MaxValue;
        int bestCount = -1;

        foreach (var kvp in _humanUnitGroups)
        {
            var info = kvp.Value;
            if (info.unitCount <= 0) continue;

            int dist = Manhattan(from, info.tile);
            if (dist == 0 || dist > range) continue;

            if (!found || dist < bestDist || (dist == bestDist && info.unitCount > bestCount))
            {
                found = true;
                best = info;
                bestDist = dist;
                bestCount = info.unitCount;
            }
        }

        return found;
    }

    // ✅ Best unit group ON a specific tile (used for "clear units on building tile first")
    private bool TryGetBestHumanUnitOnTile(TileCoord tile, out HumanUnitGroupInfo best)
    {
        best = default;
        bool found = false;
        int bestCount = -1;

        foreach (var kvp in _humanUnitGroups)
        {
            var info = kvp.Value;
            if (info.unitCount <= 0) continue;
            if (!info.tile.Equals(tile)) continue;

            if (!found || info.unitCount > bestCount)
            {
                found = true;
                best = info;
                bestCount = info.unitCount;
            }
        }

        return found;
    }

    // Pick a REAL environment tile adjacent to "target".
    private bool TryGetBestApproachTileAdjacentTo(TileCoord target, TileCoord from, out TileCoord approach)
    {
        approach = default;
        bool found = false;
        int bestDist = int.MaxValue;

        _env.GetNeighbourTilesNonAlloc(target, 1, _humanRaidNeighboursBuffer, includeCenter: false);

        for (int i = 0; i < _humanRaidNeighboursBuffer.Count; i++)
        {
            var n = _humanRaidNeighboursBuffer[i];

            // Never stand on building coords for "approach"
            if (IsPlayerBuildingTile(n))
                continue;

            int dist = Manhattan(from, n);
            if (!found || dist < bestDist)
            {
                found = true;
                bestDist = dist;
                approach = n;
            }
        }

        return found;
    }

    private TileCoord StepTowards(TileCoord from, TileCoord goal, bool allowStepOnGoalEvenIfBuilding)
    {
        if (from.Equals(goal))
            return from;

        TileCoord best = from;
        int bestDist = Manhattan(from, goal);

        _env.GetNeighbourTilesNonAlloc(from, 1, _humanRaidNeighboursBuffer, includeCenter: false);

        for (int i = 0; i < _humanRaidNeighboursBuffer.Count; i++)
        {
            var n = _humanRaidNeighboursBuffer[i];

            // Block building tiles unless it's explicitly the goal and allowed
            if (IsPlayerBuildingTile(n) && !(allowStepOnGoalEvenIfBuilding && n.Equals(goal)))
                continue;

            int d = Manhattan(n, goal);
            if (d < bestDist)
            {
                best = n;
                bestDist = d;
            }
        }

        return best;
    }

    // ---- UNIT HUNTING ----
    private bool HandleHumanUnitHunting(ref AnimalGroupState predator, float hungerPct, float thirstPct)
    {
        var species = predator.species;
        if (species == null || !species.huntsHumans)
            return false;

        if (HumanHuntersAvoidPlayerBuildings || species.avoidsHumans)
        {
            ClearHumanUnitTarget(ref predator);
            return false;
        }

        int range = Math.Max(1, species.huntingRangeTiles);

        float huntingThreshold = (species.huntingHungerThreshold > 0f) ? species.huntingHungerThreshold : 0.6f;
        bool hungerTriggered = hungerPct >= huntingThreshold;

        bool needsOk = hungerPct < 0.5f && thirstPct < 0.5f;
        bool aggressionTriggered = needsOk && species.aggression >= 0.7f;

        if (!hungerTriggered && !aggressionTriggered)
        {
            ClearHumanUnitTarget(ref predator);
            return false;
        }

        // Continue chase
        if (predator.isHuntingHumanUnits && !string.IsNullOrEmpty(predator.huntingHumanUnitGroupId))
        {
            if (!_humanUnitGroups.TryGetValue(predator.huntingHumanUnitGroupId, out var target) || target.unitCount <= 0)
            {
                ClearHumanUnitTarget(ref predator);
                return false;
            }

            bool targetOnBuilding = IsPlayerBuildingTile(target.tile);
            bool sameTileRequired = target.isOnEnvironmentTile && !targetOnBuilding;

            bool canAttackNow = sameTileRequired
                ? predator.tile.Equals(target.tile)
                : IsAdjacent(predator.tile, target.tile);

            if (canAttackNow)
            {
                predator.isHunting = true;
                predator.lastAction = AnimalActionType.AttackPlayer;
                OnGroupAttackedPlayerUnitGroup?.Invoke(predator.id, target.groupId, target.tile);
                return true;
            }

            if (sameTileRequired)
            {
                var next = StepTowards(predator.tile, target.tile, allowStepOnGoalEvenIfBuilding: true);
                predator.tile = next;
                predator.isHunting = true;
                predator.lastAction = AnimalActionType.Move;
                return true;
            }
            else
            {
                if (!TryGetBestApproachTileAdjacentTo(target.tile, predator.tile, out var approach))
                {
                    ClearHumanUnitTarget(ref predator);
                    return false;
                }

                var next = StepTowards(predator.tile, approach, allowStepOnGoalEvenIfBuilding: false);
                predator.tile = next;
                predator.isHunting = true;
                predator.lastAction = AnimalActionType.Move;
                return true;
            }
        }

        // Acquire new target
        if (TryAcquireHumanUnitTarget(predator.tile, range, out var newTarget))
        {
            predator.isHuntingHumanUnits = true;
            predator.huntingHumanUnitGroupId = newTarget.groupId;
            predator.isHunting = true;
            predator.lastAction = AnimalActionType.Move;
            return true;
        }

        return false;
    }

    // ---- BUILDING RAIDING ----
    private bool HandleBuildingRaid(ref AnimalGroupState predator, float hungerPct, float thirstPct)
    {
        var species = predator.species;
        if (species == null || !species.huntsHumans)
            return false;

        if (HumanHuntersAvoidPlayerBuildings || species.avoidsHumans)
        {
            ClearHumanRaidTarget(ref predator);
            return false;
        }

        if (_playerBuildingTiles.Count == 0)
        {
            ClearHumanRaidTarget(ref predator);
            return false;
        }

        int range = Math.Max(1, species.huntingRangeTiles);

        float huntingThreshold = (species.huntingHungerThreshold > 0f) ? species.huntingHungerThreshold : 0.6f;
        bool hungerTriggered = hungerPct >= huntingThreshold;

        bool needsOk = hungerPct < 0.5f && thirstPct < 0.5f;
        bool aggressionTriggered = needsOk && species.aggression >= 0.7f;

        if (!hungerTriggered && !aggressionTriggered)
        {
            ClearHumanRaidTarget(ref predator);
            return false;
        }

        // Continue existing raid
        if (predator.isRaidingPlayerTile)
        {
            if (!IsPlayerBuildingTile(predator.raidTargetTile))
            {
                ClearHumanRaidTarget(ref predator);
                return false;
            }

            if (TryGetBestHumanUnitOnTile(predator.raidTargetTile, out var unitOnBuilding))
            {
                ClearHumanRaidTarget(ref predator);

                predator.isHuntingHumanUnits = true;
                predator.huntingHumanUnitGroupId = unitOnBuilding.groupId;
                predator.isHunting = true;

                return HandleHumanUnitHunting(ref predator, hungerPct, thirstPct);
            }

            if (IsAdjacent(predator.tile, predator.raidTargetTile))
            {
                predator.isHunting = true;
                predator.lastAction = AnimalActionType.AttackPlayerTile;
                OnGroupAttackedPlayerTile?.Invoke(predator.id, predator.raidTargetTile);
                return true;
            }

            if (!TryGetBestApproachTileAdjacentTo(predator.raidTargetTile, predator.tile, out var approach))
            {
                ClearHumanRaidTarget(ref predator);
                return false;
            }

            var next = StepTowards(predator.tile, approach, allowStepOnGoalEvenIfBuilding: false);
            predator.tile = next;
            predator.isHunting = true;
            predator.lastAction = AnimalActionType.Move;
            return true;
        }

        // Acquire new building target
        if (TryAcquirePlayerBuildingTarget(predator.tile, range, out var target))
        {
            // If there are units on this building tile, hunt them FIRST
            if (TryGetBestHumanUnitOnTile(target, out var unitOnBuilding))
            {
                ClearHumanRaidTarget(ref predator);

                predator.isHuntingHumanUnits = true;
                predator.huntingHumanUnitGroupId = unitOnBuilding.groupId;
                predator.isHunting = true;

                return HandleHumanUnitHunting(ref predator, hungerPct, thirstPct);
            }

            predator.isRaidingPlayerTile = true;
            predator.raidTargetTile = target;
            predator.isHunting = true;

            if (IsAdjacent(predator.tile, target))
            {
                predator.lastAction = AnimalActionType.AttackPlayerTile;
                OnGroupAttackedPlayerTile?.Invoke(predator.id, target);
            }
            else
            {
                predator.lastAction = AnimalActionType.Move;
            }

            return true;
        }

        return false;
    }

    // ✅ Entry point called from Decision.cs
    private bool HandleHumanRaiding(ref AnimalGroupState predator, float hungerPct, float thirstPct)
    {
        if (HandleHumanUnitHunting(ref predator, hungerPct, thirstPct))
            return true;

        if (HandleBuildingRaid(ref predator, hungerPct, thirstPct))
            return true;

        return false;
    }
}