using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AnimalGroupDebugSnapshot
{
    public int id;
    public string speciesName;
    public int size;
    public int ageInTurns;
    public string dietName;

    public int currentHealth;
    public int maxHealth;

    public float hunger;
    public float thirst;

    public int tileX;
    public int tileY;

    public AnimalActionType lastAction;
    public int nextUpdateTurn;

    public bool isAlive;

    public bool isLeader;
    public int herdId;
    public int leaderGroupId;

    public bool isHunting;
    public int huntingTargetGroupId;
    public bool isTargetedByPredator;
    public int targetedByPredatorGroupId;
    public int huntingEscapeCount;

    public bool isOnReproductionCooldown;
    public int nextReproductionTurn;

    public bool isInPredatorConflict;
    public int predatorConflictTargetGroupId;

    public bool isFleeingFromThreat;
    public int fleeFromPredatorGroupId;
    public int fleeUntilDistanceTiles;
    public int fleeStepsRemaining;
    public int fleeThreatTileX;
    public int fleeThreatTileY;

    public bool isRaidingPlayerTile;
    public int raidTargetTileX;
    public int raidTargetTileY;

    public bool isHuntingHumanUnits;
    public string huntingHumanUnitGroupId;
}

public partial class AnimalSimulation
{
    public List<AnimalGroupDebugSnapshot> GetDebugSnapshots()
    {
        var results = new List<AnimalGroupDebugSnapshot>(_groups.Count);

        foreach (var kvp in _groups)
        {
            var g = kvp.Value;
            if (g == null)
                continue;

            string speciesName = g.species != null
                ? (!string.IsNullOrEmpty(g.species.displayName) ? g.species.displayName : g.species.name)
                : "NULL";

            results.Add(new AnimalGroupDebugSnapshot
            {
                id = g.id,
                speciesName = speciesName,
                size = g.size,
                ageInTurns = g.ageInTurns,
                dietName = g.species != null ? g.species.diet.ToString() : AnimalDiet.Herbivore.ToString(),

                currentHealth = g.currentHealth,
                maxHealth = g.MaxHealth,

                hunger = g.hunger,
                thirst = g.thirst,

                tileX = g.tile.x,
                tileY = g.tile.y,

                lastAction = g.lastAction,
                nextUpdateTurn = g.nextUpdateTurn,

                isAlive = g.isAlive,

                isLeader = g.isLeader,
                herdId = g.herdId,
                leaderGroupId = g.leaderGroupId,

                isHunting = g.isHunting,
                huntingTargetGroupId = g.huntingTargetGroupId,
                isTargetedByPredator = g.isTargetedByPredator,
                targetedByPredatorGroupId = g.targetedByPredatorGroupId,
                huntingEscapeCount = g.huntingEscapeCount,

                isOnReproductionCooldown = g.isOnReproductionCooldown,
                nextReproductionTurn = g.nextReproductionTurn,

                isInPredatorConflict = g.isInPredatorConflict,
                predatorConflictTargetGroupId = g.predatorConflictTargetGroupId,

                isFleeingFromThreat = g.isFleeingFromThreat,
                fleeFromPredatorGroupId = g.fleeFromPredatorGroupId,
                fleeUntilDistanceTiles = g.fleeUntilDistanceTiles,
                fleeStepsRemaining = g.fleeStepsRemaining,
                fleeThreatTileX = g.fleeThreatLastKnownTile.x,
                fleeThreatTileY = g.fleeThreatLastKnownTile.y,

                isRaidingPlayerTile = g.isRaidingPlayerTile,
                raidTargetTileX = g.raidTargetTile.x,
                raidTargetTileY = g.raidTargetTile.y,

                isHuntingHumanUnits = g.isHuntingHumanUnits,
                huntingHumanUnitGroupId = g.huntingHumanUnitGroupId
            });
        }

        results.Sort((a, b) =>
        {
            int speciesCompare = string.Compare(a.speciesName, b.speciesName, StringComparison.Ordinal);
            if (speciesCompare != 0) return speciesCompare;

            int tileXCompare = a.tileX.CompareTo(b.tileX);
            if (tileXCompare != 0) return tileXCompare;

            int tileYCompare = a.tileY.CompareTo(b.tileY);
            if (tileYCompare != 0) return tileYCompare;

            return a.id.CompareTo(b.id);
        });

        return results;
    }
}