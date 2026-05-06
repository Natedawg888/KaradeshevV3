public enum PathogenCauseType
{
    Virus = 0,
    Bacteria = 1,
    Parasite = 2,
    Fungus = 3,
    Toxin = 4,
    Environmental = 5,
    WoundInfection = 6
}

public enum DiseaseSpreadType
{
    None = 0,
    Contact = 1,
    Waterborne = 2,
    Foodborne = 3,
    AnimalContact = 4,
    Wound = 5,
    Environmental = 6
}

public enum DiseaseSeverity
{
    Minor = 0,
    Moderate = 1,
    Severe = 2,
    Deadly = 3
}

public enum DiseaseTargetType
{
    Individual = 0,
    Group = 1,
    Settlement = 2,
    PopulationPool = 3,
    UnitGroup = 4
}

public enum DiseaseSourceType
{
    Unknown = 0,

    AnimalBite = 10,
    AnimalBloodContact = 11,
    HuntingWound = 12,
    DiseasedAnimal = 13,
    Carcass = 14,

    ContaminatedFood = 20,
    SpoiledFood = 21,

    FreshWater = 30,
    MixedWater = 31,
    ContaminatedWater = 32,
    FloodWater = 33,

    UnsafeConsumedResource = 40,

    VolcanicAsh = 50,
    AshCloud = 51,
    AcidRainAshExposure = 52,

    ExtremeCold = 60,
    ExtremeHeat = 61,

    BuildingCrowding = 80,
    PoorShelterHygiene = 81,
    CraftingBuildingExposure = 82,
    ProductionBuildingExposure = 83,
    WorkshopContamination = 84
}

public enum InfectionStage
{
    Incubating = 0,
    Active = 1,
    Recovering = 2
}