using System.Collections;

public sealed class KnowledgeSaveSection : SaveSectionBase
{
    public override string Key => SaveSectionKeys.Knowledge;

    public override IEnumerator CaptureInto(
        SaveSnapshot snapshot,
        SaveCaptureContext context,
        int objectsPerFrame)
    {
        snapshot.knowledge = new KnowledgeSectionSaveData
        {
            inventoryData =
                PlayerInventoryManager.Instance != null ? PlayerInventoryManager.Instance.SaveState() : null,

            knownResourcesData =
                PlayerKnownResourcesManager.Instance != null ? PlayerKnownResourcesManager.Instance.SaveState() : null,

            knownCraftingData =
                PlayerKnownCraftingManager.Instance != null ? PlayerKnownCraftingManager.Instance.SaveState() : null,

            knownProductionData =
                PlayerKnownProductionManager.Instance != null ? PlayerKnownProductionManager.Instance.SaveState() : null,

            knownBuildingsData =
                PlayerKnownBuildingsManager.Instance != null ? PlayerKnownBuildingsManager.Instance.SaveState() : null,

            knownUnitsData =
                PlayerKnownUnitsManager.Instance != null ? PlayerKnownUnitsManager.Instance.SaveState() : null,

            knownTechnologyData =
                PlayerKnownTechnologyManager.Instance != null ? PlayerKnownTechnologyManager.Instance.SaveState() : null,

            playerResearchData =
                PlayerResearchManager.Instance != null ? PlayerResearchManager.Instance.SaveState() : null,

            knownSpiritsData =
                PlayerKnownSpiritsManager.Instance != null ? PlayerKnownSpiritsManager.Instance.SaveState() : null,

            knownRitualsData =
                PlayerKnownRitualsManager.Instance != null ? PlayerKnownRitualsManager.Instance.SaveState() : null,

            playerReligionData =
                PlayerReligionManager.Instance != null ? PlayerReligionManager.Instance.SaveState() : null
        };

        ClearDirty();
        yield break;
    }
}