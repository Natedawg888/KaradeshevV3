using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BuildingManager : MonoBehaviour
{
    public static BuildingManager Instance { get; private set; }

    // Keep the same serialized lists (one list per Stage)
    [SerializeField] private List<Building> stage0Buildings = new();
    [SerializeField] private List<Building> stage1Buildings = new();
    [SerializeField] private List<Building> stage2Buildings = new();
    [SerializeField] private List<Building> stage3Buildings = new();
    [SerializeField] private List<Building> stage4Buildings = new();
    [SerializeField] private List<Building> stage5Buildings = new();
    [SerializeField] private List<Building> stage6Buildings = new();
    [SerializeField] private List<Building> stage7Buildings = new();
    [SerializeField] private List<Building> stage8Buildings = new();
    [SerializeField] private List<Building> stage9Buildings = new();
    [SerializeField] private List<Building> stage10Buildings = new();

    private readonly Dictionary<string, Building> defById = new();                // buildingID -> def
    private readonly Dictionary<string, BuildingControl> controlsByInstanceId = new();

    public event Action<BuildingControl> OnBuildingControlRegistered;
    public event Action<BuildingControl> OnBuildingControlUnregistered;

    // 🔁 Key by Stage (enum), not int
    private Dictionary<Stage, List<Building>> buildingsByStage;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializeBuildingStages();
            RebuildDefinitionIndex();
        }
        else
        {
            Debug.LogError("Multiple BuildingManager instances detected!");
            Destroy(gameObject);
        }

        if (buildingsByStage == null || buildingsByStage.Count == 0)
            Debug.LogError("BuildingManager: No buildings initialized! Check initialization sequence.");
    }

    private void InitializeBuildingStages()
    {
        buildingsByStage = new Dictionary<Stage, List<Building>>
        {
            { Stage.Emergence,      stage0Buildings  },
            { Stage.HunterGatherer, stage1Buildings  },
            { Stage.Agricultural,   stage2Buildings  },
            { Stage.MetalAge,       stage3Buildings  },
            { Stage.Antiquity,      stage4Buildings  },
            { Stage.Feudal,         stage5Buildings  },
            { Stage.Renaissance,    stage6Buildings  },
            { Stage.Industrial,     stage7Buildings  },
            { Stage.Information,    stage8Buildings  },
            { Stage.Digital,        stage9Buildings  },
            { Stage.Augmented,      stage10Buildings },
            // If you will use Type1 later, add another list and map here.
        };
    }

    private void RebuildDefinitionIndex()
    {
        defById.Clear();
        foreach (var kv in buildingsByStage)
        {
            var list = kv.Value;
            if (list == null) continue;

            foreach (var b in list)
            {
                if (b == null || string.IsNullOrWhiteSpace(b.buildingID)) continue;
                // last one wins if duplicate IDs exist
                defById[b.buildingID] = b;
            }
        }
    }

    public void RegisterBuildingControl(BuildingControl control)
    {
        if (control == null) return;
        var key = control.UniqueInstanceID;
        if (string.IsNullOrWhiteSpace(key)) return;

        if (!controlsByInstanceId.ContainsKey(key))
        {
            controlsByInstanceId[key] = control;
            OnBuildingControlRegistered?.Invoke(control);
        }
    }

    public void UnregisterBuildingControl(BuildingControl control)
    {
        if (control == null) return;
        var key = control.UniqueInstanceID;
        if (string.IsNullOrWhiteSpace(key)) return;

        if (controlsByInstanceId.Remove(key))
        {
            OnBuildingControlUnregistered?.Invoke(control);
        }
    }

    // Optional add API
    public void AddBuildingToStage(Stage stage, Building building)
    {
        if (!buildingsByStage.TryGetValue(stage, out var list))
        {
            Debug.LogWarning($"Invalid stage: {stage}. Building not added.");
            return;
        }
        list.Add(building);
    }

    public List<Building> GetBuildingsForStage(Stage stage)
    {
        return buildingsByStage.TryGetValue(stage, out var list) ? list : new List<Building>();
    }

    // ---------- Filtering by tile (given an explicit stage) ----------
    public List<Building> GetBuildingsForTile(
    TileSize tileSize,
    EnvironmentType environmentType,
    EnvironmentTileType environmentTileType,
    Stage stage)
    {
        var pool   = GetBuildingsForStage(stage);
        var result = new List<Building>();

        foreach (var b in pool)
        {
            if (b == null) continue;

            if (b.requiredTileSize != tileSize) continue;
            if (b.requiredEnvironmentTypes == null || !b.requiredEnvironmentTypes.Contains(environmentType)) continue;
            if (b.requiredEnvironmentTileTypes == null || !b.requiredEnvironmentTileTypes.Contains(environmentTileType)) continue;

            result.Add(b);
        }
        return result;
    }

    // convenience that maps current player level → Stage and also filters by level via the method above
    public List<Building> GetBuildingsForTile(
        TileSize tileSize,
        EnvironmentType environmentType,
        EnvironmentTileType environmentTileType)
    {
        Stage stage = Stage.Emergence;
        var lvlMgr = FindObjectOfType<LevelManager>();
        var player = PlayerLevel.Instance ?? FindObjectOfType<PlayerLevel>();
        if (lvlMgr && player)
            stage = lvlMgr.GetStageForLevel(player.currentLevel);

        return GetBuildingsForTile(tileSize, environmentType, environmentTileType, stage);
    }
    
    public Building GetBuildingByID(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        foreach (var kv in buildingsByStage)
        {
            var list = kv.Value;
            if (list == null) continue;
            for (int i = 0; i < list.Count; i++)
            {
                var b = list[i];
                if (b != null && b.buildingID == id)
                    return b;
            }
        }
        return null;
    }
}