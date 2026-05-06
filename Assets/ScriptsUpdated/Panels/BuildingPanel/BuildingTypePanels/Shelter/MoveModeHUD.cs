using UnityEngine;
using UnityEngine.UI;

public class MoveModeHUD : MonoBehaviour
{
    [Header("UI")]
    public GameObject root;     // the move-mode overlay/panel
    public Button cancelButton; // “Cancel Move” button

    private void OnEnable()
    {
        // wire cancel
        if (cancelButton)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(() => ShelterControl.CancelMoveMode(false));
        }

        // subscribe
        ShelterControl.OnMoveModeChanged -= HandleMoveModeChanged;
        ShelterControl.OnMoveModeChanged += HandleMoveModeChanged;

        ShelterControl.OnMoveFinished  -= HandleMoveFinished;
        ShelterControl.OnMoveFinished  += HandleMoveFinished;

        // initial paint in case we enabled mid-flow
        ApplyState(ShelterControl.IsMoveActive);
    }

    private void OnDisable()
    {
        ShelterControl.OnMoveModeChanged -= HandleMoveModeChanged;
        ShelterControl.OnMoveFinished    -= HandleMoveFinished;
    }

    private void HandleMoveModeChanged(bool active) => ApplyState(active);
    private void HandleMoveFinished(bool moved)     => ApplyState(false);

    private void ApplyState(bool active)
    {
        TileInteraction.SetSelectionEnabled(false);

        if (root) root.SetActive(active);
    }
}
