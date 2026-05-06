using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PopulationStatisticsTutorial : MonoBehaviour
{
    private enum TutorialStep
    {
        OpenPopulationPanel,
        ExplainPopulationNumbers,
        ExplainRatios,

        OpenNeedsPanel,
        ExplainNeedsPanel,
        CloseNeedsPanel,

        OpenHealthPanel,
        ExplainHealthPanel,
        CloseHealthPanel,

        OpenStatePanel,
        ExplainStatePanel,
        CloseStatePanel,

        ClosePopulationPanel
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
    [SerializeField] private GameObject darkOverlayWithHole7;

    [SerializeField] private GameObject darkOverlayWithHole8;
    [SerializeField] private GameObject darkOverlayWithHole9;

    [Header("Primary Message UI")]
    [SerializeField] private GameObject messagePanel;
    [SerializeField] private TMP_Text messageText;

    [Header("Ratios Message UI")]
    [SerializeField] private GameObject ratiosMessagePanel;
    [SerializeField] private TMP_Text ratiosMessageText;

    [Header("Buttons")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button skipButton;

    [Header("References")]
    [SerializeField] private PlayerPopulationStatisticsPanelRoot populationStatisticsPanel;
    [SerializeField] private PopCivilisationNeedsPanel civilisationNeedsPanel;
    [SerializeField] private PopCivilisationHealthPanel civilisationHealthPanel;
    [SerializeField] private CivilizationStatePanel civilizationStatePanel;

    [SerializeField] private CameraControl cameraControl;
    [SerializeField] private TileInteraction tileInteraction;

    [Header("Messages")]
    [SerializeField]
    private string openPanelMessage =
        "Open the population statistics panel.";

    [SerializeField]
    private string numbersMessage =
        "These are your population numbers.";

    [SerializeField]
    private string ratiosMessage =
        "Use these to check your population: the gender ratio, the age ratios, and population growth.";

    [SerializeField]
    private string openNeedsMessage =
        "Open the civilisation needs panel.";

    [SerializeField]
    private string explainNeedsMessage =
        "This panel shows the needs of your civilisation.";

    [SerializeField]
    private string closeNeedsMessage =
        "Now close the civilisation needs panel.";

    [SerializeField]
    private string openHealthMessage =
        "Open the civilisation health panel.";

    [SerializeField]
    private string explainHealthMessage =
        "This panel shows the health of your civilisation.";

    [SerializeField]
    private string closeHealthMessage =
        "Now close the civilisation health panel.";

    [SerializeField]
    private string openStateMessage =
        "Open the civilization state panel.";

    [SerializeField]
    private string explainStateMessage =
        "This panel shows the state of your civilisation.";

    [SerializeField]
    private string closeStateMessage =
        "Now close the civilization state panel.";

    [SerializeField]
    private string closePopulationPanelMessage =
        "Now close the population statistics panel.";

    [Header("Settings")]
    [SerializeField] private bool resumeTurnTimerWhenFinished = true;

    private bool _running;
    private bool _completedThisGame;
    private bool _cameraLockedByTutorial;
    private TutorialStep _step = TutorialStep.OpenPopulationPanel;

    public bool IsRunning => _running;
    public bool HasCompletedTutorial => _completedThisGame;

    [SerializeField] private ExtraUITutorial extraUITutorial;

    public bool ShouldRunTutorial()
    {
        return !_running && !_completedThisGame;
    }

    private void Awake()
    {
        if (populationStatisticsPanel == null)
            populationStatisticsPanel = FindObjectOfType<PlayerPopulationStatisticsPanelRoot>(true);

        if (civilisationNeedsPanel == null && populationStatisticsPanel != null)
            civilisationNeedsPanel = populationStatisticsPanel.civilisationNeedsPanel;

        if (civilisationHealthPanel == null && populationStatisticsPanel != null)
            civilisationHealthPanel = populationStatisticsPanel.civilisationHealthPanel;

        if (civilizationStatePanel == null && populationStatisticsPanel != null)
            civilizationStatePanel = populationStatisticsPanel.civilizationStatePanel;

        if (cameraControl == null)
            cameraControl = FindObjectOfType<CameraControl>(true);

        if (tileInteraction == null)
            tileInteraction = FindObjectOfType<TileInteraction>(true);

        BindButtons();

        SetRootVisible(false);
        SetBlockingMode(false);
        SetAllOverlaysOff();
        SetContinueButtonVisible(false);
        SetSkipButtonVisible(false);
        ShowPrimaryMessagePanel(false);
        ShowRatiosMessagePanel(false);
    }

    public void InstallRuntimeRefs(
        PlayerPopulationStatisticsPanelRoot newPopulationStatisticsPanel = null,
        CameraControl newCameraControl = null,
        TileInteraction newTileInteraction = null,
        ExtraUITutorial newExtraUITutorial = null)
    {
        if (newPopulationStatisticsPanel != null)
            populationStatisticsPanel = newPopulationStatisticsPanel;
        else if (populationStatisticsPanel == null)
            populationStatisticsPanel = FindObjectOfType<PlayerPopulationStatisticsPanelRoot>(true);

        if (populationStatisticsPanel != null)
        {
            if (civilisationNeedsPanel == null)
                civilisationNeedsPanel = populationStatisticsPanel.civilisationNeedsPanel;

            if (civilisationHealthPanel == null)
                civilisationHealthPanel = populationStatisticsPanel.civilisationHealthPanel;

            if (civilizationStatePanel == null)
                civilizationStatePanel = populationStatisticsPanel.civilizationStatePanel;
        }

        if (newCameraControl != null)
            cameraControl = newCameraControl;
        else if (cameraControl == null)
            cameraControl = FindObjectOfType<CameraControl>(true);

        if (newTileInteraction != null)
            tileInteraction = newTileInteraction;
        else if (tileInteraction == null)
            tileInteraction = FindObjectOfType<TileInteraction>(true);

        if (newExtraUITutorial != null)
            extraUITutorial = newExtraUITutorial;

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
        _step = TutorialStep.OpenPopulationPanel;

        TurnSystem.Instance?.PauseTurnTimer();

        if (cameraControl != null && !_cameraLockedByTutorial)
        {
            cameraControl.PushInputLock();
            _cameraLockedByTutorial = true;
        }

        TileInteraction.SetSelectionEnabled(false);

        SetRootVisible(true);
        SetBlockingMode(false);
        SetAllOverlaysOff();
        SetOverlayVisible(darkOverlayWithHole, true);

        SetContinueButtonVisible(false);
        SetSkipButtonVisible(false);
        SetMessage(openPanelMessage);
    }

    private void Update()
    {
        if (!_running)
            return;

        switch (_step)
        {
            case TutorialStep.OpenPopulationPanel:
                {
                    if (IsPopulationPanelShowing())
                    {
                        _step = TutorialStep.ExplainPopulationNumbers;
                        SetAllOverlaysOff();
                        SetOverlayVisible(darkOverlayWithHole2, true);
                        SetContinueButtonVisible(true);
                        SetSkipButtonVisible(false);
                        SetMessage(numbersMessage);
                    }
                    break;
                }

            case TutorialStep.OpenNeedsPanel:
                {
                    if (IsNeedsPanelShowing())
                    {
                        _step = TutorialStep.ExplainNeedsPanel;
                        SetAllOverlaysOff();
                        SetOverlayVisible(darkOverlayWithHole5, true);
                        SetContinueButtonVisible(true);
                        SetMessage(explainNeedsMessage);
                    }
                    break;
                }

            case TutorialStep.CloseNeedsPanel:
                {
                    if (!IsNeedsPanelShowing())
                    {
                        _step = TutorialStep.OpenHealthPanel;
                        SetAllOverlaysOff();
                        SetOverlayVisible(darkOverlayWithHole6, true);
                        SetContinueButtonVisible(false);
                        SetMessage(openHealthMessage);
                    }
                    break;
                }

            case TutorialStep.OpenHealthPanel:
                {
                    if (IsHealthPanelShowing())
                    {
                        _step = TutorialStep.ExplainHealthPanel;
                        SetAllOverlaysOff();
                        SetOverlayVisible(darkOverlayWithHole7, true);
                        SetContinueButtonVisible(true);
                        SetMessage(explainHealthMessage);
                    }
                    break;
                }

            case TutorialStep.CloseHealthPanel:
                {
                    if (!IsHealthPanelShowing())
                    {
                        _step = TutorialStep.OpenStatePanel;
                        SetAllOverlaysOff();
                        SetOverlayVisible(darkOverlayWithHole8, true);
                        SetContinueButtonVisible(false);
                        SetMessage(openStateMessage);
                    }
                    break;
                }

            case TutorialStep.OpenStatePanel:
                {
                    if (IsStatePanelShowing())
                    {
                        _step = TutorialStep.ExplainStatePanel;
                        SetAllOverlaysOff();
                        SetOverlayVisible(darkOverlayWithHole9, true);
                        SetContinueButtonVisible(true);
                        SetMessage(explainStateMessage);
                    }
                    break;
                }

            case TutorialStep.CloseStatePanel:
                {
                    if (!IsStatePanelShowing())
                    {
                        _step = TutorialStep.ClosePopulationPanel;
                        SetAllOverlaysOff();
                        SetContinueButtonVisible(false);
                        SetMessage(closePopulationPanelMessage);
                    }
                    break;
                }

            case TutorialStep.ClosePopulationPanel:
                {
                    if (!IsPopulationPanelShowing())
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
            case TutorialStep.ExplainPopulationNumbers:
                {
                    _step = TutorialStep.ExplainRatios;
                    SetAllOverlaysOff();
                    SetOverlayVisible(darkOverlayWithHole3, true);
                    SetContinueButtonVisible(true);
                    SetRatiosMessage(ratiosMessage);
                    break;
                }

            case TutorialStep.ExplainRatios:
                {
                    _step = TutorialStep.OpenNeedsPanel;
                    SetAllOverlaysOff();
                    SetOverlayVisible(darkOverlayWithHole4, true);
                    SetContinueButtonVisible(false);
                    SetMessage(openNeedsMessage);
                    break;
                }

            case TutorialStep.ExplainNeedsPanel:
                {
                    _step = TutorialStep.CloseNeedsPanel;
                    SetAllOverlaysOff();
                    SetContinueButtonVisible(false);
                    SetMessage(closeNeedsMessage);
                    break;
                }

            case TutorialStep.ExplainHealthPanel:
                {
                    _step = TutorialStep.CloseHealthPanel;
                    SetAllOverlaysOff();
                    SetContinueButtonVisible(false);
                    SetMessage(closeHealthMessage);
                    break;
                }

            case TutorialStep.ExplainStatePanel:
                {
                    _step = TutorialStep.CloseStatePanel;
                    SetAllOverlaysOff();
                    SetContinueButtonVisible(false);
                    SetMessage(closeStateMessage);
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

        SetAllOverlaysOff();
        SetContinueButtonVisible(false);
        SetSkipButtonVisible(false);
        SetBlockingMode(false);
        SetRootVisible(false);
        ShowPrimaryMessagePanel(false);
        ShowRatiosMessagePanel(false);

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
        if (extraUITutorial != null && extraUITutorial.ShouldRunTutorial())
        {
            extraUITutorial.BeginTutorial();
            return;
        }

        if (resumeTurnTimerWhenFinished)
            TurnSystem.Instance?.ResumeTurnTimer();
    }

    public void ResetTutorialForNewGame()
    {
        _running = false;
        _completedThisGame = false;
        _step = TutorialStep.OpenPopulationPanel;

        SetAllOverlaysOff();
        SetContinueButtonVisible(false);
        SetSkipButtonVisible(false);
        SetBlockingMode(false);
        SetRootVisible(false);
        ShowPrimaryMessagePanel(false);
        ShowRatiosMessagePanel(false);

        if (_cameraLockedByTutorial && cameraControl != null)
        {
            cameraControl.PopInputLock();
            _cameraLockedByTutorial = false;
        }

        TileInteraction.SetSelectionEnabled(false);
        tileInteraction?.EnableSelectionAfter(0.01f);
    }

    private bool IsPopulationPanelShowing()
    {
        return populationStatisticsPanel != null && populationStatisticsPanel.IsShowing;
    }

    private bool IsNeedsPanelShowing()
    {
        return civilisationNeedsPanel != null && civilisationNeedsPanel.IsShowing;
    }

    private bool IsHealthPanelShowing()
    {
        return civilisationHealthPanel != null && civilisationHealthPanel.IsShowing;
    }

    private bool IsStatePanelShowing()
    {
        return civilizationStatePanel != null && civilizationStatePanel.IsShowing;
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
        ShowRatiosMessagePanel(false);
    }

    private void SetRatiosMessage(string value)
    {
        if (ratiosMessageText != null)
            ratiosMessageText.text = value;

        ShowPrimaryMessagePanel(false);
        ShowRatiosMessagePanel(true);
    }

    private void ShowPrimaryMessagePanel(bool visible)
    {
        if (messagePanel != null)
            messagePanel.SetActive(visible);
    }

    private void ShowRatiosMessagePanel(bool visible)
    {
        if (ratiosMessagePanel != null)
            ratiosMessagePanel.SetActive(visible);
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
        SetOverlayVisible(darkOverlayWithHole7, false);
        SetOverlayVisible(darkOverlayWithHole8, false);
        SetOverlayVisible(darkOverlayWithHole9, false);
    }

    private void SetOverlayVisible(GameObject overlay, bool visible)
    {
        if (overlay != null)
            overlay.SetActive(visible);
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