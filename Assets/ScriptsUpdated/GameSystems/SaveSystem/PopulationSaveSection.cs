using System.Collections;

public sealed class PopulationSaveSection : SaveSectionBase
{
    public override string Key => SaveSectionKeys.Population;

    public override IEnumerator CaptureInto(
        SaveSnapshot snapshot,
        SaveCaptureContext context,
        int objectsPerFrame)
    {
        snapshot.population = new PopulationSectionSaveData
        {
            playersPopulationData =
                PlayersPopulationManager.Instance != null ? PlayersPopulationManager.Instance.SaveState() : null,

            playerFamilySimulationData =
                PlayerFamilySimulationManager.Instance != null ? PlayerFamilySimulationManager.Instance.SaveState() : null,

            playerPopulationStatisticData =
                context.PopulationStatistic != null ? context.PopulationStatistic.SaveState() : null,

            playerDiseaseData = DiseaseManager.Instance != null
                ? DiseaseManager.Instance.CaptureSaveData()
                : null
        };

        ClearDirty();
        yield break;
    }
}