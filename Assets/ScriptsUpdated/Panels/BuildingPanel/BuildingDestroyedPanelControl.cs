using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

public class BuildingDestroyedPanelControl : MonoBehaviour
{
    [Header("UI Roots")]
    public GameObject root;
    public Button closeButton;

    [Header("Header")]
    public TMP_Text titleText;
    public TMP_Text coordinatesText;

    [Header("Restore")]
    public Button restoreButton;

    [Header("Restore Info")]
    public Button openRestoreInfoButton;
    public RestoreInfoPanelControl restoreInfoPanel;

    [Header("Manual Clear")]
    public Button manualClearButton;

    [Header("Clear Info")]
    public Button openClearInfoButton;
    public ClearInfoPanelControl clearInfoPanel;

    public CameraControl cameraControl;

    private BuildingControl currentBuilding;
    private TileControl currentTile;

    // cache for restore costs
    private List<ResourceCost> _restoreCosts;
    private int _restoreTurns;
    private int _restorePop;

    public event Action OnClose;

    public bool IsShowing => root != null && root.activeInHierarchy;
    public BuildingControl CurrentBuilding => currentBuilding;
    public Func<BuildingControl, bool> TutorialClearOverride;

    private void Start()
    {

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }

        if (restoreButton != null)
        {
            restoreButton.onClick.RemoveAllListeners();
            restoreButton.onClick.AddListener(OnClickRestore);
        }

        if (openRestoreInfoButton != null)
        {
            openRestoreInfoButton.onClick.RemoveAllListeners();
            openRestoreInfoButton.onClick.AddListener(OpenRestoreInfoPanel);
        }

        if (manualClearButton)
        {
            manualClearButton.onClick.RemoveAllListeners();
            manualClearButton.onClick.AddListener(OnClickManualClear);
        }

        if (openClearInfoButton)
        {
            openClearInfoButton.onClick.RemoveAllListeners();
            openClearInfoButton.onClick.AddListener(OpenClearInfoPanel);
        }

