using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

public class SaveSystem : MonoBehaviour
{
    public static SaveSystem Instance { get; private set; }

    private enum SaveSlot
    {
        CloseSave,
        TurnAutoSave
    }

    [Header("Save Files")]
    [SerializeField] private string closeSaveFileName = "environment_save.json";
    [SerializeField] private string turnAutoSaveFileName = "environment_turn_autosave.json";

    [Header("Save Capture")]
    [SerializeField, Min(1)] private int saveObjectsPerFrame = 8;

    [Header("Save Debounce")]
    [SerializeField, Min(0f)] private float saveDelay = 1.5f;

    [Header("Save Chunking")]
    [SerializeField, Min(10)] private int tileSaveChunkSize = 50;

    [Header("Cached Refs")]
    [SerializeField] private CameraControl cameraControl;
    [SerializeField] private PlayerPopulationStatistic playerPopulationStatistic;
    [SerializeField] private AnimalSimulationController animalSimulationController;

    [Header("World Load Helpers")]
    [SerializeField] private SavedTilePlacer savedTilePlacer;
    [SerializeField] private TileActivator tileActivator;

    private string _closeSaveFilePath;
    private string _turnAutoSaveFilePath;

    private SaveSlot _queuedSaveSlot = SaveSlot.TurnAutoSave;

    private static Dictionary<string, GameObject> tilePrefabLookup;
    private static Dictionary<string, GameObject> buildingPrefabLookup;
    private static Dictionary<string, ResourceDefinition> resourceLookup;

    public event Action<int, int> OnLoadProgressChanged;

    public event Action OnSaveQueued;
    public event Action OnSaveStarted;
    public event Action OnSaveCompleted;
    public event Action<string> OnSaveFailed;

    public int LoadPhaseCount => 8;

    private readonly Dictionary<string, ISaveSection> _sections = new Dictionary<string, ISaveSection>();

    private Coroutine _saveCoroutine;
    private bool _isSaving;
    private bool _isLoading;
    private bool _saveQueued;
    private float _saveTimer;

    private SaveSnapshot _cachedSnapshot;
    private bool _hasCachedSnapshot;

    private Task _backgroundWriteTask;
    private bool _backgroundSaveInFlight;
    private string _backgroundSaveError;

    public bool IsSaving => _isSaving || _backgroundSaveInFlight || _saveQueued;
    public bool IsLoading => _isLoading;

    private static readonly JsonSerializerSettings BackgroundJsonSettings = new JsonSerializerSettings
    {
        Formatting = Formatting.None,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
    };

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        _closeSaveFilePath = Path.Combine(Application.persistentDataPath, closeSaveFileName);
        _turnAutoSaveFilePath = Path.Combine(Application.persistentDataPath, turnAutoSaveFileName);

