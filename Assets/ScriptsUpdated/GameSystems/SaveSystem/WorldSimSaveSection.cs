using System.Collections;
using UnityEngine;

public sealed class WorldSimSaveSection : SaveSectionBase
{
    public override string Key => SaveSectionKeys.WorldSim;

    public override IEnumerator CaptureInto(
        SaveSnapshot snapshot,
        SaveCaptureContext context,
        int objectsPerFrame)
    {
        float t0 = Time.realtimeSinceStartup;
        AnimalSimulationSaveData animalData =
            context.AnimalController != null ? context.AnimalController.SaveState() : null;
        //Debug.Log($"[WorldSimSaveSection] animal save: {Time.realtimeSinceStartup - t0:0.000}s");

        float t1 = Time.realtimeSinceStartup;
        PlayerUnitsSaveData unitsData = PlayerUnitSaveLoad.SaveState();
        //Debug.Log($"[WorldSimSaveSection] units save: {Time.realtimeSinceStartup - t1:0.000}s");

        float t2 = Time.realtimeSinceStartup;
        PlayerTrainingSaveData trainingData =
            PlayerTrainingManager.Instance != null ? PlayerTrainingManager.Instance.SaveState() : null;
        //Debug.Log($"[WorldSimSaveSection] training save: {Time.realtimeSinceStartup - t2:0.000}s");

        float t3 = Time.realtimeSinceStartup;
        LavaOverlaySaveData lavaData =
            LavaOverlayManager.Instance != null ? LavaOverlayManager.Instance.SaveState() : null;
        //Debug.Log($"[WorldSimSaveSection] lava overlay save: {Time.realtimeSinceStartup - t3:0.000}s");

        float t4 = Time.realtimeSinceStartup;
        FloodSimulationSystem floodSystem = Object.FindObjectOfType<FloodSimulationSystem>(true);
        FloodSimulationSaveData floodData =
            floodSystem != null ? floodSystem.SaveState() : null;
        //Debug.Log($"[WorldSimSaveSection] flood simulation save: {Time.realtimeSinceStartup - t4:0.000}s");

        float t5 = Time.realtimeSinceStartup;
        EarthquakeFaultLineGenerator faultLineGenerator =
            Object.FindObjectOfType<EarthquakeFaultLineGenerator>(true);

        EarthquakeFaultLineSaveData earthquakeFaultLineData =
            faultLineGenerator != null ? faultLineGenerator.SaveState() : null;

        //Debug.Log($"[WorldSimSaveSection] earthquake fault line save: {Time.realtimeSinceStartup - t5:0.000}s");

        float t6 = Time.realtimeSinceStartup;
        EarthquakeSimulationSystem earthquakeSystem =
            Object.FindObjectOfType<EarthquakeSimulationSystem>(true);

        EarthquakeSimulationSaveData earthquakeSimulationData =
            earthquakeSystem != null ? earthquakeSystem.SaveState() : null;

        //Debug.Log($"[WorldSimSaveSection] earthquake simulation save: {Time.realtimeSinceStartup - t6:0.000}s");

        float t7 = Time.realtimeSinceStartup;
        FireSimulationSaveData fireSimulationData =
            WeatherFireSystem.Instance != null ? WeatherFireSystem.Instance.SaveState() : null;

        //Debug.Log($"[WorldSimSaveSection] fire simulation save: {Time.realtimeSinceStartup - t7:0.000}s");

        float t8 = Time.realtimeSinceStartup;
        TsunamiSimulationSaveData tsunamiSimulationData =
            TsunamiSimulationSystem.Instance != null ? TsunamiSimulationSystem.Instance.SaveState() : null;

        //Debug.Log($"[WorldSimSaveSection] tsunami simulation save: {Time.realtimeSinceStartup - t8:0.000}s");

        float t9 = Time.realtimeSinceStartup;
        VolcanoManagerSaveData volcanoManagerData =
            VolcanoManager.Instance != null ? VolcanoManager.Instance.SaveState() : null;

        //Debug.Log($"[WorldSimSaveSection] volcano manager save: {Time.realtimeSinceStartup - t9:0.000}s");

        snapshot.worldSim = new WorldSimSectionSaveData
        {
            animalSimulationData = animalData,
            playerUnitsData = unitsData,
            playerTrainingData = trainingData,

            lavaOverlayData = lavaData,
            floodSimulationData = floodData,

            earthquakeFaultLineData = earthquakeFaultLineData,
            earthquakeSimulationData = earthquakeSimulationData,

            fireSimulationData = fireSimulationData,

            tsunamiSimulationData = tsunamiSimulationData,

            volcanoManagerData = volcanoManagerData
        };

        ClearDirty();
        yield break;
    }
}
