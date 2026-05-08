using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CraftingBuildingPanelControl : MonoBehaviour
{
    [Header("Roots")]
    public GameObject root;
    public Button closeButton;

    [Header("Header")]
    public TMP_Text titleText;

    [Header("Recipe List")]
    public Transform contentRoot;
    public CraftingRecipeItem recipeItemPrefab;

    [Header("Active Orders List")]
    public Transform activeOrdersContentRoot;
    public CraftOrderWidget activeOrderWidgetPrefab;
    public GameObject noActiveOrdersObject;

    [Header("References")]
    [SerializeField] private BuildingManager buildingManager;
    [SerializeField] private PlayerKnownCraftingManager knownCraftingManager;
    [SerializeField] private CraftingRecipeManager craftingRecipeManager;

    [Header("Optional Fallbacks")]
    [Tooltip("Optional fallback BuildingPanel if OpenFor() is called with a null parent (e.g. test scenes).")]
    [SerializeField] private BuildingPanelControl defaultParentPanel;

    // --- runtime ---
    private BuildingPanelControl _parentPanel;
    private BuildingControl _building;
    private CraftingBuildingControl _crafting;
    private TileControl _tile;

    private CanvasGroup _cg;
    private bool _subscribedToKnownCrafting;

    [SerializeField] private CraftingTutorial craftingTutorial;

    public bool IsShowing => root != null && root.activeInHierarchy;

    private void Awake()
    {
        if (root != null)
        {
            _cg = root.GetComponent<CanvasGroup>();
            if (_cg == null)
                _cg = root.AddComponent<CanvasGroup>();

            _cg.alpha = 0f;
            _cg.interactable = false;
            _cg.blocksRaycasts = false;
            root.SetActive(false);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }
    }

    private void OnEnable()
    {
        SubscribeToKnownCrafting();
    }

    private void OnDisable()
    {
        UnsubscribeFromKnownCrafting();
        UnsubscribeFromCrafting();
    }

    private void OnDestroy()
    {
        UnsubscribeFromKnownCrafting();
        UnsubscribeFromCrafting();
    }

    public void OpenFor(BuildingControl building, BuildingPanelControl parent, TileControl tile)
    {
        UnsubscribeFromCrafting();

        if (craftingTutorial != null && craftingTutorial.ShouldRunTutorial())
            craftingTutorial.BeginTutorial();

        _parentPanel = parent != null ? parent : defaultParentPanel;
        _building = building;
        _crafting = building != null ? building.GetComponent<CraftingBuildingControl>() : null;
        _tile = tile != null ? tile : (building != null ? building.GetComponentInParent<TileControl>() : null);

        if (_crafting == null)
        {
            //Debug.LogError("[CraftingPanel] Building has no CraftingBuildingControl.");
            return;
        }

        _crafting.OnOrdersChanged -= RefreshActiveOrders;
        _crafting.OnOrdersChanged += RefreshActiveOrders;

        if (titleText != null && building != null)
        {
            string displayName = !string.IsNullOrWhiteSpace(building.buildingName)
                ? building.buildingName
                : (buildingManager != null
                    ? (buildingManager.GetBuildingByID(building.buildingID)?.buildingName ?? building.buildingID)
                    : building.buildingID);

            titleText.text = displayName;
        }

        RefreshAll();

        if (root != null)
            root.SetActive(true);

        if (_cg != null)
        {
            _cg.alpha = 1f;
            _cg.interactable = true;
            _cg.blocksRaycasts = true;
        }
    }

    public void Hide()
    {
        UnsubscribeFromCrafting();

        if (_cg != null)
        {
            _cg.alpha = 0f;
            _cg.interactable = false;
            _cg.blocksRaycasts = false;
        }

        if (root != null)
            root.SetActive(false);

        ClearChildren(contentRoot);
        ClearChildren(activeOrdersContentRoot);

        if (noActiveOrdersObject != null)
            noActiveOrdersObject.SetActive(false);

        _parentPanel?.SoftShowFromChild();

        _building = null;
        _crafting = null;
        _tile = null;
    }

    private void RefreshAll()
    {
        RefreshRecipeList();
        RefreshActiveOrders();
    }

    private void RefreshRecipeList()
    {
        if (contentRoot == null || recipeItemPrefab == null)
            return;

        ClearChildren(contentRoot);

        if (knownCraftingManager == null || craftingRecipeManager == null || _crafting == null)
            return;

        IReadOnlyCollection<string> knownIDs = knownCraftingManager.GetKnownIDs();
        if (knownIDs == null || knownIDs.Count == 0)
            return;

        HashSet<string> knownSet = BuildNormalizedKnownSet(knownIDs);
        if (knownSet.Count == 0)
            return;

        List<CraftingRecipe> list = new List<CraftingRecipe>(knownSet.Count);

        foreach (string knownId in knownSet)
        {
            CraftingRecipe recipe = craftingRecipeManager.GetByID(knownId);
            if (recipe == null)
            {
                //Debug.LogWarning($"[CraftingPanel] Known crafting id '{knownId}' NOT found in CraftingRecipeManager.");
                continue;
            }

            // Extra safety: if the recipe was unlearned between refreshes, do not show it.
            if (!IsRecipeKnown(recipe.craftingID))
                continue;

            if (!CanCraftHere(recipe))
                continue;

            list.Add(recipe);
        }

        list.Sort(CompareRecipesForDisplay);

        for (int i = 0; i < list.Count; i++)
        {
            CraftingRecipe recipe = list[i];
            if (recipe == null || !IsRecipeKnown(recipe.craftingID))
                continue;

            CraftingRecipeItem item = Instantiate(recipeItemPrefab, contentRoot);
            item.Bind(
                recipe,
                _crafting,
                onCraftStarted: HandleCraftStarted
            );
        }
    }

    private HashSet<string> BuildNormalizedKnownSet(IReadOnlyCollection<string> rawKnownIds)
    {
        HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (rawKnownIds == null)
            return result;

        foreach (string rawId in rawKnownIds)
        {
            string trimmed = NormalizeId(rawId);
            if (string.IsNullOrEmpty(trimmed))
                continue;

            result.Add(trimmed);
        }

        return result;
    }

    private bool IsRecipeKnown(string craftingId)
    {
        string normalizedTarget = NormalizeId(craftingId);
        if (string.IsNullOrEmpty(normalizedTarget) || knownCraftingManager == null)
            return false;

        IReadOnlyCollection<string> knownIDs = knownCraftingManager.GetKnownIDs();
        if (knownIDs == null || knownIDs.Count == 0)
            return false;

        foreach (string rawId in knownIDs)
        {
            if (string.Equals(NormalizeId(rawId), normalizedTarget, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private string NormalizeId(string id)
    {
        return string.IsNullOrWhiteSpace(id) ? null : id.Trim();
    }

    private int CompareRecipesForDisplay(CraftingRecipe a, CraftingRecipe b)
    {
        string aName = (a == null || string.IsNullOrWhiteSpace(a.craftingName)) ? "~" : a.craftingName;
        string bName = (b == null || string.IsNullOrWhiteSpace(b.craftingName)) ? "~" : b.craftingName;

        int byName = string.Compare(aName, bName, StringComparison.OrdinalIgnoreCase);
        if (byName != 0)
            return byName;

        string aId = a != null ? a.craftingID : string.Empty;
        string bId = b != null ? b.craftingID : string.Empty;
        return string.Compare(aId, bId, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshActiveOrders()
    {
        if (activeOrdersContentRoot == null || activeOrderWidgetPrefab == null)
            return;

        ClearChildren(activeOrdersContentRoot);

        if (_crafting == null)
        {
            if (noActiveOrdersObject != null)
                noActiveOrdersObject.SetActive(true);
            return;
        }

        List<CraftingBuildingControl.ActiveOrderView> activeOrders = _crafting.GetActiveOrdersSnapshot();

        for (int i = 0; i < activeOrders.Count; i++)
        {
            CraftingBuildingControl.ActiveOrderView order = activeOrders[i];
            CraftOrderWidget widget = Instantiate(activeOrderWidgetPrefab, activeOrdersContentRoot);
            widget.Bind(order.orderId, order.totalTurns, order.icon);
            widget.UpdateTurns(order.turnsLeft);
        }

        if (noActiveOrdersObject != null)
            noActiveOrdersObject.SetActive(activeOrders.Count == 0);
    }

    private void HandleCraftStarted()
    {
        RefreshAll();
    }

    private bool CanCraftHere(CraftingRecipe recipe)
    {
        if (recipe == null || _crafting == null)
            return false;

        if (!IsRecipeKnown(recipe.craftingID))
            return false;

        List<string> allowed = _crafting.allowedRecipeIDs;
        if (allowed != null && allowed.Count > 0 && !allowed.Contains(recipe.craftingID))
            return false;

        return true;
    }

    private void ClearChildren(Transform parent)
    {
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    private void UnsubscribeFromCrafting()
    {
        if (_crafting != null)
            _crafting.OnOrdersChanged -= RefreshActiveOrders;
    }

    private void SubscribeToKnownCrafting()
    {
        if (_subscribedToKnownCrafting)
            return;

        if (knownCraftingManager == null)
            return;

        knownCraftingManager.OnKnownCraftingChanged -= RefreshRecipeList;
        knownCraftingManager.OnKnownCraftingChanged += RefreshRecipeList;
        _subscribedToKnownCrafting = true;
    }

    private void UnsubscribeFromKnownCrafting()
    {
        if (!_subscribedToKnownCrafting)
            return;

        if (knownCraftingManager != null)
            knownCraftingManager.OnKnownCraftingChanged -= RefreshRecipeList;

        _subscribedToKnownCrafting = false;
    }

    public void InstallRuntimeRefs(
        BuildingManager newBuildingManager = null,
        PlayerKnownCraftingManager newKnownCraftingManager = null,
        CraftingRecipeManager newCraftingRecipeManager = null,
        CraftingTutorial newCraftingTutorial = null,
        bool refreshIfOpen = true)
    {
        bool knownMgrChanged = knownCraftingManager != newKnownCraftingManager && newKnownCraftingManager != null;

        if (knownMgrChanged)
            UnsubscribeFromKnownCrafting();

        if (newBuildingManager != null)
            buildingManager = newBuildingManager;

        if (newKnownCraftingManager != null)
            knownCraftingManager = newKnownCraftingManager;

        if (newCraftingRecipeManager != null)
            craftingRecipeManager = newCraftingRecipeManager;

        if (knownMgrChanged && isActiveAndEnabled)
            SubscribeToKnownCrafting();

        if (newCraftingTutorial != null)
            craftingTutorial = newCraftingTutorial;

        if (refreshIfOpen && root != null && root.activeInHierarchy)
            RefreshAll();
    }
}
