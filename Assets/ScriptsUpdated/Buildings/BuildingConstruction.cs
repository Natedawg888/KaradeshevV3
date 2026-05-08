using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BuildingConstruction : MonoBehaviour
{
    [Header("Stages (index 0..N-1)")]
    public List<GameObject> constructionStages = new(); // assign in prefab

    [Header("UI")]
    public GameObject constructionCanvas;   // parent canvas
    public TimerUI constructTimerUI;        // your existing timer UI

    [Header("Final Building")]
    [Tooltip("If left null, PlayerConstructionManager will use Building.finalBuildingPrefab.")]
    public GameObject finalBuildingOverride;

    [Header("Visual Options")]
    [Tooltip("If true, the first frame will show the middle stage of constructionStages.")]
    public bool startInMiddle = false;      // NEW

    // Internal state
    private int turnsToComplete;
    private int turnsLeft;
    private int currentStageIndex = -1;
    private GameObject currentStageGO;
    private Building def;
    private string reservationId;  // owned by PlayerConstructionManager
    private int reservedPopulation;
    private bool isActive;

    // One-shot visual pin to middle on the first SetStageByProgress call
    private bool _forceMiddleOnce = false;  // NEW

    public int TurnsToComplete => turnsToComplete;
    public int TurnsLeft => turnsLeft;
    public bool IsActive => isActive;

    public void Initialize(Building building, int turnsRequired, int popReserved, string reservationToken)
    {
        def                = building;
        turnsToComplete    = Mathf.Max(1, turnsRequired);
        turnsLeft          = turnsToComplete;
        reservedPopulation = Mathf.Max(1, popReserved);
        reservationId      = reservationToken;
    }

    public void BeginConstruction()
    {
        isActive = true;

        // one-shot pin for the first visual update
        _forceMiddleOnce = startInMiddle;   // NEW

        //Debug.Log($"[BuildingConstruction] Starting on '{gameObject.name}' " +
                  //$"for '{def?.buildingName ?? "NULL"}' | turns:{turnsToComplete} | stages:{(constructionStages != null ? constructionStages.Count : 0)}");

        if (constructionCanvas) constructionCanvas.SetActive(true);
        if (constructTimerUI)
        {
            constructTimerUI.Initialize(turnsToComplete);
            constructTimerUI.UpdateTimer(turnsLeft);
        }

        SetStageByProgress();
    }

    public bool AdvanceOneTurn()
    {
        if (!isActive) return false;

        turnsLeft = Mathf.Max(0, turnsLeft - 1);
        if (constructTimerUI) constructTimerUI.UpdateTimer(turnsLeft);

        SetStageByProgress();

        return turnsLeft <= 0;
    }

    public GameObject CompleteAndSpawnFinal()
    {
        isActive = false;

        if (currentStageGO) Destroy(currentStageGO);
        if (constructionCanvas) constructionCanvas.SetActive(false);

        GameObject finalPrefab = finalBuildingOverride ? finalBuildingOverride : def?.finalBuildingPrefab;
        if (!finalPrefab)
        {
            //Debug.LogError("[BuildingConstruction] No final building prefab provided.");
            return null;
        }

        var pos = transform.position;
        var rot = transform.rotation;
        var finalGO = Instantiate(finalPrefab, pos, rot);

        var bc = finalGO.GetComponent<BuildingControl>() 
                ?? finalGO.GetComponentInChildren<BuildingControl>(true);
        if (bc != null && def != null)
        {
            bc.buildingID   = def.buildingID;
            bc.buildingName = def.buildingName;
            bc.buildingType = def.buildingType;

            var tag = finalGO.GetComponent<BuildingInstance>() 
                    ?? finalGO.AddComponent<BuildingInstance>();
            if (string.IsNullOrEmpty(tag.instanceId))
                tag.instanceId = Guid.NewGuid().ToString();
            tag.definition = def;
        }
        else
        {
            //Debug.LogWarning("[BuildingConstruction] Final building missing BuildingControl or def was null; ID/Name not set.");
        }

        return finalGO;
    }

    private void SetStageByProgress()
    {
        if (constructionStages == null || constructionStages.Count == 0)
            return;

        // NEW: first frame can force the middle stage visually
        if (_forceMiddleOnce)
        {
            _forceMiddleOnce = false;
            int mid = (constructionStages.Count - 1) / 2;
            if (currentStageGO) Destroy(currentStageGO);
            currentStageIndex = mid;

            var prefab = constructionStages[mid];
            if (prefab)
            {
                currentStageGO = Instantiate(prefab, transform);
                currentStageGO.transform.localPosition = Vector3.zero;
                currentStageGO.transform.localRotation = Quaternion.identity;
            }
            return; // don't compute normal progress this frame
        }

        // Regular progress-based stage
        float progress = 1f - (turnsLeft / Mathf.Max(1f, (float)turnsToComplete)); // 0..1
        int idx = Mathf.Clamp(Mathf.RoundToInt(progress * (constructionStages.Count - 1)), 0, constructionStages.Count - 1);

        if (idx == currentStageIndex && currentStageGO != null) return;

        if (currentStageGO) Destroy(currentStageGO);
        currentStageIndex = idx;

        var stagePrefab = constructionStages[currentStageIndex];
        if (stagePrefab)
        {
            currentStageGO = Instantiate(stagePrefab, transform);
            currentStageGO.transform.localPosition = Vector3.zero;
            currentStageGO.transform.localRotation = Quaternion.identity;
        }
    }

    private void OnDestroy()
    {
        if (constructionCanvas) constructionCanvas.SetActive(false);
        if (currentStageGO) Destroy(currentStageGO);
    }

    public Building Definition => def;
    public string ReservationId => reservationId;
    public int ReservedPopulation => reservedPopulation;

    public BuildingConstructionRuntimeSaveData CaptureRuntimeSaveData()
    {
        return new BuildingConstructionRuntimeSaveData
        {
            buildingID = def != null ? def.buildingID : string.Empty,
            finalBuildingOverridePrefabName = finalBuildingOverride != null ? finalBuildingOverride.name : string.Empty,

            turnsToComplete = turnsToComplete,
            turnsLeft = turnsLeft,
            currentStageIndex = currentStageIndex,

            reservationId = reservationId,
            reservedPopulation = reservedPopulation,
            isActive = isActive,
            startInMiddle = startInMiddle
        };
    }

    public void ApplyRuntimeSaveData(BuildingConstructionRuntimeSaveData data, Building resolvedDef, GameObject resolvedFinalOverride = null)
    {
        if (data == null)
            return;

        def = resolvedDef;
        if (resolvedFinalOverride != null)
            finalBuildingOverride = resolvedFinalOverride;

        turnsToComplete = Mathf.Max(1, data.turnsToComplete);
        turnsLeft = Mathf.Clamp(data.turnsLeft, 0, turnsToComplete);

        reservationId = data.reservationId;
        reservedPopulation = Mathf.Max(1, data.reservedPopulation);

        isActive = data.isActive;
        startInMiddle = data.startInMiddle;

        _forceMiddleOnce = false;

        if (currentStageGO) Destroy(currentStageGO);
        currentStageGO = null;
        currentStageIndex = -1;

        if (constructionCanvas)
            constructionCanvas.SetActive(isActive);

        if (constructTimerUI)
        {
            constructTimerUI.Initialize(turnsToComplete);
            constructTimerUI.UpdateTimer(turnsLeft);
            constructTimerUI.gameObject.SetActive(isActive);
        }

        ForceStage(Mathf.Clamp(data.currentStageIndex, 0, Mathf.Max(0, constructionStages.Count - 1)));
    }

    private void ForceStage(int idx)
    {
        if (constructionStages == null || constructionStages.Count == 0)
            return;

        if (currentStageGO) Destroy(currentStageGO);

        currentStageIndex = Mathf.Clamp(idx, 0, constructionStages.Count - 1);

        GameObject stagePrefab = constructionStages[currentStageIndex];
        if (stagePrefab != null)
        {
            currentStageGO = Instantiate(stagePrefab, transform);
            currentStageGO.transform.localPosition = Vector3.zero;
            currentStageGO.transform.localRotation = Quaternion.identity;
        }
    }
}
