using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum EarthquakeFaultLineDirection
{
    Random = 0,
    Horizontal = 1,
    Vertical = 2,
    DiagonalNE = 3,
    DiagonalNW = 4
}

public class EarthquakeFaultLineGenerator : MonoBehaviour
{
    [Header("References")]
    public MapGenerator mapGenerator;
    public GridManager gridManager;

    [Header("Startup")]
    public bool generateOnStart = true;
    public bool waitForMapReady = true;

    [Tooltip("How many frames to wait before giving up. 0 = wait forever.")]
    [Min(0)] public int maxWaitFrames = 0;

    [Tooltip("If true, preset values overwrite inspector values.")]
    public bool usePresetSettings = true;

    [Header("Fault Line Settings")]
    public bool canHaveFaultLines = true;

    [Range(0f, 1f)]
    public float faultLineMapChance = 0.65f;

    [Min(0)] public int minFaultLines = 1;
    [Min(0)] public int maxFaultLines = 2;

    [Tooltip("1 = single block-wide line. 2+ = thicker fault band.")]
    [Min(1)] public int faultLineWidthBlocks = 1;

    [Tooltip("Extra blocks around the fault line counted as influence, not direct fault.")]
    [Min(0)] public int influenceExtraWidthBlocks = 1;

    [Range(0f, 1f)]
    public float faultLineWiggleChance = 0.35f;

    public EarthquakeFaultLineDirection direction = EarthquakeFaultLineDirection.Random;

    [Header("Map Generator Event Hook")]
    public bool generateWhenMapGenerationCompletes = true;
    public bool clearFaultsWhenMapGenerationStarts = true;

    private MapGenerator subscribedMapGenerator;
    private bool generatedForCurrentMap;

    [Header("Testing")]
    public bool regenerateWithGKey = true;
    public bool forceFaultLineForDebug = false;

    [Header("Debug")]
    public bool debugLogging = true;
    public bool drawGizmos = true;
    public Color faultColor = Color.red;
    public Color influenceColor = new Color(1f, 0.5f, 0f, 0.35f);
    public float gizmoHeight = 1.0f;

    [Tooltip("Multiplier based on block world size.")]
    public float gizmoScale = 0.75f;

    public bool HasFaults => faultBlocks.Count > 0;
    public HashSet<Vector2Int> FaultBlocks => faultBlocks;
    public HashSet<Vector2Int> FaultInfluenceBlocks => faultInfluenceBlocks;

    private readonly HashSet<Vector2Int> faultBlocks = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> faultInfluenceBlocks = new HashSet<Vector2Int>();

    private readonly List<Vector2Int> validBlockScratch = new List<Vector2Int>();
    private readonly List<Vector2Int> faultBlockScratch = new List<Vector2Int>();

