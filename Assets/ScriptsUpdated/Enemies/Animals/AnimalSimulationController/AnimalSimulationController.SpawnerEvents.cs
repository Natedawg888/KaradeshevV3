using System.Collections;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

// Wires AnimalSimulation events → ResourceSpawner system.
// Carcass spawners: fires AnimalDeathResourceSpawnerHandler.OnAnimalDied on group death.
// Dung spawners:    fires AnimalDroppingHandler.OnAnimalEnteredTile / OnAnimalLeftTile
//                   whenever a group moves to a different tile or is removed.
//
// Performance: spawner events are queued and processed at animalSpawnerEventsPerFrame
// per frame to avoid a spike when many groups spawn during reproduction.
public partial class AnimalSimulationController : MonoBehaviour
{
    // ── Performance settings ──────────────────────────────────────────────────

    [Header("Animal Spawner Event Performance")]
    [Tooltip("Queue spawner events and spread across frames instead of firing all at once.")]
    [SerializeField] private bool processAnimalSpawnerEventsOverFrames = true;

    [Tooltip("How many queued spawner events to process per frame.")]
    [SerializeField] private int animalSpawnerEventsPerFrame = 25;

    [Tooltip("Log every individual spawner event. Keep off — very spammy.")]
    [SerializeField] private bool debugAnimalSpawnerEvents = false;

    [Tooltip("Log a one-line summary per processed batch.")]
    [SerializeField] private bool debugAnimalSpawnerEventSummary = false;

    // ── Profiler markers ──────────────────────────────────────────────────────

    private static readonly ProfilerMarker s_spawnerEventQueueMarker =
        new ProfilerMarker("Animal.SpawnerEvents.ProcessQueue");
    private static readonly ProfilerMarker s_spawnerEventImmediateMarker =
        new ProfilerMarker("Animal.SpawnerEvents.Immediate");

    // ── Event queue ───────────────────────────────────────────────────────────

    private enum AnimalSpawnerEventType { EnteredTile, LeftTile, Died }

    private struct PendingAnimalSpawnerEvent
    {
        public AnimalSpawnerEventType eventType;
        public TileCoord              tileCoord;
        public AnimalDefinition       species;
        public int                    groupSize;
        public string                 speciesName; // captured at enqueue to avoid holding group ref
    }

    private readonly Queue<PendingAnimalSpawnerEvent> _pendingSpawnerEvents = new();
    private Coroutine _spawnerEventQueueRoutine;

    // ── Node cache ────────────────────────────────────────────────────────────

    private readonly Dictionary<TileCoord, EnvironmentResourceNode> _spawnerNodeCache = new();
    private bool _spawnerNodeCacheBuilt = false;

    private void EnsureNodeCache()
    {
        if (_spawnerNodeCacheBuilt) return;
        RebuildSpawnerNodeCache();
    }

    internal void RebuildSpawnerNodeCache()
    {
        _spawnerNodeCache.Clear();
        var envSource = envDataSource as MonoEnvironmentDataSource;
        if (envSource == null) { _spawnerNodeCacheBuilt = true; return; }

        foreach (var kvp in envSource.AllTiles)
        {
            var env = kvp.Value;
            if (env == null) continue;
            var node = env.GetComponent<EnvironmentResourceNode>();
            if (node == null) continue;
            _spawnerNodeCache[kvp.Key] = node;
        }
        _spawnerNodeCacheBuilt = true;
    }

    private EnvironmentResourceNode GetNodeAtCoord(TileCoord coord)
    {
        EnsureNodeCache();
        _spawnerNodeCache.TryGetValue(coord, out var node);
        return node;
    }

    // ── Subscribe / Unsubscribe ───────────────────────────────────────────────

    private readonly Dictionary<int, TileCoord> _groupLastTileForSpawners = new();

    internal void SubscribeSpawnerEvents()
    {
        if (_simulation == null) return;
        _simulation.OnGroupCreated    += HandleGroupCreatedForSpawners;
        _simulation.OnGroupUpdated    += HandleGroupUpdatedForSpawners;
        _simulation.OnGroupDiedAtTile += HandleGroupDiedAtTileForSpawners;
    }

    internal void UnsubscribeSpawnerEvents()
    {
        if (_simulation == null) return;
        _simulation.OnGroupCreated    -= HandleGroupCreatedForSpawners;
        _simulation.OnGroupUpdated    -= HandleGroupUpdatedForSpawners;
        _simulation.OnGroupDiedAtTile -= HandleGroupDiedAtTileForSpawners;
        _groupLastTileForSpawners.Clear();

        // Flush and stop the queue coroutine on teardown
        _pendingSpawnerEvents.Clear();
        if (_spawnerEventQueueRoutine != null)
        {
            StopCoroutine(_spawnerEventQueueRoutine);
            _spawnerEventQueueRoutine = null;
        }
    }

    // ── Simulation event handlers ─────────────────────────────────────────────

    private void HandleGroupCreatedForSpawners(AnimalGroupState group)
    {
        if (group?.species == null) return;
        _groupLastTileForSpawners[group.id] = group.tile;
        EnqueueOrFire(new PendingAnimalSpawnerEvent
        {
            eventType   = AnimalSpawnerEventType.EnteredTile,
            tileCoord   = group.tile,
            species     = group.species,
            speciesName = group.species.name,
            groupSize   = group.size
        });
    }

