using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Highlights every environment tile within the square repel radius of each AnimalRepeller.
/// The building's own tile is excluded (radius 1 = adjacent ring, 2 = two tiles out, etc.).
/// Cache is built on first show — after all additive scenes are loaded — to avoid empty results.
/// Overlays are created in batches to avoid frame freezes.
/// </summary>
public class RepellerZoneVisualizer : MonoBehaviour
{
    [Header("Overlay Appearance")]
    [SerializeField] private float yOffset = 0.05f;
    [SerializeField] private Color overlayColor = new Color(1f, 0.15f, 0.15f, 0.35f);

    [Header("Performance")]
    [Tooltip("Overlay quads to create per frame when showing the zone.")]
    [SerializeField] private int overlaysPerFrame = 20;

    private Button   _toggleButton;
    private bool     _highlightActive;
    private Material _overlayMaterial;

    private Coroutine _showRoutine;

    private readonly List<GameObject>                   _overlays  = new List<GameObject>();
    private readonly Dictionary<(int, int), TileControl> _tileCache = new Dictionary<(int, int), TileControl>();

    // ------------------------------------------------------------------
    // Lifecycle
    // ------------------------------------------------------------------

    private void OnEnable()  => WorldCanvasMode.OnChanged += HandleModeChanged;
    private void OnDisable()
    {
        WorldCanvasMode.OnChanged -= HandleModeChanged;
        StopShowRoutine();
        HideHighlight();
    }

    private void OnDestroy()
    {
        if (_overlayMaterial != null)
            Destroy(_overlayMaterial);
    }

    // ------------------------------------------------------------------
    // Wired by FinalSetupInstaller
    // ------------------------------------------------------------------

    public void SetToggleButton(Button button)
    {
        if (_toggleButton != null)
            _toggleButton.onClick.RemoveListener(OnTogglePressed);

        _toggleButton = button;

        if (_toggleButton != null)
        {
            _toggleButton.onClick.RemoveAllListeners();
            _toggleButton.onClick.AddListener(OnTogglePressed);
            UpdateButtonInteractable();
        }
    }

    // ------------------------------------------------------------------

    private void OnTogglePressed()
    {
        if (!WorldCanvasMode.UnitsOnly) return;

        _highlightActive = !_highlightActive;

        if (_highlightActive) BeginShowHighlight();
        else                  HideHighlight();
    }

    private void HandleModeChanged(bool unitsOnly)
    {
        UpdateButtonInteractable();

        if (!unitsOnly && _highlightActive)
        {
            _highlightActive = false;
            StopShowRoutine();
            HideHighlight();
        }
    }

    private void UpdateButtonInteractable()
    {
        if (_toggleButton != null)
            _toggleButton.interactable = WorldCanvasMode.UnitsOnly;
    }

    // ------------------------------------------------------------------
    // Cache — built lazily on first show so all additive scenes are loaded
    // ------------------------------------------------------------------

    private void RebuildTileCache()
    {
        _tileCache.Clear();

        var allTiles = FindObjectsOfType<TileControl>(true);
        for (int i = 0; i < allTiles.Length; i++)
        {
            var tile = allTiles[i];
            if (tile == null) continue;
            if (tile.tileContentType != TileContentType.Environment) continue;

            var gp  = tile.GetGridPosition();
            var key = (gp.x, gp.y);
            if (!_tileCache.ContainsKey(key))
                _tileCache[key] = tile;
        }

        Debug.Log($"[RepellerZoneVisualizer] Cache built: {_tileCache.Count} environment tiles.");
    }

    public void InvalidateTileCache() => _tileCache.Clear();

    // ------------------------------------------------------------------
    // Show / Hide
    // ------------------------------------------------------------------

    private void BeginShowHighlight()
    {
        StopShowRoutine();
        HideHighlight();
        EnsureMaterial();

        // Rebuild cache every show — ensures it's populated after additive scene loads
        RebuildTileCache();

        _showRoutine = StartCoroutine(ShowHighlightRoutine());
    }

