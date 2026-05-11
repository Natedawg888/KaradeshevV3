using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Overlays the discovered tile panel when the tile is on fire.
/// Mirrors BuildingFireOverlayControl but works with EnvironmentFireState.
/// Only shown for discovered tiles — undiscovered tiles use a plain block object.
/// </summary>
public class TileFireOverlayControl : MonoBehaviour
{
    [Header("Root")]
    public GameObject root;

    [Header("Header")]
    public TMP_Text titleText;

    [Header("Cost Section")]
    public GameObject costSection;
    public Transform costsContentRoot;
    public BuildingCostEntry costEntryPrefab;

    [Header("Requirements")]
    public TMP_Text populationText;
    public TMP_Text turnsEstimateText;

    [Header("Fight Progress")]
    public GameObject progressSection;
    public Slider fightProgressSlider;
    public TMP_Text casualtyText;
    public TMP_Text riskText;

    [Header("Buttons")]
    public Button fightButton;
    public Button cancelButton;

    private EnvironmentControl _env;
    private EnvironmentFireState _fireState;
    private readonly List<BuildingCostEntry> _spawned = new();

    private GameObject RootGO => root != null ? root : gameObject;
    public bool IsShowing => RootGO != null && RootGO.activeInHierarchy;

    private void Awake()
    {
        if (fightButton != null)
        {
            fightButton.onClick.RemoveAllListeners();
            fightButton.onClick.AddListener(OnFightClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(OnCancelClicked);
        }

        RootGO.SetActive(false);
    }

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    public void ShowFor(EnvironmentControl env)
    {
        if (!env) return;

        var fireState = env.GetComponent<EnvironmentFireState>();
        if (fireState == null || !fireState.IsOnFire) { Hide(); return; }

        Unsubscribe();

        _env       = env;
        _fireState = fireState;

        _fireState.OnExtinguished  += HandleExtinguished;
        _fireState.OnFightProgress += HandleFightProgress;

        if (titleText != null)
            titleText.text = $"{env.environmentName} is on Fire!";

        RootGO.SetActive(true);
        Refresh();
    }

    public void Hide()
    {
        Unsubscribe();
        ClearCosts();
        _env       = null;
        _fireState = null;
        RootGO.SetActive(false);
    }

    // ------------------------------------------------------------------
    // Internal
    // ------------------------------------------------------------------

    private void Refresh()
    {
        if (_fireState == null) return;
        if (_fireState.IsFighting) ShowFightingPhase();
        else                       ShowIdlePhase();
    }

    private void ShowIdlePhase()
    {
        SetSection(costSection,     true);
        SetSection(progressSection, false);

        if (cancelButton != null) cancelButton.gameObject.SetActive(false);
        if (fightButton  != null) fightButton.gameObject.SetActive(true);

        RebuildCosts();
        RefreshRequirementsText();
        RefreshFightButtonState();
    }

    private void ShowFightingPhase()
    {
        SetSection(costSection,     false);
        SetSection(progressSection, true);

        if (fightButton  != null) fightButton.gameObject.SetActive(false);
        if (cancelButton != null) cancelButton.gameObject.SetActive(true);

        RefreshFightProgress();
    }

    private void RefreshRequirementsText()
    {
        if (_fireState == null) return;

        if (populationText != null)
        {
            int avail = PlayersPopulationManager.Instance != null
                ? PlayersPopulationManager.Instance.GetAvailableTaskPopulation() : 0;
            populationText.text = $"{_fireState.populationRequired} / {avail})";
        }

        if (turnsEstimateText != null)
            turnsEstimateText.text = $"{_fireState.baseFightTurns}";
    }

    private void RefreshFightProgress()
    {
        if (_fireState == null) return;

        if (fightProgressSlider != null)
        {
            int max      = Mathf.Max(1, _fireState.baseFightTurns);
            int progress = Mathf.Clamp(max - _fireState.FightTurnsRemaining, 0, max);

            fightProgressSlider.minValue     = 0;
            fightProgressSlider.maxValue     = max;
            fightProgressSlider.value        = progress;
            fightProgressSlider.wholeNumbers = true;
            fightProgressSlider.interactable = false;
        }

        int active = Mathf.Max(0, _fireState.populationRequired - _fireState.CasualtiesSoFar);
        if (populationText != null)
            populationText.text = $"{active} / {_fireState.populationRequired}";

        if (casualtyText != null)
        {
            casualtyText.text  = $"{_fireState.CasualtiesSoFar}";
            casualtyText.color = _fireState.CasualtiesSoFar > 0 ? Color.red : Color.white;
        }

        if (riskText != null)
        {
            int riskPct = Mathf.RoundToInt(_fireState.CurrentCasualtyChance * 100f);
            riskText.text  = $"{riskPct}%";
            riskText.color = Color.Lerp(Color.green, Color.red, _fireState.CurrentCasualtyChance);
        }
    }

    private void RefreshFightButtonState()
    {
        if (_fireState == null || fightButton == null) return;

        bool canAffordResources = _fireState.CanAffordFight();
        bool canAffordPop       = _fireState.HasEnoughPopulation();

        fightButton.interactable = canAffordResources && canAffordPop;
    }

    private void RebuildCosts()
    {
        ClearCosts();

        if (_fireState == null || costEntryPrefab == null || costsContentRoot == null) return;

        var costs = _fireState.extinguishCost;
        if (costs == null || costs.Count == 0) return;

        for (int i = 0; i < costs.Count; i++)
        {
            var c = costs[i];
            if (c?.resource == null) continue;

            int have  = InventoryQuery.GetOwned(c.resource);
            var entry = Instantiate(costEntryPrefab, costsContentRoot);
            entry.Bind(c.resource, c.amount, have);
            _spawned.Add(entry);
        }
    }

    private void ClearCosts()
    {
        for (int i = 0; i < _spawned.Count; i++)
            if (_spawned[i] != null) Destroy(_spawned[i].gameObject);
        _spawned.Clear();
    }

    // ------------------------------------------------------------------
    // Button handlers
    // ------------------------------------------------------------------

    private void OnFightClicked()
    {
        if (_fireState == null || _fireState.IsFighting) return;

        if (!_fireState.TryBeginFighting())
        {
            RebuildCosts();
            RefreshFightButtonState();
            return;
        }

        ShowFightingPhase();
    }

    private void OnCancelClicked()
    {
        if (_fireState == null) return;
        _fireState.CancelFighting();
        ShowIdlePhase();
    }

    // ------------------------------------------------------------------
    // Event handlers
    // ------------------------------------------------------------------

    private void HandleExtinguished(EnvironmentFireState state) => Hide();

    private void HandleFightProgress(EnvironmentFireState state, int rollResult, int turnsRemaining)
        => Refresh();

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private void Unsubscribe()
    {
        if (_fireState == null) return;
        _fireState.OnExtinguished  -= HandleExtinguished;
        _fireState.OnFightProgress -= HandleFightProgress;
    }

    private void SetSection(GameObject section, bool on)
    {
        if (section != null) section.SetActive(on);
    }
}
