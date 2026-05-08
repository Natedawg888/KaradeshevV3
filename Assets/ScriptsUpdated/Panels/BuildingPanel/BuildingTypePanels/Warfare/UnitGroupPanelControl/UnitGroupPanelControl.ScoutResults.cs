using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class UnitGroupPanelControl : MonoBehaviour
{
    [Header("Scout Results")]
    public GameObject scoutResultsPanelRoot;
    public Transform scoutResultsContentRoot;
    public ScoutResultItemUI scoutResultItemPrefab;
    public Button scoutResultsCloseButton;

    private void SetupScoutResultsUI()
    {
        if (scoutResultsCloseButton != null)
        {
            scoutResultsCloseButton.onClick.RemoveAllListeners();
            scoutResultsCloseButton.onClick.AddListener(OnScoutResultsCloseClicked);
        }

        if (scoutResultsPanelRoot != null)
            scoutResultsPanelRoot.SetActive(false);
    }

    private void OnScoutResultsCloseClicked()
    {
        if (scoutResultsPanelRoot != null)
            scoutResultsPanelRoot.SetActive(false);

        if (_group != null)
        {
            if (_group.lastScoutResults != null)
                _group.lastScoutResults.Clear();

            _group.hasPendingScoutResults = false;
        }

        UpdateActionButtonState();
    }

    private void OpenScoutResultsPanel()
    {
        if (scoutResultsPanelRoot == null ||
            scoutResultsContentRoot == null ||
            scoutResultItemPrefab == null)
        {
            //Debug.LogWarning("[UnitGroupPanel] Scout results UI not wired in inspector.");
            return;
        }

        // Close the normal action list if it happens to be open.
        if (actionPanelRoot != null)
            actionPanelRoot.SetActive(false);

        // Clear old items
        foreach (Transform child in scoutResultsContentRoot)
        {
            Destroy(child.gameObject);
        }

        if (_group != null && _group.lastScoutResults != null && _group.lastScoutResults.Count > 0)
        {
            for (int i = 0; i < _group.lastScoutResults.Count; i++)
            {
                var entry = _group.lastScoutResults[i];
                if (entry == null) continue;

                var item = Instantiate(scoutResultItemPrefab, scoutResultsContentRoot);
                item.Setup(entry);
            }
        }
        else
        {
            // Optional: spawn a "No units or animals spotted" placeholder item here
        }

        scoutResultsPanelRoot.SetActive(true);
    }

    // Helper to change the label on the Action button.
    private void SetActionButtonLabel(string label)
    {
        if (actionOpenButton == null)
            return;

        var text = actionOpenButton.GetComponentInChildren<TMP_Text>();
        if (text != null)
            text.text = label;
    }
}
