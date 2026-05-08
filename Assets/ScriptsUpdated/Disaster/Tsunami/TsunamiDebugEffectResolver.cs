using UnityEngine;

public class TsunamiDebugEffectResolver : MonoBehaviour
{
    [Header("References")]
    public TsunamiSimulationSystem tsunamiSimulationSystem;

    [Header("Debug")]
    public bool logAdvancedCells = true;
    public bool logStartedEnded = true;

    private void OnEnable()
    {
        ResolveReferences();

        if (tsunamiSimulationSystem == null)
            return;

        tsunamiSimulationSystem.OnTsunamiStarted += HandleTsunamiStarted;
        tsunamiSimulationSystem.OnTsunamiAdvanced += HandleTsunamiAdvanced;
        tsunamiSimulationSystem.OnTsunamiEnded += HandleTsunamiEnded;
    }

    private void OnDisable()
    {
        if (tsunamiSimulationSystem == null)
            return;

        tsunamiSimulationSystem.OnTsunamiStarted -= HandleTsunamiStarted;
        tsunamiSimulationSystem.OnTsunamiAdvanced -= HandleTsunamiAdvanced;
        tsunamiSimulationSystem.OnTsunamiEnded -= HandleTsunamiEnded;
    }

    private void ResolveReferences()
    {
        if (tsunamiSimulationSystem == null)
            tsunamiSimulationSystem = TsunamiSimulationSystem.Instance;

        if (tsunamiSimulationSystem == null)
            tsunamiSimulationSystem = FindObjectOfType<TsunamiSimulationSystem>();
    }

    private void HandleTsunamiStarted(TsunamiStartedEventData data)
    {
        if (!logStartedEnded || data == null)
            return;

        //Debug.Log(
            //$"[TsunamiDebugEffectResolver] Tsunami started. " +
            //$"id={data.tsunamiId} direction={data.directionKind} energy={data.startEnergy:0.00} sourceCells={data.sourceCells.Count}");
    }

    private void HandleTsunamiAdvanced(TsunamiAdvancedEventData data)
    {
        if (!logAdvancedCells || data == null)
            return;

        //Debug.Log(
            //$"[TsunamiDebugEffectResolver] Tsunami advanced. " +
            //$"id={data.tsunamiId} step={data.stepCount} energy={data.energyRemaining:0.00} activeCells={data.activeCells.Count}");

        // Later:
        // - Damage buildings on data.activeCells
        // - Push/damage units on data.activeCells
        // - Damage/flee animals on data.activeCells
        // - Cause population casualties in shelters/buildings on data.activeCells
    }

    private void HandleTsunamiEnded(TsunamiEndedEventData data)
    {
        if (!logStartedEnded || data == null)
            return;

        //Debug.Log(
            //$"[TsunamiDebugEffectResolver] Tsunami ended. " +
            //$"id={data.tsunamiId} reason={data.reason} finalStep={data.finalStepCount} finalEnergy={data.finalEnergy:0.00}");
    }
}
