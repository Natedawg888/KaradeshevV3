using System;
using System.Collections.Generic;
using UnityEngine;

/// One SO per technology (environment category) that defines discovery/gathering buffs.
[CreateAssetMenu(menuName = "Kardashev/Tech Effects/Environment", fileName = "EnvTechEffectSO")]
public class EnvironmentTechEffectSO : TechnologyEffectSO
{
    [Header("Environment Effects (optional)")]
    public List<EnvironmentEffect> environmentEffects = new();

    [Serializable]
    public struct EnvironmentEffect
    {
        [Tooltip("Filters. Leave empty to match ANY.")]
        public List<EnvironmentType> environmentTypes;       // e.g. Forest, Desert
        public List<EnvironmentTileType> tileTypes;          // e.g. Plains, Hills
        public List<TileSize> tileSizes;                     // optional size filter

        [Header("Unlock")]
        public bool unlockExplore;

        [Header("Discovery Buffs")]
        public float discoveryFailureDeltaPct;
        public float discoveryFailureMult;
        public int discoveryTurnsDelta;
        public float discoveryTurnsMult;

        [Tooltip("Positive = reduce required population by N. Negative = increase by N.")]
        public int discoveryRequiredPopDelta;

        [Tooltip("Multiplier for required population (e.g., 0.8 = 20% less). 0 or less = ignored.")]
        public float discoveryRequiredPopMult;

        [Tooltip("Positive = reduce failure population penalty by N. Negative = increase by N.")]
        public int discoveryPenaltyDelta;

        [Tooltip("Multiplier for failure population penalty (e.g. 0.8 = 20% less). 0 or less = ignored.")]
        public float discoveryPenaltyMult;

        [Header("Gathering Buffs")]
        public float gatheringFailureDeltaPct;
        public float gatheringFailureMult;
        public int   gatheringTurnsDelta;
        public float gatheringTurnsMult;

        [Tooltip("Positive = reduce required population by N. Negative = increase by N.")]
        public int gatheringRequiredPopDelta;

        [Tooltip("Multiplier for required population (e.g., 0.8 = 20% less). 0 or less = ignored.")]
        public float gatheringRequiredPopMult;

        [Tooltip("Positive = reduce failure population penalty by N. Negative = increase by N.")]
        public int gatheringPenaltyDelta;

        [Tooltip("Multiplier for failure population penalty (e.g. 0.8 = 20% less). 0 or less = ignored.")]
        public float gatheringPenaltyMult;

        public bool Matches(EnvironmentControl env)
        {
            bool typeOk  = environmentTypes == null || environmentTypes.Count == 0 || environmentTypes.Contains(env.environmentType);
            bool tileOk  = tileTypes == null       || tileTypes.Count == 0       || tileTypes.Contains(env.environmentTileType);
            bool sizeOk  = tileSizes == null       || tileSizes.Count == 0       || tileSizes.Contains(env.tileSize);
            return typeOk && tileOk && sizeOk;
        }
    }
}