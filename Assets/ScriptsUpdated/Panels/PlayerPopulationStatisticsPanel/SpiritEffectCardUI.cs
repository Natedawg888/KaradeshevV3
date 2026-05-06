using TMPro;
using UnityEngine;

public class SpiritEffectCardUI : MonoBehaviour
{
    [Header("Text")]
    public TMP_Text effectNameText;
    public TMP_Text effectInfoText;

    public void Bind(SpiritEffectEntry entry, SpiritMoodState mood)
    {
        if (entry == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (effectNameText != null)
            effectNameText.text = SpiritEffectDisplayUtility.GetEffectDisplayName(entry.effectType);

        if (effectInfoText != null)
            effectInfoText.text = SpiritEffectDisplayUtility.GetCombinedEffectText(entry, mood);
    }
}