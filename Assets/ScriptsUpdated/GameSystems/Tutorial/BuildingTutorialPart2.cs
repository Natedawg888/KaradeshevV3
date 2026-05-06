using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildingTutorialPart2 : MonoBehaviour
{
    private enum TutorialStep
    {
        SelectBuiltBuilding,
        OpenShelterPanel,
        ExplainFamilies,
        ExplainPausePairing,
        ExplainMoveFamily,
        CloseShelterPanel,

        ExplainModeSwitch,
        SwitchToStorage,

        OpenStoragePanel,
        ExplainStoragePanel,
        CloseStoragePanel,

        OpenRepairPanel,
        ExplainRepairPanel,
        CloseRepairPanel,

        OpenResearchPanel,
        ExplainResearchPanel,
        CloseResearchPanel,

        CloseBuildingPanel
    }

    [Header("UI Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup rootCanvasGroup;

    [Header("Overlays")]
    [SerializeField] private GameObject darkOverlayWithHole;
    [SerializeField] private GameObject darkOverlayWithHole2;
    [SerializeField] private GameObject darkOverlayWithHole3;
    [SerializeField] private GameObject darkOverlayWithHole4;
    [SerializeField] private GameObject darkOverlayWithHole5;
    [SerializeField] private GameObject darkOverlayWithHole6;

    [SerializeField] private GameObject darkOverlay;
    [SerializeField] private GameObject darkOverlayWithHole7;
    [SerializeField] private GameObject darkOverlay2;
    [SerializeField] private GameObject darkOverlayWithHole8;
    [SerializeField] private GameObject darkOverlay3;

    [Header("Message UI")]
    [SerializeField] private GameObject messagePanel;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button skipButton;

    [Header("References")]
    [SerializeField] private BuildingPanelControl buildingPanel;
    [SerializeField] private ShelterPanelControl shelterPanel;
    [SerializeField] private StoragePanelControl storagePanel;
    [SerializeField] private RepairPanelControl repairPanel;
    [SerializeField] private ResearchPanelControl researchPanel;
    [SerializeField] private CameraControl cameraControl;
    [SerializeField] private TileInteraction tileInteraction;

    [Header("Messages")]
    [SerializeField]
    private string selectBuildingMessage =
        "Open the building panel again.";

    [SerializeField]
    private string openShelterMessage =
        "Open the shelter panel.";

    [SerializeField]
    private string familiesMessage =
        "These are the families of your civilization.";

    [SerializeField]
    private string pausePairingMessage =
        "Use this button to stop pairing in this building. That will stop population growth.";

    [SerializeField]
    private string moveFamilyMessage =
        "This button is used to move a family to a new shelter.";

    [SerializeField]
    private string closeShelterMessage =
        "Now close the shelter panel.";

    [SerializeField]
    private string modeSwitchMessage =
        "These buttons are used to switch the use of the building between shelter and storage.";

    [SerializeField]
    private string switchToStorageMessage =
        "Now switch this building to storage.";

    [SerializeField]
    private string openStorageMessage =
        "Open the storage building.";

    [SerializeField]
    private string storageExplanationMessage =
        "This is the storage building panel.";

    [SerializeField]
    private string closeStorageMessage =
        "Now close the storage building.";

    [SerializeField]
    private string openRepairMessage =
        "Open the repair panel.";

    [SerializeField]
    private string repairExplanationMessage =
        "This is the repair panel.";

    [SerializeField]
    private string closeRepairMessage =
        "Now close the repair panel.";

    [SerializeField]
    private string openResearchMessage =
        "Open the research panel.";

    [SerializeField]
    private string researchExplanationMessage =
        "This is the research panel.";

    [SerializeField]
    private string closeResearchMessage =
        "Now close the research panel.";

    [SerializeField]
    private string closeBuildingPanelMessage =
        "Now close the building panel.";

    [Header("Settings")]
    [SerializeField] private bool resumeTurnTimerWhenFinished = true;

    private bool _running;
    private bool _completedThisGame;
    private bool _cameraLockedByTutorial;
    private TutorialStep _step = TutorialStep.SelectBuiltBuilding;
    private BuildingControl _targetBuilding;

    public bool IsRunning => _running;
    public bool HasCompletedTutorial => _completedThisGame;

    public bool ShouldRunTutorial()
    {
        return !_running && !_completedThisGame && _targetBuilding != null;
    }

    private void Awake()
    {
        if (buildingPanel == null)
            buildingPanel = FindObjectOfType<BuildingPanelControl>(true);

        if (shelterPanel == null && buildingPanel != null)
            shelterPanel = buildingPanel.shelterPanel;

        if (storagePanel == null && buildingPanel != null)
            storagePanel = buildingPanel.storagePanel;

        if (repairPanel == null)
            repairPanel = FindObjectOfType<RepairPanelControl>(true);

        if (researchPanel == null)
            researchPanel = FindObjectOfType<ResearchPanelControl>(true);

        if (cameraControl == null)
            cameraControl = FindObjectOfType<CameraControl>(true);

        if (tileInteraction == null)
            tileInteraction = FindObjectOfType<TileInteraction>(true);

        BindButtons();
        SetRootVisible(false);
        SetBlockingMode(false);
        SetAllOverlaysOff();
        ShowMessagePanel(false);
        SetContinueButtonVisible(false);
        SetSkipButtonVisible(false);
    }

    public void InstallRuntimeRefs(
        BuildingPanelControl newBuildingPanel = null,
        ShelterPanelControl newShelterPanel = null,
        StoragePanelControl newStoragePanel = null,
        CameraControl newCameraControl = null,
        TileInteraction newTileInteraction = null)
    {
        if (newBuildingPanel != null)
            buildingPanel = newBuildingPanel;
        else if (buildingPanel == null)
            buildingPanel = FindObjectOfType<BuildingPanelControl>(true);

        if (newShelterPanel != null)
            shelterPanel = newShelterPanel;
        else if (shelterPanel == null && buildingPanel != null)
            shelterPanel = buildingPanel.shelterPanel;

        if (newStoragePanel != null)
            storagePanel = newStoragePanel;
        else if (storagePanel == null && buildingPanel != null)
            storagePanel = buildingPanel.storagePanel;

        if (repairPanel == null)
            repairPanel = FindObjectOfType<RepairPanelControl>(true);

        if (researchPanel == null)
            researchPanel = FindObjectOfType<ResearchPanelControl>(true);

        if (newCameraControl != null)
            cameraControl = newCameraControl;
        else if (cameraControl == null)
            cameraControl = FindObjectOfType<CameraControl>(true);

        if (newTileInteraction != null)
            tileInteraction = newTileInteraction;
        else if (tileInteraction == null)
            tileInteraction = FindObjectOfType<TileInteraction>(true);

        BindButtons();
    }

    public void SetTargetBuilding(BuildingControl building)
    {
        _targetBuilding = building;
    }

    public void BeginTutorial()
    {
        if (!ShouldRunTutorial())
        {
            if (resumeTurnTimerWhenFinished)
                TurnSystem.Instance?.ResumeTurnTimer();
            return;
        }

        _running = true;
        _step = TutorialStep.SelectBuiltBuilding;

        TurnSystem.Instance?.PauseTurnTimer();

        if (cameraControl != null && !_cameraLockedByTutorial)
        {
            cameraControl.PushInputLock();
            _cameraLockedByTutorial = true;
        }

        TileInteraction.SetSelectionEnabled(false);
        tileInteraction?.EnableSelectionAfter(0.01f);

        SetRootVisible(true);
        SetBlockingMode(false);
        SetAllOverlaysOff();
        SetContinueButtonVisible(false);
        SetSkipButtonVisible(false);
        SetMessage(selectBuildingMessage);
    }

    private void Update()
    {
        if (!_running)
            return;

        switch (_step)
        {
            case TutorialStep.SelectBuiltBuilding:
                {
                    if (IsTargetBuildingPanelShowing())
                    {
                        _step = TutorialStep.OpenShelterPanel;
                        SetAllOverlaysOff();
                        SetOverlayVisible(darkOverlayWithHole, true);
                        SetMessage(openShelterMessage);
                    }
                    break;
                }

            case TutorialStep.OpenShelterPanel:
                {
                    if (IsShelterPanelShowing())
                    {
                        _step = TutorialStep.ExplainFamilies;
                        SetAllOverlaysOff();
                        SetOverlayVisible(darkOverlayWithHole2, true);
                        SetContinueButtonVisible(true);
                        SetMessage(familiesMessage);
                    }
                    break;
                }

            case TutorialStep.CloseShelterPanel:
                {
                    if (!IsShelterPanelShowing() && IsTargetBuildingPanelShowing())
                    {
                        _step = TutorialStep.ExplainModeSwitch;
                        SetAllOverlaysOff();
                        SetOverlayVisible(darkOverlayWithHole5, true);
                        SetContinueButtonVisible(true);
                        SetMessage(modeSwitchMessage);
                    }
                    break;
                }

            case TutorialStep.SwitchToStorage:
                {
                    if (_targetBuilding != null &&
                        _targetBuilding.ActiveType == BuildingType.Storage &&
                        IsTargetBuildingPanelShowing())
                    {
                        _step = TutorialStep.OpenStoragePanel;
                        SetAllOverlaysOff();
                        SetOverlayVisible(darkOverlayWithHole6, true);
                        SetContinueButtonVisible(false);
                        SetMessage(openStorageMessage);
                    }
                    break;
                }

            case TutorialStep.OpenStoragePanel:
                {
                    if (IsStoragePanelShowing())
                    {
                        _step = TutorialStep.ExplainStoragePanel;
                        SetAllOverlaysOff();
                        SetOverlayVisible(darkOverlay, true);
                        SetContinueButtonVisible(true);
                        SetMessage(storageExplanationMessage);
                    }
                    break;
                }

            case TutorialStep.CloseStoragePanel:
                {
                    if (!IsStoragePanelShowing() && IsTargetBuildingPanelShowing())
                    {
                        _step = TutorialStep.OpenRepairPanel;
                        SetAllOverlaysOff();
                        SetOverlayVisible(darkOverlayWithHole7, true);
                        SetContinueButtonVisible(false);
                        SetMessage(openRepairMessage);
                    }
                    break;
                }

            case TutorialStep.OpenRepairPanel:
                {
                    if (IsRepairPanelShowing())
                    {
                        _step = TutorialStep.ExplainRepairPanel;
                        SetAllOverlaysOff();
                        SetOverlayVisible(darkOverlay2, true);
                        SetContinueButtonVisible(true);
                        SetMessage(repairExplanationMessage);
                    }
                    break;
                }

            case TutorialStep.CloseRepairPanel:
                {
                    if (!IsRepairPanelShowing() && IsTargetBuildingPanelShowing())
                    {
                        _step = TutorialStep.OpenResearchPanel;
                        SetAllOverlaysOff();
                        SetOverlayVisible(darkOverlayWithHole8, true);
                        SetContinueButtonVisible(false);
                        SetMessage(openResearchMessage);
                    }
                    break;
                }

            case TutorialStep.OpenResearchPanel:
                {
                    if (IsResearchPanelShowing())
                    {
                        _step = TutorialStep.ExplainResearchPanel;
                        SetAllOverlaysOff();
                        SetOverlayVisible(darkOverlay3, true);
                        SetContinueButtonVisible(true);
                        SetMessage(researchExplanationMessage);
                    }
                    break;
                }

            case TutorialStep.CloseResearchPanel:
                {
                    if (!IsResearchPanelShowing() && IsTargetBuildingPanelShowing())
                    {
                        _step = TutorialStep.CloseBuildingPanel;
                        SetAllOverlaysOff();
                        SetContinueButtonVisible(false);
                        SetMessage(closeBuildingPanelMessage);
                    }
                    break;
                }

            case TutorialStep.CloseBuildingPanel:
                {
                    if (!IsTargetBuildingPanelShowing())
                        CompleteTutorial();
                    break;
                }
        }
    }

    private void OnContinuePressed()
    {
        if (!_running)
            return;

        switch (_step)
        {
            case TutorialStep.ExplainFamilies:
                {
                    _step = TutorialStep.ExplainPausePairing;
                    SetAllOverlaysOff();
                    SetOverlayVisible(darkOverlayWithHole3, true);
                    SetContinueButtonVisible(true);
                    SetMessage(pausePairingMessage);
                    break;
                }

            case TutorialStep.ExplainPausePairing:
                {
                    _step = TutorialStep.ExplainMoveFamily;
                    SetAllOverlaysOff();
                    SetOverlayVisible(darkOverlayWithHole4, true);
                    SetContinueButtonVisible(true);
                    SetMessage(moveFamilyMessage);
                    break;
                }

            case TutorialStep.ExplainMoveFamily:
                {
                    _step = TutorialStep.CloseShelterPanel;
                    SetAllOverlaysOff();
                    SetContinueButtonVisible(false);
                    SetMessage(closeShelterMessage);
                    break;
                }

            case TutorialStep.ExplainModeSwitch:
                {
                    _step = TutorialStep.SwitchToStorage;
                    SetAllOverlaysOff();
                    SetOverlayVisible(darkOverlayWithHole5, true);
                    SetContinueButtonVisible(false);
                    SetMessage(switchToStorageMessage);
                    break;
                }

            case TutorialStep.ExplainStoragePanel:
                {
                    _step = TutorialStep.CloseStoragePanel;
                    SetAllOverlaysOff();
                    SetContinueButtonVisible(false);
                    SetMessage(closeStorageMessage);
                    break;
                }

            case TutorialStep.ExplainRepairPanel:
                {
                    _step = TutorialStep.CloseRepairPanel;
                    SetAllOverlaysOff();
                    SetContinueButtonVisible(false);
                    SetMessage(closeRepairMessage);
                    break;
                }

            case TutorialStep.ExplainResearchPanel:
                {
                    _step = TutorialStep.CloseResearchPanel;
                    SetAllOverlaysOff();
                    SetContinueButtonVisible(false);
                    SetMessage(closeResearchMessage);
                    break;
                }
        }
    }

    private void OnSkipPressed()
    {
        SkipTutorial();
    }

    public void SkipTutorial()
    {
        FinishTutorial(true);
    }

    private void CompleteTutorial()
    {
        FinishTutorial(true);
    }

    private void FinishTutorial(bool markComplete)
    {
        _running = false;

        if (markComplete)
            _completedThisGame = true;

        SetAllOverlaysOff();
        SetContinueButtonVisible(false);
        SetSkipButtonVisible(false);
        SetBlockingMode(false);
        SetRootVisible(false);
        ShowMessagePanel(false);

        if (_cameraLockedByTutorial && cameraControl != null)
        {
            cameraControl.PopInputLock();
            _cameraLockedByTutorial = false;
        }

        TileInteraction.SetSelectionEnabled(false);
        tileInteraction?.EnableSelectionAfter(0.01f);

        if (resumeTurnTimerWhenFinished)
            TurnSystem.Instance?.ResumeTurnTimer();
    }

    public void ResetTutorialForNewGame()
    {
        _running = false;
        _completedThisGame = false;
        _step = TutorialStep.SelectBuiltBuilding;
        _targetBuilding = null;

        SetAllOverlaysOff();
        SetContinueButtonVisible(false);
        SetSkipButtonVisible(false);
        SetBlockingMode(false);
        SetRootVisible(false);
        ShowMessagePanel(false);

        if (_cameraLockedByTutorial && cameraControl != null)
        {
            cameraControl.PopInputLock();
            _cameraLockedByTutorial = false;
        }
    }

    private bool IsTargetBuildingPanelShowing()
    {
        return buildingPanel != null &&
               buildingPanel.IsShowing &&
               buildingPanel.CurrentBuilding == _targetBuilding;
    }

    private bool IsShelterPanelShowing()
    {
        return shelterPanel != null && shelterPanel.IsShowing;
    }

    private bool IsStoragePanelShowing()
    {
        return storagePanel != null && storagePanel.IsShowing;
    }

    private bool IsRepairPanelShowing()
    {
        return repairPanel != null && repairPanel.IsShowing;
    }

    private bool IsResearchPanelShowing()
    {
        return researchPanel != null && researchPanel.IsShowing;
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

    private void SetMessage(string value)
    {
        if (messageText != null)
            messageText.text = value;

        ShowMessagePanel(true);
    }

    private void ShowMessagePanel(bool visible)
    {
        if (messagePanel != null)
            messagePanel.SetActive(visible);
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

    private void SetAllOverlaysOff()
    {
        SetOverlayVisible(darkOverlayWithHole, false);
        SetOverlayVisible(darkOverlayWithHole2, false);
        SetOverlayVisible(darkOverlayWithHole3, false);
        SetOverlayVisible(darkOverlayWithHole4, false);
        SetOverlayVisible(darkOverlayWithHole5, false);
        SetOverlayVisible(darkOverlayWithHole6, false);
        SetOverlayVisible(darkOverlay, false);
        SetOverlayVisible(darkOverlayWithHole7, false);
        SetOverlayVisible(darkOverlay2, false);
        SetOverlayVisible(darkOverlayWithHole8, false);
        SetOverlayVisible(darkOverlay3, false);
    }

    private void SetOverlayVisible(GameObject overlay, bool visible)
    {
        if (overlay != null)
            overlay.SetActive(visible);
    }

    private void SetContinueButtonVisible(bool visible)
    {
        if (continueButton != null)
            continueButton.gameObject.SetActive(visible);

        SetBlockingMode(visible);
    }

    private void SetSkipButtonVisible(bool visible)
    {
        if (skipButton != null)
            skipButton.gameObject.SetActive(visible);
    }
}