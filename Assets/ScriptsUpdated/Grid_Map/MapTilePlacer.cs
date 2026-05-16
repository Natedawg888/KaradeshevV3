using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

// #if UNITY_EDITOR          // Handles.Label is editor–only
// using UnityEditor;
// #endif

[System.Serializable]
public class TilePrefab
{
    public GameObject prefab;
    public int        spawnWeight; // Higher means more frequent
    public TileSize   tileSize;    // now uses the single enum above
}

public class MapTilePlacer : MonoBehaviour
{
    /* ───── public references ───── */
    [Header("References")]
    public MapGenerator mapGenerator;
    public GridManager gridManager;

    [Header("Water / Edge Prefabs")]
    public GameObject oceanTilePrefab;
    public GameObject beachTilePrefab;
    public GameObject lakeTilePrefab;
    public GameObject lakeEdgePrefab;
    public GameObject beachCornerPrefab;
    public GameObject lakeEdgeCornerPrefab;
    public GameObject riverTilePrefab; // straight 2×2

    [Header("River Settings")]
    [Range(0, 1)]
    public float branchSplitChance = 0.1f;

    [Header("Tile Prefabs (land fill)")]
    public TilePrefab[] tilePrefabs;

    [Header("Settings")]
    [Tooltip("Footprint of all tiles in grid cells (2 = 2×2)")]
    public int tileFootprint = 2;

    /* ───── bookkeeping ───── */
    readonly List<GameObject> spawned = new();

    readonly HashSet<Vector2Int> beachCells = new();
    readonly HashSet<Vector2Int> oceanCells = new();
    readonly HashSet<Vector2Int> coastCornerCells = new();
    readonly HashSet<Vector2Int> riverCells = new();
    readonly HashSet<Vector2Int> riverBlocks = new();

    /*──── enum ────*/
    enum TurnDirection { None, Left, Right }

    public static bool WorldReady { get; private set; } = false;

    /* ─── gizmo bookkeeping ─── */
    // struct GizmoMark  { public Vector2Int g; public TurnDirection t; }
    // struct ArrowMark { public Vector3 pos; public Vector3 dir; }
    // readonly List<GizmoMark> gizmoMarks = new();
    // readonly List<ArrowMark> arrowMarks = new();

    private Coroutine _placementRoutine;

    void Awake()
    {
        WorldReady = false;
    }

    Vector3 TileCentre(Vector2Int p)
    {
        float c = gridManager.cellSize;
        return gridManager.GetWorldPosition(p.x, p.y) +
               new Vector3(tileFootprint * 0.5f * c, 0, tileFootprint * 0.5f * c);
    }

    public void BeginPlacement()
    {
        if (_placementRoutine != null)
            return;

        _placementRoutine = StartCoroutine(PlaceTilesCoroutine());
    }

