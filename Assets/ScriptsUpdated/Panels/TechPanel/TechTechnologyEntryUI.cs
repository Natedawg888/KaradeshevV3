using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One row in the tech panel technology list.
/// Shows the tech icon and name; the button opens the technology detail panel.
/// </summary>
public class TechTechnologyEntryUI : MonoBehaviour
{
    [Header("UI")]
    public Image    icon;
    public TMP_Text nameText;
    public Button   detailButton;

    private Technology           _tech;
    private Action<Technology>   _onClicked;

    public void Bind(Technology tech, Action<Technology> onClicked)
    {
        _tech      = tech;
        _onClicked = onClicked;

        if (nameText) nameText.text = string.IsNullOrWhiteSpace(tech.techName) ? tech.techID : tech.techName;

        if (icon)
        {
            icon.sprite  = tech.icon;
            icon.enabled = tech.icon != null;
        }

        if (detailButton)
        {
            detailButton.gameObject.SetActive(onClicked != null);
            detailButton.onClick.RemoveAllListeners();
            if (onClicked != null) detailButton.onClick.AddListener(OnClicked);
        }
    }

    private void OnClicked() => _onClicked?.Invoke(_tech);
}
