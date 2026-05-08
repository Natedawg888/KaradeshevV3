using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildingUpgradePanelControl : MonoBehaviour
{
    [Header("Root")]
    public GameObject root;
    public Button closeButton;

    [Header("Selected Upgrade Info")]
    public Image upgradeIcon;
    public TMP_Text upgradeNameText;
    public TMP_Text turnsText;
    public TMP_Text populationText;

    [Header("Costs")]
    public GameObject costsRoot;
    public Transform costsContentRoot;
    public BuildingCostEntry costEntryPrefab;

    [Header("Upgrade Navigation")]
    public Button prevButton;
    public Button nextButton;

    [Header("Cost Set Navigation")]
    public Button prevCostSetButton;
    public Button nextCostSetButton;

    [Header("Confirm")]
    public Button confirmButton;

    // Runtime
    private BuildingControl _sourceBuilding;
    private TileControl _tile;
    private List<Building> _candidates = new();
    private int _index = 0;

    private void Awake()
    {
        if (root) root.SetActive(false);

        if (closeButton)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }

        // Keep existing upgrade navigation
        if (prevButton)
        {
            prevButton.onClick.RemoveAllListeners();
            prevButton.onClick.AddListener(Prev);
        }

        if (nextButton)
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(Next);
        }

        // NEW: separate cost set navigation
        if (prevCostSetButton)
        {
            prevCostSetButton.onClick.RemoveAllListeners();
            prevCostSetButton.onClick.AddListener(PrevCostSet);
        }

        if (nextCostSetButton)
        {
            nextCostSetButton.onClick.RemoveAllListeners();
            nextCostSetButton.onClick.AddListener(NextCostSet);
        }

        if (confirmButton)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnConfirm);
        }
    }

    public void OpenFor(BuildingControl building)
    {
        _sourceBuilding = building;
        _tile = building ? building.GetComponentInParent<TileControl>() : null;

        BuildCandidateList();

        if (_candidates.Count == 0)
        {
            //Debug.LogWarning("[UpgradePanel] No valid upgrade targets.");
            Close();
            return;
        }

        _index = Mathf.Clamp(_index, 0, _candidates.Count - 1);

        InitializeCostSetForCurrentCandidate();

        RebuildUI();
        if (root) root.SetActive(true);
    }

    public void Close()
    {
        if (root) root.SetActive(false);
        ClearCosts();
        _sourceBuilding = null;
        _tile = null;
        _candidates.Clear();
        _index = 0;
    }

    // -------------------------
    // Upgrade navigation
    // -------------------------
    private void Prev()
    {
        if (_candidates.Count == 0) return;

        _index = (_index - 1 + _candidates.Count) % _candidates.Count;
        InitializeCostSetForCurrentCandidate();
        RebuildUI();
    }

    private void Next()
    {
        if (_candidates.Count == 0) return;

        _index = (_index + 1) % _candidates.Count;
        InitializeCostSetForCurrentCandidate();
        RebuildUI();
    }

    // -------------------------
    // Cost set navigation
    // -------------------------
    private void PrevCostSet()
    {
        if (_candidates.Count == 0) return;

        var target = _candidates[_index];
        if (target == null || !target.HasAlternateCostSets) return;

        target.CyclePrevCostSet();
        RebuildUI();
    }

    private void NextCostSet()
    {
        if (_candidates.Count == 0) return;

        var target = _candidates[_index];
        if (target == null || !target.HasAlternateCostSets) return;

        target.CycleNextCostSet();
        RebuildUI();
    }

    private void BuildCandidateList()
    {
        _candidates.Clear();
        if (!_sourceBuilding) return;

        var fromDef = BuildingManager.Instance?.GetBuildingByID(_sourceBuilding.buildingID);
        if (fromDef == null || fromDef.upgradeToIDs == null || fromDef.upgradeToIDs.Count == 0)
            return;

        int playerLevel = PlayerLevel.Instance ? PlayerLevel.Instance.GetCurrentLevel() : int.MaxValue;

        foreach (var id in fromDef.upgradeToIDs)
        {
            if (string.IsNullOrWhiteSpace(id)) continue;

            var def = BuildingManager.Instance?.GetBuildingByID(id);
            if (def == null) continue;
            if (!def.IsAvailableAtLevel(playerLevel)) continue;

            _candidates.Add(def);
        }
    }

    private void InitializeCostSetForCurrentCandidate()
    {
        if (_candidates.Count == 0) return;

        var target = _candidates[_index];
        if (target == null) return;

        int? affordable = target.GetFirstAffordableCostSetIndex();

        if (affordable.HasValue)
        {
            target.SetActiveCostSet(affordable.Value);
        }
        else
        {
            if (target.HasAlternateCostSets)
                target.SetActiveCostSet(0);
            else
                target.SetActiveCostSet(-1);
        }
    }

    private void RebuildUI()
    {
        if (_candidates.Count == 0) return;

        var target = _candidates[_index];
        if (target == null) return;

        // Top info
        if (upgradeNameText) upgradeNameText.text = target.buildingName ?? target.buildingID;
        if (upgradeIcon) upgradeIcon.sprite = target.buildingIcon;
        if (turnsText) turnsText.text = $"{Mathf.Max(1, target.buildTurnsRequired)}";
        if (populationText) populationText.text = $"{Mathf.Max(1, target.requireBuildPopulation)}";

        // Upgrade nav visibility
        bool hasMultipleCandidates = _candidates.Count > 1;
        if (prevButton) prevButton.gameObject.SetActive(hasMultipleCandidates);
        if (nextButton) nextButton.gameObject.SetActive(hasMultipleCandidates);

        // Cost set label + nav
        bool hasMultipleCostSets = target.HasAlternateCostSets &&
                                   target.buildCostSets != null &&
                                   target.buildCostSets.Count > 1;

        if (prevCostSetButton) prevCostSetButton.gameObject.SetActive(hasMultipleCostSets);
        if (nextCostSetButton) nextCostSetButton.gameObject.SetActive(hasMultipleCostSets);

        // Active costs
        var activeCosts = target.GetActiveBuildCosts()?.ToList() ?? new List<ResourceCost>();

        ClearCosts();
        if (costsRoot) costsRoot.SetActive(false);

        if (costEntryPrefab && costsContentRoot && activeCosts.Count > 0)
        {
            if (costsRoot) costsRoot.SetActive(true);

            for (int i = 0; i < activeCosts.Count; i++)
            {
                var c = activeCosts[i];
                if (c == null || c.resource == null || c.amount <= 0) continue;

                int have = InventoryQuery.GetOwned(c.resource);
                var entry = Instantiate(costEntryPrefab, costsContentRoot);
                entry.Bind(c.resource, c.amount, have);
            }
        }

        bool canAfford = InventoryQuery.CanAfford(activeCosts);
        int freePop = PlayersPopulationManager.Instance?.GetAvailableTaskPopulation() ?? 0;
        bool popOK = freePop >= Mathf.Max(1, target.requireBuildPopulation);

        bool blockedBySourceState = IsUpgradeBlockedBySourceState();

        if (confirmButton)
            confirmButton.interactable = canAfford && popOK && !blockedBySourceState;
    }

    private void ClearCosts()
    {
        if (!costsContentRoot) return;

        for (int i = costsContentRoot.childCount - 1; i >= 0; i--)
            Destroy(costsContentRoot.GetChild(i).gameObject);
    }

    private void OnConfirm()
    {
        if (_candidates.Count == 0 || !_sourceBuilding)
            return;

        if (IsUpgradeBlockedBySourceState(out string blockReason))
        {
            //Debug.LogWarning($"[UpgradePanel] {blockReason}");
            return;
        }

        var target = _candidates[_index];
        if (target == null) return;

        var activeCosts = target.GetActiveBuildCosts()?.ToList() ?? new List<ResourceCost>();

        if (!InventoryQuery.CanAfford(activeCosts))
        {
            //Debug.LogWarning($"[UpgradePanel] Cannot afford upgrade cost set '{target.GetActiveCostSetLabel()}'.");
            return;
        }

        int needPop = Mathf.Max(1, target.requireBuildPopulation);
        int avail = PlayersPopulationManager.Instance?.GetAvailableTaskPopulation() ?? 0;

        if (avail < needPop)
        {
            //Debug.LogWarning($"[UpgradePanel] Not enough population (need {needPop}).");
            return;
        }

        if (!SpendCosts(activeCosts))
        {
            //Debug.LogWarning("[UpgradePanel] Spend costs failed.");
            return;
        }

        StartCoroutine(SpawnUpgradeConstructionRoutine(target, activeCosts));
    }

    private System.Collections.IEnumerator SpawnUpgradeConstructionRoutine(Building target, List<ResourceCost> spentCosts)
    {
        if (target == null || target.buildingPrefab == null)
        {
            //Debug.LogError("[UpgradePanel] Target definition missing buildingPrefab.");
            yield break;
        }

        Transform parent = _tile ? _tile.transform : (_sourceBuilding ? _sourceBuilding.transform.parent : null);

        GameObject dummy = Instantiate(target.buildingPrefab);
        if (parent)
        {
            dummy.transform.SetParent(parent, false);
            dummy.transform.localPosition = Vector3.zero;
            dummy.transform.localRotation = Quaternion.identity;
        }

        yield return null;

        Vector3 snapPos = dummy.transform.position;
        Quaternion snapRot = dummy.transform.rotation;
        Destroy(dummy);

        GameObject constructionGO = Instantiate(target.buildingPrefab);
        constructionGO.transform.SetParent(null, false);
        constructionGO.transform.position = snapPos;
        constructionGO.transform.rotation = snapRot;
        constructionGO.transform.localScale = Vector3.one;

        var bc = constructionGO.GetComponent<BuildingConstruction>();
        if (bc) bc.startInMiddle = true;

        bool started = PlayerConstructionManager.Instance?.StartConstruction(
            constructionGO,
            target,
            reservationIdFromPlacement: null,
            reservedPop: Mathf.Max(1, target.requireBuildPopulation),
            turnsRequired: Mathf.Max(1, target.buildTurnsRequired)
        ) ?? false;

        if (!started)
        {
            //Debug.LogWarning("[UpgradePanel] StartConstruction failed; refund and destroy spawned construction.");
            RefundCosts(spentCosts);
            Destroy(constructionGO);
            yield break;
        }

        if (_sourceBuilding)
        {
            var oldGO = _sourceBuilding.gameObject;
            _sourceBuilding = null;
            Destroy(oldGO);
        }

        Close();
    }

    private bool SpendCosts(List<ResourceCost> costs)
    {
        if (costs == null || costs.Count == 0) return true;

        var pim = PlayerInventoryManager.Instance;
        if (!pim)
        {
            //Debug.LogError("[UpgradePanel] No PlayerInventoryManager.");
            return false;
        }

        if (!InventoryQuery.CanAfford(costs)) return false;

        var rollback = new List<ResourceCost>();

        for (int i = 0; i < costs.Count; i++)
        {
            var c = costs[i];
            if (c == null || c.resource == null || c.amount <= 0) continue;

            bool ok = c.resource.isGroup ? pim.TryRemoveGroup(c.resource, c.amount)
                                         : pim.TryRemove(c.resource, c.amount);

            if (!ok)
            {
                for (int r = 0; r < rollback.Count; r++)
                    pim.TryAdd(rollback[r].resource, rollback[r].amount);

                //Debug.LogWarning("[UpgradePanel] Spend mid-transaction failed, rolled back.");
                return false;
            }

            if (!c.resource.isGroup)
                rollback.Add(c);
        }

        return true;
    }

    private void RefundCosts(List<ResourceCost> costs)
    {
        var pim = PlayerInventoryManager.Instance;
        if (!pim || costs == null) return;

        for (int i = 0; i < costs.Count; i++)
        {
            var c = costs[i];
            if (c == null || c.resource == null || c.amount <= 0) continue;

            if (!c.resource.isGroup)
                pim.TryAdd(c.resource, c.amount);
        }
    }

    private bool IsUpgradeBlockedBySourceState(out string reason)
    {
        reason = null;

        if (_sourceBuilding == null)
            return false;

        var storage = _sourceBuilding.GetComponent<StorageBuildingControl>();
        if (storage != null && storage.GetTotalStoredAmount() > 0)
        {
            reason = "Cannot upgrade this storage building while it is storing resources.";
            return true;
        }

        var production = _sourceBuilding.GetComponent<ProductionBuildingControl>();
        if (production != null && production.HasActivePlan)
        {
            reason = "Cannot upgrade this production building while it has an active production plan.";
            return true;
        }

        var crafting = _sourceBuilding.GetComponent<CraftingBuildingControl>();
        if (crafting != null && crafting.HasActiveOrders)
        {
            reason = "Cannot upgrade this crafting building while it has an active crafting order.";
            return true;
        }

        return false;
    }

    private bool IsUpgradeBlockedBySourceState()
    {
        return IsUpgradeBlockedBySourceState(out _);
    }
}
