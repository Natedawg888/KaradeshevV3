using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildingFireOverlayControl : MonoBehaviour
{
    [Header("Root")]
    public GameObject root;

    [Header("Header")]
    public TMP_Text titleText;

    [Header("Costs")]
    public Transform costsContentRoot;
    public BuildingCostEntry costEntryPrefab;

    [Header("Actions")]
    public Button extinguishButton;
    public TMP_Text cantAffordHint;

    private BuildingControl _building;
    private BuildingFireState _fireState;
    private readonly List<BuildingCostEntry> _spawned = new();

    private GameObject RootGO => root != null ? root : gameObject;
    public bool IsShowing => RootGO != null && RootGO.activeInHierarchy;

    private void Awake()
    {
        if (extinguishButton != null)
        {
            extinguishButton.onClick.RemoveAllListeners();
            extinguishButton.onClick.AddListener(OnExtinguishClicked);
        }

        RootGO.SetActive(false);
    }

    public void ShowFor(BuildingControl building)
    {
        if (!building) return;

        var fireState = building.GetComponent<BuildingFireState>();
        if (fireState == null || !fireState.IsOnFire)
        {
            Hide();
            return;
        }

        Unsubscribe();

        _building  = building;
        _fireState = fireState;

        _fireState.OnExtinguished += HandleExtinguished;

        if (titleText != null)
            titleText.text = $"{GetBuildingName()} is on Fire!";

        RootGO.SetActive(true);
        RebuildCosts();
    }

    public void Hide()
    {
        Unsubscribe();
        ClearCosts();
        _building  = null;
        _fireState = null;
        RootGO.SetActive(false);
    }

    // ------------------------------------------------------------------

    private void RebuildCosts()
    {
        ClearCosts();

        if (_fireState == null || costEntryPrefab == null || costsContentRoot == null)
            return;

        var costs = _fireState.extinguishCost;
        bool canAfford = true;

        if (costs != null)
        {
            for (int i = 0; i < costs.Count; i++)
            {
                var c = costs[i];
                if (c?.resource == null) continue;

                int have = InventoryQuery.GetOwned(c.resource);
                if (have < c.amount) canAfford = false;

                var entry = Instantiate(costEntryPrefab, costsContentRoot);
                entry.Bind(c.resource, c.amount, have);
                _spawned.Add(entry);
            }
        }

        if (extinguishButton != null)
            extinguishButton.interactable = canAfford;

        if (cantAffordHint != null)
            cantAffordHint.gameObject.SetActive(!canAfford);
    }

    private void ClearCosts()
    {
        for (int i = 0; i < _spawned.Count; i++)
            if (_spawned[i] != null) Destroy(_spawned[i].gameObject);
        _spawned.Clear();
    }

    private void OnExtinguishClicked()
    {
        if (_fireState == null || !_fireState.IsOnFire) return;

        var costs = _fireState.extinguishCost;
        if (costs != null && costs.Count > 0 && !ResourceDeduction.Deduct(costs))
        {
            RebuildCosts();
            return;
        }

        _fireState.Extinguish();
    }

    private void HandleExtinguished(BuildingFireState state)
    {
        Hide();
    }

    private void Unsubscribe()
    {
        if (_fireState != null)
            _fireState.OnExtinguished -= HandleExtinguished;
    }

    private string GetBuildingName()
    {
        if (_building == null) return "Building";
        return !string.IsNullOrWhiteSpace(_building.buildingName)
            ? _building.buildingName
            : _building.gameObject.name;
    }
}
