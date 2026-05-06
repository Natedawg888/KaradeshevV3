using UnityEngine;

/// Base class for all technology effect ScriptableObjects (Environment, Buildings, Civ, World, Resources…).
/// Concrete effect SOs (e.g., EnvironmentTechEffectSO) should inherit from this.
public abstract class TechnologyEffectSO : ScriptableObject
{
    [Header("Identity (must match Technology.techID that owns this effect)")]
    public string techID;
}
