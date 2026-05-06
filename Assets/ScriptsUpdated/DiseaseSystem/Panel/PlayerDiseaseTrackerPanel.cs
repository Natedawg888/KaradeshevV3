using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerDiseaseTrackerPanel : MonoBehaviour
{
    [Header("Panel")]
    public GameObject panelRoot;
    public Button openButton;
    public Button closeButton;

    [Header("Content")]
    public Transform contentRoot;
    public PlayerDiseaseTrackerRow rowPrefab;

    [Header("Empty State")]
    public TMP_Text emptyText;

    [Header("Options")]
    public bool refreshWhenOpened = true;
    public bool refreshOnEnable = false;

    private readonly List<PlayerDiseaseTrackerRow> _spawnedRows = new();

    private void Awake()
    {
        if (openButton != null)
        {
            openButton.onClick.RemoveListener(Show);
            openButton.onClick.AddListener(Show);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Hide);
            closeButton.onClick.AddListener(Hide);
        }

        Hide();
    }

    private void OnEnable()
    {
        if (refreshOnEnable && IsShowing)
            Refresh();
    }

    public bool IsShowing =>
        panelRoot != null && panelRoot.activeInHierarchy;

    public void Show()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (refreshWhenOpened)
            Refresh();
    }

    public void Hide()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    public void Refresh()
    {
        ClearRows();

        if (DiseaseManager.Instance == null)
        {
            SetEmptyText(true, "Disease system not found.");
            return;
        }

        IReadOnlyList<PlayerDiseaseSummary> summaries =
            DiseaseManager.Instance.GetActivePlayerDiseaseSummaries();

        if (summaries == null || summaries.Count == 0)
        {
            SetEmptyText(true, "No active diseases in the population.");
            return;
        }

        SetEmptyText(false, string.Empty);

        for (int i = 0; i < summaries.Count; i++)
        {
            PlayerDiseaseSummary summary = summaries[i];

            if (summary == null)
                continue;

            PlayerDiseaseTrackerRow row = CreateRow();
            row.Bind(summary);
        }
    }

    private PlayerDiseaseTrackerRow CreateRow()
    {
        if (rowPrefab == null || contentRoot == null)
            return null;

        PlayerDiseaseTrackerRow row = Instantiate(rowPrefab, contentRoot);
        row.gameObject.SetActive(true);

        _spawnedRows.Add(row);
        return row;
    }

    private void ClearRows()
    {
        for (int i = 0; i < _spawnedRows.Count; i++)
        {
            if (_spawnedRows[i] != null)
                Destroy(_spawnedRows[i].gameObject);
        }

        _spawnedRows.Clear();
    }

    private void SetEmptyText(bool show, string message)
    {
        if (emptyText == null)
            return;

        emptyText.gameObject.SetActive(show);
        emptyText.text = message;
    }
}