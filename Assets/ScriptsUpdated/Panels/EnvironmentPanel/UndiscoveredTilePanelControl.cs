using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;


public class UndiscoveredTilePanelControl : MonoBehaviour
{
    [Header("UI References")]
    public GameObject root;
    public TMP_Text titleText;
    public TMP_Text descriptionText;
    public TMP_Text coordinatesText;
    public Button discoverButton;
    public Button closeButton;

    [Header("Detail Overlay")]
    public Button detailsButton; // button on this panel to open the discovery details
    public DiscoveryDetailsPanelControl detailsPanel;

    [Header("Block UI")]
    public Button discoverBlockedOverlay;        // transparent overlay, enabled only when blocked
    public LowDiscoveryPopupPanel lowDiscoveryPopup;

    [Header("Fire")]
    public GameObject fireBlockOverlay;

    public bool IsShowing => root != null ? root.activeInHierarchy : gameObject.activeInHierarchy;
    public EnvironmentControl CurrentEnvironment => currentEnv;
    public Func<EnvironmentControl, bool> TutorialDiscoverOverride;

    public bool SuppressSelectionReenableOnHide { get; set; }

    private EnvironmentControl currentEnv;
    private EnvironmentFireState currentFireState;

    public event Action OnClose;

    public CameraControl cameraControl;

    private void Start()
    {
        if (discoverButton != null)
            discoverButton.onClick.AddListener(OnDiscoverClicked);

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }

        if (detailsButton != null)
        {
            detailsButton.onClick.AddListener(() =>
            {
                if (currentEnv != null && detailsPanel != null)
                    detailsPanel.ShowFor(currentEnv);
            });
        }

        // Overlay shows popup when blocked
        if (discoverBlockedOverlay)
        {
            discoverBlockedOverlay.onClick.RemoveAllListeners();
            discoverBlockedOverlay.onClick.AddListener(ShowBlockedPopup);
            discoverBlockedOverlay.gameObject.SetActive(false); // default off
        }