        EncryptionHelper.WarmUpKeys();
        RegisterSections();
        RefreshCachedReferences();
    }

    private void Update()
    {
        if (_backgroundSaveInFlight && _backgroundWriteTask != null && _backgroundWriteTask.IsCompleted)
        {
            _backgroundSaveInFlight = false;

            string error = _backgroundSaveError;
            _backgroundSaveError = null;

            if (!string.IsNullOrEmpty(error))
            {
                //Debug.LogError("[SaveSystem] Background save failed:\n" + error);
                OnSaveFailed?.Invoke(error);
            }
            else
            {
                //Debug.Log("[SaveSystem] Background save completed successfully.");
                OnSaveCompleted?.Invoke();
            }
        }

        if (!_isSaving && !_backgroundSaveInFlight && _saveQueued)
        {
            _saveTimer -= Time.unscaledDeltaTime;
            if (_saveTimer <= 0f)
            {
                _saveQueued = false;
                _saveTimer = saveDelay;
                StartSaveCaptureNow(_queuedSaveSlot);
            }
        }
    }

    private void RegisterSections()
    {
        _sections.Clear();

        _sections.Add(SaveSectionKeys.WorldObjects, new WorldObjectsSaveSection());
        _sections.Add(SaveSectionKeys.CoreSystems, new CoreSystemsSaveSection());
        _sections.Add(SaveSectionKeys.Knowledge, new KnowledgeSaveSection());
        _sections.Add(SaveSectionKeys.Population, new PopulationSaveSection());
        _sections.Add(SaveSectionKeys.WorldSim, new WorldSimSaveSection());
        _sections.Add(SaveSectionKeys.Jobs, new JobsSaveSection());
        _sections.Add(SaveSectionKeys.Notifications, new NotificationsSaveSection());
    }

    private void RefreshCachedReferences()
    {
        if (cameraControl == null)
            cameraControl = FindObjectOfType<CameraControl>(true);

        if (playerPopulationStatistic == null)
            playerPopulationStatistic = FindObjectOfType<PlayerPopulationStatistic>(true);

        if (animalSimulationController == null)
            animalSimulationController = FindObjectOfType<AnimalSimulationController>(true);
    }

    public static void MarkSectionDirty(string key)
    {
        if (Instance == null)
            return;

        if (Instance._sections.TryGetValue(key, out ISaveSection section))
            section.MarkDirty();
    }

    public static void RequestTurnAutoSave()
    {
        if (Instance == null || Instance._isLoading)
            return;

        if (Instance._queuedSaveSlot != SaveSlot.CloseSave)
            Instance._queuedSaveSlot = SaveSlot.TurnAutoSave;

        if (Instance._saveQueued || Instance._isSaving || Instance._backgroundSaveInFlight)
            return;

        Instance._saveQueued = true;
        Instance._saveTimer = Instance.saveDelay;
        Instance.OnSaveQueued?.Invoke();
    }

    public static void SaveCloseGameNow()
    {
        if (Instance == null || Instance._isLoading)
            return;

        Instance._saveQueued = false;
        Instance._saveTimer = Instance.saveDelay;
        Instance._queuedSaveSlot = SaveSlot.CloseSave;

        if (Instance._isSaving || Instance._backgroundSaveInFlight)
        {
            Instance._saveQueued = true;
            Instance._saveTimer = 0f;
            Instance.OnSaveQueued?.Invoke();
            return;
        }

        Instance.StartSaveCaptureNow(SaveSlot.CloseSave);
    }

    // Compatibility wrappers so existing calls still work.
    public static void RequestSave()
    {
        RequestTurnAutoSave();
    }

    public static void SaveGame()
    {
        SaveCloseGameNow();
    }

    public static bool HasSave()
    {
        if (Instance == null)
            return false;

        return Instance.IsUsableSaveSet(Instance._closeSaveFilePath) ||
               Instance.IsUsableSaveSet(Instance._turnAutoSaveFilePath);
    }

    public static void DeleteSave()
    {
        if (Instance == null)
            return;

        try
        {
            Instance.DeleteSaveForRoot(Instance._closeSaveFilePath);
            Instance.DeleteSaveForRoot(Instance._turnAutoSaveFilePath);
        }
        catch (Exception ex)
        {
            //Debug.LogError("[SaveSystem] Failed to delete save files:\n" + ex);
        }
    }

    public void InstallWorldLoadHelpers(SavedTilePlacer newSavedTilePlacer, TileActivator newTileActivator)
    {
        savedTilePlacer = newSavedTilePlacer;
        tileActivator = newTileActivator;
    }

    private void DeleteSaveForRoot(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            return;

        string rootDir = Path.GetDirectoryName(rootPath);
        string rootStem = Path.GetFileNameWithoutExtension(rootPath);

        if (File.Exists(rootPath))
            File.Delete(rootPath);

        if (File.Exists(rootPath + ".bak"))
            File.Delete(rootPath + ".bak");

        if (File.Exists(rootPath + ".tmp"))
            File.Delete(rootPath + ".tmp");

        if (!string.IsNullOrEmpty(rootDir) && Directory.Exists(rootDir))
        {
            string[] partFiles = Directory.GetFiles(rootDir, $"{rootStem}.*");
            for (int i = 0; i < partFiles.Length; i++)
            {
                if (File.Exists(partFiles[i]))
                    File.Delete(partFiles[i]);
            }
        }
    }

    private void ReportLoadProgress(int completedPhases)
    {
        int total = LoadPhaseCount;
        int remaining = Mathf.Clamp(total - completedPhases, 0, total);
        OnLoadProgressChanged?.Invoke(total, remaining);
    }

    private void StartSaveCaptureNow(SaveSlot slot)
    {
        if (_saveCoroutine != null)
            StopCoroutine(_saveCoroutine);

        _saveCoroutine = StartCoroutine(SaveSnapshotCoroutine(slot));
    }

    private IEnumerator SaveSnapshotCoroutine(SaveSlot slot)
    {
        if (_backgroundSaveInFlight)
        {
            _saveQueued = true;
            _saveTimer = 0f;
            yield break;
        }

        _isSaving = true;
        OnSaveStarted?.Invoke();
        RefreshCachedReferences();

        if (_hasCachedSnapshot && !HasAnyDirtySections())
        {
            //Debug.Log("[SaveSystem] No dirty sections. Reusing cached snapshot.");
            SaveSnapshot cachedSnapshot = CreateWorkingSnapshotFromCache();
            cachedSnapshot.meta = BuildMetaForSnapshot(cachedSnapshot);
            StartBackgroundWrite(cachedSnapshot, slot);

            _isSaving = false;
            _saveCoroutine = null;
            yield break;
        }

        SaveSnapshot snapshot = CreateWorkingSnapshotFromCache();

        SaveCaptureContext context = new SaveCaptureContext(
            cameraControl,
            playerPopulationStatistic,
            animalSimulationController
        );

        foreach (ISaveSection section in _sections.Values)
        {
            if (_hasCachedSnapshot && !section.IsDirty)
            {
                //Debug.Log($"[SaveSystem] Reusing cached section '{section.Key}'.");
                continue;
            }

            float start = Time.realtimeSinceStartup;
            yield return section.CaptureInto(snapshot, context, saveObjectsPerFrame);
            //Debug.Log($"[SaveSystem] Captured section '{section.Key}' in {Time.realtimeSinceStartup - start:0.000}s");
        }

        int liveBuildingCount = FindObjectsOfType<BuildingSaveable>(true).Length;
        int liveConstructionCount = FindObjectsOfType<ConstructionTileSaveable>(true).Length;

        //Debug.Log($"[SaveSystem] Snapshot counts: tiles={snapshot.tiles.Count}, buildings={snapshot.buildings.Count}, constructions={snapshot.constructions.Count}");
        //Debug.Log($"[SaveSystem] Live counts: buildings={liveBuildingCount}, constructions={liveConstructionCount}");

        if (liveBuildingCount > 0 && snapshot.buildings.Count == 0)
        {
            string error = "[SaveSystem] Refusing to save: live buildings exist, but snapshot.buildings is empty.";
            //Debug.LogError(error);
            OnSaveFailed?.Invoke(error);

            _isSaving = false;
            _saveCoroutine = null;
            yield break;
        }

        if (liveConstructionCount > 0 && snapshot.constructions.Count == 0)
        {
            string error = "[SaveSystem] Refusing to save: live construction tiles exist, but snapshot.constructions is empty.";
            //Debug.LogError(error);
            OnSaveFailed?.Invoke(error);

            _isSaving = false;
            _saveCoroutine = null;
            yield break;
        }

        snapshot.meta = BuildMetaForSnapshot(snapshot);

        CommitSnapshotToCache(snapshot);
        StartBackgroundWrite(snapshot, slot);

        _isSaving = false;
        _saveCoroutine = null;
    }

    private int GetTileChunkCount(int tileCount)
    {
        int safeChunkSize = Mathf.Max(1, tileSaveChunkSize);
        return Mathf.CeilToInt(tileCount / (float)safeChunkSize);
    }

    private void StartBackgroundWrite(SaveSnapshot snapshot, SaveSlot slot)
    {
        if (_backgroundSaveInFlight)
        {
            _saveQueued = true;
            _saveTimer = 0f;
            return;
        }

        _backgroundSaveInFlight = true;
        _backgroundSaveError = null;

        string rootPath = GetRootPath(slot);

        _backgroundWriteTask = Task.Run(() =>
        {
            try
            {
                WriteSnapshotToDisk(snapshot, rootPath);
            }
            catch (Exception ex)
            {
                _backgroundSaveError = ex.ToString();
            }
        });

        //Debug.Log($"[SaveSystem] Save write started for slot: {slot}");
    }

    private void WriteSnapshotToDisk(SaveSnapshot snapshot, string rootPath)
    {
        string rootDir = Path.GetDirectoryName(rootPath);
        string rootStem = Path.GetFileNameWithoutExtension(rootPath);
        int safeChunkSize = Mathf.Max(1, tileSaveChunkSize);

        if (!string.IsNullOrWhiteSpace(rootDir))
            Directory.CreateDirectory(rootDir);

        // Collect all independent write actions so they can run in parallel.
        var writeActions = new System.Collections.Generic.List<Action>();

        for (int i = 0; i < snapshot.tiles.Count; i += safeChunkSize)
        {
            int chunkStart = i;
            int chunkIndex = i / safeChunkSize;
            string chunkPath = Path.Combine(rootDir, $"{rootStem}.world_tiles_{chunkIndex}.json");

            writeActions.Add(() =>
            {
                TileChunkSaveData chunk = new TileChunkSaveData();
                int end = System.Math.Min(chunkStart + safeChunkSize, snapshot.tiles.Count);
                for (int j = chunkStart; j < end; j++)
                    chunk.tiles.Add(snapshot.tiles[j]);
                WriteJsonAtomically(chunkPath, chunk);
            });
        }

        if (snapshot.buildings != null && snapshot.buildings.Count > 0)
        {
            string path = Path.Combine(rootDir, $"{rootStem}.world_buildings.json");
            writeActions.Add(() => WriteJsonAtomically(path,
                new BuildingSectionSaveData { buildings = snapshot.buildings }));
        }

        if (snapshot.constructions != null && snapshot.constructions.Count > 0)
        {
            string path = Path.Combine(rootDir, $"{rootStem}.world_construction.json");
            writeActions.Add(() => WriteJsonAtomically(path,
                new ConstructionSectionSaveData { constructionTiles = snapshot.constructions }));
        }

        if (snapshot.coreSystems != null)
        {
            string path = Path.Combine(rootDir, $"{rootStem}.core_systems.json");
            writeActions.Add(() => WriteJsonAtomically(path, snapshot.coreSystems));
        }

        if (snapshot.knowledge != null)
        {
            string path = Path.Combine(rootDir, $"{rootStem}.knowledge.json");
            writeActions.Add(() => WriteJsonAtomically(path, snapshot.knowledge));
        }

        if (snapshot.population != null)
        {
            string path = Path.Combine(rootDir, $"{rootStem}.population.json");
            writeActions.Add(() => WriteJsonAtomically(path, snapshot.population));
        }

        if (snapshot.worldSim != null)
        {
            string path = Path.Combine(rootDir, $"{rootStem}.world_sim.json");
            writeActions.Add(() => WriteJsonAtomically(path, snapshot.worldSim));
        }

        if (snapshot.jobs != null)
        {
            string path = Path.Combine(rootDir, $"{rootStem}.jobs.json");
            writeActions.Add(() => WriteJsonAtomically(path, snapshot.jobs));
        }

        if (snapshot.notifications != null)
        {
            string path = Path.Combine(rootDir, $"{rootStem}.notifications.json");
            writeActions.Add(() => WriteJsonAtomically(path, snapshot.notifications));
        }

        // Write all sections in parallel, then meta LAST so the save is only
        // considered complete once every part exists on disk.
        Parallel.Invoke(writeActions.ToArray());
        WriteJsonAtomically(rootPath, snapshot.meta);
    }

    private string GetRootPath(SaveSlot slot)
    {
        return slot == SaveSlot.CloseSave ? _closeSaveFilePath : _turnAutoSaveFilePath;
    }

    private string GetSavePartPath(string rootPath, string suffix)
    {
        string rootDir = Path.GetDirectoryName(rootPath);
        string rootStem = Path.GetFileNameWithoutExtension(rootPath);
        return Path.Combine(rootDir, $"{rootStem}.{suffix}.json");
    }

    private void WriteJsonAtomically<T>(string finalPath, T payload)
    {
        string tempPath = finalPath + ".tmp";
        string backupPath = finalPath + ".bak";

        string json = JsonConvert.SerializeObject(payload, BackgroundJsonSettings);
        byte[] encryptedBytes = EncryptionHelper.EncryptToBytes(json);

        File.WriteAllBytes(tempPath, encryptedBytes);

        if (File.Exists(finalPath))
        {
            if (File.Exists(backupPath))
                File.Delete(backupPath);

            File.Copy(finalPath, backupPath);
            File.Delete(finalPath);
        }

        File.Move(tempPath, finalPath);
    }

    private bool SaveFileExistsOrBackup(string path)
    {
        return File.Exists(path) || File.Exists(path + ".bak");
    }

    private bool IsUsableSaveSet(string rootPath)
    {
        EnvironmentSaveMeta meta = ReadJsonFile<EnvironmentSaveMeta>(rootPath);
        if (meta == null)
            return false;

        for (int i = 0; i < Mathf.Max(0, meta.tileChunkCount); i++)
        {
            if (!SaveFileExistsOrBackup(GetSavePartPath(rootPath, $"world_tiles_{i}")))
                return false;
        }

        if (meta.hasBuildings && !SaveFileExistsOrBackup(GetSavePartPath(rootPath, "world_buildings")))
            return false;

        if (meta.hasConstruction && !SaveFileExistsOrBackup(GetSavePartPath(rootPath, "world_construction")))
            return false;

        if (meta.hasCoreSystems && !SaveFileExistsOrBackup(GetSavePartPath(rootPath, "core_systems")))
            return false;

        if (meta.hasKnowledge && !SaveFileExistsOrBackup(GetSavePartPath(rootPath, "knowledge")))
            return false;

        if (meta.hasPopulation && !SaveFileExistsOrBackup(GetSavePartPath(rootPath, "population")))
            return false;

        if (meta.hasWorldSim && !SaveFileExistsOrBackup(GetSavePartPath(rootPath, "world_sim")))
            return false;

        if (meta.hasJobs && !SaveFileExistsOrBackup(GetSavePartPath(rootPath, "jobs")))
            return false;

        return true;
    }

    private bool TryGetBestLoadRoot(out string rootPath, out EnvironmentSaveMeta meta)
    {
        if (IsUsableSaveSet(_closeSaveFilePath))
        {
            rootPath = _closeSaveFilePath;
            meta = ReadJsonFile<EnvironmentSaveMeta>(rootPath);
            //Debug.Log("[SaveSystem] Loading close save.");
            return true;
        }

        if (IsUsableSaveSet(_turnAutoSaveFilePath))
        {
            rootPath = _turnAutoSaveFilePath;
            meta = ReadJsonFile<EnvironmentSaveMeta>(rootPath);
            //Debug.LogWarning("[SaveSystem] Close save invalid or incomplete. Falling back to turn autosave.");
            return true;
        }

        rootPath = null;
        meta = null;
        return false;
    }

    private T ReadJsonFile<T>(string path) where T : class
    {
        T loaded = TryReadJsonFile<T>(path);
        if (loaded != null)
            return loaded;

        string backupPath = path + ".bak";
        if (File.Exists(backupPath))
        {
            //Debug.LogWarning($"[SaveSystem] Main save failed, trying backup: {backupPath}");
            loaded = TryReadJsonFile<T>(backupPath);

            if (loaded != null)
                return loaded;
        }

        return null;
    }

    private T TryReadJsonFile<T>(string path) where T : class
    {
        if (!File.Exists(path))
            return null;

        try
        {
            byte[] fileBytes = File.ReadAllBytes(path);
            if (fileBytes == null || fileBytes.Length == 0)
                return null;

            string json;

            if (EncryptionHelper.LooksEncryptedBytes(fileBytes))
            {
                json = EncryptionHelper.DecryptFromBytes(fileBytes);
            }
            else
            {
                // Plaintext fallback for older saves
                json = File.ReadAllText(path);
            }

            if (string.IsNullOrWhiteSpace(json))
                return null;

            return JsonConvert.DeserializeObject<T>(json);
        }
        catch (CryptographicException ex)
        {
            //Debug.LogError($"[SaveSystem] Save file '{path}' failed validation or was tampered with:\n{ex}");
            return null;
        }
        catch (Exception ex)
        {
            //Debug.LogError($"[SaveSystem] Failed to read save file '{path}':\n{ex}");
            return null;
        }
    }

    private IEnumerator WaitForWorldLoadHelpers()
    {
        float timeout = 10f;
        float elapsed = 0f;

        while ((savedTilePlacer == null || tileActivator == null) && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (savedTilePlacer == null) {}
            //Debug.LogWarning("[SaveSystem] SavedTilePlacer was not assigned before load.");

        if (tileActivator == null) {}
            //Debug.LogWarning("[SaveSystem] TileActivator was not assigned before load.");
    }

    private void BuildPrefabLookups()
    {
        if (tilePrefabLookup == null)
            tilePrefabLookup = new Dictionary<string, GameObject>(StringComparer.Ordinal);

        if (buildingPrefabLookup == null)
            buildingPrefabLookup = new Dictionary<string, GameObject>(StringComparer.Ordinal);

        if (resourceLookup == null)
            resourceLookup = new Dictionary<string, ResourceDefinition>(StringComparer.Ordinal);

        tilePrefabLookup.Clear();
        buildingPrefabLookup.Clear();
        resourceLookup.Clear();

        GameObject[] allPrefabs = Resources.LoadAll<GameObject>(string.Empty);
        for (int i = 0; i < allPrefabs.Length; i++)
        {
            GameObject prefab = allPrefabs[i];
            if (prefab == null || string.IsNullOrWhiteSpace(prefab.name))
                continue;

            string cleanName = CleanPrefabName(prefab.name);

            if (prefab.GetComponent<TileScript>() != null && !tilePrefabLookup.ContainsKey(cleanName))
                tilePrefabLookup.Add(cleanName, prefab);

            bool isWorldObjectPrefab =
                prefab.GetComponent<BuildingControl>() != null ||
                prefab.GetComponent<BuildingConstruction>() != null ||
                prefab.GetComponent<BuildingSaveable>() != null ||
                prefab.GetComponent<ConstructionTileSaveable>() != null;

            if (isWorldObjectPrefab && !buildingPrefabLookup.ContainsKey(cleanName))
                buildingPrefabLookup.Add(cleanName, prefab);
        }

        ResourceDefinition[] resources = Resources.LoadAll<ResourceDefinition>(string.Empty);
        for (int i = 0; i < resources.Length; i++)
        {
            ResourceDefinition def = resources[i];
            if (def == null)
                continue;

            if (!string.IsNullOrWhiteSpace(def.resourceName) && !resourceLookup.ContainsKey(def.resourceName))
                resourceLookup.Add(def.resourceName, def);

            if (!string.IsNullOrWhiteSpace(def.name) && !resourceLookup.ContainsKey(def.name))
                resourceLookup.Add(def.name, def);
        }

        //Debug.Log($"[SaveSystem] Prefab lookup built. Tiles={tilePrefabLookup.Count}, WorldObjects={buildingPrefabLookup.Count}");
    }

    private static string CleanPrefabName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return string.Empty;

        return rawName.Replace("(Clone)", "").Trim();
    }

    private static ResourceDefinition ResolveResourceDefinitionByKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || resourceLookup == null)
            return null;

        resourceLookup.TryGetValue(key.Trim(), out ResourceDefinition result);
        return result;
    }

    private IEnumerator LoadTileChunks(string rootPath, EnvironmentSaveMeta meta, List<TileSaveData> outTiles)
    {
        outTiles.Clear();

        if (meta == null || !meta.isChunkedSave)
        {
            //Debug.LogWarning("[SaveSystem] Meta is null or not chunked. No tile chunks loaded.");
            yield break;
        }

        string rootDir = Path.GetDirectoryName(rootPath);
        string rootStem = Path.GetFileNameWithoutExtension(rootPath);

        //Debug.Log($"[SaveSystem] LoadTileChunks: meta.tileChunkCount={meta.tileChunkCount}");

        int chunkCount = Mathf.Max(0, meta.tileChunkCount);

        // Recovery path: if meta says 0, try scanning for existing tile chunk files anyway.
        if (chunkCount <= 0 && !string.IsNullOrEmpty(rootDir) && Directory.Exists(rootDir))
        {
            string[] discovered = Directory.GetFiles(rootDir, $"{rootStem}.world_tiles_*.json");
            Array.Sort(discovered, StringComparer.OrdinalIgnoreCase);

            //Debug.Log($"[SaveSystem] Meta had 0 tile chunks. Found {discovered.Length} tile chunk files on disk.");

            for (int i = 0; i < discovered.Length; i++)
            {
                string path = discovered[i];
                TileChunkSaveData chunk = ReadJsonFile<TileChunkSaveData>(path);

                if (chunk != null && chunk.tiles != null)
                {
                    outTiles.AddRange(chunk.tiles);
                    //Debug.Log($"[SaveSystem] Loaded recovery tile chunk '{Path.GetFileName(path)}' with {chunk.tiles.Count} tiles.");
                }
                else
                {
                    //Debug.LogWarning($"[SaveSystem] Recovery tile chunk '{Path.GetFileName(path)}' was empty or invalid.");
                }

                yield return null;
            }

            yield break;
        }

        for (int i = 0; i < chunkCount; i++)
        {
            string path = GetSavePartPath(rootPath, $"world_tiles_{i}");
            bool exists = File.Exists(path) || File.Exists(path + ".bak");

            //Debug.Log($"[SaveSystem] Loading tile chunk {i + 1}/{chunkCount}: {path} exists={exists}");

            TileChunkSaveData chunk = ReadJsonFile<TileChunkSaveData>(path);

            if (chunk != null && chunk.tiles != null)
            {
                outTiles.AddRange(chunk.tiles);
                //Debug.Log($"[SaveSystem] Loaded chunk {i} with {chunk.tiles.Count} tiles.");
            }
            else
            {
                //Debug.LogWarning($"[SaveSystem] Tile chunk file missing or empty: {path}");
            }

            yield return null;
        }
    }

    private IEnumerator LoadTiles(List<TileSaveData> savedTiles)
    {
        if (savedTiles == null || savedTiles.Count == 0)
            yield break;

        for (int i = 0; i < savedTiles.Count; i++)
        {
            TileSaveData data = savedTiles[i];
            if (data == null || data.tileData == null)
                continue;

            TileScript existingTile = FindMatchingExistingTile(data.tileData);
            if (existingTile == null)
                continue;

            TileSaveable saveable = existingTile.GetComponent<TileSaveable>();
            if (saveable != null)
                saveable.LoadState(data.tileData);

            RestoreEnvironmentState(existingTile, data.environmentData);

            if (i % Mathf.Max(1, saveObjectsPerFrame) == 0)
                yield return null;
        }
    }

    private void LoadBuildings(List<BuildingTileSaveData> savedBuildings)
    {
        if (savedBuildings == null)
            return;

        BuildingSaveable[] existingBuildings = FindObjectsOfType<BuildingSaveable>(true);
        HashSet<BuildingSaveable> usedBuildings = new HashSet<BuildingSaveable>();

        for (int i = 0; i < savedBuildings.Count; i++)
        {
            BuildingTileSaveData buildingData = savedBuildings[i];
            if (buildingData == null || buildingData.saveData == null || buildingData.saveData.transformData == null)
                continue;

            //Debug.Log($"[SaveSystem] Loading building prefab '{buildingData.prefabName}'");

            BuildingSaveable targetBuilding = FindMatchingExistingBuilding(buildingData, existingBuildings, usedBuildings);

            if (targetBuilding == null)
            {
                string prefabKey = CleanPrefabName(buildingData.prefabName);

                if (!buildingPrefabLookup.TryGetValue(prefabKey, out GameObject buildingPrefab))
                {
                    //Debug.LogWarning("[SaveSystem] Building prefab not found for: " + buildingData.prefabName);
                    continue;
                }

                GameObject newBuilding = Instantiate(
                    buildingPrefab,
                    buildingData.saveData.transformData.position,
                    buildingData.saveData.transformData.rotation
                );

                newBuilding.name = CleanPrefabName(buildingPrefab.name);
                newBuilding.transform.localScale = buildingData.saveData.transformData.scale;

                targetBuilding = newBuilding.GetComponent<BuildingSaveable>();
            }

            if (targetBuilding == null)
            {
                //Debug.LogWarning($"[SaveSystem] Instantiated building prefab '{buildingData.prefabName}' but no BuildingSaveable was found.");
                continue;
            }

            usedBuildings.Add(targetBuilding);
            targetBuilding.LoadState(buildingData.saveData);

            //Debug.Log($"[SaveSystem] Restored building '{targetBuilding.name}' id='{buildingData.saveData.uniqueID}'");
        }
    }

    private static BuildingSaveable FindMatchingExistingBuilding(
        BuildingTileSaveData buildingData,
        BuildingSaveable[] existingBuildings,
        HashSet<BuildingSaveable> usedBuildings)
    {
        if (buildingData == null || buildingData.saveData == null || buildingData.saveData.transformData == null)
            return null;

        string wantedId = buildingData.saveData.uniqueID;
        Vector3 wantedPos = buildingData.saveData.transformData.position;

        if (!string.IsNullOrEmpty(wantedId))
        {
            for (int i = 0; i < existingBuildings.Length; i++)
            {
                BuildingSaveable building = existingBuildings[i];
                if (building == null || usedBuildings.Contains(building))
                    continue;

                if (building.uniqueID == wantedId)
                    return building;
            }
        }

        for (int i = 0; i < existingBuildings.Length; i++)
        {
            BuildingSaveable building = existingBuildings[i];
            if (building == null || usedBuildings.Contains(building))
                continue;

            if (Vector3.Distance(building.transform.position, wantedPos) <= 0.01f)
                return building;
        }

        return null;
    }

    private void LoadConstructionTiles(List<ConstructionTileSaveData> savedConstructions)
    {
        PlayerConstructionManager.Instance?.ClearAllConstructionsForLoad();

        BuildingConstruction[] existing = FindObjectsOfType<BuildingConstruction>(true);
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null)
                Destroy(existing[i].gameObject);
        }

        if (savedConstructions == null)
            return;

        for (int i = 0; i < savedConstructions.Count; i++)
        {
            ConstructionTileSaveData saved = savedConstructions[i];
            if (saved == null || saved.constructionTileData == null || saved.constructionTileData.transformData == null)
                continue;

            string prefabKey = CleanPrefabName(saved.constructionTilePrefabName);

            if (!buildingPrefabLookup.TryGetValue(prefabKey, out GameObject constructionPrefab))
            {
                //Debug.LogWarning("Construction prefab not found for: " + saved.constructionTilePrefabName);
                continue;
            }

            GameObject newConstruction = Instantiate(
                constructionPrefab,
                saved.constructionTileData.transformData.position,
                saved.constructionTileData.transformData.rotation
            );

            newConstruction.name = CleanPrefabName(constructionPrefab.name);
            newConstruction.transform.localScale = saved.constructionTileData.transformData.scale;

            ConstructionTileSaveable saveable = newConstruction.GetComponent<ConstructionTileSaveable>();
            if (saveable != null)
                saveable.LoadFromSaveData(saved);
            else {}
                //Debug.LogWarning($"Loaded construction prefab '{constructionPrefab.name}' has no ConstructionTileSaveable.");
        }
    }

    private static TileScript FindMatchingExistingTile(SaveData data)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.uniqueID))
            return null;

        foreach (TileSaveable live in TileSaveable.Live)
        {
            if (live == null)
                continue;

            if (live.uniqueID == data.uniqueID)
                return live.GetComponent<TileScript>();
        }

        return null;
    }

    private static BuildingSaveable FindMatchingExistingBuilding(SaveData data)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.uniqueID))
            return null;

        foreach (BuildingSaveable live in BuildingSaveable.Live)
        {
            if (live == null)
                continue;

            if (live.uniqueID == data.uniqueID)
                return live;
        }

        return null;
    }

    private static void RestoreEnvironmentState(TileScript tile, EnvironmentRuntimeSaveData envData)
    {
        if (tile == null || envData == null)
            return;

        bool spawned = false;

        if (!string.IsNullOrWhiteSpace(envData.spawnedPrefabName))
        {
            spawned = tile.TryForceSpawnSavedPrefab(
                envData.spawnedPrefabName,
                envData.environmentType,
                envData.environmentTileType,
                envData.isDiscovered,
                envData.localYRotation
            );
        }

        if (!spawned)
        {
            spawned = tile.ForceSpawnSpecific(
                envData.environmentType,
                envData.environmentTileType,
                envData.isDiscovered
            );

            if (spawned && tile.GetSpawnedInstance() != null)
            {
                tile.GetSpawnedInstance().transform.localRotation =
                    Quaternion.Euler(0f, envData.localYRotation, 0f);
            }
        }

        if (!spawned)
        {
            //Debug.LogWarning(
                //$"Failed to restore environment on tile '{tile.name}'. " +
                //$"Wanted prefab '{envData.spawnedPrefabName}', envType '{envData.environmentType}', tileType '{envData.environmentTileType}'.");
            return;
        }

        EnvironmentControl envControl = tile.GetComponentInChildren<EnvironmentControl>(true);
        if (envControl == null)
        {
            //Debug.LogWarning($"Spawned environment on tile '{tile.name}' but no EnvironmentControl was found.");
            return;
        }

        envControl.ApplyRuntimeSaveData(envData, ResolveResourceDefinitionByKey);
        RestoreVolcanoState(tile, envData);
    }

    public IEnumerator LoadWorldStateCoroutine()
    {
        if (_isLoading)
            yield break;

        _isLoading = true;

        if (!HasSave())
        {
            _isLoading = false;
            yield break;
        }

        RefreshCachedReferences();
        BuildPrefabLookups();

        yield return WaitForWorldLoadHelpers();
        ReportLoadProgress(1);

        string loadRootPath;
        EnvironmentSaveMeta meta;

        if (!TryGetBestLoadRoot(out loadRootPath, out meta))
        {
            //Debug.LogWarning("[SaveSystem] No valid close save or turn autosave found.");
            _isLoading = false;
            yield break;
        }

        List<TileSaveData> loadedTiles = new List<TileSaveData>();
        yield return LoadTileChunks(loadRootPath, meta, loadedTiles);
        ReportLoadProgress(2);

        if (savedTilePlacer != null)
        {
            yield return savedTilePlacer.PlaceSavedTilesCoroutine(loadedTiles);
        }
        else
        {
            //Debug.LogWarning("[SaveSystem] SavedTilePlacer is missing, falling back to matching existing tiles.");
            yield return LoadTiles(loadedTiles);
        }

        if (tileActivator != null)
        {
            tileActivator.BeginActivation(null, false, false);

            while (tileActivator.IsRunning)
                yield return null;
        }

        ReportLoadProgress(3);
        yield return null;

        //Debug.Log($"[SaveSystem] meta.hasBuildings={meta.hasBuildings}");

        BuildingSectionSaveData buildingSection = null;
        ConstructionSectionSaveData constructionSection = null;
        CoreSystemsSectionSaveData core = null;
        KnowledgeSectionSaveData knowledge = null;
        PopulationSectionSaveData population = null;
        WorldSimSectionSaveData sim = null;
        JobsSectionSaveData jobs = null;

        if (meta.hasBuildings)
        {
            buildingSection = ReadJsonFile<BuildingSectionSaveData>(GetSavePartPath(loadRootPath, "world_buildings"));

            if (buildingSection != null)
                LoadBuildings(buildingSection.buildings);
        }
        ReportLoadProgress(4);

        if (meta.hasConstruction)
        {
            constructionSection = ReadJsonFile<ConstructionSectionSaveData>(GetSavePartPath(loadRootPath, "world_construction"));

            if (constructionSection != null)
                LoadConstructionTiles(constructionSection.constructionTiles);
        }
        ReportLoadProgress(5);

        if (meta.hasCoreSystems)
        {
            core = ReadJsonFile<CoreSystemsSectionSaveData>(GetSavePartPath(loadRootPath, "core_systems"));

            if (core != null)
            {
                if (contextCameraSafe(core.cameraPoseData))
                    cameraControl?.LoadState(core.cameraPoseData);

                TurnSystem.Instance?.LoadState(core.turnData);
                SeasonManager.Instance?.LoadState(core.seasonData);
                ClimateManager.Instance?.LoadState(core.climateData);
                WeatherSystemsSaveLoad.LoadState(core.weatherData);
                PlayerLevel.Instance?.LoadState(core.playerLevelData);
                ProfilePanelControl.Instance?.LoadState(core.playerProfileData);
                CivilizationStateManager.Instance?.LoadState(core.civilizationStateData);
            }
        }
        ReportLoadProgress(6);

        if (meta.hasKnowledge)
        {
            knowledge = ReadJsonFile<KnowledgeSectionSaveData>(GetSavePartPath(loadRootPath, "knowledge"));

            if (knowledge != null)
                LoadKnowledgeSection(knowledge);
        }

        if (meta.hasPopulation)
        {
            population = ReadJsonFile<PopulationSectionSaveData>(GetSavePartPath(loadRootPath, "population"));

            if (population != null)
                LoadPopulationSection(population);
        }

        if (meta.hasWorldSim)
        {
            sim = ReadJsonFile<WorldSimSectionSaveData>(GetSavePartPath(loadRootPath, "world_sim"));

            if (sim != null)
                LoadWorldSimSection(sim);
        }

        if (meta.hasJobs)
        {
            jobs = ReadJsonFile<JobsSectionSaveData>(GetSavePartPath(loadRootPath, "jobs"));

            if (jobs != null)
                LoadJobsSection(jobs);
        }

        if (meta.hasNotifications)
        {
            var notifData = ReadJsonFile<NotificationsSaveData>(GetSavePartPath(loadRootPath, "notifications"));
            NotificationManager.Instance?.LoadState(notifData);
        }

        ReportLoadProgress(7);

        SeedSnapshotCacheFromLoadedData(
            meta,
            loadedTiles,
            buildingSection,
            constructionSection,
            core,
            knowledge,
            population,
            sim,
            jobs
        );

        yield return null;
        ReportLoadProgress(8);

        _isLoading = false;
    }

    private bool contextCameraSafe(CameraPoseSaveData data)
    {
        return data != null && cameraControl != null;
    }

    private void LoadKnowledgeSection(KnowledgeSectionSaveData data)
    {
        if (data == null) return;

        PlayerKnownTechnologyManager.Instance?.LoadState(data.knownTechnologyData);
        PlayerResearchManager.Instance?.LoadState(data.playerResearchData);
        PlayerInventoryManager.Instance?.LoadState(data.inventoryData);
        PlayerKnownResourcesManager.Instance?.LoadState(data.knownResourcesData);
        PlayerKnownCraftingManager.Instance?.LoadState(data.knownCraftingData);
        PlayerKnownProductionManager.Instance?.LoadState(data.knownProductionData);
        PlayerKnownBuildingsManager.Instance?.LoadState(data.knownBuildingsData);
        PlayerKnownUnitsManager.Instance?.LoadState(data.knownUnitsData);

        PlayerKnownSpiritsManager.Instance?.LoadState(data.knownSpiritsData);
        PlayerKnownRitualsManager.Instance?.LoadState(data.knownRitualsData);
        PlayerReligionManager.Instance?.LoadState(data.playerReligionData);
    }

    private void LoadPopulationSection(PopulationSectionSaveData data)
    {
        if (data == null) return;

        PlayersPopulationManager.Instance?.LoadState(data.playersPopulationData);
        PlayerFamilySimulationManager.Instance?.LoadState(data.playerFamilySimulationData);
        contextPopulationStatSafe(data.playerPopulationStatisticData);
        DiseaseManager.Instance?.LoadState(data.playerDiseaseData);
    }

    private void LoadWorldSimSection(WorldSimSectionSaveData data)
    {
        if (data == null)
            return;

        AnimalSimulationAccess.Current?.LoadState(data.animalSimulationData);

        if (data.playerUnitsData != null)
            PlayerUnitSaveLoad.LoadState(data.playerUnitsData);

        if (data.playerTrainingData != null)
            PlayerTrainingManager.Instance?.LoadState(data.playerTrainingData);

        if (data.volcanoManagerData != null)
        {
            if (VolcanoManager.Instance != null)
                VolcanoManager.Instance.LoadState(data.volcanoManagerData);
            else {}
                //Debug.LogWarning("[SaveSystem] Volcano manager save data exists, but no VolcanoManager was found.");
        }

        // Load lava first because flood checks lava blocking.
        if (data.lavaOverlayData != null)
            LavaOverlayManager.Instance?.LoadState(data.lavaOverlayData);

        if (data.floodSimulationData != null)
        {
            FloodSimulationSystem floodSystem =
                UnityEngine.Object.FindObjectOfType<FloodSimulationSystem>(true);

            if (floodSystem != null)
                floodSystem.LoadState(data.floodSimulationData);
            else {}
                //Debug.LogWarning("[SaveSystem] Flood save data exists, but no FloodSimulationSystem was found.");
        }

        // Load fault lines before earthquake sim because the sim uses the fault generator for epicentres.
        if (data.earthquakeFaultLineData != null)
        {
            EarthquakeFaultLineGenerator faultLineGenerator =
                UnityEngine.Object.FindObjectOfType<EarthquakeFaultLineGenerator>(true);

            if (faultLineGenerator != null)
                faultLineGenerator.LoadState(data.earthquakeFaultLineData);
            else {}
                //Debug.LogWarning("[SaveSystem] Earthquake fault line save data exists, but no EarthquakeFaultLineGenerator was found.");
        }

        if (data.earthquakeSimulationData != null)
        {
            EarthquakeSimulationSystem earthquakeSystem =
                UnityEngine.Object.FindObjectOfType<EarthquakeSimulationSystem>(true);

            if (earthquakeSystem != null)
                earthquakeSystem.LoadState(data.earthquakeSimulationData);
            else {}
                //Debug.LogWarning("[SaveSystem] Earthquake simulation save data exists, but no EarthquakeSimulationSystem was found.");
        }

        if (data.fireSimulationData != null)
        {
            if (WeatherFireSystem.Instance != null)
                WeatherFireSystem.Instance.LoadState(data.fireSimulationData);
            else {}
                //Debug.LogWarning("[SaveSystem] Fire save data exists, but no WeatherFireSystem was found.");
        }

        if (data.tsunamiSimulationData != null)
        {
            if (TsunamiSimulationSystem.Instance != null)
                TsunamiSimulationSystem.Instance.LoadState(data.tsunamiSimulationData);
            else {}
                //Debug.LogWarning("[SaveSystem] Tsunami save data exists, but no TsunamiSimulationSystem was found.");
        }
    }

    private void LoadJobsSection(JobsSectionSaveData data)
    {
        if (data == null)
            return;

        PlayerDiscoveryManager.Instance?.LoadState(data.playerDiscoveryData);
        PlayerSurveyManager.Instance?.LoadState(data.playerSurveyData);
        PlayerGatheringManager.Instance?.LoadState(data.playerGatheringData);
        PlayerClearingManager.Instance?.LoadState(data.playerClearingData);
        PlayerCraftingManager.Instance?.LoadState(data.playerCraftingData);

        PlayerProductionSaveLoad.LoadState(data.playerProductionData);
        PlayerShelterSaveLoad.LoadState(data.playerShelterData);
        PlayerStorageSaveLoad.LoadState(data.playerStorageData);

        PlayerReligionBuildingSaveLoad.LoadState(data.playerReligionBuildingsData);
    }

    private void contextPopulationStatSafe(PlayerPopulationStatisticSaveData data)
    {
        if (data != null && playerPopulationStatistic != null)
            playerPopulationStatistic.LoadState(data);
    }

    private bool HasAnyDirtySections()
    {
        foreach (ISaveSection section in _sections.Values)
        {
            if (section != null && section.IsDirty)
                return true;
        }

        return false;
    }

    private void ClearAllSectionDirtyFlags()
    {
        foreach (ISaveSection section in _sections.Values)
        {
            section?.ClearDirty();
        }
    }

    private SaveSnapshot CreateWorkingSnapshotFromCache()
    {
        if (!_hasCachedSnapshot || _cachedSnapshot == null)
            return new SaveSnapshot();

        return new SaveSnapshot
        {
            meta = CloneMeta(_cachedSnapshot.meta),

            tiles = _cachedSnapshot.tiles != null
                ? new List<TileSaveData>(_cachedSnapshot.tiles)
                : new List<TileSaveData>(),

            buildings = _cachedSnapshot.buildings != null
                ? new List<BuildingTileSaveData>(_cachedSnapshot.buildings)
                : new List<BuildingTileSaveData>(),

            constructions = _cachedSnapshot.constructions != null
                ? new List<ConstructionTileSaveData>(_cachedSnapshot.constructions)
                : new List<ConstructionTileSaveData>(),

            coreSystems = _cachedSnapshot.coreSystems,
            knowledge = _cachedSnapshot.knowledge,
            population = _cachedSnapshot.population,
            worldSim = _cachedSnapshot.worldSim,
            jobs = _cachedSnapshot.jobs
        };
    }

    private void CommitSnapshotToCache(SaveSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        _cachedSnapshot = new SaveSnapshot
        {
            meta = CloneMeta(snapshot.meta),

            tiles = snapshot.tiles != null
                ? new List<TileSaveData>(snapshot.tiles)
                : new List<TileSaveData>(),

            buildings = snapshot.buildings != null
                ? new List<BuildingTileSaveData>(snapshot.buildings)
                : new List<BuildingTileSaveData>(),

            constructions = snapshot.constructions != null
                ? new List<ConstructionTileSaveData>(snapshot.constructions)
                : new List<ConstructionTileSaveData>(),

            coreSystems = snapshot.coreSystems,
            knowledge = snapshot.knowledge,
            population = snapshot.population,
            worldSim = snapshot.worldSim,
            jobs = snapshot.jobs,
            notifications = snapshot.notifications
        };

        _hasCachedSnapshot = true;
        ClearAllSectionDirtyFlags();
    }

    private static EnvironmentSaveMeta CloneMeta(EnvironmentSaveMeta meta)
    {
        if (meta == null)
            return null;

        return new EnvironmentSaveMeta
        {
            isChunkedSave = meta.isChunkedSave,
            version = meta.version,
            tileChunkCount = meta.tileChunkCount,
            hasBuildings = meta.hasBuildings,
            hasConstruction = meta.hasConstruction,
            hasCoreSystems = meta.hasCoreSystems,
            hasKnowledge = meta.hasKnowledge,
            hasPopulation = meta.hasPopulation,
            hasWorldSim = meta.hasWorldSim,
            hasJobs = meta.hasJobs,
            hasNotifications = meta.hasNotifications
        };
    }

    private EnvironmentSaveMeta BuildMetaForSnapshot(SaveSnapshot snapshot)
    {
        return new EnvironmentSaveMeta
        {
            isChunkedSave = true,
            version = 4,
            tileChunkCount = GetTileChunkCount(snapshot.tiles != null ? snapshot.tiles.Count : 0),
            hasBuildings = snapshot.buildings != null && snapshot.buildings.Count > 0,
            hasConstruction = snapshot.constructions != null && snapshot.constructions.Count > 0,
            hasCoreSystems = snapshot.coreSystems != null,
            hasKnowledge = snapshot.knowledge != null,
            hasPopulation = snapshot.population != null,
            hasWorldSim = snapshot.worldSim != null,
            hasJobs = snapshot.jobs != null,
            hasNotifications = snapshot.notifications != null
        };
    }

    private void SeedSnapshotCacheFromLoadedData(
        EnvironmentSaveMeta meta,
        List<TileSaveData> loadedTiles,
        BuildingSectionSaveData buildingSection,
        ConstructionSectionSaveData constructionSection,
        CoreSystemsSectionSaveData core,
        KnowledgeSectionSaveData knowledge,
        PopulationSectionSaveData population,
        WorldSimSectionSaveData sim,
        JobsSectionSaveData jobs)
    {
        _cachedSnapshot = new SaveSnapshot
        {
            meta = CloneMeta(meta),
            tiles = loadedTiles != null ? new List<TileSaveData>(loadedTiles) : new List<TileSaveData>(),
            buildings = buildingSection != null && buildingSection.buildings != null
                ? new List<BuildingTileSaveData>(buildingSection.buildings)
                : new List<BuildingTileSaveData>(),
            constructions = constructionSection != null && constructionSection.constructionTiles != null
                ? new List<ConstructionTileSaveData>(constructionSection.constructionTiles)
                : new List<ConstructionTileSaveData>(),
            coreSystems = core,
            knowledge = knowledge,
            population = population,
            worldSim = sim,
            jobs = jobs
        };

        _hasCachedSnapshot = true;
        ClearAllSectionDirtyFlags();
    }

    private static void RestoreVolcanoState(TileScript tile, EnvironmentRuntimeSaveData envData)
    {
        if (tile == null || envData == null || envData.volcanoData == null)
            return;

        GameObject spawnedInstance = tile.GetSpawnedInstance();
        if (spawnedInstance == null)
            return;

        VolcanoTileState volcano = spawnedInstance.GetComponentInChildren<VolcanoTileState>(true);
        if (volcano == null)
            volcano = spawnedInstance.GetComponentInParent<VolcanoTileState>(true);

        if (volcano == null)
        {
            //Debug.LogWarning(
                //$"[SaveSystem] Volcano data existed for tile '{tile.name}', " +
                //$"but no VolcanoTileState was found on restored environment '{spawnedInstance.name}'.");
            return;
        }

        volcano.Bind(tile);
        volcano.ApplyRuntimeSaveData(envData.volcanoData);
    }
}
