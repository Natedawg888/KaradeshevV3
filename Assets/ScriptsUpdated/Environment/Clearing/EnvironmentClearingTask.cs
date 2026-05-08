using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles clearing an area of environment tiles over multiple turns:
/// - Finds TileControl tiles within this object's BoxCollider.
/// - Marks any EnvironmentResourceNode as barren for the clearing duration.
/// - Each turn, slowly replaces tiles with a cleared tile prefab.
/// - When done, frees population and destroys itself.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class EnvironmentClearingTask : MonoBehaviour
{
    [Header("Clearing Settings")]
    public int clearingTurnsRequired = 0;
    public int requiredClearingPopulation = 0;

    public GameObject clearedTilePrefab;

    [Header("Environment / Calculators")]
    public EnvironmentControl environmentControl;

    [Header("Base Tile Placement (optional tiny tiles)")]
    public bool spawnBaseEnvironmentTilesFirst = false;
    public GameObject baseTilePrefab;
    public EnvironmentType forcedEnvironmentType;
    public GridManager gridManager;

    [Header("UI")]
    public TimerUI clearingTimerUI;   // already on the prefab

    private List<Individual> _assignedWorkers = null;

    // runtime
    private int _turnsLeft;
    private readonly List<TileControl> _affectedTiles = new();
    private float _tilesToChangePerTurn;
    private float _tilesToChangeAccumulated;

    private BoxCollider _box;

    // Population reservation
    private string _reservationId; // from PlayersPopulationManager

    private void Awake()
    {
        _box = GetComponent<BoxCollider>();
        _box.isTrigger = true; // region marker, not a physical collider
    }

    private void OnValidate()
    {
        if (_box == null)
            _box = GetComponent<BoxCollider>();
    }

    private void Start()
    {
        if (gridManager == null)
            gridManager = FindObjectOfType<GridManager>();

        // Make sure our BoxCollider matches the environment tile's footprint
        SyncBoxToEnvironmentTile();

        // 1) Spawn base tiny tiles OR just collect existing tiles
        if (spawnBaseEnvironmentTilesFirst && gridManager != null && baseTilePrefab != null)
        {
            SpawnBaseTilesAndCollectCells();
        }
        else
        {
            CollectAffectedTiles();
        }

        // 2) Disable interaction on tiles that will be processed
        SetTilesInteractable(false);

        // 3) Ensure we have an EnvironmentControl
        if (environmentControl == null && _affectedTiles.Count > 0)
        {
            for (int i = 0; i < _affectedTiles.Count && environmentControl == null; i++)
            {
                var tile = _affectedTiles[i];
                if (tile == null) continue;
                environmentControl = tile.GetComponentInChildren<EnvironmentControl>();
            }
        }

        // 4) Use calculators...
        if (environmentControl != null)
        {
            var envType  = environmentControl.environmentType;
            var tileType = environmentControl.environmentTileType;
            var size     = environmentControl.tileSize;

            if (clearingTurnsRequired <= 0)
                clearingTurnsRequired = ClearingTurnCalculator.CalculateClearingTurns(envType, tileType, size);

            if (requiredClearingPopulation <= 0)
                requiredClearingPopulation = ClearingPopulationRequirementCalculator
                    .CalculateRequiredPopulation(envType, tileType, size);
        }

        if (clearingTurnsRequired <= 0)
            clearingTurnsRequired = 1;

        _turnsLeft = clearingTurnsRequired;

        if (_affectedTiles.Count > 0)
            _tilesToChangePerTurn = (float)_affectedTiles.Count / clearingTurnsRequired;
        else
            _tilesToChangePerTurn = 0f;

        if (clearingTimerUI != null)
        {
            clearingTimerUI.Initialize(clearingTurnsRequired);
            clearingTimerUI.UpdateTimer(_turnsLeft);
        }

        if (!ReservePopulationForClearing())
        {
            //Debug.LogWarning("[EnvironmentClearingTask] Not enough available population to start clearing; cancelling task.");
            SetTilesInteractable(true);
            Destroy(gameObject);
            return;
        }

        PlayerEnvironmentClearingManager.Instance?.RegisterTask(this);
    }

    // --------------------------------------------------------------------
    // NEW: spawn tiny base tiles in each cell, track them in _affectedTiles
    // --------------------------------------------------------------------
    private void SpawnBaseTilesAndCollectCells()
    {
        _affectedTiles.Clear();
        if (gridManager == null || baseTilePrefab == null || _box == null)
        {
            //Debug.LogWarning("[EnvironmentClearingTask] SpawnBaseTilesAndCollectCells aborted: missing refs.");
            return;
        }

        Bounds bounds = _box.bounds;

        Vector2Int minGridPos = gridManager.GetGridPosition(bounds.min);
        Vector2Int maxGridPos = gridManager.GetGridPosition(bounds.max);

        float halfCell = gridManager.cellSize * 0.5f;

        //Debug.Log($"[EnvironmentClearingTask] SpawnBaseTilesAndCollectCells " +
                //$"min={minGridPos} max={maxGridPos} bounds={bounds}");

        for (int x = minGridPos.x; x <= maxGridPos.x; x++)
        {
            for (int y = minGridPos.y; y <= maxGridPos.y; y++)
            {
                Vector3 cellCorner = gridManager.GetWorldPosition(x, y);
                Vector3 cellCenter = cellCorner + new Vector3(halfCell, 0f, halfCell);
                Vector2Int gridPos = new Vector2Int(x, y);

                // Only consider cells whose CENTER is inside the bounds
                if (!bounds.Contains(new Vector3(cellCenter.x, bounds.center.y, cellCenter.z)))
                    continue;

                // For clearing we IGNORE previous occupancy and overwrite
                GameObject baseTile = Instantiate(baseTilePrefab, cellCenter, Quaternion.identity);
                baseTile.name = $"TinyBase_{gridPos.x}_{gridPos.y}";

                gridManager.MarkCellOccupied(gridPos.x, gridPos.y);

                // Decide which environment type to use:
                EnvironmentType envTypeToUse =
                    (environmentControl != null)
                        ? environmentControl.environmentType
                        : forcedEnvironmentType;

                EnvironmentTileType tileTypeToUse = EnvironmentTileType.Land;

                // Get the TileScript on the base tile (or its children)
                var tileScript = baseTile.GetComponentInChildren<TileScript>(true);
                if (tileScript != null)
                {
                    //Debug.Log(
                        //$"[EnvironmentClearingTask] Tiny base '{baseTile.name}' got TileScript. " +
                        //$"Calling ForceSpawnSpecific({envTypeToUse}, {tileTypeToUse}). " +
                        //$"Options count={ (tileScript.options != null ? tileScript.options.Length : 0) }");

                    bool ok = tileScript.ForceSpawnSpecific(envTypeToUse, tileTypeToUse);
                    if (!ok)
                    {
                        //Debug.LogWarning(
                            //$"[EnvironmentClearingTask] ForceSpawnSpecific({envTypeToUse}, {tileTypeToUse}) " +
                            //$"FAILED on tiny base tile at grid {gridPos}. Falling back to SpawnEnvironmentTile().");

                        tileScript.SpawnEnvironmentTile();
                    }
                }
                else
                {
                    //Debug.LogError(
                        //$"[EnvironmentClearingTask] baseTilePrefab '{baseTilePrefab.name}' has no TileScript " +
                        //$"(even in children); cannot spawn environment on tiny base tile at grid {gridPos}.");
                }

                // 🔥 The TileControl lives on the spawned environment prefab.
                TileControl tileControl = null;

                // This is the *tiny* environment spawned from the baseTile
                var tinyEnv = baseTile.GetComponentInChildren<EnvironmentControl>(true);
                if (tinyEnv != null)
                {
                    // Grab its TileControl parent
                    tileControl = tinyEnv.GetComponentInParent<TileControl>();

                    // ✅ Disable ALL BoxColliders under the spawned environment tile
                    // so the player can't interact with these tiny clearing tiles.
                    var envBoxes = tinyEnv.GetComponentsInChildren<BoxCollider>(true);
                    foreach (var box in envBoxes)
                    {
                        box.enabled = false;
                    }
                }

                if (tileControl != null)
                {
                    _affectedTiles.Add(tileControl);
                    // Optional double-safety gate if your interaction system checks this:
                    tileControl.isInteractable = false;
                }
                else
                {
                    //Debug.LogWarning(
                        //$"[EnvironmentClearingTask] Could not find TileControl on spawned environment " +
                        //$"under '{baseTile.name}' at grid {gridPos}.");
                }
            }
        }

        // Remove the original big tile(s) now that we've laid down tiny ones
        Collider[] hits = Physics.OverlapBox(bounds.center, bounds.extents, Quaternion.identity);
        foreach (var hit in hits)
        {
            var tile = hit.GetComponent<TileControl>();
            if (tile == null) continue;

            // Skip our newly spawned tiny environment tiles (they're already in _affectedTiles)
            if (_affectedTiles.Contains(tile))
                continue;

            //Debug.Log($"[EnvironmentClearingTask] Destroying original big tile '{tile.name}' in cleared area.");
            Destroy(tile.gameObject);
        }

        //Debug.Log($"[EnvironmentClearingTask] Spawned base tiles and collected {_affectedTiles.Count} cells to clear in area '{name}'.");
    }

    // --------------------------------------------------------------------
    // Turn progression is unchanged, but now operates on tiny base tiles
    // --------------------------------------------------------------------
    public void AdvanceOneTurn()
    {
        if (_turnsLeft <= 0) return;

        _turnsLeft = Mathf.Max(0, _turnsLeft - 1);
        _tilesToChangeAccumulated += _tilesToChangePerTurn;

        if (clearingTimerUI != null)
            clearingTimerUI.UpdateTimer(_turnsLeft);

        int tilesToChange = Mathf.FloorToInt(_tilesToChangeAccumulated);
        _tilesToChangeAccumulated -= tilesToChange;

        if (_turnsLeft <= 0)
        {
            CompleteClearing();
        }
        else if (tilesToChange > 0)
        {
            ClearSomeTiles(tilesToChange);
        }
    }

    private void ClearSomeTiles(int count)
    {
        for (int i = 0; i < count && _affectedTiles.Count > 0; i++)
        {
            int index = Random.Range(0, _affectedTiles.Count);
            var tile  = _affectedTiles[index];

            if (tile != null)
            {
                // NOW: this is a tiny base tile → convert to cleared tile
                ReplaceWithClearedTile(tile);
            }

            _affectedTiles.RemoveAt(index);
        }
    }

    private void CompleteClearing()
    {
        // convert any remaining tiny base tiles to cleared tiles
        for (int i = _affectedTiles.Count - 1; i >= 0; i--)
        {
            var tile = _affectedTiles[i];
            if (tile != null)
                ReplaceWithClearedTile(tile);
        }

        _affectedTiles.Clear();

        ReleasePopulationReservation();

        if (clearingTimerUI != null)
            clearingTimerUI.gameObject.SetActive(false);

        PlayerEnvironmentClearingManager.Instance?.NotifyTaskCompleted(this);
        Destroy(gameObject);
    }

    private void ReplaceWithClearedTile(TileControl tile)
    {
        if (tile == null || clearedTilePrefab == null)
            return;

        Vector3 position    = tile.transform.position;
        Quaternion rotation = tile.transform.rotation;

        if (gridManager != null)
        {
            Vector2Int gridPos = gridManager.GetGridPosition(position);
            Vector3 cellCorner = gridManager.GetWorldPosition(gridPos.x, gridPos.y);
            float halfCell = gridManager.cellSize * 0.5f;
            position = cellCorner + new Vector3(halfCell, 0f, halfCell);
        }

        Instantiate(clearedTilePrefab, position, rotation);
        Destroy(tile.gameObject);
    }

    // ------------------------------------------------------
    // Turn progression (driven by PlayerEnvironmentClearingManager)
    // ------------------------------------------------------

    private void SyncBoxToEnvironmentTile()
    {
        if (_box == null || environmentControl == null)
            return;

        // Try to get the TileControl that owns this environment
        var tile = environmentControl.GetComponentInParent<TileControl>();
        if (tile == null)
        {
            //Debug.LogWarning("[EnvironmentClearingTask] No TileControl parent found for environment; " +
                            //"cannot sync BoxCollider size.");
            return;
        }

        var srcBox = tile.GetComponent<BoxCollider>();
        if (srcBox == null)
        {
            //Debug.LogWarning("[EnvironmentClearingTask] TileControl has no BoxCollider; " +
                            //"cannot sync BoxCollider size.");
            return;
        }

        _box.center = srcBox.center;

        // Copy size then shrink a bit so we don't accidentally overlap into neighbours
        Vector3 size = srcBox.size;

        const float shrink = 0.01f;   // total shrink (so 0.05 each side effectively)
        size.x = Mathf.Max(0.01f, size.x - shrink);
        size.z = Mathf.Max(0.01f, size.z - shrink);
        // leave Y alone
        _box.size = size;

        _box.isTrigger = true;

        //Debug.Log($"[EnvironmentClearingTask] Synced BoxCollider to tile (shrunk). New size={_box.size}, center={_box.center}");
    }

    private void CollectAffectedTiles()
    {
        _affectedTiles.Clear();
        if (_box == null) return;

        Bounds b = _box.bounds;
        Collider[] hits = Physics.OverlapBox(b.center, b.extents, Quaternion.identity);

        foreach (var hit in hits)
        {
            if (hit == null) continue;

            var tile = hit.GetComponent<TileControl>();
            if (tile != null && !_affectedTiles.Contains(tile))
            {
                _affectedTiles.Add(tile);
            }
        }

        //Debug.Log($"[EnvironmentClearingTask] Found {_affectedTiles.Count} tiles to clear in area '{name}'.");
    }

    /// <summary>
    /// Enable/disable interaction on all affected tiles.
    /// </summary>
    private void SetTilesInteractable(bool interactable)
    {
        foreach (var tile in _affectedTiles)
        {
            if (tile == null) continue;
            tile.isInteractable = interactable;
        }
    }

    private bool ReservePopulationForClearing()
    {
        if (requiredClearingPopulation <= 0)
            return true; // nothing to reserve

        var sim = PlayerFamilySimulationManager.Instance;
        if (sim == null)
        {
            //Debug.LogWarning("[EnvironmentClearingTask] PlayerFamilySimulationManager not found; skipping population reservation.");
            return true; // allow task to proceed, but no reservation
        }

        if (!sim.TryPickRandomNonBusyTaskIndividuals(
                requiredClearingPopulation,
                out var picked,
                out _reservationId))
        {
            _reservationId = null;
            _assignedWorkers = null;
            return false; // not enough free people
        }

        _assignedWorkers = picked;
        return true;
    }

    private void ReleasePopulationReservation()
    {
        if (string.IsNullOrEmpty(_reservationId))
            return;

        var pop = PlayersPopulationManager.Instance;
        if (pop != null)
        {
            pop.ReleaseReservation(_reservationId);
        }

        _reservationId = null;
        _assignedWorkers = null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_box == null) _box = GetComponent<BoxCollider>();
        if (_box == null) return;

        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.25f);
        Bounds b = _box.bounds;
        Gizmos.DrawCube(b.center, b.size);
        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(b.center, b.size);
    }
#endif
}