    IEnumerator PlaceTilesCoroutine()
    {
        bool shouldPlaceRivers = true;

        var presetMgr = EnvironmentPresetManager.Instance;
        var currentPreset = presetMgr != null ? presetMgr.GetCurrentPreset() : null;
        var section = currentPreset != null ? currentPreset.planetarySection : null;

        if (section != null)
        {
            shouldPlaceRivers = section.placeRivers;

            if (section.overrideBranchSplitChance)
                branchSplitChance = section.presetBranchSplitChance;
        }

        /* ---------- reference checks ---------- */
        if (!mapGenerator || !gridManager ||
            !oceanTilePrefab || !beachTilePrefab ||
            !lakeTilePrefab || !lakeEdgePrefab)
        {
            //Debug.LogWarning("MapTilePlacer: assign all references first!");
            _placementRoutine = null;
            yield break;
        }

        if (shouldPlaceRivers && !riverTilePrefab)
        {
            //Debug.LogWarning("MapTilePlacer: riverTilePrefab missing but preset wants rivers.");
            _placementRoutine = null;
            yield break;
        }

        /* ---------- wipe prev run ---------- */
        spawned.Clear();
        beachCells.Clear();
        oceanCells.Clear();
        coastCornerCells.Clear();
        riverCells.Clear();
        riverBlocks.Clear();

        int blockSize = mapGenerator.blockSize;
        int size = Mathf.Max(1, tileFootprint);
        float cell = gridManager.cellSize;

        /* =================================================================
           SECTION 1 –– cache block locations
           =================================================================*/
        CacheBlocks(out var seaBlocks,
                    out var lakeBlocks,
                    out var landBlocks,
                    out var interiorLandBlocks);

        yield return null; // ♦ allow one frame for GC & rendering

        /* =================================================================
           SECTION 2 –– fill Sea / Lake interiors
           =================================================================*/
        yield return StartCoroutine(FillWaterBlocks(seaBlocks, oceanTilePrefab, blockSize, size, cell));
        yield return StartCoroutine(FillWaterBlocks(lakeBlocks, lakeTilePrefab, blockSize, size, cell));

        yield return null; // ♦

        /* =================================================================
           SECTION 3 –– coasts (beach / lake-edge)
           =================================================================*/
        BuildCoastLines(seaBlocks, lakeBlocks, landBlocks,
                        blockSize, size, cell);

        yield return null; // ♦

        /* =================================================================
           SECTION 4 –– rivers
           =================================================================*/
        if (shouldPlaceRivers)
        {
            BuildRivers(interiorLandBlocks, blockSize, size, cell);
            yield return null; // ♦
        }

        /* =================================================================
           SECTION 5 –– land-fill with weighted prefabs
           =================================================================*/
        FillInteriorLand(landBlocks, blockSize);

        /* =================================================================
           CLEAN-UP
           =================================================================*/
        if (mapGenerator != null)
        {
            mapGenerator.ClearBlockColliders();
            mapGenerator.enabled = false;
        }

        /*  announce that all map tiles are in place  */
        WorldReady = true; // ← other scripts can start their work now

        enabled = false;
        _placementRoutine = null;
    }

    private void CacheBlocks(
        out HashSet<Vector2Int> seaBlocks,
        out HashSet<Vector2Int> lakeBlocks,
        out List<Vector2Int> landBlocks,
        out HashSet<Vector2Int> interiorLandBlocks)
    {
        int blockSize = mapGenerator.blockSize;

        seaBlocks = new HashSet<Vector2Int>();
        lakeBlocks = new HashSet<Vector2Int>();
        landBlocks = new List<Vector2Int>();

        // 1a) classify each block‐collider under MapGenerator
        foreach (Transform t in mapGenerator.transform)
        {
            var bc = t.GetComponent<BoxCollider>();
            if (bc == null) continue;

            Vector2Int g = gridManager.GetGridPosition(bc.transform.position);
            if (t.CompareTag("SeaBlock")) seaBlocks.Add(g);
            else if (t.CompareTag("LakeBlock")) lakeBlocks.Add(g);
            else if (t.CompareTag("LandBlock")) landBlocks.Add(g);
        }

        // 1b) find “interior” land‐blocks (no water neighbour)
        interiorLandBlocks = new HashSet<Vector2Int>();
        var blockDirs = new[] {
            new Vector2Int( 0,  blockSize), new Vector2Int( 0, -blockSize),
            new Vector2Int( blockSize, 0),  new Vector2Int(-blockSize, 0),
            new Vector2Int( blockSize,  blockSize), new Vector2Int(-blockSize,  blockSize),
            new Vector2Int( blockSize, -blockSize), new Vector2Int(-blockSize, -blockSize)
        };

        foreach (var lb in landBlocks)
        {
            bool hasWaterNeighbour = false;
            foreach (var d in blockDirs)
            {
                var nb = lb + d;
                if (seaBlocks.Contains(nb) || lakeBlocks.Contains(nb))
                {
                    hasWaterNeighbour = true;
                    break;
                }
            }
            if (!hasWaterNeighbour)
                interiorLandBlocks.Add(lb);
        }
    }

