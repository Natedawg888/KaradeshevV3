using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One row in the tech panel building list.
/// Shows icon and name; the button opens the building detail panel.
/// </summary>
public class TechBuildingEntryUI : MonoBehaviour
{
    [Header("UI")]
    public Image icon;
    public TMP_Text nameText;
    public Button detailButton;

    private Building _building;
    private Action<Building> _onClicked;

    public void Bind(Building building, Action<Building> onClicked)
    {
        _building  = building;
        _onClicked = onClicked;

        if (nameText) nameText.text = building.buildingName ?? building.buildingID;

        if (icon)
        {
            icon.sprite  = building.buildingIcon;
            icon.enabled = building.buildingIcon != null;
        }

        if (detailButton)
        {
            detailButton.gameObject.SetActive(onClicked != null);
            detailButton.onClick.RemoveAllListeners();
            if (onClicked != null) detailButton.onClick.AddListener(OnClicked);
        }
    }

    private void OnClicked() => _onClicked?.Invoke(_building);
}
