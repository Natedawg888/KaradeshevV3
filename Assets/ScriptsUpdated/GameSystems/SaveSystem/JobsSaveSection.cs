using System.Collections;

public sealed class JobsSaveSection : SaveSectionBase
{
    public override string Key => SaveSectionKeys.Jobs;

    public override IEnumerator CaptureInto(
        SaveSnapshot snapshot,
        SaveCaptureContext context,
        int objectsPerFrame)
    {
        snapshot.jobs = new JobsSectionSaveData
        {
            playerDiscoveryData =
                PlayerDiscoveryManager.Instance != null ? PlayerDiscoveryManager.Instance.SaveState() : null,

            playerSurveyData =
                PlayerSurveyManager.Instance != null ? PlayerSurveyManager.Instance.SaveState() : null,

            playerGatheringData =
                PlayerGatheringManager.Instance != null ? PlayerGatheringManager.Instance.SaveState() : null,

            playerClearingData =
                PlayerClearingManager.Instance != null ? PlayerClearingManager.Instance.SaveState() : null,

            playerCraftingData =
                PlayerCraftingManager.Instance != null ? PlayerCraftingManager.Instance.SaveState() : null,

            playerProductionData =
                PlayerProductionSaveLoad.SaveState(),

            playerShelterData =
                PlayerShelterSaveLoad.SaveState(),

            playerStorageData =
                PlayerStorageSaveLoad.SaveState(),

            playerReligionBuildingsData =
                PlayerReligionBuildingSaveLoad.SaveState()
        };

        ClearDirty();
        yield break;
    }
}