using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryTutorial : MonoBehaviour
{
    private enum TutorialStep
    {
        OpenInventoryPanel,
        ExplainFilters,
        ExplainWeight,
        ExplainGeneral,
        ExplainSpoilage,
        CloseInventoryPanel
    }

    [Header("UI Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup rootCanvasGroup;

    [Header("Overlays")]
    [SerializeField] private GameObject darkOverlayWithHole;
    [SerializeField] private GameObject darkOverlayWithHole2;
    [SerializeField] private GameObject darkOverlayWithHole3;
    [SerializeField] private GameObject darkOverlay;
    [SerializeField] private GameObject darkOverlay2;

    [Header("Primary Message UI")]
    [SerializeField] private GameObject messagePanel;
    [SerializeField] private TMP_Text messageText;

    [Header("Spoilage Message UI")]
    [SerializeField] private GameObject spoilageMessagePanel;
    [SerializeField] private TMP_Text spoilageMessageText;

    [Header("Buttons")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button skipButton;

    [Header("References")]
    [SerializeField] private InventoryPanelControl inventoryPanel;
    [SerializeField] private CameraControl cameraControl;
    [SerializeField] private TileInteraction tileInteraction;

    [Header("Messages")]
    [SerializeField]
    private string openInventoryMessage =
        "Open the inventory panel.";

    [SerializeField]
    private string filterMessage =
        "These filter buttons let you switch between food, water, and materials.";

    [SerializeField]
    private string weightMessage =
        "Your inventory fills and empties based on item weight.";

    [SerializeField]
    private string generalMessage =
        "This is where you keep track of your stored resources.";

    [SerializeField]
    private string spoilageMessage =
        "When food spoils it can build up in your inventory. You need to remove it, because if it is the only food left your population may eat it and get sick.";

    [SerializeField]
    private string closeInventoryMessage =
        "Now close the inventory panel.";

    [Header("Settings")]
    [SerializeField] private bool resumeTurnTimerWhenFinished = true;

    private bool _running;
    private bool _completedThisGame;
    private bool _cameraLockedByTutorial;
    private TutorialStep _step = TutorialStep.OpenInventoryPanel;

    public bool IsRunning => _running;
    public bool HasCompletedTutorial => _completedThisGame;

    [SerializeField] private PopulationStatisticsTutorial populationStatisticsTutorial;

    public bool ShouldRunTutorial()
    {
        return !_running && !_completedThisGame;
    }

    private void Awake()
    {
        if (inventoryPanel == null)
            inventoryPanel = FindObjectOfType<InventoryPanelControl>(true);

        if (cameraControl == null)
            cameraControl = FindObjectOfType<CameraControl>(true);

        if (tileInteraction == null)
            tileInteraction = FindObjectOfType<TileInteraction>(true);

        BindButtons();

        SetRootVisible(false);
        SetBlockingMode(false);
        SetOverlayState(false, false, false, false, false);
        SetContinueButtonVisible(false);
        SetSkipButtonVisible(false);
        ShowPrimaryMessagePanel(false);
        ShowSpoilageMessagePanel(false);
    }

    public void InstallRuntimeRefs(
        InventoryPanelControl newInventoryPanel = null,
        CameraControl newCameraControl = null,
        TileInteraction newTileInteraction = null,
        PopulationStatisticsTutorial newPopulationStatisticsTutorial = null)
    {
        if (newInventoryPanel != null)
            inventoryPanel = newInventoryPanel;
        else if (inventoryPanel == null)
            inventoryPanel = FindObjectOfType<InventoryPanelControl>(true);

        if (newCameraControl != null)
            cameraControl = newCameraControl;
        else if (cameraControl == null)
            cameraControl = FindObjectOfType<CameraControl>(true);

        if (newTileInteraction != null)
            tileInteraction = newTileInteraction;
        else if (tileInteraction == null)
            tileInteraction = FindObjectOfType<TileInteraction>(true);

        if (newPopulationStatisticsTutorial != null)
            populationStatisticsTutorial = newPopulationStatisticsTutorial;

        BindButtons();
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
        _step = TutorialStep.OpenInventoryPanel;

        TurnSystem.Instance?.PauseTurnTimer();

        if (cameraControl != null && !_cameraLockedByTutorial)
        {
            cameraControl.PushInputLock();
            _cameraLockedByTutorial = true;
        }

        TileInteraction.SetSelectionEnabled(false);

        SetRootVisible(true);
        SetBlockingMode(false);
        SetOverlayState(
            showOpenHole: true,
            showFiltersHole: false,
            showWeightHole: false,
            showGeneralDark: false,
            showSpoilageDark: false
        );

        SetContinueButtonVisible(false);
        SetSkipButtonVisible(false);
        SetMessage(openInventoryMessage);
    }

    private void Update()
    {
        if (!_running)
            return;

        switch (_step)
        {
            case TutorialStep.OpenInventoryPanel:
                {
                    if (IsInventoryPanelShowing())
                    {
                        _step = TutorialStep.ExplainFilters;

                        SetOverlayState(
                            showOpenHole: false,
                            showFiltersHole: true,
                            showWeightHole: false,
                            showGeneralDark: false,
                            showSpoilageDark: false
                        );

                        SetContinueButtonVisible(true);
                        SetSkipButtonVisible(false);
                        SetMessage(filterMessage);
                    }

                    break;
                }

            case TutorialStep.CloseInventoryPanel:
                {
                    if (!IsInventoryPanelShowing())
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
            case TutorialStep.ExplainFilters:
                {
                    _step = TutorialStep.ExplainWeight;

                    SetOverlayState(
                        showOpenHole: false,
                        showFiltersHole: false,
                        showWeightHole: true,
                        showGeneralDark: false,
                        showSpoilageDark: false
                    );

                    SetContinueButtonVisible(true);
                    SetMessage(weightMessage);
                    break;
                }

            case TutorialStep.ExplainWeight:
                {
                    _step = TutorialStep.ExplainGeneral;

                    SetOverlayState(
                        showOpenHole: false,
                        showFiltersHole: false,
                        showWeightHole: false,
                        showGeneralDark: true,
                        showSpoilageDark: false
                    );

                    SetContinueButtonVisible(true);
                    SetMessage(generalMessage);
                    break;
                }

            case TutorialStep.ExplainGeneral:
                {
                    _step = TutorialStep.ExplainSpoilage;

                    SetOverlayState(
                        showOpenHole: false,
                        showFiltersHole: false,
                        showWeightHole: false,
                        showGeneralDark: false,
                        showSpoilageDark: true
                    );

                    SetContinueButtonVisible(true);
                    SetSpoilageMessage(spoilageMessage);
                    break;
                }

            case TutorialStep.ExplainSpoilage:
                {
                    _step = TutorialStep.CloseInventoryPanel;

                    SetOverlayState(
                        showOpenHole: false,
                        showFiltersHole: false,
                        showWeightHole: false,
                        showGeneralDark: false,
                        showSpoilageDark: false
                    );

                    SetContinueButtonVisible(false);
                    SetMessage(closeInventoryMessage);
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

        SetOverlayState(false, false, false, false, false);
        SetContinueButtonVisible(false);
        SetSkipButtonVisible(false);
        SetBlockingMode(false);
        SetRootVisible(false);
        ShowPrimaryMessagePanel(false);
        ShowSpoilageMessagePanel(false);

        if (_cameraLockedByTutorial && cameraControl != null)
        {
            cameraControl.PopInputLock();
            _cameraLockedByTutorial = false;
        }

        TileInteraction.SetSelectionEnabled(false);
        tileInteraction?.EnableSelectionAfter(0.01f);

        BeginNextTutorialOrResume();
    }

    private void BeginNextTutorialOrResume()
    {
        if (populationStatisticsTutorial != null && populationStatisticsTutorial.ShouldRunTutorial())
        {
            populationStatisticsTutorial.BeginTutorial();
            return;
        }

        if (resumeTurnTimerWhenFinished)
            TurnSystem.Instance?.ResumeTurnTimer();
    }

    public void ResetTutorialForNewGame()
    {
        _running = false;
        _completedThisGame = false;
        _step = TutorialStep.OpenInventoryPanel;

        SetOverlayState(false, false, false, false, false);
        SetContinueButtonVisible(false);
        SetSkipButtonVisible(false);
        SetBlockingMode(false);
        SetRootVisible(false);
        ShowPrimaryMessagePanel(false);
        ShowSpoilageMessagePanel(false);

        if (_cameraLockedByTutorial && cameraControl != null)
        {
            cameraControl.PopInputLock();
            _cameraLockedByTutorial = false;
        }

        TileInteraction.SetSelectionEnabled(false);
        tileInteraction?.EnableSelectionAfter(0.01f);
    }

    private bool IsInventoryPanelShowing()
    {
        if (inventoryPanel == null)
            return false;

        FieldInfo rootField = inventoryPanel.GetType().GetField(
            "root",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
        );

        if (rootField != null)
        {
            GameObject rootObj = rootField.GetValue(inventoryPanel) as GameObject;
            if (rootObj != null)
                return rootObj.activeInHierarchy;
        }

        PropertyInfo isShowingProp = inventoryPanel.GetType().GetProperty(
            "IsShowing",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
        );

        if (isShowingProp != null && isShowingProp.PropertyType == typeof(bool))
            return (bool)isShowingProp.GetValue(inventoryPanel);

        return inventoryPanel.gameObject.activeInHierarchy;
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

        ShowPrimaryMessagePanel(true);
        ShowSpoilageMessagePanel(false);
    }

    private void SetSpoilageMessage(string value)
    {
        if (spoilageMessageText != null)
            spoilageMessageText.text = value;

        ShowPrimaryMessagePanel(false);
        ShowSpoilageMessagePanel(true);
    }

    private void ShowPrimaryMessagePanel(bool visible)
    {
        if (messagePanel != null)
            messagePanel.SetActive(visible);
    }

    private void ShowSpoilageMessagePanel(bool visible)
    {
        if (spoilageMessagePanel != null)
            spoilageMessagePanel.SetActive(visible);
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

    private void SetOverlayState(
        bool showOpenHole,
        bool showFiltersHole,
        bool showWeightHole,
        bool showGeneralDark,
        bool showSpoilageDark)
    {
        if (darkOverlayWithHole != null)
            darkOverlayWithHole.SetActive(showOpenHole);

        if (darkOverlayWithHole2 != null)
            darkOverlayWithHole2.SetActive(showFiltersHole);

        if (darkOverlayWithHole3 != null)
            darkOverlayWithHole3.SetActive(showWeightHole);

        if (darkOverlay != null)
            darkOverlay.SetActive(showGeneralDark);

        if (darkOverlay2 != null)
            darkOverlay2.SetActive(showSpoilageDark);
    }

    private void SetSkipButtonVisible(bool visible)
    {
        if (skipButton == null)
            return;

        skipButton.gameObject.SetActive(visible);
        skipButton.interactable = visible;

        if (visible)
            skipButton.transform.SetAsLastSibling();
    }

    private void SetContinueButtonVisible(bool visible)
    {
        if (continueButton == null)
            return;

        continueButton.gameObject.SetActive(visible);
        continueButton.interactable = visible;

        if (visible)
            continueButton.transform.SetAsLastSibling();

        if (visible)
            SetBlockingMode(true);
        else
            SetBlockingMode(false);
    }
}