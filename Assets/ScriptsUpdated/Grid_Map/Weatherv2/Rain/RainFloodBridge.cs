using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RainFloodBridge : MonoBehaviour
{
    [Header("References")]
    public FloodSimulationSystem floodSimulation;
    public RainSimulationSystem rainSimulationSystem;

    [Header("Behaviour")]
    public bool enableBridge = true;

    [Tooltip("If true, this bridge feeds rain into the flood system on end turn.")]
    public bool subscribeToEndTurn = true;

    [Tooltip("If true, after feeding rain into the flood accumulator, this also advances the flood simulation.")]
    public bool advanceFloodAfterRainInput = true;

    [Tooltip("Turn this off if another manager already calls FloodSimulationSystem.AdvanceFloodOneTurn().")]
    public bool preventDoubleFloodAdvance = true;

    [Header("Rain Input")]
    [Range(0f, 3f)]
    public float rainInputMultiplier = 1f;

    [Range(0f, 1f)]
    public float minRainIntensityToFeedFlood = 0.05f;

    [Tooltip("0 or lower means unlimited. Useful if you get massive rain maps and want to cap work per turn.")]
    public int maxRainCellsFedPerTurn = 0;

    [Header("Flood Chance By Rain Intensity")]
    public bool useRainFloodChance = true;

    [Tooltip("If true, rain only rolls for flooding if the flood system says the cell can contribute.")]
    public bool onlyRollNearFloodSources = true;

    [Range(0f, 1f)]
    public float lightRainFloodChance = 0.08f;

    [Range(0f, 1f)]
    public float normalRainFloodChance = 0.35f;

    [Range(0f, 1f)]
    public float heavyRainFloodChance = 0.85f;

    [Tooltip("Extra chance if the cell is already flooded.")]
    [Range(0f, 1f)]
    public float existingFloodChanceBonus = 0.25f;

    [Tooltip("Extra chance if the rain is directly on a river/lake/ocean/beach source cell.")]
    [Range(0f, 1f)]
    public float sourceCellChanceBonus = 0.15f;

    [Header("Flood Amount By Rain Intensity")]
    [Range(0f, 3f)]
    public float lightRainWaterMultiplier = 0.35f;

    [Range(0f, 3f)]
    public float normalRainWaterMultiplier = 1f;

    [Range(0f, 3f)]
    public float heavyRainWaterMultiplier = 1.75f;

    [Tooltip("Optional extra burst chance for heavy rain. This makes some heavy-rain turns cause stronger flooding.")]
    [Range(0f, 1f)]
    public float heavyRainBurstChance = 0.15f;

    [Range(1f, 4f)]
    public float heavyRainBurstMultiplier = 1.5f;

    [Header("Debug")]
    public bool debugLogging = false;
    public bool debugSkippedSourceDetails = true;

    [Min(0)]
    public int maxSkippedSourceDetailLogsPerTurn = 8;

    public bool debugFedRainCells = false;

    private readonly List<TileCoord> rainCellScratch = new List<TileCoord>();

    private bool subscribedToTurnSystem;
    private int lastFloodAdvanceTurn = int.MinValue;

    private void Reset()
    {
        TryAutoAssignReferences();
    }

    private void Awake()
    {
        TryAutoAssignReferences();
    }

    private void Start()
    {
        TryAutoAssignReferences();
    }

    private void OnEnable()
    {
        TryAutoAssignReferences();

        if (subscribeToEndTurn)
            SubscribeToTurnSystem();
    }

    private void OnDisable()
    {
        UnsubscribeFromTurnSystem();
    }

    private void TryAutoAssignReferences()
    {
        if (floodSimulation == null)
            floodSimulation = FindFirstObjectByType<FloodSimulationSystem>();

        if (rainSimulationSystem == null)
            rainSimulationSystem = RainSimulationSystem.Instance;

        if (rainSimulationSystem == null)
            rainSimulationSystem = FindFirstObjectByType<RainSimulationSystem>();
    }

    private void SubscribeToTurnSystem()
    {
        if (subscribedToTurnSystem)
            return;

        TurnSystem.SubscribeToEndOfTurn(HandleEndOfTurn);
        subscribedToTurnSystem = true;
    }

    private void UnsubscribeFromTurnSystem()
    {
        if (!subscribedToTurnSystem)
            return;

        TurnSystem.UnsubscribeFromEndOfTurn(HandleEndOfTurn);
        subscribedToTurnSystem = false;
    }

    private void HandleEndOfTurn()
    {
        ProcessRainFloodingEndTurn();

        if (!advanceFloodAfterRainInput || floodSimulation == null)
            return;

        int currentTurn = TurnSystem.GetCurrentTurn();

        if (preventDoubleFloodAdvance && lastFloodAdvanceTurn == currentTurn)
            return;

        lastFloodAdvanceTurn = currentTurn;
        floodSimulation.AdvanceFloodOneTurn(currentTurn);
    }

    public void ProcessRainFloodingEndTurn()
    {
        if (!enableBridge)
            return;

        if (floodSimulation == null)
        {
            if (debugLogging) {}
                //Debug.LogWarning("[RainFloodBridge] No FloodSimulationSystem found.");

            return;
        }

        if (rainSimulationSystem == null)
        {
            if (debugLogging) {}
                //Debug.LogWarning("[RainFloodBridge] No RainSimulationSystem found.");

            return;
        }

        if (!rainSimulationSystem.TryInitializeGrid())
        {
            if (debugLogging) {}
                //Debug.LogWarning("[RainFloodBridge] Rain grid is not initialized yet.");

            return;
        }

        rainCellScratch.Clear();

        if (!rainSimulationSystem.CopyActiveRainCells(rainCellScratch))
        {
            if (debugLogging) {}
                //Debug.Log("[RainFloodBridge] No active rain cells this turn.");

            return;
        }

        int fedCount = 0;
        int skippedBySourceRules = 0;
        int skippedByChance = 0;
        int skippedByLowIntensity = 0;
        int skippedByZeroInput = 0;

        int sourceDetailLogs = 0;
        int limit = maxRainCellsFedPerTurn <= 0 ? int.MaxValue : maxRainCellsFedPerTurn;

        for (int i = 0; i < rainCellScratch.Count && fedCount < limit; i++)
        {
            TileCoord coord = rainCellScratch[i];

            float intensity01 = rainSimulationSystem.GetRainIntensity01AtCell(coord);

            RainSimulationSystem.RainIntensityLevel level =
                rainSimulationSystem.GetRainIntensityLevelAtCell(coord);

            if (intensity01 < minRainIntensityToFeedFlood)
            {
                skippedByLowIntensity++;
                continue;
            }

            if (onlyRollNearFloodSources &&
                !floodSimulation.CanRainContributeToFloodingAtCell(coord))
            {
                skippedBySourceRules++;

                if (debugLogging &&
                    debugSkippedSourceDetails &&
                    sourceDetailLogs < maxSkippedSourceDetailLogsPerTurn)
                {
                    string reason = floodSimulation.GetRainFloodContributionDebugReason(coord);

                    //Debug.Log(
                        //$"[RainFloodBridge] Skipped rain flood source cell. " +
                        //$"Coord=({coord.x},{coord.y}), " +
                        //$"RainLevel={level}, " +
                        //$"RainIntensity={intensity01:0.00}. " +
                        //$"Reason: {reason}");

                    sourceDetailLogs++;
                }

                continue;
            }

            float chance01 = GetFloodChance01(coord, level, intensity01);

            if (useRainFloodChance && Random.value > chance01)
            {
                skippedByChance++;
                continue;
            }

            float rainInput01 = GetRainInput01(level, intensity01);

            if (rainInput01 <= 0f)
            {
                skippedByZeroInput++;
                continue;
            }

            floodSimulation.AddRainfallAtCell(coord, rainInput01);
            fedCount++;

            if (debugLogging && debugFedRainCells)
            {
                //Debug.Log(
                    //$"[RainFloodBridge] Fed rain flood cell. " +
                    //$"Coord=({coord.x},{coord.y}), " +
                    //$"RainLevel={level}, " +
                    //$"RainIntensity={intensity01:0.00}, " +
                    //$"Chance={chance01:0.00}, " +
                    //$"Input={rainInput01:0.00}");
            }
        }

        if (debugLogging)
        {
            //Debug.Log(
                //$"[RainFloodBridge] Rain flood input. " +
                //$"ActiveRainCells={rainCellScratch.Count}, " +
                //$"Fed={fedCount}, " +
                //$"SkippedSource={skippedBySourceRules}, " +
                //$"SkippedChance={skippedByChance}, " +
                //$"SkippedLowIntensity={skippedByLowIntensity}, " +
                //$"SkippedZeroInput={skippedByZeroInput}, " +
                //$"CachedFloodSources={floodSimulation.DebugValidFloodSourceCellCount}, " +
                //$"CachedBlockedCells={floodSimulation.DebugBlockedFloodCellCount}");
        }
    }

    private float GetFloodChance01(
        TileCoord coord,
        RainSimulationSystem.RainIntensityLevel level,
        float intensity01)
    {
        float baseChance;

        switch (level)
        {
            case RainSimulationSystem.RainIntensityLevel.Light:
                baseChance = lightRainFloodChance;
                break;

            case RainSimulationSystem.RainIntensityLevel.Heavy:
                baseChance = heavyRainFloodChance;
                break;

            case RainSimulationSystem.RainIntensityLevel.Normal:
                baseChance = normalRainFloodChance;
                break;

            default:
                baseChance = 0f;
                break;
        }

        // Smoothly nudge chance upward inside each level.
        float intensityBonus = Mathf.Clamp01(intensity01) * 0.15f;
        baseChance += intensityBonus;

        if (floodSimulation != null)
        {
            if (floodSimulation.IsFlooded(coord))
                baseChance += existingFloodChanceBonus;

            if (floodSimulation.IsValidFloodSourceCell(coord))
                baseChance += sourceCellChanceBonus;
        }

        return Mathf.Clamp01(baseChance);
    }

    private float GetRainInput01(
        RainSimulationSystem.RainIntensityLevel level,
        float intensity01)
    {
        float multiplier;

        switch (level)
        {
            case RainSimulationSystem.RainIntensityLevel.Light:
                multiplier = lightRainWaterMultiplier;
                break;

            case RainSimulationSystem.RainIntensityLevel.Heavy:
                multiplier = heavyRainWaterMultiplier;

                if (Random.value <= heavyRainBurstChance)
                    multiplier *= heavyRainBurstMultiplier;

                break;

            case RainSimulationSystem.RainIntensityLevel.Normal:
                multiplier = normalRainWaterMultiplier;
                break;

            default:
                multiplier = 0f;
                break;
        }

        return Mathf.Clamp01(intensity01 * rainInputMultiplier * multiplier);
    }

    public void AddRainAtCell(TileCoord coord, float rain01)
    {
        if (!enableBridge)
            return;

        if (floodSimulation == null)
            return;

        floodSimulation.AddRainfallAtCell(coord, Mathf.Clamp01(rain01));
    }

    public void AddRainAtCells(IEnumerable<TileCoord> coords, float rain01)
    {
        if (!enableBridge || coords == null)
            return;

        if (floodSimulation == null)
            return;

        float clampedRain = Mathf.Clamp01(rain01);

        foreach (TileCoord coord in coords)
            floodSimulation.AddRainfallAtCell(coord, clampedRain);
    }

    [ContextMenu("Debug/Process Rain Flooding End Turn")]
    private void ContextProcessRainFloodingEndTurn()
    {
        ProcessRainFloodingEndTurn();
    }

    [ContextMenu("Debug/Process Rain And Advance Flood")]
    private void ContextProcessRainAndAdvanceFlood()
    {
        ProcessRainFloodingEndTurn();

        if (floodSimulation != null)
            floodSimulation.AdvanceFloodOneTurn(TurnSystem.GetCurrentTurn());
    }
}
