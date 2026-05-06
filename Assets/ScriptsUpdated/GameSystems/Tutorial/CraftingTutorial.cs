using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CraftingTutorial : MonoBehaviour
{
    private enum TutorialStep
    {
        Intro,
        ExplainCostChange,
        ExplainOutput
    }

    [Header("UI Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup rootCanvasGroup;

    [Header("Overlays")]
    [SerializeField] private GameObject darkOverlay;
    [SerializeField] private GameObject darkOverlayWithHole;
    [SerializeField] private GameObject darkOverlayWithHole2;

    [Header("Message UI")]
    [SerializeField] private GameObject messagePanel;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button continueButton;

    [Header("References")]
    [SerializeField] private CraftingBuildingPanelControl craftingPanel;

    [Header("Messages")]
    [SerializeField]
    private string introMessage =
        "This is the crafting panel.";

    [SerializeField]
    private string costChangeMessage =
        "Use this area to change the cost option.";

    [SerializeField]
    private string outputMessage =
        "This is where you see the output.";

    [Header("Settings")]
    [SerializeField] private bool resumeTurnTimerWhenFinished = true;

    private bool _running;
    private bool _completedThisGame;
    private TutorialStep _step = TutorialStep.Intro;

    public bool IsRunning => _running;
    public bool HasCompletedTutorial => _completedThisGame;

    public bool ShouldRunTutorial()
    {
        return !_running && !_completedThisGame;
    }

    private void Awake()
    {
        if (craftingPanel == null)
            craftingPanel = FindObjectOfType<CraftingBuildingPanelControl>(true);

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(OnContinuePressed);
            continueButton.onClick.AddListener(OnContinuePressed);
        }

        SetRootVisible(false);
        SetBlockingMode(false);
        SetAllOverlaysOff();
    }

    public void BeginTutorial()
    {
        if (!ShouldRunTutorial())
            return;

        _running = true;
        _step = TutorialStep.Intro;

        TurnSystem.Instance?.PauseTurnTimer();

        SetRootVisible(true);
        SetBlockingMode(true);
        SetAllOverlaysOff();
        SetOverlayVisible(darkOverlay, true);

        if (messagePanel != null)
            messagePanel.SetActive(true);

        if (continueButton != null)
            continueButton.gameObject.SetActive(true);

        SetMessage(introMessage);
    }

    private void OnContinuePressed()
    {
        if (!_running)
            return;

        switch (_step)
        {
            case TutorialStep.Intro:
                {
                    _step = TutorialStep.ExplainCostChange;
                    SetAllOverlaysOff();
                    SetOverlayVisible(darkOverlayWithHole, true);
                    SetMessage(costChangeMessage);
                    break;
                }

            case TutorialStep.ExplainCostChange:
                {
                    _step = TutorialStep.ExplainOutput;
                    SetAllOverlaysOff();
                    SetOverlayVisible(darkOverlayWithHole2, true);
                    SetMessage(outputMessage);
                    break;
                }

            case TutorialStep.ExplainOutput:
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
        _step = TutorialStep.Intro;

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
        SetOverlayVisible(darkOverlayWithHole, false);
        SetOverlayVisible(darkOverlayWithHole2, false);
    }

    private void SetOverlayVisible(GameObject overlay, bool visible)
    {
        if (overlay != null)
            overlay.SetActive(visible);
    }
}