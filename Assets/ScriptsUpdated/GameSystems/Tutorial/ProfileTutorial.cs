using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProfileTutorial : MonoBehaviour
{
    private enum TutorialStep
    {
        ExplainNames,
        ExplainAvatarButton,
        ExplainExitButton,
        CloseProfilePanel
    }

    [Header("UI Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup rootCanvasGroup;

    [Header("Overlays")]
    [SerializeField] private GameObject darkOverlayWithHole;
    [SerializeField] private GameObject darkOverlayWithHole2;
    [SerializeField] private GameObject darkOverlayWithHole3;

    [Header("Message UI")]
    [SerializeField] private GameObject messagePanel;
    [SerializeField] private TMP_Text messageText;

    [Header("Buttons")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button skipButton;

    [Header("References")]
    [SerializeField] private ProfilePanelControl profilePanelControl;

    [Header("Messages")]
    [SerializeField]
    private string namesMessage =
        "Change your civilization name and player name here.";

    [SerializeField]
    private string avatarMessage =
        "Click here to change your avatar.";

    [SerializeField]
    private string exitMessage =
        "Click here to exit to the title screen.";

    [SerializeField]
    private string closeProfileMessage =
        "Now close the profile panel.";

    [Header("Settings")]
    [SerializeField] private bool resumeTurnTimerWhenFinished = true;

    private bool _running;
    private bool _completedThisGame;
    private TutorialStep _step = TutorialStep.ExplainNames;

    public bool IsRunning => _running;
    public bool HasCompletedTutorial => _completedThisGame;

    [SerializeField] private BuildingTutorial buildingTutorial;

    public bool ShouldRunTutorial()
    {
        return !_running && !_completedThisGame;
    }

    private void Awake()
    {
        if (profilePanelControl == null)
            profilePanelControl = FindObjectOfType<ProfilePanelControl>(true);

        BindButtons();

        SetRootVisible(false);
        SetBlockingMode(false);
        SetAllOverlaysOff();
        SetContinueButtonVisible(false);
        SetSkipButtonVisible(false);
        ShowMessagePanel(false);
    }

    public void InstallRuntimeRefs(ProfilePanelControl newProfilePanelControl = null)
    {
        if (newProfilePanelControl != null)
            profilePanelControl = newProfilePanelControl;
        else if (profilePanelControl == null)
            profilePanelControl = FindObjectOfType<ProfilePanelControl>(true);

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
        _step = TutorialStep.ExplainNames;

        TurnSystem.Instance?.PauseTurnTimer();

        SetRootVisible(true);
        SetBlockingMode(true);
        SetAllOverlaysOff();
        SetOverlayVisible(darkOverlayWithHole, true);
        SetContinueButtonVisible(true);
        SetSkipButtonVisible(false);
        SetMessage(namesMessage);
    }

    private void Update()
    {
        if (!_running)
            return;

        if (_step == TutorialStep.CloseProfilePanel && !IsProfilePanelShowing())
            CompleteTutorial();
    }

    private void OnContinuePressed()
    {
        if (!_running)
            return;

        switch (_step)
        {
            case TutorialStep.ExplainNames:
                {
                    _step = TutorialStep.ExplainAvatarButton;
                    SetAllOverlaysOff();
                    SetOverlayVisible(darkOverlayWithHole2, true);
                    SetContinueButtonVisible(true);
                    SetMessage(avatarMessage);
                    break;
                }

            case TutorialStep.ExplainAvatarButton:
                {
                    _step = TutorialStep.ExplainExitButton;
                    SetAllOverlaysOff();
                    SetOverlayVisible(darkOverlayWithHole3, true);
                    SetContinueButtonVisible(true);
                    SetMessage(exitMessage);
                    break;
                }

            case TutorialStep.ExplainExitButton:
                {
                    _step = TutorialStep.CloseProfilePanel;
                    SetAllOverlaysOff();
                    SetContinueButtonVisible(false);
                    SetMessage(closeProfileMessage);
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
        ShowMessagePanel(false);

        BeginNextTutorialOrResume();
    }

    private void BeginNextTutorialOrResume()
    {
        if (buildingTutorial != null && buildingTutorial.ShouldRunTutorial())
        {
            buildingTutorial.BeginTutorial();
            return;
        }

        if (resumeTurnTimerWhenFinished)
            TurnSystem.Instance?.ResumeTurnTimer();
    }

    public void ResetTutorialForNewGame()
    {
        _running = false;
        _completedThisGame = false;
        _step = TutorialStep.ExplainNames;

        SetAllOverlaysOff();
        SetContinueButtonVisible(false);
        SetSkipButtonVisible(false);
        SetBlockingMode(false);
        SetRootVisible(false);
        ShowMessagePanel(false);
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
        SetOverlayVisible(darkOverlayWithHole3, false);
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

        SetBlockingMode(visible);
    }
}