    private void HandleGroupUpdatedForSpawners(AnimalGroupState group)
    {
        if (group?.species == null) return;

        if (_groupLastTileForSpawners.TryGetValue(group.id, out TileCoord lastTile)
            && !lastTile.Equals(group.tile))
        {
            EnqueueOrFire(new PendingAnimalSpawnerEvent
            {
                eventType   = AnimalSpawnerEventType.LeftTile,
                tileCoord   = lastTile,
                species     = group.species,
                speciesName = group.species.name,
                groupSize   = group.size
            });
            EnqueueOrFire(new PendingAnimalSpawnerEvent
            {
                eventType   = AnimalSpawnerEventType.EnteredTile,
                tileCoord   = group.tile,
                species     = group.species,
                speciesName = group.species.name,
                groupSize   = group.size
            });
        }

        _groupLastTileForSpawners[group.id] = group.tile;
    }

    private void HandleGroupDiedAtTileForSpawners(AnimalGroupState group, TileCoord tile)
    {
        if (group?.species == null) return;

        // Carcass + left-tile on death
        EnqueueOrFire(new PendingAnimalSpawnerEvent
        {
            eventType   = AnimalSpawnerEventType.Died,
            tileCoord   = tile,
            species     = group.species,
            speciesName = group.species.name,
            groupSize   = group.size
        });
        EnqueueOrFire(new PendingAnimalSpawnerEvent
        {
            eventType   = AnimalSpawnerEventType.LeftTile,
            tileCoord   = tile,
            species     = group.species,
            speciesName = group.species.name,
            groupSize   = group.size
        });

        _groupLastTileForSpawners.Remove(group.id);
    }

    // ── Queue / immediate dispatch ────────────────────────────────────────────

    private void EnqueueOrFire(PendingAnimalSpawnerEvent evt)
    {
        if (processAnimalSpawnerEventsOverFrames)
        {
            _pendingSpawnerEvents.Enqueue(evt);
            if (_spawnerEventQueueRoutine == null && isActiveAndEnabled)
                _spawnerEventQueueRoutine = StartCoroutine(ProcessAnimalSpawnerEventQueue());
        }
        else
        {
            using (s_spawnerEventImmediateMarker.Auto())
                ProcessAnimalSpawnerEventImmediate(evt);
        }
    }

    private IEnumerator ProcessAnimalSpawnerEventQueue()
    {
        using (s_spawnerEventQueueMarker.Auto())
        {
            while (_pendingSpawnerEvents.Count > 0)
            {
                int processedThisFrame = 0;
                int batchLimit = Mathf.Max(1, animalSpawnerEventsPerFrame);

                while (_pendingSpawnerEvents.Count > 0 && processedThisFrame < batchLimit)
                {
                    var evt = _pendingSpawnerEvents.Dequeue();
                    ProcessAnimalSpawnerEventImmediate(evt);
                    processedThisFrame++;
                }

                if (debugAnimalSpawnerEventSummary && processedThisFrame > 0)
                    Debug.Log($"[SpawnerEvents] Processed {processedThisFrame} queued events. " +
                              $"Remaining={_pendingSpawnerEvents.Count}");

                yield return null;
            }
        }

        _spawnerEventQueueRoutine = null;
    }

    // ── Event execution ───────────────────────────────────────────────────────

    private void ProcessAnimalSpawnerEventImmediate(PendingAnimalSpawnerEvent evt)
    {
        switch (evt.eventType)
        {
            case AnimalSpawnerEventType.EnteredTile:
                FireAnimalEnteredTile(evt.tileCoord, evt.species, evt.speciesName, evt.groupSize);
                break;

            case AnimalSpawnerEventType.LeftTile:
                FireAnimalLeftTile(evt.tileCoord, evt.species, evt.speciesName);
                break;

            case AnimalSpawnerEventType.Died:
                FireAnimalDied(evt.tileCoord, evt.speciesName, evt.groupSize);
                break;
        }
    }

    private void FireAnimalEnteredTile(TileCoord tile, AnimalDefinition species,
                                        string speciesName, int size)
    {
        var node = GetNodeAtCoord(tile);
        if (node == null) return;
        AnimalDroppingHandler.OnAnimalEnteredTile?.Invoke(
            new AnimalTileRequest { targetNode = node, speciesID = speciesName });
        if (debugAnimalSpawnerEvents)
            Debug.Log($"[SpawnerEvents] Animal entered tile {tile} ('{speciesName}' x{size})");
    }

    private void FireAnimalLeftTile(TileCoord tile, AnimalDefinition species, string speciesName)
    {
        var node = GetNodeAtCoord(tile);
        if (node == null) return;
        AnimalDroppingHandler.OnAnimalLeftTile?.Invoke(
            new AnimalTileRequest { targetNode = node, speciesID = speciesName });
        if (debugAnimalSpawnerEvents)
            Debug.Log($"[SpawnerEvents] Animal left tile {tile} ('{speciesName}')");
    }

    private void FireAnimalDied(TileCoord tile, string speciesName, int groupSize)
    {
        var node = GetNodeAtCoord(tile);
        if (node == null) return;
        AnimalDeathResourceSpawnerHandler.OnAnimalDied?.Invoke(
            new AnimalDeathSpawnRequest
            {
                targetNode = node,
                speciesID  = speciesName,
                groupSize  = groupSize
            });
        if (debugAnimalSpawnerEvents)
            Debug.Log($"[SpawnerEvents] Carcass event → '{speciesName}' x{groupSize} at {tile}");
    }
}
