using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildingRewardEntry : MonoBehaviour
{
    public Image icon;
    public TMP_Text nameText;
    public TMP_Text amountText;

    public void Bind(ResourceDefinition res, int amount)
    {
        if (!res) return;

        if (icon && res.resourceIcon)
            icon.sprite = res.resourceIcon;

        if (nameText)
            nameText.text = string.IsNullOrEmpty(res.resourceName) ? res.name : res.resourceName;

        if (amountText)
            amountText.text = $"{amount}";
    }
}