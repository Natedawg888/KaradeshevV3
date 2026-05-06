using UnityEngine;

[CreateAssetMenu(menuName = "World/Environment Neighbour Filter", fileName = "EnvNeighbourFilter_")]
public class EnvironmentNeighbourFilterSO : ScriptableObject
{
    [Tooltip("If non-empty, must see at least one neighbor with one of these types (spawn-order safe: only enforced when neighbours exist).")]
    public EnvironmentType[] allowedNeighborEnvironmentTypes;

    [Tooltip("If non-empty, fails if any neighbor has one of these types.")]
    public EnvironmentType[] disallowedNeighborEnvironmentTypes;

    [Header("Climate Requirements (optional)")]
    [Tooltip("If enabled, this variant is only eligible when tileTemp is within [minTempC..maxTempC].")]
    public bool useTemperatureRange = false;
    public float minTempC = -5f;
    public float maxTempC = 25f;

    [Tooltip("If enabled, this variant is only eligible when tileHum is within [minHumidity..maxHumidity].")]
    public bool useHumidityRange = false;

    [Range(0f, 1f)] public float minHumidity = 0f;
    [Range(0f, 1f)] public float maxHumidity = 1f;

    public int GetSpecificityScore()
    {
        int a = allowedNeighborEnvironmentTypes != null ? allowedNeighborEnvironmentTypes.Length : 0;
        int d = disallowedNeighborEnvironmentTypes != null ? disallowedNeighborEnvironmentTypes.Length : 0;

        int score = a + d;

        // Count climate constraints as “more specific”
        if (useTemperatureRange) score += 2;
        if (useHumidityRange) score += 2;

        return score;
    }
}