    private IEnumerator FillWaterBlocks(
        HashSet<Vector2Int> blocks,
        GameObject prefab,
        int blockSize,
        int size,
        float cell)
    {
        int spawnedThisBatch = 0;

        foreach (var g in blocks)
        {
            Vector3 wMin = gridManager.GetWorldPosition(
                g.x - blockSize / 2,
                g.y - blockSize / 2);

            for (int x = 0; x < blockSize; x += size)
            {
                for (int y = 0; y < blockSize; y += size)
                {
                    Vector3 mid = wMin + new Vector3(
                        x + size * 0.5f,
                        0,
                        y + size * 0.5f) * cell;

                    GameObject go = Instantiate(prefab, mid, Quaternion.identity, transform);
                    spawned.Add(go);

                    Vector2Int originCell = new Vector2Int(
                        g.x - blockSize / 2 + x,
                        g.y - blockSize / 2 + y);

                    MarkCells(go, originCell);

                    if (prefab == oceanTilePrefab)
                        oceanCells.Add(originCell);

                    spawnedThisBatch++;
                    if (spawnedThisBatch >= 50)
                    {
                        spawnedThisBatch = 0;
                        yield return null;
                    }
                }
            }
        }
    }

    void BuildCoastLines(
        HashSet<Vector2Int> seaBlocks,
        HashSet<Vector2Int> lakeBlocks,
        List<Vector2Int> landBlocks,
        int blockSize,
        int size,
        float cell)
    {
        /* -----------------------------------------------------------
           local helpers (only used inside this method)
           ----------------------------------------------------------- */
        void TryEdge(Vector3 wMin, Vector2Int gLand,
                    int lx, int ly, GameObject prefab)
        {
            var origin = new Vector2Int(
                gLand.x - blockSize / 2 + lx,
                gLand.y - blockSize / 2 + ly);

            if (!CanPlaceTile(origin.x, origin.y,
                              new Vector2Int(size, size)))
                return;

            Vector3 mid = wMin + new Vector3(
                            lx + size * 0.5f, 0,
                            ly + size * 0.5f) * cell;

            GameObject go = Instantiate(prefab, mid,
                                        Quaternion.identity, transform);
            spawned.Add(go);
            MarkCells(go, origin);

            beachCells.Add(origin); // both beach & lake-edge
        }

        void TryCorner(Vector3 wMin, Vector2Int gLand,
                       int lx, int ly, GameObject prefab)
        {
            var origin = new Vector2Int(
                gLand.x - blockSize / 2 + lx,
                gLand.y - blockSize / 2 + ly);

            if (!CanPlaceTile(origin.x, origin.y,
                              new Vector2Int(size, size)))
                return;

            Vector3 mid = wMin + new Vector3(
                            lx + size * 0.5f, 0,
                            ly + size * 0.5f) * cell;

            GameObject go = Instantiate(prefab, mid,
                                        Quaternion.identity, transform);
            spawned.Add(go);
            MarkCells(go, origin);
            coastCornerCells.Add(origin);
        }

        /* -----------------------------------------------------------
           main sweep of every land-block
           ----------------------------------------------------------- */
        Vector2Int[] blockDirs =
        {
            new(blockSize, 0),   new(-blockSize, 0),
            new(0,  blockSize),  new(0, -blockSize),
            new(blockSize,  blockSize),  new(-blockSize,  blockSize),
            new(blockSize, -blockSize),  new(-blockSize, -blockSize)
        };

        foreach (var gLand in landBlocks)
        {
            Vector3 wMin = gridManager.GetWorldPosition(
                            gLand.x - blockSize / 2,
                            gLand.y - blockSize / 2);

            foreach (var d in blockDirs)
            {
                Vector2Int nb = gLand + d;
                bool sea = seaBlocks.Contains(nb);
                bool lake = lakeBlocks.Contains(nb);
                if (!sea && !lake) continue;

                GameObject edgePrefab = sea ? beachTilePrefab : lakeEdgePrefab;
                GameObject cornerPrefab = sea ? beachCornerPrefab : lakeEdgeCornerPrefab;

                bool diag = d.x != 0 && d.y != 0;

                if (diag)
                {
                    int lx = d.x > 0 ? blockSize - size : 0;
                    int ly = d.y > 0 ? blockSize - size : 0;
                    TryCorner(wMin, gLand, lx, ly, cornerPrefab);
                }
                else
                {
                    bool horiz = d.x != 0;
                    for (int i = 0; i < blockSize; i += size)
                    {
                        int lx = horiz ? (d.x > 0 ? blockSize - size : 0) : i;
                        int ly = horiz ? i : (d.y > 0 ? blockSize - size : 0);
                        TryEdge(wMin, gLand, lx, ly, edgePrefab);
                    }
                }
            }
        }
    }

