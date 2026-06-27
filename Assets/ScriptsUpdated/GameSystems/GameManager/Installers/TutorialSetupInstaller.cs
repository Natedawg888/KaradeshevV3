using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TutorialSetupInstaller : MonoBehaviour
{
    public enum PartType { Static, CameraDrag, CameraZoom, MinimapRotate, ShelterPlacement, HighlightAdjacent, OpenUndiscoveredTile, OpenDiscoveryDetails, CloseDiscoveryDetails, ClickDiscoverButton, ResumeOrSpeedUp, FastForwardDiscovery, TriggerConsumption, WaitForConsumptionDismiss, OpenInventoryPanel, CloseInventoryPanel, RemoveSpoiledFood, SelectDiscoveredTile, ClickSurveyButton, OpenSurveyPanel, CloseSurveyPanel, ClickGatherButton, OpenCollectedGoodsPanel, CloseCollectedGoodsPanel, ClickBuildButton, SelectBuildingItem, RegenerateMapDiscovered, SelectTinyGrasslandOrSavanna, OpenBuildingCostPanel, CloseBuildingCostPanel, ClickCatalogBuildButton, ShowCostSwitchButtons, ConfirmBuildingPlacement, SelectPlacedBuilding, OpenShelterPanel, CloseShelterPanel, CloseBuildingPanel, DamageBuilding, SelectDamagedBuilding, OpenRepairPanel, ClickFullRepairButton, ClickRepairButton, CloseRepairAndDamagedPanels, FastForwardRepair, OpenResearchPanel, OpenResearchNeedsPanel, CloseResearchNeedsPanel, CloseResearchPanel, OpenLevelInfoPanel, CloseLevelInfoPanel, SecondBuildingPlacement, SelectSecondBuilding, OpenStoragePanel, CloseStoragePanel, ThirdBuildingPlacement, SelectThirdBuilding, ClickSwitchBuildingType, OpenCraftingPanel, OpenCraftingCostPanel, ClickCraftingOutputView, CloseCraftingPanel, FourthBuildingPlacement, SelectFourthBuilding, OpenProductionPanel, StartProductionPlan, SelectProductionTargets, OpenProductionRunningPanel, CloseProductionRunningPanel, FifthBuildingPlacement, SelectFifthBuilding, OpenTradePanel, SelectTraderEntry, OpenTraderOffering, OfferResources, FinishTrade, CloseTraderPanel, CloseTradePanel, SixthBuildingPlacement, SelectSixthBuilding, OpenReligiousPanel, OpenRitualPanel, StartInitialSummoningRitual, FastForwardRitual, SelectSummoningSpirit, RegenerateMapClearBuildings, SeventhBuildingPlacement, SelectSeventhBuilding }

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

    [Header("Second Building Placement Part")]
    [SerializeField] private string secondBuildingID = "";

    [Header("Third Building Placement Part")]
    [SerializeField] private string thirdBuildingID = "";
    [SerializeField] private string thirdBuildingAlternateID = "";

    [Header("Fourth Building Placement Part")]
    [SerializeField] private string fourthBuildingID = "";
    [SerializeField] private string fourthBuildingAlternateID = "";

    [Header("Fifth Building Placement Part")]
    [SerializeField] private string fifthBuildingID = "";
    [SerializeField] private string fifthBuildingAlternateID = "";

    [Header("Sixth Building Placement Part")]
    [SerializeField] private string sixthBuildingID = "";
    [SerializeField] private string sixthBuildingAlternateID = "";

    [Header("Seventh Building Placement Part")]
    [SerializeField] private string seventhBuildingID = "";
    [SerializeField] private string seventhBuildingAlternateID = "";

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
    private GameObject _placedSecondBuilding;
    private Vector3 _placedSecondBuildingWorldPos;
    private GameObject _placedThirdBuilding;
    private Vector3 _placedThirdBuildingWorldPos;
    private bool _waitingForThirdBuildingPanel;
    private GameObject _placedFourthBuilding;
    private Vector3 _placedFourthBuildingWorldPos;
    private bool _waitingForFourthBuildingPanel;
    private GameObject _placedFifthBuilding;
    private Vector3 _placedFifthBuildingWorldPos;
    private bool _waitingForFifthBuildingPanel;
    private GameObject _placedSixthBuilding;
    private Vector3 _placedSixthBuildingWorldPos;
    private bool _waitingForSixthBuildingPanel;
    private GameObject _placedSeventhBuilding;
    private Vector3 _placedSeventhBuildingWorldPos;
    private bool _waitingForSeventhBuildingPanel;
    private ReligiousBuildingPanelControl _religiousPanel;
    private bool _waitingForReligiousPanelOpen;
    private ReligiousRitualPanelControl _ritualPanel;
    private bool _waitingForRitualPanelOpen;
    private ReligiousBuildingControl _ritualBuildingControl;
    private bool _waitingForRitualStart;
    private Coroutine _fastForwardRitualRoutine;
    private SummoningSpiritOfferPanelControl _summoningOfferPanel;
    private bool _waitingForSummoningPanelOpen;
    private bool _waitingForSpiritChosen;
    private TradePanelControl _tradePanel;
    private bool _waitingForTradePanelOpen;
    private TraderPanelControl _traderPanel;
    private bool _waitingForTraderPanelOpen;
    private OfferingPanelControl _offeringPanel;
    private bool _waitingForOfferingPanelOpen;
    private bool _waitingForPlayerOffer;
    private bool _waitingForTradeAccepted;
    private bool _waitingForTraderPanelClose;
    private bool _waitingForTradePanelClose;
    private ProductionBuildingPanelControl _productionPanel;
    private bool _waitingForProductionPanelOpen;
    private ProductionRunningPanelControl _productionRunningPanel;
    private bool _waitingForProductionRunningPanelOpen;
    private bool _waitingForProductionRunningPanelClose;
    private ProductionPlanItem _tutorialProductionItem;
    private bool _waitingForProductionStart;
    private bool _waitingForProductionTargets;
    private bool _waitingForBuildingTypeSwitch;
    private CraftingBuildingPanelControl _craftingPanel;
    private bool _waitingForCraftingPanelOpen;
    private bool _waitingForCraftingPanelClose;
    private CraftingRecipeItem _tutorialCraftingItem;
    private bool _waitingForCraftingCostPanel;
    private bool _waitingForCraftingOutputView;
    private bool _waitingForSecondBuildingPanel;
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
    private StoragePanelControl _storagePanel;
    private bool _waitingForStoragePanelOpen;
    private bool _waitingForStoragePanelClose;

    private Coroutine _damageBuildingRoutine;
    private bool _waitingForDamagedPanelOpen;
    private BuildingDamagedPanelControl _damagedPanel;
    private TileControl _placedBuildingTile;

    private bool _waitingForRepairPanelOpen;
    private RepairPanelControl _repairPanel;

    private bool _waitingForFullRepairTier;
    private bool _waitingForRepairStart;
    private BuildingRepair _placedBuildingRepair;
    private bool _waitingForRepairAndDamagedClose;
    private Coroutine _fastForwardRepairRoutine;

    private bool _waitingForResearchPanelOpen;
    private ResearchPanelControl _researchPanel;

    private bool _waitingForResearchNeedsPanelOpen;
    private bool _waitingForResearchNeedsPanelClose;
    private bool _waitingForResearchPanelClose;
    private TechnologyItem _trackedTechItem;

    private bool _waitingForLevelInfoPanelOpen;
    private bool _waitingForLevelInfoPanelClose;
    private TechPanelControl _techPanel;

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


        if (_waitingForStoragePanelOpen)
        {
            if (_storagePanel != null && _storagePanel.IsShowing)
            {
                _waitingForStoragePanelOpen = false;
                ShowPart(_currentPart + 1);
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

            case PartType.OpenRepairPanel:
            {
                if (_repairPanel == null)
                    _repairPanel = FindFirstObjectByType<RepairPanelControl>(FindObjectsInactive.Include);
                if (_repairPanel != null)
                {
                    if (_repairPanel.IsShowing)
                    {
                        ShowPart(_currentPart + 1);
                    }
                    else
                    {
                        _repairPanel.OnOpen += OnRepairPanelOpened;
                        _waitingForRepairPanelOpen = true;
                    }
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.ClickFullRepairButton:
            {
                if (_repairPanel == null)
                    _repairPanel = FindFirstObjectByType<RepairPanelControl>(FindObjectsInactive.Include);
                if (_repairPanel != null)
                {
                    BuildingRepair.TutorialBypassCosts = true;
                    _repairPanel.OnFullTierClicked += OnFullRepairTierClicked;
                    _waitingForFullRepairTier = true;
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.ClickRepairButton:
            {
                BuildingControl building = FindBuildingNear(_placedBuildingWorldPos);
                _placedBuildingRepair = building != null ? building.GetComponent<BuildingRepair>() : null;
                if (_placedBuildingRepair != null)
                {
                    _placedBuildingRepair.OnRepairStarted += OnRepairStarted;
                    _waitingForRepairStart = true;
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.CloseRepairAndDamagedPanels:
            {
                if (_damagedPanel == null)
                    _damagedPanel = FindFirstObjectByType<BuildingDamagedPanelControl>(FindObjectsInactive.Include);
                if (_damagedPanel != null)
                {
                    if (!_damagedPanel.IsShowing)
                    {
                        ShowPart(_currentPart + 1);
                    }
                    else
                    {
                        _damagedPanel.OnClose += OnRepairAndDamagedPanelsClosed;
                        _waitingForRepairAndDamagedClose = true;
                    }
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.FastForwardRepair:
            {
                if (_placedBuildingRepair == null)
                {
                    BuildingControl building = FindBuildingNear(_placedBuildingWorldPos);
                    _placedBuildingRepair = building != null ? building.GetComponent<BuildingRepair>() : null;
                }
                if (_placedBuildingRepair != null && _placedBuildingRepair.IsRepairing)
                {
                    if (_fastForwardRepairRoutine != null) StopCoroutine(_fastForwardRepairRoutine);
                    _fastForwardRepairRoutine = StartCoroutine(FastForwardRepairCoroutine(_placedBuildingRepair));
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.OpenResearchPanel:
            {
                if (_researchPanel == null)
                    _researchPanel = FindFirstObjectByType<ResearchPanelControl>(FindObjectsInactive.Include);
                if (_researchPanel != null)
                {
                    if (_researchPanel.IsShowing)
                    {
                        ShowPart(_currentPart + 1);
                    }
                    else
                    {
                        ResearchPanelControl.TutorialShowAllTech = true;

                        // Allow the player to select the placed building so they can reach the panel
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
                        }

                        _researchPanel.OnOpen += OnResearchPanelOpened;
                        _waitingForResearchPanelOpen = true;
                    }
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.OpenResearchNeedsPanel:
            {
                // Find the first TechnologyItem spawned in the open research panel
                if (_researchPanel == null)
                    _researchPanel = FindFirstObjectByType<ResearchPanelControl>(FindObjectsInactive.Include);

                _trackedTechItem = null;
                if (_researchPanel != null && _researchPanel.contentRoot != null)
                    _trackedTechItem = _researchPanel.contentRoot.GetComponentInChildren<TechnologyItem>(true);

                if (_trackedTechItem != null)
                {
                    if (_trackedTechItem.NeedsPanelActive)
                    {
                        ShowPart(_currentPart + 1);
                    }
                    else
                    {
                        _trackedTechItem.OnNeedsPanelShown += OnResearchNeedsPanelShown;
                        _waitingForResearchNeedsPanelOpen = true;
                    }
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.OpenLevelInfoPanel:
            {
                if (_techPanel == null)
                    _techPanel = FindFirstObjectByType<TechPanelControl>(FindObjectsInactive.Include);
                if (_techPanel != null)
                {
                    TechPanelControl.TutorialShowAll = true;
                    if (_techPanel.IsShowing)
                    {
                        ShowPart(_currentPart + 1);
                    }
                    else
                    {
                        _techPanel.OnOpen += OnLevelInfoPanelOpened;
                        _waitingForLevelInfoPanelOpen = true;
                    }
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.CloseLevelInfoPanel:
            {
                if (_techPanel == null)
                    _techPanel = FindFirstObjectByType<TechPanelControl>(FindObjectsInactive.Include);
                if (_techPanel != null)
                {
                    if (!_techPanel.IsShowing)
                    {
                        ShowPart(_currentPart + 1);
                    }
                    else
                    {
                        _techPanel.OnClose += OnLevelInfoPanelClosed;
                        _waitingForLevelInfoPanelClose = true;
                    }
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.SecondBuildingPlacement:
                if (_cameraControl != null)
                    _cameraControl.SetTutorialInputRestrictions(
                        restrictInput: true,
                        allowWorldDrag: false,
                        allowZoom: false,
                        allowMinimapRotation: false);
                PlaceSecondBuildingOnMap();
                _activeNextButton = FindNextButton(tutorialParts[_currentPart]);
                if (_activeNextButton != null)
                {
                    _activeNextButton.gameObject.SetActive(true);
                    _activeNextButton.interactable = true;
                    _activeNextButton.onClick.AddListener(OnNextPressed);
                }
                break;

            case PartType.ThirdBuildingPlacement:
                if (_cameraControl != null)
                    _cameraControl.SetTutorialInputRestrictions(
                        restrictInput: true,
                        allowWorldDrag: false,
                        allowZoom: false,
                        allowMinimapRotation: false);
                PlaceThirdBuildingOnMap();
                _activeNextButton = FindNextButton(tutorialParts[_currentPart]);
                if (_activeNextButton != null)
                {
                    _activeNextButton.gameObject.SetActive(true);
                    _activeNextButton.interactable = true;
                    _activeNextButton.onClick.AddListener(OnNextPressed);
                }
                break;

            case PartType.SelectSecondBuilding:
            {
                TileControl buildingTile = _placedSecondBuilding != null
                    ? _placedSecondBuilding.GetComponentInParent<TileControl>()
                    : null;
                buildingTile ??= FindTileControlNear(_placedSecondBuildingWorldPos);

                if (_buildingPanel == null)
                    _buildingPanel = FindFirstObjectByType<BuildingPanelControl>(FindObjectsInactive.Include);

                if (buildingTile != null && _buildingPanel != null)
                {
                    if (_cameraControl != null)
                        _cameraControl.SetTutorialInputRestrictions(
                            restrictInput: true,
                            allowWorldDrag: true,
                            allowZoom: true,
                            allowMinimapRotation: true);
                    TileInteraction.SetTutorialAllowedTile(buildingTile);
                    TileInteraction.SetSelectionEnabled(true);
                    _waitingForSecondBuildingPanel = true;
                    var ti = TileInteraction.GetInstance();
                    if (ti != null) ti.OnTileSelected += OnSecondBuildingTileSelectedForPanel;
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.SelectThirdBuilding:
            {
                TileControl buildingTile = _placedThirdBuilding != null
                    ? _placedThirdBuilding.GetComponentInParent<TileControl>()
                    : null;
                buildingTile ??= FindTileControlNear(_placedThirdBuildingWorldPos);

                if (_buildingPanel == null)
                    _buildingPanel = FindFirstObjectByType<BuildingPanelControl>(FindObjectsInactive.Include);

                if (buildingTile != null && _buildingPanel != null)
                {
                    if (_cameraControl != null)
                        _cameraControl.SetTutorialInputRestrictions(
                            restrictInput: true,
                            allowWorldDrag: true,
                            allowZoom: true,
                            allowMinimapRotation: true);
                    TileInteraction.SetTutorialAllowedTile(buildingTile);
                    TileInteraction.SetSelectionEnabled(true);
                    _waitingForThirdBuildingPanel = true;
                    var ti = TileInteraction.GetInstance();
                    if (ti != null) ti.OnTileSelected += OnThirdBuildingTileSelected;
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.ClickSwitchBuildingType:
            {
                if (_buildingPanel == null)
                    _buildingPanel = FindFirstObjectByType<BuildingPanelControl>(FindObjectsInactive.Include);
                if (_buildingPanel != null)
                {
                    _buildingPanel.OnBuildingTypeSwitched += OnBuildingTypeSwitched;
                    _waitingForBuildingTypeSwitch = true;
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.OpenCraftingPanel:
            {
                CraftingBuildingPanelControl.TutorialShowAllRecipes = true;
                if (_craftingPanel == null)
                    _craftingPanel = FindFirstObjectByType<CraftingBuildingPanelControl>(FindObjectsInactive.Include);
                if (_craftingPanel != null)
                {
                    if (_craftingPanel.IsShowing)
                    {
                        _craftingPanel.RefreshForTutorial();
                        ShowPart(_currentPart + 1);
                    }
                    else
                    {
                        _craftingPanel.OnOpen += OnCraftingPanelOpened;
                        _waitingForCraftingPanelOpen = true;
                    }
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.OpenCraftingCostPanel:
            {
                if (_craftingPanel == null)
                    _craftingPanel = FindFirstObjectByType<CraftingBuildingPanelControl>(FindObjectsInactive.Include);
                _tutorialCraftingItem = _craftingPanel != null
                    ? _craftingPanel.contentRoot.GetComponentInChildren<CraftingRecipeItem>(true)
                    : null;
                if (_tutorialCraftingItem != null)
                {
                    if (_tutorialCraftingItem.costPanelRoot != null && _tutorialCraftingItem.costPanelRoot.activeSelf)
                    {
                        ShowPart(_currentPart + 1);
                    }
                    else
                    {
                        _tutorialCraftingItem.OnCostsPanelShown += OnCraftingCostPanelShown;
                        _waitingForCraftingCostPanel = true;
                    }
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.ClickCraftingOutputView:
            {
                if (_tutorialCraftingItem == null && _craftingPanel != null)
                    _tutorialCraftingItem = _craftingPanel.contentRoot.GetComponentInChildren<CraftingRecipeItem>(true);
                if (_tutorialCraftingItem != null)
                {
                    _tutorialCraftingItem.OnOutputViewShown += OnCraftingOutputViewShown;
                    _waitingForCraftingOutputView = true;
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.CloseCraftingPanel:
            {
                if (_craftingPanel == null)
                    _craftingPanel = FindFirstObjectByType<CraftingBuildingPanelControl>(FindObjectsInactive.Include);
                if (_craftingPanel != null)
                {
                    if (!_craftingPanel.IsShowing)
                    {
                        CraftingBuildingPanelControl.TutorialShowAllRecipes = false;
                        ShowPart(_currentPart + 1);
                    }
                    else
                    {
                        _craftingPanel.OnClose += OnCraftingPanelClosed;
                        _waitingForCraftingPanelClose = true;
                    }
                }
                else
                {
                    CraftingBuildingPanelControl.TutorialShowAllRecipes = false;
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.FourthBuildingPlacement:
                if (_cameraControl != null)
                    _cameraControl.SetTutorialInputRestrictions(
                        restrictInput: true,
                        allowWorldDrag: false,
                        allowZoom: false,
                        allowMinimapRotation: false);
                PlaceFourthBuildingOnMap();
                _activeNextButton = FindNextButton(tutorialParts[_currentPart]);
                if (_activeNextButton != null)
                {
                    _activeNextButton.gameObject.SetActive(true);
                    _activeNextButton.interactable = true;
                    _activeNextButton.onClick.AddListener(OnNextPressed);
                }
                break;

            case PartType.SelectFourthBuilding:
            {
                TileControl buildingTile = _placedFourthBuilding != null
                    ? _placedFourthBuilding.GetComponentInParent<TileControl>()
                    : null;
                buildingTile ??= FindTileControlNear(_placedFourthBuildingWorldPos);

                if (_buildingPanel == null)
                    _buildingPanel = FindFirstObjectByType<BuildingPanelControl>(FindObjectsInactive.Include);

                if (buildingTile != null && _buildingPanel != null)
                {
                    if (_cameraControl != null)
                        _cameraControl.SetTutorialInputRestrictions(
                            restrictInput: true,
                            allowWorldDrag: true,
                            allowZoom: true,
                            allowMinimapRotation: true);
                    TileInteraction.SetTutorialAllowedTile(buildingTile);
                    TileInteraction.SetSelectionEnabled(true);
                    _waitingForFourthBuildingPanel = true;
                    var ti = TileInteraction.GetInstance();
                    if (ti != null) ti.OnTileSelected += OnFourthBuildingTileSelected;
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.OpenProductionPanel:
            {
                ProductionBuildingPanelControl.TutorialShowAllPlans = true;
                if (_productionPanel == null)
                    _productionPanel = FindFirstObjectByType<ProductionBuildingPanelControl>(FindObjectsInactive.Include);
                if (_productionPanel != null)
                {
                    if (_productionPanel.IsShowing)
                    {
                        _productionPanel.RefreshForTutorial();
                        ShowPart(_currentPart + 1);
                    }
                    else
                    {
                        _productionPanel.OnOpen += OnProductionPanelOpened;
                        _waitingForProductionPanelOpen = true;
                    }
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.StartProductionPlan:
            {
                if (_productionPanel == null)
                    _productionPanel = FindFirstObjectByType<ProductionBuildingPanelControl>(FindObjectsInactive.Include);
                _tutorialProductionItem = _productionPanel != null
                    ? _productionPanel.contentRoot.GetComponentInChildren<ProductionPlanItem>(true)
                    : null;
                if (_tutorialProductionItem != null)
                {
                    ProductionPlanItem.TutorialBypassCosts = true;
                    _tutorialProductionItem.OnProductionStarted += OnProductionPlanStarted;
                    _waitingForProductionStart = true;
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.SelectProductionTargets:
            {
                if (!ProductionSelectionController.IsSelectionActive)
                {
                    ShowPart(_currentPart + 1);
                }
                else
                {
                    if (_cameraControl != null)
                    {
                        _cameraControl.SetTutorialInputRestrictions(
                            restrictInput: true,
                            allowWorldDrag: true,
                            allowZoom: true,
                            allowMinimapRotation: false);
                        _cameraControl.ZoomByUnits(10f);
                    }
                    ProductionSelectionController.OnSelectionCompleted += OnProductionSelectionCompleted;
                    _waitingForProductionTargets = true;
                }
                break;
            }

            case PartType.OpenProductionRunningPanel:
                ShowPart(_currentPart + 1);
                break;

            case PartType.CloseProductionRunningPanel:
            {
                if (_productionRunningPanel == null)
                    _productionRunningPanel = FindFirstObjectByType<ProductionRunningPanelControl>(FindObjectsInactive.Include);
                if (_productionRunningPanel != null)
                {
                    if (!_productionRunningPanel.IsShowing)
                    {
                        ProductionBuildingPanelControl.TutorialShowAllPlans = false;
                        ShowPart(_currentPart + 1);
                    }
                    else
                    {
                        _productionRunningPanel.OnClose += OnProductionRunningPanelClosed;
                        _waitingForProductionRunningPanelClose = true;
                    }
                }
                else
                {
                    ProductionBuildingPanelControl.TutorialShowAllPlans = false;
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.FifthBuildingPlacement:
                if (_cameraControl != null)
                    _cameraControl.SetTutorialInputRestrictions(
                        restrictInput: true,
                        allowWorldDrag: false,
                        allowZoom: false,
                        allowMinimapRotation: false);
                PlaceFifthBuildingOnMap();
                _activeNextButton = FindNextButton(tutorialParts[_currentPart]);
                if (_activeNextButton != null)
                {
                    _activeNextButton.gameObject.SetActive(true);
                    _activeNextButton.interactable = true;
                    _activeNextButton.onClick.AddListener(OnNextPressed);
                }
                break;

            case PartType.SelectFifthBuilding:
            {
                TileControl buildingTile = _placedFifthBuilding != null
                    ? _placedFifthBuilding.GetComponentInParent<TileControl>()
                    : null;
                buildingTile ??= FindTileControlNear(_placedFifthBuildingWorldPos);

                if (_buildingPanel == null)
                    _buildingPanel = FindFirstObjectByType<BuildingPanelControl>(FindObjectsInactive.Include);

                if (buildingTile != null && _buildingPanel != null)
                {
                    if (_cameraControl != null)
                        _cameraControl.SetTutorialInputRestrictions(
                            restrictInput: true,
                            allowWorldDrag: true,
                            allowZoom: true,
                            allowMinimapRotation: true);
                    TileInteraction.SetTutorialAllowedTile(buildingTile);
                    TileInteraction.SetSelectionEnabled(true);
                    _waitingForFifthBuildingPanel = true;
                    var ti = TileInteraction.GetInstance();
                    if (ti != null) ti.OnTileSelected += OnFifthBuildingTileSelected;
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.OpenTradePanel:
            {
                if (_tradePanel == null)
                    _tradePanel = FindFirstObjectByType<TradePanelControl>(FindObjectsInactive.Include);
                if (_tradePanel != null)
                {
                    if (_tradePanel.IsShowing)
                    {
                        ShowPart(_currentPart + 1);
                    }
                    else
                    {
                        _tradePanel.OnOpen += OnTradePanelOpened;
                        _waitingForTradePanelOpen = true;
                    }
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.SelectTraderEntry:
            {
                TraderPanelControl.TutorialShowAllOfferings = true;
                if (_traderPanel == null)
                    _traderPanel = FindFirstObjectByType<TraderPanelControl>(FindObjectsInactive.Include);
                if (_traderPanel != null)
                {
                    if (_traderPanel.IsShowing)
                    {
                        ShowPart(_currentPart + 1);
                    }
                    else
                    {
                        _traderPanel.OnOpen += OnTraderPanelOpened;
                        _waitingForTraderPanelOpen = true;
                    }
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.OpenTraderOffering:
            {
                if (_offeringPanel == null)
                    _offeringPanel = FindFirstObjectByType<OfferingPanelControl>(FindObjectsInactive.Include);
                if (_offeringPanel != null)
                {
                    if (_offeringPanel.IsShowing)
                    {
                        ShowPart(_currentPart + 1);
                    }
                    else
                    {
                        _offeringPanel.OnOpen += OnOfferingPanelOpened;
                        _waitingForOfferingPanelOpen = true;
                    }
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.OfferResources:
            {
                if (_offeringPanel == null)
                    _offeringPanel = FindFirstObjectByType<OfferingPanelControl>(FindObjectsInactive.Include);
                if (_offeringPanel != null)
                {
                    _offeringPanel.OnPlayerOfferAdded += OnPlayerOfferAdded;
                    _waitingForPlayerOffer = true;
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.FinishTrade:
            {
                if (_offeringPanel == null)
                    _offeringPanel = FindFirstObjectByType<OfferingPanelControl>(FindObjectsInactive.Include);
                if (_offeringPanel != null)
                {
                    _offeringPanel.OnTradeAccepted += OnTradeAccepted;
                    _waitingForTradeAccepted = true;
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.CloseTraderPanel:
            {
                if (_traderPanel == null)
                    _traderPanel = FindFirstObjectByType<TraderPanelControl>(FindObjectsInactive.Include);
                if (_traderPanel != null)
                {
                    if (!_traderPanel.IsShowing)
                    {
                        ShowPart(_currentPart + 1);
                    }
                    else
                    {
                        _traderPanel.OnClose += OnTraderPanelClosed;
                        _waitingForTraderPanelClose = true;
                    }
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.CloseTradePanel:
            {
                if (_tradePanel == null)
                    _tradePanel = FindFirstObjectByType<TradePanelControl>(FindObjectsInactive.Include);
                if (_tradePanel != null)
                {
                    if (!_tradePanel.IsShowing)
                    {
                        ShowPart(_currentPart + 1);
                    }
                    else
                    {
                        _tradePanel.OnClose += OnTradePanelClosed;
                        _waitingForTradePanelClose = true;
                    }
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.SixthBuildingPlacement:
                if (_cameraControl != null)
                    _cameraControl.SetTutorialInputRestrictions(
                        restrictInput: true,
                        allowWorldDrag: false,
                        allowZoom: false,
                        allowMinimapRotation: false);
                PlaceSixthBuildingOnMap();
                _activeNextButton = FindNextButton(tutorialParts[_currentPart]);
                if (_activeNextButton != null)
                {
                    _activeNextButton.gameObject.SetActive(true);
                    _activeNextButton.interactable = true;
                    _activeNextButton.onClick.AddListener(OnNextPressed);
                }
                break;

            case PartType.SelectSixthBuilding:
            {
                TileControl buildingTile = _placedSixthBuilding != null
                    ? _placedSixthBuilding.GetComponentInParent<TileControl>()
                    : null;
                buildingTile ??= FindTileControlNear(_placedSixthBuildingWorldPos);

                if (_buildingPanel == null)
                    _buildingPanel = FindFirstObjectByType<BuildingPanelControl>(FindObjectsInactive.Include);

                if (buildingTile != null && _buildingPanel != null)
                {
                    if (_cameraControl != null)
                        _cameraControl.SetTutorialInputRestrictions(
                            restrictInput: true,
                            allowWorldDrag: true,
                            allowZoom: true,
                            allowMinimapRotation: true);
                    TileInteraction.SetTutorialAllowedTile(buildingTile);
                    TileInteraction.SetSelectionEnabled(true);
                    _waitingForSixthBuildingPanel = true;
                    var ti = TileInteraction.GetInstance();
                    if (ti != null) ti.OnTileSelected += OnSixthBuildingTileSelected;
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.OpenReligiousPanel:
            {
                if (_religiousPanel == null)
                    _religiousPanel = FindFirstObjectByType<ReligiousBuildingPanelControl>(FindObjectsInactive.Include);
                if (_religiousPanel != null)
                {
                    if (_religiousPanel.IsShowing)
                    {
                        ShowPart(_currentPart + 1);
                    }
                    else
                    {
                        _religiousPanel.OnOpen += OnReligiousPanelOpened;
                        _waitingForReligiousPanelOpen = true;
                    }
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.OpenRitualPanel:
            {
                if (_ritualPanel == null)
                    _ritualPanel = FindFirstObjectByType<ReligiousRitualPanelControl>(FindObjectsInactive.Include);
                ReligiousRitualPanelControl.TutorialShowOnlySummoningRitual = true;
                if (_ritualPanel != null)
                {
                    if (_ritualPanel.IsShowing)
                    {
                        ShowPart(_currentPart + 1);
                    }
                    else
                    {
                        _ritualPanel.OnOpen += OnRitualPanelOpened;
                        _waitingForRitualPanelOpen = true;
                    }
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.StartInitialSummoningRitual:
            {
                ReligiousBuildingControl.TutorialBypassChecks = true;
                if (_ritualBuildingControl == null && _religiousPanel != null)
                    _ritualBuildingControl = _religiousPanel.CurrentControl;
                if (_ritualBuildingControl != null)
                {
                    _ritualBuildingControl.OnRitualStarted += OnRitualStarted;
                    _waitingForRitualStart = true;
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.FastForwardRitual:
            {
                if (_ritualBuildingControl == null && _religiousPanel != null)
                    _ritualBuildingControl = _religiousPanel.CurrentControl;

                if (_ritualPanel != null && _ritualPanel.IsShowing) _ritualPanel.Hide();
                if (_religiousPanel != null && _religiousPanel.IsShowing) _religiousPanel.Hide();
                if (_buildingPanel != null && _buildingPanel.IsShowing) _buildingPanel.Hide();

                PlayerRitualManager.TutorialBypassSpiritFilter = true;

                if (_ritualBuildingControl != null && _ritualBuildingControl.HasActiveRitual)
                {
                    if (_fastForwardRitualRoutine != null) StopCoroutine(_fastForwardRitualRoutine);
                    _fastForwardRitualRoutine = StartCoroutine(FastForwardRitualCoroutine(_ritualBuildingControl));
                }
                else
                {
                    PlayerRitualManager.TutorialBypassSpiritFilter = false;
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.SelectSummoningSpirit:
            {
                PlayerReligionManager.TutorialBypassAcceptChecks = true;
                if (_summoningOfferPanel == null)
                    _summoningOfferPanel = FindFirstObjectByType<SummoningSpiritOfferPanelControl>(FindObjectsInactive.Include);
                if (_summoningOfferPanel != null)
                {
                    if (_summoningOfferPanel.IsShowing)
                    {
                        _summoningOfferPanel.OnSpiritChosen += OnSpiritChosenCallback;
                        _waitingForSpiritChosen = true;
                    }
                    else
                    {
                        _summoningOfferPanel.OnOpen += OnSummoningPanelOpened;
                        _waitingForSummoningPanelOpen = true;
                    }
                }
                else
                {
                    PlayerReligionManager.TutorialBypassAcceptChecks = false;
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.RegenerateMapClearBuildings:
                if (_cameraControl != null)
                    _cameraControl.SetTutorialInputRestrictions(
                        restrictInput: true,
                        allowWorldDrag: false,
                        allowZoom: false,
                        allowMinimapRotation: false);
                TileInteraction.SetSelectionEnabled(false);
                if (_regenRoutine != null) StopCoroutine(_regenRoutine);
                _regenRoutine = StartCoroutine(TutorialRegenerateMapClearBuildingsCoroutine());
                break;

            case PartType.SeventhBuildingPlacement:
                if (_cameraControl != null)
                    _cameraControl.SetTutorialInputRestrictions(
                        restrictInput: true,
                        allowWorldDrag: false,
                        allowZoom: false,
                        allowMinimapRotation: false);
                PlaceSeventhBuildingOnMap();
                _activeNextButton = FindNextButton(tutorialParts[_currentPart]);
                if (_activeNextButton != null)
                {
                    _activeNextButton.gameObject.SetActive(true);
                    _activeNextButton.interactable = true;
                    _activeNextButton.onClick.AddListener(OnNextPressed);
                }
                break;

            case PartType.SelectSeventhBuilding:
            {
                TileControl buildingTile = _placedSeventhBuilding != null
                    ? _placedSeventhBuilding.GetComponentInParent<TileControl>()
                    : null;
                buildingTile ??= FindTileControlNear(_placedSeventhBuildingWorldPos);

                if (_buildingPanel == null)
                    _buildingPanel = FindFirstObjectByType<BuildingPanelControl>(FindObjectsInactive.Include);

                if (buildingTile != null && _buildingPanel != null)
                {
                    if (_cameraControl != null)
                        _cameraControl.SetTutorialInputRestrictions(
                            restrictInput: true,
                            allowWorldDrag: true,
                            allowZoom: true,
                            allowMinimapRotation: true);
                    TileInteraction.SetTutorialAllowedTile(buildingTile);
                    TileInteraction.SetSelectionEnabled(true);
                    _waitingForSeventhBuildingPanel = true;
                    var ti = TileInteraction.GetInstance();
                    if (ti != null) ti.OnTileSelected += OnSeventhBuildingTileSelected;
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.OpenStoragePanel:
            {
                if (_storagePanel == null)
                    _storagePanel = FindFirstObjectByType<StoragePanelControl>(FindObjectsInactive.Include);
                if (_storagePanel != null)
                {
                    if (_storagePanel.IsShowing)
                    {
                        ShowPart(_currentPart + 1);
                    }
                    else
                    {
                        _waitingForStoragePanelOpen = true;
                    }
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.CloseStoragePanel:
            {
                if (_storagePanel == null)
                    _storagePanel = FindFirstObjectByType<StoragePanelControl>(FindObjectsInactive.Include);
                if (_storagePanel != null)
                {
                    if (!_storagePanel.IsShowing)
                    {
                        ShowPart(_currentPart + 1);
                    }
                    else
                    {
                        _storagePanel.OnClose += OnStoragePanelClosed;
                        _waitingForStoragePanelClose = true;
                    }
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.CloseResearchNeedsPanel:
            {
                if (_trackedTechItem == null && _researchPanel != null && _researchPanel.contentRoot != null)
                    _trackedTechItem = _researchPanel.contentRoot.GetComponentInChildren<TechnologyItem>(true);

                if (_trackedTechItem != null)
                {
                    if (!_trackedTechItem.NeedsPanelActive)
                    {
                        ShowPart(_currentPart + 1);
                    }
                    else
                    {
                        _trackedTechItem.OnNeedsPanelHidden += OnResearchNeedsPanelHidden;
                        _waitingForResearchNeedsPanelClose = true;
                    }
                }
                else
                {
                    ShowPart(_currentPart + 1);
                }
                break;
            }

            case PartType.CloseResearchPanel:
            {
                if (_researchPanel == null)
                    _researchPanel = FindFirstObjectByType<ResearchPanelControl>(FindObjectsInactive.Include);
                if (_researchPanel != null)
                {
                    if (!_researchPanel.IsShowing)
                    {
                        ShowPart(_currentPart + 1);
                    }
                    else
                    {
                        _researchPanel.OnClose += OnResearchPanelClosed;
                        _waitingForResearchPanelClose = true;
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

    private void PlaceSecondBuildingOnMap()
    {
        if (string.IsNullOrEmpty(secondBuildingID) || BuildingManager.Instance == null)
            return;

        Building buildingDef = BuildingManager.Instance.GetBuildingByID(secondBuildingID);
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

        GameObject prefab = buildingDef.finalBuildingPrefab != null
            ? buildingDef.finalBuildingPrefab
            : buildingDef.buildingPrefab;

        _placedSecondBuildingWorldPos = worldPos;

        if (prefab != null)
            _placedSecondBuilding = Instantiate(prefab, worldPos, target.transform.rotation);

        TileControl tileControl = target.GetComponent<TileControl>();
        GameObject toDestroy = (tileControl != null && tileControl.transform.parent != null)
            ? tileControl.transform.parent.gameObject
            : target.gameObject;
        Destroy(toDestroy);

        _cameraControl?.FocusOnPoint(worldPos, envForward, 6f);
    }

    private void PlaceThirdBuildingOnMap()
    {
        if (BuildingManager.Instance == null)
            return;

        EnvironmentControl[] allEnvs = FindObjectsByType<EnvironmentControl>(FindObjectsSortMode.None);

        // Try primary building ID, then alternate, then same-size fallback.
        Building primaryDef = !string.IsNullOrEmpty(thirdBuildingID)
            ? BuildingManager.Instance.GetBuildingByID(thirdBuildingID)
            : null;

        Building alternateDef = !string.IsNullOrEmpty(thirdBuildingAlternateID)
            ? BuildingManager.Instance.GetBuildingByID(thirdBuildingAlternateID)
            : null;

        Building chosenDef = null;
        EnvironmentControl chosenEnv = null;

        // 1. Try primary
        if (primaryDef != null)
        {
            EnvironmentControl env = FindBuildingCandidate(allEnvs, primaryDef);
            if (env != null) { chosenDef = primaryDef; chosenEnv = env; }
        }

        // 2. Try alternate
        if (chosenEnv == null && alternateDef != null)
        {
            EnvironmentControl env = FindBuildingCandidate(allEnvs, alternateDef);
            if (env != null) { chosenDef = alternateDef; chosenEnv = env; }
        }

        // 3. Same-size fallback: use primary def (or alternate) but ignore environment type/tile type
        if (chosenEnv == null)
        {
            Building fallbackDef = primaryDef ?? alternateDef;
            if (fallbackDef != null)
            {
                List<EnvironmentControl> sizeMatches = new List<EnvironmentControl>();
                foreach (EnvironmentControl env in allEnvs)
                {
                    if (env.tileSize == fallbackDef.requiredTileSize && (env.environmentTileType == EnvironmentTileType.Land || env.environmentTileType == EnvironmentTileType.Cave || env.environmentTileType == EnvironmentTileType.Mountain))
                        sizeMatches.Add(env);
                }
                if (sizeMatches.Count > 0)
                {
                    chosenDef = fallbackDef;
                    chosenEnv = sizeMatches[Random.Range(0, sizeMatches.Count)];
                }
            }
        }

        if (chosenDef == null || chosenEnv == null)
            return;

        Vector3 worldPos = chosenEnv.transform.position;
        Vector3 envForward = chosenEnv.transform.forward;

        GameObject prefab = chosenDef.finalBuildingPrefab != null
            ? chosenDef.finalBuildingPrefab
            : chosenDef.buildingPrefab;

        _placedThirdBuildingWorldPos = worldPos;

        if (prefab != null)
            _placedThirdBuilding = Instantiate(prefab, worldPos, chosenEnv.transform.rotation);

        TileControl tileControl = chosenEnv.GetComponent<TileControl>();
        GameObject toDestroy = (tileControl != null && tileControl.transform.parent != null)
            ? tileControl.transform.parent.gameObject
            : chosenEnv.gameObject;
        Destroy(toDestroy);

        _cameraControl?.FocusOnPoint(worldPos, envForward, 6f);
    }

    private void PlaceFifthBuildingOnMap()
    {
        if (BuildingManager.Instance == null)
            return;

        EnvironmentControl[] allEnvs = FindObjectsByType<EnvironmentControl>(FindObjectsSortMode.None);

        Building primaryDef = !string.IsNullOrEmpty(fifthBuildingID)
            ? BuildingManager.Instance.GetBuildingByID(fifthBuildingID)
            : null;

        Building alternateDef = !string.IsNullOrEmpty(fifthBuildingAlternateID)
            ? BuildingManager.Instance.GetBuildingByID(fifthBuildingAlternateID)
            : null;

        Building chosenDef = null;
        EnvironmentControl chosenEnv = null;

        if (primaryDef != null)
        {
            EnvironmentControl env = FindBuildingCandidate(allEnvs, primaryDef);
            if (env != null) { chosenDef = primaryDef; chosenEnv = env; }
        }

        if (chosenEnv == null && alternateDef != null)
        {
            EnvironmentControl env = FindBuildingCandidate(allEnvs, alternateDef);
            if (env != null) { chosenDef = alternateDef; chosenEnv = env; }
        }

        if (chosenEnv == null)
        {
            Building fallbackDef = primaryDef ?? alternateDef;
            if (fallbackDef != null)
            {
                List<EnvironmentControl> sizeMatches = new List<EnvironmentControl>();
                foreach (EnvironmentControl env in allEnvs)
                {
                    if (env.tileSize == fallbackDef.requiredTileSize && (env.environmentTileType == EnvironmentTileType.Land || env.environmentTileType == EnvironmentTileType.Cave || env.environmentTileType == EnvironmentTileType.Mountain))
                        sizeMatches.Add(env);
                }
                if (sizeMatches.Count > 0)
                {
                    chosenDef = fallbackDef;
                    chosenEnv = sizeMatches[Random.Range(0, sizeMatches.Count)];
                }
            }
        }

        if (chosenDef == null || chosenEnv == null)
            return;

        Vector3 worldPos = chosenEnv.transform.position;
        Vector3 envForward = chosenEnv.transform.forward;

        GameObject prefab = chosenDef.finalBuildingPrefab != null
            ? chosenDef.finalBuildingPrefab
            : chosenDef.buildingPrefab;

        _placedFifthBuildingWorldPos = worldPos;

        if (prefab != null)
            _placedFifthBuilding = Instantiate(prefab, worldPos, chosenEnv.transform.rotation);

        TileControl tileControl = chosenEnv.GetComponent<TileControl>();
        GameObject toDestroy = (tileControl != null && tileControl.transform.parent != null)
            ? tileControl.transform.parent.gameObject
            : chosenEnv.gameObject;
        Destroy(toDestroy);

        // Force a trader to appear immediately so the trade panel shows one when opened
        if (_placedFifthBuilding != null)
        {
            var tradeCtrl = _placedFifthBuilding.GetComponent<TradeBuildingControl>();
            if (tradeCtrl != null && !tradeCtrl.HasActiveTrader())
                tradeCtrl.GenerateTrader();
        }

        _cameraControl?.FocusOnPoint(worldPos, envForward, 6f);
    }

    private void PlaceSixthBuildingOnMap()
    {
        if (BuildingManager.Instance == null)
            return;

        EnvironmentControl[] allEnvs = FindObjectsByType<EnvironmentControl>(FindObjectsSortMode.None);

        Building primaryDef = !string.IsNullOrEmpty(sixthBuildingID)
            ? BuildingManager.Instance.GetBuildingByID(sixthBuildingID)
            : null;

        Building alternateDef = !string.IsNullOrEmpty(sixthBuildingAlternateID)
            ? BuildingManager.Instance.GetBuildingByID(sixthBuildingAlternateID)
            : null;

        Building chosenDef = null;
        EnvironmentControl chosenEnv = null;

        if (primaryDef != null)
        {
            EnvironmentControl env = FindBuildingCandidate(allEnvs, primaryDef);
            if (env != null) { chosenDef = primaryDef; chosenEnv = env; }
        }

        if (chosenEnv == null && alternateDef != null)
        {
            EnvironmentControl env = FindBuildingCandidate(allEnvs, alternateDef);
            if (env != null) { chosenDef = alternateDef; chosenEnv = env; }
        }

        if (chosenEnv == null)
        {
            Building fallbackDef = primaryDef ?? alternateDef;
            if (fallbackDef != null)
            {
                List<EnvironmentControl> sizeMatches = new List<EnvironmentControl>();
                foreach (EnvironmentControl env in allEnvs)
                {
                    if (env.tileSize == fallbackDef.requiredTileSize && (env.environmentTileType == EnvironmentTileType.Land || env.environmentTileType == EnvironmentTileType.Cave || env.environmentTileType == EnvironmentTileType.Mountain))
                        sizeMatches.Add(env);
                }
                if (sizeMatches.Count > 0)
                {
                    chosenDef = fallbackDef;
                    chosenEnv = sizeMatches[Random.Range(0, sizeMatches.Count)];
                }
            }
        }

        if (chosenDef == null || chosenEnv == null)
            return;

        Vector3 worldPos = chosenEnv.transform.position;
        Vector3 envForward = chosenEnv.transform.forward;

        GameObject prefab = chosenDef.finalBuildingPrefab != null
            ? chosenDef.finalBuildingPrefab
            : chosenDef.buildingPrefab;

        _placedSixthBuildingWorldPos = worldPos;

        if (prefab != null)
            _placedSixthBuilding = Instantiate(prefab, worldPos, chosenEnv.transform.rotation);

        TileControl tileControl = chosenEnv.GetComponent<TileControl>();
        GameObject toDestroy = (tileControl != null && tileControl.transform.parent != null)
            ? tileControl.transform.parent.gameObject
            : chosenEnv.gameObject;
        Destroy(toDestroy);

        _cameraControl?.FocusOnPoint(worldPos, envForward, 6f);
    }

    private void PlaceFourthBuildingOnMap()
    {
        if (BuildingManager.Instance == null)
            return;

        EnvironmentControl[] allEnvs = FindObjectsByType<EnvironmentControl>(FindObjectsSortMode.None);

        Building primaryDef = !string.IsNullOrEmpty(fourthBuildingID)
            ? BuildingManager.Instance.GetBuildingByID(fourthBuildingID)
            : null;

        Building alternateDef = !string.IsNullOrEmpty(fourthBuildingAlternateID)
            ? BuildingManager.Instance.GetBuildingByID(fourthBuildingAlternateID)
            : null;

        Building chosenDef = null;
        EnvironmentControl chosenEnv = null;

        if (primaryDef != null)
        {
            EnvironmentControl env = FindBuildingCandidate(allEnvs, primaryDef);
            if (env != null) { chosenDef = primaryDef; chosenEnv = env; }
        }

        if (chosenEnv == null && alternateDef != null)
        {
            EnvironmentControl env = FindBuildingCandidate(allEnvs, alternateDef);
            if (env != null) { chosenDef = alternateDef; chosenEnv = env; }
        }

        if (chosenEnv == null)
        {
            Building fallbackDef = primaryDef ?? alternateDef;
            if (fallbackDef != null)
            {
                List<EnvironmentControl> sizeMatches = new List<EnvironmentControl>();
                foreach (EnvironmentControl env in allEnvs)
                {
                    if (env.tileSize == fallbackDef.requiredTileSize && (env.environmentTileType == EnvironmentTileType.Land || env.environmentTileType == EnvironmentTileType.Cave || env.environmentTileType == EnvironmentTileType.Mountain))
                        sizeMatches.Add(env);
                }
                if (sizeMatches.Count > 0)
                {
                    chosenDef = fallbackDef;
                    chosenEnv = sizeMatches[Random.Range(0, sizeMatches.Count)];
                }
            }
        }

        if (chosenDef == null || chosenEnv == null)
            return;

        Vector3 worldPos = chosenEnv.transform.position;
        Vector3 envForward = chosenEnv.transform.forward;

        GameObject prefab = chosenDef.finalBuildingPrefab != null
            ? chosenDef.finalBuildingPrefab
            : chosenDef.buildingPrefab;

        _placedFourthBuildingWorldPos = worldPos;

        if (prefab != null)
            _placedFourthBuilding = Instantiate(prefab, worldPos, chosenEnv.transform.rotation);

        TileControl tileControl = chosenEnv.GetComponent<TileControl>();
        GameObject toDestroy = (tileControl != null && tileControl.transform.parent != null)
            ? tileControl.transform.parent.gameObject
            : chosenEnv.gameObject;
        Destroy(toDestroy);

        _cameraControl?.FocusOnPoint(worldPos, envForward, 6f);
    }

    private EnvironmentControl FindBuildingCandidate(EnvironmentControl[] allEnvs, Building buildingDef)
    {
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

        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
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

        PlayerInventoryManager.TutorialBypassCapacity = true;
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

    private IEnumerator TutorialRegenerateMapClearBuildingsCoroutine()
    {
        if (_tileActivator != null)
            _tileActivator.OnTilesActivated -= OnWorldSpawned;

        if (_discoveredTilePanel == null)
            _discoveredTilePanel = FindFirstObjectByType<DiscoveredTilePanelControl>(FindObjectsInactive.Include);
        _discoveredTilePanel?.Hide();

        GameObject[] placedBuildings = new[]
        {
            _placedShelterBuilding, _placedSecondBuilding, _placedThirdBuilding,
            _placedFourthBuilding, _placedFifthBuilding, _placedSixthBuilding
        };
        foreach (GameObject b in placedBuildings)
        {
            if (b != null) Destroy(b);
        }
        _placedShelterBuilding = null;
        _placedSecondBuilding = null;
        _placedThirdBuilding = null;
        _placedFourthBuilding = null;
        _placedFifthBuilding = null;
        _placedSixthBuilding = null;

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
        yield return null;

        MapTilePlacer.ResetWorldReady();
        _mapGenerator.enabled = true;
        _mapTilePlacer.enabled = true;

        yield return StartCoroutine(_mapGenerator.RegenerateCoroutine());

        _mapTilePlacer.BeginPlacement();
        yield return new WaitUntil(() => MapTilePlacer.WorldReady);

        _tileActivator.BeginActivation(_tileActivator.timerUI, true, true);
        yield return new WaitUntil(() => !_tileActivator.IsRunning);

        var allEnvs = FindObjectsByType<EnvironmentControl>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < allEnvs.Length; i++)
        {
            if (allEnvs[i] != null)
                allEnvs[i].CompleteTutorialDiscoveryNow();
        }

        _regenRoutine = null;
        ShowPart(_currentPart + 1);
    }

    private void PlaceSeventhBuildingOnMap()
    {
        if (BuildingManager.Instance == null)
            return;

        EnvironmentControl[] allEnvs = FindObjectsByType<EnvironmentControl>(FindObjectsSortMode.None);

        Building primaryDef = !string.IsNullOrEmpty(seventhBuildingID)
            ? BuildingManager.Instance.GetBuildingByID(seventhBuildingID)
            : null;

        Building alternateDef = !string.IsNullOrEmpty(seventhBuildingAlternateID)
            ? BuildingManager.Instance.GetBuildingByID(seventhBuildingAlternateID)
            : null;

        Building chosenDef = null;
        EnvironmentControl chosenEnv = null;

        if (primaryDef != null)
        {
            EnvironmentControl env = FindBuildingCandidate(allEnvs, primaryDef);
            if (env != null) { chosenDef = primaryDef; chosenEnv = env; }
        }

        if (chosenEnv == null && alternateDef != null)
        {
            EnvironmentControl env = FindBuildingCandidate(allEnvs, alternateDef);
            if (env != null) { chosenDef = alternateDef; chosenEnv = env; }
        }

        if (chosenEnv == null)
        {
            Building fallbackDef = primaryDef ?? alternateDef;
            if (fallbackDef != null)
            {
                List<EnvironmentControl> sizeMatches = new List<EnvironmentControl>();
                foreach (EnvironmentControl env in allEnvs)
                {
                    if (env.tileSize == fallbackDef.requiredTileSize && (env.environmentTileType == EnvironmentTileType.Land || env.environmentTileType == EnvironmentTileType.Cave || env.environmentTileType == EnvironmentTileType.Mountain))
                        sizeMatches.Add(env);
                }
                if (sizeMatches.Count > 0)
                {
                    chosenDef = fallbackDef;
                    chosenEnv = sizeMatches[Random.Range(0, sizeMatches.Count)];
                }
            }
        }

        if (chosenDef == null || chosenEnv == null)
            return;

        Vector3 worldPos = chosenEnv.transform.position;
        Vector3 envForward = chosenEnv.transform.forward;

        GameObject prefab = chosenDef.finalBuildingPrefab != null
            ? chosenDef.finalBuildingPrefab
            : chosenDef.buildingPrefab;

        _placedSeventhBuildingWorldPos = worldPos;

        if (prefab != null)
            _placedSeventhBuilding = Instantiate(prefab, worldPos, chosenEnv.transform.rotation);

        TileControl tileControl = chosenEnv.GetComponent<TileControl>();
        GameObject toDestroy = (tileControl != null && tileControl.transform.parent != null)
            ? tileControl.transform.parent.gameObject
            : chosenEnv.gameObject;
        Destroy(toDestroy);

        _cameraControl?.FocusOnPoint(worldPos, envForward, 6f);
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

    private void OnStoragePanelClosed()
    {
        if (!_waitingForStoragePanelClose) return;
        _waitingForStoragePanelClose = false;
        if (_storagePanel != null) _storagePanel.OnClose -= OnStoragePanelClosed;
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

    private void OnSecondBuildingTileSelectedForPanel(TileControl tile)
    {
        if (!_waitingForSecondBuildingPanel) return;
        _waitingForSecondBuildingPanel = false;
        var ti = TileInteraction.GetInstance();
        if (ti != null) ti.OnTileSelected -= OnSecondBuildingTileSelectedForPanel;
        TileInteraction.ClearTutorialAllowedTile();
        ShowPart(_currentPart + 1);
    }

    private void OnThirdBuildingTileSelected(TileControl tile)
    {
        if (!_waitingForThirdBuildingPanel) return;
        _waitingForThirdBuildingPanel = false;
        var ti = TileInteraction.GetInstance();
        if (ti != null) ti.OnTileSelected -= OnThirdBuildingTileSelected;
        TileInteraction.ClearTutorialAllowedTile();
        ShowPart(_currentPart + 1);
    }

    private void OnFourthBuildingTileSelected(TileControl tile)
    {
        if (!_waitingForFourthBuildingPanel) return;
        _waitingForFourthBuildingPanel = false;
        var ti = TileInteraction.GetInstance();
        if (ti != null) ti.OnTileSelected -= OnFourthBuildingTileSelected;
        TileInteraction.ClearTutorialAllowedTile();
        ShowPart(_currentPart + 1);
    }

    private void OnFifthBuildingTileSelected(TileControl tile)
    {
        if (!_waitingForFifthBuildingPanel) return;
        _waitingForFifthBuildingPanel = false;
        var ti = TileInteraction.GetInstance();
        if (ti != null) ti.OnTileSelected -= OnFifthBuildingTileSelected;
        TileInteraction.ClearTutorialAllowedTile();
        ShowPart(_currentPart + 1);
    }

    private void OnSixthBuildingTileSelected(TileControl tile)
    {
        if (!_waitingForSixthBuildingPanel) return;
        _waitingForSixthBuildingPanel = false;
        var ti = TileInteraction.GetInstance();
        if (ti != null) ti.OnTileSelected -= OnSixthBuildingTileSelected;
        TileInteraction.ClearTutorialAllowedTile();
        ShowPart(_currentPart + 1);
    }

    private void OnSeventhBuildingTileSelected(TileControl tile)
    {
        if (!_waitingForSeventhBuildingPanel) return;
        _waitingForSeventhBuildingPanel = false;
        var ti = TileInteraction.GetInstance();
        if (ti != null) ti.OnTileSelected -= OnSeventhBuildingTileSelected;
        TileInteraction.ClearTutorialAllowedTile();
        ShowPart(_currentPart + 1);
    }

    private void OnReligiousPanelOpened()
    {
        if (!_waitingForReligiousPanelOpen) return;
        _waitingForReligiousPanelOpen = false;
        if (_religiousPanel != null) _religiousPanel.OnOpen -= OnReligiousPanelOpened;
        ShowPart(_currentPart + 1);
    }

    private void OnRitualPanelOpened()
    {
        if (!_waitingForRitualPanelOpen) return;
        _waitingForRitualPanelOpen = false;
        if (_ritualPanel != null) _ritualPanel.OnOpen -= OnRitualPanelOpened;
        ShowPart(_currentPart + 1);
    }

    private void OnRitualStarted()
    {
        if (!_waitingForRitualStart) return;
        _waitingForRitualStart = false;
        if (_ritualBuildingControl != null) _ritualBuildingControl.OnRitualStarted -= OnRitualStarted;
        ShowPart(_currentPart + 1);
    }

    private void OnSummoningPanelOpened()
    {
        if (!_waitingForSummoningPanelOpen) return;
        _waitingForSummoningPanelOpen = false;
        if (_summoningOfferPanel != null) _summoningOfferPanel.OnOpen -= OnSummoningPanelOpened;
        _summoningOfferPanel.OnSpiritChosen += OnSpiritChosenCallback;
        _waitingForSpiritChosen = true;
    }

    private void OnSpiritChosenCallback()
    {
        if (!_waitingForSpiritChosen) return;
        _waitingForSpiritChosen = false;
        if (_summoningOfferPanel != null) _summoningOfferPanel.OnSpiritChosen -= OnSpiritChosenCallback;
        PlayerReligionManager.TutorialBypassAcceptChecks = false;
        ShowPart(_currentPart + 1);
    }

    private void OnTradePanelOpened()
    {
        if (!_waitingForTradePanelOpen) return;
        _waitingForTradePanelOpen = false;
        if (_tradePanel != null) _tradePanel.OnOpen -= OnTradePanelOpened;
        ShowPart(_currentPart + 1);
    }

    private void OnTraderPanelOpened()
    {
        if (!_waitingForTraderPanelOpen) return;
        _waitingForTraderPanelOpen = false;
        if (_traderPanel != null) _traderPanel.OnOpen -= OnTraderPanelOpened;
        TraderPanelControl.TutorialShowAllOfferings = false;
        ShowPart(_currentPart + 1);
    }

    private void OnOfferingPanelOpened()
    {
        if (!_waitingForOfferingPanelOpen) return;
        _waitingForOfferingPanelOpen = false;
        if (_offeringPanel != null) _offeringPanel.OnOpen -= OnOfferingPanelOpened;
        ShowPart(_currentPart + 1);
    }

    private void OnPlayerOfferAdded()
    {
        if (!_waitingForPlayerOffer) return;
        _waitingForPlayerOffer = false;
        if (_offeringPanel != null) _offeringPanel.OnPlayerOfferAdded -= OnPlayerOfferAdded;
        ShowPart(_currentPart + 1);
    }

    private void OnTradeAccepted()
    {
        if (!_waitingForTradeAccepted) return;
        _waitingForTradeAccepted = false;
        if (_offeringPanel != null) _offeringPanel.OnTradeAccepted -= OnTradeAccepted;
        ShowPart(_currentPart + 1);
    }

    private void OnTraderPanelClosed()
    {
        if (!_waitingForTraderPanelClose) return;
        _waitingForTraderPanelClose = false;
        if (_traderPanel != null) _traderPanel.OnClose -= OnTraderPanelClosed;
        ShowPart(_currentPart + 1);
    }

    private void OnTradePanelClosed()
    {
        if (!_waitingForTradePanelClose) return;
        _waitingForTradePanelClose = false;
        if (_tradePanel != null) _tradePanel.OnClose -= OnTradePanelClosed;
        ShowPart(_currentPart + 1);
    }

    private void OnProductionPanelOpened()
    {
        if (!_waitingForProductionPanelOpen) return;
        _waitingForProductionPanelOpen = false;
        if (_productionPanel != null) _productionPanel.OnOpen -= OnProductionPanelOpened;
        ShowPart(_currentPart + 1);
    }

    private void OnProductionRunningPanelOpened()
    {
        if (!_waitingForProductionRunningPanelOpen) return;
        _waitingForProductionRunningPanelOpen = false;
        if (_productionRunningPanel != null) _productionRunningPanel.OnOpen -= OnProductionRunningPanelOpened;
        ShowPart(_currentPart + 1);
    }

    private void OnProductionRunningPanelClosed()
    {
        if (!_waitingForProductionRunningPanelClose) return;
        _waitingForProductionRunningPanelClose = false;
        if (_productionRunningPanel != null) _productionRunningPanel.OnClose -= OnProductionRunningPanelClosed;
        ProductionBuildingPanelControl.TutorialShowAllPlans = false;
        ShowPart(_currentPart + 1);
    }

    private void OnProductionPlanStarted()
    {
        if (!_waitingForProductionStart) return;
        _waitingForProductionStart = false;
        ProductionPlanItem.TutorialBypassCosts = false;
        if (_tutorialProductionItem != null) _tutorialProductionItem.OnProductionStarted -= OnProductionPlanStarted;
        ShowPart(_currentPart + 1);
    }

    private void OnProductionSelectionCompleted(ProductionBuildingControl building, ProductionPlan plan)
    {
        if (!_waitingForProductionTargets) return;
        _waitingForProductionTargets = false;
        ProductionSelectionController.OnSelectionCompleted -= OnProductionSelectionCompleted;
        ShowPart(_currentPart + 1);
    }

    private void OnBuildingTypeSwitched()
    {
        if (!_waitingForBuildingTypeSwitch) return;
        _waitingForBuildingTypeSwitch = false;
        if (_buildingPanel != null) _buildingPanel.OnBuildingTypeSwitched -= OnBuildingTypeSwitched;
        ShowPart(_currentPart + 1);
    }

    private void OnCraftingPanelOpened()
    {
        if (!_waitingForCraftingPanelOpen) return;
        _waitingForCraftingPanelOpen = false;
        if (_craftingPanel != null) _craftingPanel.OnOpen -= OnCraftingPanelOpened;
        ShowPart(_currentPart + 1);
    }

    private void OnCraftingPanelClosed()
    {
        if (!_waitingForCraftingPanelClose) return;
        _waitingForCraftingPanelClose = false;
        if (_craftingPanel != null) _craftingPanel.OnClose -= OnCraftingPanelClosed;
        CraftingBuildingPanelControl.TutorialShowAllRecipes = false;
        ShowPart(_currentPart + 1);
    }

    private void OnCraftingCostPanelShown()
    {
        if (!_waitingForCraftingCostPanel) return;
        _waitingForCraftingCostPanel = false;
        if (_tutorialCraftingItem != null) _tutorialCraftingItem.OnCostsPanelShown -= OnCraftingCostPanelShown;
        ShowPart(_currentPart + 1);
    }

    private void OnCraftingOutputViewShown()
    {
        if (!_waitingForCraftingOutputView) return;
        _waitingForCraftingOutputView = false;
        if (_tutorialCraftingItem != null) _tutorialCraftingItem.OnOutputViewShown -= OnCraftingOutputViewShown;
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

    private void OnRepairPanelOpened()
    {
        if (!_waitingForRepairPanelOpen) return;
        _waitingForRepairPanelOpen = false;
        if (_repairPanel != null) _repairPanel.OnOpen -= OnRepairPanelOpened;
        ShowPart(_currentPart + 1);
    }

    private void OnFullRepairTierClicked()
    {
        if (!_waitingForFullRepairTier) return;
        _waitingForFullRepairTier = false;
        if (_repairPanel != null) _repairPanel.OnFullTierClicked -= OnFullRepairTierClicked;
        ShowPart(_currentPart + 1);
    }

    private void OnRepairStarted(RepairOption opt, int totalTurns)
    {
        if (!_waitingForRepairStart) return;
        _waitingForRepairStart = false;
        BuildingRepair.TutorialBypassCosts = false;
        if (_placedBuildingRepair != null) _placedBuildingRepair.OnRepairStarted -= OnRepairStarted;

        _repairPanel?.Close();
        _damagedPanel?.Hide();

        ShowPart(_currentPart + 1);
    }

    private void OnRepairAndDamagedPanelsClosed()
    {
        if (!_waitingForRepairAndDamagedClose) return;
        _waitingForRepairAndDamagedClose = false;
        if (_damagedPanel != null) _damagedPanel.OnClose -= OnRepairAndDamagedPanelsClosed;
        ShowPart(_currentPart + 1);
    }

    private void OnResearchPanelOpened()
    {
        if (!_waitingForResearchPanelOpen) return;
        _waitingForResearchPanelOpen = false;
        if (_researchPanel != null) _researchPanel.OnOpen -= OnResearchPanelOpened;
        TileInteraction.ClearTutorialAllowedTile();
        ShowPart(_currentPart + 1);
    }

    private void OnResearchNeedsPanelShown()
    {
        if (!_waitingForResearchNeedsPanelOpen) return;
        _waitingForResearchNeedsPanelOpen = false;
        if (_trackedTechItem != null) _trackedTechItem.OnNeedsPanelShown -= OnResearchNeedsPanelShown;
        _trackedTechItem = null;
        ShowPart(_currentPart + 1);
    }

    private void OnResearchNeedsPanelHidden()
    {
        if (!_waitingForResearchNeedsPanelClose) return;
        _waitingForResearchNeedsPanelClose = false;
        if (_trackedTechItem != null) _trackedTechItem.OnNeedsPanelHidden -= OnResearchNeedsPanelHidden;
        _trackedTechItem = null;
        ShowPart(_currentPart + 1);
    }

    private void OnResearchPanelClosed()
    {
        if (!_waitingForResearchPanelClose) return;
        _waitingForResearchPanelClose = false;
        if (_researchPanel != null) _researchPanel.OnClose -= OnResearchPanelClosed;
        ShowPart(_currentPart + 1);
    }

    private void OnLevelInfoPanelOpened()
    {
        if (!_waitingForLevelInfoPanelOpen) return;
        _waitingForLevelInfoPanelOpen = false;
        if (_techPanel != null) _techPanel.OnOpen -= OnLevelInfoPanelOpened;
        ShowPart(_currentPart + 1);
    }

    private void OnLevelInfoPanelClosed()
    {
        if (!_waitingForLevelInfoPanelClose) return;
        _waitingForLevelInfoPanelClose = false;
        if (_techPanel != null) _techPanel.OnClose -= OnLevelInfoPanelClosed;
        TechPanelControl.TutorialShowAll = false;
        ShowPart(_currentPart + 1);
    }

    private IEnumerator FastForwardRepairCoroutine(BuildingRepair repair)
    {
        while (repair != null && repair.IsRepairing)
        {
            if (TurnSystem.Instance != null)
            {
                yield return TurnSystem.Instance.StartCoroutine(
                    TurnSystem.Instance.RunGhostPhaseAdvance(() => repair.TurnTick())
                );
            }
            else
            {
                repair.TurnTick();
                yield return null;
            }
        }

        _fastForwardRepairRoutine = null;
        ShowPart(_currentPart + 1);
    }

    private IEnumerator FastForwardRitualCoroutine(ReligiousBuildingControl control)
    {
        while (control != null && control.HasActiveRitual)
        {
            if (TurnSystem.Instance != null)
            {
                yield return TurnSystem.Instance.StartCoroutine(
                    TurnSystem.Instance.RunGhostPhaseAdvance(() => control.TurnTick())
                );
            }
            else
            {
                control.TurnTick();
                yield return null;
            }
        }

        _fastForwardRitualRoutine = null;
        ReligiousRitualPanelControl.TutorialShowOnlySummoningRitual = false;
        ReligiousBuildingControl.TutorialBypassChecks = false;
        PlayerRitualManager.TutorialBypassSpiritFilter = false;

        // If EnqueueSummoningChoice succeeded but the panel ref was null, retry now
        if (_summoningOfferPanel == null)
            _summoningOfferPanel = FindFirstObjectByType<SummoningSpiritOfferPanelControl>(FindObjectsInactive.Include);
        PlayerRitualManager mgr = PlayerRitualManager.Instance;
        if (mgr != null && mgr.HasActiveChoice && _summoningOfferPanel != null && !_summoningOfferPanel.IsShowing)
            _summoningOfferPanel.OpenFor(mgr.ActiveChoice);

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

        if (_waitingForSecondBuildingPanel)
        {
            _waitingForSecondBuildingPanel = false;
            var ti = TileInteraction.GetInstance();
            if (ti != null) ti.OnTileSelected -= OnSecondBuildingTileSelectedForPanel;
            TileInteraction.ClearTutorialAllowedTile();
        }

        if (_waitingForThirdBuildingPanel)
        {
            _waitingForThirdBuildingPanel = false;
            var ti = TileInteraction.GetInstance();
            if (ti != null) ti.OnTileSelected -= OnThirdBuildingTileSelected;
            TileInteraction.ClearTutorialAllowedTile();
        }

        if (_waitingForFourthBuildingPanel)
        {
            _waitingForFourthBuildingPanel = false;
            var ti = TileInteraction.GetInstance();
            if (ti != null) ti.OnTileSelected -= OnFourthBuildingTileSelected;
            TileInteraction.ClearTutorialAllowedTile();
        }

        if (_waitingForFifthBuildingPanel)
        {
            _waitingForFifthBuildingPanel = false;
            var ti = TileInteraction.GetInstance();
            if (ti != null) ti.OnTileSelected -= OnFifthBuildingTileSelected;
            TileInteraction.ClearTutorialAllowedTile();
        }

        if (_waitingForSixthBuildingPanel)
        {
            _waitingForSixthBuildingPanel = false;
            var ti = TileInteraction.GetInstance();
            if (ti != null) ti.OnTileSelected -= OnSixthBuildingTileSelected;
            TileInteraction.ClearTutorialAllowedTile();
        }

        if (_waitingForSeventhBuildingPanel)
        {
            _waitingForSeventhBuildingPanel = false;
            var ti = TileInteraction.GetInstance();
            if (ti != null) ti.OnTileSelected -= OnSeventhBuildingTileSelected;
            TileInteraction.ClearTutorialAllowedTile();
        }

        if (_waitingForReligiousPanelOpen && _religiousPanel != null)
        {
            _religiousPanel.OnOpen -= OnReligiousPanelOpened;
            _waitingForReligiousPanelOpen = false;
        }

        ReligiousRitualPanelControl.TutorialShowOnlySummoningRitual = false;
        ReligiousBuildingControl.TutorialBypassChecks = false;
        PlayerRitualManager.TutorialBypassSpiritFilter = false;

        if (_waitingForRitualPanelOpen && _ritualPanel != null)
        {
            _ritualPanel.OnOpen -= OnRitualPanelOpened;
            _waitingForRitualPanelOpen = false;
        }

        if (_waitingForRitualStart && _ritualBuildingControl != null)
        {
            _ritualBuildingControl.OnRitualStarted -= OnRitualStarted;
            _waitingForRitualStart = false;
        }

        if (_fastForwardRitualRoutine != null)
        {
            StopCoroutine(_fastForwardRitualRoutine);
            _fastForwardRitualRoutine = null;
        }

        if (_waitingForSummoningPanelOpen && _summoningOfferPanel != null)
        {
            _summoningOfferPanel.OnOpen -= OnSummoningPanelOpened;
            _waitingForSummoningPanelOpen = false;
        }

        if (_waitingForSpiritChosen && _summoningOfferPanel != null)
        {
            _summoningOfferPanel.OnSpiritChosen -= OnSpiritChosenCallback;
            _waitingForSpiritChosen = false;
        }

        PlayerReligionManager.TutorialBypassAcceptChecks = false;

        if (_waitingForTradePanelOpen && _tradePanel != null)
        {
            _tradePanel.OnOpen -= OnTradePanelOpened;
            _waitingForTradePanelOpen = false;
        }

        if (_waitingForTraderPanelOpen && _traderPanel != null)
        {
            _traderPanel.OnOpen -= OnTraderPanelOpened;
            _waitingForTraderPanelOpen = false;
        }

        TraderPanelControl.TutorialShowAllOfferings = false;

        if (_waitingForOfferingPanelOpen && _offeringPanel != null)
        {
            _offeringPanel.OnOpen -= OnOfferingPanelOpened;
            _waitingForOfferingPanelOpen = false;
        }

        if (_waitingForPlayerOffer && _offeringPanel != null)
        {
            _offeringPanel.OnPlayerOfferAdded -= OnPlayerOfferAdded;
            _waitingForPlayerOffer = false;
        }

        if (_waitingForTradeAccepted && _offeringPanel != null)
        {
            _offeringPanel.OnTradeAccepted -= OnTradeAccepted;
            _waitingForTradeAccepted = false;
        }

        if (_waitingForTraderPanelClose && _traderPanel != null)
        {
            _traderPanel.OnClose -= OnTraderPanelClosed;
            _waitingForTraderPanelClose = false;
        }

        if (_waitingForTradePanelClose && _tradePanel != null)
        {
            _tradePanel.OnClose -= OnTradePanelClosed;
            _waitingForTradePanelClose = false;
        }

        if (_waitingForProductionPanelOpen && _productionPanel != null)
        {
            _productionPanel.OnOpen -= OnProductionPanelOpened;
            _waitingForProductionPanelOpen = false;
        }

        if (_waitingForProductionRunningPanelOpen && _productionRunningPanel != null)
        {
            _productionRunningPanel.OnOpen -= OnProductionRunningPanelOpened;
            _waitingForProductionRunningPanelOpen = false;
        }

        if (_waitingForProductionRunningPanelClose && _productionRunningPanel != null)
        {
            _productionRunningPanel.OnClose -= OnProductionRunningPanelClosed;
            _waitingForProductionRunningPanelClose = false;
        }

        if (_waitingForProductionStart && _tutorialProductionItem != null)
        {
            _tutorialProductionItem.OnProductionStarted -= OnProductionPlanStarted;
            _waitingForProductionStart = false;
        }
        ProductionPlanItem.TutorialBypassCosts = false;

        if (_waitingForProductionTargets)
        {
            ProductionSelectionController.OnSelectionCompleted -= OnProductionSelectionCompleted;
            _waitingForProductionTargets = false;
        }

        ProductionBuildingPanelControl.TutorialShowAllPlans = false;

        if (_waitingForBuildingTypeSwitch && _buildingPanel != null)
        {
            _buildingPanel.OnBuildingTypeSwitched -= OnBuildingTypeSwitched;
            _waitingForBuildingTypeSwitch = false;
        }

        if (_waitingForCraftingPanelOpen && _craftingPanel != null)
        {
            _craftingPanel.OnOpen -= OnCraftingPanelOpened;
            _waitingForCraftingPanelOpen = false;
        }

        if (_waitingForCraftingPanelClose && _craftingPanel != null)
        {
            _craftingPanel.OnClose -= OnCraftingPanelClosed;
            _waitingForCraftingPanelClose = false;
        }

        if (_waitingForCraftingCostPanel && _tutorialCraftingItem != null)
        {
            _tutorialCraftingItem.OnCostsPanelShown -= OnCraftingCostPanelShown;
            _waitingForCraftingCostPanel = false;
        }

        if (_waitingForCraftingOutputView && _tutorialCraftingItem != null)
        {
            _tutorialCraftingItem.OnOutputViewShown -= OnCraftingOutputViewShown;
            _waitingForCraftingOutputView = false;
        }

        CraftingBuildingPanelControl.TutorialShowAllRecipes = false;

        _waitingForStoragePanelOpen = false;

        if (_waitingForStoragePanelClose && _storagePanel != null)
        {
            _storagePanel.OnClose -= OnStoragePanelClosed;
            _waitingForStoragePanelClose = false;
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

        if (_waitingForRepairPanelOpen && _repairPanel != null)
        {
            _repairPanel.OnOpen -= OnRepairPanelOpened;
            _waitingForRepairPanelOpen = false;
        }

        if (_waitingForFullRepairTier && _repairPanel != null)
        {
            _repairPanel.OnFullTierClicked -= OnFullRepairTierClicked;
            _waitingForFullRepairTier = false;
            BuildingRepair.TutorialBypassCosts = false;
        }

        if (_waitingForRepairStart && _placedBuildingRepair != null)
        {
            _placedBuildingRepair.OnRepairStarted -= OnRepairStarted;
            _waitingForRepairStart = false;
            BuildingRepair.TutorialBypassCosts = false;
        }

        if (_waitingForRepairAndDamagedClose && _damagedPanel != null)
        {
            _damagedPanel.OnClose -= OnRepairAndDamagedPanelsClosed;
            _waitingForRepairAndDamagedClose = false;
        }

        if (_fastForwardRepairRoutine != null)
        {
            StopCoroutine(_fastForwardRepairRoutine);
            _fastForwardRepairRoutine = null;
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

        if (_waitingForResearchPanelOpen && _researchPanel != null)
        {
            _researchPanel.OnOpen -= OnResearchPanelOpened;
            _waitingForResearchPanelOpen = false;
            ResearchPanelControl.TutorialShowAllTech = false;
            TileInteraction.ClearTutorialAllowedTile();
        }

        if (_waitingForResearchNeedsPanelOpen && _trackedTechItem != null)
        {
            _trackedTechItem.OnNeedsPanelShown -= OnResearchNeedsPanelShown;
            _waitingForResearchNeedsPanelOpen = false;
            _trackedTechItem = null;
        }

        if (_waitingForResearchNeedsPanelClose && _trackedTechItem != null)
        {
            _trackedTechItem.OnNeedsPanelHidden -= OnResearchNeedsPanelHidden;
            _waitingForResearchNeedsPanelClose = false;
            _trackedTechItem = null;
        }

        if (_waitingForResearchPanelClose && _researchPanel != null)
        {
            _researchPanel.OnClose -= OnResearchPanelClosed;
            _waitingForResearchPanelClose = false;
        }

        if (_waitingForLevelInfoPanelOpen && _techPanel != null)
        {
            _techPanel.OnOpen -= OnLevelInfoPanelOpened;
            _waitingForLevelInfoPanelOpen = false;
            TechPanelControl.TutorialShowAll = false;
        }

        if (_waitingForLevelInfoPanelClose && _techPanel != null)
        {
            _techPanel.OnClose -= OnLevelInfoPanelClosed;
            _waitingForLevelInfoPanelClose = false;
            TechPanelControl.TutorialShowAll = false;
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