    private void StopShowRoutine()
    {
        if (_showRoutine != null)
        {
            StopCoroutine(_showRoutine);
            _showRoutine = null;
        }
    }

    private IEnumerator ShowHighlightRoutine()
    {
        if (AnimalRepellerRegistry.Active.Count == 0)
        {
            Debug.Log("[RepellerZoneVisualizer] No active repellers found.");
            yield break;
        }

        if (_tileCache.Count == 0)
        {
            Debug.LogWarning("[RepellerZoneVisualizer] Tile cache is empty — no environment tiles found.");
            yield break;
        }

        var grid = GridManager.Instance;
        var visited = new HashSet<(int, int)>();
        var toSpawn = new List<TileControl>();

        foreach (var repeller in AnimalRepellerRegistry.Active)
        {
            if (repeller == null) continue;

            Vector2Int center = grid != null
                ? grid.GetGridPosition(repeller.transform.position)
                : Vector2Int.zero;

            int radius = Mathf.Max(1, repeller.repelRadiusTiles);

            for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
            {
                if (dx == 0 && dy == 0) continue;

                int tx = center.x + dx;
                int ty = center.y + dy;

                if (!visited.Add((tx, ty))) continue;

                if (_tileCache.TryGetValue((tx, ty), out var tile) && tile != null)
                    toSpawn.Add(tile);
            }
        }

        Debug.Log($"[RepellerZoneVisualizer] Spawning {toSpawn.Count} overlay(s).");

        int batch = 0;
        for (int i = 0; i < toSpawn.Count; i++)
        {
            CreateOverlayForTile(toSpawn[i]);

            if (++batch >= overlaysPerFrame)
            {
                batch = 0;
                yield return null;
            }
        }

        _showRoutine = null;
    }

    private void HideHighlight()
    {
        for (int i = 0; i < _overlays.Count; i++)
        {
            if (_overlays[i] != null)
                Destroy(_overlays[i]);
        }
        _overlays.Clear();
    }

    // ------------------------------------------------------------------
    // Overlay creation
    // ------------------------------------------------------------------

    private void CreateOverlayForTile(TileControl tile)
    {
        Vector3 size   = GetTileFootprint(tile);
        Vector3 center = tile.transform.position;

        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "RepellerZoneOverlay";

        int uiLayer = LayerMask.NameToLayer("UI");
        go.layer = uiLayer >= 0 ? uiLayer : 0;

        go.transform.position   = new Vector3(center.x, center.y + yOffset, center.z);
        go.transform.rotation   = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = new Vector3(size.x, size.z, 1f);

        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);

        go.GetComponent<MeshRenderer>().sharedMaterial = _overlayMaterial;
        _overlays.Add(go);
    }

    private static Vector3 GetTileFootprint(TileControl tile)
    {
        var box = tile.GetComponent<BoxCollider>();
        if (box != null)
        {
            Vector3 s = box.size;
            return new Vector3(
                Mathf.Abs(s.x * tile.transform.lossyScale.x),
                Mathf.Abs(s.y * tile.transform.lossyScale.y),
                Mathf.Abs(s.z * tile.transform.lossyScale.z));
        }

        var rend = tile.GetComponentInChildren<Renderer>(true);
        if (rend != null) return rend.bounds.size;

        float cell = GridManager.Instance != null ? GridManager.Instance.cellSize : 1f;
        return new Vector3(cell, cell, cell);
    }

    private void EnsureMaterial()
    {
        if (_overlayMaterial != null) return;

        var shader = Shader.Find("Sprites/Default")
                  ?? Shader.Find("Legacy Shaders/Transparent/Diffuse")
                  ?? Shader.Find("Unlit/Color");

        _overlayMaterial = new Material(shader) { color = overlayColor };

        _overlayMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _overlayMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _overlayMaterial.SetInt("_ZWrite",   0);
        _overlayMaterial.SetInt("_ZTest",    (int)UnityEngine.Rendering.CompareFunction.Always);
        _overlayMaterial.renderQueue = 4000;
    }
}
