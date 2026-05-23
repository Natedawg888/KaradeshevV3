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
    [SerializeField] private Button takeBackButton;

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

        if (amountText != null)
            amountText.text = entry.amount.ToString();

        if (takeBackButton != null)
        {
            takeBackButton.onClick.RemoveAllListeners();
            takeBackButton.onClick.AddListener(TakeBack);
        }
    }

    private void TakeBack()
    {
        if (_entry == null) return;
        _entry.amount = 0;
        _onChanged?.Invoke();
    }
}
