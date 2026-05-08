using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class TsunamiFloodBridge : MonoBehaviour
{
    [Header("References")]
    public FloodSimulationSystem floodSimulation;

    [Tooltip("Drag your TsunamiSimulationSystem object here. Kept as MonoBehaviour so this bridge survives API changes.")]
    public MonoBehaviour tsunamiSimulationSystem;

    [Header("Behaviour")]
    public bool enableBridge = true;

    [Tooltip("If true, this bridge tries to poll active tsunami cells at end-turn using reflection.")]
    public bool pollTsunamiAtEndOfTurn = false;

    [Range(0f, 1f)] public float fallbackEnergy01 = 0.5f;

    [Header("Debug")]
    public bool debugLogging = false;

    private readonly List<TileCoord> tsunamiCellScratch = new List<TileCoord>();

    private void Reset()
    {
        TryAutoAssignReferences();
    }

    private void Awake()
    {
        TryAutoAssignReferences();
    }

    private void TryAutoAssignReferences()
    {
        if (floodSimulation == null)
            floodSimulation = FindFirstObjectByType<FloodSimulationSystem>();

        if (tsunamiSimulationSystem == null)
        {
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);

            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null && behaviours[i].GetType().Name == "TsunamiSimulationSystem")
                {
                    tsunamiSimulationSystem = behaviours[i];
                    break;
                }
            }
        }
    }

    public void AddFloodFromTsunamiCell(TileCoord coord, float energy01)
    {
        if (!enableBridge || floodSimulation == null)
            return;

        floodSimulation.AddTsunamiFloodWater(coord, energy01);
    }

    public void ApplyTsunamiCells(IEnumerable<TileCoord> cells, float energy01)
    {
        if (!enableBridge || floodSimulation == null || cells == null)
            return;

        foreach (TileCoord coord in cells)
            floodSimulation.AddTsunamiFloodWater(coord, energy01);
    }

    public void ApplyTsunamiCells(IEnumerable<TileCoord> cells, Func<TileCoord, float> energyProvider)
    {
        if (!enableBridge || floodSimulation == null || cells == null)
            return;

        foreach (TileCoord coord in cells)
        {
            float energy01 = energyProvider != null ? energyProvider(coord) : fallbackEnergy01;
            floodSimulation.AddTsunamiFloodWater(coord, energy01);
        }
    }

    public void ProcessTsunamiFloodingEndTurn()
    {
        if (!enableBridge || !pollTsunamiAtEndOfTurn)
            return;

        PollTsunamiSystemAndApplyFlooding();
    }

    [ContextMenu("Debug/Poll Tsunami And Apply Flooding")]
    public void PollTsunamiSystemAndApplyFlooding()
    {
        if (floodSimulation == null || tsunamiSimulationSystem == null)
            return;

        tsunamiCellScratch.Clear();

        if (!TryReadActiveTsunamiCells(tsunamiSimulationSystem, tsunamiCellScratch))
        {
            if (debugLogging) {}
                //Debug.LogWarning("[TsunamiFloodBridge] Could not find active tsunami cells on TsunamiSimulationSystem.");

            return;
        }

        for (int i = 0; i < tsunamiCellScratch.Count; i++)
        {
            TileCoord coord = tsunamiCellScratch[i];
            float energy01 = TryReadTsunamiEnergy01(tsunamiSimulationSystem, coord, fallbackEnergy01);
            floodSimulation.AddTsunamiFloodWater(coord, energy01);
        }

        if (debugLogging) {}
            //Debug.Log($"[TsunamiFloodBridge] Applied tsunami flood input to {tsunamiCellScratch.Count} cells.");
    }

    private bool TryReadActiveTsunamiCells(object tsunami, List<TileCoord> result)
    {
        string[] memberNames =
        {
            "ActiveTsunamiCells",
            "activeTsunamiCells",
            "CurrentTsunamiCells",
            "currentTsunamiCells",
            "ActiveWaveCells",
            "activeWaveCells"
        };

        for (int i = 0; i < memberNames.Length; i++)
        {
            object value = ReflectionGetMemberValue(tsunami, memberNames[i]);

            if (TryAddEnumerableCoords(value, result))
                return result.Count > 0;
        }

        return false;
    }

    private float TryReadTsunamiEnergy01(object tsunami, TileCoord coord, float fallback)
    {
        Type type = tsunami.GetType();

        string[] methodNames =
        {
            "GetEnergy01AtCell",
            "GetTsunamiEnergy01AtCell",
            "GetEnergyAtCell",
            "GetWaveEnergy01",
            "GetWaveEnergy01AtCell"
        };

        for (int i = 0; i < methodNames.Length; i++)
        {
            MethodInfo m = type.GetMethod(methodNames[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (m == null)
                continue;

            ParameterInfo[] parameters = m.GetParameters();

            try
            {
                object result = null;

                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(TileCoord))
                    result = m.Invoke(tsunami, new object[] { coord });
                else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(Vector2Int))
                    result = m.Invoke(tsunami, new object[] { new Vector2Int(coord.x, coord.y) });
                else if (parameters.Length == 2 && parameters[0].ParameterType == typeof(int) && parameters[1].ParameterType == typeof(int))
                    result = m.Invoke(tsunami, new object[] { coord.x, coord.y });

                if (result != null)
                    return Mathf.Clamp01(Convert.ToSingle(result));
            }
            catch
            {
                // ignored
            }
        }

        return Mathf.Clamp01(fallback);
    }

    private bool TryAddEnumerableCoords(object value, List<TileCoord> result)
    {
        if (value == null || result == null)
            return false;

        if (value is System.Collections.IEnumerable enumerable && !(value is string))
        {
            bool any = false;

            foreach (object item in enumerable)
            {
                if (TryObjectToTileCoord(item, out TileCoord coord))
                {
                    result.Add(coord);
                    any = true;
                }
            }

            return any;
        }

        return false;
    }

    private bool TryObjectToTileCoord(object value, out TileCoord coord)
    {
        coord = default;

        if (value == null)
            return false;

        if (value is TileCoord tileCoord)
        {
            coord = tileCoord;
            return true;
        }

        if (value is Vector2Int v2)
        {
            coord = new TileCoord(v2.x, v2.y);
            return true;
        }

        object xValue = ReflectionGetMemberValue(value, "x") ?? ReflectionGetMemberValue(value, "X");
        object yValue = ReflectionGetMemberValue(value, "y") ?? ReflectionGetMemberValue(value, "Y");

        if (xValue == null || yValue == null)
            return false;

        try
        {
            coord = new TileCoord(Convert.ToInt32(xValue), Convert.ToInt32(yValue));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private object ReflectionGetMemberValue(object target, string name)
    {
        if (target == null || string.IsNullOrWhiteSpace(name))
            return null;

        Type type = target.GetType();

        FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
            return field.GetValue(target);

        PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.CanRead)
            return property.GetValue(target);

        return null;
    }
}
