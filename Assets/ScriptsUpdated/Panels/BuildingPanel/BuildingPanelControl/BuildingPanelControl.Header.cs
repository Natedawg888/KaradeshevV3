using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class BuildingPanelControl : MonoBehaviour
{
    [Header("Header")]
    public TMP_Text titleText;
    public TMP_Text coordinatesText;

    [Header("Renaming UI")]
    public Button renameButton;
    public GameObject renameContainer;
    public TMP_InputField renameInputField;
    public Button saveRenameButton;
    public Button cancelRenameButton;

    private void RefreshHeader()
    {
        if (currentBuilding == null)
            return;

        string displayName = !string.IsNullOrWhiteSpace(currentBuilding.buildingName)
            ? currentBuilding.buildingName
            : (buildingManager != null
                ? (buildingManager.GetBuildingByID(currentBuilding.buildingID)?.buildingName ?? currentBuilding.buildingID)
                : currentBuilding.buildingID);

        if (titleText != null)
            titleText.text = displayName;

        if (coordinatesText != null)
        {
            Vector2Int coords = Vector2Int.zero;
            if (currentTile != null)
                coords = currentTile.GetGridPosition();

            coordinatesText.text = $"Coordinates: {coords.x}, {coords.y}";
        }
    }

    private void BeginRename()
    {
        if (currentBuilding == null)
            return;

        if (renameContainer != null)
            renameContainer.SetActive(true);
        if (renameButton != null)
            renameButton.gameObject.SetActive(false);

        string displayName = !string.IsNullOrWhiteSpace(currentBuilding.buildingName)
            ? currentBuilding.buildingName
            : (buildingManager != null
                ? (buildingManager.GetBuildingByID(currentBuilding.buildingID)?.buildingName ?? currentBuilding.buildingID)
                : currentBuilding.buildingID);

        if (renameInputField != null)
        {
            renameInputField.text = displayName;
            renameInputField.ActivateInputField();
        }
    }

    private void SubmitRename()
    {
        if (currentBuilding == null || renameInputField == null)
            return;

        string newName = renameInputField.text.Trim();
        if (!string.IsNullOrEmpty(newName))
        {
            currentBuilding.buildingName = newName;
            if (titleText != null)
                titleText.text = newName;
        }

        if (renameContainer != null)
            renameContainer.SetActive(false);
        if (renameButton != null)
            renameButton.gameObject.SetActive(true);
    }

    private void CancelRename()
    {
        if (renameContainer != null)
            renameContainer.SetActive(false);
        if (renameButton != null)
            renameButton.gameObject.SetActive(true);
    }
}