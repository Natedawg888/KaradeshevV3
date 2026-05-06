using System.Collections.Generic;
using UnityEngine;

public enum Stage
{
    Emergence,
    HunterGatherer,
    Agricultural,
    MetalAge,
    Antiquity,
    Feudal,
    Renaissance,
    Industrial,
    Information,
    Digital,
    Augmented,
    Type1,
}

[System.Serializable]
public class StageData
{
    public Stage stage;
    public string displayName;
    public Sprite icon;

    [TextArea]
    public string description;
}

[System.Serializable]
public class LevelData
{
    public int level;
    public int xpRequired;
    public Stage stage;
    public float secondsPerTurn = 5f;
}

public class LevelManager : MonoBehaviour
{
    [Header("Stages")]
    public List<StageData> stages = new List<StageData>();

    [Header("Levels")]
    public List<LevelData> levels = new List<LevelData>();

    public float defaultSecondsPerTurn = 10f;

    private Dictionary<int, LevelData> _levelLookup;
    private Dictionary<Stage, StageData> _stageLookup;
    private bool _cacheBuilt;

    public LevelData GetLevelData(int level)
    {
        EnsureCache();
        _levelLookup.TryGetValue(level, out LevelData data);
        return data;
    }

    public StageData GetStageData(Stage stage)
    {
        EnsureCache();
        _stageLookup.TryGetValue(stage, out StageData data);
        return data;
    }

    public Stage GetStageForLevel(int level)
    {
        LevelData ld = GetLevelData(level);
        return ld != null ? ld.stage : Stage.Emergence;
    }

    public float GetSecondsPerTurnForLevel(int level)
    {
        LevelData ld = GetLevelData(level);
        if (ld != null && ld.secondsPerTurn > 0f)
            return ld.secondsPerTurn;

        return defaultSecondsPerTurn;
    }

    private void EnsureCache()
    {
        if (_cacheBuilt)
            return;

        _cacheBuilt = true;

        _levelLookup = new Dictionary<int, LevelData>();
        _stageLookup = new Dictionary<Stage, StageData>();

        if (levels != null)
        {
            for (int i = 0; i < levels.Count; i++)
            {
                LevelData data = levels[i];
                if (data == null)
                    continue;

                if (!_levelLookup.ContainsKey(data.level))
                    _levelLookup.Add(data.level, data);
            }
        }

        if (stages != null)
        {
            for (int i = 0; i < stages.Count; i++)
            {
                StageData data = stages[i];
                if (data == null)
                    continue;

                if (!_stageLookup.ContainsKey(data.stage))
                    _stageLookup.Add(data.stage, data);
            }
        }
    }

    public void RebuildCache()
    {
        _cacheBuilt = false;
        _levelLookup = null;
        _stageLookup = null;
        EnsureCache();
    }
}