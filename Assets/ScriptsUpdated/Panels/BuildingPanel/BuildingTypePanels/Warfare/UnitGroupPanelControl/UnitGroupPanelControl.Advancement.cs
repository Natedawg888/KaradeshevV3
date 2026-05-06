using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class UnitGroupPanelControl : MonoBehaviour
{
    [Header("Advancement UI")]
    public Button advanceButton;
    public Button advanceCancelButton;              // cancel/close button
    public GameObject advancementPanelRoot;
    public Transform advancementChoicesContentRoot;
    public UnitGroupAdvanceChoiceItem advancementChoicePrefab;

    // Call this from your main Awake / Setup method:
    private void SetupAdvancementUI()
    {
        if (advanceButton != null)
        {
            advanceButton.onClick.RemoveAllListeners();
            advanceButton.onClick.AddListener(BeginAdvancement);
        }

        // cancel / close button
        if (advanceCancelButton != null)
        {
            advanceCancelButton.onClick.RemoveAllListeners();
            advanceCancelButton.onClick.AddListener(CloseAdvancementPanel);
        }

        if (advancementPanelRoot != null)
            advancementPanelRoot.SetActive(false);
    }

    private void UpdateAdvanceButtonState()
    {
        if (advanceButton == null)
            return;

        if (_group == null || _group.unitType == null || _trainerContext == null)
        {
            advanceButton.interactable = false;
            return;
        }

        var unit = _group.unitType;
        bool atMaxSkill     = _group.skillLevel >= Mathf.Max(1, unit.maxSkillLevel);
        bool hasValidTarget = HasAnyValidAdvancementTarget(_group);

        // Must be at max skill AND have at least one valid + known target
        advanceButton.interactable = atMaxSkill && hasValidTarget;
    }

    private bool HasAnyValidAdvancementTarget(TileUnitGroupData group)
    {
        if (group == null || group.unitType == null)
            return false;

        var unit     = group.unitType;
        var knownMgr = PlayerKnownUnitsManager.Instance;

        if (unit.advancementOptions == null || unit.advancementOptions.Count == 0)
            return false;

        for (int i = 0; i < unit.advancementOptions.Count; i++)
        {
            var opt = unit.advancementOptions[i];
            if (opt == null || opt.targetUnit == null) 
                continue;

            // Player must know the target unit
            if (knownMgr != null && !knownMgr.IsKnown(opt.targetUnit))
                continue;

            if (IsValidAdvancementTarget(group, opt))
                return true;
        }
        return false;
    }

    private bool IsValidAdvancementTarget(TileUnitGroupData group, MilitiaUnitAdvancementOption option)
    {
        if (group == null || group.unitType == null || option == null || option.targetUnit == null)
            return false;

        var source = group.unitType;
        var target = option.targetUnit;

        int   currentHealth   = source.maxHealth + group.bonusHealth;
        float currentMove     = source.movementSpeed + group.bonusMovementSpeed;
        int   currentPower    = source.power + group.bonusPower;
        int   currentDefense  = source.defense + group.bonusDefense;
        int   currentAgility  = source.agility + group.bonusAgility;
        int   currentAccuracy = source.accuracy + group.bonusAccuracy;
        int   currentRange    = source.range + group.bonusRange;
        int   currentStealth  = source.stealth + group.bonusStealth;

        bool hasCustomReq =
            option.requireHealth   ||
            option.requireMovement ||
            option.requirePower    ||
            option.requireDefense  ||
            option.requireAgility  ||
            option.requireAccuracy ||
            option.requireRange    ||
            option.requireStealth;

        if (hasCustomReq)
        {
            if (option.requireHealth   && currentHealth   < option.minHealth)    return false;
            if (option.requireMovement && currentMove     < option.minMovement)  return false;
            if (option.requirePower    && currentPower    < option.minPower)     return false;
            if (option.requireDefense  && currentDefense  < option.minDefense)   return false;
            if (option.requireAgility  && currentAgility  < option.minAgility)   return false;
            if (option.requireAccuracy && currentAccuracy < option.minAccuracy)  return false;
            if (option.requireRange    && currentRange    < option.minRange)     return false;
            if (option.requireStealth  && currentStealth  < option.minStealth)   return false;

            return true;
        }
        else
        {
            // Fallback: old behaviour – gate based on target unit's base combat stats
            if (currentPower    < target.power)          return false;
            if (currentDefense  < target.defense)        return false;
            if (currentAgility  < target.agility)        return false;
            if (currentAccuracy < target.accuracy)       return false;
            if (currentRange    < target.range)          return false;
            if (currentStealth  < target.stealth)        return false;

            return true;
        }
    }

    private void BeginAdvancement()
    {
        if (_group == null || _group.unitType == null || _trainerContext == null)
            return;

        // If somehow there are no valid + known targets, bail
        if (!HasAnyValidAdvancementTarget(_group))
            return;

        if (advancementPanelRoot != null)
            advancementPanelRoot.SetActive(true);

        RebuildAdvancementChoices();
    }

    private void RebuildAdvancementChoices()
    {
        if (advancementChoicesContentRoot == null || advancementChoicePrefab == null)
            return;

        // clear old items
        for (int i = advancementChoicesContentRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(advancementChoicesContentRoot.GetChild(i).gameObject);
        }

        var unit     = _group.unitType;
        var knownMgr = PlayerKnownUnitsManager.Instance;

        if (unit.advancementOptions == null || unit.advancementOptions.Count == 0)
            return;

        for (int i = 0; i < unit.advancementOptions.Count; i++)
        {
            var opt = unit.advancementOptions[i];
            if (opt == null || opt.targetUnit == null) 
                continue;

            var target = opt.targetUnit;

            // Only show choices for units the player actually knows
            if (knownMgr != null && !knownMgr.IsKnown(target))
                continue;

            if (!IsValidAdvancementTarget(_group, opt))
                continue;

            var item = Instantiate(advancementChoicePrefab, advancementChoicesContentRoot);
            item.Setup(target, _group, _owner, _trainerContext, this);
        }
    }

    public void CloseAdvancementPanel()
    {
        if (advancementPanelRoot != null)
            advancementPanelRoot.SetActive(false);

        if (advancementChoicesContentRoot != null)
        {
            for (int i = advancementChoicesContentRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(advancementChoicesContentRoot.GetChild(i).gameObject);
            }
        }
    }
}