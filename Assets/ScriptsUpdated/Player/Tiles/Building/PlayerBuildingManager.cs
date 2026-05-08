using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerBuildingManager : MonoBehaviour
{
    public static PlayerBuildingManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private BuildingManager buildingManager;
    [SerializeField] private PlayerKnownBuildingsManager playerKnownBuildingsManager;

    [Serializable]
    public class Record
    {
        public string instanceId;
        public Building definition;
        public BuildingType type;
        public string familyId;
        public GameObject instance;
        public Vector3 worldPos;
        public bool isStarter;
    }

    [SerializeField] private List<Record> records = new();

    private readonly Dictionary<string, Record> byId = new();
    private readonly Dictionary<BuildingType, int> countsByType = new();
    private readonly Dictionary<string, int> countsByFamily = new();

    public event Action<Record> OnBuildingPlaced;
    public event Action<Record> OnBuildingRemoved;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            //Debug.LogWarning("Multiple PlayerBuildingManager instances; destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        RebuildLookupsFromRecords();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public IReadOnlyList<Record> GetAll() => records;

    public IReadOnlyList<Record> GetByType(BuildingType type)
    {
        List<Record> result = new List<Record>();

        for (int i = 0; i < records.Count; i++)
        {
            Record rec = records[i];
            if (rec != null && rec.type == type)
                result.Add(rec);
        }

        return result;
    }

    public int GetCountByType(BuildingType type)
    {
        return countsByType.TryGetValue(type, out int c) ? c : 0;
    }

    public int GetCountByFamily(string familyId)
    {
        if (string.IsNullOrWhiteSpace(familyId))
            return 0;

        string key = familyId.Trim();
        return countsByFamily.TryGetValue(key, out int c) ? c : 0;
    }

    public int GetCountForBuildingFamily(Building def)
    {
        if (def == null)
            return 0;

        return GetCountByFamily(def.FamilyKey);
    }

    public bool HasReachedFamilyLimit(Building def)
    {
        if (def == null)
            return false;

        if (!def.HasFamilyLimit)
            return false;

        return GetCountForBuildingFamily(def) >= def.maxCountPerFamily;
    }

    public int GetRemainingFamilyCapacity(Building def)
    {
        if (def == null)
            return 0;

        if (!def.HasFamilyLimit)
            return int.MaxValue;

        return Mathf.Max(0, def.maxCountPerFamily - GetCountForBuildingFamily(def));
    }

    public Record GetById(string instanceId)
    {
        return (!string.IsNullOrEmpty(instanceId) && byId.TryGetValue(instanceId, out Record rec))
            ? rec
            : null;
    }

    public void Register(BuildingInstance bi)
    {
        if (bi == null || bi.definition == null || string.IsNullOrEmpty(bi.instanceId))
            return;

        if (byId.ContainsKey(bi.instanceId))
            return;

        string familyKey = bi.definition.FamilyKey;

        Record rec = new Record
        {
            instanceId = bi.instanceId,
            definition = bi.definition,
            type = bi.definition.buildingType,
            familyId = familyKey,
            instance = bi.gameObject,
            worldPos = bi.transform.position,
            isStarter = bi.isStarter
        };

        records.Add(rec);
        byId[rec.instanceId] = rec;

        if (!countsByType.ContainsKey(rec.type))
            countsByType[rec.type] = 0;
        countsByType[rec.type]++;

        if (!string.IsNullOrWhiteSpace(rec.familyId))
        {
            if (!countsByFamily.ContainsKey(rec.familyId))
                countsByFamily[rec.familyId] = 0;

            countsByFamily[rec.familyId]++;
        }

        OnBuildingPlaced?.Invoke(rec);
    }

    public void Unregister(BuildingInstance bi)
    {
        if (bi == null || string.IsNullOrEmpty(bi.instanceId))
            return;

        if (!byId.TryGetValue(bi.instanceId, out Record rec))
            return;

        byId.Remove(bi.instanceId);
        records.Remove(rec);

        countsByType[rec.type] = Mathf.Max(0, GetCountByType(rec.type) - 1);

        if (!string.IsNullOrWhiteSpace(rec.familyId) && countsByFamily.ContainsKey(rec.familyId))
        {
            countsByFamily[rec.familyId] = Mathf.Max(0, countsByFamily[rec.familyId] - 1);

            if (countsByFamily[rec.familyId] == 0)
                countsByFamily.Remove(rec.familyId);
        }

        OnBuildingRemoved?.Invoke(rec);
    }

    public Record RegisterManual(GameObject go, Building def, bool isStarter = false)
    {
        if (!go || def == null)
            return null;

        BuildingInstance tag = go.GetComponent<BuildingInstance>();
        if (!tag)
            tag = go.AddComponent<BuildingInstance>();

        tag.definition = def;
        tag.isStarter = isStarter;

        Register(tag);
        return GetById(tag.instanceId);
    }

    public List<Building> GetAvailableBuildingsForTile(TileSize size, EnvironmentType env, EnvironmentTileType tile)
    {
        List<Building> result = new List<Building>();

        if (buildingManager == null)
            return result;

        List<Building> source = buildingManager.GetBuildingsForTile(size, env, tile);
        if (source == null || source.Count == 0)
            return result;

        bool hasKnownManager = playerKnownBuildingsManager != null;

        for (int i = 0; i < source.Count; i++)
        {
            Building b = source[i];
            if (b == null)
                continue;

            if (hasKnownManager && !playerKnownBuildingsManager.IsKnown(b.buildingID))
                continue;

            if (HasReachedFamilyLimit(b))
                continue;

            result.Add(b);
        }

        return result;
    }

    private void RebuildLookupsFromRecords()
    {
        byId.Clear();
        countsByType.Clear();
        countsByFamily.Clear();

        foreach (BuildingType t in Enum.GetValues(typeof(BuildingType)))
            countsByType[t] = 0;

        if (records == null)
        {
            records = new List<Record>();
            return;
        }

        for (int i = 0; i < records.Count; i++)
        {
            Record rec = records[i];
            if (rec == null || string.IsNullOrEmpty(rec.instanceId))
                continue;

            if (!byId.ContainsKey(rec.instanceId))
                byId.Add(rec.instanceId, rec);

            if (!countsByType.ContainsKey(rec.type))
                countsByType[rec.type] = 0;
            countsByType[rec.type]++;

            if (!string.IsNullOrWhiteSpace(rec.familyId))
            {
                if (!countsByFamily.ContainsKey(rec.familyId))
                    countsByFamily[rec.familyId] = 0;

                countsByFamily[rec.familyId]++;
            }
        }
    }

    public void SetBuildingManager(BuildingManager newBuildingManager)
    {
        if (newBuildingManager == null)
            return;

        buildingManager = newBuildingManager;
    }
}
