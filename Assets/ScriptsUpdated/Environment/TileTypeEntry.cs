using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TileTypeEntry : MonoBehaviour
{
    public Image icon;
    public TMP_Text label;

    public void Bind(EnvironmentTileType tileType, Sprite tileIcon = null)
    {
        if (label != null)
            label.text = tileType.ToString();

        if (icon != null)
        {
            if (tileIcon != null)
            {
                icon.enabled = true;
                icon.sprite = tileIcon;
            }
            else
            {
                icon.enabled = false;
            }
        }
    }
}