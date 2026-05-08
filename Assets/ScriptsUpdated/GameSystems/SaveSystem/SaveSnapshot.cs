using System;
using System.Collections.Generic;

[Serializable]
public class SaveSnapshot
{
    public EnvironmentSaveMeta meta;

    public List<TileSaveData> tiles = new List<TileSaveData>();
    public List<BuildingTileSaveData> buildings = new List<BuildingTileSaveData>();
    public List<ConstructionTileSaveData> constructions = new List<ConstructionTileSaveData>();

    public CoreSystemsSectionSaveData coreSystems;
    public KnowledgeSectionSaveData knowledge;
    public PopulationSectionSaveData population;
    public WorldSimSectionSaveData worldSim;
    public JobsSectionSaveData jobs;
    public NotificationsSaveData notifications;
}