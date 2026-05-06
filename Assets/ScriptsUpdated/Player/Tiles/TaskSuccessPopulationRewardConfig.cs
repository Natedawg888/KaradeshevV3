using System;
using UnityEngine;

public enum EnvironmentTaskKind
{
    Discovery,
    Gathering
}

[CreateAssetMenu(menuName = "Kardashev/Task Success Population Reward Config",
                 fileName = "TaskSuccessPopulationRewardConfig")]
public class TaskSuccessPopulationRewardConfig : ScriptableObject
{
    [Header("Enable")]
    public bool enabled = true;

    [Header("Base Chances (percent)")]
    [Range(0f, 100f)] public float discoveryJoinChance = 8f;
    [Range(0f, 100f)] public float gatheringJoinChance = 3f;

    [Header("TileSize Chance Bonus")]
    [Tooltip("If on, chance increases with bigger tiles.")]
    public bool scaleChanceWithTileSize = true;

    [Tooltip("Example 2 means +2% chance per step above Tiny.")]
    [Range(0f, 25f)] public float chanceBonusPerTileSizeStep = 2f;

    [Header("What arrives?")]
    [Tooltip("If roll succeeds, chance it is a NEW FAMILY (otherwise a few individuals join existing families).")]
    [Range(0f, 100f)] public float newFamilyChance = 55f;

    [Header("If NEW FAMILY")]
    public Vector2Int adultsRange = new Vector2Int(2, 2);    // usually 2 adults
    public Vector2Int childrenRange = new Vector2Int(0, 2);

    [Header("If INDIVIDUALS")]
    public Vector2Int individualsRange = new Vector2Int(1, 3);

    [Header("Stats")]
    public Vector2Int adultAgeTurnsRange = new Vector2Int(70, 130);
    public Vector2Int childAgeTurnsRange = new Vector2Int(5, 40);

    [Range(0f, 1f)] public float minHealth01 = 0.65f;
    [Range(0f, 1f)] public float maxHealth01 = 1.0f;

    public float GetChance(EnvironmentTaskKind kind, TileSize tileSize)
    {
        float baseChance = (kind == EnvironmentTaskKind.Discovery) ? discoveryJoinChance : gatheringJoinChance;

        if (scaleChanceWithTileSize)
        {
            int stepAboveTiny = Mathf.Max(0, (int)tileSize - (int)TileSize.Tiny);
            baseChance += stepAboveTiny * chanceBonusPerTileSizeStep;
        }

        return Mathf.Clamp(baseChance, 0f, 100f);
    }

    public bool RollTrigger(EnvironmentTaskKind kind, TileSize tileSize)
    {
        if (!enabled) return false;
        float c = GetChance(kind, tileSize) / 100f;
        return UnityEngine.Random.value < c;
    }

    public bool RollNewFamily()
    {
        return UnityEngine.Random.value < (newFamilyChance / 100f);
    }

    public int RollAdults() => RollInt(adultsRange);
    public int RollChildren() => RollInt(childrenRange);
    public int RollIndividuals() => RollInt(individualsRange);

    private static int RollInt(Vector2Int r)
    {
        int min = Mathf.Min(r.x, r.y);
        int max = Mathf.Max(r.x, r.y);
        return UnityEngine.Random.Range(min, max + 1);
    }

    public int RollAdultAgeTurns() => RollInt(adultAgeTurnsRange);
    public int RollChildAgeTurns() => RollInt(childAgeTurnsRange);

    public float RollHealth01()
    {
        float a = Mathf.Min(minHealth01, maxHealth01);
        float b = Mathf.Max(minHealth01, maxHealth01);
        return Mathf.Lerp(a, b, UnityEngine.Random.value);
    }
}
