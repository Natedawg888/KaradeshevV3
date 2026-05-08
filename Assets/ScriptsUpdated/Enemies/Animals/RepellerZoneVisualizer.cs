using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Toggles a light-red overlay on every environment tile covered by an AnimalRepeller.
/// Attach to a GameObject in the FinalSetup scene.
/// Wire the button via FinalSetupInstaller (looks for "RepellerZoneButton" in the UI scene).
/// Only shows overlays while in warfare mode (WorldCanvasMode.UnitsOnly == true).
/// </summary>
public class RepellerZoneVisualizer : MonoBehaviour
{
    [Header("Overlay Appearance")]
    [Tooltip("Scale applied to each tile overlay quad. Tune to match your tile size.")]
    [SerializeField] private float overlayScale = 1f;

    [Tooltip("Slight y-lift above the ground plane to avoid z-fighting.")]
    [SerializeField] private float yOffset = 0.05f;

    [Tooltip("Light red, semi-transparent color for the repeller zone.")]
    [SerializeField] private Color overlayColor = new Color(1f, 0.15f, 0.15f, 0.35f);

    private Button _toggleButton;
    private bool   _highlightActive;
    private readonly List<GameObject> _overlays = new List<GameObject>();
    private Material _overlayMaterial;

    // ------------------------------------------------------------------

    private void OnEnable()
    {
        WorldCanvasMode.OnChanged += HandleModeChanged;
    }

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

        if (_highlightActive)
            ShowHighlight();
        else
            HideHighlight();
    }

    private void HandleModeChanged(bool unitsOnly)
    {
        UpdateButtonInteractable();

        // Auto-hide overlays when leaving warfare mode
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
        HideHighlight(); // clear any stale overlays

        var repelledCoords = BuildRepelledSet();
        if (repelledCoords.Count == 0) return;

        EnsureMaterial();

        // Find every EnvironmentControl tile and overlay the repelled ones
        var envControls = FindObjectsOfType<EnvironmentControl>(true);
        for (int i = 0; i < envControls.Length; i++)
        {
            var env = envControls[i];
            if (env == null) continue;

            var tile = env.GetComponentInParent<TileControl>(true);
            if (tile == null) continue;

            var gp   = tile.GetGridPosition();
            var coord = new TileCoord { x = gp.x, y = gp.y };

            if (!repelledCoords.Contains(coord)) continue;

            CreateOverlay(tile.transform.position);
        }
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

    private void CreateOverlay(Vector3 tileWorldPos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "RepellerZoneOverlay";

        // Lay flat on the ground, facing up
        go.transform.position   = new Vector3(tileWorldPos.x, tileWorldPos.y + yOffset, tileWorldPos.z);
        go.transform.rotation   = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = Vector3.one * overlayScale;

        // No collider needed
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);

        go.GetComponent<MeshRenderer>().sharedMaterial = _overlayMaterial;

        _overlays.Add(go);
    }

    private void EnsureMaterial()
    {
        if (_overlayMaterial != null) return;

        // Use a built-in transparent shader available in all Unity versions
        var shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Transparent/Diffuse");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        _overlayMaterial = new Material(shader) { color = overlayColor };

        if (_overlayMaterial.HasProperty("_SrcBlend"))
        {
            _overlayMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _overlayMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _overlayMaterial.SetInt("_ZWrite", 0);
            _overlayMaterial.renderQueue = 3000;
        }
    }

    // Builds the full set of repelled TileCoords from the registry
    private static HashSet<TileCoord> BuildRepelledSet()
    {
        var set  = new HashSet<TileCoord>();
        var grid = GridManager.Instance;

        foreach (var repeller in AnimalRepellerRegistry.Active)
        {
            if (repeller == null) continue;

            Vector2Int center = grid != null
                ? grid.GetGridPosition(repeller.transform.position)
                : Vector2Int.zero;

            int radius = Mathf.Max(1, repeller.repelRadiusTiles);

            for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
                set.Add(new TileCoord { x = center.x + dx, y = center.y + dy });
        }

        return set;
    }
}
