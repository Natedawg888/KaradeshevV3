using UnityEngine;

[DisallowMultipleComponent]
public sealed class RainVisualShapeCache : MonoBehaviour
{
    public float lastCellSize = -1f;
    public float lastCoverage = -1f;
    public float lastShapeHeight = -1f;
    public RainSimulationSystem.RainIntensityLevel lastIntensityLevel = RainSimulationSystem.RainIntensityLevel.None;
}