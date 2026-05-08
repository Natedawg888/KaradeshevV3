using UnityEngine;

/// <summary>
/// Attach to a building to repel animals from raiding nearby storage tiles.
/// The radius (in tiles) is passed to the simulation each turn so animals avoid
/// food-seeking movement into the covered area.
/// </summary>
public class AnimalRepeller : MonoBehaviour
{
    [Tooltip("How many tiles in each direction from this building to repel animal food-seeking.")]
    [Min(1)] public int repelRadiusTiles = 2;

    private void OnEnable()  => AnimalRepellerRegistry.Register(this);
    private void OnDisable() => AnimalRepellerRegistry.Unregister(this);
}
