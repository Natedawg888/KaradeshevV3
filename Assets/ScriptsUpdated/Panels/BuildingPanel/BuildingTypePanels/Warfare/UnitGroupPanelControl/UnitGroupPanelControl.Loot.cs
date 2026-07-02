using System;
using System.Collections.Generic;
using UnityEngine;

public partial class UnitGroupPanelControl : MonoBehaviour
{
    private readonly List<(ResourceDefinition def, int amount)> _leftBehindUnitLootBuffer = new();

    private void SetupLootUI()
    {
        if (lootCloseButton != null)
        {
            lootCloseButton.onClick.RemoveAllListeners();
            lootCloseButton.onClick.AddListener(() => CloseLootPanel(discardLeftovers: true));
        }

        if (lootPanelRoot != null)
            lootPanelRoot.SetActive(false);
    }

    private void ToggleLootPanel()
    {
        if (lootPanelRoot == null) return;

        if (lootPanelRoot.activeSelf)
        {
            CloseLootPanel(discardLeftovers: true);
            return;
        }

        OpenLootPanel();
    }

    private void OpenLootPanel()
    {
        if (_group == null || !_group.HasPendingLoot || lootPanelRoot == null || lootListContentRoot == null || collectedItemPrefab == null)
            return;

        if (actionPanelRoot) actionPanelRoot.SetActive(false);
        if (trackingResultsPanelRoot) trackingResultsPanelRoot.SetActive(false);
        if (scoutResultsPanelRoot) scoutResultsPanelRoot.SetActive(false);
        if (meleeTargetsPanelRoot) meleeTargetsPanelRoot.SetActive(false);
        if (inCombatPanelRoot) inCombatPanelRoot.SetActive(false);

        RebuildLootList();
        lootPanelRoot.SetActive(true);
    }

    private void CloseLootPanel(bool discardLeftovers)
    {
        if (discardLeftovers && _group != null && _group.HasPendingLoot)
        {
            _leftBehindUnitLootBuffer.Clear();

            for (int i = 0; i < _group.pendingLoot.Count; i++)
            {
                var stack = _group.pendingLoot[i];
                if (stack.resource == null || stack.amount <= 0)
                    continue;

                _leftBehindUnitLootBuffer.Add((stack.resource, stack.amount));
            }

            if (_leftBehindUnitLootBuffer.Count > 0)
            {
                var religion = PlayerReligionManager.Instance;
                if (religion != null)
                    religion.NotifyUnitLootLeftBehind(_leftBehindUnitLootBuffer);
            }
        }

        if (lootPanelRoot)
            lootPanelRoot.SetActive(false);

        if (discardLeftovers && _group != null)
            _group.ClearPendingLoot();

        UpdateActionButtonState();
    }

    private void RebuildLootList()
    {
        if (_group == null || lootListContentRoot == null || collectedItemPrefab == null)
            return;

        foreach (Transform child in lootListContentRoot)
            Destroy(child.gameObject);

        if (!_group.HasPendingLoot)
            return;

        for (int i = 0; i < _group.pendingLoot.Count; i++)
        {
            var stack = _group.pendingLoot[i];
            if (stack.resource == null || stack.amount <= 0) continue;

            var resource = stack.resource;

            var item = Instantiate(collectedItemPrefab, lootListContentRoot);
            item.BindLoot(resource, stack.amount, desired => TryTakeLoot(resource, desired), OnLootChanged);
        }
    }

    private int TryTakeLoot(ResourceDefinition def, int desired)
    {
        if (_group == null || def == null || desired <= 0)
            return 0;

        int available = 0;
        for (int i = 0; i < _group.pendingLoot.Count; i++)
        {
            var s = _group.pendingLoot[i];
            if (s.resource == def)
            {
                available = Mathf.Max(0, s.amount);
                break;
            }
        }

        int want = Mathf.Clamp(desired, 0, available);
        if (want <= 0) return 0;

        int added = TryAddToPlayerInventory(def, want);
        if (added <= 0) return 0;

        for (int i = 0; i < _group.pendingLoot.Count; i++)
        {
            var s = _group.pendingLoot[i];
            if (s.resource != def) continue;

            s.amount -= added;

            if (s.amount <= 0)
                _group.pendingLoot.RemoveAt(i);
            else
                _group.pendingLoot[i] = s;

            break;
        }

        return added;
    }

    private int TryAddToPlayerInventory(ResourceDefinition def, int desired)
    {
        var inv = PlayerInventoryManager.Instance;
        if (inv == null || def == null || desired <= 0) return 0;

        float perUnitSpace = def.weightPerUnit * def.sizePerUnit;
        if (perUnitSpace <= 0f)
            return inv.TryAdd(def, desired) ? desired : 0;

        float cap = inv.GetMaxSpace(def.resourceType);
        float used = inv.GetUsedSpace(def.resourceType);

        int maxBySpace = Mathf.FloorToInt((cap - used) / perUnitSpace);
        int want = Mathf.Clamp(desired, 0, Mathf.Max(0, maxBySpace));

        if (want <= 0) return 0;

        while (want > 0 && !inv.TryAdd(def, want))
            want--;

        return want;
    }

    private void OnLootChanged()
    {
        if (_group == null) return;

        if (!_group.HasPendingLoot)
        {
            CloseLootPanel(discardLeftovers: false);
            return;
        }

        RebuildLootList();
    }

    public void OpenTutorialLootPanel()
    {
        OpenLootPanel();
    }

    public void TakeAllTutorialLoot()
    {
        if (_group == null || !_group.HasPendingLoot) return;

        var toTake = new System.Collections.Generic.List<(ResourceDefinition res, int amount)>();
        for (int i = 0; i < _group.pendingLoot.Count; i++)
        {
            var stack = _group.pendingLoot[i];
            if (stack.resource != null && stack.amount > 0)
                toTake.Add((stack.resource, stack.amount));
        }

        foreach (var (res, amt) in toTake)
            TryTakeLoot(res, amt);

        CloseLootPanel(discardLeftovers: true);
    }
}