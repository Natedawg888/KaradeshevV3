using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unified registry of every building in the world, regardless of owner.
///
/// WeatherGridManager and disaster systems query this instead of
/// PlayerBuildingManager so effects (fire, lava, floods, etc.) apply equally
/// to player and AI buildings.
///
/// Player buildings are bridged automatically by subscribing to
/// PlayerBuildingManager events. AI building managers call
/// RegisterAIBuilding / UnregisterAIBuilding directly.
/// </summary>
public class WorldBuildingManager : MonoBehaviour
{
    public static WorldBuildingManager Instance { get; private set; }

    public enum OwnerType { Player, AI }

    public class Record
    {
        public string instanceId;
        public Building definition;
        public BuildingType type;
        public string familyId;
        public GameObject instance;
        public Vector3 worldPos;
        public bool isStarter;
        public OwnerType ownerType;
        public string ownerId; // "player", "ai_0", "ai_1", …
    }

    [Header("Late-bound Sources")]
    [SerializeField] private PlayerBuildingManager playerBuildingManager;

    private readonly Dictionary<string, Record> _recordsById = new();
    private readonly List<Record> _allRecords = new();

    private PlayerBuildingManager _subscribedPlayerBuildingManager;

    public event Action<Record> OnBuildingPlaced;
    public event Action<Record> OnBuildingRemoved;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        ResolveLateRefs();
        RebindPlayerSubscription();
    }

    private void OnDisable()
    {
        UnbindPlayerSubscription();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ------------------------------------------------------------------
    // Bootstrap / installer API
    // ------------------------------------------------------------------

    /// <summary>
    /// Called by the player installer scene after PlayerBuildingManager loads.
    /// Bridges all existing and future player buildings into this registry.
    /// </summary>
    public void SetPlayerBuildingManager(PlayerBuildingManager mgr, bool syncExisting = true)
    {
        if (ReferenceEquals(playerBuildingManager, mgr) &&
            ReferenceEquals(_subscribedPlayerBuildingManager, mgr))
            return;

        playerBuildingManager = mgr;
        RebindPlayerSubscription();

        if (syncExisting)
            SyncFromPlayerBuildingManager();
    }

    // ------------------------------------------------------------------
    // AI building registration (called by AIBuildingManager per AI player)
    // ------------------------------------------------------------------

    public Record RegisterAIBuilding(GameObject go, Building def, string ownerId)
    {
        if (go == null || def == null)
            return null;

        // Use a composite key so multiple AI players can own buildings with the same instance ID.
        string instanceId = go.GetInstanceID().ToString() + "_" + ownerId;

        if (_recordsById.TryGetValue(instanceId, out Record existing))
            return existing;

        Record record = new Record
        {
            instanceId = instanceId,
            definition = def,
            type = def.buildingType,
            familyId = def.FamilyKey,
            instance = go,
            worldPos = go.transform.position,
            ownerType = OwnerType.AI,
            ownerId = ownerId
        };

        AddRecord(record);
        return record;
    }

    public void UnregisterAIBuilding(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId))
            return;

        if (_recordsById.TryGetValue(instanceId, out Record record))
            RemoveRecord(record);
    }

    // ------------------------------------------------------------------
    // Query API
    // ------------------------------------------------------------------

    public IReadOnlyList<Record> GetAll() => _allRecords;

    public bool TryGetById(string instanceId, out Record record)
    {
        record = null;
        return !string.IsNullOrEmpty(instanceId) && _recordsById.TryGetValue(instanceId, out record);
    }

    // ------------------------------------------------------------------
    // Internal helpers
    // ------------------------------------------------------------------

    private void ResolveLateRefs()
    {
        if (playerBuildingManager == null)
            playerBuildingManager = PlayerBuildingManager.Instance;
    }

    private void SyncFromPlayerBuildingManager()
    {
        if (playerBuildingManager == null)
            return;

        IReadOnlyList<PlayerBuildingManager.Record> existing = playerBuildingManager.GetAll();
        if (existing == null)
            return;

        for (int i = 0; i < existing.Count; i++)
            HandlePlayerBuildingPlaced(existing[i]);
    }

    private void RebindPlayerSubscription()
    {
        if (_subscribedPlayerBuildingManager == playerBuildingManager)
            return;

        UnbindPlayerSubscription();
        _subscribedPlayerBuildingManager = playerBuildingManager;

        if (_subscribedPlayerBuildingManager != null)
        {
            _subscribedPlayerBuildingManager.OnBuildingPlaced += HandlePlayerBuildingPlaced;
            _subscribedPlayerBuildingManager.OnBuildingRemoved += HandlePlayerBuildingRemoved;
        }
    }

    private void UnbindPlayerSubscription()
    {
        if (_subscribedPlayerBuildingManager == null)
            return;

        _subscribedPlayerBuildingManager.OnBuildingPlaced -= HandlePlayerBuildingPlaced;
        _subscribedPlayerBuildingManager.OnBuildingRemoved -= HandlePlayerBuildingRemoved;
        _subscribedPlayerBuildingManager = null;
    }

    private void HandlePlayerBuildingPlaced(PlayerBuildingManager.Record playerRecord)
    {
        if (playerRecord == null || string.IsNullOrEmpty(playerRecord.instanceId))
            return;

        if (_recordsById.ContainsKey(playerRecord.instanceId))
            return;

        Record record = new Record
        {
            instanceId = playerRecord.instanceId,
            definition = playerRecord.definition,
            type = playerRecord.type,
            familyId = playerRecord.familyId,
            instance = playerRecord.instance,
            worldPos = playerRecord.worldPos,
            isStarter = playerRecord.isStarter,
            ownerType = OwnerType.Player,
            ownerId = "player"
        };

        AddRecord(record);
    }

    private void HandlePlayerBuildingRemoved(PlayerBuildingManager.Record playerRecord)
    {
        if (playerRecord == null || string.IsNullOrEmpty(playerRecord.instanceId))
            return;

        if (_recordsById.TryGetValue(playerRecord.instanceId, out Record record))
            RemoveRecord(record);
    }

    private void AddRecord(Record record)
    {
        _recordsById[record.instanceId] = record;
        _allRecords.Add(record);
        OnBuildingPlaced?.Invoke(record);
    }

    private void RemoveRecord(Record record)
    {
        _recordsById.Remove(record.instanceId);
        _allRecords.Remove(record);
        OnBuildingRemoved?.Invoke(record);
    }
}