    private Coroutine generateRoutine;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        RebindMapGeneratorEvents();
    }

    private void OnDisable()
    {
        UnbindMapGeneratorEvents();
    }

    private IEnumerator Start()
    {
        // Let bootstrap/additive scene references settle.
        yield return null;

        ResolveReferences();
        RebindMapGeneratorEvents();

        if (!generateOnStart)
            yield break;

        // Important:
        // Do not only wait for OnMapGenerationCompleted.
        // If that event already fired before this object subscribed, this coroutine
        // will still wait until the map is ready and generate automatically.
        generatedForCurrentMap = false;
        BeginGenerateFaultLinesFromPreset();

        if (debugLogging)
            Debug.Log("EarthquakeFaultLineGenerator: Started automatic wait-for-map fault generation.");
    }

    private void Update()
    {
        if (regenerateWithGKey && Input.GetKeyDown(KeyCode.G))
        {
            Debug.Log("EarthquakeFaultLineGenerator: Manual regenerate with G key.");
            BeginGenerateFaultLinesFromPreset();
        }
    }

    [ContextMenu("Generate Fault Lines From Preset")]
    public void BeginGenerateFaultLinesFromPreset()
    {
        if (generateRoutine != null)
            StopCoroutine(generateRoutine);

        generateRoutine = StartCoroutine(GenerateFaultLinesWhenReady());
    }

    [ContextMenu("Clear Fault Lines")]
    public void ClearFaultLines()
    {
        faultBlocks.Clear();
        faultInfluenceBlocks.Clear();

        MarkEarthquakeFaultSaveDirty();
    }

    private void RebindMapGeneratorEvents()
    {
        if (subscribedMapGenerator == mapGenerator)
            return;

        UnbindMapGeneratorEvents();

        subscribedMapGenerator = mapGenerator;

        if (subscribedMapGenerator != null)
        {
            subscribedMapGenerator.OnMapGenerationStarted += HandleMapGenerationStarted;
            subscribedMapGenerator.OnMapGenerationCompleted += HandleMapGenerationCompleted;

            if (debugLogging)
                Debug.Log($"EarthquakeFaultLineGenerator: Subscribed to {subscribedMapGenerator.name} map generation events.");
        }
    }

    private void UnbindMapGeneratorEvents()
    {
        if (subscribedMapGenerator == null)
            return;

        subscribedMapGenerator.OnMapGenerationStarted -= HandleMapGenerationStarted;
        subscribedMapGenerator.OnMapGenerationCompleted -= HandleMapGenerationCompleted;
        subscribedMapGenerator = null;
    }

    private void HandleMapGenerationStarted()
    {
        generatedForCurrentMap = false;

        if (clearFaultsWhenMapGenerationStarts)
            ClearFaultLines();

        if (debugLogging)
            Debug.Log("EarthquakeFaultLineGenerator: Map generation started, clearing old fault lines.");
    }

    private void HandleMapGenerationCompleted()
    {
        ResolveReferences();
        RebindMapGeneratorEvents();

        if (!generateOnStart || !generateWhenMapGenerationCompletes)
            return;

        if (generatedForCurrentMap)
            return;

        if (debugLogging)
            Debug.Log("EarthquakeFaultLineGenerator: Map generation completed, generating fault lines.");

        BeginGenerateFaultLinesFromPreset();
    }

    private IEnumerator GenerateFaultLinesWhenReady()
    {
        int waitedFrames = 0;

        while (true)
        {
            ResolveReferences();

            if (!waitForMapReady)
                break;

            if (IsMapReadyForFaultLines())
                break;

            waitedFrames++;

            if (maxWaitFrames > 0 && waitedFrames >= maxWaitFrames)
            {
                Debug.LogWarning(
                    $"EarthquakeFaultLineGenerator: Timed out after {waitedFrames} frames. " +
                    $"MapGenerator={(mapGenerator != null ? mapGenerator.name : "NULL")}, " +
                    $"GridManager={(gridManager != null ? gridManager.name : "NULL")}"
                );

                generateRoutine = null;
                yield break;
            }

            yield return null;
        }

        if (debugLogging)
        {
            Debug.Log(
                $"EarthquakeFaultLineGenerator: Map ready after {waitedFrames} frame(s). " +
                $"BlockColumns={mapGenerator.BlockColumns}, BlockRows={mapGenerator.BlockRows}, " +
                $"HasBlockTerrainData={mapGenerator.HasBlockTerrainData}"
            );
        }

        GenerateFaultLinesFromPreset();

        generateRoutine = null;
    }

    private bool IsMapReadyForFaultLines()
    {
        if (mapGenerator == null || gridManager == null)
            return false;

        if (gridManager.columns <= 0 || gridManager.rows <= 0)
            return false;

        if (mapGenerator.blockSize <= 0)
            return false;

        if (mapGenerator.IsGenerating)
            return false;

        if (!mapGenerator.HasBlockTerrainData)
            return false;

        if (mapGenerator.BlockColumns <= 0 || mapGenerator.BlockRows <= 0)
            return false;

        CountValidBlocksStrict(validBlockScratch);

        return validBlockScratch.Count > 0;
    }

    public void GenerateFaultLinesFromPreset()
    {
        ResolveReferences();

        if (usePresetSettings)
            ApplyPresetSettings();

        GenerateFaultLines();
    }

    public void GenerateFaultLines()
    {
        faultBlocks.Clear();
        faultInfluenceBlocks.Clear();

        ResolveReferences();

        if (mapGenerator == null || gridManager == null)
        {
            Debug.LogWarning("EarthquakeFaultLineGenerator: Missing MapGenerator or GridManager.");
            return;
        }

        if (!mapGenerator.HasBlockTerrainData)
        {
            Debug.LogWarning("EarthquakeFaultLineGenerator: MapGenerator has no block terrain data yet.");
            return;
        }

        if (!canHaveFaultLines)
        {
            if (debugLogging)
                Debug.Log("EarthquakeFaultLineGenerator: Fault lines disabled for this map.");

            return;
        }

        if (!forceFaultLineForDebug && Random.value > faultLineMapChance)
        {
            if (debugLogging)
                Debug.Log($"EarthquakeFaultLineGenerator: This map rolled no fault lines. Chance={faultLineMapChance:0.00}");

            return;
        }

        CountValidBlocksStrict(validBlockScratch);

        if (validBlockScratch.Count == 0)
        {
            Debug.LogWarning("EarthquakeFaultLineGenerator: No valid block indexes found after map was ready.");
            return;
        }

        int min = Mathf.Max(0, minFaultLines);
        int max = Mathf.Max(min, maxFaultLines);

        if (forceFaultLineForDebug)
        {
            min = Mathf.Max(1, min);
            max = Mathf.Max(min, max);
        }

        int count = Random.Range(min, max + 1);

        for (int i = 0; i < count; i++)
            GenerateSingleFaultLine();

        BuildInfluenceBlocks();

        if (debugLogging)
        {
            Debug.Log(
                $"EarthquakeFaultLineGenerator: Generated {count} fault line(s). " +
                $"FaultBlocks={faultBlocks.Count}, InfluenceBlocks={faultInfluenceBlocks.Count}"
            );
        }

        MarkEarthquakeFaultSaveDirty();
    }

    public Vector2Int GetRandomEpicentre()
    {
        if (faultBlocks.Count > 0)
        {
            faultBlockScratch.Clear();
            faultBlockScratch.AddRange(faultBlocks);
            return faultBlockScratch[Random.Range(0, faultBlockScratch.Count)];
        }

        CountValidBlocksStrict(validBlockScratch);

        if (validBlockScratch.Count > 0)
            return validBlockScratch[Random.Range(0, validBlockScratch.Count)];

        return Vector2Int.zero;
    }

    private void ApplyPresetSettings()
    {
        var preset = EnvironmentPresetManager.Instance != null
            ? EnvironmentPresetManager.Instance.GetCurrentPreset()
            : null;

        var section = preset != null ? preset.planetarySection : null;

        if (section == null)
        {
            if (debugLogging)
                Debug.Log("EarthquakeFaultLineGenerator: No preset section found. Using inspector settings.");

            return;
        }

        canHaveFaultLines = section.canHaveFaultLines;
        faultLineMapChance = section.faultLineMapChance;
        minFaultLines = section.minFaultLines;
        maxFaultLines = section.maxFaultLines;
        faultLineWidthBlocks = section.faultLineWidthBlocks;
        faultLineWiggleChance = section.faultLineWiggleChance;

        if (debugLogging)
        {
            Debug.Log(
                $"EarthquakeFaultLineGenerator: Applied preset settings. " +
                $"canHaveFaultLines={canHaveFaultLines}, chance={faultLineMapChance}, " +
                $"min={minFaultLines}, max={maxFaultLines}"
            );
        }
    }

    private void ResolveReferences()
    {
        if (gridManager == null)
            gridManager = GridManager.Instance;

        if (mapGenerator == null)
            mapGenerator = FindObjectOfType<MapGenerator>();
    }

    private void CountValidBlocksStrict(List<Vector2Int> results)
    {
        results.Clear();

        if (mapGenerator == null)
            return;

        for (int bx = 0; bx < mapGenerator.BlockColumns; bx++)
        {
            for (int by = 0; by < mapGenerator.BlockRows; by++)
            {
                Vector2Int block = new Vector2Int(bx, by);

                if (mapGenerator.IsValidBlock(block))
                    results.Add(block);
            }
        }
    }

    private void GenerateSingleFaultLine()
    {
        EarthquakeFaultLineDirection dir = direction;

        if (dir == EarthquakeFaultLineDirection.Random)
            dir = (EarthquakeFaultLineDirection)Random.Range(1, 5);

        Vector2Int start = PickStartBlock(dir);
        Vector2Int step = GetStepForDirection(dir);

        Vector2Int current = start;

        int maxSteps = Mathf.Max(mapGenerator.BlockColumns, mapGenerator.BlockRows) + 4;

        for (int i = 0; i < maxSteps; i++)
        {
            if (!IsValidFaultBlock(current))
            {
                current += step;
                continue;
            }

            AddFaultBlockWithWidth(current);

            if (Random.value < faultLineWiggleChance)
            {
                int wiggleSign = Random.value < 0.5f ? -1 : 1;
                current += GetPerpendicularStep(step) * wiggleSign;
            }

            current += step;

            if (current.x < 0 || current.y < 0 ||
                current.x >= mapGenerator.BlockColumns ||
                current.y >= mapGenerator.BlockRows)
            {
                break;
            }
        }
    }

    private bool IsValidFaultBlock(Vector2Int block)
    {
        return mapGenerator != null && mapGenerator.IsValidBlock(block);
    }

    private Vector2Int PickStartBlock(EarthquakeFaultLineDirection dir)
    {
        CountValidBlocksStrict(validBlockScratch);

        if (validBlockScratch.Count == 0)
            return Vector2Int.zero;

        int maxX = mapGenerator.BlockColumns - 1;
        int maxY = mapGenerator.BlockRows - 1;

        Vector2Int picked = PickRandomValidBlock();

        switch (dir)
        {
            case EarthquakeFaultLineDirection.Horizontal:
                return new Vector2Int(0, picked.y);

            case EarthquakeFaultLineDirection.Vertical:
                return new Vector2Int(picked.x, 0);

            case EarthquakeFaultLineDirection.DiagonalNE:
                return Random.value < 0.5f
                    ? new Vector2Int(0, picked.y)
                    : new Vector2Int(picked.x, 0);

            case EarthquakeFaultLineDirection.DiagonalNW:
                return Random.value < 0.5f
                    ? new Vector2Int(maxX, picked.y)
                    : new Vector2Int(picked.x, 0);

            default:
                return picked;
        }
    }

    private Vector2Int PickRandomValidBlock()
    {
        if (validBlockScratch.Count == 0)
            CountValidBlocksStrict(validBlockScratch);

        if (validBlockScratch.Count == 0)
            return Vector2Int.zero;

        return validBlockScratch[Random.Range(0, validBlockScratch.Count)];
    }

    private Vector2Int GetStepForDirection(EarthquakeFaultLineDirection dir)
    {
        switch (dir)
        {
            case EarthquakeFaultLineDirection.Horizontal:
                return new Vector2Int(1, 0);

            case EarthquakeFaultLineDirection.Vertical:
                return new Vector2Int(0, 1);

            case EarthquakeFaultLineDirection.DiagonalNE:
                return new Vector2Int(1, 1);

            case EarthquakeFaultLineDirection.DiagonalNW:
                return new Vector2Int(-1, 1);

            default:
                return new Vector2Int(1, 0);
        }
    }

    private Vector2Int GetPerpendicularStep(Vector2Int step)
    {
        if (step.x != 0 && step.y == 0)
            return new Vector2Int(0, 1);

        if (step.x == 0 && step.y != 0)
            return new Vector2Int(1, 0);

        return Random.value < 0.5f
            ? new Vector2Int(1, 0)
            : new Vector2Int(0, 1);
    }

    private void AddFaultBlockWithWidth(Vector2Int centre)
    {
        int width = Mathf.Max(1, faultLineWidthBlocks);
        int radius = Mathf.FloorToInt((width - 1) * 0.5f);

        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                Vector2Int b = new Vector2Int(
                    centre.x + dx,
                    centre.y + dy
                );

                if (IsValidFaultBlock(b))
                    faultBlocks.Add(b);
            }
        }

        if (IsValidFaultBlock(centre))
            faultBlocks.Add(centre);
    }

    private void BuildInfluenceBlocks()
    {
        faultInfluenceBlocks.Clear();

        int width = Mathf.Max(0, influenceExtraWidthBlocks);

        foreach (Vector2Int fault in faultBlocks)
        {
            for (int dx = -width; dx <= width; dx++)
            {
                for (int dy = -width; dy <= width; dy++)
                {
                    Vector2Int b = new Vector2Int(
                        fault.x + dx,
                        fault.y + dy
                    );

                    if (!IsValidFaultBlock(b))
                        continue;

                    if (faultBlocks.Contains(b))
                        continue;

                    faultInfluenceBlocks.Add(b);
                }
            }
        }
    }

    public EarthquakeFaultLineSaveData SaveState()
    {
        ResolveReferences();

        EarthquakeFaultLineSaveData data = new EarthquakeFaultLineSaveData
        {
            generatedForCurrentMap = generatedForCurrentMap,
            hasFaults = faultBlocks.Count > 0,

            blockColumns = mapGenerator != null ? mapGenerator.BlockColumns : 0,
            blockRows = mapGenerator != null ? mapGenerator.BlockRows : 0,

            directionValue = (int)direction
        };

        foreach (Vector2Int block in faultBlocks)
            data.faultBlocks.Add(new BlockCoordSaveData(block.x, block.y));

        foreach (Vector2Int block in faultInfluenceBlocks)
            data.faultInfluenceBlocks.Add(new BlockCoordSaveData(block.x, block.y));

        return data;
    }

    public void LoadState(EarthquakeFaultLineSaveData data)
    {
        ResolveReferences();

        if (generateRoutine != null)
        {
            StopCoroutine(generateRoutine);
            generateRoutine = null;
        }

        faultBlocks.Clear();
        faultInfluenceBlocks.Clear();
        validBlockScratch.Clear();
        faultBlockScratch.Clear();

        if (data == null)
        {
            generatedForCurrentMap = false;
            return;
        }

        generatedForCurrentMap = data.generatedForCurrentMap;

        if (IsValidDirectionValue(data.directionValue))
            direction = (EarthquakeFaultLineDirection)data.directionValue;

        if (data.faultBlocks != null)
        {
            for (int i = 0; i < data.faultBlocks.Count; i++)
            {
                BlockCoordSaveData saved = data.faultBlocks[i];

                if (saved == null)
                    continue;

                Vector2Int block = new Vector2Int(saved.x, saved.y);

                if (!IsBlockLoadable(block))
                    continue;

                faultBlocks.Add(block);
            }
        }

        if (data.faultInfluenceBlocks != null && data.faultInfluenceBlocks.Count > 0)
        {
            for (int i = 0; i < data.faultInfluenceBlocks.Count; i++)
            {
                BlockCoordSaveData saved = data.faultInfluenceBlocks[i];

                if (saved == null)
                    continue;

                Vector2Int block = new Vector2Int(saved.x, saved.y);

                if (!IsBlockLoadable(block))
                    continue;

                if (faultBlocks.Contains(block))
                    continue;

                faultInfluenceBlocks.Add(block);
            }
        }
        else if (faultBlocks.Count > 0)
        {
            BuildInfluenceBlocks();
        }

        // Important: this prevents Start/event logic from regenerating over loaded faults.
        generatedForCurrentMap = true;

        if (debugLogging)
        {
            Debug.Log(
                $"EarthquakeFaultLineGenerator: Loaded fault state. " +
                $"FaultBlocks={faultBlocks.Count}, InfluenceBlocks={faultInfluenceBlocks.Count}");
        }
    }

    private bool IsValidDirectionValue(int value)
    {
        return value >= (int)EarthquakeFaultLineDirection.Random &&
               value <= (int)EarthquakeFaultLineDirection.DiagonalNW;
    }

    private bool IsBlockLoadable(Vector2Int block)
    {
        if (block.x < 0 || block.y < 0)
            return false;

        if (mapGenerator == null)
            return true;

        if (mapGenerator.BlockColumns > 0 && block.x >= mapGenerator.BlockColumns)
            return false;

        if (mapGenerator.BlockRows > 0 && block.y >= mapGenerator.BlockRows)
            return false;

        if (!mapGenerator.HasBlockTerrainData)
            return true;

        return mapGenerator.IsValidBlock(block);
    }

    private void MarkEarthquakeFaultSaveDirty()
    {
        SaveSystem.MarkSectionDirty(SaveSectionKeys.WorldSim);
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos)
            return;

        if (mapGenerator == null)
            mapGenerator = FindObjectOfType<MapGenerator>();

        if (mapGenerator == null)
            return;

        if (gridManager == null)
            gridManager = GridManager.Instance;

        if (gridManager == null)
            return;

        float blockWorldSize = mapGenerator.blockSize * gridManager.cellSize;
        float size = blockWorldSize * Mathf.Max(0.1f, gizmoScale);

        Gizmos.color = influenceColor;

        foreach (Vector2Int b in faultInfluenceBlocks)
        {
            Vector3 pos = mapGenerator.GetBlockWorldCenter(b);
            pos.y += gizmoHeight;

            Gizmos.DrawCube(pos, new Vector3(size, 0.08f, size));
        }

        Gizmos.color = faultColor;

        foreach (Vector2Int b in faultBlocks)
        {
            Vector3 pos = mapGenerator.GetBlockWorldCenter(b);
            pos.y += gizmoHeight + 0.08f;

            Gizmos.DrawCube(pos, new Vector3(size, 0.12f, size));
        }
    }
}