using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnvironmentTileTutorial : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CameraControl cameraControl;

    [Header("UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup rootCanvasGroup;
    [SerializeField] private GameObject messagePanel;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button skipButton;

    [Header("Camera Focus")]
    [SerializeField] private float focusHeight = 12f;
    [SerializeField] private float focusYaw = 0f;

    [Header("Message")]
    [TextArea]
    [SerializeField]
    private string introMessage =
        "These are the environment tiles around your starting tile.";

    [Header("Overlap Highlight")]
    [SerializeField] private Vector3 colliderExpansion = new Vector3(6f, 2f, 6f);
    [SerializeField] private LayerMask overlapMask = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

    [SerializeField] private PopulationTutorial populationTutorial;

    private readonly List<TileControl> _highlightedTiles = new List<TileControl>();

    private bool _running;
    private bool _completedThisGame;
    private bool _cameraLockedByTutorial;

    private GameObject _starterTarget;
    private Transform _starterRoot;
    private BoxCollider _starterBoxCollider;
    private TileControl _starterTileControl;
    private Vector3 _starterFocusPoint;
    private bool _hasStarterTarget;

    public bool IsRunning => _running;
    public bool HasCompletedTutorial => _completedThisGame;

    public bool ShouldRunTutorial()
    {
        return !_running && !_completedThisGame && _hasStarterTarget && _starterBoxCollider != null;
    }

    private void Awake()
    {
        if (cameraControl == null)
            cameraControl = FindObjectOfType<CameraControl>();

        BindButtons();
        SetRootVisible(false);
        SetBlockingMode(false);
    }

    // Kept compatible with your current installer chain.
    public void InstallRuntimeRefs(
        CameraControl newCameraControl = null,
        GridManager unusedGridManager = null,
        MonoEnvironmentDataSource unusedEnvironmentDataSource = null)
    {
        if (newCameraControl != null)
            cameraControl = newCameraControl;

        BindButtons();
    }

    public void SetStarterTarget(GameObject starterTarget)
    {
        _starterTarget = starterTarget;
        _starterRoot = starterTarget != null ? starterTarget.transform : null;
        _starterBoxCollider = null;
        _starterTileControl = null;
        _starterFocusPoint = starterTarget != null ? starterTarget.transform.position : Vector3.zero;
        _hasStarterTarget = false;

        if (_starterTarget == null)
            return;

        _starterBoxCollider =
            _starterTarget.GetComponent<BoxCollider>() ??
            _starterTarget.GetComponentInChildren<BoxCollider>(true) ??
            _starterTarget.GetComponentInParent<BoxCollider>();

        _starterTileControl =
            _starterTarget.GetComponent<TileControl>() ??
            _starterTarget.GetComponentInChildren<TileControl>(true) ??
            _starterTarget.GetComponentInParent<TileControl>();

        if (_starterBoxCollider != null)
        {
            _starterFocusPoint = _starterBoxCollider.bounds.center;
            _hasStarterTarget = true;
        }
        else
        {
            Debug.LogWarning("[EnvironmentTileTutorial] Starter target has no BoxCollider to expand from.");
        }
    }

    // Safe to keep because other scripts may still call it.
    // It only updates the focus point and does not replace the starter collider target.
    public void SetStarterWorldPoint(Vector3 worldPoint)
    {
        _starterFocusPoint = worldPoint;
    }

    private void BindButtons()
    {
        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(OnContinuePressed);
            continueButton.onClick.AddListener(OnContinuePressed);
        }

        if (skipButton != null)
        {
            skipButton.onClick.RemoveListener(OnSkipPressed);
            skipButton.onClick.AddListener(OnSkipPressed);
        }
    }

    public void BeginTutorial()
    {
        if (!ShouldRunTutorial())
        {
            TurnSystem.Instance?.ResumeTurnTimer();
            return;
        }

        _running = true;

        if (cameraControl != null)
        {
            cameraControl.SaveCameraPose();
            cameraControl.PushInputLock();
            _cameraLockedByTutorial = true;
            cameraControl.FocusTopDownOnPoint(_starterFocusPoint, focusHeight, focusYaw);
        }

        HighlightTilesAroundStarter();

        SetRootVisible(true);
        SetBlockingMode(true);

        if (messagePanel != null)
            messagePanel.SetActive(true);

        if (continueButton != null)
            continueButton.gameObject.SetActive(true);

        if (skipButton != null)
            skipButton.gameObject.SetActive(false);

        SetMessage(introMessage);
    }

    private void OnContinuePressed()
    {
        CompleteTutorial();
    }

    private void OnSkipPressed()
    {
        SkipTutorial();
    }

    public void SkipTutorial()
    {
        FinishTutorial(markComplete: true);
    }

    private void CompleteTutorial()
    {
        FinishTutorial(markComplete: true);
    }

    private void FinishTutorial(bool markComplete)
    {
        _running = false;

        if (markComplete)
            _completedThisGame = true;

        ClearHighlights();

        if (_cameraLockedByTutorial && cameraControl != null)
        {
            cameraControl.PopInputLock();
            _cameraLockedByTutorial = false;
        }

        if (cameraControl != null)
            cameraControl.RestoreCameraPose();

        SetBlockingMode(false);
        SetRootVisible(false);

        BeginNextTutorialOrResume();
    }

    private void BeginNextTutorialOrResume()
    {
        if (populationTutorial != null && populationTutorial.ShouldRunTutorial())
        {
            populationTutorial.BeginTutorial();
            return;
        }

        TurnSystem.Instance?.ResumeTurnTimer();
        TileInteraction.SetSelectionEnabled(false);
        TileInteraction.GetInstance()?.EnableSelectionAfter(0.01f);
    }

    public void ResetTutorialForNewGame()
    {
        _running = false;
        _completedThisGame = false;
        _starterTarget = null;
        _starterRoot = null;
        _starterBoxCollider = null;
        _starterTileControl = null;
        _starterFocusPoint = Vector3.zero;
        _hasStarterTarget = false;

        ClearHighlights();

        if (_cameraLockedByTutorial && cameraControl != null)
        {
            cameraControl.PopInputLock();
            _cameraLockedByTutorial = false;
        }

        SetBlockingMode(false);
        SetRootVisible(false);
    }

    private void HighlightTilesAroundStarter()
    {
        ClearHighlights();

        if (_starterBoxCollider == null)
            return;

        Bounds expandedBounds = _starterBoxCollider.bounds;
        expandedBounds.Expand(colliderExpansion);

        Collider[] hits = Physics.OverlapBox(
            expandedBounds.center,
            expandedBounds.extents,
            Quaternion.identity,
            overlapMask,
            triggerInteraction
        );

        HashSet<TileControl> uniqueTiles = new HashSet<TileControl>();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
                continue;

            TileControl tileControl =
                hit.GetComponent<TileControl>() ??
                hit.GetComponentInParent<TileControl>() ??
                hit.GetComponentInChildren<TileControl>(true);

            if (tileControl == null)
                continue;

            if (_starterTileControl != null && tileControl == _starterTileControl)
                continue;

            if (_starterRoot != null && tileControl.transform.IsChildOf(_starterRoot))
                continue;

            if (!uniqueTiles.Add(tileControl))
                continue;

            tileControl.SelectTile();
            _highlightedTiles.Add(tileControl);
        }
    }

    private void ClearHighlights()
    {
        for (int i = 0; i < _highlightedTiles.Count; i++)
        {
            if (_highlightedTiles[i] != null)
                _highlightedTiles[i].DeselectTile();
        }

        _highlightedTiles.Clear();
    }

    private void SetMessage(string value)
    {
        if (messageText != null)
            messageText.text = value;
    }

    private void SetRootVisible(bool visible)
    {
        if (root != null)
            root.SetActive(visible);
    }

    private void SetBlockingMode(bool blocking)
    {
        if (rootCanvasGroup == null)
            return;

        rootCanvasGroup.blocksRaycasts = blocking;
        rootCanvasGroup.interactable = blocking;
    }
}