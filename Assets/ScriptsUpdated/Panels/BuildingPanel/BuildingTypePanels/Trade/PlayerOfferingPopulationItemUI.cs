using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerOfferingPopulationItemUI : MonoBehaviour
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
    [SerializeField] private Button takeBackButton;

    private TradePopulationEntry _entry;
    private Action _onChanged;

    public void Bind(TradePopulationEntry entry, Action onChanged)
    {
        _entry = entry;
        _onChanged = onChanged;

        if (ageGroupIcon != null)
            ageGroupIcon.sprite = SpriteForAge(entry.ageGroup);

        if (genderIcon != null)
            genderIcon.sprite = SpriteForGender(entry.gender);

        if (nameText != null)
            nameText.text = LabelForAge(entry.ageGroup);

        if (amountText != null)
            amountText.text = entry.count.ToString();

        if (takeBackButton != null)
        {
            takeBackButton.onClick.RemoveAllListeners();
            takeBackButton.onClick.AddListener(TakeBack);
        }
    }

    private void TakeBack()
    {
        if (_entry == null) return;
        _entry.count = 0;
        _onChanged?.Invoke();
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
