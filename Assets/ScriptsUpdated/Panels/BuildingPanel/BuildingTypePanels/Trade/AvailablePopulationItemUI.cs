using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AvailablePopulationItemUI : MonoBehaviour
{
    [Header("Age Group Icons")]
    [SerializeField] private Sprite childSprite;
    [SerializeField] private Sprite teenSprite;
    [SerializeField] private Sprite adultSprite;
    [SerializeField] private Sprite elderSprite;

    [Header("Gender Icons")]
    [SerializeField] private Sprite maleSprite;
    [SerializeField] private Sprite femaleSprite;

    [Header("Display")]
    [SerializeField] private Image ageGroupIcon;
    [SerializeField] private Image genderIcon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text amountText;

    [Header("Actions")]
    [SerializeField] private Button increaseButton;
    [SerializeField] private Button decreaseButton;
    [SerializeField] private Button confirmButton;

    private TradePopulationEntry _source;
    private int _maxAmount;
    private int _stagedAmount;
    private Action<TradePopulationEntry, int> _onConfirm;

    public void Bind(TradePopulationEntry entry, int currentOffered, Action<TradePopulationEntry, int> onConfirm)
    {
        _source = entry;
        _maxAmount = entry.count;
        _stagedAmount = currentOffered;
        _onConfirm = onConfirm;

        if (ageGroupIcon != null)
            ageGroupIcon.sprite = SpriteForAge(entry.ageGroup);

        if (genderIcon != null)
            genderIcon.sprite = SpriteForGender(entry.gender);

        if (nameText != null)
            nameText.text = LabelForAge(entry.ageGroup);

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

    private void Confirm() => _onConfirm?.Invoke(_source, _stagedAmount);

    private void RefreshDisplay()
    {
        if (amountText != null)
            amountText.text = $"{_stagedAmount} / {_maxAmount}";
    }

    private string LabelForAge(AgeGroup age)
    {
        switch (age)
        {
            case AgeGroup.Child: return "Child";
            case AgeGroup.Teen:  return "Teen";
            case AgeGroup.Adult: return "Adult";
            case AgeGroup.Elder: return "Elder";
            default:             return "Adult";
        }
    }

    private Sprite SpriteForAge(AgeGroup age)
    {
        switch (age)
        {
            case AgeGroup.Child: return childSprite;
            case AgeGroup.Teen:  return teenSprite;
            case AgeGroup.Adult: return adultSprite;
            case AgeGroup.Elder: return elderSprite;
            default:             return adultSprite;
        }
    }

    private Sprite SpriteForGender(Gender gender)
    {
        switch (gender)
        {
            case Gender.Male:   return maleSprite;
            case Gender.Female: return femaleSprite;
            default:            return maleSprite;
        }
    }
}