    void BuildRivers(
        HashSet<Vector2Int> interiorLandBlocks,
        int blockSize,
        int size,
        float cell)
    {
        // --- basic terrain checks ---------------------------------------

        bool Coast(Vector2Int p) =>
            beachCells.Contains(p);

        bool CoastCorner(Vector2Int p) =>
            coastCornerCells.Contains(p);

        bool Free(Vector2Int p)
        {
            if (p.x < 0 || p.y < 0 ||
                p.x + size > gridManager.columns ||
                p.y + size > gridManager.rows)
                return false;

            if (Coast(p) || CoastCorner(p)) return false;

            for (int dx = 0; dx < size; dx++)
                for (int dy = 0; dy < size; dy++)
                    if (gridManager.IsCellOccupied(p.x + dx, p.y + dy))
                        return false;

            return true;
        }

        bool AdjacentToCoast(Vector2Int p)
        {
            var n4 = new[] {
                new Vector2Int(p.x + size, p.y),
                new Vector2Int(p.x - size, p.y),
                new Vector2Int(p.x,        p.y + size),
                new Vector2Int(p.x,        p.y - size)
            };
            return n4.Any(n => Coast(n) || CoastCorner(n));
        }

        bool AdjacentToEdge(Vector2Int p)
        {
            return p.x <= 0
                || p.y <= 0
                || p.x + size >= gridManager.columns
                || p.y + size >= gridManager.rows;
        }

        // register a placed river tile’s cells & block
        void RegisterRiverCells(Vector2Int origin)
        {
            for (int dx = 0; dx < size; dx++)
                for (int dy = 0; dy < size; dy++)
                    riverCells.Add(new Vector2Int(origin.x + dx, origin.y + dy));

            riverBlocks.Add(new Vector2Int(
                origin.x / blockSize,
                origin.y / blockSize));
        }

        Quaternion Rot(bool vertical) =>
            vertical
                ? Quaternion.identity
                : Quaternion.Euler(0, 90, 0);

        Vector2Int Step(Vector2Int cur, bool vertical, bool positive) =>
            vertical
                ? new Vector2Int(cur.x, cur.y + (positive ? size : -size))
                : new Vector2Int(cur.x + (positive ? size : -size), cur.y);

        // can we carve water here? ignore the provided block
        bool CanPlaceRiverAt(Vector2Int pos, Vector2Int ignoreBlock)
        {
            if (!Free(pos)) return false;
            var b = new Vector2Int(pos.x / blockSize, pos.y / blockSize);
            if (b != ignoreBlock && riverBlocks.Contains(b))
                return false;
            return true;
        }

        // now ignores adjacency to the single 'ignore' cell
        bool AdjacentToRiverAnywhere(Vector2Int p, Vector2Int ignore)
        {
            var n4 = new[] {
                new Vector2Int(p.x + size, p.y),
                new Vector2Int(p.x - size, p.y),
                new Vector2Int(p.x,        p.y + size),
                new Vector2Int(p.x,        p.y - size)
            };
            foreach (var n in n4)
            {
                if (n == ignore) continue;
                if (riverCells.Contains(n))
                    return true;
            }
            return false;
        }

        // returns true if the given block contains any coast / corner cells
        bool BlockHasCoastBlock(Vector2Int block)
        {
            int bx = block.x * blockSize;
            int by = block.y * blockSize;
            for (int dx = 0; dx < blockSize; dx++)
                for (int dy = 0; dy < blockSize; dy++)
                {
                    var cell = new Vector2Int(bx + dx, by + dy);
                    if (beachCells.Contains(cell) || coastCornerCells.Contains(cell))
                        return true;
                }
            return false;
        }

        bool TryTurn(
            ref bool vertical,
            ref bool positive,
            Vector2Int current,
            TurnDirection prevTurn,
            out TurnDirection chosen,
            TurnDirection forced = TurnDirection.None)
        {
            chosen = TurnDirection.None;

            // figure out which block we're in right now
            var currBlock = new Vector2Int(
                current.x / blockSize,
                current.y / blockSize
            );

            // if this block has any coast/corner, do NOT turn in it
            if (BlockHasCoastBlock(currBlock))
                return false;

            foreach (var turn in new[] { TurnDirection.Right, TurnDirection.Left })
            {
                if (turn == prevTurn) continue;
                if (forced != TurnDirection.None && turn != forced) continue;

                bool v2 = !vertical;
                bool d2 = (turn == TurnDirection.Right)
                            ? (vertical ? positive : !positive)
                            : (vertical ? !positive : positive);

                var next = Step(current, v2, d2);

                if (CanPlaceRiverAt(next, currBlock)
                    && !AdjacentToCoast(next)
                    && !AdjacentToRiverAnywhere(next, current)
                    && !AdjacentToEdge(next))
                {
                    vertical = v2;
                    positive = d2;
                    chosen = turn;
                    return true;
                }
            }

            return false;
        }

        void GrowBranch(
            Vector2Int origin,
            bool vertical,
            bool positive,
            TurnDirection firstForced)
        {
            var cur = origin;
            int straights = UnityEngine.Random.Range(3, 4);
            bool needForced = true;
            var prevTurn = TurnDirection.None;

            while (true)
            {
                var currBlock = new Vector2Int(cur.x / blockSize, cur.y / blockSize);
                var next = Step(cur, vertical, positive);

                if (!CanPlaceRiverAt(next, currBlock))
                    break;

                // carve the next tile
                var go = Instantiate(riverTilePrefab, TileCentre(next), Rot(vertical), transform);
                spawned.Add(go);
                MarkCells(go, next);
                RegisterRiverCells(next);
                cur = next;

                // stop if we hit coast/corner or edge
                if (AdjacentToCoast(cur) || AdjacentToEdge(cur))
                    break;

                // --- SPLIT LOGIC ---
                if (UnityEngine.Random.value < branchSplitChance)
                {
                    // try both left and right splits
                    foreach (var turn in new[] { TurnDirection.Right, TurnDirection.Left })
                    {
                        bool sv = !vertical;
                        bool sd = (turn == TurnDirection.Right)
                                    ? (vertical ? positive : !positive)
                                    : (vertical ? !positive : positive);

                        var splitPos = Step(cur, sv, sd);
                        var splitBlock = new Vector2Int(splitPos.x / blockSize,
                                                        splitPos.y / blockSize);

                        // only split if it moves into a new block,
                        // is placeable, not adjacent to coast/edge,
                        // and not adjacent to any existing river cell except the parent tile
                        if (splitBlock != currBlock
                            && CanPlaceRiverAt(splitPos, currBlock)
                            && !AdjacentToCoast(splitPos)
                            && !AdjacentToEdge(splitPos)
                            && !AdjacentToRiverAnywhere(splitPos, cur))
                        {
                            // launch the new branch (parent keeps going)
                            GrowBranch(cur, sv, sd, firstForced);
                            break; // only one split per tile
                        }
                    }
                }

                // try turning after enough straights
                if (--straights <= 0)
                {
                    TurnDirection bend;
                    if (TryTurn(
                            ref vertical,
                            ref positive,
                            cur,
                            prevTurn,
                            out bend,
                            needForced ? firstForced : TurnDirection.None))
                    {
                        prevTurn = bend;
                        needForced = false;
                        straights = UnityEngine.Random.Range(2, 4);
                    }
                }
            }
        }

        // --- pick a valid start (not at edge or next to coast/corner) ----

        var pool = new List<Vector2Int>();
        for (int gx = 0; gx < gridManager.columns; gx += size)
            for (int gy = 0; gy < gridManager.rows; gy += size)
            {
                var p = new Vector2Int(gx, gy);

                if (p.x <= 0 || p.y <= 0 ||
                    p.x + size >= gridManager.columns ||
                    p.y + size >= gridManager.rows)
                    continue;

                if (AdjacentToCoast(p)) continue;

                var mid = new Vector2Int(
                    (p.x / blockSize) * blockSize + blockSize / 2,
                    (p.y / blockSize) * blockSize + blockSize / 2);
                if (!interiorLandBlocks.Contains(mid)) continue;

                if (Free(p))
                    pool.Add(p);
            }

        if (pool.Count == 0) return;

        var start = pool[UnityEngine.Random.Range(0, pool.Count)];
        bool startV = false;
        var firstGO = Instantiate(
            riverTilePrefab,
            TileCentre(start),
            Rot(startV),
            transform);
        spawned.Add(firstGO);
        MarkCells(firstGO, start);
        RegisterRiverCells(start);

        GrowBranch(start, startV, true, TurnDirection.Right);
        GrowBranch(start, startV, false, TurnDirection.Left);
    }

