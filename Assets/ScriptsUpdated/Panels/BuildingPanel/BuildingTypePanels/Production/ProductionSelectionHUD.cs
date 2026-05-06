using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProductionSelectionHUD : MonoBehaviour
{
    [Header("UI")]
    public GameObject root;
    public Button cancelButton;
    public TMP_Text statusText;

    [Header("Optional: Panels to soft-hide during selection")]
    public BuildingPanelControl buildingPanel;

    private void OnEnable()
    {
        if (cancelButton)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(() =>
            {
                // cancel and clear selection
                ProductionSelectionController.CancelSelection(false);
            });
        }

        ProductionSelectionController.OnSelectionModeChanged -= HandleModeChanged;
        ProductionSelectionController.OnSelectionModeChanged += HandleModeChanged;

        ProductionSelectionController.OnSelectionProgress   -= HandleProgress;
        ProductionSelectionController.OnSelectionProgress   += HandleProgress;

        HandleModeChanged(ProductionSelectionController.IsSelectionActive);
    }

    private void OnDisable()
    {
        ProductionSelectionController.OnSelectionModeChanged -= HandleModeChanged;
        ProductionSelectionController.OnSelectionProgress   -= HandleProgress;
    }

    private void HandleModeChanged(bool active)
    {
        TileInteraction.SetSelectionEnabled(false);

        if (root) root.SetActive(active);

        // Soft-hide the building panel during selection (and restore after)
        if (buildingPanel != null)
        {
            if (active) buildingPanel.SoftHideForProductionSelection();
            else buildingPanel.SoftShowAfterProductionSelection();
        }

        if (active)
        {
            HandleProgress(
                ProductionSelectionController.SelectedCount,
                ProductionSelectionController.MaxTiles
            );
        }
    }

    private void HandleProgress(int picked, int max)
    {
        if (!root || !root.activeSelf) return;
        if (!statusText) return;

        if (max <= 0) statusText.text = "Select tiles";
        else          statusText.text = $"Tiles: {picked}/{max}";
    }
}