using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One row in the unit detail action list.
/// Shows action icon and name; the button is reserved for the action detail panel.
/// </summary>
public class TechUnitActionEntryUI : MonoBehaviour
{
    [Header("UI")]
    public Image icon;
    public TMP_Text nameText;
    public Button detailButton;

    private UnitActionDefinitionSO _action;
    private Action<UnitActionDefinitionSO> _onClicked;

    public void Bind(UnitActionDefinitionSO action, Action<UnitActionDefinitionSO> onClicked)
    {
        _action    = action;
        _onClicked = onClicked;

        if (nameText) nameText.text = action.displayName ?? action.actionID;

        if (icon)
        {
            icon.sprite  = action.icon;
            icon.enabled = action.icon != null;
        }

        if (detailButton)
        {
            detailButton.gameObject.SetActive(onClicked != null);
            detailButton.onClick.RemoveAllListeners();
            if (onClicked != null) detailButton.onClick.AddListener(OnClicked);
        }
    }

    private void OnClicked() => _onClicked?.Invoke(_action);
}
