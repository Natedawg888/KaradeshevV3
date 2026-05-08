using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Kardashev/Unit Actions/Track Area", fileName = "TrackAreaAction")]
public class TrackAreaActionSO : UnitActionDefinitionSO
{
    [Header("Scan Area")]
    public int maxRangeInTiles = 3;

    [Header("What to Track")]
    public bool trackAnimals = true;
    public bool trackUnits = false;

    [Header("Performance Safety")]
    [Min(1)] public int maxTilesToProcess = 64;
    [Min(1)] public int maxAnimalResults = 64;
    [Min(1)] public int maxUnitResults = 64;

    [Header("Coroutine Batching")]
    [Tooltip("How many tiles to process each frame while resolving tracking.")]
    [Min(1)] public int tilesProcessedPerFrame = 10;

    [Tooltip("Disable temporarily to test whether the UI/event subscriber is the spike.")]
    public bool raiseResultsEvent = true;

    [Tooltip("Disable in normal play; logs can spike badly when many actions happen.")]
    public bool verboseLogging = false;

    // Prevent the same group from starting multiple tracking coroutines at once.
    private static readonly HashSet<string> _groupsResolving = new();

    private const float ACC_WEIGHT = 1f;
    private const float RANGE_WEIGHT = 0.75f;

    private const int MIN_MARKER_TURNS = 1;
    private const int MAX_MARKER_TURNS = 8;
    private const float STATS_FOR_MAX_MARKER = 20f;

    public override bool CanUnitUseAction(MilitiaUnit unit) => unit != null;

    public override bool IsValidTarget(TileUnitGroupData group, TileControl originTile, TileControl targetTile)
    {
        if (group == null || originTile == null || targetTile == null)
            return false;

        return originTile == targetTile;
    }

    public override int GetTurnCost(TileUnitGroupData group, TileControl originTile, TileControl targetTile) => 0;

    public int GetMarkerDurationTurns(TileUnitGroupData group)
    {
        if (group == null || group.unitType == null)
            return MIN_MARKER_TURNS;

        var unit = group.unitType;

        float acc = unit.accuracy + group.bonusAccuracy;
        float range = unit.range + group.bonusRange;

        float statScore = (acc * ACC_WEIGHT) + (range * RANGE_WEIGHT);

        float t = STATS_FOR_MAX_MARKER <= 0f
            ? 0f
            : Mathf.Clamp01(statScore / STATS_FOR_MAX_MARKER);

        int turns = Mathf.RoundToInt(Mathf.Lerp(MIN_MARKER_TURNS, MAX_MARKER_TURNS, t));
        return Mathf.Clamp(turns, MIN_MARKER_TURNS, MAX_MARKER_TURNS);
    }

    public override void Resolve(TileUnitGroupData group, TileUnitGroupControl owner, TileControl targetTile)
    {
        if (group == null || targetTile == null)
            return;

        var mgr = UnitGroupActionManager.Instance;
        if (mgr == null)
            return;

        if (string.IsNullOrWhiteSpace(group.groupId))
            return;

        // Prevent duplicate async resolves for the same group.
        if (!_groupsResolving.Add(group.groupId))
            return;

        var co = mgr.StartManagedRoutine(ResolveAsync(group, owner, targetTile, mgr));
        if (co == null)
            _groupsResolving.Remove(group.groupId);
    }

