using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    [Header("Grid Reference")]
    public GridManager gridManager;

    [Header("Noise Settings")]
    public int seed = 0;
    public float noiseScale = 0.1f;
    [Range(1, 8)] public int octaves = 4;
    public float persistence = 0.5f;
    public float lacunarity = 2f;

    [Header("Threshold & Cleanup")]
    [Range(0f, 1f)] public float landThreshold = 0.5f;
    [Range(4, 8)] public int flipLimit = 5;

    [Header("Draw Settings")]
    [Tooltip("Size of each block in grid cells, e.g. 4 = 4x4.")]
    public int blockSize = 4;
    public Color landColor = Color.green;
    public Color seaColor = Color.blue;
    public Color lakeColor = Color.magenta;

    [Header("Coroutine Settings")]
    [SerializeField] private int cellsPerBatch = 2048;
    [SerializeField] private int blocksPerBatch = 128;

    private Vector2 noiseOffset;

    public bool[,] map { get; private set; }

    private TerrainBlockKind[,] blockTerrain;
    private readonly List<GameObject> blockColliders = new List<GameObject>();

    public event System.Action OnMapGenerationStarted;
    public event System.Action OnMapGenerationCompleted;

    public bool IsReadyForFaultLines
    {
        get
        {
            return !IsGenerating &&
                   HasBlockTerrainData &&
                   gridManager != null &&
                   BlockColumns > 0 &&
                   BlockRows > 0;
        }
    }

    public bool IsGenerating { get; private set; }

    public int BlockColumns
    {
        get
        {
            if (gridManager == null) return 0;
            return Mathf.CeilToInt(gridManager.columns / (float)Mathf.Max(1, blockSize));
        }
    }

    public int BlockRows
    {
        get
        {
            if (gridManager == null) return 0;
            return Mathf.CeilToInt(gridManager.rows / (float)Mathf.Max(1, blockSize));
        }
    }

    public bool HasBlockTerrainData => blockTerrain != null;

    public IEnumerator RegenerateCoroutine()
    {
        if (IsGenerating)
        {
            Debug.LogWarning("MapGenerator is already generating.");
            yield break;
        }

        IsGenerating = true;
        OnMapGenerationStarted?.Invoke();

        if (!PrepareGeneration(out int W, out int H))
        {
            IsGenerating = false;
            yield break;
        }

        yield return null;

        if (landThreshold <= 0f)
        {
            yield return StartCoroutine(FillAllLandCoroutine(W, H));
        }
        else
        {
            yield return StartCoroutine(GenerateNoiseMapCoroutine(W, H));
            yield return StartCoroutine(SmoothMapCoroutine(W, H));
        }

        yield return StartCoroutine(CreateBlockCollidersCoroutine());

        IsGenerating = false;
        OnMapGenerationCompleted?.Invoke();
    }

    private bool PrepareGeneration(out int W, out int H)
    {
        W = 0;
        H = 0;

        var preset = EnvironmentPresetManager.Instance?.GetCurrentPreset();
        if (preset != null && preset.planetarySection != null)
            ApplyPlanetaryOverrides(preset.planetarySection);

        if (gridManager == null)
        {
            Debug.LogWarning("MapGenerator: GridManager is missing.");
            return false;
        }

        gridManager.InitializeGrid();
        ClearBlockColliders();

        W = gridManager.columns;
        H = gridManager.rows;

        map = new bool[W, H];
        blockTerrain = null;

        seed = UnityEngine.Random.Range(0, int.MaxValue);
        Random.InitState(seed);
        noiseOffset = new Vector2(Random.value * 1000f, Random.value * 1000f);

        return true;
    }

    private IEnumerator FillAllLandCoroutine(int W, int H)
    {
        int processed = 0;
        int batch = Mathf.Max(1, cellsPerBatch);

        for (int x = 0; x < W; x++)
        {
            for (int y = 0; y < H; y++)
            {
                map[x, y] = true;

                processed++;
                if (processed >= batch)
                {
                    processed = 0;
                    yield return null;
                }
            }
        }
    }

    private IEnumerator GenerateNoiseMapCoroutine(int W, int H)
    {
        int processed = 0;
        int batch = Mathf.Max(1, cellsPerBatch);

        for (int x = 0; x < W; x++)
        {
            for (int y = 0; y < H; y++)
            {
                float v = 0f;
                float amp = 1f;
                float freq = noiseScale;
                float maxA = 0f;

                for (int o = 0; o < octaves; o++)
                {
                    float p = Mathf.PerlinNoise((x + noiseOffset.x) * freq, (y + noiseOffset.y) * freq);
                    v += (p * 2f - 1f) * amp;
                    maxA += amp;
                    amp *= persistence;
                    freq *= lacunarity;
                }

                map[x, y] = (v / maxA) > (landThreshold * 2f - 1f);

                processed++;
                if (processed >= batch)
                {
                    processed = 0;
                    yield return null;
                }
            }
        }
    }

    private IEnumerator SmoothMapCoroutine(int W, int H)
    {
        bool[,] tmp = (bool[,])map.Clone();

        int processed = 0;
        int batch = Mathf.Max(1, cellsPerBatch);

        for (int x = 0; x < W; x++)
        {
            for (int y = 0; y < H; y++)
            {
                int landC = 0;
                int seaC = 0;

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;

                        int nx = x + dx;
                        int ny = y + dy;

                        if (nx < 0 || nx >= W || ny < 0 || ny >= H) continue;

                        if (map[nx, ny]) landC++;
                        else seaC++;
                    }
                }

                if (map[x, y] && seaC >= flipLimit) tmp[x, y] = false;
                else if (!map[x, y] && landC >= flipLimit) tmp[x, y] = true;

                processed++;
                if (processed >= batch)
                {
                    processed = 0;
                    yield return null;
                }
            }
        }

        map = tmp;
    }

    public void ClearBlockColliders()
    {
        foreach (var go in blockColliders)
        {
            if (go != null)
                Destroy(go);
        }

        blockColliders.Clear();
    }

    private IEnumerator CreateBlockCollidersCoroutine()
    {
        if (gridManager == null || map == null)
            yield break;

        float cell = gridManager.cellSize;
        Vector3 origin = Vector3.zero;
        int W = gridManager.columns;
        int H = gridManager.rows;

        int bW = BlockColumns;
        int bH = BlockRows;

        bool[,] blockLand = new bool[bW, bH];
        bool[,] blockSea = new bool[bW, bH];
        bool[,] blockLake = new bool[bW, bH];
        bool[,] visited = new bool[bW, bH];

        blockTerrain = new TerrainBlockKind[bW, bH];

        int processed = 0;
        int batch = Mathf.Max(1, blocksPerBatch);

        for (int bx = 0; bx < bW; bx++)
        {
            for (int by = 0; by < bH; by++)
            {
                int landC = 0;
                int tot = 0;

                for (int i = 0; i < blockSize; i++)
                {
                    for (int j = 0; j < blockSize; j++)
                    {
                        int x = bx * blockSize + i;
                        int y = by * blockSize + j;
                        if (x >= W || y >= H) continue;

                        tot++;
                        if (map[x, y]) landC++;
                    }
                }

                blockLand[bx, by] = landC > tot / 2;
                blockSea[bx, by] = !blockLand[bx, by];

                processed++;
                if (processed >= batch)
                {
                    processed = 0;
                    yield return null;
                }
            }
        }

        processed = 0;

        for (int bx = 0; bx < bW; bx++)
        {
            for (int by = 0; by < bH; by++)
            {
                if (visited[bx, by] || blockLand[bx, by]) continue;

                Queue<Vector2Int> q = new Queue<Vector2Int>();
                List<Vector2Int> region = new List<Vector2Int>();
                bool touchesEdge = false;

                visited[bx, by] = true;
                q.Enqueue(new Vector2Int(bx, by));

                while (q.Count > 0)
                {
                    Vector2Int c = q.Dequeue();
                    region.Add(c);

                    if (c.x == 0 || c.y == 0 || c.x == bW - 1 || c.y == bH - 1)
                        touchesEdge = true;

                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0) continue;

                            int nx = c.x + dx;
                            int ny = c.y + dy;

                            if (nx < 0 || nx >= bW || ny < 0 || ny >= bH) continue;

                            if (!visited[nx, ny] && blockSea[nx, ny])
                            {
                                visited[nx, ny] = true;
                                q.Enqueue(new Vector2Int(nx, ny));
                            }
                        }
                    }

                    processed++;
                    if (processed >= batch)
                    {
                        processed = 0;
                        yield return null;
                    }
                }

                if (!touchesEdge)
                {
                    foreach (var c in region)
                        blockLake[c.x, c.y] = true;
                }
            }
        }

        processed = 0;

        for (int bx = 0; bx < bW; bx++)
        {
            for (int by = 0; by < bH; by++)
            {
                TerrainBlockKind kind;

                if (blockLand[bx, by]) kind = TerrainBlockKind.Land;
                else if (blockLake[bx, by]) kind = TerrainBlockKind.Lake;
                else kind = TerrainBlockKind.Sea;

                blockTerrain[bx, by] = kind;

                string tag = GetTagForTerrainKind(kind);

                float size = cell * blockSize;
                Vector3 center = origin + new Vector3(
                    (bx * blockSize + blockSize * 0.5f) * cell,
                    0,
                    (by * blockSize + blockSize * 0.5f) * cell
                );

                GameObject block = new GameObject($"Block_{bx}_{by}");
                block.transform.position = center;
                block.transform.parent = transform;
                block.tag = tag;

                BoxCollider collider = block.AddComponent<BoxCollider>();
                float visualSize = size * 0.9875f;
                collider.size = new Vector3(visualSize, 1f, visualSize);
                collider.center = new Vector3(0, 0.5f, 0);

                blockColliders.Add(block);

                processed++;
                if (processed >= batch)
                {
                    processed = 0;
                    yield return null;
                }
            }
        }
    }

    private string GetTagForTerrainKind(TerrainBlockKind kind)
    {
        switch (kind)
        {
            case TerrainBlockKind.Land:
                return "LandBlock";

            case TerrainBlockKind.Lake:
                return "LakeBlock";

            case TerrainBlockKind.Sea:
            default:
                return "SeaBlock";
        }
    }

    public bool IsValidBlock(Vector2Int block)
    {
        return block.x >= 0 && block.y >= 0 && block.x < BlockColumns && block.y < BlockRows;
    }

    public bool TryGetBlockTerrain(Vector2Int block, out TerrainBlockKind kind)
    {
        kind = TerrainBlockKind.Sea;

        if (blockTerrain == null || !IsValidBlock(block))
            return false;

        kind = blockTerrain[block.x, block.y];
        return true;
    }

    public TerrainBlockKind GetBlockTerrainOrDefault(Vector2Int block, TerrainBlockKind fallback = TerrainBlockKind.Sea)
    {
        if (TryGetBlockTerrain(block, out TerrainBlockKind kind))
            return kind;

        return fallback;
    }

    public bool TrySetBlockTerrain(Vector2Int block, TerrainBlockKind kind, bool updateCellMap = true)
    {
        if (blockTerrain == null || !IsValidBlock(block))
            return false;

        blockTerrain[block.x, block.y] = kind;

        if (updateCellMap && map != null)
            StampBlockIntoCellMap(block, kind);

        return true;
    }

    private void StampBlockIntoCellMap(Vector2Int block, TerrainBlockKind kind)
    {
        bool isLandLike = kind == TerrainBlockKind.Land;

        int startX = block.x * blockSize;
        int startY = block.y * blockSize;
        int endX = Mathf.Min(startX + blockSize, gridManager.columns);
        int endY = Mathf.Min(startY + blockSize, gridManager.rows);

        for (int x = startX; x < endX; x++)
        {
            for (int y = startY; y < endY; y++)
                map[x, y] = isLandLike;
        }
    }

    public Vector3 GetBlockWorldCenter(Vector2Int block)
    {
        float cell = gridManager != null ? gridManager.cellSize : 1f;

        return new Vector3(
            (block.x * blockSize + blockSize * 0.5f) * cell,
            0f,
            (block.y * blockSize + blockSize * 0.5f) * cell
        );
    }

    public Vector2Int GetBlockMinCell(Vector2Int block)
    {
        return new Vector2Int(block.x * blockSize, block.y * blockSize);
    }

    public Vector2Int GetBlockFromCell(Vector2Int cell)
    {
        int safeBlockSize = Mathf.Max(1, blockSize);
        return new Vector2Int(cell.x / safeBlockSize, cell.y / safeBlockSize);
    }

    public Vector2Int GetBlockFromWorld(Vector3 worldPosition)
    {
        if (gridManager == null)
            return Vector2Int.zero;

        Vector2Int cell = gridManager.GetGridPosition(worldPosition);
        return GetBlockFromCell(cell);
    }

    public void GetBlocksInRadius(Vector2Int center, float radiusBlocks, List<Vector2Int> results)
    {
        results.Clear();

        int r = Mathf.CeilToInt(radiusBlocks);

        for (int x = center.x - r; x <= center.x + r; x++)
        {
            for (int y = center.y - r; y <= center.y + r; y++)
            {
                Vector2Int b = new Vector2Int(x, y);
                if (!IsValidBlock(b)) continue;

                float d = Vector2Int.Distance(center, b);
                if (d <= radiusBlocks)
                    results.Add(b);
            }
        }
    }

    public void AddBlockAndNeighbours(Vector2Int block, HashSet<Vector2Int> results)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                Vector2Int n = new Vector2Int(block.x + dx, block.y + dy);
                if (IsValidBlock(n))
                    results.Add(n);
            }
        }
    }

    public void ApplyPlanetaryOverrides(PlanetarySectionSettings s)
    {
        if (s == null) return;

        noiseScale = s.noiseScale;
        octaves = Mathf.Clamp(s.octaves, 1, 8);
        persistence = Mathf.Clamp01(s.persistence);
        lacunarity = Mathf.Max(0.0001f, s.lacunarity);

        landThreshold = Mathf.Clamp01(s.landThreshold);
        flipLimit = Mathf.Clamp(s.flipLimit, 4, 8);
    }

    private void OnDrawGizmosSelected()
    {
        if (gridManager == null || blockTerrain == null)
            return;

        float cell = gridManager.cellSize;
        float size = blockSize * cell;

        for (int x = 0; x < BlockColumns; x++)
        {
            for (int y = 0; y < BlockRows; y++)
            {
                TerrainBlockKind kind = blockTerrain[x, y];

                switch (kind)
                {
                    case TerrainBlockKind.Land:
                        Gizmos.color = new Color(landColor.r, landColor.g, landColor.b, 0.15f);
                        break;
                    case TerrainBlockKind.Sea:
                        Gizmos.color = new Color(seaColor.r, seaColor.g, seaColor.b, 0.15f);
                        break;
                    case TerrainBlockKind.Lake:
                        Gizmos.color = new Color(lakeColor.r, lakeColor.g, lakeColor.b, 0.15f);
                        break;
                }

                Vector3 center = GetBlockWorldCenter(new Vector2Int(x, y));
                Gizmos.DrawCube(center + Vector3.up * 0.03f, new Vector3(size, 0.05f, size));
            }
        }
    }
}