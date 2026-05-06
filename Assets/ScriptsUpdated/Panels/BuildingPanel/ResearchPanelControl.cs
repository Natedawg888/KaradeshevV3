using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ResearchPanelControl : MonoBehaviour
{
    [Header("Root")]
    public GameObject root;
    public Button closeButton;

    [Header("List")]
    public Transform contentRoot;
    public TechnologyItem techItemPrefab;

    private BuildingControl _station;

    public bool IsShowing => root != null ? root.activeInHierarchy : gameObject.activeInHierarchy;

    private void Awake()
    {
        if (root) root.SetActive(false);

        if (closeButton)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }
    }

    public void OpenFor(BuildingControl station)
    {
        _station = station;

        if (root) root.SetActive(true);
        gameObject.SetActive(true);

        var bt = station ? station.GetComponent<BuildingTechnology>() : null;
        List<Technology> source;

        if (bt != null)
        {
            source = bt.GetAvailableAtPlayerLevel();

            // ✅ Hide researched and currently-being-researched
            if (PlayerResearchManager.Instance)
                source = PlayerResearchManager.Instance.FilterOutResearchedAndActive(source, station);
        }
        else
        {
            source = new List<Technology>();
            Debug.LogWarning("[ResearchPanel] No BuildingTechnology on station; showing empty.");
        }

        PopulateList(station, source);
    }

    public void Close()
    {
        if (root) root.SetActive(false);
        ClearList();
        _station = null;
    }

    public void RefreshNow()
    {
        if (_station == null) return;
        OpenFor(_station);
    }

    private void PopulateList(BuildingControl station, List<Technology> techs)
    {
        ClearList();

        if (techs == null || techs.Count == 0)
            return;

        if (!contentRoot || !techItemPrefab)
        {
            Debug.LogError("[ResearchPanel] Missing contentRoot or techItemPrefab.");
            return;
        }

        for (int i = 0; i < techs.Count; i++)
        {
            var t = techs[i];
            if (t == null) continue;

            var go = Instantiate(techItemPrefab, contentRoot);
            // TechnologyItem has: Bind(Technology t, BuildingControl stationBuilding, ResearchPanelControl owner)
            go.Bind(t, station, this);
        }
    }

    private void ClearList()
    {
        if (!contentRoot) return;
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);
    }
}