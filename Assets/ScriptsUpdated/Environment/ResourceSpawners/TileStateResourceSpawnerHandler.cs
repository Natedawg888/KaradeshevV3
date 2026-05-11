using UnityEngine;

// Attach alongside EnvironmentFireState and EnvironmentResourceNode.
// Reacts to fire events and adds/removes temporary burnt-remains spawners.
public class TileStateResourceSpawnerHandler : MonoBehaviour
{
    [Header("Fire / Burnt Spawners")]
    [Tooltip("Spawner for Embers — added while the tile is actively burning.")]
    public ResourceSpawnerDefinition emberSpawner;

    [Tooltip("Spawner for Charcoal — added after the fire is extinguished.")]
    public ResourceSpawnerDefinition charcoalSpawner;

    [Tooltip("Spawner for Ash — added after the fire is extinguished, lasts longer.")]
    public ResourceSpawnerDefinition ashSpawner;

    [Header("Lifetime Overrides (turns)")]
    [Min(1)] public int emberLifetimeTurns    = 3;
    [Min(1)] public int charcoalLifetimeTurns = 8;
    [Min(1)] public int ashLifetimeTurns      = 15;

    [Header("Debug")]
    [SerializeField] private bool debugLogging;

    private EnvironmentResourceNode node;
    private EnvironmentFireState     fireState;

    private void Awake()
    {
        node      = GetComponent<EnvironmentResourceNode>();
        fireState = GetComponent<EnvironmentFireState>();

        if (node == null)
            Debug.LogWarning($"[TileState] [{name}] No EnvironmentResourceNode found on this GameObject.");
        if (fireState == null)
            Debug.LogWarning($"[TileState] [{name}] No EnvironmentFireState found on this GameObject.");
    }

    private void OnEnable()
    {
        if (fireState != null)
        {
            fireState.OnIgnited      += HandleIgnited;
            fireState.OnExtinguished += HandleExtinguished;
        }

        ResourceSourceCache.RegisterDynamicSpawner(emberSpawner);
        ResourceSourceCache.RegisterDynamicSpawner(charcoalSpawner);
        ResourceSourceCache.RegisterDynamicSpawner(ashSpawner);
    }

    private void OnDisable()
    {
        if (fireState != null)
        {
            fireState.OnIgnited      -= HandleIgnited;
            fireState.OnExtinguished -= HandleExtinguished;
        }

        ResourceSourceCache.UnregisterDynamicSpawner(emberSpawner);
        ResourceSourceCache.UnregisterDynamicSpawner(charcoalSpawner);
        ResourceSourceCache.UnregisterDynamicSpawner(ashSpawner);
    }

    private void HandleIgnited(EnvironmentFireState state)
    {
        if (node == null) return;

        node.SetTileState(TileStateFlags.HasBeenIgnited, true);
        if (debugLogging)
            Debug.Log($"[TileState] [{name}] Tile ignited — marking HasBeenIgnited");

        node.RemoveSpawnersByReason(SpawnerSourceReason.BurntTile);

        if (emberSpawner != null)
        {
            node.AddTemporarySpawner(emberSpawner, emberLifetimeTurns,
                                     SpawnerSourceReason.BurntTile, runImmediately: true);
            if (debugLogging)
                Debug.Log($"[TileState] [{name}] Ember spawner added (lifetime={emberLifetimeTurns}t)");
        }
    }

    private void HandleExtinguished(EnvironmentFireState state)
    {
        if (node == null) return;

        if (emberSpawner != null)
            node.RemoveSpawner(emberSpawner.spawnerID);

        if (charcoalSpawner != null)
        {
            node.AddTemporarySpawner(charcoalSpawner, charcoalLifetimeTurns,
                                     SpawnerSourceReason.BurntTile, runImmediately: true);
            if (debugLogging)
                Debug.Log($"[TileState] [{name}] Charcoal spawner added (lifetime={charcoalLifetimeTurns}t)");
        }

        if (ashSpawner != null)
        {
            node.AddTemporarySpawner(ashSpawner, ashLifetimeTurns,
                                     SpawnerSourceReason.BurntTile, runImmediately: true);
            if (debugLogging)
                Debug.Log($"[TileState] [{name}] Ash spawner added (lifetime={ashLifetimeTurns}t)");
        }
    }
}
