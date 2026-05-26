using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Attach to a full-screen overlay GameObject (start inactive).
// Wire panelRoot, mainMenuButton, newGameButton in the Inspector.
// Set titleSceneName = "TitleScene" and bootstrapSceneName = "BootstrapCore".
public class GameOverScreen : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;

    [Header("Buttons")]
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button newGameButton;

    [Header("Optional Labels")]
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text subtitleLabel;

    [Header("Scene Names")]
    [SerializeField] private string titleSceneName    = "TitleScene";
    [SerializeField] private string bootstrapSceneName = "BootstrapCore";

    private bool _triggered;

    private void Awake()
    {
        Hide();

        if (mainMenuButton) mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        if (newGameButton)  newGameButton.onClick.AddListener(OnNewGameClicked);
    }

    private void OnEnable()
    {
        var pop = PlayersPopulationManager.Instance;
        if (pop != null)
            pop.OnPopulationChanged += HandlePopulationChanged;
    }

    private void OnDisable()
    {
        var pop = PlayersPopulationManager.Instance;
        if (pop != null)
            pop.OnPopulationChanged -= HandlePopulationChanged;
    }

    private void HandlePopulationChanged()
    {
        if (_triggered) return;

        var pop = PlayersPopulationManager.Instance;
        if (pop == null) return;

        if (pop.GetTotalPopulation() <= 0)
            TriggerGameOver();
    }

    private void TriggerGameOver()
    {
        _triggered = true;

        // Stop the turn timer immediately.
        if (TurnSystem.Instance != null)
            TurnSystem.Instance.PauseTurnTimer();

        // Commit score before showing the screen.
        CommitScore();

        if (titleLabel)   titleLabel.text    = "Your Civilization Has Fallen";
        if (subtitleLabel) subtitleLabel.text = "The last of your people are gone.";

        Show();
    }

    private void Show()
    {
        if (panelRoot) panelRoot.SetActive(true);
    }

    private void Hide()
    {
        if (panelRoot) panelRoot.SetActive(false);
    }

    private void OnMainMenuClicked()
    {
        StartCoroutine(LoadSceneRoutine(titleSceneName, newGame: false));
    }

    private void OnNewGameClicked()
    {
        StartCoroutine(LoadSceneRoutine(bootstrapSceneName, newGame: true));
    }

    private IEnumerator LoadSceneRoutine(string sceneName, bool newGame)
    {
        if (mainMenuButton) mainMenuButton.interactable = false;
        if (newGameButton)  newGameButton.interactable  = false;

        if (newGame)
            GameStartContext.SetRequestedMode(GameStartMode.NewGame);

        // Do NOT save — the civilization is dead; preserve the last autosave instead.
        SaveSystem.DeleteSave();

        yield return null;

        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    private static void CommitScore()
    {
        if (ScoreManager.Instance == null) return;

        string playerName = ProfilePanelControl.Instance != null
            ? ProfilePanelControl.Instance.PlayerName        : string.Empty;
        string civName    = ProfilePanelControl.Instance != null
            ? ProfilePanelControl.Instance.CivilizationName  : string.Empty;
        string avatarName = ProfilePanelControl.Instance != null
            ? ProfilePanelControl.Instance.CurrentAvatarName : string.Empty;

        ScoreManager.Instance.CommitScoreToLeaderboard(playerName, civName, avatarName);
    }
}
