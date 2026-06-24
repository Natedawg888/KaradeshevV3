using System;
using System.Collections.Generic;
using UnityEngine;

public class BuildingPlacementManager : MonoBehaviour
{
    public static BuildingPlacementManager Instance { get; private set; }

    [Header("Panel")]
    [SerializeField] private BuildingPlacementPanelControl panel;

    [Header("Camera")]
    [SerializeField] private CameraControl cameraControl;

    [Header("References")]
    [SerializeField] private GridManager gridManager;

    private List<ResourceCost> selectedCostSnapshot;

    private Building def;
    private EnvironmentControl env;
    private GameObject previewInstance;
    private string reservationId;

    public event Action<BuildingConstruction> OnPlacementFinalized;
    public static bool TutorialBypassCosts = false;

    private bool usedFinalAsPreview = false;
    private List<(Renderer r, bool wasEnabled)> envRenderersState;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public bool IsPlacing => previewInstance != null;

    public void BeginPlacement(Building building, EnvironmentControl targetEnv)
    {
        if (IsPlacing)
            CancelPlacement();

        def = building;
        env = targetEnv;

        if (def == null || env == null)
        {
            //Debug.LogWarning("[Placement] BeginPlacement missing def/env.");
            return;
        }

        selectedCostSnapshot = new List<ResourceCost>();
        var active = def.GetActiveBuildCosts();
        if (active != null)
        {
            foreach (var rc in active)
            {
                if (rc != null)
                {
                    selectedCostSnapshot.Add(new ResourceCost
                    {
                        resource = rc.resource,
                        amount = rc.amount
                    });
                }
            }
        }

        int needPop = Mathf.Max(1, def.requireBuildPopulation);

        if (!PlayersPopulationManager.Instance.TryPickRandomNonBusyTaskIndividuals(
                needPop, out var picked, out reservationId))
        {
            //Debug.Log("[Placement] Failed to reserve/build workers (none available or all busy).");
            return;
        }

        if (picked == null || picked.Count != needPop)
        {
            PlayersPopulationManager.Instance.ReleaseBusyIndividuals(reservationId, picked);
            reservationId = null;
            //Debug.Log("[Placement] Did not get the exact required worker count.");
            return;
        }

        PlayersPopulationManager.Instance?.ForceSyncUI();

        if (!TutorialBypassCosts)
            PlayerInventoryManager.Instance?.ReserveResources(selectedCostSnapshot);

        GameObject prefab = def.finalBuildingPrefab != null ? def.finalBuildingPrefab : def.buildingPrefab;
        usedFinalAsPreview = (prefab == def.finalBuildingPrefab);

        if (prefab == null)
        {
            //Debug.LogError("[Placement] No building prefab assigned.");
            ReleaseReservation();
            return;
        }

        previewInstance = Instantiate(prefab, env.transform.position, SnapYTo90(env.transform.rotation));
        SetPreviewVisual(previewInstance, true);

        SetEnvironmentHidden(true);

        TileInteraction.SetSelectionEnabled(false);
        TileInteraction.GetInstance()?.DeselectCurrent();

        cameraControl?.FocusOnPoint(env.transform.position, env.transform.forward, 6f);
        cameraControl?.SaveCameraPose();

        if (panel != null)
        {
            var rootGO = panel.GetComponentInParent<Canvas>(true)?.gameObject ?? panel.transform.root.gameObject;
            if (!rootGO.activeSelf)
                rootGO.SetActive(true);

            if (!panel.gameObject.activeSelf)
                panel.gameObject.SetActive(true);

            panel.Bind(this, previewInstance, def);
        }
        else
        {
            //Debug.LogError("[Placement] BuildingPlacementPanelControl reference is missing.");
        }
    }

    public void RotateQuarter(int dir)
    {
        if (!IsPlacing || previewInstance == null)
            return;

        if (def != null && !def.canRotate)
            return;

        dir = Math.Sign(dir);
        if (dir == 0)
            return;

        var y = previewInstance.transform.eulerAngles.y;
        float baseSnap = Mathf.Round(y / 90f) * 90f;
        previewInstance.transform.rotation = Quaternion.Euler(0f, baseSnap + dir * 90f, 0f);
    }