    void FillInteriorLand(List<Vector2Int> landBlocks, int blockSize)
    {
        /* -- 5a) collect every free cell inside every land-block -- */
        var available = new List<Vector2Int>();
        int cols = gridManager.columns, rows = gridManager.rows;
        int size = Mathf.Max(1, tileFootprint);

        foreach (var blockCentre in landBlocks)
        {
            int bx = blockCentre.x - blockSize / 2;
            int by = blockCentre.y - blockSize / 2;

            for (int dx = 0; dx < blockSize; dx++)
                for (int dy = 0; dy < blockSize; dy++)
                {
                    var pos = new Vector2Int(bx + dx, by + dy);
                    if (pos.x < 0 || pos.y < 0 || pos.x >= cols || pos.y >= rows) continue;
                    if (!gridManager.IsCellOccupied(pos.x, pos.y))
                        available.Add(pos);
                }
        }

        /* -- 5b) one weighted list of prefab assets ------------------ */
        var weightedPrefabs = CreateWeightedTileList();
        Shuffle(weightedPrefabs);

        /* -- 5c) shuffle free positions so placement feels organic ---- */
        Shuffle(available);

        /* -- 5d) iterate until every cell is filled ------------------- */
        int prefabIndex = 0;
        while (available.Count > 0)
        {
            if (prefabIndex >= weightedPrefabs.Count)
            {
                weightedPrefabs = CreateWeightedTileList();
                Shuffle(weightedPrefabs);
                prefabIndex = 0;
            }

            var prefab = weightedPrefabs[prefabIndex++];
            var pos = available[0];

            var bc = prefab.GetComponent<BoxCollider>();
            if (bc == null)
            {
                available.RemoveAt(0);
                continue;
            }

            int sx = Mathf.CeilToInt(bc.size.x / gridManager.cellSize);
            int sy = Mathf.CeilToInt(bc.size.z / gridManager.cellSize);
            var footprint = new Vector2Int(sx, sy);

            if (!CanPlaceTile(pos.x, pos.y, footprint))
                continue; // try next prefab but keep the same cell

            /* compute world position (centre of the collider) */
            Vector3 wp = gridManager.GetWorldPosition(pos.x, pos.y);
            wp.x += sx * gridManager.cellSize * 0.5f;
            wp.z += sy * gridManager.cellSize * 0.5f;

            var inst = Instantiate(prefab,
                                   wp,
                                   Quaternion.Euler(0, 90 * UnityEngine.Random.Range(0, 4), 0),
                                   transform);
            spawned.Add(inst);

            /* mark every covered grid cell as occupied and remove from list */
            for (int ix = 0; ix < sx; ix++)
                for (int iy = 0; iy < sy; iy++)
                {
                    var cell = new Vector2Int(pos.x + ix, pos.y + iy);
                    gridManager.MarkCellOccupied(cell.x, cell.y);
                    available.Remove(cell); // safe even if it doesn’t exist
                }
        }
    }

