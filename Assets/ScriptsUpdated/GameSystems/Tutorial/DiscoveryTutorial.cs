using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DiscoveryTutorial : MonoBehaviour
{
    private enum TutorialStep
    {
        OpenUndiscoveredPanel,
        OpenDetailsPanel,
        CloseDetailsPanel,
        DiscoverTile,
        GhostTickSequence,
        FailureExample,
        FinalReveal
    }

    [Header("UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup rootCanvasGroup;
    [SerializeField] private GameObject darkOverlayWithHole;
    [SerializeField] private GameObject darkOverlayWithHole2;
    [SerializeField] private GameObject messagePanel;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button skipButton;

    [Header("References")]
    [SerializeField] private UndiscoveredTilePanelControl undiscoveredTilePanel;
    [SerializeField] private CameraControl cameraControl;
    [SerializeField] private TileInteraction tileInteraction;

    [Header("Message Behaviour")]
    [SerializeField] private bool detailsPanelHasVisualCloseButton = false;

    [Header("Tutorial Discovery")]
    [SerializeField] private int tutorialDiscoveryTurnsOverride = -1;
    [SerializeField] private int fakeFailurePopulationLost = 0;

    [Header("Settings")]
    [SerializeField] private bool resumeTurnTimerWhenFinished = true;

    private bool _running;
    private bool _completedThisGame;
    private bool _panelWasOpenWhenStarted;
    private bool _cameraLockedByTutorial;

    private TutorialStep _step = TutorialStep.OpenUndiscoveredPanel;
    private EnvironmentControl _tutorialEnv;
    private Coroutine _ghostSequenceRoutine;

    public bool IsRunning => _running;
    public bool HasCompletedTutorial => _completedThisGame;

    [SerializeField] private GatheringTutorial gatheringTutorial;

    public bool ShouldRunTutorial()
    {
        return !_running && !_completedThisGame;
    }

    private void Awake()
    {
        if (undiscoveredTilePanel == null)
            undiscoveredTilePanel = FindObjectOfType<UndiscoveredTilePanelControl>(true);

        if (cameraControl == null)
            cameraControl = FindObjectOfType<CameraControl>(true);

        if (tileInteraction == null)
            tileInteraction = FindObjectOfType<TileInteraction>(true);

        BindButtons();
        SetRootVisible(false);
        SetBlockingMode(false);
        SetHoleOverlayVisible(false);
        SetHoleOverlay2Visible(false);
        SetContinueButtonVisible(false);
    }

    public void InstallRuntimeRefs(
    UndiscoveredTilePanelControl newUndiscoveredTilePanel = null,
    CameraControl newCameraControl = null,
    TileInteraction newTileInteraction = null,
    GatheringTutorial newGatheringTutorial = null)
    {
        UnbindUndiscoveredPanelHooks();

        if (newUndiscoveredTilePanel != null)
            undiscoveredTilePanel = newUndiscoveredTilePanel;
        else if (undiscoveredTilePanel == null)
            undiscoveredTilePanel = FindObjectOfType<UndiscoveredTilePanelControl>(true);

        if (newCameraControl != null)
            cameraControl = newCameraControl;
        else if (cameraControl == null)
            cameraControl = FindObjectOfType<CameraControl>(true);

        if (newTileInteraction != null)
            tileInteraction = newTileInteraction;
        else if (tileInteraction == null)
            tileInteraction = FindObjectOfType<TileInteraction>(true);

        if (newGatheringTutorial != null)
            gatheringTutorial = newGatheringTutorial;

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

        if (undiscoveredTilePanel == null)
            undiscoveredTilePanel = FindObjectOfType<UndiscoveredTilePanelControl>(true);

        if (cameraControl == null)
            cameraControl = FindObjectOfType<CameraControl>(true);

        if (tileInteraction == null)
            tileInteraction = FindObjectOfType<TileInteraction>(true);

        BindUndiscoveredPanelHooks();

        _running = true;
        _step = TutorialStep.OpenUndiscoveredPanel;
        _panelWasOpenWhenStarted = IsUndiscoveredPanelShowing();
        _tutorialEnv = null;
        _cameraLockedByTutorial = false;

        TurnSystem.Instance?.PauseTurnTimer();

        SetRootVisible(true);
        SetBlockingMode(false);
        SetHoleOverlayVisible(false);
        SetHoleOverlay2Visible(false);
        SetSkipButtonVisible(false);
        SetContinueButtonVisible(false);

        if (messagePanel != null)
            messagePanel.SetActive(true);

        SetMessage("Click one of the environment tiles to open the undiscovered tile panel.");
    }

    private void Update()
    {
        if (!_running || undiscoveredTilePanel == null)
            return;

        bool panelShowing = IsUndiscoveredPanelShowing();
        bool detailsShowing = IsDetailsPanelShowing();

        switch (_step)
        {
            case TutorialStep.OpenUndiscoveredPanel:
                {
                    if (!_panelWasOpenWhenStarted && panelShowing)
                    {
                        _step = TutorialStep.OpenDetailsPanel;
                        SetHoleOverlayVisible(true);
                        SetHoleOverlay2Visible(false);
                        SetSkipButtonVisible(false);
                        SetContinueButtonVisible(false);
                        SetMessage("Open the discovery details panel to find out the tile's requirements.");
                    }
                    break;
                }

            case TutorialStep.OpenDetailsPanel:
                {
                    if (detailsShowing)
                    {
                        _step = TutorialStep.CloseDetailsPanel;
                        SetHoleOverlayVisible(false);
                        SetHoleOverlay2Visible(false);
                        SetSkipButtonVisible(false);
                        SetContinueButtonVisible(false);

                        if (detailsPanelHasVisualCloseButton)
                            SetMessage("Click the close button to close the discovery details panel.");
                        else
                            SetMessage("Click anywhere outside the panel to close it.");
                    }
                    break;
                }

            case TutorialStep.CloseDetailsPanel:
                {
                    if (!detailsShowing)
                    {
                        _step = TutorialStep.DiscoverTile;
                        SetHoleOverlayVisible(false);
                        SetHoleOverlay2Visible(true);
                        SetSkipButtonVisible(false);
                        SetContinueButtonVisible(false);
                        SetMessage("Now discover the tile.");
                    }
                    break;
                }
        }
    }

    private void OnContinuePressed()
    {
        if (!_running)
            return;

        if (_step == TutorialStep.FailureExample)
        {
            if (_tutorialEnv != null)
                _tutorialEnv.AcknowledgeFailureIndicators();

            _step = TutorialStep.GhostTickSequence;
            SetHoleOverlayVisible(false);
            SetHoleOverlay2Visible(false);
            SetSkipButtonVisible(false);
            SetContinueButtonVisible(false);
            SetMessage("Tiles will tick over until it completes.");

            if (_ghostSequenceRoutine != null)
                StopCoroutine(_ghostSequenceRoutine);

            _ghostSequenceRoutine = StartCoroutine(RunTutorialCompletionSequence());
            return;
        }

        if (_step == TutorialStep.FinalReveal)
            CompleteTutorial();
    }

    private void OnSkipPressed()
    {
        SkipTutorial();
    }

    private bool HandleTutorialDiscoverRequested(EnvironmentControl env)
    {
        if (!_running || _step != TutorialStep.DiscoverTile || env == null)
            return false;

        TileInteraction.SetSelectionEnabled(false);

        if (cameraControl == null)
            cameraControl = FindObjectOfType<CameraControl>(true);

        if (cameraControl != null)
        {
            cameraControl.PushInputLock();
            _cameraLockedByTutorial = true;
        }

        _tutorialEnv = env;
        _step = TutorialStep.GhostTickSequence;

        SetHoleOverlayVisible(false);
        SetHoleOverlay2Visible(false);
        SetSkipButtonVisible(false);
        SetContinueButtonVisible(false);
        SetMessage("Tiles will tick over each turn.");

        if (undiscoveredTilePanel != null)
        {
            undiscoveredTilePanel.SuppressSelectionReenableOnHide = true;
            undiscoveredTilePanel.Hide();
        }

        TileInteraction.SetSelectionEnabled(false);

        if (_ghostSequenceRoutine != null)
            StopCoroutine(_ghostSequenceRoutine);

        _ghostSequenceRoutine = StartCoroutine(RunTutorialFailurePreviewSequence());
        return true;
    }

    private IEnumerator RunTutorialFailurePreviewSequence()
    {
        if (_tutorialEnv == null)
            yield break;

        int turnsToShow = tutorialDiscoveryTurnsOverride > 0
            ? tutorialDiscoveryTurnsOverride
            : Mathf.Max(1, _tutorialEnv.discoveryTurnsRequired);

        _tutorialEnv.BeginTutorialDiscoverySimulation(turnsToShow);

        // One ghost tick only, and show the failure icon on that tick.
        if (TurnSystem.Instance != null)
        {
            yield return TurnSystem.Instance.StartCoroutine(
                TurnSystem.Instance.RunGhostPhaseAdvance(() =>
                {
                    _tutorialEnv.ApplyTutorialDiscoveryGhostTick();
                    _tutorialEnv.ShowTutorialDiscoveryFailureIcon(fakeFailurePopulationLost);
                })
            );
        }
        else
        {
            _tutorialEnv.ApplyTutorialDiscoveryGhostTick();
            _tutorialEnv.ShowTutorialDiscoveryFailureIcon(fakeFailurePopulationLost);
            yield return null;
        }

        _step = TutorialStep.FailureExample;
        SetSkipButtonVisible(false);
        SetContinueButtonVisible(true);
        SetMessage("When a task fails, an icon will appear above the tile telling you that it failed and whether you lost any population.");
    }

    private IEnumerator RunTutorialCompletionSequence()
    {
        if (_tutorialEnv == null)
            yield break;

        while (_tutorialEnv.discoveryTurnsLeft > 0)
        {
            if (TurnSystem.Instance != null)
            {
                yield return TurnSystem.Instance.StartCoroutine(
                    TurnSystem.Instance.RunGhostPhaseAdvance(() => _tutorialEnv.ApplyTutorialDiscoveryGhostTick())
                );
            }
            else
            {
                _tutorialEnv.ApplyTutorialDiscoveryGhostTick();
                yield return null;
            }
        }

        _tutorialEnv.CompleteTutorialDiscoveryNow();

        _step = TutorialStep.FinalReveal;
        SetSkipButtonVisible(false);
        SetContinueButtonVisible(true);
        SetMessage("When the tile is discovered you can gather, survey or build on the tile.");
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

        if (_ghostSequenceRoutine != null)
        {
            StopCoroutine(_ghostSequenceRoutine);
            _ghostSequenceRoutine = null;
        }

        if (_tutorialEnv != null)
            _tutorialEnv.AcknowledgeFailureIndicators();

        UnbindUndiscoveredPanelHooks();

        SetHoleOverlayVisible(false);
        SetHoleOverlay2Visible(false);
        SetSkipButtonVisible(false);
        SetContinueButtonVisible(false);
        SetBlockingMode(false);
        SetRootVisible(false);

        TileInteraction.SetSelectionEnabled(false);

        if (tileInteraction == null)
            tileInteraction = FindObjectOfType<TileInteraction>(true);

        tileInteraction?.EnableSelectionAfter(0.01f);

        if (_cameraLockedByTutorial)
        {
            if (cameraControl == null)
                cameraControl = FindObjectOfType<CameraControl>(true);

            cameraControl?.PopInputLock();
            _cameraLockedByTutorial = false;
        }

        BeginNextTutorialOrResume();
    }

    private void BeginNextTutorialOrResume()
    {
        if (gatheringTutorial != null && _tutorialEnv != null)
        {
            gatheringTutorial.SetTargetEnvironment(_tutorialEnv);

            if (gatheringTutorial.ShouldRunTutorial())
            {
                gatheringTutorial.BeginTutorial();
                return;
            }
        }

        if (resumeTurnTimerWhenFinished)
            TurnSystem.Instance?.ResumeTurnTimer();
    }

    public void ResetTutorialForNewGame()
    {
        _running = false;
        _completedThisGame = false;
        _panelWasOpenWhenStarted = false;
        _step = TutorialStep.OpenUndiscoveredPanel;
        _tutorialEnv = null;

        if (_ghostSequenceRoutine != null)
        {
            StopCoroutine(_ghostSequenceRoutine);
            _ghostSequenceRoutine = null;
        }

        UnbindUndiscoveredPanelHooks();

        SetHoleOverlayVisible(false);
        SetHoleOverlay2Visible(false);
        SetSkipButtonVisible(false);
        SetContinueButtonVisible(false);
        SetBlockingMode(false);
        SetRootVisible(false);

        if (_cameraLockedByTutorial)
        {
            if (cameraControl == null)
                cameraControl = FindObjectOfType<CameraControl>(true);

            cameraControl?.PopInputLock();
            _cameraLockedByTutorial = false;
        }
    }

    private void BindButtons()
    {
        if (skipButton != null)
        {
            skipButton.onClick.RemoveListener(OnSkipPressed);
            skipButton.onClick.AddListener(OnSkipPressed);
        }

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(OnContinuePressed);
            continueButton.onClick.AddListener(OnContinuePressed);
        }
    }

    private void BindUndiscoveredPanelHooks()
    {
        if (undiscoveredTilePanel == null)
            return;

        undiscoveredTilePanel.TutorialDiscoverOverride = HandleTutorialDiscoverRequested;
    }

    private void UnbindUndiscoveredPanelHooks()
    {
        if (undiscoveredTilePanel == null)
            return;

        if (undiscoveredTilePanel.TutorialDiscoverOverride == (System.Func<EnvironmentControl, bool>)HandleTutorialDiscoverRequested)
            undiscoveredTilePanel.TutorialDiscoverOverride = null;
    }

    private bool IsUndiscoveredPanelShowing()
    {
        return undiscoveredTilePanel != null && undiscoveredTilePanel.IsShowing;
    }

    private bool IsDetailsPanelShowing()
    {
        if (undiscoveredTilePanel == null || undiscoveredTilePanel.detailsPanel == null)
            return false;

        return undiscoveredTilePanel.detailsPanel.IsShowing;
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

    private void SetHoleOverlay2Visible(bool visible)
    {
        if (darkOverlayWithHole2 != null)
            darkOverlayWithHole2.SetActive(visible);
    }

    private void SetSkipButtonVisible(bool visible)
    {
        if (skipButton != null)
            skipButton.gameObject.SetActive(visible);
    }

    private void SetContinueButtonVisible(bool visible)
    {
        if (continueButton != null)
            continueButton.gameObject.SetActive(visible);

        // Only make the tutorial root interactable when the continue button is up.
        if (visible)
            SetBlockingMode(true);
        else
            SetBlockingMode(false);
    }
}