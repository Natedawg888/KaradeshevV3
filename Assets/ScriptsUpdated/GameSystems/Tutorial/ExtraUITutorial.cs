using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ExtraUITutorial : MonoBehaviour
{
    private enum TutorialStep
    {
        ExplainExperience,
        ExplainSeasonTimer,
        ExplainPhaseTimer,
        ExplainTurnCounter,
        ExplainTurnSpeed,
        OpenProfilePanel
    }

    [Header("UI Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup rootCanvasGroup;

    [Header("Overlays")]
    [SerializeField] private GameObject darkOverlayWithHole;
    [SerializeField] private GameObject darkOverlayWithHole2;
    [SerializeField] private GameObject darkOverlay;
    [SerializeField] private GameObject darkOverlayWithHole3;
    [SerializeField] private GameObject darkOverlayWithHole4;
    [SerializeField] private GameObject darkOverlay2;

    [Header("Message UI")]
    [SerializeField] private GameObject messagePanel;
    [SerializeField] private TMP_Text messageText;

    [Header("Buttons")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button skipButton;

    [Header("References")]
    [SerializeField] private ProfilePanelControl profilePanelControl;
    [SerializeField] private CameraControl cameraControl;
    [SerializeField] private TileInteraction tileInteraction;

    [Header("Messages")]
    [SerializeField]
    private string experienceMessage =
        "This is your experience tracker and level.";

    [SerializeField]
    private string seasonMessage =
        "This is your season timer to tell you what season it is.";

    [SerializeField]
    private string phaseTimerMessage =
        "This is your phase timer and it counts down your turns.";

    [SerializeField]
    private string turnCounterMessage =
        "This is where you keep track of your turns.";

    [SerializeField]
    private string turnSpeedMessage =
        "This is your turn speed control.";

    [SerializeField]
    private string openProfileMessage =
        "Open the profile panel.";

    [Header("Settings")]
    [SerializeField] private bool resumeTurnTimerWhenFinished = true;

    private bool _running;
    private bool _completedThisGame;
    private bool _cameraLockedByTutorial;
    private TutorialStep _step = TutorialStep.ExplainExperience;

    public bool IsRunning => _running;
    public bool HasCompletedTutorial => _completedThisGame;

    [SerializeField] private ProfileTutorial profileTutorial;

    public bool ShouldRunTutorial()
    {
        return !_running && !_completedThisGame;
    }

    private void Awake()
    {
        if (profilePanelControl == null)
            profilePanelControl = FindObjectOfType<ProfilePanelControl>(true);

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
        ShowMessagePanel(false);
    }

    public void InstallRuntimeRefs(
        ProfilePanelControl newProfilePanelControl = null,
        CameraControl newCameraControl = null,
        TileInteraction newTileInteraction = null,
        ProfileTutorial newProfileTutorial = null)
    {
        if (newProfilePanelControl != null)
            profilePanelControl = newProfilePanelControl;
        else if (profilePanelControl == null)
            profilePanelControl = FindObjectOfType<ProfilePanelControl>(true);

        if (newCameraControl != null)
            cameraControl = newCameraControl;
        else if (cameraControl == null)
            cameraControl = FindObjectOfType<CameraControl>(true);

        if (newTileInteraction != null)
            tileInteraction = newTileInteraction;
        else if (tileInteraction == null)
            tileInteraction = FindObjectOfType<TileInteraction>(true);

        if (newProfileTutorial != null)
            profileTutorial = newProfileTutorial;

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
        _step = TutorialStep.ExplainExperience;

        TurnSystem.Instance?.PauseTurnTimer();

        if (cameraControl != null && !_cameraLockedByTutorial)
        {
            cameraControl.PushInputLock();
            _cameraLockedByTutorial = true;
        }

        TileInteraction.SetSelectionEnabled(false);

        SetRootVisible(true);
        SetBlockingMode(true);
        SetAllOverlaysOff();
        SetOverlayVisible(darkOverlayWithHole, true);
        SetContinueButtonVisible(true);
        SetSkipButtonVisible(false);
        SetMessage(experienceMessage);
    }

    private void Update()
    {
        if (!_running)
            return;

        if (_step == TutorialStep.OpenProfilePanel && IsProfilePanelShowing())
        {
            BeginNextTutorialOrResume();
        }
    }

    private void OnContinuePressed()
    {
        if (!_running)
            return;

        switch (_step)
        {
            case TutorialStep.ExplainExperience:
                {
                    _step = TutorialStep.ExplainSeasonTimer;
                    SetAllOverlaysOff();
                    SetOverlayVisible(darkOverlayWithHole2, true);
                    SetContinueButtonVisible(true);
                    SetMessage(seasonMessage);
                    break;
                }

            case TutorialStep.ExplainSeasonTimer:
                {
                    _step = TutorialStep.ExplainPhaseTimer;
                    SetAllOverlaysOff();
                    SetOverlayVisible(darkOverlay, true);
                    SetContinueButtonVisible(true);
                    SetMessage(phaseTimerMessage);
                    break;
                }

            case TutorialStep.ExplainPhaseTimer:
                {
                    _step = TutorialStep.ExplainTurnCounter;
                    SetAllOverlaysOff();
                    SetOverlayVisible(darkOverlayWithHole3, true);
                    SetContinueButtonVisible(true);
                    SetMessage(turnCounterMessage);
                    break;
                }

            case TutorialStep.ExplainTurnCounter:
                {
                    _step = TutorialStep.ExplainTurnSpeed;
                    SetAllOverlaysOff();
                    SetOverlayVisible(darkOverlayWithHole4, true);
                    SetContinueButtonVisible(true);
                    SetMessage(turnSpeedMessage);
                    break;
                }

            case TutorialStep.ExplainTurnSpeed:
                {
                    _step = TutorialStep.OpenProfilePanel;
                    SetAllOverlaysOff();
                    SetOverlayVisible(darkOverlay2, true);
                    SetContinueButtonVisible(false);
                    SetMessage(openProfileMessage);
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
        FinishTutorial(markComplete: true, chainToProfileTutorial: false);
    }

    private void CompleteTutorial()
    {
        FinishTutorial(markComplete: true, chainToProfileTutorial: false);
    }

    private void BeginNextTutorialOrResume()
    {
        if (profileTutorial != null && profileTutorial.ShouldRunTutorial())
        {
            FinishTutorial(markComplete: true, chainToProfileTutorial: true);
            profileTutorial.BeginTutorial();
            return;
        }

        CompleteTutorial();
    }

    private void FinishTutorial(bool markComplete, bool chainToProfileTutorial)
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

        if (!chainToProfileTutorial)
        {
            TileInteraction.SetSelectionEnabled(false);
            tileInteraction?.EnableSelectionAfter(0.01f);

            if (resumeTurnTimerWhenFinished)
                TurnSystem.Instance?.ResumeTurnTimer();
        }
    }

    public void ResetTutorialForNewGame()
    {
        _running = false;
        _completedThisGame = false;
        _step = TutorialStep.ExplainExperience;

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
    }

    private bool IsProfilePanelShowing()
    {
        return profilePanelControl != null && profilePanelControl.IsShowing;
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
        SetOverlayVisible(darkOverlay, false);
        SetOverlayVisible(darkOverlayWithHole3, false);
        SetOverlayVisible(darkOverlayWithHole4, false);
        SetOverlayVisible(darkOverlay2, false);
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

        SetBlockingMode(visible || (_step == TutorialStep.OpenProfilePanel));
    }
}