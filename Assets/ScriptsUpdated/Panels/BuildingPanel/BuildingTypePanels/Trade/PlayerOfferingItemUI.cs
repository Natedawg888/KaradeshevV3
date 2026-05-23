using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerOfferingItemUI : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text amountText;

    [Header("Actions")]
    [SerializeField] private Button increaseButton;
    [SerializeField] private Button decreaseButton;

    private ResourceAmount _entry;
    private Action _onChanged;

    public void Bind(ResourceAmount entry, Action onChanged)
    {
        _entry = entry;
        _onChanged = onChanged;

        if (icon != null)
            icon.sprite = entry.resource?.resourceIcon;

        if (nameText != null)
            nameText.text = entry.resource?.resourceName;

        RefreshAmountText();

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
    }

    private void Increase()
    {
        if (_entry?.resource == null) return;
        int available = PlayerInventoryManager.Instance?.GetAmount(_entry.resource) ?? 0;
        if (_entry.amount < available)
        {
            _entry.amount++;
            RefreshAmountText();
            _onChanged?.Invoke();
        }
    }

    private void Decrease()
    {
        if (_entry == null) return;
        _entry.amount = Mathf.Max(0, _entry.amount - 1);
        RefreshAmountText();
        _onChanged?.Invoke();
    }

    private void RefreshAmountText()
    {
        if (amountText != null && _entry != null)
            amountText.text = _entry.amount.ToString();
    }
}
