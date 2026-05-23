using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OfferingItemUI : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text amountText;

    [Header("Actions")]
    [SerializeField] private Button selectButton;

    public void Bind(ResourceAmount resource, Action onSelect)
    {
        if (icon != null)
            icon.sprite = resource.resource?.resourceIcon;

        if (nameText != null)
            nameText.text = resource.resource?.resourceName;

        if (amountText != null)
            amountText.text = resource.amount.ToString();

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onSelect?.Invoke());
        }
    }
}