    private List<GameObject> CreateWeightedTileList()
    {
        var list = new List<GameObject>();

        int SizeBias(TileSize s)
            => EnvironmentPresetManager.Instance
                ? Mathf.Max(0, EnvironmentPresetManager.Instance.GetTileSizeWeight(s))
                : 1;

        foreach (var tp in tilePrefabs)
        {
            if (tp == null || tp.prefab == null) continue;

            int baseWeight = Mathf.Max(0, tp.spawnWeight);
            int sizeWeight = SizeBias(tp.tileSize);

            int finalWeight = baseWeight * sizeWeight;

            for (int i = 0; i < finalWeight; i++)
                list.Add(tp.prefab);
        }

        // Safety fallback: if everything got weighted to 0, allow at least something
        if (list.Count == 0)
        {
            foreach (var tp in tilePrefabs)
                if (tp != null && tp.prefab != null)
                    list.Add(tp.prefab);
        }

        return list;
    }

    // Fisher–Yates
    private void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int randomIndex = UnityEngine.Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }

    void MarkCells(GameObject go, Vector2Int origin)
    {
        var bc = go.GetComponent<BoxCollider>();
        if (!bc) return;

        /* use the GridManager’s cell-size directly */
        float cellSize = gridManager.cellSize;

        int sx = Mathf.CeilToInt(bc.size.x / cellSize);
        int sy = Mathf.CeilToInt(bc.size.z / cellSize);

        for (int dx = 0; dx < sx; dx++)
            for (int dy = 0; dy < sy; dy++)
                gridManager.MarkCellOccupied(origin.x + dx,
                                             origin.y + dy);
    }

    private bool CanPlaceTile(int x, int y, Vector2Int size)
    {
        if (x < 0 || y < 0 ||
            x + size.x > gridManager.columns ||
            y + size.y > gridManager.rows)
            return false;

        for (int dx = 0; dx < size.x; dx++)
            for (int dy = 0; dy < size.y; dy++)
                if (gridManager.IsCellOccupied(x + dx, y + dy))
                    return false;

        return true;
    }

    void OnDisable()
    {
        if (mapGenerator != null)
            mapGenerator.ClearBlockColliders();

        _placementRoutine = null;
    }

    public static void ResetWorldReady()
    {
        WorldReady = false;
    }

    public static void SetWorldReady(bool ready)
    {
        WorldReady = ready;
    }

    public void ClearPlacedTilesAndState()
    {
        if (_placementRoutine != null)
        {
            StopCoroutine(_placementRoutine);
            _placementRoutine = null;
        }

        WorldReady = false;

        // Destroy everything this placer created.
        HashSet<GameObject> toDestroy = new HashSet<GameObject>();

        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null)
                toDestroy.Add(spawned[i]);
        }

        // Fallback: also clear any child tile objects under this placer.
        foreach (Transform child in transform)
        {
            if (child != null)
                toDestroy.Add(child.gameObject);
        }

        foreach (GameObject go in toDestroy)
        {
            if (go != null)
                Destroy(go);
        }

        spawned.Clear();
        beachCells.Clear();
        oceanCells.Clear();
        coastCornerCells.Clear();
        riverCells.Clear();
        riverBlocks.Clear();

        if (gridManager != null)
            gridManager.InitializeGrid();
    }

    /* =================================================================
       DEBUG / VISUALISATION
       =================================================================*/
    // #if UNITY_EDITOR
    //     void OnDrawGizmos()
    //     {
    //         if (!Application.isPlaying || gizmoMarks.Count == 0 || !gridManager)
    //             return;

    //         foreach (var m in gizmoMarks)
    //         {
    //             Vector3 pos = TileCentre(m.g) + Vector3.up * 0.05f;

    //             switch (m.t)
    //             {
    //                 case TurnDirection.None:  Gizmos.color = Color.green; break; // S
    //                 case TurnDirection.Left:  Gizmos.color = Color.red;   break; // L
    //                 case TurnDirection.Right: Gizmos.color = Color.blue;  break; // R
    //             }

    //             Gizmos.DrawWireCube(pos, Vector3.one * gridManager.cellSize * 0.9f);

    //             string label = m.t == TurnDirection.None ? "S"
    //                          : m.t == TurnDirection.Left  ? "L" : "R";

    //             Handles.Label(pos + Vector3.up * 0.1f,
    //                           label,
    //                           new GUIStyle
    //                           {
    //                               normal     = new GUIStyleState { textColor = Gizmos.color },
    //                               alignment  = TextAnchor.MiddleCenter,
    //                               fontStyle  = FontStyle.Bold
    //                           });
    //         }
    //     }
    // #endif
}
