using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Kardashev/Unit Actions/Scout Tile", fileName = "ScoutTileAction")]
public class ScoutTileActionSO : UnitActionDefinitionSO
{
    [Header("Targeting")]
    [Tooltip("Maximum BFS radius in tiles.")]
    public int maxRangeInTiles = 3;

    [Tooltip("Can scout environment tiles.")]
    public bool allowEnvironmentTiles = true;

    [Tooltip("Can scout building tiles.")]
    public bool allowBuildingTiles = true;

    [Tooltip("Allow targets that are already discovered.")]
    public bool allowDiscoveredTiles = true;

    [Tooltip("Allow targets that are still undiscovered.")]
    public bool allowUndiscoveredTiles = true;

    [Header("Base Time")]
    [Tooltip("Base number of turns before stat modifiers.")]
    public int baseTurns = 3;

    [Tooltip("Multiplier if tile is already discovered.")]
    public float discoveredTileTurnMult = 0.75f;

    [Tooltip("Multiplier if tile is undiscovered.")]
    public float undiscoveredTileTurnMult = 1.25f;

    [Header("Stat Weights")]
    [Tooltip("How much accuracy helps speed up scouting.")]
    public float accuracyWeight = 1f;

    [Tooltip("How much range helps speed up scouting.")]
    public float rangeWeight = 0.75f;

    [Tooltip("How much movement speed helps speed up scouting.")]
    public float movementWeight = 0.5f;

    [Header("Stat Scaling")]
    [Tooltip("Total stat score at which the unit reaches the fastest scout time.")]
    public float fastStatsScore = 20f;

    [Tooltip("Slowest possible multiplier on baseTurns when stats are low.")]
    public float maxSlowMult = 2f;

    [Tooltip("Fastest possible multiplier on baseTurns when stats are high.")]
    public float minFastMult = 0.5f;

    // ✅ Non-alloc buffer to collect behaviours and filter to IScoutResultSource
    private static readonly List<MonoBehaviour> _sourceBuffer = new(64);

    public override bool CanUnitUseAction(MilitiaUnit unit)
    {
        // Later you can restrict by MilitiaUnitCategory etc.
        return unit != null;
    }

    public override bool IsValidTarget(
        TileUnitGroupData group,
        TileControl originTile,
        TileControl targetTile)
    {
        if (group == null || originTile == null || targetTile == null)
            return false;

        // Don't scout the tile we're standing on.
        if (originTile == targetTile)
            return false;

        bool isEnv = targetTile.tileContentType == TileContentType.Environment;
        bool isBuilding = targetTile.tileContentType == TileContentType.Building;

        if (isEnv && !allowEnvironmentTiles) return false;
        if (isBuilding && !allowBuildingTiles) return false;
        if (!isEnv && !isBuilding) return false;

        bool isDiscovered = IsTileDiscovered(targetTile);

        if (isDiscovered && !allowDiscoveredTiles) return false;
        if (!isDiscovered && !allowUndiscoveredTiles) return false;

        return true;
    }

    public override int GetTurnCost(
        TileUnitGroupData group,
        TileControl originTile,
        TileControl targetTile)
    {
        if (group == null || group.unitType == null)
            return 0;

        var unit = group.unitType;

        bool isDiscovered = IsTileDiscovered(targetTile);

        // Effective stats including training bonuses.
        float acc = unit.accuracy + group.bonusAccuracy;
        float range = unit.range + group.bonusRange;
        float move = unit.movementSpeed + group.bonusMovementSpeed;

        // Weighted stat score.
        float statScore =
            acc * accuracyWeight +
            range * rangeWeight +
            move * movementWeight;

        // Convert stats into a time multiplier between [minFastMult, maxSlowMult]
        float timeMult = 1f;
        if (fastStatsScore > 0f)
        {
            float t = Mathf.Clamp01(statScore / fastStatsScore);
            // t = 0  -> maxSlowMult
            // t = 1  -> minFastMult
            timeMult = Mathf.Lerp(maxSlowMult, minFastMult, t);
        }

        float tileMult = isDiscovered ? discoveredTileTurnMult : undiscoveredTileTurnMult;
        float turnsF = baseTurns * timeMult * tileMult;

        int turns = Mathf.Max(1, Mathf.RoundToInt(turnsF));
        return turns;
    }

