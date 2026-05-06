using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RepairPanelControl : MonoBehaviour
{
    [Header("Root")]
    public GameObject root;
    public Button closeButton;

    [Header("Header")]
    public TMP_Text titleText;
    public TMP_Text stateHintText;

    [Header("Tier Buttons")]
    public Button tenBtn;                         // 10%
    public Button fiftyBtn;                       // 50%
    public Button fullBtn;                        // 100% (costs 90%)

    
    [Header("Work Requirements")]
    public TMP_Text scaledTurnsText;
    public TMP_Text scaledPopulationText;

    [Tooltip("Tint used for the selected tier button")]
    public Color selectedTierColor = new(0.85f, 0.95f, 1f);
    public Color normalTierColor   = Color.white;

    [Header("Costs List")]
    public GameObject costsRoot;
    public Transform costsContentRoot;            // ScrollView/Content
    public BuildingCostEntry costEntryPrefab;

    [Header("Actions")]
    public Button repairButton;                   // confirm/perform repair

    // --- runtime ---
    private BuildingControl _building;
    private BuildingHealth  _health;
    private BuildingRepair _repair;
    
    private GameObject RootGO => root != null ? root : gameObject;

    private RepairOption _selected = RepairOption.TenPercent;
    private readonly List<BuildingCostEntry> _spawned = new();

    public bool IsShowing => RootGO != null && RootGO.activeInHierarchy;

    public void Awake()
    {
        if (closeButton)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }

        if (tenBtn)
        {
            tenBtn.onClick.RemoveAllListeners();
            tenBtn.onClick.AddListener(() => SelectTier(RepairOption.TenPercent));
        }
        if (fiftyBtn)
        {
            fiftyBtn.onClick.RemoveAllListeners();
            fiftyBtn.onClick.AddListener(() => SelectTier(RepairOption.FiftyPercent));
        }
        if (fullBtn)
        {
            fullBtn.onClick.RemoveAllListeners();
            fullBtn.onClick.AddListener(() => SelectTier(RepairOption.Full));
        }

        if (repairButton)
        {
            repairButton.onClick.RemoveAllListeners();
            repairButton.onClick.AddListener(DoRepair);
        }

        RootGO.SetActive(false);
    }

    public void OpenFor(BuildingControl building)
    {
        ShowFor(building); // keep old name working
    }

    public void Close()
    {
        Hide();
    }

    public void ShowFor(BuildingControl building)
    {
        if (!building) return;

        _building = building;
        _health   = building.GetComponent<BuildingHealth>();
        _repair   = building.GetComponent<BuildingRepair>();

        if (!_health || !_repair) { Hide(); return; }

        if (titleText)
        {
            var defName = BuildingManager.Instance?.GetBuildingByID(building.buildingID)?.buildingName;
            var display = string.IsNullOrWhiteSpace(building.buildingName) ? (defName ?? building.buildingID)
                                                                        : building.buildingName;
            titleText.text = $"Repair: {display}";
        }

        RootGO.SetActive(true);

        // Base interactivity (blocked only if destroyed + policy)
        var status = _building.GetComponent<BuildingStatus>();
        bool destroyedBlocked = status && status.CurrentState == BuildingState.Destroyed && _repair.allowRepairWhenDestroyed == false;

        SetAllInteractable(!destroyedBlocked);

        // Visibility: if can attempt repair now => show costs, hide hint. Else show hint and hide costs.
        if (CanAttemptRepairNow())
        {
            if (stateHintText) stateHintText.gameObject.SetActive(false);
            if (costsRoot)     costsRoot.SetActive(true);
        }
        else
        {
            // Decide the correct hint message
            string hint = destroyedBlocked ? "Cannot repair while Destroyed."
                                        : "Fully repaired.";
            SetStateHint(hint, isWarning: destroyedBlocked);
        }

        _selected = RepairOption.TenPercent;
        PaintTierButtons();
        RefreshWorkUI();
        RebuildCosts(); // will keep costsRoot true when repairable, or keep it hidden if we showed a hint
    }

    public void Hide()
    {
        if (root != null)
            RootGO.SetActive(false);
        else
            gameObject.SetActive(false);

        ClearCosts();
        _building = null;
        _health   = null;
        _repair   = null;
    }

    // -------- internals --------

    private void SelectTier(RepairOption opt)
    {
        _selected = opt;
        PaintTierButtons();
        RefreshWorkUI();
        RebuildCosts();
    }

    private void PaintTierButtons()
    {
        Paint(tenBtn,   _selected == RepairOption.TenPercent);
        Paint(fiftyBtn, _selected == RepairOption.FiftyPercent);
        Paint(fullBtn,  _selected == RepairOption.Full);

        void Paint(Button b, bool sel)
        {
            if (!b) return;
            if (b.targetGraphic is Image img)
                img.color = sel ? selectedTierColor : normalTierColor;
        }
    }

    private void RebuildCosts()
    {
        if (!_repair || !costEntryPrefab || !costsContentRoot) return;

        ClearCosts();

        // If no repair can be attempted now, keep hint shown and costs hidden.
        if (!CanAttemptRepairNow())
        {
            if (costsRoot) costsRoot.SetActive(false);
            // hint already set in ShowFor or DoRepair
            if (repairButton) repairButton.interactable = false;
            return;
        }

        // We can repair: hide hint, show costs
        if (stateHintText) stateHintText.gameObject.SetActive(false);
        if (costsRoot)     costsRoot.SetActive(true);

        var lines = _repair.GetRepairCosts(_selected);
        bool canAfford = _repair.CanAfford(_selected); // will fix this next

        if (lines != null)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                int have = InventoryQuery.GetOwned(line.resource); // <— change here
                var entry = Instantiate(costEntryPrefab, costsContentRoot);
                entry.Bind(line.resource, line.amount, have);
                _spawned.Add(entry);
            }
        }
        
        RefreshWorkUI();

        if (repairButton)
            repairButton.interactable = canAfford;

        // If there are literally no cost lines for this tier, still keep costsRoot visible (repair is possible),
        // but do NOT show the state hint anymore per your rule.
    }

    private void ClearCosts()
    {
        for (int i = 0; i < _spawned.Count; i++)
            if (_spawned[i]) Destroy(_spawned[i].gameObject);
        _spawned.Clear();
    }

    private void DoRepair()
    {
        if (!_repair) return;

        if (_repair.TryRepair(_selected))
        {
            // After successful repair, check if more repair is needed or we’re done.
            if (CanAttemptRepairNow())
            {
                // Still needs repair → keep hint hidden, costs visible
                if (stateHintText) stateHintText.gameObject.SetActive(false);
                if (costsRoot)     costsRoot.SetActive(true);
                RebuildCosts();
            }
            else
            {
                // No more repair to do
                SetStateHint("Fully repaired.");
                if (repairButton) repairButton.interactable = false;
            }
        }
        else
        {
            // Recompute UI if spend/affordability/policy prevented repair
            RebuildCosts();
        }
    }

    private void SetAllInteractable(bool on)
    {
        if (tenBtn)   tenBtn.interactable   = on;
        if (fiftyBtn) fiftyBtn.interactable = on;
        if (fullBtn)  fullBtn.interactable  = on;
        if (repairButton) repairButton.interactable = on;
    }

    private void SetStateHint(string msg, bool isWarning = false)
    {
        if (!stateHintText) return;

        if (string.IsNullOrEmpty(msg))
        {
            stateHintText.gameObject.SetActive(false);
        }
        else
        {
            if (costsRoot) costsRoot.SetActive(false);
            stateHintText.gameObject.SetActive(true);
            stateHintText.text = msg;
        }
    }

    private bool CanAttemptRepairNow()
    {
        if (_health == null || _repair == null) return false;

        if (_repair.IsRepairing)
            return false;

        var status = _building ? _building.GetComponent<BuildingStatus>() : null;
        bool destroyedBlocked = status && status.CurrentState == BuildingState.Destroyed && _repair.allowRepairWhenDestroyed == false;

        if (destroyedBlocked) return false;

        return _health.CurrentHealth < _health.maxHealth;
    }

    private void RefreshWorkUI()
    {
        if (_repair == null) return;

        var (turns, pop) = _repair.GetScaledWork(_selected);

        if (scaledTurnsText)       scaledTurnsText.text       = $"{turns}";
        if (scaledPopulationText)  scaledPopulationText.text  = $"{pop}";
    }

}
