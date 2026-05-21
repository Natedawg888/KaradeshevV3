using System.Collections;
using Unity.Profiling;
using UnityEngine;

public sealed class WorldSimSaveSection : SaveSectionBase
{
    public override string Key => SaveSectionKeys.WorldSim;

    // Cached on first save — avoids FindObjectOfType on every save
    private FloodSimulationSystem        _floodSystem;
    private EarthquakeFaultLineGenerator _faultLineGenerator;
    private EarthquakeSimulationSystem   _earthquakeSystem;

    private static readonly ProfilerMarker _pmAnimal    = new ProfilerMarker("SaveSystem.Capture.WorldSim.Animal");
    private static readonly ProfilerMarker _pmUnits     = new ProfilerMarker("SaveSystem.Capture.WorldSim.Units");
    private static readonly ProfilerMarker _pmDisaster  = new ProfilerMarker("SaveSystem.Capture.WorldSim.Disaster");
    private static readonly ProfilerMarker _pmWeather   = new ProfilerMarker("SaveSystem.Capture.WorldSim.Weather");

    public override IEnumerator CaptureInto(
        SaveSnapshot snapshot,
        SaveCaptureContext context,
        int objectsPerFrame)
    {
        // --- Animal simulation (potentially slow) ---
        _pmAnimal.Begin();
        AnimalSimulationSaveData animalData =
            context.AnimalController != null ? context.AnimalController.SaveState() : null;
        _pmAnimal.End();
        yield return null;

        // --- Units & training ---
        _pmUnits.Begin();
        PlayerUnitsSaveData unitsData = PlayerUnitSaveLoad.SaveState();
        PlayerTrainingSaveData trainingData =
            PlayerTrainingManager.Instance != null ? PlayerTrainingManager.Instance.SaveState() : null;
        _pmUnits.End();
        yield return null;

        // --- Disaster systems: lava, flood, earthquake ---
        // FindObjectOfType only on first save; subsequent saves reuse cached ref
        _pmDisaster.Begin();
        LavaOverlaySaveData lavaData =
            LavaOverlayManager.Instance != null ? LavaOverlayManager.Instance.SaveState() : null;

        if (_floodSystem == null)
            _floodSystem = Object.FindObjectOfType<FloodSimulationSystem>(true);
        FloodSimulationSaveData floodData =
            _floodSystem != null ? _floodSystem.SaveState() : null;

        if (_faultLineGenerator == null)
            _faultLineGenerator = Object.FindObjectOfType<EarthquakeFaultLineGenerator>(true);
        EarthquakeFaultLineSaveData earthquakeFaultLineData =
            _faultLineGenerator != null ? _faultLineGenerator.SaveState() : null;

        if (_earthquakeSystem == null)
            _earthquakeSystem = Object.FindObjectOfType<EarthquakeSimulationSystem>(true);
        EarthquakeSimulationSaveData earthquakeSimulationData =
            _earthquakeSystem != null ? _earthquakeSystem.SaveState() : null;
        _pmDisaster.End();
        yield return null;

        // --- Fire, tsunami, volcano ---
        _pmWeather.Begin();
        FireSimulationSaveData fireSimulationData =
            WeatherFireSystem.Instance != null ? WeatherFireSystem.Instance.SaveState() : null;
        TsunamiSimulationSaveData tsunamiSimulationData =
            TsunamiSimulationSystem.Instance != null ? TsunamiSimulationSystem.Instance.SaveState() : null;
        VolcanoManagerSaveData volcanoManagerData =
            VolcanoManager.Instance != null ? VolcanoManager.Instance.SaveState() : null;
        SolarStormSaveData solarStormData =
            SolarStormSystem.Instance != null ? SolarStormSystem.Instance.SaveState() : null;
        _pmWeather.End();

        snapshot.worldSim = new WorldSimSectionSaveData
        {
            animalSimulationData    = animalData,
            playerUnitsData         = unitsData,
            playerTrainingData      = trainingData,
            lavaOverlayData         = lavaData,
            floodSimulationData     = floodData,
            earthquakeFaultLineData = earthquakeFaultLineData,
            earthquakeSimulationData = earthquakeSimulationData,
            fireSimulationData      = fireSimulationData,
            tsunamiSimulationData   = tsunamiSimulationData,
            volcanoManagerData      = volcanoManagerData,
            solarStormData          = solarStormData
        };

        ClearDirty();
    }
}
