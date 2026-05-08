using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows a flat semi-transparent red square over each AnimalRepeller's zone.
/// One quad per repeller — sized from GridManager.cellSize × radius. No tile lookup needed.
/// Only active while in warfare mode (WorldCanvasMode.UnitsOnly == true).
/// </summary>
public class RepellerZoneVisualizer : MonoBehaviour
{
    [Header("Overlay Appearance")]
    [SerializeField] private float yOffset = 0.05f;
    [SerializeField] private Color overlayColor = new Color(1f, 0.15f, 0.15f, 0.35f);

    private Button   _toggleButton;
    private bool     _highlightActive;
    private Material _overlayMaterial;

    private readonly List<GameObject> _overlays = new List<GameObject>();

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
        EnsureMaterial();

        var grid = GridManager.Instance;
        float cellSize = grid != null ? grid.cellSize : 1f;

        foreach (var repeller in AnimalRepellerRegistry.Active)
        {
            if (repeller == null) continue;

            int   radius    = Mathf.Max(1, repeller.repelRadiusTiles);
            float sideLen   = (radius * 2 + 1) * cellSize; // full square from -radius to +radius
            Vector3 worldPos = repeller.transform.position;

            CreateSquare(worldPos, sideLen);
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

    private void CreateSquare(Vector3 worldPos, float sideLen)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "RepellerZoneOverlay";

        int uiLayer = LayerMask.NameToLayer("UI");
        go.layer = uiLayer >= 0 ? uiLayer : 0;

        go.transform.position   = new Vector3(worldPos.x, worldPos.y + yOffset, worldPos.z);
        go.transform.rotation   = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = new Vector3(sideLen, sideLen, 1f);

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

        _overlayMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _overlayMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _overlayMaterial.SetInt("_ZWrite",   0);
        _overlayMaterial.SetInt("_ZTest",    (int)UnityEngine.Rendering.CompareFunction.Always);
        _overlayMaterial.renderQueue = 4000;
    }
}