        if (cameraControl == null)
            cameraControl = FindObjectOfType<CameraControl>();
    }

    public void Show(BuildingControl building, TileControl tile = null)
    {
        currentBuilding = building;
        currentTile     = tile != null ? tile : building.GetComponentInParent<TileControl>();

        TileInteraction.SetSelectionEnabled(false);

        cameraControl.PushInputLock();

        root.SetActive(true);

        // Title: "<Name> (Destroyed)"
        string baseName = !string.IsNullOrWhiteSpace(building.buildingName)
            ? building.buildingName
            : (BuildingManager.Instance?.GetBuildingByID(building.buildingID)?.buildingName
               ?? building.buildingID);
        if (titleText) titleText.text = $"{baseName} (Destroyed)";

        // Coords
        if (coordinatesText)
        {
            Vector2Int coords = Vector2Int.zero;
            if (currentTile != null) coords = currentTile.GetGridPosition();
            coordinatesText.text = $"Coordinates: {coords.x}, {coords.y}";
        }

        // Compute restore params (cost / time / pop)
        ComputeRestoreParams();

        UpdateManualClearButtonState();

        // Enable/disable restore button based on affordability (without opening the info panel)
        if (restoreButton) restoreButton.interactable = CanAffordRestore();

        // Ensure the info panel starts closed
        if (restoreInfoPanel) restoreInfoPanel.Hide();
    }

    public void Hide()
    {
        TileInteraction.SetSelectionEnabled(false);
        TileInteraction.GetInstance()?.EnableSelectionAfter(0.01f);

        cameraControl.PopInputLock();

        root.SetActive(false);
        OnClose?.Invoke();

        if (restoreInfoPanel) restoreInfoPanel.Hide();

        currentBuilding = null;
        currentTile     = null;
    }

    private void UpdateManualClearButtonState()
    {
        if (!manualClearButton || currentBuilding == null)
            return;

        // During the building tutorial, always allow the Clear button
        // so the tutorial can hijack the click instantly.
        if (IsTutorialClearActive())
        {
            manualClearButton.interactable = true;
            return;
        }

        var status = currentBuilding.GetComponent<BuildingStatus>();
        var def = BuildingManager.Instance?.GetBuildingByID(currentBuilding.buildingID);

        bool enable = false;

        if (status != null && def != null && status.CurrentState == BuildingState.Destroyed)
        {
            int remainingAuto = status.AutoClearTurnsRemaining;
            int manualTurns = Mathf.Max(0, def.manualClearTurns);

            bool timingOK = (remainingAuto > manualTurns);
            bool canAfford = InventoryQuery.CanAfford(def.manualClearCosts);

            enable = timingOK && canAfford;
        }

        manualClearButton.interactable = enable;
    }

    private bool IsTutorialClearActive()
    {
        return currentBuilding != null && TutorialClearOverride != null;
    }

    // ---------- Restore Info Toggle ----------

    private void OpenRestoreInfoPanel()
    {
        if (!restoreInfoPanel)
        {
            //Debug.LogError("[DestroyedPanel] restoreInfoPanel reference is missing.");
            return;
        }

        // Recompute in case inventory changed since Show()
        ComputeRestoreParams();

        // Make sure the component is enabled so it can run Show/logic
        if (!restoreInfoPanel.enabled) restoreInfoPanel.enabled = true;

        if (!restoreInfoPanel.gameObject.activeSelf)
            restoreInfoPanel.gameObject.SetActive(true);

        restoreInfoPanel.Show(_restoreCosts, _restoreTurns, _restorePop);

        // Sync the main Restore button to affordability as computed by the info panel
        if (restoreButton) restoreButton.interactable = CanAffordRestore();
    }

    // ---------- Restore Core ----------

    private void OnClickRestore()
    {
        if (currentBuilding == null)
        {
            //Debug.LogError("[DestroyedPanel] currentBuilding is null in OnClickRestore.");
            return;
        }

        var def = BuildingManager.Instance?.GetBuildingByID(currentBuilding.buildingID);
        if (def == null)
        {
            //Debug.LogError($"[DestroyedPanel] Could not resolve Building definition for '{currentBuilding.buildingID}'.");
            return;
        }
        if (def.buildingPrefab == null)
        {
            //Debug.LogError($"[DestroyedPanel] Definition '{def.buildingID}' has no construction prefab (buildingPrefab).");
            return;
        }

        // 1) Pay resources
        if (!SpendRestoreCosts())
        {
            //Debug.LogWarning("[DestroyedPanel] SpendRestoreCosts() returned false; aborting restore.");
            if (restoreButton) restoreButton.interactable = false;
            return;
        }

        // 2) Kick off the pose-sampling + spawn routine
        StartCoroutine(RestoreRoutine(def));
    }

    private IEnumerator RestoreRoutine(Building def)
    {
        // Capture parent (tile if present) only for DUMMY snapping
        Transform tileParent = currentTile ? currentTile.transform :
                            (currentBuilding ? currentBuilding.transform.parent : null);

        if (tileParent == null) {}
            //Debug.LogWarning("[DestroyedPanel] No tile parent; will place in world space.");

        // --- A) Spawn a DUMMY to get the snapped pose (PARENTED to the tile) ---
        GameObject dummy = Instantiate(def.buildingPrefab);
        if (tileParent != null)
        {
            dummy.transform.SetParent(tileParent, false);
            dummy.transform.localPosition = Vector3.zero;
            dummy.transform.localRotation = Quaternion.identity;
        }
        //Debug.Log("[DestroyedPanel] Dummy construction spawned to sample pose.");

        // Allow layout/snapping scripts to run
        yield return null;

        // Read pose from dummy, then delete it
        Vector3 snapPos = dummy.transform.position;
        Quaternion snapRot = dummy.transform.rotation;
        Destroy(dummy);
        //Debug.Log($"[DestroyedPanel] Dummy deleted. Captured pose pos={snapPos} rot={snapRot.eulerAngles}");

        // --- B) Spawn the REAL construction UNPARENTED (world root) ---
        GameObject newConstructionGO = Instantiate(def.buildingPrefab);
        newConstructionGO.transform.SetParent(null, false);
        newConstructionGO.transform.position = snapPos;
        newConstructionGO.transform.rotation = snapRot;
        newConstructionGO.transform.localScale = Vector3.one;

        // NEW: start the visuals in the middle
        var bc = newConstructionGO.GetComponent<BuildingConstruction>();
        if (bc) bc.startInMiddle = true;

        // Start construction via manager so turns/pop are respected
        var pcm = PlayerConstructionManager.Instance;
        if (pcm != null)
        {
            bool ok = pcm.StartConstruction(
                newConstructionGO,
                def,
                reservationIdFromPlacement: null,
                reservedPop: _restorePop,
                turnsRequired: _restoreTurns
            );
            if (!ok)
            {
                //Debug.LogWarning("[DestroyedPanel] StartConstruction failed; destroying spawned construction.");
                Destroy(newConstructionGO);
                yield break;
            }
        }

        // --- C) Now destroy the ruined building (coroutine work already done) ---
        if (currentBuilding != null)
        {
            var ruinedGO = currentBuilding.gameObject;
            currentBuilding = null;
            Destroy(ruinedGO);
            //Debug.Log("[DestroyedPanel] Ruined building destroyed.");
        }

        Hide();
    }

    private void OnClickManualClear()
    {
        if (!currentBuilding)
        {
            //Debug.LogError("[DestroyedPanel] No currentBuilding.");
            return;
        }

        if (currentBuilding != null &&
            TutorialClearOverride != null &&
            TutorialClearOverride.Invoke(currentBuilding))
        {
            Hide();
            return;
        }

        var def = BuildingManager.Instance?.GetBuildingByID(currentBuilding.buildingID);
        if (def == null)
        {
            //Debug.LogError($"[DestroyedPanel] No Building def for '{currentBuilding.buildingID}'.");
            return;
        }

        // Let the manager handle pop, costs, ticking, rewards, clearing:
        bool ok = PlayerClearingManager.Instance?.StartManualClear(currentBuilding, def) ?? false;
        if (ok) Hide();
        //else Debug.LogWarning("[DestroyedPanel] Manual clear did not start.");
    }

    private void ComputeRestoreParams()
    {
        _restoreCosts = new List<ResourceCost>();
        _restoreTurns = 1;
        _restorePop = 1;

        if (currentBuilding == null) return;

        var def = BuildingManager.Instance?.GetBuildingByID(currentBuilding.buildingID);
        if (def == null) return;

        // 50% costs, ceil to int, min 1
        if (def.buildCosts != null)
        {
            for (int i = 0; i < def.buildCosts.Count; i++)
            {
                var line = def.buildCosts[i];
                if (line == null || line.resource == null) continue;

                int half = Mathf.CeilToInt(line.amount * 0.5f);
                half = Mathf.Max(1, half); // "rounding to 1. 0.1 = 1"
                _restoreCosts.Add(new ResourceCost { resource = line.resource, amount = half });
            }
        }

        // Half turns / half pop, ceil, min 1
        _restoreTurns = Mathf.Max(1, Mathf.CeilToInt(def.buildTurnsRequired * 0.5f));
        _restorePop = Mathf.Max(1, Mathf.CeilToInt(def.requireBuildPopulation * 0.5f));
    }
    
    private List<ResourceCost> BuildAggregatedCosts(List<ResourceCost> src)
    {
        var result = new List<ResourceCost>();
        if (src == null || src.Count == 0) return result;

        // Use a dictionary keyed by ResourceDefinition reference
        var map = new Dictionary<ResourceDefinition, int>();
        for (int i = 0; i < src.Count; i++)
        {
            var c = src[i];
            if (c == null || c.resource == null || c.amount <= 0) continue;

            if (!map.TryGetValue(c.resource, out int cur))
                map[c.resource] = c.amount;
            else
                map[c.resource] = cur + c.amount;
        }

        foreach (var kv in map)
            result.Add(new ResourceCost { resource = kv.Key, amount = kv.Value });

        return result;
    }

    private bool CanAffordRestore()
    {
        var aggregated = BuildAggregatedCosts(_restoreCosts);
        return InventoryQuery.CanAfford(aggregated);
    }

    private bool SpendRestoreCosts()
    {
        if (_restoreCosts == null || _restoreCosts.Count == 0) return true;

        var aggregated = BuildAggregatedCosts(_restoreCosts);

        // Final pre-check against the same (aggregated) list used for spending
        if (!InventoryQuery.CanAfford(aggregated)) return false;

        var pim = PlayerInventoryManager.Instance;
        if (pim == null)
        {
            //Debug.LogError("[DestroyedPanel] PlayerInventoryManager.Instance is null.");
            return false;
        }

        // We'll only track rollback items for non-group lines (exact defs).
        var rollback = new List<ResourceCost>();

        for (int i = 0; i < aggregated.Count; i++)
        {
            var line = aggregated[i];
            if (line == null || line.resource == null) continue;
            if (line.resource.isGroup) continue; // skip here; handle in pass 2

            if (!pim.TryRemove(line.resource, line.amount))
            {
                // rollback non-group spends so far
                for (int r = 0; r < rollback.Count; r++)
                    pim.TryAdd(rollback[r].resource, rollback[r].amount);

                //Debug.LogWarning("[DestroyedPanel] Spend failed on non-group; rolled back.");
                return false;
            }
            rollback.Add(line);
        }

        for (int i = 0; i < aggregated.Count; i++)
        {
            var line = aggregated[i];
            if (line == null || line.resource == null) continue;
            if (!line.resource.isGroup) continue;

            if (!pim.TryRemoveGroup(line.resource, line.amount))
            {
                for (int r = 0; r < rollback.Count; r++)
                    pim.TryAdd(rollback[r].resource, rollback[r].amount);

                //Debug.LogWarning("[DestroyedPanel] Group spend failed unexpectedly after afford-check.");
                return false;
            }
        }

        return true;
    }

    private void OpenClearInfoPanel()
    {
        if (!currentBuilding)
        {
            //Debug.LogError("[DestroyedPanel] No currentBuilding for ClearInfo.");
            return;
        }

        var def = BuildingManager.Instance?.GetBuildingByID(currentBuilding.buildingID);
        if (def == null)
        {
            //Debug.LogError("[DestroyedPanel] Building def not found for ClearInfo.");
            return;
        }

        var status = currentBuilding.GetComponent<BuildingStatus>();
        if (!status)
        {
            //Debug.LogError("[DestroyedPanel] BuildingStatus missing for ClearInfo.");
            return;
        }

        if (!clearInfoPanel)
        {
            //Debug.LogError("[DestroyedPanel] clearInfoPanel reference missing.");
            return;
        }

        int remainingAuto = status.AutoClearTurnsRemaining; // int.MaxValue if disabled
        if (!clearInfoPanel.enabled) clearInfoPanel.enabled = true;
        if (!clearInfoPanel.gameObject.activeSelf) clearInfoPanel.gameObject.SetActive(true);

        clearInfoPanel.Show(
            def.manualClearCosts,
            def.manualClearRewards,
            def.manualClearTurns,
            def.manualClearPopulation,
            remainingAuto
        );
    }
}
