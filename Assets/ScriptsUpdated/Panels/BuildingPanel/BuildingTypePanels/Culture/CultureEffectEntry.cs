using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CultureEffectEntry : MonoBehaviour
{
    [Header("UI")]
    public Image statIcon;
    public TMP_Text statNameText;
    public TMP_Text rateText;

    [Header("Rate Colors")]
    public Color increaseColor = new Color(0.2f, 0.8f, 0.2f);
    public Color decreaseColor = new Color(0.9f, 0.3f, 0.2f);

    public void Bind(CultureEffect effect, Sprite icon)
    {
        if (effect == null)
            return;

        if (statIcon != null)
        {
            statIcon.sprite = icon;
            statIcon.gameObject.SetActive(icon != null);
        }

        if (statNameText != null)
            statNameText.text = effect.stat.ToString();

        if (rateText != null)
        {
            float pct = effect.ratePerTurn * 100f;
            bool positive = pct >= 0f;
            rateText.text = (positive ? "+" : "") + pct.ToString("0.##") + "% / turn";
            rateText.color = positive ? increaseColor : decreaseColor;
        }
    }
}
