using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class FloodOverlayManager : MonoBehaviour
{
    private struct FloodVisualRecord
    {
        public GameObject visual;
        public GameObject prefab;
        public FloodOverlayVisualKind kind;
        public float rotationY;
        public Renderer[] renderers;
        public FloodWaterMaterialKind waterKind;
    }

    private struct FloodVisualSelection
    {
        public bool shouldRender;
        public FloodOverlayVisualKind kind;
        public float rotationY;

        public FloodVisualSelection(bool shouldRender, FloodOverlayVisualKind kind, float rotationY)
        {
            this.shouldRender = shouldRender;
            this.kind = kind;
            this.rotationY = rotationY;
        }
    }

    private static readonly FloodVisualSelection NoVisual =
        new FloodVisualSelection(false, FloodOverlayVisualKind.None, 0f);

    private static readonly Vector2Int[] DirtyNeighborhood =
    {
        new Vector2Int(-1, -1),
        new Vector2Int( 0, -1),
        new Vector2Int( 1, -1),
        new Vector2Int(-1,  0),
        new Vector2Int( 0,  0),
        new Vector2Int( 1,  0),
        new Vector2Int(-1,  1),
        new Vector2Int( 0,  1),
        new Vector2Int( 1,  1),
    };

    [Header("References")]
    public FloodSimulationSystem floodSimulation;
    public GridManager gridManager;

    [Header("Overlay Root")]
    public Transform overlayParent;

    [Tooltip("Y height for flood visuals. Set this above environment tiles and below unit/building visuals if needed.")]
    public float overlayHeightOffset = 0.08f;

    [Tooltip("Optional extra local scale multiplier for all flood prefabs.")]
    public Vector3 floodPrefabScale = Vector3.one;

    [Header("Prefabs")]
    public GameObject fillPrefab;
    public GameObject straightPrefab;
    public GameObject innerCornerPrefab;
    public GameObject outerCornerPrefab;

    [Header("Water Materials")]
    public Material freshWaterMaterial;
    public Material oceanWaterMaterial;

    [Tooltip("Optional. If empty, Mixed will pick ocean if involved, otherwise fresh.")]
    public Material mixedWaterMaterial;

    [Tooltip("If true, swaps only one material slot on the flood prefab renderers.")]
    public bool replaceOnlyWaterMaterialSlot = true;

    [Tooltip("Usually 0 if the whole flood prefab is just water.")]
    [Min(0)] public int waterMaterialSlotIndex = 0;

    [Tooltip("If true, searches the renderer material names for Water/Flood/Ocean/Fresh before using slot index.")]
    public bool findWaterMaterialSlotByName = true;

    public string waterMaterialNameContains = "water";

    [Header("Depth Visuals")]
    public bool scaleWithDepth = true;
    public float minYScale = 0.04f;
    public float maxYScale = 0.18f;

    public bool alphaWithDepth = true;
    [Range(0f, 1f)] public float minAlpha = 0.35f;
    [Range(0f, 1f)] public float maxAlpha = 0.85f;

    [Header("Visual Refresh")]
    public bool processVisualRefreshOverFrames = true;
    [Min(1)] public int overlaysUpdatedPerFrame = 64;

    [Header("Pool")]
    public bool prewarmOnStart = true;
    [Min(0)] public int prewarmEachPrefabCount = 32;

    [Header("Debug")]
    public bool debugLogging = false;

    private FloodOverlayPool pool;

    private readonly Dictionary<TileCoord, FloodVisualRecord> activeVisuals =
        new Dictionary<TileCoord, FloodVisualRecord>();

    private readonly Queue<TileCoord> pendingVisualRefreshes = new Queue<TileCoord>();
    private readonly HashSet<TileCoord> pendingVisualRefreshSet = new HashSet<TileCoord>();

    private readonly List<TileCoord> fullRefreshSnapshot = new List<TileCoord>(256);

    private Coroutine visualRefreshRoutine;

    private MaterialPropertyBlock propertyBlock;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Reset()
    {
        TryAutoAssignReferences();
    }

    private void Awake()
    {
        TryAutoAssignReferences();
        EnsureRootAndPool();

        propertyBlock = new MaterialPropertyBlock();
    }

    private void Start()
    {
        EnsureRootAndPool();

        if (prewarmOnStart)
            Prewarm();
    }

    private void OnEnable()
    {
        TryAutoAssignReferences();

        if (floodSimulation != null)
        {
            floodSimulation.OnFloodCellChanged += HandleFloodCellChanged;
            floodSimulation.OnFloodCellsChanged += HandleFloodCellsChanged;
            floodSimulation.OnFloodCleared += HandleFloodCleared;
        }
    }

    private void OnDisable()
    {
        if (floodSimulation != null)
        {
            floodSimulation.OnFloodCellChanged -= HandleFloodCellChanged;
            floodSimulation.OnFloodCellsChanged -= HandleFloodCellsChanged;
            floodSimulation.OnFloodCleared -= HandleFloodCleared;
        }

        if (visualRefreshRoutine != null)
        {
            StopCoroutine(visualRefreshRoutine);
            visualRefreshRoutine = null;
        }

        pendingVisualRefreshes.Clear();
        pendingVisualRefreshSet.Clear();
    }

    private void TryAutoAssignReferences()
    {
        if (floodSimulation == null)
            floodSimulation = FindFirstObjectByType<FloodSimulationSystem>();

        if (gridManager == null)
            gridManager = GridManager.Instance;

        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();
    }

    private void EnsureRootAndPool()
    {
        if (overlayParent == null)
        {
            GameObject root = new GameObject("Flood Overlay Root");
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            overlayParent = root.transform;
        }

        if (pool == null)
            pool = new FloodOverlayPool(overlayParent);
    }

    private void Prewarm()
    {
        EnsureRootAndPool();

        pool.Prewarm(fillPrefab, prewarmEachPrefabCount);
        pool.Prewarm(straightPrefab, prewarmEachPrefabCount);
        pool.Prewarm(innerCornerPrefab, prewarmEachPrefabCount);
        pool.Prewarm(outerCornerPrefab, prewarmEachPrefabCount);
    }

    private void HandleFloodCellChanged(FloodCellChangedEvent evt)
    {
        MarkDirtyNeighborhood(evt.coord);
    }

    private void HandleFloodCellsChanged(IReadOnlyList<TileCoord> coords)
    {
        if (coords == null)
            return;

        for (int i = 0; i < coords.Count; i++)
            MarkDirtyNeighborhood(coords[i]);
    }

    private void HandleFloodCleared()
    {
        ClearAllVisualsOnly();
    }

    private void MarkDirtyNeighborhood(TileCoord center)
    {
        for (int i = 0; i < DirtyNeighborhood.Length; i++)
        {
            int x = center.x + DirtyNeighborhood[i].x;
            int y = center.y + DirtyNeighborhood[i].y;

            if (IsOutsideGrid(x, y))
                continue;

            QueueVisualRefresh(new TileCoord(x, y));
        }
    }

    private void QueueVisualRefresh(TileCoord coord)
    {
        if (IsOutsideGrid(coord.x, coord.y))
            return;

        if (!pendingVisualRefreshSet.Add(coord))
            return;

        pendingVisualRefreshes.Enqueue(coord);

        if (!processVisualRefreshOverFrames)
        {
            ProcessVisualRefreshImmediate();
            return;
        }

        if (visualRefreshRoutine == null && isActiveAndEnabled)
            visualRefreshRoutine = StartCoroutine(VisualRefreshRoutine());
    }

    private IEnumerator VisualRefreshRoutine()
    {
        while (pendingVisualRefreshes.Count > 0)
        {
            int processed = 0;
            int max = Mathf.Max(1, overlaysUpdatedPerFrame);

            while (pendingVisualRefreshes.Count > 0 && processed < max)
            {
                TileCoord coord = pendingVisualRefreshes.Dequeue();
                pendingVisualRefreshSet.Remove(coord);

                RefreshVisualAtCell(coord);
                processed++;
            }

            if (pendingVisualRefreshes.Count > 0)
                yield return null;
        }

        visualRefreshRoutine = null;
    }

    private void ProcessVisualRefreshImmediate()
    {
        while (pendingVisualRefreshes.Count > 0)
        {
            TileCoord coord = pendingVisualRefreshes.Dequeue();
            pendingVisualRefreshSet.Remove(coord);

            RefreshVisualAtCell(coord);
        }
    }

    private void RefreshVisualAtCell(TileCoord coord)
    {
        EnsureRootAndPool();

        FloodVisualSelection selection = DetermineVisualForCell(coord);

        if (!selection.shouldRender)
        {
            ReleaseVisual(coord);
            return;
        }

        GameObject prefab = GetPrefab(selection.kind);

        if (prefab == null)
        {
            ReleaseVisual(coord);
            return;
        }

        Vector3 pos = GetWorldPositionForCell(coord);
        Quaternion rot = Quaternion.Euler(0f, selection.rotationY, 0f);

        bool hasRecord = activeVisuals.TryGetValue(coord, out FloodVisualRecord record);

        bool needsReplacement =
            !hasRecord ||
            record.visual == null ||
            record.prefab != prefab ||
            record.kind != selection.kind;

        if (needsReplacement)
        {
            if (hasRecord && record.visual != null && record.prefab != null && pool != null)
                pool.Return(record.prefab, record.visual);

            GameObject instance = pool.Get(prefab, pos, rot);

            if (instance == null)
                return;

            instance.name = $"Flood_{selection.kind}_{coord.x}_{coord.y}";
            instance.transform.localScale = floodPrefabScale;

            record = new FloodVisualRecord
            {
                visual = instance,
                prefab = prefab,
                kind = selection.kind,
                rotationY = selection.rotationY,
                renderers = instance.GetComponentsInChildren<Renderer>(true),
                waterKind = GetWaterMaterialKindForVisual(coord)
            };
        }
        else
        {
            record.visual.transform.SetPositionAndRotation(pos, rot);
            record.visual.transform.localScale = floodPrefabScale;
            record.visual.name = $"Flood_{selection.kind}_{coord.x}_{coord.y}";
            record.rotationY = selection.rotationY;
            record.waterKind = GetWaterMaterialKindForVisual(coord);
        }

        ApplyWaterMaterial(coord, ref record);
        ApplyDepthVisuals(coord, ref record);

        activeVisuals[coord] = record;
    }

    private void ReleaseVisual(TileCoord coord)
    {
        if (!activeVisuals.TryGetValue(coord, out FloodVisualRecord record))
            return;

        if (record.visual != null && record.prefab != null && pool != null)
            pool.Return(record.prefab, record.visual);

        activeVisuals.Remove(coord);
    }

    private FloodVisualSelection DetermineVisualForCell(TileCoord coord)
    {
        int x = coord.x;
        int y = coord.y;

        bool center = HasFloodAt(x, y);

        bool n = HasFloodAt(x, y + 1);
        bool e = HasFloodAt(x + 1, y);
        bool s = HasFloodAt(x, y - 1);
        bool w = HasFloodAt(x - 1, y);

        bool ne = HasFloodAt(x + 1, y + 1);
        bool se = HasFloodAt(x + 1, y - 1);
        bool sw = HasFloodAt(x - 1, y - 1);
        bool nw = HasFloodAt(x - 1, y + 1);

        // Actual flood cells are always fill.
        if (center)
            return new FloodVisualSelection(true, FloodOverlayVisualKind.Fill, 0f);

        int cardinalCount = 0;
        if (n) cardinalCount++;
        if (e) cardinalCount++;
        if (s) cardinalCount++;
        if (w) cardinalCount++;

        int diagonalCount = 0;
        if (ne) diagonalCount++;
        if (se) diagonalCount++;
        if (sw) diagonalCount++;
        if (nw) diagonalCount++;

        if (cardinalCount == 0 && diagonalCount == 0)
            return NoVisual;

        // INNER CORNERS — copied from lava logic.
        if (s && w && !n && !e)
            return new FloodVisualSelection(true, FloodOverlayVisualKind.InnerCorner, 90f);

        if (n && w && !s && !e)
            return new FloodVisualSelection(true, FloodOverlayVisualKind.InnerCorner, 180f);

        if (n && e && !s && !w)
            return new FloodVisualSelection(true, FloodOverlayVisualKind.InnerCorner, -90f);

        if (s && e && !n && !w)
            return new FloodVisualSelection(true, FloodOverlayVisualKind.InnerCorner, 0f);

        // OUTER CORNERS — copied from lava logic.
        if (cardinalCount == 0)
        {
            if (sw && !se && !nw && !ne)
                return new FloodVisualSelection(true, FloodOverlayVisualKind.OuterCorner, 180f);

            if (nw && !sw && !ne && !se)
                return new FloodVisualSelection(true, FloodOverlayVisualKind.OuterCorner, -90f);

            if (ne && !nw && !se && !sw)
                return new FloodVisualSelection(true, FloodOverlayVisualKind.OuterCorner, 0f);

            if (se && !ne && !sw && !nw)
                return new FloodVisualSelection(true, FloodOverlayVisualKind.OuterCorner, 90f);
        }

        // STRAIGHTS — copied from lava logic.
        if (s && !n && !e && !w)
            return new FloodVisualSelection(true, FloodOverlayVisualKind.Straight, 90f);

        if (w && !n && !e && !s)
            return new FloodVisualSelection(true, FloodOverlayVisualKind.Straight, 180f);

        if (n && !e && !s && !w)
            return new FloodVisualSelection(true, FloodOverlayVisualKind.Straight, -90f);

        if (e && !n && !s && !w)
            return new FloodVisualSelection(true, FloodOverlayVisualKind.Straight, 0f);

        if (s && !n)
            return new FloodVisualSelection(true, FloodOverlayVisualKind.Straight, 0f);

        if (w && !e)
            return new FloodVisualSelection(true, FloodOverlayVisualKind.Straight, 90f);

        if (n && !s)
            return new FloodVisualSelection(true, FloodOverlayVisualKind.Straight, 180f);

        if (e && !w)
            return new FloodVisualSelection(true, FloodOverlayVisualKind.Straight, 270f);

        if (s) return new FloodVisualSelection(true, FloodOverlayVisualKind.Straight, 0f);
        if (w) return new FloodVisualSelection(true, FloodOverlayVisualKind.Straight, 90f);
        if (n) return new FloodVisualSelection(true, FloodOverlayVisualKind.Straight, 180f);
        if (e) return new FloodVisualSelection(true, FloodOverlayVisualKind.Straight, 270f);

        return NoVisual;
    }

    private GameObject GetPrefab(FloodOverlayVisualKind kind)
    {
        switch (kind)
        {
            case FloodOverlayVisualKind.Fill:
                return fillPrefab;

            case FloodOverlayVisualKind.Straight:
                return straightPrefab != null ? straightPrefab : fillPrefab;

            case FloodOverlayVisualKind.InnerCorner:
                return innerCornerPrefab != null ? innerCornerPrefab : fillPrefab;

            case FloodOverlayVisualKind.OuterCorner:
                return outerCornerPrefab != null ? outerCornerPrefab : fillPrefab;

            default:
                return null;
        }
    }

    private bool HasFloodAt(int x, int y)
    {
        if (IsOutsideGrid(x, y))
            return false;

        if (floodSimulation == null)
            return false;

        return floodSimulation.IsFlooded(new TileCoord(x, y));
    }

    private FloodWaterMaterialKind GetWaterMaterialKindForVisual(TileCoord coord)
    {
        FloodSourceType sourceType = GetSourceTypeForVisual(coord);

        switch (sourceType)
        {
            case FloodSourceType.Ocean:
            case FloodSourceType.Tsunami:
                return FloodWaterMaterialKind.OceanWater;

            case FloodSourceType.Mixed:
                return FloodWaterMaterialKind.Mixed;

            case FloodSourceType.River:
            case FloodSourceType.Lake:
            case FloodSourceType.Rain:
            case FloodSourceType.None:
            default:
                return FloodWaterMaterialKind.FreshWater;
        }
    }

    private FloodSourceType GetSourceTypeForVisual(TileCoord coord)
    {
        if (floodSimulation == null)
            return FloodSourceType.None;

        if (floodSimulation.TryGetFloodCell(coord, out FloodCellState state) && state != null)
            return state.sourceType;

        float bestDepth = -1f;
        FloodSourceType bestSource = FloodSourceType.None;

        for (int i = 0; i < DirtyNeighborhood.Length; i++)
        {
            TileCoord nearby = new TileCoord(
                coord.x + DirtyNeighborhood[i].x,
                coord.y + DirtyNeighborhood[i].y);

            if (!floodSimulation.TryGetFloodCell(nearby, out FloodCellState nearbyState) ||
                nearbyState == null)
            {
                continue;
            }

            if (nearbyState.floodDepth01 > bestDepth)
            {
                bestDepth = nearbyState.floodDepth01;
                bestSource = nearbyState.sourceType;
            }
        }

        return bestSource;
    }

    private Material GetWaterMaterial(FloodWaterMaterialKind kind)
    {
        switch (kind)
        {
            case FloodWaterMaterialKind.OceanWater:
                return oceanWaterMaterial != null ? oceanWaterMaterial : freshWaterMaterial;

            case FloodWaterMaterialKind.Mixed:
                if (mixedWaterMaterial != null)
                    return mixedWaterMaterial;

                return oceanWaterMaterial != null ? oceanWaterMaterial : freshWaterMaterial;

            case FloodWaterMaterialKind.FreshWater:
            default:
                return freshWaterMaterial != null ? freshWaterMaterial : oceanWaterMaterial;
        }
    }

    private void ApplyWaterMaterial(TileCoord coord, ref FloodVisualRecord record)
    {
        if (record.visual == null)
            return;

        FloodWaterMaterialKind kind = GetWaterMaterialKindForVisual(coord);
        Material material = GetWaterMaterial(kind);

        if (material == null)
            return;

        if (record.renderers == null || record.renderers.Length == 0)
            record.renderers = record.visual.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < record.renderers.Length; i++)
        {
            Renderer renderer = record.renderers[i];

            if (renderer == null)
                continue;

            ApplyWaterMaterialToRenderer(renderer, material);
        }

        record.waterKind = kind;
    }

    private void ApplyWaterMaterialToRenderer(Renderer renderer, Material material)
    {
        if (renderer == null || material == null)
            return;

        Material[] materials = renderer.sharedMaterials;

        if (materials == null || materials.Length == 0)
            return;

        if (!replaceOnlyWaterMaterialSlot)
        {
            for (int i = 0; i < materials.Length; i++)
                materials[i] = material;

            renderer.sharedMaterials = materials;
            return;
        }

        int slot = ResolveWaterMaterialSlot(materials);

        if (slot < 0 || slot >= materials.Length)
            return;

        materials[slot] = material;
        renderer.sharedMaterials = materials;
    }

    private int ResolveWaterMaterialSlot(Material[] materials)
    {
        if (materials == null || materials.Length == 0)
            return -1;

        if (findWaterMaterialSlotByName && !string.IsNullOrWhiteSpace(waterMaterialNameContains))
        {
            string search = waterMaterialNameContains.ToLowerInvariant();

            for (int i = 0; i < materials.Length; i++)
            {
                Material mat = materials[i];

                if (mat == null)
                    continue;

                string matName = mat.name.ToLowerInvariant();

                if (matName.Contains(search))
                    return i;
            }

            string[] fallbackNames =
            {
                "flood",
                "fresh",
                "ocean",
                "sea",
                "river",
                "lake"
            };

            for (int f = 0; f < fallbackNames.Length; f++)
            {
                string fallback = fallbackNames[f];

                for (int i = 0; i < materials.Length; i++)
                {
                    Material mat = materials[i];

                    if (mat == null)
                        continue;

                    string matName = mat.name.ToLowerInvariant();

                    if (matName.Contains(fallback))
                        return i;
                }
            }
        }

        if (waterMaterialSlotIndex >= 0 && waterMaterialSlotIndex < materials.Length)
            return waterMaterialSlotIndex;

        return 0;
    }

    private float GetVisualDepth01(TileCoord coord)
    {
        if (floodSimulation == null)
            return 0f;

        if (floodSimulation.TryGetFloodCell(coord, out FloodCellState state) && state != null)
            return Mathf.Clamp01(state.floodDepth01);

        float best = 0f;

        for (int i = 0; i < DirtyNeighborhood.Length; i++)
        {
            TileCoord nearby = new TileCoord(
                coord.x + DirtyNeighborhood[i].x,
                coord.y + DirtyNeighborhood[i].y);

            if (floodSimulation.TryGetFloodCell(nearby, out FloodCellState nearbyState) && nearbyState != null)
                best = Mathf.Max(best, nearbyState.floodDepth01);
        }

        return Mathf.Clamp01(best);
    }

    private void ApplyDepthVisuals(TileCoord coord, ref FloodVisualRecord record)
    {
        if (record.visual == null)
            return;

        float depth01 = GetVisualDepth01(coord);

        if (scaleWithDepth)
        {
            Vector3 scale = floodPrefabScale;
            scale.y = Mathf.Lerp(minYScale, maxYScale, depth01);
            record.visual.transform.localScale = scale;
        }

        if (!alphaWithDepth)
            return;

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        if (record.renderers == null || record.renderers.Length == 0)
            record.renderers = record.visual.GetComponentsInChildren<Renderer>(true);

        float alpha = Mathf.Lerp(minAlpha, maxAlpha, depth01);

        for (int i = 0; i < record.renderers.Length; i++)
        {
            Renderer r = record.renderers[i];

            if (r == null)
                continue;

            Material shared = r.sharedMaterial;
            if (shared == null)
                continue;

            r.GetPropertyBlock(propertyBlock);

            if (shared.HasProperty(BaseColorId))
            {
                Color c = shared.GetColor(BaseColorId);
                c.a = alpha;
                propertyBlock.SetColor(BaseColorId, c);
            }

            if (shared.HasProperty(ColorId))
            {
                Color c = shared.GetColor(ColorId);
                c.a = alpha;
                propertyBlock.SetColor(ColorId, c);
            }

            r.SetPropertyBlock(propertyBlock);
        }
    }

    public void RebuildAllVisuals()
    {
        ClearAllVisualsOnly();

        if (floodSimulation == null)
            return;

        fullRefreshSnapshot.Clear();

        foreach (TileCoord coord in floodSimulation.ActiveFloodCells.Keys)
            fullRefreshSnapshot.Add(coord);

        for (int i = 0; i < fullRefreshSnapshot.Count; i++)
            MarkDirtyNeighborhood(fullRefreshSnapshot[i]);

        fullRefreshSnapshot.Clear();
    }

    public void ClearAllVisualsOnly()
    {
        foreach (KeyValuePair<TileCoord, FloodVisualRecord> pair in activeVisuals)
        {
            FloodVisualRecord record = pair.Value;

            if (record.visual != null && record.prefab != null && pool != null)
                pool.Return(record.prefab, record.visual);
        }

        activeVisuals.Clear();
        pendingVisualRefreshes.Clear();
        pendingVisualRefreshSet.Clear();
    }

    private Vector3 GetWorldPositionForCell(TileCoord coord)
    {
        TryAutoAssignReferences();

        if (gridManager == null)
            return new Vector3(coord.x, overlayHeightOffset, coord.y);

        try
        {
            Vector3 corner = gridManager.GetWorldPosition(coord.x, coord.y);
            float size = gridManager.cellSize;

            return new Vector3(
                corner.x + size * 0.5f,
                overlayHeightOffset,
                corner.z + size * 0.5f);
        }
        catch
        {
            // Reflection fallback below.
        }

        Type type = gridManager.GetType();

        MethodInfo methodV2 = type.GetMethod("GetWorldPosition", new[] { typeof(Vector2Int) });
        if (methodV2 != null)
        {
            object result = methodV2.Invoke(gridManager, new object[] { new Vector2Int(coord.x, coord.y) });
            if (result is Vector3 v3)
                return new Vector3(v3.x, overlayHeightOffset, v3.z);
        }

        float cellSize = 1f;

        object cellSizeValue =
            ReflectionGetMemberValue(gridManager, "cellSize") ??
            ReflectionGetMemberValue(gridManager, "CellSize");

        if (cellSizeValue != null)
        {
            try { cellSize = Convert.ToSingle(cellSizeValue); }
            catch { cellSize = 1f; }
        }

        Vector3 fallback = gridManager.transform.position + new Vector3(coord.x * cellSize, 0f, coord.y * cellSize);

        return new Vector3(
            fallback.x + cellSize * 0.5f,
            overlayHeightOffset,
            fallback.z + cellSize * 0.5f);
    }

    private bool IsOutsideGrid(int x, int y)
    {
        TryAutoAssignReferences();

        if (gridManager == null)
            return true;

        return x < 0 ||
               y < 0 ||
               x >= gridManager.columns ||
               y >= gridManager.rows;
    }

    private object ReflectionGetMemberValue(object target, string name)
    {
        if (target == null || string.IsNullOrWhiteSpace(name))
            return null;

        Type type = target.GetType();

        FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
            return field.GetValue(target);

        PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.CanRead)
            return property.GetValue(target);

        return null;
    }

    [ContextMenu("Debug/Rebuild All Flood Visuals")]
    private void ContextRebuildAllVisuals()
    {
        RebuildAllVisuals();
    }

    [ContextMenu("Debug/Clear Flood Visuals Only")]
    private void ContextClearVisualsOnly()
    {
        ClearAllVisualsOnly();
    }
}