    private IEnumerator ResolveAsync(
        TileUnitGroupData group,
        TileUnitGroupControl owner,
        TileControl targetTile,
        UnitGroupActionManager mgr)
    {
        // Local buffers so concurrent tracking coroutines do not stomp each other.
        List<TileControl> tilesInRange = new(128);
        List<AnimalGroupState> animalGroupsBuf = new(32);

        try
        {
            if (group == null || targetTile == null || mgr == null)
                yield break;

            // BeginTrackingForGroup already cached the current tile into activeActionTargetTile.
            var origin = targetTile;

            tilesInRange.Clear();
            mgr.CollectTilesInRangeSphere(
                origin,
                owner != null ? owner.transform.position : origin.transform.position,
                Mathf.Max(1, maxRangeInTiles),
                tilesInRange,
                includeOrigin: true
            );

            if (group.lastTrackingAnimalResults == null)
                group.lastTrackingAnimalResults = new List<TrackingResultEntry>(32);

            if (group.lastTrackingUnitResults == null)
                group.lastTrackingUnitResults = new List<TrackingResultEntry>(32);

            group.lastTrackingAnimalResults.Clear();
            group.lastTrackingUnitResults.Clear();

            var sim = AnimalSimulationAccess.Current;

            int animalCount = 0;
            int unitCount = 0;
            int tilesToProcess = Mathf.Min(tilesInRange.Count, Mathf.Max(1, maxTilesToProcess));
            int tilesPerFrame = Mathf.Max(1, tilesProcessedPerFrame);

            int index = 0;
            while (index < tilesToProcess)
            {
                int end = Mathf.Min(index + tilesPerFrame, tilesToProcess);

                for (; index < end; index++)
                {
                    if (group == null)
                        yield break;

                    var tile = tilesInRange[index];
                    if (tile == null)
                        continue;

                    Vector2Int grid = tile.GetGridPosition();

                    // ---------------- Units ----------------
                    if (trackUnits && unitCount < maxUnitResults)
                    {
                        TileUnitGroupControl unitCtrl = null;
                        mgr.TryGetUnitControlForTile(tile, out unitCtrl);

                        if (unitCtrl != null && unitCtrl.Groups != null)
                        {
                            for (int i = 0; i < unitCtrl.Groups.Count; i++)
                            {
                                var other = unitCtrl.Groups[i];
                                if (other == null || other.unitType == null)
                                    continue;

                                if (other == group)
                                    continue;

                                group.lastTrackingUnitResults.Add(new TrackingResultEntry
                                {
                                    entityName = !string.IsNullOrEmpty(other.groupName)
                                        ? other.groupName
                                        : other.unitType.unitName,
                                    icon = other.unitType.unitIcon,
                                    count = other.unitCount,
                                    entityType = TrackEntityType.Unit,
                                    sourceGrid = grid,
                                    sourceTile = tile
                                });

                                unitCount++;
                                if (unitCount >= maxUnitResults)
                                    break;
                            }
                        }
                    }

                    // ---------------- Animals ----------------
                    if (trackAnimals && sim != null && animalCount < maxAnimalResults)
                    {
                        TileCoord coord = new TileCoord { x = grid.x, y = grid.y };

                        animalGroupsBuf.Clear();
                        sim.CollectGroupsOnTile(coord, animalGroupsBuf);

                        if (animalGroupsBuf.Count > 0)
                        {
                            for (int i = 0; i < animalGroupsBuf.Count; i++)
                            {
                                var ag = animalGroupsBuf[i];
                                if (ag == null || !ag.isAlive || ag.species == null)
                                    continue;

                                group.lastTrackingAnimalResults.Add(new TrackingResultEntry
                                {
                                    entityName = !string.IsNullOrEmpty(ag.species.displayName)
                                        ? ag.species.displayName
                                        : ag.species.name,
                                    icon = ag.species.icon,
                                    count = ag.size,
                                    entityType = TrackEntityType.Animal,
                                    sourceGrid = grid,
                                    sourceTile = tile
                                });

                                animalCount++;
                                if (animalCount >= maxAnimalResults)
                                    break;
                            }
                        }
                    }

                    if (animalCount >= maxAnimalResults && unitCount >= maxUnitResults)
                    {
                        index = tilesToProcess;
                        break;
                    }
                }

                if (index < tilesToProcess)
                    yield return null;
            }

            if (group == null)
                yield break;

            group.hasPendingTrackingResults = true;
            group.lastTrackingMarkerTurns = GetMarkerDurationTurns(group);

            if (raiseResultsEvent)
                UnitGroupActionManager.RaiseTrackingResultsReady(group);

            if (verboseLogging)
            {
                //Debug.Log(
                    //$"[TrackAreaAction] Completed for group {group.groupId}. " +
                    //$"Animals={group.lastTrackingAnimalResults.Count}, " +
                    //$"Units={group.lastTrackingUnitResults.Count}, " +
                    //$"MarkerTurns={group.lastTrackingMarkerTurns}, " +
                    //$"TilesProcessed={tilesToProcess}/{tilesInRange.Count}, " +
                    //$"TilesPerFrame={tilesPerFrame}.");
            }
        }
        finally
        {
            if (group != null && !string.IsNullOrWhiteSpace(group.groupId))
                _groupsResolving.Remove(group.groupId);
        }
    }
}
