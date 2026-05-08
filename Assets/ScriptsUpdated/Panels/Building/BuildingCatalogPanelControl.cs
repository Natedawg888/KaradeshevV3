using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BuildingCatalogPanelControl : MonoBehaviour
{
    [Header("Root")]
    public GameObject root;
    public Button closeButton;

    [Header("List")]
    public Transform contentArea;
    public GameObject buildingItemPrefab;

    public System.Action OnClosed;

    private DiscoveredTilePanelControl ownerPanel;
    private readonly List<BuildingCatalogItem> spawnedItems = new();

    public bool IsShowing => root != null && root.activeInHierarchy;
    public IReadOnlyList<BuildingCatalogItem> SpawnedItems => spawnedItems;
    public BuildingCatalogItem PrimaryItem => spawnedItems.Count > 0 ? spawnedItems[0] : null;

    private void Awake()
    {
        if (closeButton)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }

        if (root) root.SetActive(false);
    }

    private void Update()
    {
        if (root && root.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            Hide();
    }

    private void ClearContent()
    {
        spawnedItems.Clear();

        for (int i = contentArea.childCount - 1; i >= 0; i--)
            Destroy(contentArea.GetChild(i).gameObject);
    }

    public void ShowFor(EnvironmentControl env, DiscoveredTilePanelControl owner)
    {
        ownerPanel = owner;
        ShowFor(env);
    }

    public void ShowFor(EnvironmentControl env)
    {
        if (!env) return;

        if (!buildingItemPrefab)
        {
            //Debug.LogError("[BuildingCatalogPanel] buildingItemPrefab is not assigned.");
            return;
        }

        if (root) root.SetActive(true);

        var available = PlayerBuildingManager.Instance
            ? PlayerBuildingManager.Instance.GetAvailableBuildingsForTile(
                env.tileSize, env.environmentType, env.environmentTileType)
            : new List<Building>();

        ClearContent();

        foreach (var b in available)
        {
            var go = Instantiate(buildingItemPrefab, contentArea);
            var ui = go.GetComponent<BuildingCatalogItem>();
            if (ui != null)
            {
                ui.Bind(b, env, this, ownerPanel);
                spawnedItems.Add(ui);
            }
            else
            {
                //Debug.LogError("[BuildingCatalogPanel] The prefab is missing a BuildingCatalogItem component.");
            }
        }
    }

    public void Hide()
    {
        if (root) root.SetActive(false);
        ClearContent();
        OnClosed?.Invoke();
        ownerPanel = null;
    }
}
