using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generic reusable title + value row for tech panel detail content areas.
/// Use Setup(title, value) or Setup(title, value, icon).
///
/// Inspector setup:
///   titleText  — left/label text
///   valueText  — right/value text
///   iconImage  — (optional) icon sprite
///   iconRoot   — (optional) parent GameObject toggled when icon is shown/hidden
/// </summary>
public class TechDetailValueRowUI : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text  titleText;
    public TMP_Text  valueText;

    public void Setup(string title, string value)
    {
        if (titleText) titleText.text = title;
        if (valueText) valueText.text = value;
    }

    public void Setup(string title, string value, Sprite icon)
    {
        if (titleText) titleText.text = title;
        if (valueText) valueText.text = value;
    }
}
