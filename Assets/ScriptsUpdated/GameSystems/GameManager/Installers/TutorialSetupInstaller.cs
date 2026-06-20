using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TutorialSetupInstaller : MonoBehaviour
{
    public enum PartType { Static, CameraDrag, CameraZoom, MinimapRotate, ShelterPlacement, HighlightAdjacent, OpenUndiscoveredTile, OpenDiscoveryDetails }

    [Header("Tutorial Parts (shown in order)")]
    [SerializeField] private GameObject[] tutorialParts;
    [SerializeField] private PartType[] partTypes;

    [Header("Button Lookup")]
    [SerializeField] private string nextButtonName = "NextButton";

    [Header("Interaction Thresholds")]
    [SerializeField] private float pinchDeltaThreshold = 20f;
    [SerializeField] private float minimapRotateYawThreshold = 20f;

    [Header("Shelter Placement Part")]
    [SerializeField] private string shelterBuildingID = "";

    private CameraControl _cameraControl;
    private TileActivator _tileActivator;
    private int _currentPart = -1;
    private Button _activeNextButton;

    private bool _waitingForDrag;
    private bool _waitingForDragRelease;
    private bool _waitingForZoom;
    private bool _waitingForRotate;
    private bool _zoomedIn;
    private bool _zoomedOut;
    private bool _startedMinimapRotate;
    private float _minimapRotateStartYaw;

    private Vector2Int _placedShelterGridPos;
    private Vector3 _placedShelterWorldPos;
    private readonly List<TileControl> _highlightedTileControls = new List<TileControl>();
    private bool _shouldRestoreCameraPose;

    private bool _waitingForUndiscoveredPanel;
    private UndiscoveredTilePanelControl _undiscoveredPanel;

    private bool _waitingForDiscoveryDetails;
    private DiscoveryDetailsPanelControl _discoveryDetailsPanel;

    public Scene LoadedScene => gameObject.scene;

    public void InstallRefs(CameraControl cameraControl, TileActivator tileActivator)
    {
        _cameraControl = cameraControl;

        if (_tileActivator != null)
            _tileActivator.OnTilesActivated -= OnWorldSpawned;

        _tileActivator = tileActivator;

        if (_tileActivator != null)
            _tileActivator.OnTilesActivated += OnWorldSpawned;
    }

    private void OnDestroy()
    {
        if (_tileActivator != null)
            _tileActivator.OnTilesActivated -= OnWorldSpawned;

        UnbindActiveNextButton();
    }

    private void Update()
    {
        if (_waitingForDrag)
        {
            if (_cameraControl != null && _cameraControl.IsDragging())
            {
                _waitingForDrag = false;
                _waitingForDragRelease = true;
            }
            return;
        }

        if (_waitingForDragRelease)
        {
            if (_cameraControl != null && !_cameraControl.IsDragging()
                && !Input.GetMouseButton(0) && Input.touchCount == 0)
            {
                _waitingForDragRelease = false;
                ShowPart(_currentPart + 1);
            }
            return;
        }

        if (_waitingForZoom)
        {
            int dir = GetZoomDirectionThisFrame();
            if (dir > 0) _zoomedIn = true;
            else if (dir < 0) _zoomedOut = true;

            if (_zoomedIn && _zoomedOut)
            {
                _waitingForZoom = false;
                ShowPart(_currentPart + 1);
            }
            return;
        }

        if (_waitingForRotate)
        {
            if (_cameraControl == null) return;

            if (_cameraControl.IsRotatingFromMinimap())
            {
                float currentYaw = _cameraControl.GetCurrentYaw();

                if (!_startedMinimapRotate)
                {
                    _startedMinimapRotate = true;
                    _minimapRotateStartYaw = currentYaw;
                }

                float yawDelta = Mathf.Abs(Mathf.DeltaAngle(_minimapRotateStartYaw, currentYaw));
                if (yawDelta >= minimapRotateYawThreshold)
                {
                    _waitingForRotate = false;
                    ShowPart(_currentPart + 1);
                }
            }
            return;
        }
    }

    private void OnWorldSpawned()
    {
        TurnSystem.Instance?.PauseTurnTimer();

        if (_cameraControl != null)
        {
            _cameraControl.SetTutorialInputRestrictions(
                restrictInput: true,
                allowWorldDrag: false,
                allowZoom: false,
                allowMinimapRotation: false);

            CenterCameraOnMap();
        }

        ShowPart(0);
    }

    private void CenterCameraOnMap()
    {
        GridManager gm = GridManager.Instance;
        if (gm == null || _cameraControl == null)
            return;

        float centerX = (gm.columns / 2f) * gm.cellSize;
        float centerZ = (gm.rows / 2f) * gm.cellSize;
        Transform camT = _cameraControl.transform;
        camT.position = new Vector3(centerX, camT.position.y, centerZ);
    }

    private void OnNextPressed()
    {
        ShowPart(_currentPart + 1);
    }

    private void ShowPart(int index)
    {
        if (tutorialParts == null || tutorialParts.Length == 0)
            return;

        if (_currentPart >= 0 && _currentPart < tutorialParts.Length && tutorialParts[_currentPart] != null)
            tutorialParts[_currentPart].SetActive(false);

        UnbindActiveNextButton();
        ClearInteractiveState();

        _currentPart = index;

        if (_currentPart >= tutorialParts.Length || tutorialParts[_currentPart] == null)
            return;

        tutorialParts[_currentPart].SetActive(true);

        switch (GetPartType(_currentPart))
        {
            case PartType.Static:
                _activeNextButton = FindNextButton(tutorialParts[_currentPart]);
                if (_activeNextButton != null)
                {
                    _activeNextButton.gameObject.SetActive(true);
                    _activeNextButton.interactable = true;
                    _activeNextButton.onClick.AddListener(OnNextPressed);
                }
                break;

            case PartType.CameraDrag:
                if (_cameraControl != null)
                    _cameraControl.SetTutorialInputRestrictions(
                        restrictInput: true,
                        allowWorldDrag: true,
                        allowZoom: false,
                        allowMinimapRotation: false);
                _waitingForDrag = true;
                break;

            case PartType.CameraZoom:
                if (_cameraControl != null)
                    _cameraControl.SetTutorialInputRestrictions(
                        restrictInput: true,
                        allowWorldDrag: false,
                        allowZoom: true,
                        allowMinimapRotation: false);
                _waitingForZoom = true;
                _zoomedIn = false;
                _zoomedOut = false;
                break;

            case PartType.MinimapRotate:
                if (_cameraControl != null)
                    _cameraControl.SetTutorialInputRestrictions(
                        restrictInput: true,
                        allowWorldDrag: false,
                        allowZoom: false,
                        allowMinimapRotation: true);
                _waitingForRotate = true;
                _startedMinimapRotate = false;
                _minimapRotateStartYaw = 0f;
                break;

            case PartType.ShelterPlacement:
                if (_cameraControl != null)
                    _cameraControl.SetTutorialInputRestrictions(
                        restrictInput: true,
                        allowWorldDrag: false,
                        allowZoom: false,
                        allowMinimapRotation: false);
                PlaceShelterOnMap();
                _activeNextButton = FindNextButton(tutorialParts[_currentPart]);
                if (_activeNextButton != null)
                {
                    _activeNextButton.gameObject.SetActive(true);
                    _activeNextButton.interactable = true;
                    _activeNextButton.onClick.AddListener(OnNextPressed);
                }
                break;

            case PartType.OpenUndiscoveredTile:
                if (_cameraControl != null)
                    _cameraControl.SetTutorialInputRestrictions(
                        restrictInput: true,
                        allowWorldDrag: true,
                        allowZoom: false,
                        allowMinimapRotation: false);
                TileInteraction.SetSelectionEnabled(true);
                if (_undiscoveredPanel == null)
                    _undiscoveredPanel = FindFirstObjectByType<UndiscoveredTilePanelControl>(FindObjectsInactive.Include);
                if (_undiscoveredPanel != null)
                {
                    _undiscoveredPanel.OnOpen += OnUndiscoveredPanelOpened;
                    _waitingForUndiscoveredPanel = true;
                }
                break;

            case PartType.OpenDiscoveryDetails:
                if (_discoveryDetailsPanel == null)
                    _discoveryDetailsPanel = FindFirstObjectByType<DiscoveryDetailsPanelControl>(FindObjectsInactive.Include);
                if (_discoveryDetailsPanel != null)
                {
                    _discoveryDetailsPanel.OnOpen += OnDiscoveryDetailsPanelOpened;
                    _waitingForDiscoveryDetails = true;
                }
                break;

            case PartType.HighlightAdjacent:
                if (_cameraControl != null)
                {
                    _cameraControl.SetTutorialInputRestrictions(
                        restrictInput: true,
                        allowWorldDrag: false,
                        allowZoom: false,
                        allowMinimapRotation: false);
                    _cameraControl.SaveCameraPose();
                    _shouldRestoreCameraPose = true;
                    _cameraControl.FocusTopDownOnPoint(_placedShelterWorldPos, float.MaxValue);
                }
                HighlightTilesAroundShelter();
                _activeNextButton = FindNextButton(tutorialParts[_currentPart]);
                if (_activeNextButton != null)
                {
                    _activeNextButton.gameObject.SetActive(true);
                    _activeNextButton.interactable = true;
                    _activeNextButton.onClick.AddListener(OnNextPressed);
                }
                break;
        }
    }

    private void PlaceShelterOnMap()
    {
        if (string.IsNullOrEmpty(shelterBuildingID) || BuildingManager.Instance == null)
            return;

        Building buildingDef = BuildingManager.Instance.GetBuildingByID(shelterBuildingID);
        if (buildingDef == null)
            return;

        EnvironmentControl[] allEnvs = FindObjectsByType<EnvironmentControl>(FindObjectsSortMode.None);
        List<EnvironmentControl> candidates = new List<EnvironmentControl>();

        foreach (EnvironmentControl env in allEnvs)
        {
            bool typeOk = buildingDef.requiredEnvironmentTypes == null
                || buildingDef.requiredEnvironmentTypes.Count == 0
                || buildingDef.requiredEnvironmentTypes.Contains(env.environmentType);

            bool tileTypeOk = buildingDef.requiredEnvironmentTileTypes == null
                || buildingDef.requiredEnvironmentTileTypes.Count == 0
                || buildingDef.requiredEnvironmentTileTypes.Contains(env.environmentTileType);

            bool sizeOk = env.tileSize == buildingDef.requiredTileSize;

            if (typeOk && tileTypeOk && sizeOk)
                candidates.Add(env);
        }

        if (candidates.Count == 0)
            return;

        EnvironmentControl target = candidates[Random.Range(0, candidates.Count)];
        Vector3 worldPos = target.transform.position;
        Vector3 envForward = target.transform.forward;

        _placedShelterWorldPos = worldPos;
        if (GridManager.Instance != null)
            _placedShelterGridPos = GridManager.Instance.GetGridPosition(worldPos);

        GameObject prefab = buildingDef.finalBuildingPrefab != null
            ? buildingDef.finalBuildingPrefab
            : buildingDef.buildingPrefab;

        if (prefab != null)
            Instantiate(prefab, worldPos, target.transform.rotation);

        // Remove environment tile the same way BuildingPlacementManager does
        TileControl tileControl = target.GetComponent<TileControl>();
        GameObject toDestroy = (tileControl != null && tileControl.transform.parent != null)
            ? tileControl.transform.parent.gameObject
            : target.gameObject;
        Destroy(toDestroy);

        _cameraControl?.FocusOnPoint(worldPos, envForward, 6f);
    }

    private void OnUndiscoveredPanelOpened()
    {
        if (!_waitingForUndiscoveredPanel) return;
        _waitingForUndiscoveredPanel = false;
        if (_undiscoveredPanel != null)
            _undiscoveredPanel.OnOpen -= OnUndiscoveredPanelOpened;
        ShowPart(_currentPart + 1);
    }

    private void OnDiscoveryDetailsPanelOpened()
    {
        if (!_waitingForDiscoveryDetails) return;
        _waitingForDiscoveryDetails = false;
        if (_discoveryDetailsPanel != null)
            _discoveryDetailsPanel.OnOpen -= OnDiscoveryDetailsPanelOpened;
        ShowPart(_currentPart + 1);
    }

    private void HighlightTilesAroundShelter()
    {
        GridManager gm = GridManager.Instance;
        if (gm == null) return;

        float half = gm.cellSize * 0.4f;

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dz == 0) continue;

                int nx = _placedShelterGridPos.x + dx;
                int nz = _placedShelterGridPos.y + dz;

                if (nx < 0 || nx >= gm.columns || nz < 0 || nz >= gm.rows)
                    continue;

                Vector3 neighborPos = gm.GetWorldPosition(nx, nz);
                Collider[] hits = Physics.OverlapBox(neighborPos, new Vector3(half, 2f, half));

                foreach (Collider col in hits)
                {
                    TileControl tc = col.GetComponentInParent<TileControl>();
                    if (tc == null) tc = col.GetComponent<TileControl>();
                    if (tc != null && !_highlightedTileControls.Contains(tc))
                    {
                        tc.SelectTile();
                        _highlightedTileControls.Add(tc);
                    }
                }
            }
        }
    }

    private Button FindNextButton(GameObject part)
    {
        if (part == null)
            return null;

        Button[] buttons = part.GetComponentsInChildren<Button>(true);

        if (buttons.Length == 0)
            return null;

        if (!string.IsNullOrEmpty(nextButtonName))
        {
            foreach (Button btn in buttons)
            {
                if (btn.gameObject.name == nextButtonName)
                    return btn;
            }
        }

        return buttons[0];
    }

    private PartType GetPartType(int index)
    {
        if (partTypes != null && index < partTypes.Length)
            return partTypes[index];
        return PartType.Static;
    }

    private void ClearInteractiveState()
    {
        _waitingForDrag = false;
        _waitingForDragRelease = false;
        _waitingForZoom = false;
        _waitingForRotate = false;
        _zoomedIn = false;
        _zoomedOut = false;
        _startedMinimapRotate = false;
        _minimapRotateStartYaw = 0f;

        foreach (TileControl tc in _highlightedTileControls)
            if (tc != null) tc.DeselectTile();
        _highlightedTileControls.Clear();

        if (_shouldRestoreCameraPose && _cameraControl != null)
        {
            _cameraControl.RestoreCameraPose();
            _shouldRestoreCameraPose = false;
        }

        if (_waitingForUndiscoveredPanel && _undiscoveredPanel != null)
        {
            _undiscoveredPanel.OnOpen -= OnUndiscoveredPanelOpened;
            _waitingForUndiscoveredPanel = false;
        }

        if (_waitingForDiscoveryDetails && _discoveryDetailsPanel != null)
        {
            _discoveryDetailsPanel.OnOpen -= OnDiscoveryDetailsPanelOpened;
            _waitingForDiscoveryDetails = false;
        }
    }

    private void UnbindActiveNextButton()
    {
        if (_activeNextButton != null)
        {
            _activeNextButton.onClick.RemoveListener(OnNextPressed);
            _activeNextButton = null;
        }
    }

    private int GetZoomDirectionThisFrame()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0.0001f) return 1;
        if (scroll < -0.0001f) return -1;

        if (Input.touchCount == 2)
        {
            Touch a = Input.GetTouch(0);
            Touch b = Input.GetTouch(1);
            Vector2 aPrev = a.position - a.deltaPosition;
            Vector2 bPrev = b.position - b.deltaPosition;
            float delta = Vector2.Distance(a.position, b.position) - Vector2.Distance(aPrev, bPrev);
            if (delta >= pinchDeltaThreshold) return 1;
            if (delta <= -pinchDeltaThreshold) return -1;
        }

        return 0;
    }
}
