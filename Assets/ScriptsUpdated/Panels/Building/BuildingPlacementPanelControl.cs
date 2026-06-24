using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BuildingPlacementPanelControl : MonoBehaviour
{
    public static bool TutorialDisableCancelButton = false;

    [Header("Controls")]
    public Button rotateLeftButton;
    public Button rotateRightButton;
    public Button confirmButton;
    public Button cancelButton;

    [Header("Info")]
    public TMP_Text buildingNameText;   // optional label

    private BuildingPlacementManager mgr;

    public void Bind(BuildingPlacementManager manager, GameObject previewInstance, Building building)
    {
        mgr = manager;

        if (buildingNameText != null)
            buildingNameText.text = building != null ? building.buildingName : string.Empty;

        if (rotateLeftButton != null) rotateLeftButton.onClick.RemoveAllListeners();
        if (rotateRightButton != null) rotateRightButton.onClick.RemoveAllListeners();
        if (confirmButton != null) confirmButton.onClick.RemoveAllListeners();
        if (cancelButton != null) cancelButton.onClick.RemoveAllListeners();

        bool allowRotation = building == null || building.canRotate;

        if (rotateLeftButton != null)
        {
            rotateLeftButton.onClick.AddListener(() =>
            {
                if (mgr != null && allowRotation)
                    mgr.RotateQuarter(-1);
            });

            rotateLeftButton.interactable = allowRotation;
            rotateLeftButton.gameObject.SetActive(allowRotation);
        }

        if (rotateRightButton != null)
        {
            rotateRightButton.onClick.AddListener(() =>
            {
                if (mgr != null && allowRotation)
                    mgr.RotateQuarter(+1);
            });

            rotateRightButton.interactable = allowRotation;
            rotateRightButton.gameObject.SetActive(allowRotation);
        }

        if (confirmButton != null)
            confirmButton.onClick.AddListener(() =>
            {
                if (mgr != null)
                    mgr.FinalizePlacement();
            });

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(() =>
            {
                if (mgr != null)
                    mgr.CancelPlacement();
            });

            if (TutorialDisableCancelButton)
                cancelButton.gameObject.SetActive(false);
        }

        gameObject.SetActive(true);
    }
}