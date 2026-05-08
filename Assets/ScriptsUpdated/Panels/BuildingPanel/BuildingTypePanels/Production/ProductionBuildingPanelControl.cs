using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProductionBuildingPanelControl : MonoBehaviour
{
    [Header("Roots")]
    public GameObject root;
    public Button closeButton;

    [Header("Header")]
    public TMP_Text titleText;

    [Header("List")]
    public Transform contentRoot;
    public ProductionPlanItem planItemPrefab;

    [Header("References")]
    [SerializeField] private BuildingManager buildingManager;
    [SerializeField] private PlayerKnownProductionManager knownProductionManager;
    [SerializeField] private ProductionPlanManager productionPlanManager;

    [Header("Optional Fallbacks")]
    [Tooltip("Optional fallback BuildingPanel if OpenFor() is called with a null parent (e.g. test scenes).")]
    [SerializeField] private BuildingPanelControl defaultParentPanel;

    // --- runtime ---
    private BuildingPanelControl _parentPanel;
    private BuildingControl _building;
    private ProductionBuildingControl _production;
    private TileControl _tile;

    // Track whether we hid this panel specifically because of selection mode
    private bool _hiddenForSelection = false;
    private bool _subscribedToKnownProduction = false;

    [SerializeField] private ProductionTutorial productionTutorial;

    private void Awake()
    {
        if (root != null)
            root.SetActive(false);

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }
    }

    private void OnEnable()
    {
        SubscribeToKnownProduction();

        ProductionSelectionController.OnSelectionModeChanged -= HandleSelectionModeChanged;
        ProductionSelectionController.OnSelectionModeChanged += HandleSelectionModeChanged;
    }

    private void OnDisable()
    {
        UnsubscribeFromKnownProduction();
        ProductionSelectionController.OnSelectionModeChanged -= HandleSelectionModeChanged;
    }

    private void OnDestroy()
    {
        UnsubscribeFromKnownProduction();
        ProductionSelectionController.OnSelectionModeChanged -= HandleSelectionModeChanged;
    }

    public void OpenFor(BuildingControl building, BuildingPanelControl parent, TileControl tile)
    {
        _hiddenForSelection = false;

        _parentPanel = parent != null ? parent : defaultParentPanel;
        _building = building;
        _production = building != null ? building.GetComponent<ProductionBuildingControl>() : null;
        _tile = tile != null ? tile : (building != null ? building.GetComponentInParent<TileControl>() : null);

        if (_production == null)
        {
            //Debug.LogError("[ProductionPanel] Building has no ProductionBuildingControl.");
            return;
        }

        if (titleText != null && building != null)
        {
            string displayName = !string.IsNullOrWhiteSpace(building.buildingName)
                ? building.buildingName
                : (buildingManager != null
                    ? (buildingManager.GetBuildingByID(building.buildingID)?.buildingName ?? building.buildingID)
                    : building.buildingID);

            titleText.text = displayName;
        }

        RefreshList();

        if (root != null)
            root.SetActive(true);

        if (productionTutorial != null && productionTutorial.ShouldRunTutorial())
            productionTutorial.BeginTutorial();
    }

    public void Hide()
    {
        _hiddenForSelection = false;

        if (root != null)
            root.SetActive(false);

        ClearList();

        _parentPanel?.SoftShowFromChild();

        _building = null;
        _production = null;
        _tile = null;
    }

    private void RefreshList()
    {
        if (contentRoot == null || planItemPrefab == null)
            return;

        ClearList();

        if (knownProductionManager == null || productionPlanManager == null || _production == null)
            return;

        IReadOnlyCollection<string> knownIDs = knownProductionManager.GetKnownIDs();
        if (knownIDs == null || knownIDs.Count == 0)
            return;

        HashSet<string> knownSet = BuildNormalizedKnownSet(knownIDs);
        if (knownSet.Count == 0)
            return;

        List<ProductionPlan> list = new List<ProductionPlan>(knownSet.Count);

        foreach (string knownId in knownSet)
        {
            ProductionPlan plan = productionPlanManager.GetByID(knownId);
            if (plan == null)
            {
                //Debug.LogWarning($"[ProductionPanel] Known production id '{knownId}' NOT found in ProductionPlanManager.");
                continue;
            }

            // Extra safety: if the plan was unlearned between refreshes, do not show it.
            if (!IsPlanKnown(plan.productionID))
                continue;

            if (!_production.IsPlanAllowed(plan.productionID))
            {
                //Debug.Log($"[ProductionPanel] Plan '{plan.productionID}' exists but NOT allowed for building '{_building?.buildingID}'.");
                continue;
            }

            list.Add(plan);
        }

        list.Sort(ComparePlansForDisplay);

        for (int i = 0; i < list.Count; i++)
        {
            ProductionPlan plan = list[i];
            if (plan == null || !IsPlanKnown(plan.productionID))
                continue;

            ProductionPlanItem item = Instantiate(planItemPrefab, contentRoot);
            item.Bind(plan, _production);
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

    private bool IsPlanKnown(string productionId)
    {
        string normalizedTarget = NormalizeId(productionId);
        if (string.IsNullOrEmpty(normalizedTarget) || knownProductionManager == null)
            return false;

        IReadOnlyCollection<string> knownIDs = knownProductionManager.GetKnownIDs();
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

    private int ComparePlansForDisplay(ProductionPlan a, ProductionPlan b)
    {
        string aName = (a == null || string.IsNullOrWhiteSpace(a.planName)) ? "~" : a.planName;
        string bName = (b == null || string.IsNullOrWhiteSpace(b.planName)) ? "~" : b.planName;

        int byName = string.Compare(aName, bName, StringComparison.OrdinalIgnoreCase);
        if (byName != 0)
            return byName;

        string aId = a != null ? a.productionID : string.Empty;
        string bId = b != null ? b.productionID : string.Empty;
        return string.Compare(aId, bId, StringComparison.OrdinalIgnoreCase);
    }

    private void ClearList()
    {
        if (contentRoot == null)
            return;

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);
    }

    private void HandleSelectionModeChanged(bool active)
    {
        if (_production == null || root == null)
            return;

        ProductionBuildingControl activeBuilding = ProductionSelectionController.ActiveBuilding;

        if (active)
        {
            if (activeBuilding != _production)
                return;

            if (!root.activeSelf)
                return;

            _hiddenForSelection = true;
            root.SetActive(false);
        }
        else
        {
            if (!_hiddenForSelection)
                return;

            _hiddenForSelection = false;

            RefreshList();

            if (!root.activeSelf)
                root.SetActive(true);
        }
    }

    private void SubscribeToKnownProduction()
    {
        if (_subscribedToKnownProduction)
            return;

        if (knownProductionManager == null)
            return;

        knownProductionManager.OnKnownProductionChanged -= RefreshList;
        knownProductionManager.OnKnownProductionChanged += RefreshList;
        _subscribedToKnownProduction = true;
    }

    private void UnsubscribeFromKnownProduction()
    {
        if (!_subscribedToKnownProduction)
            return;

        if (knownProductionManager != null)
            knownProductionManager.OnKnownProductionChanged -= RefreshList;

        _subscribedToKnownProduction = false;
    }

    public void InstallRuntimeRefs(
        BuildingManager newBuildingManager = null,
        PlayerKnownProductionManager newKnownProductionManager = null,
        ProductionPlanManager newProductionPlanManager = null,
        ProductionTutorial newProductionTutorial = null,
        bool refreshIfOpen = true)
    {
        bool knownMgrChanged =
            newKnownProductionManager != null &&
            knownProductionManager != newKnownProductionManager;

        if (knownMgrChanged)
            UnsubscribeFromKnownProduction();

        if (newBuildingManager != null)
            buildingManager = newBuildingManager;

        if (newKnownProductionManager != null)
            knownProductionManager = newKnownProductionManager;

        if (newProductionPlanManager != null)
            productionPlanManager = newProductionPlanManager;

        if (knownMgrChanged && isActiveAndEnabled)
            SubscribeToKnownProduction();

        if (newProductionTutorial != null)
            productionTutorial = newProductionTutorial;

        if (refreshIfOpen && root != null && root.activeSelf)
            RefreshList();
    }
}
