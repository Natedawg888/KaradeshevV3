using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ResourceEntryUI : MonoBehaviour
{
    [Header("References")]
    public Image iconImage;
    public TMP_Text nameText;
    public TMP_Text amountText;
    public Slider spoilageSlider;

    /// Call this to fill in the UI for one resource entry.
    public void Initialize(ResourceSpawnEntry entry)
    {
        if (entry == null || entry.definition == null)
            return;

        // Icon
        if (iconImage != null)
            iconImage.sprite = entry.definition.resourceIcon;

        // Name
        if (nameText != null)
            nameText.text = entry.definition.resourceName;

        // Amount
        if (amountText != null)
            amountText.text = entry.amount.ToString();

        // Spoilage countdown
        if (spoilageSlider != null)
        {
            SeasonDefinition season = SeasonManager.Instance != null
                ? SeasonManager.Instance.CurrentSeason
                : null;

            bool allowed = entry.definition.IsAllowedInSeason(season);

            int baseInterval = Mathf.Max(1, entry.definition.spoilageInterval);
            int effectiveInterval = allowed
                ? baseInterval
                : Mathf.Max(1, baseInterval * 2);

            int elapsed = Mathf.Max(0, entry.turnsSinceLastSpoilage);
            int remaining = Mathf.Clamp(effectiveInterval - elapsed, 0, effectiveInterval);

            spoilageSlider.minValue = 0;
            spoilageSlider.maxValue = effectiveInterval;
            spoilageSlider.wholeNumbers = true;
            spoilageSlider.value = remaining;
            spoilageSlider.interactable = false;
            spoilageSlider.direction = Slider.Direction.LeftToRight;
        }
    }
}