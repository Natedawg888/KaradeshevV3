using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BuildingCostEntry : MonoBehaviour
{
    public Image icon;
    public TMP_Text resourceNameText;
    public TMP_Text needText;
    public TMP_Text haveText;

    public void Bind(ResourceDefinition res, int needed, int owned)
    {
        if (icon && res && res.resourceIcon)
            icon.sprite = res.resourceIcon;

        if (resourceNameText && res)
            resourceNameText.text = res.resourceName;

        if (needText)
            needText.text = ShortNumberFormatter.Format(needed);

        if (haveText)
        {
            haveText.text = ShortNumberFormatter.Format(owned);
            haveText.color = owned >= needed ? Color.green : Color.red;
        }
    }
}