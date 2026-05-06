using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnvironmentTypeEntry : MonoBehaviour
{
    public Image icon;
    public TMP_Text label;

    public void Bind(EnvironmentType envType, Sprite envIcon = null)
    {
        if (label != null)
            label.text = envType.ToString();

        if (icon != null)
        {
            if (envIcon != null)
            {
                icon.enabled = true;
                icon.sprite = envIcon;
            }
            else
            {
                icon.enabled = false;
            }
        }
    }
}