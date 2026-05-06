using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PopulationTutorial : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup rootCanvasGroup;
    [SerializeField] private GameObject darkOverlayWithHole;
    [SerializeField] private GameObject messagePanel;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button skipButton;

    [Header("Settings")]
    [SerializeField] private bool resumeTurnTimerWhenFinished = true;

    [Header("Next Tutorial")]
    [SerializeField] private DiscoveryTutorial discoveryTutorial;

    private bool _running;
    private bool _completedThisGame;

    public bool IsRunning => _running;
    public bool HasCompletedTutorial => _completedThisGame;

    public bool ShouldRunTutorial()
    {
        return !_running && !_completedThisGame;
    }

    private void Awake()
    {
        if (discoveryTutorial == null)
            discoveryTutorial = FindObjectOfType<DiscoveryTutorial>(true);

        BindButtons();
        SetRootVisible(false);
        SetBlockingMode(false);
        SetHoleOverlayVisible(false);
    }

    public void InstallRuntimeRefs(DiscoveryTutorial newDiscoveryTutorial = null)
    {
        if (newDiscoveryTutorial != null)
            discoveryTutorial = newDiscoveryTutorial;
        else if (discoveryTutorial == null)
            discoveryTutorial = FindObjectOfType<DiscoveryTutorial>(true);

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
        TurnSystem.Instance?.PauseTurnTimer();

        SetRootVisible(true);
        SetBlockingMode(true);
        SetHoleOverlayVisible(true);

        if (messagePanel != null)
            messagePanel.SetActive(true);

        if (continueButton != null)
            continueButton.gameObject.SetActive(true);

        if (skipButton != null)
            skipButton.gameObject.SetActive(false);

        SetMessage("This is your population. The top values show your current and total population. The bottom values show your available population and used population.");
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

        SetHoleOverlayVisible(false);
        SetBlockingMode(false);
        SetRootVisible(false);

        BeginNextTutorialOrResume();
    }

    private void BeginNextTutorialOrResume()
    {
        if (discoveryTutorial == null)
            discoveryTutorial = FindObjectOfType<DiscoveryTutorial>(true);

        if (discoveryTutorial != null && discoveryTutorial.ShouldRunTutorial())
        {
            discoveryTutorial.BeginTutorial();

            TileInteraction.SetSelectionEnabled(false);
            TileInteraction.GetInstance()?.EnableSelectionAfter(0.01f);
            return;
        }

        Debug.LogWarning("[PopulationTutorial] DiscoveryTutorial was not found or could not start.");

        if (resumeTurnTimerWhenFinished)
        {
            TurnSystem.Instance?.ResumeTurnTimer();
            TileInteraction.SetSelectionEnabled(false);
            TileInteraction.GetInstance()?.EnableSelectionAfter(0.01f);
        }
    }

    public void ResetTutorialForNewGame()
    {
        _running = false;
        _completedThisGame = false;

        SetHoleOverlayVisible(false);
        SetBlockingMode(false);
        SetRootVisible(false);
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

    private void SetHoleOverlayVisible(bool visible)
    {
        if (darkOverlayWithHole != null)
            darkOverlayWithHole.SetActive(visible);
    }
}