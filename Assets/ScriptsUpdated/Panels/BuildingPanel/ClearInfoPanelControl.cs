using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ClearInfoPanelControl : MonoBehaviour
{
    [Header("Root")]
    public GameObject root;

    [Header("Controls")]
    public Button closeButton;
    public Button showCostsButton;
    public Button showRewardsButton;

    [Header("Info")]
    public TMP_Text turnsText;                // manualClearTurns
    public TMP_Text populationText;           // manualClearPopulation
    public TMP_Text autoClearRemainingText;   // status.AutoClearTurnsRemaining

    [Header("Costs UI")]
    public GameObject costsRoot;
    public Transform costsContentRoot;
    public BuildingCostEntry costEntryPrefab;

    [Header("Rewards UI")]
    public GameObject rewardsRoot;
    public Transform rewardsContentRoot;
    public BuildingRewardEntry rewardEntryPrefab;

    // State
    private readonly List<ResourceCost> _lastCosts = new();
    private readonly List<ResourceAmount> _lastRewards = new();
    private int _lastTurns;
    private int _lastPop;
    private int _lastAutoClearRemaining;

    public bool CanAffordNow { get; private set; } = true;
    public event Action OnClosed;

    private void Awake()
    {
        if (root) root.SetActive(false);
        if (costsRoot) costsRoot.SetActive(false);
        if (rewardsRoot) rewardsRoot.SetActive(false);

        if (closeButton)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() =>
            {
                Hide();
                OnClosed?.Invoke();
            });
        }

        if (showCostsButton)
        {
            showCostsButton.onClick.RemoveAllListeners();
            showCostsButton.onClick.AddListener(() => ShowTabCosts());
        }

        if (showRewardsButton)
        {
            showRewardsButton.onClick.RemoveAllListeners();
            showRewardsButton.onClick.AddListener(() => ShowTabRewards());
        }
    }

    public void Show(
        List<ResourceCost> costs,
        List<ResourceAmount> rewards,
        int manualClearTurns,
        int manualClearPopulation,
        int autoClearTurnsRemaining)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        _lastCosts.Clear();
        if (costs != null) _lastCosts.AddRange(costs);

        _lastRewards.Clear();
        if (rewards != null) _lastRewards.AddRange(rewards);

        _lastTurns = Mathf.Max(0, manualClearTurns);
        _lastPop   = Mathf.Max(0, manualClearPopulation);
        _lastAutoClearRemaining = Mathf.Max(0, autoClearTurnsRemaining);

        // Header info
        if (turnsText)              turnsText.text = $"{_lastTurns}";
        if (populationText)         populationText.text = $"{_lastPop}";
        if (autoClearRemainingText) autoClearRemainingText.text = 
            (_lastAutoClearRemaining == int.MaxValue) ? "∞" : $"{_lastAutoClearRemaining}";

        // Build default view: Costs first
        RebuildCosts();
        RebuildRewards();
        ShowTabCosts();

        if (root) root.SetActive(true);
    }

    public void Hide()
    {
        if (root) root.SetActive(false);
        ClearCosts();
        ClearRewards();
    }

    private void ShowTabCosts()
    {
        if (costsRoot)   costsRoot.SetActive(true);
        if (rewardsRoot) rewardsRoot.SetActive(false);
    }

    private void ShowTabRewards()
    {
        if (costsRoot)   costsRoot.SetActive(false);
        if (rewardsRoot) rewardsRoot.SetActive(true);
    }

    // ---------- Costs ----------
    private void RebuildCosts()
    {
        ClearCosts();
        if (!costEntryPrefab || !costsContentRoot)
        {
            CanAffordNow = true;
            return;
        }

        bool afford = true;

        if (_lastCosts != null && _lastCosts.Count > 0)
        {
            for (int i = 0; i < _lastCosts.Count; i++)
            {
                var line = _lastCosts[i];
                if (line == null || line.resource == null) continue;

                int have = InventoryQuery.GetOwned(line.resource);
                var entry = Instantiate(costEntryPrefab, costsContentRoot);
                entry.Bind(line.resource, line.amount, have);
                if (have < line.amount) afford = false;
            }
            CanAffordNow = afford;
        }
        else
        {
            // No costs: trivially affordable
            CanAffordNow = true;
        }
    }

    private void ClearCosts()
    {
        if (!costsContentRoot) return;
        for (int i = costsContentRoot.childCount - 1; i >= 0; i--)
            Destroy(costsContentRoot.GetChild(i).gameObject);
    }

    // ---------- Rewards ----------
    private void RebuildRewards()
    {
        ClearRewards();
        if (!rewardEntryPrefab || !rewardsContentRoot) return;

        if (_lastRewards != null && _lastRewards.Count > 0)
        {
            for (int i = 0; i < _lastRewards.Count; i++)
            {
                var r = _lastRewards[i];
                if (r == null || r.resource == null || r.amount <= 0) continue;

                var entry = Instantiate(rewardEntryPrefab, rewardsContentRoot);
                entry.Bind(r.resource, r.amount);
            }
        }
    }

    private void ClearRewards()
    {
        if (!rewardsContentRoot) return;
        for (int i = rewardsContentRoot.childCount - 1; i >= 0; i--)
            Destroy(rewardsContentRoot.GetChild(i).gameObject);
    }
}
