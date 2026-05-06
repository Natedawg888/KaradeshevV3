using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RitualPopulationCostEntryUI : MonoBehaviour
{
    [Header("Refs")]
    public Image ageGroupIcon;
    public Image genderIcon;
    public TMP_Text nameText;
    public TMP_Text amountText;
    public TMP_Text availableText;

    [Header("Colors")]
    public Color canAffordColor = new Color(0.20f, 0.70f, 0.20f);
    public Color cannotAffordColor = new Color(0.80f, 0.20f, 0.20f);

    public void Bind(
        Sprite ageSprite,
        Sprite genderSprite,
        string displayName,
        int requiredAmount,
        int availableAmount)
    {
        if (ageGroupIcon != null)
            ageGroupIcon.sprite = ageSprite;

        if (genderIcon != null)
            genderIcon.sprite = genderSprite;

        if (nameText != null)
            nameText.text = string.IsNullOrWhiteSpace(displayName) ? "Population" : displayName;

        bool enough = availableAmount >= requiredAmount;
        Color useColor = enough ? canAffordColor : cannotAffordColor;
        string hex = ColorUtility.ToHtmlStringRGB(useColor);

        if (amountText != null)
        {
            amountText.richText = true;
            amountText.text = $"x <color=#{hex}>{Mathf.Max(0, requiredAmount)}</color>";
        }

        if (availableText != null)
        {
            availableText.richText = true;
            availableText.text = $"Available: <color=#{hex}>{Mathf.Max(0, availableAmount)}</color>";
        }
    }
}