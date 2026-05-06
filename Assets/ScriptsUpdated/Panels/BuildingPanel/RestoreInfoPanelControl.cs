using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class RestoreInfoPanelControl : MonoBehaviour
{
    [Header("Root")]
    public GameObject root;

    [Header("Controls")]
    public Button closeButton; // NEW

    [Header("Info")]
    public TMP_Text turnsText;
    public TMP_Text populationText;

    [Header("Costs UI")]
    public GameObject costsRoot;
    public Transform costsContentRoot;
    public BuildingCostEntry costEntryPrefab;

    public bool CanAffordNow { get; private set; } = true;
    public event Action OnClosed;

    private readonly List<ResourceCost> _lastCosts = new();
    private int _lastTurns;
    private int _lastPop;

    private void Awake()
    {
        if (root) root.SetActive(false);
        if (costsRoot) costsRoot.SetActive(false);

        if (closeButton)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() =>
            {
                Hide();
                OnClosed?.Invoke();
            });
        }
    }

    public void Show(List<ResourceCost> costs, int turns, int pop)
    {
        // Make sure the component object is active (root visibility still controlled by 'root').
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        _lastCosts.Clear();
        if (costs != null) _lastCosts.AddRange(costs);
        _lastTurns = turns;
        _lastPop   = pop;

        if (turnsText)      turnsText.text      = $"{Mathf.Max(1, turns)}";
        if (populationText) populationText.text = $"{Mathf.Max(1, pop)}";

        RebuildCosts();
        if (root) root.SetActive(true);
    }

    public void Hide()
    {
        if (root) root.SetActive(false);
        ClearCosts();
    }

    /// <summary>Rebuild cost entries and recompute affordability.</summary>
    public void RebuildCosts()
    {
        ClearCosts();

        if (costsRoot) costsRoot.SetActive(false);

        if (costEntryPrefab && costsContentRoot && _lastCosts != null && _lastCosts.Count > 0)
        {
            if (costsRoot) costsRoot.SetActive(true);

            bool afford = true;

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
            // No costs → trivially affordable
            CanAffordNow = true;
        }

        // Keep the numbers visible even when there are zero cost lines.
        if (turnsText)      turnsText.text      = $"{Mathf.Max(1, _lastTurns)}";
        if (populationText) populationText.text = $"{Mathf.Max(1, _lastPop)}";
    }

    private void ClearCosts()
    {
        if (!costsContentRoot) return;
        for (int i = costsContentRoot.childCount - 1; i >= 0; i--)
        {
            var child = costsContentRoot.GetChild(i);
            if (child) Destroy(child.gameObject);
        }
    }
}