using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TutorialSetupInstaller : MonoBehaviour
{
    public enum PartType { Static, CameraDrag, CameraZoom, MinimapRotate, ShelterPlacement, HighlightAdjacent, OpenUndiscoveredTile, OpenDiscoveryDetails, CloseDiscoveryDetails, ClickDiscoverButton, ResumeOrSpeedUp, FastForwardDiscovery, TriggerConsumption, WaitForConsumptionDismiss, OpenInventoryPanel, CloseInventoryPanel, RemoveSpoiledFood, SelectDiscoveredTile, ClickSurveyButton, OpenSurveyPanel, CloseSurveyPanel, ClickGatherButton, OpenCollectedGoodsPanel, CloseCollectedGoodsPanel, ClickBuildButton, SelectBuildingItem, RegenerateMapDiscovered, SelectTinyGrasslandOrSavanna, OpenBuildingCostPanel, CloseBuildingCostPanel, ClickCatalogBuildButton, ShowCostSwitchButtons, ConfirmBuildingPlacement, SelectPlacedBuilding, OpenShelterPanel, CloseShelterPanel, CloseBuildingPanel, DamageBuilding, SelectDamagedBuilding }

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

    [Header("Remove Spoiled Food Part")]
    [SerializeField] private ResourceDefinition tutorialSpoiledFoodDef;
    [SerializeField] private int tutorialSpoiledFoodAmount = 3;

    [Header("Map Regeneration Part")]
    [SerializeField] private MapGenerator _mapGenerator;
    [SerializeField] private MapTilePlacer _mapTilePlacer;

    [Header("Tutorial Building Selection")]
    [SerializeField] private string tutorialGrasslandBuildingID = "";
    [SerializeField] private string tutorialSavannaBuildingID = "";

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
    private bool _waitingForDiscoverButton;
    private bool _waitingForResumeOrSpeed;
    private bool _waitingForTurnComplete;
    private UndiscoveredTilePanelControl _undiscoveredPanel;
    private EnvironmentControl _trackedDiscoveryEnv;
    private Coroutine _fastForwardRoutine;

    private bool _waitingForDiscoveryDetails;
    private bool _waitingForDiscoveryDetailsClose;
    private DiscoveryDetailsPanelControl _discoveryDetailsPanel;

    private bool _waitingForConsumptionDismiss;

    private bool _waitingForInventoryOpen;
    private bool _waitingForInventoryClose;
    private InventoryPanelControl _inventoryPanel;

    private bool _waitingForSpoiledFoodRemoval;
    private string _spoiledFoodTargetId;

    private bool _waitingForDiscoveredTileSelect;

    private bool _waitingForSurveyComplete;
    private DiscoveredTilePanelControl _discoveredTilePanel;

    private bool _waitingForSurveyPanelOpen;
    private bool _waitingForSurveyPanelClose;
    private SurveyPanelControl _surveyPanel;

    private bool _waitingForGatherClick;
    private Coroutine _fastForwardGatherRoutine;

    private bool _waitingForCollectedGoodsOpen;
    private bool _waitingForCollectedGoodsClose;
    private CollectedGoodsPanelControl _collectedGoodsPanel;

    private bool _waitingForBuildButtonClick;
    private bool _waitingForBuildingItemSelect;

    private GameObject _placedShelterBuilding;
    private Coroutine _regenRoutine;

    private bool _waitingForGrasslandOrSavannaSelect;
    private readonly List<TileControl> _grassSavannaHighlights = new List<TileControl>();
    private EnvironmentType _selectedGrassOrSavannaType;

    private bool _waitingForCostPanelOpen;
    private bool _waitingForCostPanelClose;
    private bool _waitingForCatalogBuild;
    private bool _waitingForCostSwitchNext;
    private BuildingCatalogItem _tutorialCatalogItem;

    private bool _waitingForPlacementConfirm;
    private Coroutine _constructionGhostRoutine;
    private Vector3 _placedBuildingWorldPos;
    private bool _waitingForBuildingTileSelect;

    private bool _waitingForShelterPanelOpen;
    private bool _waitingForShelterPanelClose;
    private bool _waitingForBuildingPanelClose;
    private ShelterPanelControl _shelterPanel;
    private BuildingPanelControl _buildingPanel;

    private Coroutine _damageBuildingRoutine;
    private bool _waitingForDamagedPanelOpen;
    private BuildingDamagedPanelControl _damagedPanel;
    private TileControl _placedBuildingTile;

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

        if (_waitingForSpoiledFoodRemoval && !string.IsNullOrEmpty(_spoiledFoodTargetId))
        {
            var inv = PlayerInventoryManager.Instance;
            if (inv == null) return;

            bool stillPresent = false;
            var stacks = inv.GetStacks(ResourceType.Food);
            for (int i = 0; i < stacks.Count; i++)
            {
                var s = stacks[i];
                if (s?.definition == null) continue;
                if (string.Equals(s.definition.resourceID, _spoiledFoodTargetId, System.StringComparison.OrdinalIgnoreCase) && s.amount > 0)
                {
                    stillPresent = true;
                    break;
                }
            }

            if (!stillPresent)
            {
                _waitingForSpoiledFoodRemoval = false;
                _spoiledFoodTargetId = null;
                ShowPart(_currentPart + 1);
            }
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

            case PartType.ResumeOrSpeedUp:
                if (_cameraControl != null)
                {
                    _cameraControl.SetTutorialInputRestrictions(
                        restrictInput: true,
                        allowWorldDrag: false,
                        allowZoom: false,
                        allowMinimapRotation: false);

                    if (_undiscoveredPanel == null)
                        _undiscoveredPanel = FindFirstObjectByType<UndiscoveredTilePanelControl>(FindObjectsInactive.Include);

                    EnvironmentControl discoveredEnv = _undiscoveredPanel != null
                        ? _undiscoveredPanel.CurrentEnvironment
                        : null;

                    if (discoveredEnv != null)
                        _cameraControl.FocusOnPoint(discoveredEnv.transform.position, discoveredEnv.transform.forward, 6f);
                }
                if (TurnSystem.Instance != null)
                {
                    TurnSystem.Instance.OnResumed += OnTurnResumedOrSpeeded;
                    TurnSystem.Instance.OnSpeedToggled += OnTurnResumedOrSpeeded;
                    _waitingForResumeOrSpeed = true;
                }
                break;

            case PartType.FastForwardDiscovery:
                EnvironmentControl.TutorialBypassTaskFailure = true;
                TurnSystem.OnStartOfTurn += OnTurnCompletedForFastForward;
                _waitingForTurnComplete = true;
                break;

            case PartType.TriggerConsumption:
                if (_cameraControl != null)
                    _cameraControl.SetTutorialInputRestrictions(
                        restrictInput: true,
                        allowWorldDrag: false,
                        allowZoom: false,
                        allowMinimapRotation: false);
                _activeNextButton = FindNextButton(tutorialParts[_currentPart]);
                if (_activeNextButton != null)
                {
                    _activeNextButton.gameObject.SetActive(true);
                    _activeNextButton.interactable = true;
                    _activeNextButton.onClick.AddListener(OnTriggerConsumptionNextPressed);
                }
                break;

            case PartType.WaitForConsumptionDismiss:
                PopulationConsumptionPanel.OnDismissed += OnConsumptionPanelDismissed;
                _waitingForConsumptionDismiss = true;
                break;

            case PartType.OpenInventoryPanel:
                if (_cameraControl != null)
                    _cameraControl.SetTutorialInputRestrictions(
                        restrictInput: true,
                        allowWorldDrag: false,
                        allowZoom: false,
                        allowMinimapRotation: false);
                if (_inventoryPanel == null)
                    _inventoryPanel = FindFirstObjectByType<InventoryPanelControl>(FindObjectsInactive.Include);
                if (_inventoryPanel != null)
                {
                    _inventoryPanel.OnOpen += OnInventoryPanelOpenedSwitchToFood;
                    _waitingForInventoryOpen = true;
                }
                break;

            case PartType.CloseInventoryPanel:
                if (_inventoryPanel == null)
                    _inventoryPanel = FindFirstObjectByType<InventoryPanelControl>(FindObjectsInactive.Include);
                if (_inventoryPanel != null)
                {
                    _inventoryPanel.OnClose += OnInventoryPanelClosed;
                    _waitingForInventoryClose = true;
                }
                break;

            case PartType.RemoveSpoiledFood:
                if (tutorialSpoiledFoodDef != null)
                {
                    PlayerInventoryManager.Instance?.TryAdd(tutorialSpoiledFoodDef, tutorialSpoiledFoodAmount);
                    if (_inventoryPanel == null)
                        _inventoryPanel = FindFirstObjectByType<InventoryPanelControl>(FindObjectsInactive.Include);
                    _spoiledFoodTargetId = tutorialSpoiledFoodDef.resourceID;
                    _inventoryPanel?.Refresh();
                    _inventoryPanel?.PinResourceToFirst(_spoiledFoodTargetId);
                    _waitingForSpoiledFoodRemoval = true;
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;

            case PartType.SelectDiscoveredTile:
            {
                TileControl allowedTile = null;
                if (_trackedDiscoveryEnv != null)
                    allowedTile = _trackedDiscoveryEnv.GetComponentInParent<TileControl>();

                if (allowedTile != null)
                {
                    if (_cameraControl != null)
                        _cameraControl.SetTutorialInputRestrictions(
                            restrictInput: true,
                            allowWorldDrag: true,
                            allowZoom: true,
                            allowMinimapRotation: true);
                    TileInteraction.SetTutorialAllowedTile(allowedTile);
                    TileInteraction.SetSelectionEnabled(true);
                    var ti = TileInteraction.GetInstance();
                    if (ti != null)
                    {
                        ti.OnTileSelected += OnDiscoveredTileSelected;
                        _waitingForDiscoveredTileSelect = true;
                    }
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.ClickSurveyButton:
            {
                if (_discoveredTilePanel == null)
                    _discoveredTilePanel = FindFirstObjectByType<DiscoveredTilePanelControl>(FindObjectsInactive.Include);

                if (_discoveredTilePanel != null && PlayerSurveyManager.Instance != null)
                {
                    _discoveredTilePanel.TutorialSurveyOverride = OnTutorialSurveyClicked;
                    PlayerSurveyManager.Instance.OnSurveyCompleted += OnSurveyCompletedForTutorial;
                    _waitingForSurveyComplete = true;
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.OpenSurveyPanel:
                if (_surveyPanel == null)
                    _surveyPanel = FindFirstObjectByType<SurveyPanelControl>(FindObjectsInactive.Include);
                if (_discoveredTilePanel == null)
                    _discoveredTilePanel = FindFirstObjectByType<DiscoveredTilePanelControl>(FindObjectsInactive.Include);
                if (_surveyPanel != null)
                {
                    _surveyPanel.OnOpen += OnSurveyPanelOpened;
                    _waitingForSurveyPanelOpen = true;
                    if (_discoveredTilePanel != null)
                        _discoveredTilePanel.TutorialSurveyOverride = OnTutorialSurveyPanelOpenClicked;
                }
                break;

            case PartType.CloseSurveyPanel:
                if (_surveyPanel == null)
                    _surveyPanel = FindFirstObjectByType<SurveyPanelControl>(FindObjectsInactive.Include);
                if (_surveyPanel != null)
                {
                    _surveyPanel.OnClose += OnSurveyPanelClosed;
                    _waitingForSurveyPanelClose = true;
                }
                break;

            case PartType.ClickDiscoverButton:
                if (_undiscoveredPanel == null)
                    _undiscoveredPanel = FindFirstObjectByType<UndiscoveredTilePanelControl>(FindObjectsInactive.Include);
                if (_undiscoveredPanel != null)
                {
                    _undiscoveredPanel.OnDiscoverPressed += OnDiscoverButtonClicked;
                    _waitingForDiscoverButton = true;
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

            case PartType.CloseDiscoveryDetails:
                if (_discoveryDetailsPanel == null)
                    _discoveryDetailsPanel = FindFirstObjectByType<DiscoveryDetailsPanelControl>(FindObjectsInactive.Include);
                if (_discoveryDetailsPanel != null)
                {
                    _discoveryDetailsPanel.OnClose += OnDiscoveryDetailsPanelClosed;
                    _waitingForDiscoveryDetailsClose = true;
                }
                break;

            case PartType.ClickGatherButton:
            {
                if (_discoveredTilePanel == null)
                    _discoveredTilePanel = FindFirstObjectByType<DiscoveredTilePanelControl>(FindObjectsInactive.Include);
                if (_discoveredTilePanel != null)
                {
                    _discoveredTilePanel.TutorialGatherOverride = OnTutorialGatherClicked;
                    _waitingForGatherClick = true;

                    TileControl allowedTile = _trackedDiscoveryEnv != null
                        ? _trackedDiscoveryEnv.GetComponentInParent<TileControl>()
                        : null;
                    if (allowedTile != null)
                    {
                        if (_cameraControl != null)
                            _cameraControl.SetTutorialInputRestrictions(
                                restrictInput: true,
                                allowWorldDrag: true,
                                allowZoom: true,
                                allowMinimapRotation: true);
                        TileInteraction.SetTutorialAllowedTile(allowedTile);
                        TileInteraction.SetSelectionEnabled(true);
                    }
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.OpenCollectedGoodsPanel:
            {
                PlayerInventoryManager.TutorialBypassCapacity = true;
                if (_collectedGoodsPanel == null)
                    _collectedGoodsPanel = FindFirstObjectByType<CollectedGoodsPanelControl>(FindObjectsInactive.Include);
                if (_collectedGoodsPanel != null)
                {
                    if (_collectedGoodsPanel.IsShowing)
                    {
                        _collectedGoodsPanel.RefreshList();
                        ShowPart(_currentPart + 1);
                    }
                    else
                    {
                        _collectedGoodsPanel.OnOpen += OnCollectedGoodsPanelOpened;
                        _waitingForCollectedGoodsOpen = true;
                    }
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.CloseCollectedGoodsPanel:
            {
                if (_collectedGoodsPanel == null)
                    _collectedGoodsPanel = FindFirstObjectByType<CollectedGoodsPanelControl>(FindObjectsInactive.Include);
                if (_collectedGoodsPanel != null)
                {
                    _collectedGoodsPanel.OnClose += OnCollectedGoodsPanelClosed;
                    _waitingForCollectedGoodsClose = true;
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.ClickBuildButton:
            {
                if (_discoveredTilePanel == null)
                    _discoveredTilePanel = FindFirstObjectByType<DiscoveredTilePanelControl>(FindObjectsInactive.Include);
                if (_discoveredTilePanel != null)
                {
                    _discoveredTilePanel.TutorialBuildOverride = OnTutorialBuildClicked;
                    _waitingForBuildButtonClick = true;

                    TileControl allowedTile = _trackedDiscoveryEnv != null
                        ? _trackedDiscoveryEnv.GetComponentInParent<TileControl>()
                        : null;
                    if (allowedTile != null)
                    {
                        if (_cameraControl != null)
                            _cameraControl.SetTutorialInputRestrictions(
                                restrictInput: true,
                                allowWorldDrag: true,
                                allowZoom: true,
                                allowMinimapRotation: true);
                        TileInteraction.SetTutorialAllowedTile(allowedTile);
                        TileInteraction.SetSelectionEnabled(true);
                    }
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.SelectBuildingItem:
            {
                var catalog = _discoveredTilePanel != null ? _discoveredTilePanel.buildingCatalogPanel : null;
                if (catalog == null)
                    catalog = FindFirstObjectByType<BuildingCatalogPanelControl>(FindObjectsInactive.Include);

                var items = catalog != null ? catalog.SpawnedItems : null;
                if (items != null && items.Count > 0)
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        if (items[i] != null)
                            items[i].TutorialBuildOverride = OnTutorialBuildingItemSelected;
                    }
                    _waitingForBuildingItemSelect = true;
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

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

            case PartType.RegenerateMapDiscovered:
                if (_cameraControl != null)
                    _cameraControl.SetTutorialInputRestrictions(
                        restrictInput: true,
                        allowWorldDrag: false,
                        allowZoom: false,
                        allowMinimapRotation: false);
                TileInteraction.SetSelectionEnabled(false);
                if (_regenRoutine != null) StopCoroutine(_regenRoutine);
                _regenRoutine = StartCoroutine(TutorialRegenerateMapWithDiscoveredCoroutine());
                break;

            case PartType.OpenBuildingCostPanel:
            {
                _tutorialCatalogItem = GetTutorialCatalogItem();
                if (_tutorialCatalogItem != null)
                {
                    if (_tutorialCatalogItem.IsCostsPanelShowing)
                    {
                        ShowPart(_currentPart + 1);
                    }
                    else
                    {
                        _tutorialCatalogItem.OnCostsPanelShown += OnCostsPanelShown;
                        _waitingForCostPanelOpen = true;
                    }
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.CloseBuildingCostPanel:
            {
                if (_tutorialCatalogItem == null)
                    _tutorialCatalogItem = GetTutorialCatalogItem();
                if (_tutorialCatalogItem != null)
                {
                    if (!_tutorialCatalogItem.IsCostsPanelShowing)
                    {
                        ShowPart(_currentPart + 1);
                    }
                    else
                    {
                        _tutorialCatalogItem.OnCostsPanelHidden += OnCostsPanelHidden;
                        _waitingForCostPanelClose = true;
                    }
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.ClickCatalogBuildButton:
            {
                if (_tutorialCatalogItem == null)
                    _tutorialCatalogItem = GetTutorialCatalogItem();
                if (_tutorialCatalogItem != null)
                {
                    _tutorialCatalogItem.TutorialForceGreenCostsButton = true;
                    _tutorialCatalogItem.TutorialBuildOverride = OnTutorialCatalogBuildButtonClicked;
                    _waitingForCatalogBuild = true;
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }


            case PartType.ConfirmBuildingPlacement:
            {
                if (BuildingPlacementManager.Instance != null)
                {
                    BuildingPlacementManager.Instance.OnPlacementFinalized += OnBuildingPlacementFinalized;
                    _waitingForPlacementConfirm = true;
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.SelectPlacedBuilding:
            {
                TileControl buildingTile = _placedBuildingTile ?? FindTileControlNear(_placedBuildingWorldPos);
                if (buildingTile != null)
                {
                    if (_cameraControl != null)
                        _cameraControl.SetTutorialInputRestrictions(
                            restrictInput: true,
                            allowWorldDrag: true,
                            allowZoom: true,
                            allowMinimapRotation: true);
                    TileInteraction.SetTutorialAllowedTile(buildingTile);
                    TileInteraction.SetSelectionEnabled(true);
                    var ti = TileInteraction.GetInstance();
                    if (ti != null)
                    {
                        ti.OnTileSelected += OnPlacedBuildingTileSelected;
                        _waitingForBuildingTileSelect = true;
                    }
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.ShowCostSwitchButtons:
            {
                if (_tutorialCatalogItem == null)
                    _tutorialCatalogItem = GetTutorialCatalogItem();
                if (_tutorialCatalogItem != null)
                    _tutorialCatalogItem.ForceShowCostSwitchButtonsForTutorial();

                _waitingForCostSwitchNext = true;
                _activeNextButton = FindNextButton(tutorialParts[_currentPart]);
                if (_activeNextButton != null)
                {
                    _activeNextButton.gameObject.SetActive(true);
                    _activeNextButton.interactable = true;
                    _activeNextButton.onClick.AddListener(OnNextPressed);
                }
                break;
            }

            case PartType.OpenShelterPanel:
            {
                if (_shelterPanel == null)
                    _shelterPanel = FindFirstObjectByType<ShelterPanelControl>(FindObjectsInactive.Include);
                if (_shelterPanel != null)
                {
                    if (_shelterPanel.IsShowing)
                    {
                        ShowPart(_currentPart + 1);
                    }
                    else
                    {
                        _shelterPanel.OnOpen += OnShelterPanelOpened;
                        _waitingForShelterPanelOpen = true;
                    }
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.DamageBuilding:
            {
                BuildingControl building = FindBuildingNear(_placedBuildingWorldPos);
                if (building != null)
                {
                    if (_cameraControl != null)
                        _cameraControl.FocusOnPoint(building.transform.position, building.transform.forward, 6f);

                    if (_damageBuildingRoutine != null) StopCoroutine(_damageBuildingRoutine);
                    _damageBuildingRoutine = StartCoroutine(DamageBuildingCoroutine(building));
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.SelectDamagedBuilding:
            {
                TileControl buildingTile = _placedBuildingTile ?? FindTileControlNear(_placedBuildingWorldPos);
                if (_damagedPanel == null)
                    _damagedPanel = FindFirstObjectByType<BuildingDamagedPanelControl>(FindObjectsInactive.Include);

                if (buildingTile != null && _damagedPanel != null)
                {
                    if (_damagedPanel.IsShowing)
                    {
                        ShowPart(_currentPart + 1);
                    }
                    else
                    {
                        if (_cameraControl != null)
                            _cameraControl.SetTutorialInputRestrictions(
                                restrictInput: true,
                                allowWorldDrag: true,
                                allowZoom: true,
                                allowMinimapRotation: true);
                        TileInteraction.SetTutorialAllowedTile(buildingTile);
                        TileInteraction.SetSelectionEnabled(true);
                        _damagedPanel.OnShow += OnDamagedPanelOpened;
                        _waitingForDamagedPanelOpen = true;
                    }
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.CloseShelterPanel:
            {
                if (_shelterPanel == null)
                    _shelterPanel = FindFirstObjectByType<ShelterPanelControl>(FindObjectsInactive.Include);
                if (_shelterPanel != null)
                {
                    if (!_shelterPanel.IsShowing)
                    {
                        ShowPart(_currentPart + 1);
                    }
                    else
                    {
                        _shelterPanel.OnClose += OnShelterPanelClosed;
                        _waitingForShelterPanelClose = true;
                    }
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.CloseBuildingPanel:
            {
                if (_buildingPanel == null)
                    _buildingPanel = FindFirstObjectByType<BuildingPanelControl>(FindObjectsInactive.Include);
                if (_buildingPanel != null)
                {
                    if (!_buildingPanel.IsShowing)
                    {
                        ShowPart(_currentPart + 1);
                    }
                    else
                    {
                        _buildingPanel.OnClose += OnBuildingPanelClosed;
                        _waitingForBuildingPanelClose = true;
                    }
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.SelectTinyGrasslandOrSavanna:
            {
                if (_cameraControl != null)
                    _cameraControl.SetTutorialInputRestrictions(
                        restrictInput: true,
                        allowWorldDrag: true,
                        allowZoom: true,
                        allowMinimapRotation: false);

                _grassSavannaHighlights.Clear();
                var allEnvs = FindObjectsByType<EnvironmentControl>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                foreach (var env in allEnvs)
                {
                    if (env == null) continue;
                    if (env.tileSize != TileSize.Tiny) continue;
                    if (env.environmentType != EnvironmentType.Grassland && env.environmentType != EnvironmentType.Savanna) continue;
                    if (env.environmentTileType != EnvironmentTileType.Land) continue;
                    var tc = env.GetComponentInParent<TileControl>();
                    if (tc != null && !_grassSavannaHighlights.Contains(tc))
                        _grassSavannaHighlights.Add(tc);
                }

                foreach (var tc in _grassSavannaHighlights)
                    tc.SelectTile();

                TileInteraction.SetTutorialAllowedTiles(_grassSavannaHighlights);
                TileInteraction.SetSelectionEnabled(true);

                var ti = TileInteraction.GetInstance();
                if (ti != null)
                {
                    ti.OnTileSelected += OnGrasslandOrSavannaSelected;
                    _waitingForGrasslandOrSavannaSelect = true;
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }
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
            _placedShelterBuilding = Instantiate(prefab, worldPos, target.transform.rotation);

        // Remove environment tile the same way BuildingPlacementManager does
        TileControl tileControl = target.GetComponent<TileControl>();
        GameObject toDestroy = (tileControl != null && tileControl.transform.parent != null)
            ? tileControl.transform.parent.gameObject
            : target.gameObject;
        Destroy(toDestroy);

        _cameraControl?.FocusOnPoint(worldPos, envForward, 6f);
    }

    private void OnTurnCompletedForFastForward()
    {
        if (!_waitingForTurnComplete) return;
        _waitingForTurnComplete = false;
        TurnSystem.OnStartOfTurn -= OnTurnCompletedForFastForward;
        TurnSystem.Instance?.PauseTurnTimer();
        if (_fastForwardRoutine != null) StopCoroutine(_fastForwardRoutine);
        _fastForwardRoutine = StartCoroutine(FastForwardDiscoveryCoroutine());
    }

    private IEnumerator FastForwardDiscoveryCoroutine()
    {
        EnvironmentControl env = _trackedDiscoveryEnv;
        if (env == null) { ShowPart(_currentPart + 1); yield break; }

        // Force tutorial simulation mode so ApplyTutorialDiscoveryGhostTick won't early-return
        if (env.discoveryTurnsLeft > 0)
            env.BeginTutorialDiscoverySimulation(env.discoveryTurnsLeft);

        while (env.discoveryTurnsLeft > 0)
        {
            if (TurnSystem.Instance != null)
            {
                yield return TurnSystem.Instance.StartCoroutine(
                    TurnSystem.Instance.RunGhostPhaseAdvance(() => env.ApplyTutorialDiscoveryGhostTick())
                );
            }
            else
            {
                env.ApplyTutorialDiscoveryGhostTick();
                yield return null;
            }
        }

        env.CompleteTutorialDiscoveryNow();
        PlayerDiscoveryManager.Instance?.ForceReleaseReservation(env);
        EnvironmentControl.TutorialBypassTaskFailure = false;
        yield return null;
        ShowPart(_currentPart + 1);
    }

    private void OnTurnResumedOrSpeeded()
    {
        if (!_waitingForResumeOrSpeed) return;
        _waitingForResumeOrSpeed = false;
        if (TurnSystem.Instance != null)
        {
            TurnSystem.Instance.OnResumed -= OnTurnResumedOrSpeeded;
            TurnSystem.Instance.OnSpeedToggled -= OnTurnResumedOrSpeeded;
        }
        ShowPart(_currentPart + 1);
    }

    private void OnDiscoverButtonClicked()
    {
        if (!_waitingForDiscoverButton) return;
        _waitingForDiscoverButton = false;
        if (_undiscoveredPanel != null)
        {
            _trackedDiscoveryEnv = _undiscoveredPanel.CurrentEnvironment;
            _undiscoveredPanel.OnDiscoverPressed -= OnDiscoverButtonClicked;
        }
        ShowPart(_currentPart + 1);
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

    private void OnDiscoveryDetailsPanelClosed()
    {
        if (!_waitingForDiscoveryDetailsClose) return;
        _waitingForDiscoveryDetailsClose = false;
        if (_discoveryDetailsPanel != null)
            _discoveryDetailsPanel.OnClose -= OnDiscoveryDetailsPanelClosed;
        ShowPart(_currentPart + 1);
    }

    private void OnTriggerConsumptionNextPressed()
    {
        PlayerAggregatedPopulationSimulationManager.Instance?.ForceConsumptionCycle();
        ShowPart(_currentPart + 1);
    }

    private void OnConsumptionPanelDismissed()
    {
        if (!_waitingForConsumptionDismiss) return;
        _waitingForConsumptionDismiss = false;
        PopulationConsumptionPanel.OnDismissed -= OnConsumptionPanelDismissed;
        ShowPart(_currentPart + 1);
    }

    private void OnInventoryPanelOpenedSwitchToFood()
    {
        if (!_waitingForInventoryOpen) return;
        _waitingForInventoryOpen = false;
        if (_inventoryPanel != null)
        {
            _inventoryPanel.OnOpen -= OnInventoryPanelOpenedSwitchToFood;
            _inventoryPanel.ShowFoodTab();
        }
        ShowPart(_currentPart + 1);
    }

    private void OnInventoryPanelClosed()
    {
        if (!_waitingForInventoryClose) return;
        _waitingForInventoryClose = false;
        if (_inventoryPanel != null) _inventoryPanel.OnClose -= OnInventoryPanelClosed;
        ShowPart(_currentPart + 1);
    }

    private bool OnTutorialSurveyClicked(EnvironmentControl env)
    {
        if (PlayerSurveyManager.Instance != null)
        {
            PlayerSurveyManager.Instance.StartSurvey(env);
            PlayerSurveyManager.Instance.ForceCompleteSurvey(env);
        }
        return true;
    }

    private bool OnTutorialSurveyPanelOpenClicked(EnvironmentControl env)
    {
        if (_surveyPanel == null || env == null) return false;

        var node = env.GetComponent<EnvironmentResourceNode>();
        if (node == null || node.SpawnedResources == null) return false;

        var entries = new List<SurveyPanelControl.TutorialSurveyEntry>();
        for (int i = 0; i < node.SpawnedResources.Count; i++)
        {
            var e = node.SpawnedResources[i];
            if (e == null || e.definition == null || e.amount <= 0) continue;
            entries.Add(new SurveyPanelControl.TutorialSurveyEntry { definition = e.definition, amount = e.amount });
        }

        _surveyPanel.ShowTutorialEntries(entries);
        return true;
    }

    private bool OnTutorialGatherClicked(EnvironmentControl env)
    {
        if (env == null) return false;
        _waitingForGatherClick = false;
        if (_discoveredTilePanel != null &&
            _discoveredTilePanel.TutorialGatherOverride == (System.Func<EnvironmentControl, bool>)OnTutorialGatherClicked)
            _discoveredTilePanel.TutorialGatherOverride = null;

        EnvironmentControl.TutorialBypassTaskFailure = true;
        env.BeginGatheringVisuals();

        if (_discoveredTilePanel != null)
        {
            _discoveredTilePanel.SuppressSelectionReenableOnHide = true;
            _discoveredTilePanel.cameraControl?.PopInputLock();
        }

        TileInteraction.SetSelectionEnabled(false);

        if (_cameraControl != null)
            _cameraControl.FocusOnPoint(env.transform.position, env.transform.forward, 6f);

        if (_fastForwardGatherRoutine != null)
            StopCoroutine(_fastForwardGatherRoutine);
        _fastForwardGatherRoutine = StartCoroutine(FastForwardGatheringCoroutine(env));
        return true;
    }

    private IEnumerator FastForwardGatheringCoroutine(EnvironmentControl env)
    {
        if (env == null) { ShowPart(_currentPart + 1); yield break; }

        while (env.gatheringTurnsLeft > 0)
        {
            bool finalTick = env.gatheringTurnsLeft <= 1;

            if (TurnSystem.Instance != null)
            {
                yield return TurnSystem.Instance.StartCoroutine(
                    TurnSystem.Instance.RunGhostPhaseAdvance(() =>
                    {
                        if (finalTick)
                            env.StorePendingLoot(BuildGatherLoot(env));
                        env.AdvanceGatheringTurn();
                    })
                );
            }
            else
            {
                if (finalTick)
                    env.StorePendingLoot(BuildGatherLoot(env));
                env.AdvanceGatheringTurn();
                yield return null;
            }
        }

        _fastForwardGatherRoutine = null;
        PlayerGatheringManager.Instance?.ForceReleaseReservation(env);
        EnvironmentControl.TutorialBypassTaskFailure = false;
        TileInteraction.SetSelectionEnabled(false);
        TileInteraction.GetInstance()?.EnableSelectionAfter(0.01f);
        ShowPart(_currentPart + 1);
    }

    private IEnumerator TutorialRegenerateMapWithDiscoveredCoroutine()
    {
        // Unsubscribe so OnTilesActivated doesn't restart the tutorial from part 0
        if (_tileActivator != null)
            _tileActivator.OnTilesActivated -= OnWorldSpawned;

        // Close the discovered tile panel (it stays open after collected goods panel closes)
        if (_discoveredTilePanel == null)
            _discoveredTilePanel = FindFirstObjectByType<DiscoveredTilePanelControl>(FindObjectsInactive.Include);
        _discoveredTilePanel?.Hide();

        // Destroy the shelter building placed during ShelterPlacement
        if (_placedShelterBuilding != null)
        {
            Destroy(_placedShelterBuilding);
            _placedShelterBuilding = null;
        }

        if (_mapGenerator == null)
            _mapGenerator = FindFirstObjectByType<MapGenerator>();
        if (_mapTilePlacer == null)
            _mapTilePlacer = FindFirstObjectByType<MapTilePlacer>();

        if (_mapGenerator == null || _mapTilePlacer == null || _tileActivator == null)
        {
            _regenRoutine = null;
            ShowPart(_currentPart + 1);
            yield break;
        }

        _mapTilePlacer.ClearPlacedTilesAndState();
        yield return null; // let Destroy() calls process

        MapTilePlacer.ResetWorldReady();
        _mapGenerator.enabled = true;
        _mapTilePlacer.enabled = true;

        yield return StartCoroutine(_mapGenerator.RegenerateCoroutine());

        _mapTilePlacer.BeginPlacement();
        yield return new WaitUntil(() => MapTilePlacer.WorldReady);

        _tileActivator.BeginActivation(_tileActivator.timerUI, true, true);
        yield return new WaitUntil(() => !_tileActivator.IsRunning);

        // Mark every environment tile as already discovered
        var allEnvs = FindObjectsByType<EnvironmentControl>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < allEnvs.Length; i++)
        {
            if (allEnvs[i] != null)
                allEnvs[i].CompleteTutorialDiscoveryNow();
        }

        _regenRoutine = null;
        ShowPart(_currentPart + 1);
    }

    private void OnSurveyCompletedForTutorial(EnvironmentControl env)
    {
        if (!_waitingForSurveyComplete) return;
        _waitingForSurveyComplete = false;
        if (PlayerSurveyManager.Instance != null)
            PlayerSurveyManager.Instance.OnSurveyCompleted -= OnSurveyCompletedForTutorial;
        if (_discoveredTilePanel != null &&
            _discoveredTilePanel.TutorialSurveyOverride == (System.Func<EnvironmentControl, bool>)OnTutorialSurveyClicked)
            _discoveredTilePanel.TutorialSurveyOverride = null;
        ShowPart(_currentPart + 1);
    }

    private void OnSurveyPanelOpened()
    {
        if (!_waitingForSurveyPanelOpen) return;
        _waitingForSurveyPanelOpen = false;
        if (_surveyPanel != null) _surveyPanel.OnOpen -= OnSurveyPanelOpened;
        if (_discoveredTilePanel != null &&
            _discoveredTilePanel.TutorialSurveyOverride == (System.Func<EnvironmentControl, bool>)OnTutorialSurveyPanelOpenClicked)
            _discoveredTilePanel.TutorialSurveyOverride = null;
        ShowPart(_currentPart + 1);
    }

    private void OnSurveyPanelClosed()
    {
        if (!_waitingForSurveyPanelClose) return;
        _waitingForSurveyPanelClose = false;
        if (_surveyPanel != null) _surveyPanel.OnClose -= OnSurveyPanelClosed;
        ShowPart(_currentPart + 1);
    }

    private void OnShelterPanelOpened()
    {
        if (!_waitingForShelterPanelOpen) return;
        _waitingForShelterPanelOpen = false;
        if (_shelterPanel != null) _shelterPanel.OnOpen -= OnShelterPanelOpened;
        ShowPart(_currentPart + 1);
    }

    private void OnShelterPanelClosed()
    {
        if (!_waitingForShelterPanelClose) return;
        _waitingForShelterPanelClose = false;
        if (_shelterPanel != null) _shelterPanel.OnClose -= OnShelterPanelClosed;
        ShowPart(_currentPart + 1);
    }

    private void OnBuildingPanelClosed()
    {
        if (!_waitingForBuildingPanelClose) return;
        _waitingForBuildingPanelClose = false;
        if (_buildingPanel != null) _buildingPanel.OnClose -= OnBuildingPanelClosed;
        ShowPart(_currentPart + 1);
    }

    private static List<(ResourceDefinition, int)> BuildGatherLoot(EnvironmentControl env)
    {
        var loot = new List<(ResourceDefinition, int)>();
        var node = env.GetComponent<EnvironmentResourceNode>();
        if (node == null || node.SpawnedResources == null) return loot;
        for (int i = 0; i < node.SpawnedResources.Count; i++)
        {
            var s = node.SpawnedResources[i];
            if (s != null && s.definition != null && s.amount > 0)
                loot.Add((s.definition, s.amount));
        }
        return loot;
    }

    private void OnCollectedGoodsPanelOpened()
    {
        if (!_waitingForCollectedGoodsOpen) return;
        _waitingForCollectedGoodsOpen = false;
        if (_collectedGoodsPanel != null) _collectedGoodsPanel.OnOpen -= OnCollectedGoodsPanelOpened;
        PlayerInventoryManager.TutorialBypassCapacity = true;
        ShowPart(_currentPart + 1);
    }

    private void OnCollectedGoodsPanelClosed()
    {
        if (!_waitingForCollectedGoodsClose) return;
        _waitingForCollectedGoodsClose = false;
        PlayerInventoryManager.TutorialBypassCapacity = false;
        if (_collectedGoodsPanel != null) _collectedGoodsPanel.OnClose -= OnCollectedGoodsPanelClosed;
        ShowPart(_currentPart + 1);
    }

    private bool OnTutorialBuildClicked(EnvironmentControl env)
    {
        if (env == null) return false;
        _waitingForBuildButtonClick = false;
        _discoveredTilePanel.TutorialBuildOverride = null;
        TileInteraction.ClearTutorialAllowedTile();

        var catalog = _discoveredTilePanel.buildingCatalogPanel;
        if (catalog != null)
        {
            string targetID = _selectedGrassOrSavannaType == EnvironmentType.Savanna
                ? tutorialSavannaBuildingID
                : tutorialGrasslandBuildingID;

            if (!string.IsNullOrEmpty(targetID))
                catalog.ShowForTutorial(env, _discoveredTilePanel, targetID);
            else
                catalog.ShowFor(env, _discoveredTilePanel);
        }

        ShowPart(_currentPart + 1);
        return true;
    }

    private bool OnTutorialBuildingItemSelected(BuildingCatalogItem item)
    {
        if (!_waitingForBuildingItemSelect) return false;
        _waitingForBuildingItemSelect = false;

        var catalog = _discoveredTilePanel != null ? _discoveredTilePanel.buildingCatalogPanel : null;
        if (catalog != null)
        {
            var items = catalog.SpawnedItems;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null &&
                    items[i].TutorialBuildOverride == (System.Func<BuildingCatalogItem, bool>)OnTutorialBuildingItemSelected)
                    items[i].TutorialBuildOverride = null;
            }
        }

        ShowPart(_currentPart + 1);
        return true;
    }

    private void OnDiscoveredTileSelected(TileControl tile)
    {
        if (!_waitingForDiscoveredTileSelect) return;
        _waitingForDiscoveredTileSelect = false;
        TileInteraction.GetInstance().OnTileSelected -= OnDiscoveredTileSelected;
        TileInteraction.ClearTutorialAllowedTile();
        ShowPart(_currentPart + 1);
    }

    private void OnGrasslandOrSavannaSelected(TileControl tile)
    {
        if (!_waitingForGrasslandOrSavannaSelect) return;
        if (!_grassSavannaHighlights.Contains(tile)) return;

        _waitingForGrasslandOrSavannaSelect = false;
        var ti = TileInteraction.GetInstance();
        if (ti != null) ti.OnTileSelected -= OnGrasslandOrSavannaSelected;

        foreach (var tc in _grassSavannaHighlights)
            if (tc != null && tc != tile) tc.DeselectTile();
        _grassSavannaHighlights.Clear();

        TileInteraction.ClearTutorialAllowedTiles();

        // Store the selected env so ClickBuildButton can focus on this tile and
        // so OnTutorialBuildClicked knows which building variant to show.
        var env = tile.GetComponentInChildren<EnvironmentControl>(true);
        if (env != null)
        {
            _trackedDiscoveryEnv = env;
            _selectedGrassOrSavannaType = env.environmentType;
        }

        ShowPart(_currentPart + 1);
    }

    private BuildingCatalogItem GetTutorialCatalogItem()
    {
        var catalog = _discoveredTilePanel != null ? _discoveredTilePanel.buildingCatalogPanel : null;
        if (catalog == null)
            catalog = FindFirstObjectByType<BuildingCatalogPanelControl>(FindObjectsInactive.Include);
        return catalog != null && catalog.SpawnedItems.Count > 0 ? catalog.SpawnedItems[0] : null;
    }

    private void OnCostsPanelShown()
    {
        if (!_waitingForCostPanelOpen) return;
        _waitingForCostPanelOpen = false;
        if (_tutorialCatalogItem != null) _tutorialCatalogItem.OnCostsPanelShown -= OnCostsPanelShown;
        ShowPart(_currentPart + 1);
    }

    private void OnCostsPanelHidden()
    {
        if (!_waitingForCostPanelClose) return;
        _waitingForCostPanelClose = false;
        if (_tutorialCatalogItem != null) _tutorialCatalogItem.OnCostsPanelHidden -= OnCostsPanelHidden;
        ShowPart(_currentPart + 1);
    }

    private void OnBuildingPlacementFinalized(BuildingConstruction bc)
    {
        if (!_waitingForPlacementConfirm) return;
        _waitingForPlacementConfirm = false;
        BuildingPlacementManager.TutorialBypassCosts = false;
        if (bc != null)
            _placedBuildingWorldPos = bc.transform.position;
        if (BuildingPlacementManager.Instance != null)
            BuildingPlacementManager.Instance.OnPlacementFinalized -= OnBuildingPlacementFinalized;

        if (bc != null && PlayerConstructionManager.Instance != null)
        {
            if (_constructionGhostRoutine != null) StopCoroutine(_constructionGhostRoutine);
            _constructionGhostRoutine = StartCoroutine(TutorialConstructionGhostCoroutine(bc));
        }
        else
        {
            ShowPart(_currentPart + 1);
        }
    }

    private IEnumerator TutorialConstructionGhostCoroutine(BuildingConstruction bc)
    {
        yield return StartCoroutine(PlayerConstructionManager.Instance.TutorialGhostCompleteConstruction(bc));
        _constructionGhostRoutine = null;

        // Cache the building's TileControl now that the final GO exists
        BuildingControl placed = FindBuildingNear(_placedBuildingWorldPos);
        if (placed != null)
            _placedBuildingTile = placed.GetComponentInParent<TileControl>();

        ShowPart(_currentPart + 1);
    }

    private void OnPlacedBuildingTileSelected(TileControl tile)
    {
        if (!_waitingForBuildingTileSelect) return;
        _waitingForBuildingTileSelect = false;
        var ti = TileInteraction.GetInstance();
        if (ti != null) ti.OnTileSelected -= OnPlacedBuildingTileSelected;
        TileInteraction.ClearTutorialAllowedTile();
        ShowPart(_currentPart + 1);
    }

    private static TileControl FindTileControlNear(Vector3 worldPos)
    {
        var all = FindObjectsByType<TileControl>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        TileControl best = null;
        float bestDist = float.PositiveInfinity;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            float d = Vector3.SqrMagnitude(all[i].transform.position - worldPos);
            if (d < bestDist)
            {
                bestDist = d;
                best = all[i];
            }
        }
        return best;
    }

    private static BuildingControl FindBuildingNear(Vector3 worldPos)
    {
        var all = FindObjectsByType<BuildingControl>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        BuildingControl best = null;
        float bestDist = float.PositiveInfinity;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            float d = Vector3.SqrMagnitude(all[i].transform.position - worldPos);
            if (d < bestDist)
            {
                bestDist = d;
                best = all[i];
            }
        }
        return best;
    }

    private IEnumerator DamageBuildingCoroutine(BuildingControl building)
    {
        var health = building.GetComponent<BuildingHealth>();
        var status = building.GetComponent<BuildingStatus>();

        if (health == null || status == null)
        {
            _damageBuildingRoutine = null;
            ShowPart(_currentPart + 1);
            yield break;
        }

        yield return new WaitForSeconds(0.5f);

        int target = Mathf.FloorToInt(health.maxHealth * health.damagedThreshold);

        while (health.CurrentHealth > target && status.CurrentState == BuildingState.Normal)
        {
            int chunk = Mathf.Max(1, Mathf.CeilToInt((health.CurrentHealth - target) / 5f));
            health.ApplyDamage(chunk);
            yield return new WaitForSeconds(0.12f);
        }

        _damageBuildingRoutine = null;
        ShowPart(_currentPart + 1);
    }

    private void OnDamagedPanelOpened()
    {
        if (!_waitingForDamagedPanelOpen) return;
        _waitingForDamagedPanelOpen = false;
        if (_damagedPanel != null) _damagedPanel.OnShow -= OnDamagedPanelOpened;
        TileInteraction.ClearTutorialAllowedTile();
        ShowPart(_currentPart + 1);
    }

    private bool OnTutorialCatalogBuildButtonClicked(BuildingCatalogItem item)
    {
        if (!_waitingForCatalogBuild) return false;
        _waitingForCatalogBuild = false;

        if (item != null)
        {
            item.TutorialBuildOverride = null;
            item.TutorialForceGreenCostsButton = false;
        }
        _tutorialCatalogItem = null;

        // Bypass cost/pop checks and enter placement directly
        if (item != null && item.Definition != null && item.TargetEnvironment != null)
        {
            BuildingPlacementPanelControl.TutorialDisableCancelButton = true;
            BuildingPlacementManager.TutorialBypassCosts = true;
            BuildingPlacementManager.Instance?.BeginPlacement(item.Definition, item.TargetEnvironment);
        }

        // Panels are hidden by BuildingPlacementManager on placement start,
        // but hide defensively in case it doesn't
        var catalog = _discoveredTilePanel != null ? _discoveredTilePanel.buildingCatalogPanel : null;
        catalog?.Hide();
        _discoveredTilePanel?.Hide();

        ShowPart(_currentPart + 1);
        return true;
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

        if (_waitingForTurnComplete)
        {
            TurnSystem.OnStartOfTurn -= OnTurnCompletedForFastForward;
            _waitingForTurnComplete = false;
        }

        EnvironmentControl.TutorialBypassTaskFailure = false;

        if (_fastForwardRoutine != null)
        {
            StopCoroutine(_fastForwardRoutine);
            _fastForwardRoutine = null;
        }

        if (_waitingForResumeOrSpeed && TurnSystem.Instance != null)
        {
            TurnSystem.Instance.OnResumed -= OnTurnResumedOrSpeeded;
            TurnSystem.Instance.OnSpeedToggled -= OnTurnResumedOrSpeeded;
            _waitingForResumeOrSpeed = false;
        }

        if (_waitingForDiscoverButton && _undiscoveredPanel != null)
        {
            _undiscoveredPanel.OnDiscoverPressed -= OnDiscoverButtonClicked;
            _waitingForDiscoverButton = false;
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

        if (_waitingForDiscoveryDetailsClose && _discoveryDetailsPanel != null)
        {
            _discoveryDetailsPanel.OnClose -= OnDiscoveryDetailsPanelClosed;
            _waitingForDiscoveryDetailsClose = false;
        }

        if (_waitingForConsumptionDismiss)
        {
            PopulationConsumptionPanel.OnDismissed -= OnConsumptionPanelDismissed;
            _waitingForConsumptionDismiss = false;
        }

        if (_waitingForInventoryOpen && _inventoryPanel != null)
        {
            _inventoryPanel.OnOpen -= OnInventoryPanelOpenedSwitchToFood;
            _waitingForInventoryOpen = false;
        }

        if (_waitingForInventoryClose && _inventoryPanel != null)
        {
            _inventoryPanel.OnClose -= OnInventoryPanelClosed;
            _waitingForInventoryClose = false;
        }

        if (_waitingForSpoiledFoodRemoval)
        {
            _waitingForSpoiledFoodRemoval = false;
            _spoiledFoodTargetId = null;
        }

        if (_waitingForDiscoveredTileSelect)
        {
            _waitingForDiscoveredTileSelect = false;
            var ti = TileInteraction.GetInstance();
            if (ti != null) ti.OnTileSelected -= OnDiscoveredTileSelected;
            TileInteraction.ClearTutorialAllowedTile();
        }

        if (_waitingForGrasslandOrSavannaSelect)
        {
            _waitingForGrasslandOrSavannaSelect = false;
            var ti = TileInteraction.GetInstance();
            if (ti != null) ti.OnTileSelected -= OnGrasslandOrSavannaSelected;
            foreach (var tc in _grassSavannaHighlights)
                if (tc != null) tc.DeselectTile();
            _grassSavannaHighlights.Clear();
            TileInteraction.ClearTutorialAllowedTiles();
        }

        if (_waitingForSurveyComplete)
        {
            _waitingForSurveyComplete = false;
            if (PlayerSurveyManager.Instance != null)
                PlayerSurveyManager.Instance.OnSurveyCompleted -= OnSurveyCompletedForTutorial;
            if (_discoveredTilePanel != null &&
                _discoveredTilePanel.TutorialSurveyOverride == (System.Func<EnvironmentControl, bool>)OnTutorialSurveyClicked)
                _discoveredTilePanel.TutorialSurveyOverride = null;
        }

        if (_waitingForSurveyPanelOpen && _surveyPanel != null)
        {
            _surveyPanel.OnOpen -= OnSurveyPanelOpened;
            _waitingForSurveyPanelOpen = false;
            if (_discoveredTilePanel != null &&
                _discoveredTilePanel.TutorialSurveyOverride == (System.Func<EnvironmentControl, bool>)OnTutorialSurveyPanelOpenClicked)
                _discoveredTilePanel.TutorialSurveyOverride = null;
        }

        if (_waitingForSurveyPanelClose && _surveyPanel != null)
        {
            _surveyPanel.OnClose -= OnSurveyPanelClosed;
            _waitingForSurveyPanelClose = false;
        }

        if (_waitingForGatherClick && _discoveredTilePanel != null)
        {
            if (_discoveredTilePanel.TutorialGatherOverride == (System.Func<EnvironmentControl, bool>)OnTutorialGatherClicked)
                _discoveredTilePanel.TutorialGatherOverride = null;
            _waitingForGatherClick = false;
            TileInteraction.ClearTutorialAllowedTile();
        }

        if (_fastForwardGatherRoutine != null)
        {
            StopCoroutine(_fastForwardGatherRoutine);
            _fastForwardGatherRoutine = null;
        }

        if (_regenRoutine != null)
        {
            StopCoroutine(_regenRoutine);
            _regenRoutine = null;
        }

        if (_waitingForCollectedGoodsOpen && _collectedGoodsPanel != null)
        {
            _collectedGoodsPanel.OnOpen -= OnCollectedGoodsPanelOpened;
            _waitingForCollectedGoodsOpen = false;
        }

        if (_waitingForCollectedGoodsClose && _collectedGoodsPanel != null)
        {
            _collectedGoodsPanel.OnClose -= OnCollectedGoodsPanelClosed;
            _waitingForCollectedGoodsClose = false;
        }

        PlayerInventoryManager.TutorialBypassCapacity = false;

        if (_waitingForBuildButtonClick && _discoveredTilePanel != null)
        {
            if (_discoveredTilePanel.TutorialBuildOverride == (System.Func<EnvironmentControl, bool>)OnTutorialBuildClicked)
                _discoveredTilePanel.TutorialBuildOverride = null;
            _waitingForBuildButtonClick = false;
            TileInteraction.ClearTutorialAllowedTile();
        }

        if (_waitingForBuildingItemSelect)
        {
            _waitingForBuildingItemSelect = false;
            var catalog = _discoveredTilePanel != null ? _discoveredTilePanel.buildingCatalogPanel : null;
            if (catalog != null)
            {
                var items = catalog.SpawnedItems;
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i] != null &&
                        items[i].TutorialBuildOverride == (System.Func<BuildingCatalogItem, bool>)OnTutorialBuildingItemSelected)
                        items[i].TutorialBuildOverride = null;
                }
            }
        }

        if (_waitingForCostPanelOpen && _tutorialCatalogItem != null)
        {
            _tutorialCatalogItem.OnCostsPanelShown -= OnCostsPanelShown;
            _waitingForCostPanelOpen = false;
        }

        if (_waitingForCostPanelClose && _tutorialCatalogItem != null)
        {
            _tutorialCatalogItem.OnCostsPanelHidden -= OnCostsPanelHidden;
            _waitingForCostPanelClose = false;
        }

        if (_waitingForCatalogBuild && _tutorialCatalogItem != null)
        {
            _tutorialCatalogItem.TutorialBuildOverride = null;
            _tutorialCatalogItem.TutorialForceGreenCostsButton = false;
            _waitingForCatalogBuild = false;
        }

        if (_waitingForCostSwitchNext)
        {
            _waitingForCostSwitchNext = false;
            if (_tutorialCatalogItem != null)
                _tutorialCatalogItem.RestoreCostSwitchButtonVisibility();
        }

        _tutorialCatalogItem = null;

        if (_waitingForPlacementConfirm)
        {
            _waitingForPlacementConfirm = false;
            BuildingPlacementManager.TutorialBypassCosts = false;
            if (BuildingPlacementManager.Instance != null)
                BuildingPlacementManager.Instance.OnPlacementFinalized -= OnBuildingPlacementFinalized;
        }

        if (_constructionGhostRoutine != null)
        {
            StopCoroutine(_constructionGhostRoutine);
            _constructionGhostRoutine = null;
        }

        if (_waitingForBuildingTileSelect)
        {
            _waitingForBuildingTileSelect = false;
            var ti = TileInteraction.GetInstance();
            if (ti != null) ti.OnTileSelected -= OnPlacedBuildingTileSelected;
            TileInteraction.ClearTutorialAllowedTile();
        }

        if (_damageBuildingRoutine != null)
        {
            StopCoroutine(_damageBuildingRoutine);
            _damageBuildingRoutine = null;
        }

        if (_waitingForDamagedPanelOpen && _damagedPanel != null)
        {
            _damagedPanel.OnShow -= OnDamagedPanelOpened;
            _waitingForDamagedPanelOpen = false;
            TileInteraction.ClearTutorialAllowedTile();
        }

        if (_waitingForShelterPanelOpen && _shelterPanel != null)
        {
            _shelterPanel.OnOpen -= OnShelterPanelOpened;
            _waitingForShelterPanelOpen = false;
        }

        if (_waitingForShelterPanelClose && _shelterPanel != null)
        {
            _shelterPanel.OnClose -= OnShelterPanelClosed;
            _waitingForShelterPanelClose = false;
        }

        if (_waitingForBuildingPanelClose && _buildingPanel != null)
        {
            _buildingPanel.OnClose -= OnBuildingPanelClosed;
            _waitingForBuildingPanelClose = false;
        }

        BuildingPlacementPanelControl.TutorialDisableCancelButton = false;
    }

    private void UnbindActiveNextButton()
    {
        if (_activeNextButton != null)
        {
            _activeNextButton.onClick.RemoveListener(OnNextPressed);
            _activeNextButton.onClick.RemoveListener(OnTriggerConsumptionNextPressed);
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
