using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DiscoveredTilePanelControl : MonoBehaviour
{
    [Header("UI Roots")]
    public GameObject root;
    public Button closeButton;

    [Header("Header")]
    public TMP_Text titleText;
    public TMP_Text coordinatesText;

    [Header("Environment Display")]
    public Image environmentImage;         // the big icon for this environment

    [Header("Environment→Sprite Mapping")]
    public List<EnvironmentImageEntry> environmentSprites = new();

    [Header("Renaming UI")]
    public Button renameButton;
    public GameObject renameContainer;        // parent of input + save/cancel
    public TMP_InputField renameInputField;
    public Button saveRenameButton;
    public Button cancelRenameButton;

    [Header("Gathering")]
    public Button gatherButton;
    public Button gatherDetailsButton;
    public GatheringDetailsPanelControl gatheringDetailsPanel;

    public GameObject gatheringOb;       // container for the gathering progress UI
    public Slider gatheringProgressSlider;

    [Header("Survey")]
    public Button surveyButton;
    public Button surveyDetailsButton;
    public SurveyPanelControl surveyPanel;
    public SurveyDetailsPanelControl detailsPanel;
    public GameObject resurveyOb;
    public Slider resurveySlider;
    public GameObject surveyOb;
    public Slider surveyProgressSlider;

    [Header("Build")]
    public Button buildButton;
    public BuildingCatalogPanelControl buildingCatalogPanel;

    [Header("Clearing")]
    public Button clearButton;
    public Button clearDetailsButton;
    public ClearingDetailsPanelControl clearingDetailsPanel;

    [Header("Block UI (Gathering)")]
    public Button gatherBlockedOverlay;
    public LowDiscoveryPopupPanel lowDiscoveryPopup;

    private EnvironmentControl currentEnv;
    public event Action OnClose;

    public bool IsShowing => root != null ? root.activeInHierarchy : gameObject.activeInHierarchy;
    public EnvironmentControl CurrentEnvironment => currentEnv;
    public Func<EnvironmentControl, bool> TutorialSurveyOverride;
    public Func<EnvironmentControl, bool> TutorialGatherOverride;
    public bool SuppressSelectionReenableOnHide { get; set; }

    public Func<EnvironmentControl, bool> TutorialBuildOverride;

    public CameraControl cameraControl;

    [Serializable]
    public struct EnvironmentImageEntry
    {
        public EnvironmentType environmentType;
        public Sprite sprite;
        public List<EnvironmentTileType> tileTypes; // multiple associated tile types
    }

    private void Start()
    {
        // Close
        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(Hide);

        // Rename
        renameButton.onClick.RemoveAllListeners();
        renameButton.onClick.AddListener(BeginRename);
        saveRenameButton.onClick.RemoveAllListeners();
        saveRenameButton.onClick.AddListener(SubmitRename);
        cancelRenameButton.onClick.RemoveAllListeners();
        cancelRenameButton.onClick.AddListener(CancelRename);
        renameContainer.SetActive(false);

        // Survey
        surveyButton.onClick.RemoveAllListeners();
        surveyButton.onClick.AddListener(OnSurveyClicked);

        // Manual Details button
        surveyDetailsButton.onClick.RemoveAllListeners();
        surveyDetailsButton.onClick.AddListener(() =>
        {
            if (currentEnv != null)
                detailsPanel.ShowFor(currentEnv, showNoResources: false);
        });

        // Hide slider initially
        resurveyOb.gameObject.SetActive(false);
        surveyOb.gameObject.SetActive(false);

        // Gather
        if (gatherButton != null)
        {
            gatherButton.onClick.RemoveAllListeners();
            gatherButton.onClick.AddListener(OnGatherClicked);
        }

        if (gatherDetailsButton != null)
        {
            gatherDetailsButton.onClick.RemoveAllListeners();
            gatherDetailsButton.onClick.AddListener(() =>
            {
                if (currentEnv != null)
                    gatheringDetailsPanel?.ShowFor(currentEnv, showNoResources: false);
            });
        }

        if (gatheringOb != null) gatheringOb.SetActive(false);

        // Build
        if (buildButton != null)
        {
            buildButton.onClick.RemoveAllListeners();
            buildButton.onClick.AddListener(() =>
            {
                if (currentEnv == null)
                    return;

                if (TutorialBuildOverride != null && TutorialBuildOverride.Invoke(currentEnv))
                    return;

                if (buildingCatalogPanel != null)
                    buildingCatalogPanel.ShowFor(currentEnv, this);
            });
        }

        if (clearButton != null)
        {
            clearButton.onClick.RemoveAllListeners();
            clearButton.onClick.AddListener(OnClearClicked);
        }

        if (clearDetailsButton != null)
        {
            clearDetailsButton.onClick.RemoveAllListeners();
            clearDetailsButton.onClick.AddListener(() =>
            {
                if (currentEnv != null)
                    clearingDetailsPanel?.ShowFor(currentEnv);
            });
        }

        if (gatherBlockedOverlay)
        {
            gatherBlockedOverlay.onClick.RemoveAllListeners();
            gatherBlockedOverlay.onClick.AddListener(ShowGatherBlockedPopup);
            gatherBlockedOverlay.gameObject.SetActive(false);
        }

        if (cameraControl == null)
            cameraControl = FindObjectOfType<CameraControl>();

        Hide();
    }

    public void Show(EnvironmentControl env)
    {
        if (env == null) return;
        currentEnv = env;

        root.SetActive(true);

        TileInteraction.SetSelectionEnabled(false);
        cameraControl.PushInputLock();

        // Header
        titleText.text = env.environmentName;
        var coords = env.GetComponent<TileControl>()?.GetGridPosition() ?? env.gridPosition;
        coordinatesText.text = $"Coordinates: {coords.x}, {coords.y}";

        // Environment image & tile types
        var eType = env.environmentType;
        var tType = env.environmentTileType;

        EnvironmentImageEntry? match = environmentSprites
            .FirstOrDefault(e => e.environmentType == eType
                            && e.tileTypes != null
                            && e.tileTypes.Contains(tType));

        if (match == null || match.Value.sprite == null)
        {
            match = environmentSprites
                .FirstOrDefault(e => e.environmentType == eType
                                && (e.tileTypes == null || e.tileTypes.Count == 0));
        }

        if (match != null && match.Value.sprite != null)
            environmentImage.sprite = match.Value.sprite;
        else
            environmentImage.sprite = null;

        // Make sure rename UI is closed
        renameContainer.SetActive(false);
        renameButton.gameObject.SetActive(true);
        surveyPanel.Hide();

        // Re‐survey slider
        if (env.isSurveying)
        {
            surveyOb.gameObject.SetActive(true);
            surveyProgressSlider.minValue = 0;
            surveyProgressSlider.maxValue = env.surveyTurnsRequired;
            surveyProgressSlider.value = env.surveyTurnsLeft;

            resurveyOb.gameObject.SetActive(false);
        }
        else if (env.IsSurveyed)
        {
            resurveyOb.gameObject.SetActive(true);
            resurveySlider.minValue = 0;
            resurveySlider.maxValue = env.resurveyInterval;
            resurveySlider.value = env.resurveyTurnsLeft;

            surveyOb.gameObject.SetActive(false);
        }
        else
        {
            surveyOb.gameObject.SetActive(false);
            resurveyOb.gameObject.SetActive(false);
        }

        // Gathering button interactivity
        if (gatherButton != null)
            gatherButton.interactable = env.IsDiscovered && !env.isGathering;

        if (gatheringOb != null)
        {
            if (env.isGathering)
            {
                gatheringOb.SetActive(true);
                if (gatheringProgressSlider != null)
                {
                    gatheringProgressSlider.minValue = 0;
                    gatheringProgressSlider.maxValue = env.gatheringTurnsRequired;
                    gatheringProgressSlider.value = env.gatheringTurnsLeft;
                    gatheringProgressSlider.wholeNumbers = true;
                    gatheringProgressSlider.direction = Slider.Direction.RightToLeft; // counts down visually
                    gatheringProgressSlider.interactable = false;
                }
            }
            else
            {
                gatheringOb.SetActive(false);
            }
        }

        if (clearButton != null)
            clearButton.interactable = env.canBeManuallyCleared;

        ApplyGatheringButtonState();

        if (currentEnv.isSurveying) buildButton.interactable = false;
        if (currentEnv.isGathering) buildButton.interactable = false;
        if (currentEnv.IsSurveyed) buildButton.interactable = true;
        if (currentEnv.IsSurveyed) surveyButton.interactable = true;
        if (!currentEnv.isGathering) buildButton.interactable  = true;

        if (surveyButton != null)
            surveyButton.interactable = !currentEnv.isSurveying;
    }

    private void Update()
    {
        if (currentEnv == null) return;

        if (currentEnv.isGathering && gatheringProgressSlider != null)
            gatheringProgressSlider.value = currentEnv.gatheringTurnsLeft;

        if (currentEnv.isSurveying && surveyProgressSlider != null)
            surveyProgressSlider.value = currentEnv.surveyTurnsLeft;

        if (currentEnv.IsSurveyed && resurveySlider != null && resurveyOb.activeSelf)
            resurveySlider.value = currentEnv.resurveyTurnsLeft;

        if (gatherButton != null)
            gatherButton.interactable = !currentEnv.isGathering;

        if (surveyButton != null)
            surveyButton.interactable = !currentEnv.isSurveying;

            if (clearButton != null)
        clearButton.interactable = currentEnv.canBeManuallyCleared;

        ApplyGatheringButtonState();
    }

    private void ApplyGatheringButtonState()
    {
        if (gatherButton == null || currentEnv == null) return;

        bool baseInteractable = currentEnv.IsDiscovered && !currentEnv.isGathering;

        // Use EFFECTIVE gathering failure% for gating
        bool blocked = false;
        float failureChance01 = GetGatherFailureChance01(currentEnv); // 0..1
        if (CivilizationDiscoverySystem.Instance != null)
            blocked = CivilizationDiscoverySystem.Instance.ShouldBlockForRisk(failureChance01);

        gatherButton.interactable = baseInteractable && !blocked;

        if (gatherBlockedOverlay)
            gatherBlockedOverlay.gameObject.SetActive(baseInteractable && blocked);

        if (gatheringOb != null)
        {
            if (currentEnv.isGathering)
            {
                gatheringOb.SetActive(true);
                if (gatheringProgressSlider != null)
                {
                    gatheringProgressSlider.minValue  = 0;
                    gatheringProgressSlider.maxValue  = currentEnv.gatheringTurnsRequired;
                    gatheringProgressSlider.value     = currentEnv.gatheringTurnsLeft;
                    gatheringProgressSlider.wholeNumbers = true;
                    gatheringProgressSlider.direction = Slider.Direction.RightToLeft;
                    gatheringProgressSlider.interactable = false;
                }
            }
            else
            {
                gatheringOb.SetActive(false);
            }
        }
    }

    private float GetGatherFailureChance01(EnvironmentControl env)
    {
        float baseFail = 0f;
        int baseTurns = 1;
        try
        {
            baseFail  = Mathf.Clamp(env.GatheringFailureChance, 0f, 100f);
                
                var tile = env.GetComponentInParent<TileControl>();
                    baseFail = Mathf.Clamp(baseFail + PredatorFailureBonus.GetBonusPercent(tile), 0f, 100f);

            baseTurns = Mathf.Max(1, env.gatheringTurnsRequired);
        }
        catch
        {
            baseFail  = Mathf.Clamp(env.DiscoveryFailureChance, 0f, 100f);
            
                var tile = env.GetComponentInParent<TileControl>();
                        baseFail = Mathf.Clamp(baseFail + PredatorFailureBonus.GetBonusPercent(tile), 0f, 100f);
            
            baseTurns = Mathf.Max(1, env.discoveryTurnsRequired);
        }

        float effFail = baseFail;
        var buffs = PlayerTechBuffs.Instance;
        if (buffs != null)
        {
            (effFail, _) = buffs.GetGatheringEffective(env, baseFail, baseTurns);
            effFail = Mathf.Clamp(effFail, 0f, 100f);
        }

        return Mathf.Clamp01(effFail / 100f);
    }

    private void ShowGatherBlockedPopup()
    {
        if (currentEnv == null) return;

        float d = CivilizationStateManager.Instance ? CivilizationStateManager.Instance.discovery01 : 0f;
        int dPct = Mathf.RoundToInt(d * 100f);

        // show effective failure in popup
        float baseFail = Mathf.Clamp(currentEnv.GatheringFailureChance, 0f, 100f);
        int   baseTurns = Mathf.Max(1, currentEnv.gatheringTurnsRequired);
        float effFail = baseFail;
        var buffs = PlayerTechBuffs.Instance;
        if (buffs != null) (effFail, _) = buffs.GetGatheringEffective(currentEnv, baseFail, baseTurns);
        effFail = Mathf.Round(Mathf.Clamp(effFail, 0f, 100f));

        string title = "Gathering Blocked";
        string body =
            $"Your people feel risk-averse right now (Discovery {dPct}%).\n\n" +
            $"This gathering attempt has a high failure chance ({effFail}%). " +
            $"Raise Discovery by succeeding at safer tasks or let it recover over time, " +
            $"then try again.";

        if (lowDiscoveryPopup)
            lowDiscoveryPopup.Show(title, body);
        else
            Debug.LogWarning("[DiscoveredTilePanel] lowDiscoveryPopup not set.");
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

        root.SetActive(false);
        currentEnv = null;
        OnClose?.Invoke();
        surveyPanel.Hide();
    }

    private void BeginRename()
    {
        if (currentEnv == null) return;
        renameContainer.SetActive(true);
        renameButton.gameObject.SetActive(false);
        renameInputField.text = currentEnv.environmentName;
        renameInputField.ActivateInputField();
    }

    private void SubmitRename()
    {
        if (currentEnv == null) return;
        var newName = renameInputField.text.Trim();
        if (!string.IsNullOrEmpty(newName))
        {
            currentEnv.environmentName = newName;
            titleText.text = newName;
        }
        renameContainer.SetActive(false);
        renameButton.gameObject.SetActive(true);
    }

    private void CancelRename()
    {
        renameContainer.SetActive(false);
        renameButton.gameObject.SetActive(true);
    }

    private void OnSurveyClicked()
    {
        if (currentEnv == null)
            return;

        if (TutorialSurveyOverride != null && TutorialSurveyOverride.Invoke(currentEnv))
            return;

        if (currentEnv.IsSurveyed)
        {
            var node = currentEnv.GetComponent<EnvironmentResourceNode>();
            surveyPanel.Show(node);
            return;
        }

        int available = PlayersPopulationManager.Instance?.GetAvailableTaskPopulation() ?? 0;
        if (available < currentEnv.requireSurveyPopulation)
        {
            detailsPanel.ShowFor(currentEnv, showNoResources: false);
            return;
        }

        bool started = PlayerSurveyManager.Instance?.StartSurvey(currentEnv) ?? false;
        if (started)
        {
            ShowSurveyProgressUI();
        }
        else
        {
            detailsPanel.ShowFor(currentEnv, showNoResources: true);
        }
    }

    private void ShowSurveyProgressUI()
    {
        if (currentEnv == null) return;

        if (surveyOb != null)
        {
            surveyOb.gameObject.SetActive(true);

            if (surveyProgressSlider != null)
            {
                surveyProgressSlider.minValue = 0;
                surveyProgressSlider.maxValue = currentEnv.surveyTurnsRequired;
                surveyProgressSlider.value    = currentEnv.surveyTurnsLeft;
                surveyProgressSlider.wholeNumbers  = true;
                surveyProgressSlider.interactable  = false;
            }
        }

        if (resurveyOb != null) resurveyOb.gameObject.SetActive(false);

        if (currentEnv.isSurveying)        surveyButton.interactable = false;
        if (currentEnv.isSurveying)        buildButton.interactable  = false;
    }

    private void OnGatherClicked()
    {
        if (currentEnv == null)
        {
            Debug.Log("[Gather] Blocked: currentEnv is null");
            return;
        }

        if (currentEnv.isGathering)
        {
            Debug.Log("[Gather] Blocked: already gathering (currentEnv.isGathering == true)");
            return;
        }

        if (currentEnv == null) return;

        if (TutorialGatherOverride != null && TutorialGatherOverride.Invoke(currentEnv))
            return;

        float failureChance01 = GetGatherFailureChance01(currentEnv);
        if (CivilizationDiscoverySystem.Instance != null &&
            CivilizationDiscoverySystem.Instance.ShouldBlockForRisk(failureChance01))
        {
            Debug.Log($"[Gather] Blocked: risk system blocked it (failChance={(failureChance01 * 100f):0.#}%) -> showing blocked popup");
            ShowGatherBlockedPopup();
            return;
        }

        int required = Mathf.Max(1, currentEnv.requireGatheringPopulation);

        if (PlayerTechBuffs.Instance != null)
        {
            required = PlayerTechBuffs.Instance.GetGatheringRequiredPopEffective(
                currentEnv,
                required
            );
        }

        required = Mathf.Max(1, required);

        int available = PlayersPopulationManager.Instance?.GetAvailableTaskPopulation() ?? 0;
        if (available < required)
        {
            Debug.Log($"[Gather] Blocked: not enough population (available={available}, required={required}) -> showing details panel");
            gatheringDetailsPanel?.ShowFor(currentEnv, showNoResources: false);
            return;
        }

        bool started = PlayerGatheringManager.Instance?.StartGathering(
            currentEnv,
            required
        ) ?? false;

        if (started)
        {
            Debug.Log("[Gather] Started: StartGathering returned true");

            if (gatherButton != null) gatherButton.interactable = false;

            if (gatheringOb != null)
            {
                gatheringOb.SetActive(true);
                if (gatheringProgressSlider != null)
                {
                    gatheringProgressSlider.minValue = 0;
                    gatheringProgressSlider.maxValue = currentEnv.gatheringTurnsRequired;
                    gatheringProgressSlider.value = currentEnv.gatheringTurnsLeft;
                    gatheringProgressSlider.wholeNumbers = true;
                    gatheringProgressSlider.direction = Slider.Direction.RightToLeft;
                    gatheringProgressSlider.interactable = false;
                }
            }
        }
        else
        {
            Debug.Log("[Gather] Blocked: StartGathering returned false -> showing details panel");
            gatheringDetailsPanel?.ShowFor(currentEnv, showNoResources: true);
        }

        if (currentEnv.isGathering)
        {
            Debug.Log("[Gather] UI: disabling build button because gathering is active");
            buildButton.interactable = false;
        }
    }

    private void OnClearClicked()
    {
        if (currentEnv == null) return;

        // Must be discovered to clear
        if (!currentEnv.IsDiscovered)
        {
            clearingDetailsPanel?.ShowFor(currentEnv);
            return;
        }

        // --- Calculate clearing cost for UI + gating ---
        int clearingTurns = ClearingTurnCalculator.CalculateClearingTurns(
            currentEnv.environmentType,
            currentEnv.environmentTileType,
            currentEnv.tileSize
        );

        int requiredPop = ClearingPopulationRequirementCalculator.CalculateRequiredPopulation(
            currentEnv.environmentType,
            currentEnv.environmentTileType,
            currentEnv.tileSize
        );

        // Check population availability
        int available = PlayersPopulationManager.Instance?.GetAvailableTaskPopulation() ?? 0;
        if (available < requiredPop)
        {
            // Not enough people – show details popup (turns + pop req)
            clearingDetailsPanel?.ShowFor(currentEnv);
            return;
        }

        // Try to start clearing via the manager
        bool started = PlayerEnvironmentClearingManager.Instance?.StartClearing(currentEnv) ?? false;

        if (!started)
        {
            // Something failed (no prefab, already clearing, etc.)
            clearingDetailsPanel?.ShowFor(currentEnv);
            return;
        }

        Hide();
    }
}