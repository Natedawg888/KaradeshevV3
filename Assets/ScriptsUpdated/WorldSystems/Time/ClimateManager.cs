using System;                    // ⬅️ NEW
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClimateManager : MonoBehaviour
{
    public static ClimateManager Instance { get; private set; }

    public event Action OnClimateRebuilt;

    [Header("Links")]
    public GridManager gridManager;
    public TileActivator tileActivator;

    [Header("Perlin Noise (patchiness)")]
    public float temperatureNoiseAmplitude = 5f;
    public float temperatureNoiseScale = 0.08f;
    public float humidityNoiseAmplitude = 0.25f;
    public float humidityNoiseScale = 0.08f;

    [Header("Transition Settings (per-frame only)")]
    [Tooltip("How many tiles are allowed to change biome per frame.")]
    public int tilesUpdatedPerFrame = 25;

    [Header("Rebuild Batching")]
    [Tooltip("How many cells of the climate grid are recomputed per frame when rebuilding climate fields.")]
    public int cellsRecomputedPerFrame = 512;

    [Header("Water Evaporation Humidity")]
    public bool waterEvaporationHumidityEnabled = true;

    [Tooltip("Water tiles only add humidity when their temperature is at or above this.")]
    public float waterEvaporationMinTemperatureC = 24f;

    [Tooltip("At this temperature or above, the full per-turn humidity amount is applied.")]
    public float waterEvaporationFullTemperatureC = 42f;

    [Tooltip("Humidity added per turn at full evaporation temperature.")]
    [Range(0f, 0.1f)]
    public float waterEvaporationHumidityPerTurnAtFullTemp = 0.015f;

    [Tooltip("Maximum extra humidity a cell can gain from local water evaporation.")]
    [Range(0f, 1f)]
    public float waterEvaporationMaxLocalBoost = 0.35f;

    public bool waterEvaporationDebugLogging = false;

    [Header("Frame pacing")]
    [Tooltip("Extra delay between tile batches during rebuild / job application (0 = next frame only).")]
    public float frameDelaySeconds = 0f;

    [Tooltip("If true, logs debug info when seasons change and tiles update.")]
    public bool debugLogging = false;

    public bool HasValidInitialClimate { get; private set; }
    public bool IsClimateReady() => HasValidInitialClimate && temperature != null && humidity != null && cols > 0 && rows > 0;

    // Internal climate state
    private float globalTemperatureOffset = 0f;
    private float globalHumidityOffset = 0f;

    // Dynamic fields used by the game (for current season)
    private float[,] temperature;              // °C
    private float[,] humidity;                 // 0..1
    private bool[,] temperatureValid;
    private bool[,] humidityValid;
    private EnvironmentType[,] currentEnvironment;

    // Static spatial patterns (lat + perlin + jitter, no global/season offsets)
    private float[,] baseTemperatureField;
    private float[,] baseHumidityField;
    private bool baseClimateInitialized = false;

    private float[,] waterHumidityBoost;

    private TileScript[,] tileGrid;
    private int cols;
    private int rows;

    private int _planetaryForcingTurnCounter = 0;
    private int _lastPlanetaryForcedRebuildTurn = -999999;

    private struct ClimateCell
    {
        public int x;
        public int y;
        public TileScript tile;
    }

    private static readonly HashSet<EnvironmentTileType> kSeasonalBiomeTileTypes = new HashSet<EnvironmentTileType>
    {
        EnvironmentTileType.Land,

        EnvironmentTileType.Coastline,
        EnvironmentTileType.CoastlineCorner,

        EnvironmentTileType.LakeEdge,
        EnvironmentTileType.LakeCorner,

        EnvironmentTileType.River,
        EnvironmentTileType.RiverCorner,
        EnvironmentTileType.RiverSplit,
        EnvironmentTileType.RiverCross,
        EnvironmentTileType.RiverEnd,

        EnvironmentTileType.RiverMouth,
        EnvironmentTileType.LakeMouth,
    };

    private readonly List<ClimateCell> _climateCells = new List<ClimateCell>();
    private readonly List<ClimateJob> _pendingJobs = new List<ClimateJob>();
    private readonly List<EnvironmentType> _neighbourEnvs = new List<EnvironmentType>(8);
    private readonly List<TileCoord> _footprintBuffer = new List<TileCoord>(8);

    private Coroutine climateCoroutine;
    private Coroutine _rebuildCoroutine;
    private int _pendingJobIndex = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (gridManager == null)
            gridManager = GridManager.Instance;

        if (tileActivator == null)
            tileActivator = FindObjectOfType<TileActivator>();
    }

    private void OnEnable()
    {
        if (gridManager == null)
            gridManager = GridManager.Instance;

        if (tileActivator == null)
            tileActivator = FindObjectOfType<TileActivator>();

        if (SeasonManager.Instance != null)
            SeasonManager.Instance.OnSeasonChanged += HandleSeasonChanged;

        if (tileActivator != null)
            tileActivator.OnTilesActivated += HandleTilesActivated;

        TurnSystem.SubscribeToEndOfTurn(HandlePlanetaryForcingTurnEnd);
    }

    private void OnDisable()
    {
        if (SeasonManager.Instance != null)
            SeasonManager.Instance.OnSeasonChanged -= HandleSeasonChanged;

        if (tileActivator != null)
            tileActivator.OnTilesActivated -= HandleTilesActivated;

        TurnSystem.UnsubscribeFromEndOfTurn(HandlePlanetaryForcingTurnEnd);
    }

    private void HandlePlanetaryForcingTurnEnd()
    {
        _planetaryForcingTurnCounter++;

        bool humidityChangedThisTurn = ApplyWaterEvaporationHumidityTurnEnd();

        bool didForceRebuild = false;

        PlanetaryForcingSettings forcing = GetPlanetaryForcingSettings();
        if (forcing != null && forcing.enabled)
        {
            int interval = Mathf.Max(0, forcing.rebuildIntervalTurns);

            if (interval > 0 &&
                tileGrid != null &&
                (_planetaryForcingTurnCounter - _lastPlanetaryForcedRebuildTurn) >= interval)
            {
                SeasonDefinition currentSeason = SeasonManager.Instance != null
                    ? SeasonManager.Instance.CurrentSeason
                    : null;

                RebuildClimateForSeason(currentSeason);
                _lastPlanetaryForcedRebuildTurn = _planetaryForcingTurnCounter;
                didForceRebuild = true;
            }
        }

        // If a full rebuild is already happening, that rebuild will invoke OnClimateRebuilt.
        if (humidityChangedThisTurn && !didForceRebuild)
        {
            OnClimateRebuilt?.Invoke();
            MarkCoreSystemsDirty();
        }
    }

    private void HandleTilesActivated()
    {
        if (gridManager == null)
            gridManager = GridManager.Instance;

        if (gridManager == null)
        {
            Debug.LogWarning("[ClimateManager] No GridManager assigned; cannot build climate map.");
            return;
        }

        cols = gridManager.columns;
        rows = gridManager.rows;

        temperature = new float[cols, rows];
        humidity = new float[cols, rows];
        temperatureValid = new bool[cols, rows];
        humidityValid = new bool[cols, rows];
        currentEnvironment = new EnvironmentType[cols, rows];
        tileGrid = new TileScript[cols, rows];

        baseTemperatureField = new float[cols, rows];
        baseHumidityField = new float[cols, rows];
        baseClimateInitialized = false;
        HasValidInitialClimate = false;

        waterHumidityBoost = new float[cols, rows];

        BuildTileLookup();

        SeasonDefinition startSeason = SeasonManager.Instance != null
            ? SeasonManager.Instance.CurrentSeason
            : null;

        RebuildClimateForSeason(startSeason);

        if (debugLogging)
            Debug.Log("[ClimateManager] Climate initialized after tiles activated.");
    }

    private void BuildTileLookup()
    {
        var allTiles = FindObjectsOfType<TileScript>(true);

        _climateCells.Clear();
        Array.Clear(tileGrid, 0, tileGrid.Length);

        for (int i = 0; i < allTiles.Length; i++)
        {
            TileScript tile = allTiles[i];
            if (tile == null)
                continue;

            int x, y;
            if (!gridManager.TryGetCell(tile.transform.position, out x, out y))
                continue;

            if (x < 0 || x >= cols || y < 0 || y >= rows)
                continue;

            tileGrid[x, y] = tile;
            _climateCells.Add(new ClimateCell { x = x, y = y, tile = tile });

            if (tile.HasSpawned)
                currentEnvironment[x, y] = tile.GetChosenEnvironmentType();
        }

        if (debugLogging)
            Debug.Log($"[ClimateManager] Built climate cell lookup. Primary cells={_climateCells.Count}");
    }

    private void HandleSeasonChanged(SeasonDefinition newSeason)
    {
        globalTemperatureOffset += GetSeasonTemperatureDriftOnEnter(newSeason);
        globalHumidityOffset += GetSeasonHumidityDriftOnEnter(newSeason);

        if (tileGrid == null)
            return;

        RebuildClimateForSeason(newSeason);
        _lastPlanetaryForcedRebuildTurn = _planetaryForcingTurnCounter;
    }

    private void RebuildClimateForSeason(SeasonDefinition season)
    {
        if (gridManager == null || tileGrid == null)
            return;

        if (_rebuildCoroutine != null)
        {
            StopCoroutine(_rebuildCoroutine);
            _rebuildCoroutine = null;
        }

        _rebuildCoroutine = StartCoroutine(RebuildClimateForSeasonCoroutine(season));
    }

    private IEnumerator RebuildClimateForSeasonCoroutine(SeasonDefinition season)
    {
        var presetMgr = EnvironmentPresetManager.Instance;
        if (presetMgr == null)
        {
            Debug.LogWarning("[ClimateManager] No EnvironmentPresetManager; cannot compute climate correctly.");
            yield break;
        }

        var currentPreset = presetMgr.GetCurrentPreset();
        if (currentPreset == null)
        {
            Debug.LogWarning("[ClimateManager] No current EnvironmentPreset set; cannot compute climate correctly.");
            yield break;
        }

        if (_climateCells.Count == 0)
        {
            Debug.LogWarning("[ClimateManager] No climate cells to process.");
            yield break;
        }

        PlanetarySectionSettings section = currentPreset.planetarySection;
        ClimateShiftSettings shift = season != null ? season.climateShift : null;

        PlanetaryForcingSample forcing = EvaluatePlanetaryForcingSample();

        float baseSeasonTempOffset = GetSeasonTemperatureOffset(season);
        float baseSeasonHumOffset = GetSeasonHumidityOffset(season);

        float tempStrength = section != null ? section.seasonalTemperatureStrength : 1f;
        float humStrength = section != null ? section.seasonalHumidityStrength : 1f;

        tempStrength *= forcing.seasonalTemperatureStrengthMultiplier;
        humStrength *= forcing.seasonalHumidityStrengthMultiplier;

        if (shift != null)
        {
            tempStrength *= Mathf.Max(0f, shift.seasonalTemperatureStrengthMultiplier);
            humStrength *= Mathf.Max(0f, shift.seasonalHumidityStrengthMultiplier);
        }

        float seasonTempOffset = baseSeasonTempOffset * tempStrength;
        float seasonHumOffset = baseSeasonHumOffset * humStrength;

        if (climateCoroutine != null)
        {
            StopCoroutine(climateCoroutine);
            climateCoroutine = null;
        }

        _pendingJobs.Clear();
        _pendingJobIndex = 0;

        Array.Clear(temperatureValid, 0, temperatureValid.Length);
        Array.Clear(humidityValid, 0, humidityValid.Length);

        int cellsPerFrame = Mathf.Max(1, cellsRecomputedPerFrame);
        int processedCells = 0;

        // IMPORTANT:
        // Recompute the base climate every rebuild so the map can actually swing
        // between hotter/wetter/colder/drier world states.
        bool recomputeBaseField = true;

        for (int i = 0; i < _climateCells.Count; i++)
        {
            ClimateCell cell = _climateCells[i];
            TileScript tile = cell.tile;
            int x = cell.x;
            int y = cell.y;

            if (tile == null || !tile.HasSpawned)
                continue;

            EnvironmentTileType tileType = tile.GetChosenTileType();
            EnvironmentType currentEnv = tile.GetChosenEnvironmentType();

            if (recomputeBaseField)
            {
                float latBaseTemp;
                float latBaseHum;
                ComputeBaseClimateForCell(x, y, section, shift, forcing, out latBaseTemp, out latBaseHum);

                float tNoise = (Mathf.PerlinNoise(x * temperatureNoiseScale, y * temperatureNoiseScale) - 0.5f) * 2f;
                float hNoise = (Mathf.PerlinNoise(x * humidityNoiseScale, y * humidityNoiseScale) - 0.5f) * 2f;

                float tempBase = latBaseTemp
                               + tNoise * temperatureNoiseAmplitude
                               + SampleTemperatureJitter(x, y, section);

                float humBase = latBaseHum
                              + hNoise * humidityNoiseAmplitude
                              + SampleHumidityJitter(x, y, section);

                baseTemperatureField[x, y] = tempBase;
                baseHumidityField[x, y] = Mathf.Clamp01(humBase);
            }

            float temp = baseTemperatureField[x, y]
           + globalTemperatureOffset
           + seasonTempOffset
           + forcing.meanTemperatureOffset;

            float hum = Mathf.Clamp01(baseHumidityField[x, y]
                 + globalHumidityOffset
                 + seasonHumOffset
                 + forcing.meanHumidityOffset);

            hum = ApplyLocalWaterHumidityBoost(x, y, hum);

            ApplyClimateToFootprint(x, y, temp, hum, currentEnv);

            if (!kSeasonalBiomeTileTypes.Contains(tileType))
            {
                processedCells++;
                if (processedCells >= cellsPerFrame)
                {
                    processedCells = 0;
                    if (frameDelaySeconds > 0f) yield return new WaitForSeconds(frameDelaySeconds);
                    else yield return null;
                }
                continue;
            }

            EnvironmentType targetEnv = PickBestEnvironmentForTile(tile, tileType, temp, hum, currentEnv);

            bool isBusy = false;
            EnvironmentControl envCtrl = null;

            GameObject spawnedInstance = tile.GetSpawnedInstance();
            if (spawnedInstance != null)
            {
                envCtrl = spawnedInstance.GetComponentInChildren<EnvironmentControl>(true);
                if (envCtrl != null &&
                    (envCtrl.isBeingDiscovered || envCtrl.isGathering || envCtrl.isSurveying))
                {
                    isBusy = true;
                }
            }

            if (!isBusy && IsSelectedWithLoot(envCtrl))
                isBusy = true;

            if (!isBusy && targetEnv != currentEnv)
            {
                if (tile.HasEnvironmentVariant(tileType, targetEnv))
                {
                    _pendingJobs.Add(new ClimateJob
                    {
                        x = x,
                        y = y,
                        tile = tile,
                        newEnvironment = targetEnv
                    });
                }
                else if (debugLogging)
                {
                    Debug.Log($"[ClimateManager] Skip switch at ({x},{y}) - no {targetEnv} variant for tileType {tileType}.");
                }
            }

            processedCells++;
            if (processedCells >= cellsPerFrame)
            {
                processedCells = 0;
                if (frameDelaySeconds > 0f) yield return new WaitForSeconds(frameDelaySeconds);
                else yield return null;
            }
        }

        baseClimateInitialized = true;
        HasValidInitialClimate = true;

        if (_pendingJobs.Count > 0)
            climateCoroutine = StartCoroutine(ApplyClimateJobsCoroutine());

        OnClimateRebuilt?.Invoke();

        MarkCoreSystemsDirty();

        if (debugLogging)
        {
            string seasonName = season != null ? season.displayName : "None";
            Debug.Log($"[ClimateManager] Season {seasonName}: recomputed climate, scheduled {_pendingJobs.Count} biome changes.");
        }

        _rebuildCoroutine = null;
    }

    private void ApplyClimateToFootprint(int primaryX, int primaryY, float temp, float hum, EnvironmentType envType)
    {
        bool appliedAny = false;

        MonoEnvironmentDataSource envSource = MonoEnvironmentDataSource.Instance;
        if (envSource != null)
        {
            _footprintBuffer.Clear();

            if (envSource.TryGetFootprintCoords(new TileCoord(primaryX, primaryY), _footprintBuffer))
            {
                for (int i = 0; i < _footprintBuffer.Count; i++)
                {
                    TileCoord coord = _footprintBuffer[i];
                    ApplyClimateToCell(coord.x, coord.y, temp, hum, envType);
                    appliedAny = true;
                }
            }
        }

        if (!appliedAny)
            ApplyClimateToCell(primaryX, primaryY, temp, hum, envType);
    }

    private void ApplyClimateToCell(int x, int y, float temp, float hum, EnvironmentType envType)
    {
        if (x < 0 || x >= cols || y < 0 || y >= rows)
            return;

        temperature[x, y] = temp;
        humidity[x, y] = hum;
        temperatureValid[x, y] = true;
        humidityValid[x, y] = true;
        currentEnvironment[x, y] = envType;
    }

    private void SetEnvironmentForFootprint(int primaryX, int primaryY, EnvironmentType envType)
    {
        bool appliedAny = false;

        MonoEnvironmentDataSource envSource = MonoEnvironmentDataSource.Instance;
        if (envSource != null)
        {
            _footprintBuffer.Clear();

            if (envSource.TryGetFootprintCoords(new TileCoord(primaryX, primaryY), _footprintBuffer))
            {
                for (int i = 0; i < _footprintBuffer.Count; i++)
                {
                    TileCoord coord = _footprintBuffer[i];
                    if (coord.x < 0 || coord.x >= cols || coord.y < 0 || coord.y >= rows)
                        continue;

                    currentEnvironment[coord.x, coord.y] = envType;
                    appliedAny = true;
                }
            }
        }

        if (!appliedAny && primaryX >= 0 && primaryX < cols && primaryY >= 0 && primaryY < rows)
            currentEnvironment[primaryX, primaryY] = envType;
    }

    private void MarkCoreSystemsDirty()
    {
        SaveSystem.MarkSectionDirty(SaveSectionKeys.CoreSystems);
    }

    private bool IsSelectedWithLoot(EnvironmentControl envCtrl)
    {
        if (envCtrl == null || !envCtrl.HasLootReady)
            return false;

        var selectedTile = TileInteraction.SelectedTile;
        if (selectedTile == null)
            return false;

        var selectedEnv = selectedTile.EnvironmentControl;
        if (selectedEnv == null)
            return false;

        return ReferenceEquals(selectedEnv, envCtrl);
    }

    private void ComputeBaseClimateForCell(
    int x, int y,
    PlanetarySectionSettings section,
    ClimateShiftSettings shift,
    PlanetaryForcingSample forcing,
    out float baseTemp,
    out float baseHum)
    {
        float rowT = (rows > 1) ? (float)y / (rows - 1) : 0.5f;
        float latDeg = Mathf.Lerp(section.minLatitudeDeg, section.maxLatitudeDeg, rowT);
        float absLat01 = Mathf.Clamp01(Mathf.Abs(latDeg) / 90f);

        float equatorTemp = section.equatorTemperature
                          + (shift != null ? shift.equatorTemperatureOffset : 0f)
                          + forcing.equatorTemperatureOffset;

        float poleTemp = section.poleTemperature
                       + (shift != null ? shift.poleTemperatureOffset : 0f)
                       + forcing.poleTemperatureOffset;

        float equatorHum = section.equatorHumidity
                         + (shift != null ? shift.equatorHumidityOffset : 0f)
                         + forcing.equatorHumidityOffset;

        float poleHum = section.poleHumidity
                      + (shift != null ? shift.poleHumidityOffset : 0f)
                      + forcing.poleHumidityOffset;

        baseTemp = Mathf.Lerp(equatorTemp, poleTemp, absLat01);
        baseHum = Mathf.Lerp(equatorHum, poleHum, absLat01);

        float hemisphereSigned = Mathf.Lerp(-1f, 1f, rowT);
        baseTemp += hemisphereSigned * forcing.northSouthTemperatureBias;
        baseHum = Mathf.Clamp01(baseHum + hemisphereSigned * forcing.northSouthHumidityBias);
    }

    private struct ClimateJob
    {
        public int x;
        public int y;
        public TileScript tile;
        public EnvironmentType newEnvironment;
    }

    private IEnumerator ApplyClimateJobsCoroutine()
    {
        int processedThisFrame = 0;

        while (_pendingJobIndex < _pendingJobs.Count)
        {
            ClimateJob job = _pendingJobs[_pendingJobIndex++];
            TileScript tile = job.tile;

            if (tile != null && tile.HasSpawned)
            {
                GameObject instance = tile.GetSpawnedInstance();
                EnvironmentControl envCtrl = null;
                bool isBusy = false;
                bool wasDiscovered = false;

                if (instance != null)
                {
                    envCtrl = instance.GetComponentInChildren<EnvironmentControl>(true);
                    if (envCtrl != null)
                    {
                        if (envCtrl.isBeingDiscovered || envCtrl.isGathering || envCtrl.isSurveying)
                            isBusy = true;

                        if (!isBusy && IsSelectedWithLoot(envCtrl))
                            isBusy = true;

                        wasDiscovered = envCtrl.IsDiscovered;
                    }
                }

                if (!isBusy)
                {
                    EnvironmentTileType tileType = tile.GetChosenTileType();

                    GetNeighbourBiomeTypes(job.x, job.y, _neighbourEnvs);

                    bool ok = tile.ForceSpawnSpecific(
                        job.newEnvironment,
                        tileType,
                        wasDiscovered,
                        _neighbourEnvs,
                        allowFallbackToAnyEnvVariant: false
                    );

                    if (ok)
                        SetEnvironmentForFootprint(job.x, job.y, job.newEnvironment);
                }
            }

            processedThisFrame++;
            if (processedThisFrame >= Mathf.Max(1, tilesUpdatedPerFrame))
            {
                processedThisFrame = 0;

                if (frameDelaySeconds > 0f) yield return new WaitForSeconds(frameDelaySeconds);
                else yield return null;
            }
        }

        if (debugLogging && _pendingJobs.Count > 0)
            Debug.Log($"[ClimateManager] Finished applying {_pendingJobs.Count} climate jobs.");

        _pendingJobs.Clear();
        _pendingJobIndex = 0;
        climateCoroutine = null;
    }

    private EnvironmentType ChooseBiome(float temp, float hum, EnvironmentType currentEnv)
    {
        hum = Mathf.Clamp01(hum);

        if (IsBiomeStable(currentEnv, temp, hum))
            return currentEnv;

        // Very cold
        if (temp < -5f)
            return EnvironmentType.Tundra;

        // Cold
        if (temp < 3f)
            return hum >= 0.20f ? EnvironmentType.BorealForest : EnvironmentType.Tundra;

        // Cool / temperate
        if (temp < 14f)
        {
            if (hum >= 0.60f) return EnvironmentType.TemperateForest;
            if (hum >= 0.24f) return EnvironmentType.Grassland;
            return EnvironmentType.Grassland;
        }

        // Warm transition zone
        // Subtropical should only happen when it is clearly humid.
        if (temp < 22f)
        {
            if (hum >= 0.72f) return EnvironmentType.SubTropical;
            if (hum >= 0.30f) return EnvironmentType.Grassland;
            return EnvironmentType.Desert;
        }

        // Main savanna belt
        if (temp < 34f)
        {
            // Tropical only in very hot + very wet pockets
            if (temp >= 27f && hum >= 0.90f) return EnvironmentType.TropicalForest;

            // Subtropical only on the wetter fringe, not the default humid result
            if (temp < 30f && hum >= 0.76f) return EnvironmentType.SubTropical;

            // This should be your dominant belt
            if (hum >= 0.32f) return EnvironmentType.Savanna;

            // Drier edge before desert
            if (hum >= 0.18f) return EnvironmentType.Grassland;

            return EnvironmentType.Desert;
        }

        // Very hot
        if (hum >= 0.92f) return EnvironmentType.TropicalForest;
        if (hum >= 0.42f) return EnvironmentType.Savanna;
        if (hum >= 0.18f) return EnvironmentType.Grassland;
        return EnvironmentType.Desert;
    }

    private bool IsBiomeStable(EnvironmentType biome, float temp, float hum)
    {
        hum = Mathf.Clamp01(hum);

        switch (biome)
        {
            case EnvironmentType.Tundra:
                return temp < -2f;

            case EnvironmentType.BorealForest:
                return temp >= -7f && temp < 6f && hum >= 0.16f;

            case EnvironmentType.TemperateForest:
                return temp >= 5f && temp < 17f && hum >= 0.54f;

            case EnvironmentType.Grassland:
                return temp >= 4f && temp < 38f && hum >= 0.16f && hum <= 0.50f;

            case EnvironmentType.SubTropical:
                // Much narrower than before
                return temp >= 18f && temp < 30f && hum >= 0.68f && hum <= 0.84f;

            case EnvironmentType.TropicalForest:
                return temp >= 27f && hum >= 0.88f;

            case EnvironmentType.Savanna:
                // Let savanna survive more of the middle humidity band
                return temp >= 20f && temp < 39f && hum >= 0.26f && hum <= 0.76f;

            case EnvironmentType.Desert:
                return temp >= 14f && hum < 0.22f;

            default:
                return false;
        }
    }

    public bool TryComputeInstantClimateAtWorldPos(Vector3 worldPos, out float temp, out float hum)
    {
        temp = 0f;
        hum = 0f;

        if (gridManager == null)
            gridManager = GridManager.Instance;

        if (gridManager == null)
            return false;

        int x, y;
        if (!gridManager.TryGetCell(worldPos, out x, out y))
            return false;

        var presetMgr = EnvironmentPresetManager.Instance;
        if (presetMgr == null)
            return false;

        var currentPreset = presetMgr.GetCurrentPreset();
        if (currentPreset == null)
            return false;

        PlanetarySectionSettings section = currentPreset.planetarySection;
        SeasonDefinition season = SeasonManager.Instance != null ? SeasonManager.Instance.CurrentSeason : null;
        ClimateShiftSettings shift = season != null ? season.climateShift : null;

        int mapRows = gridManager.rows;
        float rowT = (mapRows > 1) ? (float)y / (mapRows - 1) : 0.5f;

        float latDeg = Mathf.Lerp(section.minLatitudeDeg, section.maxLatitudeDeg, rowT);
        float absLat01 = Mathf.Clamp01(Mathf.Abs(latDeg) / 90f);

        float equatorTemp = section.equatorTemperature + (shift != null ? shift.equatorTemperatureOffset : 0f);
        float poleTemp = section.poleTemperature + (shift != null ? shift.poleTemperatureOffset : 0f);

        float equatorHum = section.equatorHumidity + (shift != null ? shift.equatorHumidityOffset : 0f);
        float poleHum = section.poleHumidity + (shift != null ? shift.poleHumidityOffset : 0f);

        float baseTemp = Mathf.Lerp(equatorTemp, poleTemp, absLat01);
        float baseHum = Mathf.Lerp(equatorHum, poleHum, absLat01);

        float baseSeasonTempOffset = GetSeasonTemperatureOffset(season);
        float baseSeasonHumOffset = GetSeasonHumidityOffset(season);

        float tempStrength = section != null ? section.seasonalTemperatureStrength : 1f;
        float humStrength = section != null ? section.seasonalHumidityStrength : 1f;

        if (shift != null)
        {
            tempStrength *= Mathf.Max(0f, shift.seasonalTemperatureStrengthMultiplier);
            humStrength *= Mathf.Max(0f, shift.seasonalHumidityStrengthMultiplier);
        }

        float seasonTempOffset = baseSeasonTempOffset * tempStrength;
        float seasonHumOffset = baseSeasonHumOffset * humStrength;

        float t = baseTemp + globalTemperatureOffset + seasonTempOffset;
        float h = Mathf.Clamp01(baseHum + globalHumidityOffset + seasonHumOffset);

        float tNoise = (Mathf.PerlinNoise(x * temperatureNoiseScale, y * temperatureNoiseScale) - 0.5f) * 2f;
        float hNoise = (Mathf.PerlinNoise(x * humidityNoiseScale, y * humidityNoiseScale) - 0.5f) * 2f;

        t += tNoise * temperatureNoiseAmplitude + SampleTemperatureJitter(x, y, section);
        h = Mathf.Clamp01(h + hNoise * humidityNoiseAmplitude + SampleHumidityJitter(x, y, section));
        h = ApplyLocalWaterHumidityBoost(x, y, h);

        temp = t;
        hum = h;
        return true;
    }

    public EnvironmentType PickBiomeFromClimate(float temp, float hum, EnvironmentType currentEnvHint = EnvironmentType.Grassland)
    {
        return ChooseBiome(temp, hum, currentEnvHint);
    }

    public bool TryGetClimateAtWorldPos(Vector3 worldPos, out float temp, out float hum)
    {
        temp = 0f;
        hum = 0f;

        if (gridManager == null)
            gridManager = GridManager.Instance;

        if (gridManager == null || temperature == null || humidity == null)
            return false;

        int x, y;
        if (!gridManager.TryGetCell(worldPos, out x, out y))
            return false;

        if (!TryGetTemperatureAtCell(x, y, out temp))
            return false;

        if (!TryGetHumidityAtCell(x, y, out hum))
            return false;

        return true;
    }

    public bool TryGetTemperatureAtCell(int x, int y, out float temp)
    {
        temp = 0f;

        if (temperature == null || temperatureValid == null)
            return false;

        if (x < 0 || x >= cols || y < 0 || y >= rows)
            return false;

        if (!temperatureValid[x, y])
            return false;

        temp = temperature[x, y];
        return true;
    }

    public bool TryGetHumidityAtCell(int x, int y, out float hum)
    {
        hum = 0f;

        if (humidity == null || humidityValid == null)
            return false;

        if (x < 0 || x >= cols || y < 0 || y >= rows)
            return false;

        if (!humidityValid[x, y])
            return false;

        hum = humidity[x, y];
        return true;
    }

    public float GetTemperatureAtCell(int x, int y)
    {
        return TryGetTemperatureAtCell(x, y, out float temp) ? temp : 0f;
    }

    public float GetHumidityAtCell(int x, int y)
    {
        return TryGetHumidityAtCell(x, y, out float hum) ? hum : 0f;
    }

    private void GetNeighbourBiomeTypes(int x, int y, List<EnvironmentType> outList)
    {
        outList.Clear();

        AddIfValid(x - 1, y, outList);
        AddIfValid(x + 1, y, outList);
        AddIfValid(x, y - 1, outList);
        AddIfValid(x, y + 1, outList);
    }

    private void AddIfValid(int nx, int ny, List<EnvironmentType> outList)
    {
        if (nx < 0 || nx >= cols || ny < 0 || ny >= rows)
            return;

        if (!temperatureValid[nx, ny] && !humidityValid[nx, ny])
            return;

        TileScript nTile = tileGrid[nx, ny];
        if (nTile == null || !nTile.HasSpawned)
            return;

        outList.Add(currentEnvironment[nx, ny]);
    }

    private EnvironmentType PickBestEnvironmentForTile(
        TileScript tile,
        EnvironmentTileType tileType,
        float temp,
        float hum,
        EnvironmentType currentEnv)
    {
        if (TileEnvironmentIsSuitable(tile, tileType, currentEnv, temp, hum))
            return currentEnv;

        EnvironmentType desired = ChooseBiome(temp, hum, currentEnv);
        if (desired != currentEnv &&
            tile.HasEnvironmentVariant(tileType, desired) &&
            TileEnvironmentIsSuitable(tile, tileType, desired, temp, hum))
        {
            return desired;
        }

        HashSet<EnvironmentType> supported = GetSupportedEnvironments(tile, tileType);
        EnvironmentType bestEnv = currentEnv;
        float bestDist = float.PositiveInfinity;

        foreach (EnvironmentType env in supported)
        {
            float d = BestDistanceToEnvironment(tile, tileType, env, temp, hum);
            if (d < bestDist - 0.0001f)
            {
                bestDist = d;
                bestEnv = env;
            }
            else if (Mathf.Abs(d - bestDist) < 0.0001f && env == desired)
            {
                bestEnv = env;
            }
        }

        if (float.IsPositiveInfinity(bestDist))
            return currentEnv;

        return bestEnv;
    }

    private HashSet<EnvironmentType> GetSupportedEnvironments(TileScript tile, EnvironmentTileType tileType)
    {
        var set = new HashSet<EnvironmentType>();

        if (tile == null || tile.options == null)
            return set;

        for (int i = 0; i < tile.options.Length; i++)
        {
            var opt = tile.options[i];
            if (opt == null || opt.tileType != tileType || opt.variants == null)
                continue;

            for (int v = 0; v < opt.variants.Length; v++)
            {
                var varnt = opt.variants[v];
                if (varnt == null || varnt.prefab == null)
                    continue;

                set.Add(varnt.environmentType);
            }
        }

        return set;
    }

    private bool TileEnvironmentIsSuitable(TileScript tile, EnvironmentTileType tileType, EnvironmentType env, float temp, float hum)
    {
        if (tile == null || tile.options == null)
            return true;

        bool foundAnyVariant = false;
        bool foundAnyRange = false;

        for (int i = 0; i < tile.options.Length; i++)
        {
            var opt = tile.options[i];
            if (opt == null || opt.tileType != tileType || opt.variants == null)
                continue;

            for (int v = 0; v < opt.variants.Length; v++)
            {
                var varnt = opt.variants[v];
                if (varnt == null || varnt.prefab == null || varnt.environmentType != env)
                    continue;

                foundAnyVariant = true;

                var f = varnt.neighborEnvFilter;
                if (f != null && (f.useTemperatureRange || f.useHumidityRange))
                {
                    foundAnyRange = true;
                    if (VariantPassesClimateRange(varnt, temp, hum))
                        return true;
                }
                else
                {
                    return true;
                }
            }
        }

        if (foundAnyRange)
            return false;

        if (!foundAnyVariant)
            return false;

        return ChooseBiome(temp, hum, env) == env;
    }

    private float BestDistanceToEnvironment(TileScript tile, EnvironmentTileType tileType, EnvironmentType env, float temp, float hum)
    {
        if (tile == null || tile.options == null)
            return float.PositiveInfinity;

        float best = float.PositiveInfinity;
        bool found = false;

        for (int i = 0; i < tile.options.Length; i++)
        {
            var opt = tile.options[i];
            if (opt == null || opt.tileType != tileType || opt.variants == null)
                continue;

            for (int v = 0; v < opt.variants.Length; v++)
            {
                var varnt = opt.variants[v];
                if (varnt == null || varnt.prefab == null || varnt.environmentType != env)
                    continue;

                found = true;
                float d = VariantClimateDistance(varnt, temp, hum);
                if (d < best)
                    best = d;
            }
        }

        return found ? best : float.PositiveInfinity;
    }

    private bool VariantPassesClimateRange(EnvironmentTileVariant v, float temp, float hum)
    {
        var f = v != null ? v.neighborEnvFilter : null;
        if (f == null)
            return true;

        if (f.useTemperatureRange)
        {
            float minT = Mathf.Min(f.minTempC, f.maxTempC);
            float maxT = Mathf.Max(f.minTempC, f.maxTempC);
            if (temp < minT || temp > maxT)
                return false;
        }

        if (f.useHumidityRange)
        {
            float minH = Mathf.Min(f.minHumidity, f.maxHumidity);
            float maxH = Mathf.Max(f.minHumidity, f.maxHumidity);
            if (hum < minH || hum > maxH)
                return false;
        }

        return true;
    }

    private float VariantClimateDistance(EnvironmentTileVariant v, float temp, float hum)
    {
        var f = v != null ? v.neighborEnvFilter : null;
        if (f == null)
            return 0f;

        float d = 0f;

        if (f.useTemperatureRange)
        {
            float minT = Mathf.Min(f.minTempC, f.maxTempC);
            float maxT = Mathf.Max(f.minTempC, f.maxTempC);
            if (temp < minT) d += (minT - temp);
            else if (temp > maxT) d += (temp - maxT);
        }

        if (f.useHumidityRange)
        {
            float minH = Mathf.Min(f.minHumidity, f.maxHumidity);
            float maxH = Mathf.Max(f.minHumidity, f.maxHumidity);
            if (hum < minH) d += (minH - hum);
            else if (hum > maxH) d += (hum - maxH);
        }

        return d;
    }

    public ClimateManagerSaveData SaveState()
    {
        ClimateManagerSaveData data = new ClimateManagerSaveData
        {
            cols = cols,
            rows = rows,
            globalTemperatureOffset = globalTemperatureOffset,
            globalHumidityOffset = globalHumidityOffset,
            baseClimateInitialized = baseClimateInitialized,
            hasValidInitialClimate = HasValidInitialClimate,
            pendingJobIndex = _pendingJobIndex,
            planetaryForcingTurnCounter = _planetaryForcingTurnCounter,
            lastPlanetaryForcedRebuildTurn = _lastPlanetaryForcedRebuildTurn,
        };

        int len = cols * rows;
        if (len > 0)
        {
            data.temperature = FlattenFloat2D(temperature, cols, rows);
            data.humidity = FlattenFloat2D(humidity, cols, rows);
            data.temperatureValid = FlattenBool2D(temperatureValid, cols, rows);
            data.humidityValid = FlattenBool2D(humidityValid, cols, rows);

            data.baseTemperatureField = FlattenFloat2D(baseTemperatureField, cols, rows);
            data.baseHumidityField = FlattenFloat2D(baseHumidityField, cols, rows);
            data.waterHumidityBoost = FlattenFloat2D(waterHumidityBoost, cols, rows);

            data.currentEnvironment = FlattenEnvironment2D(currentEnvironment, cols, rows);
        }

        for (int i = 0; i < _pendingJobs.Count; i++)
        {
            ClimateJob job = _pendingJobs[i];
            data.pendingJobs.Add(new ClimateJobSaveData
            {
                x = job.x,
                y = job.y,
                newEnvironment = job.newEnvironment
            });
        }

        return data;
    }

    public void LoadState(ClimateManagerSaveData data)
    {
        if (data == null)
            return;

        if (gridManager == null)
            gridManager = GridManager.Instance;

        if (gridManager == null)
        {
            Debug.LogWarning("[ClimateManager] Cannot load climate state without GridManager.");
            return;
        }

        if (climateCoroutine != null)
        {
            StopCoroutine(climateCoroutine);
            climateCoroutine = null;
        }

        if (_rebuildCoroutine != null)
        {
            StopCoroutine(_rebuildCoroutine);
            _rebuildCoroutine = null;
        }

        cols = data.cols;
        rows = data.rows;

        if (cols <= 0 || rows <= 0)
        {
            Debug.LogWarning("[ClimateManager] Saved climate had invalid dimensions.");
            return;
        }

        temperature = new float[cols, rows];
        humidity = new float[cols, rows];
        temperatureValid = new bool[cols, rows];
        humidityValid = new bool[cols, rows];
        currentEnvironment = new EnvironmentType[cols, rows];

        baseTemperatureField = new float[cols, rows];
        baseHumidityField = new float[cols, rows];
        waterHumidityBoost = new float[cols, rows];

        _planetaryForcingTurnCounter = Mathf.Max(0, data.planetaryForcingTurnCounter);
        _lastPlanetaryForcedRebuildTurn = data.lastPlanetaryForcedRebuildTurn;

        tileGrid = new TileScript[cols, rows];
        BuildTileLookup();

        globalTemperatureOffset = data.globalTemperatureOffset;
        globalHumidityOffset = data.globalHumidityOffset;
        baseClimateInitialized = data.baseClimateInitialized;
        HasValidInitialClimate = data.hasValidInitialClimate;

        if (data.temperature != null) UnflattenFloat2D(data.temperature, temperature, cols, rows);
        if (data.humidity != null) UnflattenFloat2D(data.humidity, humidity, cols, rows);
        if (data.temperatureValid != null) UnflattenBool2D(data.temperatureValid, temperatureValid, cols, rows);
        if (data.humidityValid != null) UnflattenBool2D(data.humidityValid, humidityValid, cols, rows);

        if (data.baseTemperatureField != null) UnflattenFloat2D(data.baseTemperatureField, baseTemperatureField, cols, rows);
        if (data.baseHumidityField != null) UnflattenFloat2D(data.baseHumidityField, baseHumidityField, cols, rows);

        if (data.currentEnvironment != null) UnflattenEnvironment2D(data.currentEnvironment, currentEnvironment, cols, rows);

        _pendingJobs.Clear();
        _pendingJobIndex = Mathf.Clamp(data.pendingJobIndex, 0, data.pendingJobs != null ? data.pendingJobs.Count : 0);

        if (data.pendingJobs != null)
        {
            for (int i = 0; i < data.pendingJobs.Count; i++)
            {
                ClimateJobSaveData savedJob = data.pendingJobs[i];
                if (savedJob == null)
                    continue;

                TileScript tile = null;
                if (savedJob.x >= 0 && savedJob.x < cols && savedJob.y >= 0 && savedJob.y < rows)
                    tile = tileGrid[savedJob.x, savedJob.y];

                _pendingJobs.Add(new ClimateJob
                {
                    x = savedJob.x,
                    y = savedJob.y,
                    tile = tile,
                    newEnvironment = savedJob.newEnvironment
                });
            }
        }

        if (_pendingJobs.Count > 0 && _pendingJobIndex < _pendingJobs.Count)
            climateCoroutine = StartCoroutine(ApplyClimateJobsCoroutine());

        OnClimateRebuilt?.Invoke();
    }

    private static float[] FlattenFloat2D(float[,] src, int width, int height)
    {
        if (src == null)
            return null;

        float[] flat = new float[width * height];
        int idx = 0;

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                flat[idx++] = src[x, y];

        return flat;
    }

    private static bool[] FlattenBool2D(bool[,] src, int width, int height)
    {
        if (src == null)
            return null;

        bool[] flat = new bool[width * height];
        int idx = 0;

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                flat[idx++] = src[x, y];

        return flat;
    }

    private static int[] FlattenEnvironment2D(EnvironmentType[,] src, int width, int height)
    {
        if (src == null)
            return null;

        int[] flat = new int[width * height];
        int idx = 0;

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                flat[idx++] = (int)src[x, y];

        return flat;
    }

    private static void UnflattenFloat2D(float[] flat, float[,] dst, int width, int height)
    {
        int idx = 0;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (idx < flat.Length)
                    dst[x, y] = flat[idx++];
    }

    private static void UnflattenBool2D(bool[] flat, bool[,] dst, int width, int height)
    {
        int idx = 0;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (idx < flat.Length)
                    dst[x, y] = flat[idx++];
    }

    private static void UnflattenEnvironment2D(int[] flat, EnvironmentType[,] dst, int width, int height)
    {
        int idx = 0;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (idx < flat.Length)
                    dst[x, y] = (EnvironmentType)flat[idx++];
    }

    private float GetSeasonTemperatureOffset(SeasonDefinition season)
    {
        return season != null && season.climateShift != null
            ? season.climateShift.seasonTemperatureOffset
            : 0f;
    }

    private float GetSeasonHumidityOffset(SeasonDefinition season)
    {
        return season != null && season.climateShift != null
            ? season.climateShift.seasonHumidityOffset
            : 0f;
    }

    private float GetSeasonTemperatureDriftOnEnter(SeasonDefinition season)
    {
        return season != null && season.climateShift != null
            ? season.climateShift.temperatureDriftOnEnter
            : 0f;
    }

    private float GetSeasonHumidityDriftOnEnter(SeasonDefinition season)
    {
        return season != null && season.climateShift != null
            ? season.climateShift.humidityDriftOnEnter
            : 0f;
    }

    private float SampleDeterministicSigned01(int x, int y, int salt)
    {
        float v = Mathf.Sin((x * 12.9898f) + (y * 78.233f) + (salt * 37.719f)) * 43758.5453f;
        return (Mathf.Repeat(v, 1f) * 2f) - 1f;
    }

    private float SampleTemperatureJitter(int x, int y, PlanetarySectionSettings section)
    {
        float range = section != null ? section.localTemperatureRange : 0f;
        if (range <= 0f) return 0f;

        return SampleDeterministicSigned01(x, y, 101) * range;
    }

    private float SampleHumidityJitter(int x, int y, PlanetarySectionSettings section)
    {
        float range = section != null ? section.localHumidityRange : 0f;
        if (range <= 0f) return 0f;

        return SampleDeterministicSigned01(x, y, 202) * range;
    }

    private PlanetaryForcingSettings GetPlanetaryForcingSettings()
    {
        var presetMgr = EnvironmentPresetManager.Instance;
        if (presetMgr == null)
            return null;

        var preset = presetMgr.GetCurrentPreset();
        if (preset == null || preset.planetarySection == null)
            return null;

        return preset.planetarySection.planetaryForcing;
    }

    private PlanetaryForcingSample EvaluatePlanetaryForcingSample()
    {
        PlanetaryForcingSettings settings = GetPlanetaryForcingSettings();
        if (settings == null || !settings.enabled)
            return PlanetaryForcingSample.Default;

        int cycleTurns = Mathf.Max(1, settings.masterCycleTurns);
        float t01 = Mathf.Repeat(((float)_planetaryForcingTurnCounter / cycleTurns) + settings.phaseOffset01, 1f);
        float twoPi = Mathf.PI * 2f;

        float ecc = Mathf.Sin(t01 * twoPi * settings.eccentricityFrequency);
        float obl = Mathf.Sin(t01 * twoPi * settings.obliquityFrequency);
        float pre = Mathf.Sin(t01 * twoPi * settings.precessionFrequency);

        PlanetaryForcingSample sample = PlanetaryForcingSample.Default;

        // Eccentricity: mean warmth + stronger/weaker seasonality
        sample.meanTemperatureOffset += ecc * settings.eccentricityMeanTempAmplitude;
        sample.meanHumidityOffset += ecc * settings.eccentricityMeanHumidityAmplitude;
        sample.seasonalTemperatureStrengthMultiplier += ecc * settings.eccentricitySeasonStrengthAmplitude;
        sample.seasonalHumidityStrengthMultiplier += ecc * (settings.eccentricitySeasonStrengthAmplitude * 0.5f);

        // Obliquity: weaker/stronger pole-equator gradient and stronger/weaker seasons
        sample.equatorTemperatureOffset += obl * settings.obliquityEquatorTempAmplitude;
        sample.poleTemperatureOffset += obl * settings.obliquityPoleTempAmplitude;
        sample.equatorHumidityOffset += obl * settings.obliquityEquatorHumidityAmplitude;
        sample.poleHumidityOffset += obl * settings.obliquityPoleHumidityAmplitude;
        sample.seasonalTemperatureStrengthMultiplier += obl * settings.obliquitySeasonStrengthAmplitude;
        sample.seasonalHumidityStrengthMultiplier += obl * (settings.obliquitySeasonStrengthAmplitude * 0.5f);

        // Precession: north/south bias + small global nudge
        sample.northSouthTemperatureBias += pre * settings.precessionNorthSouthTempBiasAmplitude;
        sample.northSouthHumidityBias += pre * settings.precessionNorthSouthHumidityBiasAmplitude;
        sample.meanTemperatureOffset += pre * settings.precessionMeanTempAmplitude;
        sample.meanHumidityOffset += pre * settings.precessionMeanHumidityAmplitude;

        sample.seasonalTemperatureStrengthMultiplier = Mathf.Max(0.1f, sample.seasonalTemperatureStrengthMultiplier);
        sample.seasonalHumidityStrengthMultiplier = Mathf.Max(0.1f, sample.seasonalHumidityStrengthMultiplier);

        return sample;
    }

    private bool ApplyWaterEvaporationHumidityTurnEnd()
    {
        if (!waterEvaporationHumidityEnabled)
            return false;

        if (!IsClimateReady())
            return false;

        if (waterHumidityBoost == null)
            return false;

        int changedCells = 0;
        int sourceTilesChecked = 0;

        MonoEnvironmentDataSource envSource = MonoEnvironmentDataSource.Instance;

        if (envSource != null)
        {
            foreach (var kvp in envSource.AllTiles)
            {
                TileCoord primaryCoord = kvp.Key;
                EnvironmentControl env = kvp.Value;

                if (env == null)
                    continue;

                sourceTilesChecked++;

                if (!IsWaterEvaporationSource(env.environmentTileType, env.environmentType))
                    continue;

                _footprintBuffer.Clear();

                if (!envSource.TryGetFootprintCoords(primaryCoord, _footprintBuffer) || _footprintBuffer.Count == 0)
                    _footprintBuffer.Add(primaryCoord);

                for (int i = 0; i < _footprintBuffer.Count; i++)
                {
                    TileCoord coord = _footprintBuffer[i];

                    if (TryApplyWaterEvaporationToCell(coord.x, coord.y))
                        changedCells++;
                }
            }
        }

        // Fallback in case MonoEnvironmentDataSource has not registered tiles yet.
        if (sourceTilesChecked == 0 && _climateCells != null)
        {
            for (int i = 0; i < _climateCells.Count; i++)
            {
                ClimateCell cell = _climateCells[i];
                TileScript tile = cell.tile;

                if (tile == null || !tile.HasSpawned)
                    continue;

                EnvironmentTileType tileType = tile.GetChosenTileType();
                EnvironmentType envType = tile.GetChosenEnvironmentType();

                if (!IsWaterEvaporationSource(tileType, envType))
                    continue;

                if (TryApplyWaterEvaporationToCell(cell.x, cell.y))
                    changedCells++;
            }
        }

        if (waterEvaporationDebugLogging && changedCells > 0)
            Debug.Log($"[ClimateManager] Water evaporation increased humidity on {changedCells} cells.");

        return changedCells > 0;
    }

    private bool TryApplyWaterEvaporationToCell(int x, int y)
    {
        if (x < 0 || x >= cols || y < 0 || y >= rows)
            return false;

        if (temperature == null || humidity == null || waterHumidityBoost == null)
            return false;

        if (temperatureValid == null || humidityValid == null)
            return false;

        if (!temperatureValid[x, y] || !humidityValid[x, y])
            return false;

        float tempC = temperature[x, y];

        if (tempC < waterEvaporationMinTemperatureC)
            return false;

        float tempRange = waterEvaporationFullTemperatureC - waterEvaporationMinTemperatureC;
        float heat01 = tempRange > 0.001f
            ? Mathf.Clamp01((tempC - waterEvaporationMinTemperatureC) / tempRange)
            : 1f;

        float add = waterEvaporationHumidityPerTurnAtFullTemp * heat01;

        if (add <= 0.000001f)
            return false;

        float oldBoost = waterHumidityBoost[x, y];
        float newBoost = Mathf.Min(waterEvaporationMaxLocalBoost, oldBoost + add);

        if (newBoost <= oldBoost + 0.000001f)
            return false;

        float delta = newBoost - oldBoost;

        waterHumidityBoost[x, y] = newBoost;
        humidity[x, y] = Mathf.Clamp01(humidity[x, y] + delta);
        humidityValid[x, y] = true;

        return true;
    }

    private float ApplyLocalWaterHumidityBoost(int x, int y, float baseHumidity)
    {
        if (waterHumidityBoost == null)
            return Mathf.Clamp01(baseHumidity);

        if (x < 0 || x >= cols || y < 0 || y >= rows)
            return Mathf.Clamp01(baseHumidity);

        return Mathf.Clamp01(baseHumidity + waterHumidityBoost[x, y]);
    }

    private bool IsWaterEvaporationSource(EnvironmentTileType tileType, EnvironmentType envType)
    {
        if (envType == EnvironmentType.Ocean || envType == EnvironmentType.Lake)
            return true;

        switch (tileType)
        {
            case EnvironmentTileType.Water:
            case EnvironmentTileType.Ocean:
            case EnvironmentTileType.Lake:
            case EnvironmentTileType.LakeEdge:
            case EnvironmentTileType.LakeCorner:

            // Your project calls beaches/coasts these tile types.
            case EnvironmentTileType.Coastline:
            case EnvironmentTileType.CoastlineCorner:
                return true;

            default:
                return false;
        }
    }
}