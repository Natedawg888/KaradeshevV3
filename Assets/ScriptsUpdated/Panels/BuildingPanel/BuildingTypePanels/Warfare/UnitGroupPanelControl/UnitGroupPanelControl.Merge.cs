using UnityEngine;

public partial class UnitGroupPanelControl
{
    private void SetupMergeUI()
    {
        if (mergeButton != null)
        {
            mergeButton.onClick.RemoveAllListeners();
            mergeButton.onClick.AddListener(BeginMerge);
        }

        if (mergePlusButton != null)
        {
            mergePlusButton.onClick.RemoveAllListeners();
            mergePlusButton.onClick.AddListener(() => AdjustMergeAmount(+1));
        }

        if (mergeMinusButton != null)
        {
            mergeMinusButton.onClick.RemoveAllListeners();
            mergeMinusButton.onClick.AddListener(() => AdjustMergeAmount(-1));
        }

        if (mergeAmountConfirmButton != null)
        {
            mergeAmountConfirmButton.onClick.RemoveAllListeners();
            mergeAmountConfirmButton.onClick.AddListener(ConfirmMergeAmount);
        }

        if (mergeCancelButton != null)
        {
            mergeCancelButton.onClick.RemoveAllListeners();
            mergeCancelButton.onClick.AddListener(CancelMerge);
        }
    }

    private void UpdateMergeButtonState()
    {
        if (mergeButton == null) return;

        bool canMerge = false;

        if (_group != null && _owner != null && _group.unitType != null)
        {
            var unit         = _group.unitType;
            int maxGroupSize = unit.maxGroupSize;
            int mySkill      = _group.skillLevel;

            var groups = _owner.Groups;
            for (int i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                if (g == null || g == _group)   continue;
                if (g.unitType != unit)         continue;
                if (g.unitCount <= 0)           continue;
                if (g.skillLevel != mySkill)    continue; // 🔹 MUST match skill level

                if (maxGroupSize > 0)
                {
                    if (_group.unitCount < maxGroupSize)
                    {
                        canMerge = true;
                        break;
                    }
                }
                else
                {
                    canMerge = true;
                    break;
                }
            }
        }

        mergeButton.interactable = canMerge;
    }

    // ---------- Merge Step 1: pick amount ----------

    private void BeginMerge()
    {
        if (_group == null || _owner == null) return;
        if (_group.unitType == null) return;

        var unit    = _group.unitType;
        int capacity = int.MaxValue;

        if (unit.maxGroupSize > 0)
        {
            capacity = Mathf.Max(0, unit.maxGroupSize - _group.unitCount);
            if (capacity <= 0)
            {
                //Debug.Log("[UnitGroupPanel] Cannot merge; group already at or above max size.");
                return;
            }
        }

        _pendingMergeAmount = Mathf.Clamp(_pendingMergeAmount, 1, capacity > 0 ? capacity : int.MaxValue);

        if (mergePanelRoot != null)
            mergePanelRoot.SetActive(true);

        if (mergeAmountControlsRoot != null)
            mergeAmountControlsRoot.SetActive(true);

        if (mergeChoicesRoot != null)
            mergeChoicesRoot.SetActive(false);

        RefreshMergeAmountUI();
        ClearMergeChoices();
    }

    private void AdjustMergeAmount(int delta)
    {
        if (_group == null || _group.unitType == null) return;

        var unit    = _group.unitType;
        int capacity = int.MaxValue;

        if (unit.maxGroupSize > 0)
        {
            capacity = Mathf.Max(0, unit.maxGroupSize - _group.unitCount);
            if (capacity <= 0)
            {
                _pendingMergeAmount = 0;
                RefreshMergeAmountUI();
                return;
            }
        }

        int maxAmount = capacity > 0 ? capacity : int.MaxValue;

        int newAmount = _pendingMergeAmount + delta;
        newAmount     = Mathf.Clamp(newAmount, 1, maxAmount);
        _pendingMergeAmount = newAmount;

        RefreshMergeAmountUI();
    }

    private void RefreshMergeAmountUI()
    {
        if (mergeAmountText != null)
            mergeAmountText.text = _pendingMergeAmount.ToString();

        bool hasAmount = _pendingMergeAmount > 0;

        if (mergeMinusButton != null)
            mergeMinusButton.interactable = _pendingMergeAmount > 1;

        if (mergePlusButton != null)
            mergePlusButton.interactable = hasAmount;

        if (mergeAmountConfirmButton != null)
            mergeAmountConfirmButton.interactable = hasAmount;
    }

    private void ConfirmMergeAmount()
    {
        if (_group == null || _owner == null)
        {
            CancelMerge();
            return;
        }

        if (mergeAmountControlsRoot != null)
            mergeAmountControlsRoot.SetActive(false);

        if (mergeChoicesRoot != null)
            mergeChoicesRoot.SetActive(true);

        RebuildMergeChoicesList();
    }

