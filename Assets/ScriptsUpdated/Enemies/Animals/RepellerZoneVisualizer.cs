using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Toggles a light-red overlay on every tile within the square repel radius of each AnimalRepeller.
/// Each overlay matches the exact world-space footprint of its TileControl (reads Collider bounds),
/// so mixed tile sizes never overlap. Renders on the UI layer above all world geometry.
/// Attach to a GameObject in the FinalSetup scene; wire via FinalSetupInstaller ("RepellerZoneButton").
/// Only active while in warfare mode (WorldCanvasMode.UnitsOnly == true).
/// </summary>
public class RepellerZoneVisualizer : MonoBehaviour
{
    [Header("Overlay Appearance")]
    [Tooltip("Slight y-lift above the ground plane to avoid z-fighting.")]
    [SerializeField] private float yOffset = 0.05f;

    [Tooltip("Light red, semi-transparent color for the repeller zone.")]
    [SerializeField] private Color overlayColor = new Color(1f, 0.15f, 0.15f, 0.35f);

    private Button   _toggleButton;
    private bool     _highlightActive;
    private Material _overlayMaterial;

    private readonly List<GameObject>          _overlays   = new List<GameObject>();
    private readonly HashSet<(int, int)>        _visited    = new HashSet<(int, int)>();
    private readonly Dictionary<(int,int), TileControl> _tileCache = new Dictionary<(int,int), TileControl>();

    // ------------------------------------------------------------------

    private void OnEnable()  => WorldCanvasMode.OnChanged += HandleModeChanged;
    private void OnDisable()
    {
        WorldCanvasMode.OnChanged -= HandleModeChanged;
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

        if (_highlightActive) ShowHighlight();
        else                  HideHighlight();
    }

    private void HandleModeChanged(bool unitsOnly)
    {
        UpdateButtonInteractable();

        if (!unitsOnly && _highlightActive)
        {
            _highlightActive = false;
            HideHighlight();
        }
    }

    private void UpdateButtonInteractable()
    {
        if (_toggleButton != null)
            _toggleButton.interactable = WorldCanvasMode.UnitsOnly;
    }

    // ------------------------------------------------------------------

    private void ShowHighlight()
    {
        HideHighlight();

        if (AnimalRepellerRegistry.Active.Count == 0) return;

        EnsureMaterial();
        BuildTileCacheIfNeeded();

        var grid = GridManager.Instance;

        _visited.Clear();

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
                if (dx == 0 && dy == 0) continue; // never highlight the repeller's own tile

                int tx = center.x + dx;
                int ty = center.y + dy;

                if (!_visited.Add((tx, ty))) continue;

                if (!_tileCache.TryGetValue((tx, ty), out var tile) || tile == null) continue;

                CreateOverlayForTile(tile);
            }
        }

        _visited.Clear();
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

    // Build coord → TileControl lookup from EnvironmentControl components only.
    // This filters to environment tiles, skipping building/empty tiles automatically.
    // Called once; call InvalidateTileCache() if the tile set changes at runtime.
    private void BuildTileCacheIfNeeded()
    {
        if (_tileCache.Count > 0) return;

        var envControls = FindObjectsOfType<EnvironmentControl>(true);
        for (int i = 0; i < envControls.Length; i++)
        {
            var env = envControls[i];
            if (env == null) continue;

            var tile = env.GetComponentInParent<TileControl>(true);
            if (tile == null) continue;

            var gp  = tile.GetGridPosition();
            var key = (gp.x, gp.y);
            if (!_tileCache.ContainsKey(key))
                _tileCache[key] = tile;
        }
    }

    public void InvalidateTileCache() => _tileCache.Clear();

    private void CreateOverlayForTile(TileControl tile)
    {
        // Read the tile's actual world-space footprint from its collider bounds
        Vector3 size   = GetTileFootprint(tile);
        Vector3 center = tile.transform.position;

        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name  = "RepellerZoneOverlay";
        go.layer = LayerMask.NameToLayer("UI");

        go.transform.position   = new Vector3(center.x, center.y + yOffset, center.z);
        go.transform.rotation   = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = new Vector3(size.x, size.z, 1f); // x/z = horizontal footprint

        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);

        go.GetComponent<MeshRenderer>().sharedMaterial = _overlayMaterial;
        _overlays.Add(go);
    }

    private static Vector3 GetTileFootprint(TileControl tile)
    {
        // BoxCollider gives the most reliable footprint
        var box = tile.GetComponent<BoxCollider>();
        if (box != null)
        {
            Vector3 s = box.size;
            // Scale by the tile's lossyScale so prefab-scale is respected
            return new Vector3(
                Mathf.Abs(s.x * tile.transform.lossyScale.x),
                Mathf.Abs(s.y * tile.transform.lossyScale.y),
                Mathf.Abs(s.z * tile.transform.lossyScale.z));
        }

        // Fall back to Renderer bounds
        var rend = tile.GetComponentInChildren<Renderer>(true);
        if (rend != null)
            return rend.bounds.size;

        // Last resort: use GridManager cell size
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
