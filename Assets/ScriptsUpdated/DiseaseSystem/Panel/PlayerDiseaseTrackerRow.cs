using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerDiseaseTrackerRow : MonoBehaviour
{
    [Header("Main UI")]
    public Image diseaseIconImage;
    public TMP_Text diseaseNameText;
    public TMP_Text descriptionText;
    public TMP_Text totalAffectedText;

    [Header("Age Group Counts")]
    public TMP_Text childAffectedText;
    public TMP_Text teenAffectedText;
    public TMP_Text adultAffectedText;
    public TMP_Text elderAffectedText;

    public void Bind(PlayerDiseaseSummary summary)
    {
        if (summary == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (diseaseIconImage != null)
        {
            diseaseIconImage.sprite = summary.diseaseIcon;
            diseaseIconImage.enabled = summary.diseaseIcon != null;
        }

        if (diseaseNameText != null)
            diseaseNameText.text = summary.displayName;

        if (descriptionText != null)
        {
            descriptionText.text = string.IsNullOrWhiteSpace(summary.description)
                ? "No description available."
                : summary.description;
        }

        if (totalAffectedText != null)
            totalAffectedText.text = $"{summary.totalAffected}";

        if (childAffectedText != null)
            childAffectedText.text = $"{summary.childAffected}";

        if (teenAffectedText != null)
            teenAffectedText.text = $"{summary.teenAffected}";

        if (adultAffectedText != null)
            adultAffectedText.text = $"{summary.adultAffected}";

        if (elderAffectedText != null)
            elderAffectedText.text = $"{summary.elderAffected}";
    }
}