    // ---------- Merge Step 2: pick target group ----------

    private void ClearMergeChoices()
    {
        if (mergeChoicesContentRoot == null) return;

        for (int i = mergeChoicesContentRoot.childCount - 1; i >= 0; i--)
        {
            var child = mergeChoicesContentRoot.GetChild(i);
            if (child != null)
                Object.Destroy(child.gameObject);
        }
    }

    private void RebuildMergeChoicesList()
    {
        ClearMergeChoices();

        if (_group == null || _owner == null) return;

        if (mergeChoicesContentRoot == null || mergeChoicePrefab == null)
        {
            //Debug.LogWarning("[UnitGroupPanel] Merge choices UI not configured.");
            return;
        }

        var unit = _group.unitType;
        if (unit == null) return;

        int mySkill = _group.skillLevel;

        int capacity = int.MaxValue;
        if (unit.maxGroupSize > 0)
        {
            capacity = Mathf.Max(0, unit.maxGroupSize - _group.unitCount);
            if (capacity <= 0)
                return;
        }

        var groups = _owner.Groups;
        for (int i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            if (g == null)             continue;
            if (g == _group)           continue;
            if (g.unitType != unit)    continue;
            if (g.unitCount <= 0)      continue;
            if (g.skillLevel != mySkill) continue; // 🔹 same skill only

            int maxFromThisGroup = Mathf.Min(g.unitCount, _pendingMergeAmount, capacity);
            if (maxFromThisGroup <= 0)
                continue;

            var item = Object.Instantiate(mergeChoicePrefab, mergeChoicesContentRoot);
            item.Setup(g, this, maxFromThisGroup);
        }
    }

    private void CancelMerge()
    {
        if (mergePanelRoot != null)
            mergePanelRoot.SetActive(false);

        if (mergeAmountControlsRoot != null)
            mergeAmountControlsRoot.SetActive(true);

        if (mergeChoicesRoot != null)
            mergeChoicesRoot.SetActive(false);

        ClearMergeChoices();
    }

    // ---------- Helpers for median ----------

    private int MedianInt(int a, int b)
    {
        long sum = (long)a + b;
        return (int)(sum / 2); // floor median for even
    }

    private int MedianExpiry(int a, int b)
    {
        // -1 means "no expiry"
        if (a < 0 && b < 0) return -1;
        if (a < 0) return b;
        if (b < 0) return a;

        long sum = (long)a + b;
        return (int)(sum / 2);
    }

