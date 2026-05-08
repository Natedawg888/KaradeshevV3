using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Toggles a light-red overlay on every tile in the square repel radius of each AnimalRepeller.
/// Overlays are placed by computing world positions directly from GridManager — no tile scene scan.
/// Attach to a GameObject in the FinalSetup scene; wire via FinalSetupInstaller ("RepellerZoneButton").
/// Only active while in warfare mode (WorldCanvasMode.UnitsOnly == true).
/// </summary>
public class RepellerZoneVisualizer : MonoBehaviour
{
    [Header("Overlay Appearance")]
    [Tooltip("Multiplier on top of GridManager.cellSize. 1 = full tile coverage.")]
    [SerializeField] private float overlayScaleMultiplier = 1f;

    [Tooltip("Slight y-lift above the ground plane to avoid z-fighting.")]
    [SerializeField] private float yOffset = 0.05f;

    [Tooltip("Light red, semi-transparent color for the repeller zone.")]
    [SerializeField] private Color overlayColor = new Color(1f, 0.15f, 0.15f, 0.35f);

    private Button   _toggleButton;
    private bool     _highlightActive;
    private Material _overlayMaterial;

    private readonly List<GameObject>   _overlays = new List<GameObject>();
    private readonly HashSet<(int, int)> _visited  = new HashSet<(int, int)>();

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
    // Highlight — computed directly from repeller positions, no tile scan
    // ------------------------------------------------------------------

    private void ShowHighlight()
    {
        HideHighlight();

        var grid = GridManager.Instance;
        if (grid == null)
        {
            Debug.LogWarning("[RepellerZoneVisualizer] GridManager not found.");
            return;
        }

        if (AnimalRepellerRegistry.Active.Count == 0) return;

        EnsureMaterial();

        float tileSize = grid.cellSize;
        float scale    = tileSize * Mathf.Max(0.01f, overlayScaleMultiplier);

        _visited.Clear();

        foreach (var repeller in AnimalRepellerRegistry.Active)
        {
            if (repeller == null) continue;

            Vector2Int center = grid.GetGridPosition(repeller.transform.position);
            int        radius = Mathf.Max(1, repeller.repelRadiusTiles);

            for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
            {
                int tx = center.x + dx;
                int ty = center.y + dy;

                if (!_visited.Add((tx, ty))) continue; // deduplicate overlapping repellers

                Vector3 worldPos = grid.GetWorldPosition(tx, ty);
                CreateOverlay(worldPos, scale);
            }
        }

        _visited.Clear(); // free entries — not needed after spawn
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

    private void CreateOverlay(Vector3 tileWorldPos, float scale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name  = "RepellerZoneOverlay";
        go.layer = LayerMask.NameToLayer("UI");

        go.transform.position   = new Vector3(tileWorldPos.x, tileWorldPos.y + yOffset, tileWorldPos.z);
        go.transform.rotation   = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = Vector3.one * scale;

        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);

        go.GetComponent<MeshRenderer>().sharedMaterial = _overlayMaterial;
        _overlays.Add(go);
    }

    private void EnsureMaterial()
    {
        if (_overlayMaterial != null) return;

        var shader = Shader.Find("Sprites/Default")
                  ?? Shader.Find("Legacy Shaders/Transparent/Diffuse")
                  ?? Shader.Find("Unlit/Color");

        _overlayMaterial = new Material(shader) { color = overlayColor };

        // Render on top of all world geometry — disable depth write/test, use Overlay queue
        _overlayMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _overlayMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _overlayMaterial.SetInt("_ZWrite",   0);
        _overlayMaterial.SetInt("_ZTest",    (int)UnityEngine.Rendering.CompareFunction.Always);
        _overlayMaterial.renderQueue = 4000; // Overlay — renders after everything else
    }
}
