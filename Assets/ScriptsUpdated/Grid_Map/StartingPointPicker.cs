using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class StartingPointPicker : MonoBehaviour
{
    [Header("Assign at Runtime")]
    public GameObject panel;
    public Button prevButton, nextButton, confirmButton;
    public CameraControl cameraControl;
    public TileActivator tileActivator;

    [Header("Tutorial")]
    [SerializeField] private CameraIntroTutorial cameraIntroTutorial;
    [SerializeField] private EnvironmentTileTutorial environmentTileTutorial;

    public Button regenerateMapButton;

    [Header("Map Regeneration")]
    [SerializeField] private MapGenerator mapGenerator;
    [SerializeField] private MapTilePlacer mapTilePlacer;

    private bool _isRegeneratingMap = false;

    private class StarterCandidate
    {
        public TileScript tile;
        public Building building;
    }

    private List<StarterCandidate> starterCandidates = new List<StarterCandidate>();
    private int currentIndex;
    private int previousIndex = -1;
    private GameObject previewInstance;

    private Building currentStarterDef;
    private bool _cameraLockedByPicker = false;
    private bool _initialized = false;
    private bool _subscribedToTileActivator = false;

    private void Awake()
    {
        if (cameraControl == null)
            cameraControl = FindObjectOfType<CameraControl>();

        if (tileActivator == null)
            tileActivator = FindObjectOfType<TileActivator>();

        if (mapGenerator == null)
            mapGenerator = FindObjectOfType<MapGenerator>();

        if (mapTilePlacer == null)
            mapTilePlacer = FindObjectOfType<MapTilePlacer>();

        if (panel != null)
            panel.SetActive(false);
    }

    private void OnEnable()
    {
        SubscribeToTileActivator();
    }

    private void OnDisable()
    {
        UnsubscribeFromTileActivator();
        UnlockCameraInput();
        ClearButtonListeners();
    }

    private void OnDestroy()
    {
        UnsubscribeFromTileActivator();
    }

    public void InstallRuntimeRefs(
        GameObject newPanel = null,
        Button newPrevButton = null,
        Button newNextButton = null,
        Button newConfirmButton = null,
        CameraControl newCameraControl = null,
        TileActivator newTileActivator = null,
        CameraIntroTutorial newCameraIntroTutorial = null,
        EnvironmentTileTutorial newEnvironmentTileTutorial = null,
        Button newRegenerateMapButton = null,
        MapGenerator newMapGenerator = null,
        MapTilePlacer newMapTilePlacer = null)
    {
        if (newPanel != null)
            panel = newPanel;

        if (newPrevButton != null)
            prevButton = newPrevButton;

        if (newNextButton != null)
            nextButton = newNextButton;

        if (newConfirmButton != null)
            confirmButton = newConfirmButton;

        if (newRegenerateMapButton != null)
            regenerateMapButton = newRegenerateMapButton;

        if (newCameraControl != null)
            cameraControl = newCameraControl;

        if (newCameraIntroTutorial != null)
            cameraIntroTutorial = newCameraIntroTutorial;

        if (newEnvironmentTileTutorial != null)
            environmentTileTutorial = newEnvironmentTileTutorial;

        if (newMapGenerator != null)
            mapGenerator = newMapGenerator;

        if (newMapTilePlacer != null)
            mapTilePlacer = newMapTilePlacer;

        if (newTileActivator != null && tileActivator != newTileActivator)
        {
            UnsubscribeFromTileActivator();
            tileActivator = newTileActivator;
            SubscribeToTileActivator();
        }

        BindButtonListeners();

        if (panel != null && !_initialized)
            panel.SetActive(false);
    }

    private void BindButtonListeners()
    {
        ClearButtonListeners();

        if (prevButton != null)
            prevButton.onClick.AddListener(OnPrev);

        if (nextButton != null)
            nextButton.onClick.AddListener(OnNext);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (regenerateMapButton != null)
            regenerateMapButton.onClick.AddListener(OnRegenerateMapClicked);
    }

    private void SubscribeToTileActivator()
    {
        if (_subscribedToTileActivator || tileActivator == null)
            return;

        tileActivator.OnTilesActivated += HandleTilesActivated;
        _subscribedToTileActivator = true;
    }

    private void UnsubscribeFromTileActivator()
    {
        if (!_subscribedToTileActivator || tileActivator == null)
            return;

        tileActivator.OnTilesActivated -= HandleTilesActivated;
        _subscribedToTileActivator = false;
    }

    private void HandleTilesActivated()
    {
        if (ShouldBlockForLoadedGame())
        {
            if (panel != null)
                panel.SetActive(false);

            return;
        }

        BeginSelection();
    }

    public void BeginSelection()
    {
        if (ShouldBlockForLoadedGame())
            return;

        if (_initialized)
            return;

        if (cameraControl == null)
        {
            //Debug.LogError("[StartingPointPicker] No CameraControl assigned.");
            return;
        }

        if (panel == null || prevButton == null || nextButton == null || confirmButton == null)
        {
            //Debug.LogError("[StartingPointPicker] Panel/buttons are not fully assigned.");
            return;
        }

        if (BuildingManager.Instance == null)
        {
            //Debug.LogError("[StartingPointPicker] BuildingManager instance missing.");
            return;
        }

        List<Building> starterBuildings = BuildingManager.Instance
            .GetBuildingsForStage(0)
            .Where(b =>
                b != null &&
                b.isStarterCandidate &&
                (b.buildingPrefab != null || b.finalBuildingPrefab != null))
            .ToList();

        if (starterBuildings.Count == 0)
        {
            //Debug.LogWarning("[StartingPointPicker] No starter candidate buildings found in stage 0.");
            return;
        }

        starterCandidates.Clear();

        TileScript[] allTiles = FindObjectsOfType<TileScript>();
        foreach (TileScript ts in allTiles)
        {
            if (ts == null)
                continue;

            if (ts.GetSpawnedInstance() == null)
                continue;

            foreach (Building building in starterBuildings)
            {
                if (MatchesStarterRequirements(building, ts))
                {
                    starterCandidates.Add(new StarterCandidate
                    {
                        tile = ts,
                        building = building
                    });
                }
            }
        }

        if (starterCandidates.Count == 0)
        {
            //Debug.LogWarning("[StartingPointPicker] No valid starter candidates matched any tiles.");
            return;
        }

        _initialized = true;

        TileInteraction.SetSelectionEnabled(false);
        TileInteraction.GetInstance()?.DeselectCurrent();

        LockCameraInput();

        panel.SetActive(true);

        BindButtonListeners();

        currentIndex = 0;
        previousIndex = -1;
        ShowPreviewFor(currentIndex);
    }

    private bool MatchesStarterRequirements(Building building, TileScript tile)
    {
        if (building == null || tile == null)
            return false;

        EnvironmentType envType = tile.GetChosenEnvironmentType();
        EnvironmentTileType tileType = tile.GetChosenTileType();

        bool environmentOk =
            building.requiredEnvironmentTypes == null ||
            building.requiredEnvironmentTypes.Count == 0 ||
            building.requiredEnvironmentTypes.Contains(envType);

        bool tileTypeOk =
            building.requiredEnvironmentTileTypes == null ||
            building.requiredEnvironmentTileTypes.Count == 0 ||
            building.requiredEnvironmentTileTypes.Contains(tileType);

        return environmentOk && tileTypeOk;
    }

    private void LockCameraInput()
    {
        if (cameraControl == null || _cameraLockedByPicker)
            return;

        // Allow zoom but block drag while picking a starting point.
        cameraControl.SetTutorialInputRestrictions(
            restrictInput:          true,
            allowWorldDrag:         false,
            allowZoom:              true,
            allowMinimapRotation:   true);
        _cameraLockedByPicker = true;
    }

    private void UnlockCameraInput()
    {
        if (cameraControl == null || !_cameraLockedByPicker)
            return;

        cameraControl.ClearTutorialInputRestrictions();
        cameraControl.ClearOrbitTarget();
        _cameraLockedByPicker = false;
    }

    private void ClearButtonListeners()
    {
        if (prevButton != null) prevButton.onClick.RemoveAllListeners();
        if (nextButton != null) nextButton.onClick.RemoveAllListeners();
        if (confirmButton != null) confirmButton.onClick.RemoveAllListeners();
        if (regenerateMapButton != null) regenerateMapButton.onClick.RemoveAllListeners();
    }

    private void OnPrev()
    {
        if (!_initialized || starterCandidates == null || starterCandidates.Count == 0)
            return;

        int next = (currentIndex - 1 + starterCandidates.Count) % starterCandidates.Count;
        ShowPreviewFor(next);
    }

    private void OnNext()
    {
        if (!_initialized || starterCandidates == null || starterCandidates.Count == 0)
            return;

        int next = (currentIndex + 1) % starterCandidates.Count;
        ShowPreviewFor(next);
    }

    private void ShowPreviewFor(int newIndex)
    {
        if (previewInstance != null && previousIndex >= 0 && previousIndex < starterCandidates.Count)
        {
            Destroy(previewInstance);

            TileScript previousTile = starterCandidates[previousIndex].tile;
            if (previousTile != null)
                previousTile.ActivateSpawnedInstance();
        }

        previousIndex = currentIndex = newIndex;

        StarterCandidate candidate = starterCandidates[currentIndex];
        TileScript ts = candidate.tile;
        Building starterDef = candidate.building;

        if (ts == null || starterDef == null)
        {
            //Debug.LogError("[StartingPointPicker] Candidate tile or building missing.");
            return;
        }

        GameObject envGO = ts.GetSpawnedInstance();
        if (envGO == null)
        {
            //Debug.LogError("[StartingPointPicker] Spawned tile object missing.");
            return;
        }

        cameraControl.FocusOnPoint(envGO.transform.position, envGO.transform.right, 5f);
        cameraControl.SetOrbitTarget(envGO.transform.position);

        ts.DeactivateSpawnedInstance();

        currentStarterDef = starterDef;

        GameObject prefab = starterDef.finalBuildingPrefab != null
            ? starterDef.finalBuildingPrefab
            : starterDef.buildingPrefab;

        if (prefab == null)
        {
            //Debug.LogError($"[StartingPointPicker] Starter prefab missing for '{starterDef.buildingName}'.");
            return;
        }

        previewInstance = Instantiate(prefab, envGO.transform.position, envGO.transform.rotation);

        WireBuildingPreview(previewInstance, starterDef);
    }

    private void OnConfirm()
    {
        if (!_initialized || starterCandidates == null || starterCandidates.Count == 0)
            return;

        StarterCandidate candidate = starterCandidates[currentIndex];
        TileScript ts = candidate.tile;

        if (ts != null)
            Destroy(ts.gameObject);

        if (previewInstance != null)
        {
            if (currentStarterDef != null)
                WireBuildingPreview(previewInstance, currentStarterDef);

            BuildingInstance tag = previewInstance.GetComponent<BuildingInstance>();
            if (!tag) tag = previewInstance.AddComponent<BuildingInstance>();
            tag.definition = currentStarterDef;
            tag.isStarter = true;

            BuildingControl bc = previewInstance.GetComponent<BuildingControl>();
            if (bc != null && !bc.enabled)
                bc.enabled = true;

            PlayerBuildingManager.Instance?.Register(tag);
        }

        Vector3 starterWorldPoint = previewInstance != null
            ? previewInstance.transform.position
            : (ts != null ? ts.transform.position : Vector3.zero);

        if (cameraIntroTutorial != null)
        {
            cameraIntroTutorial.SetStarterWorldPoint(starterWorldPoint);
            cameraIntroTutorial.SetStarterTarget(previewInstance);
        }

        if (environmentTileTutorial != null)
        {
            environmentTileTutorial.SetStarterTarget(previewInstance);
            environmentTileTutorial.SetStarterWorldPoint(starterWorldPoint);
        }

        panel.SetActive(false);
        ClearButtonListeners();
        UnlockCameraInput();

        PlayersPopulationManager.Instance?.MarkUIDirty();

        if (cameraControl != null)
        {
            Transform camT = cameraControl.transform;
            cameraControl.transform.position = new Vector3(camT.position.x, 10f, camT.position.z);
        }

        StartCoroutine(SaveAfterStarterConfirmed());

        bool startedTutorial = false;

        if (cameraIntroTutorial != null && cameraIntroTutorial.ShouldRunTutorial())
        {
            cameraIntroTutorial.BeginTutorial();
            startedTutorial = true;
        }
        else if (environmentTileTutorial != null && environmentTileTutorial.ShouldRunTutorial())
        {
            environmentTileTutorial.BeginTutorial();
            startedTutorial = true;
        }

        if (!startedTutorial)
        {
            TileInteraction.SetSelectionEnabled(false);
            TileInteraction.GetInstance()?.EnableSelectionAfter(0.01f);
            TurnSystem.Instance?.ResumeTurnTimer();
        }
    }

    private IEnumerator SaveAfterStarterConfirmed()
    {
        yield return null; // let Destroy(ts.gameObject) finish this frame

        if (SaveSystem.Instance == null)
        {
            //Debug.LogWarning("[StartingPointPicker] SaveSystem instance missing; could not autosave starter choice.");
            yield break;
        }

        SaveSystem.RequestSave();
        //Debug.Log("[StartingPointPicker] Autosaved after starter point confirmed.");
    }

    private void WireBuildingPreview(GameObject go, Building def)
    {
        if (!go || def == null)
            return;

        BuildingInstance tag = go.GetComponent<BuildingInstance>();
        if (!tag) tag = go.AddComponent<BuildingInstance>();
        tag.definition = def;
        if (string.IsNullOrEmpty(tag.instanceId))
            tag.instanceId = Guid.NewGuid().ToString();
        tag.isStarter = false;

        BuildingControl bc = go.GetComponent<BuildingControl>();
        if (!bc) bc = go.AddComponent<BuildingControl>();
        bc.buildingID = def.buildingID;
        bc.buildingName = def.buildingName;
        bc.buildingType = def.buildingType;
        bc.enabled = false;

        BuildingHealth bh = go.GetComponent<BuildingHealth>();
        if (bh)
            bh.RefreshDefaultsFromManager(def.buildingID);
    }

    private bool ShouldBlockForLoadedGame()
    {
        return SaveSystem.Instance != null && SaveSystem.Instance.IsLoading;
    }

    private void OnRegenerateMapClicked()
    {
        if (_isRegeneratingMap)
            return;

        StartCoroutine(RegenerateMapFromPickerCoroutine());
    }

    private IEnumerator RegenerateMapFromPickerCoroutine()
    {
        _isRegeneratingMap = true;

        ResetPickerStateForMapRegeneration();

        TileInteraction.SetSelectionEnabled(false);
        TileInteraction.GetInstance()?.DeselectCurrent();

        if (panel != null)
            panel.SetActive(false);

        if (tileActivator != null && tileActivator.loadingScreen != null)
            tileActivator.loadingScreen.SetActive(true);

        if (tileActivator != null && tileActivator.timerUI != null)
            tileActivator.timerUI.gameObject.SetActive(true);

        if (mapGenerator == null)
            mapGenerator = FindObjectOfType<MapGenerator>();

        if (mapTilePlacer == null)
            mapTilePlacer = FindObjectOfType<MapTilePlacer>();

        if (tileActivator == null)
            tileActivator = FindObjectOfType<TileActivator>();

        if (mapGenerator == null || mapTilePlacer == null || tileActivator == null)
        {
            Debug.LogWarning("[StartingPointPicker] Cannot regenerate map. Missing MapGenerator, MapTilePlacer, or TileActivator.");
            _isRegeneratingMap = false;
            yield break;
        }

        // Clear old preview and old generated map objects.
        mapTilePlacer.ClearPlacedTilesAndState();

        // Let Destroy() process before building the new map.
        yield return null;

        MapTilePlacer.ResetWorldReady();

        mapGenerator.enabled = true;
        mapTilePlacer.enabled = true;

        yield return StartCoroutine(mapGenerator.RegenerateCoroutine());

        mapTilePlacer.BeginPlacement();

        yield return new WaitUntil(() => MapTilePlacer.WorldReady);

        // This will show/update the loading screen, activate all TileScripts,
        // then fire OnTilesActivated, which makes this picker BeginSelection() again.
        tileActivator.BeginActivation(tileActivator.timerUI, true, true);

        yield return new WaitUntil(() => !tileActivator.IsRunning);

        _isRegeneratingMap = false;
    }

    private void ResetPickerStateForMapRegeneration()
    {
        ClearButtonListeners();

        if (previewInstance != null)
        {
            Destroy(previewInstance);
            previewInstance = null;
        }

        starterCandidates.Clear();

        currentIndex = 0;
        previousIndex = -1;
        currentStarterDef = null;

        _initialized = false;
    }
}
