using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpiritAffiliatedItemUI : MonoBehaviour
{
    [Header("Identity")]
    public Image spiritIconImage;
    public TMP_Text spiritNameText;
    public TMP_Text spiritDescriptionText;

    [Header("Mood")]
    public Slider moodSlider;
    public TMP_Text moodText;
    public Image moodFaceImage;

    [Header("Mood Sprites")]
    public Sprite angryMoodSprite;
    public Sprite sadMoodSprite;
    public Sprite neutralMoodSprite;
    public Sprite pleasedMoodSprite;
    public Sprite exaltedMoodSprite;

    [Header("Effects")]
    public Transform effectsContentRoot;
    public SpiritEffectCardUI effectCardPrefab;

    private readonly List<SpiritEffectCardUI> _spawnedEffectCards = new List<SpiritEffectCardUI>();

    public void Bind(SpiritRuntimeState runtimeState)
    {
        ClearEffectCards();

        if (runtimeState == null || runtimeState.definition == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        SpiritDefinitionSO spirit = runtimeState.definition;
        SpiritMoodState mood = spirit.GetMoodForFavor(runtimeState.favor);

        if (spiritIconImage != null)
        {
            spiritIconImage.sprite = spirit.icon;
            spiritIconImage.enabled = spirit.icon != null;
        }

        if (spiritNameText != null)
            spiritNameText.text = string.IsNullOrWhiteSpace(spirit.displayName) ? "Unknown Spirit" : spirit.displayName;

        if (spiritDescriptionText != null)
            spiritDescriptionText.text = spirit.description ?? string.Empty;

        if (moodSlider != null)
        {
            moodSlider.minValue = spirit.minFavor;
            moodSlider.maxValue = spirit.maxFavor;
            moodSlider.value = runtimeState.favor;
        }

        if (moodText != null)
            moodText.text = SpiritEffectDisplayUtility.GetMoodDisplayName(mood);

        if (moodFaceImage != null)
        {
            moodFaceImage.sprite = GetMoodSprite(mood);
            moodFaceImage.enabled = moodFaceImage.sprite != null;
        }

        int effectCount = 0;

        if (effectsContentRoot != null && effectCardPrefab != null && spirit.effects != null)
        {
            for (int i = 0; i < spirit.effects.Count; i++)
            {
                SpiritEffectEntry entry = spirit.effects[i];
                if (entry == null)
                    continue;

                SpiritEffectCardUI card = Instantiate(effectCardPrefab, effectsContentRoot);
                card.Bind(entry, mood);
                _spawnedEffectCards.Add(card);
                effectCount++;
            }
        }
    }

    private Sprite GetMoodSprite(SpiritMoodState mood)
    {
        switch (mood)
        {
            case SpiritMoodState.Angry:
                return angryMoodSprite;
            case SpiritMoodState.Sad:
                return sadMoodSprite;
            case SpiritMoodState.Pleased:
                return pleasedMoodSprite;
            case SpiritMoodState.Exalted:
                return exaltedMoodSprite;
            default:
                return neutralMoodSprite;
        }
    }

    private void ClearEffectCards()
    {
        for (int i = 0; i < _spawnedEffectCards.Count; i++)
        {
            if (_spawnedEffectCards[i] != null)
                Destroy(_spawnedEffectCards[i].gameObject);
        }

        _spawnedEffectCards.Clear();
    }
}