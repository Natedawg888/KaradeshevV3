using UnityEngine;

public interface IBuildingTornadoEffectHandler
{
    void ApplyTornadoEffect(
        BuildingControl building,
        float casualtyMultiplier,
        int finalDamage,
        int hitNumberThisInterval,
        int totalHitsThisInterval,
        float maxStormIntensity01,
        int maxLifetimeRemaining,
        int impactedCellCount,
        bool debugLogging);
}