        if (cameraControl == null)
            cameraControl = FindObjectOfType<CameraControl>();
    }

    private void OnDisable() => UnsubscribeFireState();
    private void OnDestroy() => UnsubscribeFireState();

    public void Show(EnvironmentControl env)
    {
        if (env == null) return;

        UnsubscribeFireState();
        currentEnv = env;
        currentFireState = env.GetComponent<EnvironmentFireState>();

        if (currentFireState != null)
            currentFireState.OnIgnited      += HandleFireIgnited;
        if (currentFireState != null)
            currentFireState.OnExtinguished += HandleFireExtinguished;

        if (root != null) root.SetActive(true);
        else gameObject.SetActive(true);

        TileInteraction.SetSelectionEnabled(false);

        titleText.text = "Undiscovered Tile";
        descriptionText.text = "This area hasn't been explored yet. You can begin discovery to reveal its details.";

        Vector2Int coords;
        var tileControl = env.GetComponent<TileControl>();
        coords = tileControl ? tileControl.GetGridPosition() : env.gridPosition;
        coordinatesText.text = $"Tile Coordinates: {coords.x}, {coords.y}";

        // --- Use EFFECTIVE discovery failure% for gating ---
        float baseFail = Mathf.Clamp(env.DiscoveryFailureChance, 0f, 100f);

        var tile = env.GetComponentInParent<TileControl>();
            baseFail = Mathf.Clamp(baseFail + PredatorFailureBonus.GetBonusPercent(tile), 0f, 100f);
            
        int   baseTurns = Mathf.Max(1, env.discoveryTurnsRequired);
        float effFail = baseFail;

        var buffs = PlayerTechBuffs.Instance;
        if (buffs != null)
        {
            (effFail, _) = buffs.GetDiscoveryEffective(env, baseFail, baseTurns);
            effFail = Mathf.Clamp(effFail, 0f, 100f);
        }

        bool blocked = false;
        float failureChance01 = Mathf.Clamp01(effFail / 100f);
        if (CivilizationDiscoverySystem.Instance != null)
            blocked = CivilizationDiscoverySystem.Instance.ShouldBlockForRisk(failureChance01);

        // Apply UI state
        if (discoverButton) discoverButton.interactable = !blocked;

        // Turn on the overlay *only* when blocked
        if (discoverBlockedOverlay)
            discoverBlockedOverlay.gameObject.SetActive(blocked);

        cameraControl.PushInputLock();

        RefreshFireBlock();
    }

    private void ShowBlockedPopup()
    {
        if (currentEnv == null) return;

        float d = CivilizationStateManager.Instance ? CivilizationStateManager.Instance.discovery01 : 0f;
        int dPct = Mathf.RoundToInt(d * 100f);

        // Use effective failure in message too
        float baseFail = Mathf.Clamp(currentEnv.DiscoveryFailureChance, 0f, 100f);
        int   baseTurns = Mathf.Max(1, currentEnv.discoveryTurnsRequired);
        float effFail = baseFail;
        var buffs = PlayerTechBuffs.Instance;
        if (buffs != null) (effFail, _) = buffs.GetDiscoveryEffective(currentEnv, baseFail, baseTurns);
        effFail = Mathf.Round(Mathf.Clamp(effFail, 0f, 100f));

        string title = "Discovery Too Low";
        string body =
            $"Your people feel risk-averse right now (Discovery {dPct}%).\n\n" +
            $"This environment has a high failure chance ({effFail}%). " +
            $"Raise Discovery by succeeding at safer tasks or let it recover over time, " +
            $"then try again.";

        if (lowDiscoveryPopup)
            lowDiscoveryPopup.Show(title, body);
        else
            //Debug.LogWarning("[UndiscoveredTilePanel] lowDiscoveryPopup not set.");
    }

    public void Hide()
    {

        if (!SuppressSelectionReenableOnHide)
        {
            TileInteraction.SetSelectionEnabled(false);
            TileInteraction.GetInstance()?.EnableSelectionAfter(0.01f);

            cameraControl.PopInputLock();
        }

        SuppressSelectionReenableOnHide = false;

        if (root != null) root.SetActive(false);

        UnsubscribeFireState();
        currentEnv = null;
        OnClose?.Invoke();
    }

    private void RefreshFireBlock()
    {
        if (fireBlockOverlay == null) return;
        bool onFire = currentFireState != null && currentFireState.IsOnFire;
        fireBlockOverlay.SetActive(onFire);
    }

    private void HandleFireIgnited(EnvironmentFireState state)     => RefreshFireBlock();
    private void HandleFireExtinguished(EnvironmentFireState state) => RefreshFireBlock();

    private void UnsubscribeFireState()
    {
        if (currentFireState == null) return;
        currentFireState.OnIgnited      -= HandleFireIgnited;
        currentFireState.OnExtinguished -= HandleFireExtinguished;
        currentFireState = null;
    }

    private void OnDiscoverClicked()
    {
        if (currentEnv == null) return;

        if (TutorialDiscoverOverride != null && TutorialDiscoverOverride.Invoke(currentEnv))
            return;

        int required = currentEnv.requireDiscoveryPopulation;
        if (PlayerTechBuffs.Instance != null)
            required = PlayerTechBuffs.Instance.GetDiscoveryRequiredPopEffective(
                currentEnv,
                currentEnv.requireDiscoveryPopulation
            );

        int available = PlayersPopulationManager.Instance != null
            ? PlayersPopulationManager.Instance.GetAvailableTaskPopulation()
            : 0;

        if (available < required)
        {
            if (detailsPanel != null)
                detailsPanel.ShowFor(currentEnv);
            else
                //Debug.LogWarning("Not enough population and detailsPanel is null.");
            return;
        }

        if (PlayerDiscoveryManager.Instance == null)
        {
            //Debug.LogWarning("PlayerDiscoveryManager.Instance is null. Cannot start discovery.");
            return;
        }

        bool started = PlayerDiscoveryManager.Instance.StartDiscovery(currentEnv);
        if (started)
        {
            PlayersPopulationManager.Instance?.ForceSyncUI();
            Hide();
        }
        else
        {
            if (detailsPanel != null)
                detailsPanel.ShowFor(currentEnv);

            //Debug.Log($"Failed to start discovery on {currentEnv.environmentName}. Requirements not met.");
        }
    }
}
