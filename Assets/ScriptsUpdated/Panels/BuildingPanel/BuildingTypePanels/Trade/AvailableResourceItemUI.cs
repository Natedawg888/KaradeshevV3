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
    [SerializeField] private Button increaseButton;
    [SerializeField] private Button decreaseButton;
    [SerializeField] private Button confirmButton;

    private ResourceDefinition _def;
    private int _maxAmount;
    private int _stagedAmount;
    private Action<ResourceDefinition, int> _onConfirm;

    public void Bind(InventoryStack stack, int currentOffered, Action<ResourceDefinition, int> onConfirm)
    {
        _def = stack.definition;
        _maxAmount = stack.amount;
        _stagedAmount = currentOffered;
        _onConfirm = onConfirm;

        if (icon != null)
            icon.sprite = _def?.resourceIcon;

        if (nameText != null)
            nameText.text = _def?.resourceName;

        RefreshDisplay();

        if (increaseButton != null)
        {
            increaseButton.onClick.RemoveAllListeners();
            increaseButton.onClick.AddListener(Increase);
        }

        if (decreaseButton != null)
        {
            decreaseButton.onClick.RemoveAllListeners();
            decreaseButton.onClick.AddListener(Decrease);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(Confirm);
        }
    }

    private void Increase()
    {
        if (_stagedAmount >= _maxAmount) return;
        _stagedAmount++;
        RefreshDisplay();
    }

    private void Decrease()
    {
        if (_stagedAmount <= 0) return;
        _stagedAmount--;
        RefreshDisplay();
    }

    private void Confirm() => _onConfirm?.Invoke(_def, _stagedAmount);

    private void RefreshDisplay()
    {
        if (amountText != null)
            amountText.text = $"{_stagedAmount} / {_maxAmount}";
    }
}
