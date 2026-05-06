using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TileWorldCanvasToggleButton : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button toggleButton;

    [Header("UnitsOnly Filtering")]
    [SerializeField] private bool hideEmptyUnitCanvases = true;

    [Header("Startup")]
    [SerializeField] private bool startUnitsOnly = false;

    [Header("Root Names (match hierarchy parents)")]
    [SerializeField]
    private string[] unitRootNames =
    {
        "UnitCanvas",
        "UnitWorldCanvas"
    };

    [SerializeField]
    private string[] actionRootNames =
    {
        "UnitTileActions"
    };

    [SerializeField]
    private string[] environmentRootNames =
    {
        "EnvironmentTileCanvas",
        "EnvironmentWorldCanvas"
    };

    [SerializeField]
    private string[] buildingRootNames =
    {
        "BuildingTileCanvas",
        "BuildingWorldCanvas"
    };

    [SerializeField]
    private string[] animalRootNames =
    {
        "AnimalCanvas",
        "AnimalTileCanvas",
        "AnimalWorldCanvas"
    };

    [Header("Name Matching")]
    [Tooltip("If true, 'BuildingTileCanvas (1)' etc will match via StartsWith.")]
    [SerializeField] private bool allowStartsWithMatch = true;

    [Header("Performance")]
    [Tooltip("How many canvases to process per frame while toggling.")]
    [SerializeField] private int canvasesPerFrame = 60;

    [Tooltip("How many TileWorldCanvasVisibility refreshes per frame.")]
    [SerializeField] private int visibilityRefreshPerFrame = 80;

    private struct CanvasState
    {
        public GameObject go;
        public bool activeSelf;
        public bool canvasEnabled;
    }

    private readonly Dictionary<int, CanvasState> _suppressedCache = new Dictionary<int, CanvasState>();

    private Coroutine _applyRoutine;
    private int _applyVersion = 0;

    private void Awake()
    {
        if (toggleButton != null)
            toggleButton.onClick.AddListener(OnTogglePressed);
    }

    private void OnEnable()
    {
        WorldCanvasMode.OnChanged += OnModeChanged;

        WorldCanvasMode.SetUnitsOnly(startUnitsOnly);
        OnModeChanged(WorldCanvasMode.UnitsOnly);
    }

    private void OnDisable()
    {
        WorldCanvasMode.OnChanged -= OnModeChanged;

        if (_applyRoutine != null)
        {
            StopCoroutine(_applyRoutine);
            _applyRoutine = null;
        }

        if (toggleButton != null)
            toggleButton.onClick.RemoveListener(OnTogglePressed);
    }

    private void OnTogglePressed()
    {
        WorldCanvasMode.Toggle();
    }

    private void OnModeChanged(bool unitsOnly)
    {
        _applyVersion++;
        if (_applyRoutine != null)
        {
            StopCoroutine(_applyRoutine);
            _applyRoutine = null;
        }

        _applyRoutine = StartCoroutine(ApplyBatched(unitsOnly, _applyVersion));
    }

    private bool CanvasTileHasUnits(Canvas canvas)
    {
        var groupCtrl = canvas.GetComponentInParent<TileUnitGroupControl>(true);
        if (groupCtrl == null) return true;
        return groupCtrl.Groups != null && groupCtrl.Groups.Count > 0;
    }

    private bool ActionCanvasShouldShow(Canvas canvas)
    {
        if (canvas == null)
            return false;

        var moveUIs = canvas.GetComponentsInChildren<TileMovementUI>(true);
        for (int i = 0; i < moveUIs.Length; i++)
        {
            var ui = moveUIs[i];
            if (ui == null) continue;

            if (ui.moveHereButton != null && ui.moveHereButton.gameObject.activeSelf)
                return true;

            if (ui.scoutButton != null && ui.scoutButton.gameObject.activeSelf)
                return true;
        }

        var tile = canvas.GetComponentInParent<TileControl>(true);
        if (tile != null && TrackingMarkerManager.Instance != null)
        {
            if (TrackingMarkerManager.Instance.IsMarkerActive(tile))
                return true;
        }

        var trackingUIs = canvas.GetComponentsInChildren<TileTrackingMarkerUI>(true);
        for (int i = 0; i < trackingUIs.Length; i++)
        {
            var ui = trackingUIs[i];
            if (ui == null) continue;

            if (ui.root != null)
            {
                if (ui.root.activeSelf)
                    return true;
            }
            else if (ui.gameObject.activeSelf)
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerator ApplyBatched(bool unitsOnly, int version)
    {
        var canvases = FindObjectsOfType<Canvas>(true);

        if (unitsOnly)
        {
            for (int i = 0; i < canvases.Length; i++)
            {
                if (version != _applyVersion) yield break;

                var canvas = canvases[i];
                if (canvas == null) continue;
                if (canvas.renderMode != RenderMode.WorldSpace) continue;

                bool isUnit = HasAncestorNamed(canvas.transform, unitRootNames);
                bool isAction = HasAncestorNamed(canvas.transform, actionRootNames);
                bool isAnimal = HasAncestorNamed(canvas.transform, animalRootNames);
                bool isEnv = HasAncestorNamed(canvas.transform, environmentRootNames);
                bool isBuilding = HasAncestorNamed(canvas.transform, buildingRootNames);

                if (!isUnit && !isAction && !isAnimal && !isEnv && !isBuilding)
                    continue;

                if (isAction)
                {
                    if (ActionCanvasShouldShow(canvas))
                        ShowCanvas(canvas);
                    else
                        HideCanvas(canvas);
                }
                else if (isUnit)
                {
                    if (hideEmptyUnitCanvases && !CanvasTileHasUnits(canvas))
                        HideCanvas(canvas);
                    else
                        ShowCanvas(canvas);
                }
                else if (isAnimal)
                {
                    var animalUI =
                        canvas.GetComponent<TileAnimalUI>() ??
                        canvas.GetComponentInParent<TileAnimalUI>(true);

                    bool shouldShowAnimal = animalUI != null && animalUI.ShouldShowBattleCanvas();

                    if (shouldShowAnimal)
                        ShowCanvas(canvas);
                    else
                        HideCanvas(canvas);
                }
                else
                {
                    CacheIfNeeded(canvas);
                    HideCanvas(canvas);
                }

                if (canvasesPerFrame > 0 && (i % canvasesPerFrame) == 0)
                    yield return null;
            }
        }
        else
        {
            for (int i = 0; i < canvases.Length; i++)
            {
                if (version != _applyVersion) yield break;

                var canvas = canvases[i];
                if (canvas == null) continue;
                if (canvas.renderMode != RenderMode.WorldSpace) continue;

                bool isUnit = HasAncestorNamed(canvas.transform, unitRootNames);
                bool isAction = HasAncestorNamed(canvas.transform, actionRootNames);
                bool isAnimal = HasAncestorNamed(canvas.transform, animalRootNames);
                bool isEnv = HasAncestorNamed(canvas.transform, environmentRootNames);
                bool isBuilding = HasAncestorNamed(canvas.transform, buildingRootNames);

                if (!isUnit && !isAction && !isAnimal && !isEnv && !isBuilding)
                    continue;

                if (isUnit || isAction || isAnimal)
                {
                    HideCanvas(canvas);
                }
                else
                {
                    RestoreIfCached(canvas);
                }

                if (canvasesPerFrame > 0 && (i % canvasesPerFrame) == 0)
                    yield return null;
            }

            _suppressedCache.Clear();
        }

        var vis = FindObjectsOfType<TileWorldCanvasVisibility>(true);
        for (int i = 0; i < vis.Length; i++)
        {
            if (version != _applyVersion) yield break;

            if (vis[i] != null)
                vis[i].Refresh();

            if (visibilityRefreshPerFrame > 0 && (i % visibilityRefreshPerFrame) == 0)
                yield return null;
        }

        _applyRoutine = null;
    }

    private bool HasAncestorNamed(Transform t, string[] targetNames)
    {
        if (targetNames == null || targetNames.Length == 0) return false;

        Transform cur = t;
        while (cur != null)
        {
            if (MatchesAny(cur.gameObject.name, targetNames))
                return true;

            cur = cur.parent;
        }
        return false;
    }

    private bool MatchesAny(string actual, string[] targets)
    {
        for (int i = 0; i < targets.Length; i++)
        {
            string target = targets[i];
            if (string.IsNullOrEmpty(target)) continue;

            if (actual == target) return true;
            if (allowStartsWithMatch && actual.StartsWith(target)) return true;
        }
        return false;
    }

    private void CacheIfNeeded(Canvas canvas)
    {
        int id = canvas.GetInstanceID();
        if (_suppressedCache.ContainsKey(id)) return;

        _suppressedCache[id] = new CanvasState
        {
            go = canvas.gameObject,
            activeSelf = canvas.gameObject.activeSelf,
            canvasEnabled = canvas.enabled
        };
    }

    private void RestoreIfCached(Canvas canvas)
    {
        int id = canvas.GetInstanceID();

        if (_suppressedCache.TryGetValue(id, out var saved) && saved.go != null)
        {
            saved.go.SetActive(saved.activeSelf);
            canvas.enabled = saved.canvasEnabled;
        }
    }

    private void HideCanvas(Canvas canvas)
    {
        canvas.enabled = false;
        canvas.gameObject.SetActive(false);

        TileInteraction.SetSelectionEnabled(false);
        TileInteraction.GetInstance()?.EnableSelectionAfter(0.01f);
    }

    private void ShowCanvas(Canvas canvas)
    {
        canvas.gameObject.SetActive(true);
        canvas.enabled = true;

        TileInteraction.SetSelectionEnabled(false);
    }

    public void SetToggleButton(Button newToggleButton)
    {
        if (newToggleButton == null)
            return;

        if (toggleButton != null)
            toggleButton.onClick.RemoveListener(OnTogglePressed);

        toggleButton = newToggleButton;
        toggleButton.onClick.RemoveAllListeners();
        toggleButton.onClick.AddListener(OnTogglePressed);
    }
}