    public void FinalizePlacement()
    {
        if (!IsPlacing)
        {
            //Debug.LogWarning("[Placement] Finalize called but not placing.");
            return;
        }

        if (!ValidatePlacement(previewInstance, def, env))
        {
            //Debug.Log("[Placement] Invalid placement.");
            return;
        }

        PlayerInventoryManager.Instance?.ClearResourceReservation();

        if (!TutorialBypassCosts && !ResourceDeduction.Deduct(selectedCostSnapshot))
        {
            //Debug.LogWarning("[Placement] Resource deduction failed at finalize.");
            return;
        }

        if (def.buildingPrefab == null)
        {
            //Debug.LogError("[Placement] No construction prefab (buildingPrefab) set on Building.");
            CancelPlacement();
            return;
        }

        var pos = previewInstance.transform.position;
        var rot = previewInstance.transform.rotation;

        Destroy(previewInstance);
        previewInstance = null;

        var constructionGO = Instantiate(def.buildingPrefab, pos, rot);
        //Debug.Log($"[Placement] Spawned constructionGO '{constructionGO.name}' at {pos}.");

        bool ok = PlayerConstructionManager.Instance
            && PlayerConstructionManager.Instance.StartConstruction(
                constructionGO,
                def,
                reservationId,
                def.requireBuildPopulation,
                def.buildTurnsRequired
            );

        if (!ok)
        {
            //Debug.LogWarning("[Placement] StartConstruction returned false. Destroying constructionGO and restoring environment.");
            if (constructionGO != null)
                Destroy(constructionGO);

            ReleaseReservation();
            SetEnvironmentHidden(false);
        }
        else
        {
            reservationId = null;

            EjectAnimalsFromPlacementTile();

            //Debug.Log("[Placement] Construction started successfully – destroying environment tile.");
            DestroyEnvironmentUnderConstruction();

            var bc = constructionGO != null ? constructionGO.GetComponent<BuildingConstruction>() : null;
            if (bc != null) OnPlacementFinalized?.Invoke(bc);
        }

        TileInteraction.SetSelectionEnabled(false);
        TileInteraction.GetInstance()?.EnableSelectionAfter(0.01f);

        cameraControl?.RestoreCameraPose();

        EndPlacement(success: true);
    }

    private void DestroyEnvironmentUnderConstruction()
    {
        if (env == null)
            return;

        var tile = env.GetComponent<TileControl>();
        GameObject toDestroy = null;

        if (tile != null && tile.transform.parent != null)
            toDestroy = tile.transform.parent.gameObject;
        else
            toDestroy = env.gameObject;

        if (toDestroy != null)
            Destroy(toDestroy);
    }

    public void CancelPlacement()
    {
        if (!IsPlacing)
            return;

        if (previewInstance != null)
            Destroy(previewInstance);

        previewInstance = null;

        ReleaseReservation();

        TileInteraction.SetSelectionEnabled(false);
        TileInteraction.GetInstance()?.EnableSelectionAfter(0.01f);

        SetEnvironmentHidden(false);

        EndPlacement(success: false);
    }

    private void EndPlacement(bool success)
    {
        if (panel != null)
            panel.gameObject.SetActive(false);

        def = null;
        env = null;
        usedFinalAsPreview = false;
        envRenderersState = null;
        selectedCostSnapshot = null;
    }

    private void ReleaseReservation()
    {
        PlayerInventoryManager.Instance?.ClearResourceReservation();

        if (!string.IsNullOrEmpty(reservationId))
        {
            PlayersPopulationManager.Instance.ReleaseReservation(reservationId);
            reservationId = null;
        }
    }

    private void SetPreviewVisual(GameObject go, bool isPreview)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            foreach (var m in r.materials)
            {
                if (m == null)
                    continue;

                // optional ghost tint/alpha here
            }
        }
    }

    private bool ValidatePlacement(GameObject preview, Building b, EnvironmentControl e)
    {
        if (b == null || e == null)
            return false;

        if (!b.requiredEnvironmentTypes.Contains(e.environmentType))
            return false;

        if (!b.requiredEnvironmentTileTypes.Contains(e.environmentTileType))
            return false;

        return true;
    }

    private Quaternion SnapYTo90(Quaternion q)
    {
        var e = q.eulerAngles;
        e.y = Mathf.Round(e.y / 90f) * 90f;
        return Quaternion.Euler(0f, e.y, 0f);
    }

    private void SetEnvironmentHidden(bool hidden)
    {
        if (env == null)
            return;

        if (hidden)
        {
            envRenderersState = new List<(Renderer, bool)>();

            foreach (var r in env.GetComponentsInChildren<Renderer>(true))
            {
                envRenderersState.Add((r, r.enabled));
                r.enabled = false;
            }
        }
        else if (envRenderersState != null)
        {
            foreach (var (r, wasEnabled) in envRenderersState)
            {
                if (r != null)
                    r.enabled = wasEnabled;
            }
        }
    }

    private void EjectAnimalsFromPlacementTile()
    {
        if (env == null)
            return;

        if (gridManager == null)
        {
            //Debug.LogWarning("[Placement] GridManager reference is missing, cannot eject animals.");
            return;
        }

        if (AnimalSimulationAccess.Current == null)
        {
            //Debug.LogWarning("[Placement] AnimalSimulationAccess.Current is null, cannot eject animals.");
            return;
        }

        var tile = env.GetComponent<TileControl>();
        if (tile == null)
        {
            //Debug.LogWarning("[Placement] No TileControl found on environment tile, cannot eject animals.");
            return;
        }

        Vector2Int gridPos = gridManager.GetGridPosition(tile.transform.position);
        TileCoord coord = new TileCoord(gridPos.x, gridPos.y);

        int moved = AnimalSimulationAccess.Current.EjectGroupsFromDestroyedTile(coord);

        if (moved > 0) {}
            //Debug.Log($"[Placement] Ejected {moved} animal group(s) from tile ({coord.x}, {coord.y}).");
    }

    public void InstallRuntimeRefs(
    BuildingPlacementPanelControl newPanel = null,
    CameraControl newCameraControl = null,
    GridManager newGridManager = null)
    {
        if (newPanel != null)
            panel = newPanel;

        if (newCameraControl != null)
            cameraControl = newCameraControl;

        if (newGridManager != null)
            gridManager = newGridManager;
    }
}
