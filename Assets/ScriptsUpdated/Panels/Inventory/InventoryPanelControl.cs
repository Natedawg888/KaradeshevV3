using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryPanelControl : MonoBehaviour
{
    [Header("Roots")]
    public GameObject root;
    public Button openInventoryButton;
    public Button closeInventoryButton;

    [Header("Tabs")]
    public Button showMaterialsButton;
    public Button showFoodButton;
    public Button showWaterButton;

    [Header("List")]
    public Transform contentParent;                 // scroll content (should have GridLayoutGroup)
    public GameObject inventoryItemPrefab;          // holds InventoryItemUI

    [Header("Layout")]
    public GridLayoutGroup contentGrid;             // optional: auto-filled from contentParent
    public int gridColumns = 2;                     // fixed column count

    [Header("Capacity UI")]
    public TMP_Text capacityText;

    public CameraControl cameraControl;

    private enum Tab { Materials, Food, Water }
    private Tab _currentTab = Tab.Materials;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Auto-grab grid from contentParent if not set
        if (contentGrid == null && contentParent != null)
            contentGrid = contentParent.GetComponent<GridLayoutGroup>();

        ApplyGridSettings();
    }
#endif

    private void Awake()
    {
        if (openInventoryButton != null)
        {
            openInventoryButton.onClick.RemoveAllListeners();
            openInventoryButton.onClick.AddListener(Show);
        }

        if (closeInventoryButton != null)
        {
            closeInventoryButton.onClick.RemoveAllListeners();
            closeInventoryButton.onClick.AddListener(Hide);
        }

        if (showMaterialsButton != null)
        {
            showMaterialsButton.onClick.RemoveAllListeners();
            showMaterialsButton.onClick.AddListener(() => SetTab(Tab.Materials));
        }

        if (showFoodButton != null)
        {
            showFoodButton.onClick.RemoveAllListeners();
            showFoodButton.onClick.AddListener(() => SetTab(Tab.Food));
        }

        if (showWaterButton != null)
        {
            showWaterButton.onClick.RemoveAllListeners();
            showWaterButton.onClick.AddListener(() => SetTab(Tab.Water));
        }

        // Ensure grid is forced to 2 columns at runtime too
        if (contentGrid == null && contentParent != null)
            contentGrid = contentParent.GetComponent<GridLayoutGroup>();

        ApplyGridSettings();
    }

    private void SetTab(Tab tab)
    {
        _currentTab = tab;
        Refresh();
    }

    private void ApplyGridSettings()
    {
        if (contentGrid == null) return;

        contentGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        contentGrid.constraintCount = Mathf.Max(1, gridColumns);
    }

    public void Show()
    {
        TileInteraction.SetSelectionEnabled(false);
        root?.SetActive(true);
        ApplyGridSettings();
        cameraControl.PushInputLock();
        Refresh();
    }

    public void Hide()
    {
        TileInteraction.SetSelectionEnabled(false);
        TileInteraction.GetInstance()?.EnableSelectionAfter(0.01f);
        cameraControl.PopInputLock();

        root?.SetActive(false);
        Clear();
        if (capacityText != null) capacityText.text = string.Empty;
    }

    public void Refresh()
    {
        Clear();

        if (PlayerInventoryManager.Instance == null || contentParent == null || inventoryItemPrefab == null)
        {
            if (capacityText != null) capacityText.text = "0/0";
            return;
        }

        switch (_currentTab)
        {
            case Tab.Materials:
                {
                    var stacks = PlayerInventoryManager.Instance.GetStacks(ResourceType.Material);
                    AddStacks(stacks);
                    UpdateCapacityText(ResourceType.Material);
                    break;
                }

            case Tab.Food:
                {
                    var stacks = PlayerInventoryManager.Instance.GetStacks(ResourceType.Food);
                    AddStacks(stacks);
                    UpdateCapacityText(ResourceType.Food);
                    break;
                }

            case Tab.Water:
                {
                    var stacks = PlayerInventoryManager.Instance.GetStacks(ResourceType.Water);
                    AddStacks(stacks);
                    UpdateCapacityText(ResourceType.Water);
                    break;
                }
        }
    }

    private void AddStacks(IReadOnlyList<InventoryStack> stacks)
    {
        if (stacks == null) return;

        foreach (var s in stacks)
        {
            var row = Instantiate(inventoryItemPrefab, contentParent);
            var ui = row.GetComponent<InventoryItemUI>();
            if (ui != null) ui.Bind(s);
        }
    }

    private void Clear()
    {
        if (contentParent == null) return;

        for (int i = contentParent.childCount - 1; i >= 0; i--)
            Destroy(contentParent.GetChild(i).gameObject);
    }

    private void UpdateCapacityText(ResourceType type)
    {
        if (capacityText == null || PlayerInventoryManager.Instance == null) return;

        float used = PlayerInventoryManager.Instance.GetUsedSpace(type);
        float max = PlayerInventoryManager.Instance.GetMaxSpace(type);

        capacityText.text = $"{Mathf.CeilToInt(used)}/{Mathf.CeilToInt(max)}";
    }

    public void ApplyInventoryItemPrefab(GameObject newPrefab, bool repopulate = true)
    {
        if (!newPrefab) return;

        inventoryItemPrefab = newPrefab;

        if (repopulate && Application.isPlaying && root != null && root.activeSelf)
            Refresh();
    }
}