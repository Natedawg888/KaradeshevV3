using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProductionTutorial : MonoBehaviour
{
    private enum TutorialStep
    {
        ExplainPlans,
        ExplainExtractorVsImporter,
        ExplainOngoingCost,
        ExplainSwitchOutput
    }

    [Header("UI Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup rootCanvasGroup;

    [Header("Overlays")]
    [SerializeField] private GameObject darkOverlay;
    [SerializeField] private GameObject darkOverlay2;
    [SerializeField] private GameObject darkOverlay3;
    [SerializeField] private GameObject darkOverlay4;

    [Header("Message UI")]
    [SerializeField] private GameObject messagePanel;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button continueButton;

    [Header("References")]
    [SerializeField] private ProductionBuildingPanelControl productionPanel;

    [Header("Messages")]
    [SerializeField]
    private string plansMessage =
        "These are the production plans for the building.";

    [SerializeField]
    private string extractorVsImporterMessage =
        "Production buildings can either be extractors, meaning they extract raw resources from tiles, or importers that use resources strictly from the inventory.";

    [SerializeField]
    private string ongoingCostMessage =
        "Production is an ongoing cost.";

    [SerializeField]
    private string switchOutputMessage =
        "You can switch the output here.";

    [Header("Settings")]
    [SerializeField] private bool resumeTurnTimerWhenFinished = true;

    private bool _running;
    private bool _completedThisGame;
    private TutorialStep _step = TutorialStep.ExplainPlans;

    public bool IsRunning => _running;
    public bool HasCompletedTutorial => _completedThisGame;

    public bool ShouldRunTutorial()
    {
        return !_running && !_completedThisGame;
    }

    private void Awake()
    {
        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(OnContinuePressed);
            continueButton.onClick.AddListener(OnContinuePressed);
        }

        SetRootVisible(false);
        SetBlockingMode(false);
        SetAllOverlaysOff();
    }

    public void InstallRuntimeRefs(ProductionBuildingPanelControl newProductionPanel = null)
    {
        if (newProductionPanel != null)
            productionPanel = newProductionPanel;
    }

    public void BeginTutorial()
    {
        if (!ShouldRunTutorial())
            return;

        _running = true;
        _step = TutorialStep.ExplainPlans;

        TurnSystem.Instance?.PauseTurnTimer();

        SetRootVisible(true);
        SetBlockingMode(true);
        SetAllOverlaysOff();
        SetOverlayVisible(darkOverlay, true);

        if (messagePanel != null)
            messagePanel.SetActive(true);

        if (continueButton != null)
            continueButton.gameObject.SetActive(true);

        SetMessage(plansMessage);
    }

    private void OnContinuePressed()
    {
        if (!_running)
            return;

        switch (_step)
        {
            case TutorialStep.ExplainPlans:
                {
                    _step = TutorialStep.ExplainExtractorVsImporter;
                    SetAllOverlaysOff();
                    SetOverlayVisible(darkOverlay2, true);
                    SetMessage(extractorVsImporterMessage);
                    break;
                }

            case TutorialStep.ExplainExtractorVsImporter:
                {
                    _step = TutorialStep.ExplainOngoingCost;
                    SetAllOverlaysOff();
                    SetOverlayVisible(darkOverlay3, true);
                    SetMessage(ongoingCostMessage);
                    break;
                }

            case TutorialStep.ExplainOngoingCost:
                {
                    _step = TutorialStep.ExplainSwitchOutput;
                    SetAllOverlaysOff();
                    SetOverlayVisible(darkOverlay4, true);
                    SetMessage(switchOutputMessage);
                    break;
                }

            case TutorialStep.ExplainSwitchOutput:
                {
                    FinishTutorial(markComplete: true);
                    break;
                }
        }
    }

    private void FinishTutorial(bool markComplete)
    {
        _running = false;

        if (markComplete)
            _completedThisGame = true;

        SetAllOverlaysOff();
        SetBlockingMode(false);
        SetRootVisible(false);

        if (messagePanel != null)
            messagePanel.SetActive(false);

        if (continueButton != null)
            continueButton.gameObject.SetActive(false);

        if (resumeTurnTimerWhenFinished)
            TurnSystem.Instance?.ResumeTurnTimer();
    }

    public void ResetTutorialForNewGame()
    {
        _running = false;
        _completedThisGame = false;
        _step = TutorialStep.ExplainPlans;

        SetAllOverlaysOff();
        SetBlockingMode(false);
        SetRootVisible(false);

        if (messagePanel != null)
            messagePanel.SetActive(false);

        if (continueButton != null)
            continueButton.gameObject.SetActive(false);
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

    private void SetAllOverlaysOff()
    {
        SetOverlayVisible(darkOverlay, false);
        SetOverlayVisible(darkOverlay2, false);
        SetOverlayVisible(darkOverlay3, false);
        SetOverlayVisible(darkOverlay4, false);
    }

    private void SetOverlayVisible(GameObject overlay, bool visible)
    {
        if (overlay != null)
            overlay.SetActive(visible);
    }
}