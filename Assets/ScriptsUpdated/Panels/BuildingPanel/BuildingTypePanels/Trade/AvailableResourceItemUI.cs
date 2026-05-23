using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AvailableResourceItemUI : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text amountText;

    [Header("Actions")]
    [SerializeField] private Button addButton;

    public void Bind(InventoryStack stack, Action<ResourceDefinition> onAdd)
    {
        var def = stack.definition;

        if (icon != null)
            icon.sprite = def?.resourceIcon;

        if (nameText != null)
            nameText.text = def?.resourceName;

        if (amountText != null)
            amountText.text = stack.amount.ToString();

        if (addButton != null)
        {
            addButton.onClick.RemoveAllListeners();
            addButton.onClick.AddListener(() => onAdd?.Invoke(def));
        }
    }
}
