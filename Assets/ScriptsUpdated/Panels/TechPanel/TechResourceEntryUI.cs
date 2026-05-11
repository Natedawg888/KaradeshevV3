using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One row in the tech panel resource list.
/// Shows the resource icon and name; the button opens the detail panel.
/// </summary>
public class TechResourceEntryUI : MonoBehaviour
{
    [Header("UI")]
    public Image icon;
    public TMP_Text nameText;
    public Button detailButton;

    private ResourceDefinition _resource;
    private Action<ResourceDefinition> _onClicked;

    public void Bind(ResourceDefinition resource, Action<ResourceDefinition> onClicked)
    {
        _resource  = resource;
        _onClicked = onClicked;

        if (nameText) nameText.text = resource.resourceName ?? resource.resourceID;

        if (icon)
        {
            icon.sprite  = resource.resourceIcon;
            icon.enabled = resource.resourceIcon != null;
        }

        if (detailButton)
        {
            detailButton.onClick.RemoveAllListeners();
            detailButton.onClick.AddListener(OnClicked);
        }
    }

    private void OnClicked() => _onClicked?.Invoke(_resource);
}