    // Called by MergeGroupItemUI
    public void ConfirmMergeInto(TileUnitGroupData sourceGroup)
    {
        if (_group == null || _owner == null || sourceGroup == null)
        {
            CancelMerge();
            return;
        }

        if (sourceGroup == _group)
        {
            CancelMerge();
            return;
        }

        var unit = _group.unitType;
        if (unit == null || sourceGroup.unitType != unit)
        {
            //Debug.LogWarning("[UnitGroupPanel] Cannot merge groups with different unit types.");
            CancelMerge();
            return;
        }

        if (sourceGroup.skillLevel != _group.skillLevel)
        {
            //Debug.LogWarning("[UnitGroupPanel] Cannot merge groups with different skill levels.");
            CancelMerge();
            return;
        }

        int capacity = int.MaxValue;
        if (unit.maxGroupSize > 0)
        {
            capacity = Mathf.Max(0, unit.maxGroupSize - _group.unitCount);
            if (capacity <= 0)
            {
                //Debug.Log("[UnitGroupPanel] Merge aborted; no capacity left.");
                CancelMerge();
                return;
            }
        }

        int availableFromSource = sourceGroup.unitCount;
        if (availableFromSource <= 0)
        {
            //Debug.Log("[UnitGroupPanel] Source group has no units to merge.");
            CancelMerge();
            return;
        }

        int desired    = Mathf.Max(1, _pendingMergeAmount);
        int mergeUnits = Mathf.Min(desired, availableFromSource, capacity);
        if (mergeUnits <= 0)
        {
            //Debug.Log("[UnitGroupPanel] MergeUnits computed as zero; aborting.");
            CancelMerge();
            return;
        }

        int originalTargetCount = _group.unitCount;
        int originalSourceCount = sourceGroup.unitCount;

        int newTargetCount = originalTargetCount + mergeUnits;
        int newSourceCount = originalSourceCount - mergeUnits;

        ApplyMergedBonusAverages(_group, originalTargetCount, sourceGroup, mergeUnits);

        // --- Health combining: weighted average of health fractions ---
        float targetFraction = _group.HealthFraction;
        float sourceFraction = sourceGroup.HealthFraction;

        if (!float.IsFinite(targetFraction)) targetFraction = 1f;
        if (!float.IsFinite(sourceFraction)) sourceFraction = 1f;

        float totalUnitsInCombined = originalTargetCount + mergeUnits;
        float combinedFraction     = 1f;

        if (totalUnitsInCombined > 0)
        {
            float targetContribution = targetFraction * originalTargetCount;
            float mergeContribution  = sourceFraction * mergeUnits;
            combinedFraction         = (targetContribution + mergeContribution) / totalUnitsInCombined;
        }

        combinedFraction = Mathf.Clamp01(combinedFraction);

        // --- Apply to target group ---
        _group.unitCount = newTargetCount;
        _group.RecalculateMaxHealth(keepCurrentFraction: false);
        _group.currentHealth = Mathf.Clamp(
            Mathf.RoundToInt(_group.maxHealth * combinedFraction),
            1,
            _group.maxHealth);

        _group.missedUpkeepTurns = Mathf.Clamp(
            MedianInt(_group.missedUpkeepTurns, sourceGroup.missedUpkeepTurns),
            0,
            unit.maxMissedUpkeepTurns);

        _group.expiryTurn = MedianExpiry(_group.expiryTurn, sourceGroup.expiryTurn);

        _group.upkeepStartTurn = MedianInt(_group.upkeepStartTurn, sourceGroup.upkeepStartTurn);

        // --- Adjust / remove source group ---
        if (newSourceCount > 0)
        {
            float remainingFraction = sourceFraction;

            sourceGroup.unitCount = newSourceCount;
            sourceGroup.RecalculateMaxHealth(keepCurrentFraction: false);
            sourceGroup.currentHealth = Mathf.Clamp(
                Mathf.RoundToInt(sourceGroup.maxHealth * remainingFraction),
                1,
                sourceGroup.maxHealth);

            _owner.RefreshMarker(sourceGroup);
        }
        else
        {
            _owner.RemoveGroup(sourceGroup.groupId);
        }

        // Refresh UI + markers for the target
        _owner.RefreshMarker(_group);
        Refresh();

        _kineticPanel?.RefreshForSameBuilding();

        CancelMerge();
    }

    private int WeightedAverageInt(int aValue, int aWeight, int bValue, int bWeight)
    {
        int totalWeight = aWeight + bWeight;
        if (totalWeight <= 0) return 0;

        float weighted = ((aValue * aWeight) + (bValue * bWeight)) / (float)totalWeight;
        return Mathf.RoundToInt(weighted);
    }

    private float WeightedAverageFloat(float aValue, int aWeight, float bValue, int bWeight)
    {
        int totalWeight = aWeight + bWeight;
        if (totalWeight <= 0) return 0f;

        return ((aValue * aWeight) + (bValue * bWeight)) / totalWeight;
    }

    private void ApplyMergedBonusAverages(
        TileUnitGroupData targetGroup,
        int targetUnitsBeforeMerge,
        TileUnitGroupData sourceGroup,
        int sourceUnitsMerged)
    {
        if (targetGroup == null || sourceGroup == null) return;

        targetGroup.bonusHealth = WeightedAverageInt(
            targetGroup.bonusHealth, targetUnitsBeforeMerge,
            sourceGroup.bonusHealth, sourceUnitsMerged);

        targetGroup.bonusMovementSpeed = WeightedAverageFloat(
            targetGroup.bonusMovementSpeed, targetUnitsBeforeMerge,
            sourceGroup.bonusMovementSpeed, sourceUnitsMerged);

        targetGroup.bonusPower = WeightedAverageInt(
            targetGroup.bonusPower, targetUnitsBeforeMerge,
            sourceGroup.bonusPower, sourceUnitsMerged);

        targetGroup.bonusDefense = WeightedAverageInt(
            targetGroup.bonusDefense, targetUnitsBeforeMerge,
            sourceGroup.bonusDefense, sourceUnitsMerged);

        targetGroup.bonusAgility = WeightedAverageInt(
            targetGroup.bonusAgility, targetUnitsBeforeMerge,
            sourceGroup.bonusAgility, sourceUnitsMerged);

        targetGroup.bonusAccuracy = WeightedAverageInt(
            targetGroup.bonusAccuracy, targetUnitsBeforeMerge,
            sourceGroup.bonusAccuracy, sourceUnitsMerged);

        targetGroup.bonusRange = WeightedAverageInt(
            targetGroup.bonusRange, targetUnitsBeforeMerge,
            sourceGroup.bonusRange, sourceUnitsMerged);

        targetGroup.bonusStealth = WeightedAverageInt(
            targetGroup.bonusStealth, targetUnitsBeforeMerge,
            sourceGroup.bonusStealth, sourceUnitsMerged);
    }
}
