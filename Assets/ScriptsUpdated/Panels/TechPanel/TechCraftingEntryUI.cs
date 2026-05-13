using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One row in the tech panel crafting list.
/// Shows icon and name; the button opens the crafting detail panel.
/// </summary>
public class TechCraftingEntryUI : MonoBehaviour
{
    [Header("UI")]
    public Image icon;
    public TMP_Text nameText;
    public Button detailButton;

    private CraftingRecipe _recipe;
    private Action<CraftingRecipe> _onClicked;

    public void Bind(CraftingRecipe recipe, Action<CraftingRecipe> onClicked)
    {
        _recipe    = recipe;
        _onClicked = onClicked;

        if (nameText) nameText.text = recipe.craftingName ?? recipe.craftingID;

        if (icon)
        {
            icon.sprite  = recipe.craftingIcon;
            icon.enabled = recipe.craftingIcon != null;
        }

        if (detailButton)
        {
            detailButton.onClick.RemoveAllListeners();
            detailButton.onClick.AddListener(OnClicked);
        }
    }

    private void OnClicked() => _onClicked?.Invoke(_recipe);
}