    public override void Resolve(
        TileUnitGroupData group,
        TileUnitGroupControl owner,
        TileControl targetTile)
    {
        if (targetTile == null)
            return;

        // 1) Store scout results on the group (units + animals)
        if (group != null)
        {
            if (group.lastScoutResults == null)
                group.lastScoutResults = new List<ScoutResultEntry>();
            group.lastScoutResults.Clear();

            // --- Unit groups on this tile ---
            var unitCtrl = targetTile.GetComponentInChildren<TileUnitGroupControl>();
            if (unitCtrl != null && unitCtrl.Groups != null)
            {
                foreach (var other in unitCtrl.Groups)
                {
                    if (other == null || other.unitType == null)
                        continue;

                    if (other == group) // optional: don't include self
                        continue;

                    bool isMoving =
                        other.plannedPathGridPositions != null &&
                        other.plannedStepTurnCosts != null &&
                        other.plannedPathGridPositions.Count > 0 &&
                        other.plannedPathGridPositions.Count == other.plannedStepTurnCosts.Count &&
                        other.currentPathIndex < other.plannedPathGridPositions.Count;

                    if (other.activeAction != null && other.remainingActionTurns > 0)
                        isMoving = true;

                    var entry = new ScoutResultEntry
                    {
                        entityName = !string.IsNullOrEmpty(other.groupName)
                            ? other.groupName
                            : other.unitType.unitName,
                        icon = other.unitType.unitIcon,
                        count = other.unitCount,
                        entityType = ScoutEntityType.Unit,
                        wasMoving = isMoving,

                        // animals-only flags remain false
                        wasEating = false,
                        wasDrinking = false,
                        wasHunting = false,
                        wasDefending = false,
                        wasTargeted = false,
                        wasAttacking = false,
                        wasFleeing = false
                    };

                    group.lastScoutResults.Add(entry);
                }
            }

            // --- Animals & any other scout sources (non-alloc) ---
            _sourceBuffer.Clear();
            targetTile.GetComponentsInChildren(false, _sourceBuffer);

            for (int i = 0; i < _sourceBuffer.Count; i++)
            {
                var mb = _sourceBuffer[i];
                if (mb == null) continue;

                if (mb is not IScoutResultSource src)
                    continue;

                var entry = new ScoutResultEntry
                {
                    entityName = src.GetScoutDisplayName(),
                    icon = src.GetScoutIcon(),
                    count = src.GetScoutCount(),
                    entityType = ScoutEntityType.Animal,

                    wasMoving = src.GetIsMoving(),
                    wasEating = src.GetIsEating(),
                    wasDrinking = src.GetIsDrinking(),
                    wasHunting = src.GetIsHunting(),
                    wasDefending = src.GetIsDefending(),
                    wasTargeted = src.GetIsTargeted(),
                    wasAttacking = src.GetIsAttacking(),
                    wasFleeing = src.GetIsFleeing()
                };

                group.lastScoutResults.Add(entry);
            }

            // ✅ Mark: this group has scout results to view (even if list is empty)
            group.hasPendingScoutResults = true;
        }

        int resultCount = (group != null && group.lastScoutResults != null)
            ? group.lastScoutResults.Count
            : 0;

        Debug.Log(
            $"[ScoutTileAction] Group {group?.groupId} finished scouting tile {targetTile.name}. " +
            $"Found {resultCount} entities (units + animals).");
    }

    public int GetMaxRangeInTiles()
    {
        return Mathf.Max(1, maxRangeInTiles);
    }

    // -------- helpers --------

    private bool IsTileDiscovered(TileControl tile)
    {
        if (tile == null) return true;

        // Buildings are always "known"
        if (tile.tileContentType == TileContentType.Building)
            return true;

        if (tile.tileContentType != TileContentType.Environment)
            return true;

        var status = tile.GetComponentInChildren<EnvironmentStatus>();
        if (status != null)
            return status.IsDiscovered;

        var envCtrl = tile.GetComponentInChildren<EnvironmentControl>();
        if (envCtrl != null)
            return envCtrl.IsDiscovered;

        // No info -> treat as undiscovered.
        return false;
    }
}