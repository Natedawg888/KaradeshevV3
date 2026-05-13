using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One row in the tech panel unit list.
/// Shows icon and name; the button opens the unit detail panel.
/// </summary>
public class TechUnitEntryUI : MonoBehaviour
{
    [Header("UI")]
    public Image icon;
    public TMP_Text nameText;
    public Button detailButton;

    private MilitiaUnit _unit;
    private Action<MilitiaUnit> _onClicked;

    public void Bind(MilitiaUnit unit, Action<MilitiaUnit> onClicked)
    {
        _unit      = unit;
        _onClicked = onClicked;

        if (nameText) nameText.text = unit.unitName ?? unit.unitID;

        if (icon)
        {
            icon.sprite  = unit.unitIcon;
            icon.enabled = unit.unitIcon != null;
        }

        if (detailButton)
        {
            detailButton.gameObject.SetActive(onClicked != null);
            detailButton.onClick.RemoveAllListeners();
            if (onClicked != null) detailButton.onClick.AddListener(OnClicked);
        }
    }

    private void OnClicked() => _onClicked?.Invoke(_unit);
}
