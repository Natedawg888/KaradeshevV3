using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One row in the tech panel production list.
/// Shows icon and name; the button opens the production detail panel.
/// </summary>
public class TechProductionEntryUI : MonoBehaviour
{
    [Header("UI")]
    public Image icon;
    public TMP_Text nameText;
    public Button detailButton;

    private ProductionPlan _plan;
    private Action<ProductionPlan> _onClicked;

    public void Bind(ProductionPlan plan, Action<ProductionPlan> onClicked)
    {
        _plan      = plan;
        _onClicked = onClicked;

        if (nameText) nameText.text = plan.planName ?? plan.productionID;

        if (icon)
        {
            icon.sprite  = plan.productionIcon;
            icon.enabled = plan.productionIcon != null;
        }

        if (detailButton)
        {
            detailButton.gameObject.SetActive(onClicked != null);
            detailButton.onClick.RemoveAllListeners();
            if (onClicked != null) detailButton.onClick.AddListener(OnClicked);
        }
    }

    private void OnClicked() => _onClicked?.Invoke(_plan);
}
