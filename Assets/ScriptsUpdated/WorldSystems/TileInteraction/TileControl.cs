using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TileContentType
{
    None,
    Environment,
    Building
}

public class TileControl : MonoBehaviour
{
    [Header("Content")]
    [SerializeField] private TileContentType _tileContentType = TileContentType.None;
    public TileContentType tileContentType => _tileContentType;

    [SerializeField] private EnvironmentControl environmentControl; // set if an environment lives here
    public EnvironmentControl EnvironmentControl => environmentControl;

    [SerializeField] private BuildingControl buildingControl; // set if a building lives here
    public BuildingControl BuildingControl => buildingControl;

    [Header("Tile Interactivity")]
    public bool isInteractable = true;

    [Header("Emission Highlight (Standard Shader)")]
    [SerializeField] private Color emissionOnColor = new Color(0.35f, 0.35f, 0.35f, 1f);
    [SerializeField, Range(0f, 10f)] private float emissionIntensity = 3f;
    [SerializeField] private Color emissionOffColor = Color.black;

    [Header("Extra Materials To Prepare (eg. Undiscovered Material)")]
    [Tooltip("Drag any materials here that may be swapped onto this tile at runtime (eg. Environment 'Undiscovered' material).")]
    [SerializeField] private List<Material> extraMaterialsToPrepare = new List<Material>();

    [Header("Debug")]
    [SerializeField] private bool debugEmission = false;

    private MeshRenderer[] meshRenderers;

    // MaterialPropertyBlock = stable on Android, avoids material instancing issues
    private MaterialPropertyBlock mpb;
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    private bool emissionOn = false;

    private void Awake()
    {
        meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
        mpb = new MaterialPropertyBlock();

        // Prep emission keyword on any materials currently on renderers
        EnsureSharedMaterialsHaveEmissionKeyword();

        // Prep any materials that might be swapped later (eg undiscovered material)
        PrepareExtraMaterials();

        RefreshContentType(); // detect Building / Environment / None
    }

    public void RefreshContentType()
    {
        buildingControl = GetComponentInChildren<BuildingControl>(true);
        if (buildingControl != null)
        {
            _tileContentType = TileContentType.Building;
            return;
        }

        environmentControl = GetComponentInChildren<EnvironmentControl>(true);
        _tileContentType = environmentControl != null ? TileContentType.Environment : TileContentType.None;
    }

    private void OnTransformChildrenChanged()
    {
        RefreshContentType();
    }

    public bool IsInteractable() => isInteractable;

    public void SelectTile()
    {
        if (!isInteractable) return;
        EnableEmission();

        if (debugEmission) DebugLogEmissionState("SelectTile");
    }

    public void DeselectTile()
    {
        DisableEmission();

        if (debugEmission) DebugLogEmissionState("DeselectTile");
    }

    public void ToggleEmission()
    {
        if (!isInteractable) return;

        if (emissionOn) DisableEmission();
        else EnableEmission();
    }

    private void EnableEmission()
    {
        if (emissionOn) return;

        Color final = emissionOnColor * emissionIntensity;

        foreach (var mr in meshRenderers)
        {
            if (!mr) continue;

            mr.GetPropertyBlock(mpb);
            mpb.SetColor(EmissionColorID, final);
            mr.SetPropertyBlock(mpb);
        }

        emissionOn = true;
    }

    private void DisableEmission()
    {
        if (!emissionOn) return;

        foreach (var mr in meshRenderers)
        {
            if (!mr) continue;

            mr.GetPropertyBlock(mpb);
            mpb.SetColor(EmissionColorID, emissionOffColor);
            mr.SetPropertyBlock(mpb);
        }

        emissionOn = false;
    }

    /// <summary>
    /// Call this from other scripts if they create/swap a material at runtime (eg EnvironmentControl).
    /// This ensures the material has the _EMISSION keyword enabled so MPB emission works reliably in builds.
    /// </summary>
    public void RegisterExtraMaterial(Material mat)
    {
        if (!mat) return;

        if (!extraMaterialsToPrepare.Contains(mat))
            extraMaterialsToPrepare.Add(mat);

        PrepareEmissionOnMaterialAsset(mat);
    }

    private void PrepareExtraMaterials()
    {
        if (extraMaterialsToPrepare == null) return;

        for (int i = 0; i < extraMaterialsToPrepare.Count; i++)
        {
            var mat = extraMaterialsToPrepare[i];
            PrepareEmissionOnMaterialAsset(mat);
        }
    }

    private void EnsureSharedMaterialsHaveEmissionKeyword()
    {
        foreach (var mr in meshRenderers)
        {
            if (!mr) continue;

            // Touch MATERIAL ASSETS (sharedMaterials), not instances.
            var shared = mr.sharedMaterials;
            for (int i = 0; i < shared.Length; i++)
            {
                PrepareEmissionOnMaterialAsset(shared[i]);
            }
        }
    }

    private static void PrepareEmissionOnMaterialAsset(Material mat)
    {
        if (!mat) return;

        // Only Standard/compatible shaders will have this property
        if (!mat.HasProperty("_EmissionColor"))
            return;

        // Ensure the keyword is enabled on the asset so builds include the right variant.
        if (!mat.IsKeywordEnabled("_EMISSION"))
            mat.EnableKeyword("_EMISSION");

        // Optional but helpful: ensure it isn't treated as "black emissive"
        mat.globalIlluminationFlags &= ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;

        // Keep default off (black). MPB will override per-renderer when selected.
        // (This writes to the asset. If you don't want that, comment it out.)
        mat.SetColor("_EmissionColor", Color.black);
    }

    // You can keep this if something else still uses it.
    public Vector2Int GetGridPosition()
    {
        var gridManager = FindObjectOfType<GridManager>();
        if (gridManager != null) return gridManager.GetGridPosition(transform.position);
        return Vector2Int.zero;
    }

    public bool IsEmissionOn() => emissionOn;

    private void DebugLogEmissionState(string context)
    {
        int rendererCount = meshRenderers != null ? meshRenderers.Length : 0;
        int printed = 0;

        foreach (var mr in meshRenderers)
        {
            if (!mr) continue;

            mr.GetPropertyBlock(mpb);
            Color pbCol = mpb.GetColor(EmissionColorID);

            var smats = mr.sharedMaterials;
            string matInfo = (smats != null && smats.Length > 0 && smats[0] != null)
                ? $"sharedMat='{smats[0].name}' shader='{smats[0].shader.name}' KW={smats[0].IsKeywordEnabled("_EMISSION")} _EmissionColor={smats[0].GetColor("_EmissionColor")}"
                : "no sharedMat";

            if (printed < 6)
            {
                Debug.Log(
                    $"[{name}] {context} | type={_tileContentType} emissionOnFlag={emissionOn} " +
                    $"Renderer='{mr.name}' PB_EmissionColor={pbCol} {matInfo}"
                );
                printed++;
            }
        }

        Debug.Log($"[{name}] {context} | renderers={rendererCount} emissionOnFlag={emissionOn}");
    }
}