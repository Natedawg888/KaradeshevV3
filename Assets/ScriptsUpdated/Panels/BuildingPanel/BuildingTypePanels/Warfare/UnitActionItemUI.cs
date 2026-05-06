using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

public class UnitActionItemUI : MonoBehaviour
{
    [Header("Visuals")]
    public Image iconImage;
    public TMP_Text nameLabel;

    [Header("Buttons")]
    public Button confirmButton;

    [Header("Scouting Info")]
    public GameObject scoutingValueRoot;   // root object to enable/disable
    public TMP_Text  scoutingValueLabel;   // text showing scouting stats

    private UnitActionDefinitionSO _action;
    private TileUnitGroupData      _group;

    // Old signature kept for backwards compatibility
    public void Setup(UnitActionDefinitionSO action, UnityAction onConfirm)
    {
        Setup(action, null, onConfirm);
    }

    // New overload that also receives the group (so we can read its stats)
    public void Setup(UnitActionDefinitionSO action, TileUnitGroupData group, UnityAction onConfirm)
    {
        _action = action;
        _group  = group;

        if (nameLabel != null)
        {
            string label = !string.IsNullOrEmpty(action.displayName)
                ? action.displayName
                : action.actionID;

            nameLabel.text = label;
        }

        if (iconImage != null)
        {
            iconImage.sprite = action.icon;
            iconImage.gameObject.SetActive(action.icon != null);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            if (onConfirm != null)
                confirmButton.onClick.AddListener(onConfirm);
        }

        UpdateScoutingInfo();
    }

    private void UpdateScoutingInfo()
    {
        if (scoutingValueRoot == null)
            return;

        // Only show this for scouting actions
        var scoutAction = _action as ScoutTileActionSO;
        if (scoutAction == null || _group == null || _group.unitType == null)
        {
            scoutingValueRoot.SetActive(false);
            return;
        }

        var unit = _group.unitType;

        // Same values we use in ScoutTileActionSO.GetTurnCost
        float acc   = unit.accuracy       + _group.bonusAccuracy;
        float range = unit.range          + _group.bonusRange;
        float move  = unit.movementSpeed  + _group.bonusMovementSpeed;

        float statScore =
            acc   * scoutAction.accuracyWeight +
            range * scoutAction.rangeWeight +
            move  * scoutAction.movementWeight;

        if (scoutingValueLabel != null)
        {
            // You can format this however you like
            scoutingValueLabel.text =
                $"{statScore:0.0}";
        }

        scoutingValueRoot.SetActive(true);
    }
}