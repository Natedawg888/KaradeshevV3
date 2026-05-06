using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProductionRunningTutorial : MonoBehaviour
{
    private enum TutorialStep
    {
        ExplainRunningPlan,
        ExplainRunningControls
    }

    [Header("UI Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup rootCanvasGroup;

    [Header("Overlays")]
    [SerializeField] private GameObject darkOverlay;
    [SerializeField] private GameObject darkOverlay2;

    [Header("Message UI")]
    [SerializeField] private GameObject messagePanel;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button continueButton;

    [Header("References")]
    [SerializeField] private ProductionRunningPanelControl productionRunningPanel;

    [Header("Messages")]
    [SerializeField]
    private string introMessage =
        "This controls a running production plan.";

    [SerializeField]
    private string controlsMessage =
        "Use this panel to manage and monitor the running production plan.";

    [Header("Settings")]
    [SerializeField] private bool resumeTurnTimerWhenFinished = true;

    private bool _running;
    private bool _completedThisGame;
    private TutorialStep _step = TutorialStep.ExplainRunningPlan;

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

        if (messagePanel != null)
            messagePanel.SetActive(false);

        if (continueButton != null)
            continueButton.gameObject.SetActive(false);
    }

    public void InstallRuntimeRefs(ProductionRunningPanelControl newProductionRunningPanel = null)
    {
        if (newProductionRunningPanel != null)
            productionRunningPanel = newProductionRunningPanel;
    }

    public void BeginTutorial()
    {
        if (!ShouldRunTutorial())
            return;

        _running = true;
        _step = TutorialStep.ExplainRunningPlan;

        TurnSystem.Instance?.PauseTurnTimer();

        SetRootVisible(true);
        SetBlockingMode(true);
        SetAllOverlaysOff();
        SetOverlayVisible(darkOverlay, true);

        if (messagePanel != null)
            messagePanel.SetActive(true);

        if (continueButton != null)
            continueButton.gameObject.SetActive(true);

        if (messageText != null)
            messageText.text = introMessage;
    }

    private void OnContinuePressed()
    {
        if (!_running)
            return;

        switch (_step)
        {
            case TutorialStep.ExplainRunningPlan:
                {
                    _step = TutorialStep.ExplainRunningControls;
                    SetAllOverlaysOff();
                    SetOverlayVisible(darkOverlay2, true);

                    if (messageText != null)
                        messageText.text = controlsMessage;
                    break;
                }

            case TutorialStep.ExplainRunningControls:
                {
                    FinishTutorial(true);
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
        _step = TutorialStep.ExplainRunningPlan;

        SetAllOverlaysOff();
        SetBlockingMode(false);
        SetRootVisible(false);

        if (messagePanel != null)
            messagePanel.SetActive(false);

        if (continueButton != null)
            continueButton.gameObject.SetActive(false);
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
    }

    private void SetOverlayVisible(GameObject overlay, bool visible)
    {
        if (overlay != null)
            overlay.SetActive(visible);
    }
}