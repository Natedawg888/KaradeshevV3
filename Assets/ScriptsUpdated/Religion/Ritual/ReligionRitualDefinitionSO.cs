using UnityEngine;

public enum ReligionRitualKind
{
    InitialSummoning = 0,
    ResourceOffering = 1,
    PopulationSacrifice = 2,
    Banishment = 3
}

public enum RitualSpiritSelectionMode
{
    FixedSpirit = 0,
    SelectAtRuntime = 1
}

[CreateAssetMenu(menuName = "Kardashev/Religion/Ritual", fileName = "ReligionRitual")]
public class ReligionRitualDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    public string ritualID;
    public string displayName;
    public Sprite icon;
    [TextArea(2, 5)] public string description;

    [Header("Rules")]
    public BeliefSystemType beliefSystem = BeliefSystemType.Animism;
    public ReligionRitualKind ritualKind = ReligionRitualKind.ResourceOffering;
    public RitualSpiritSelectionMode spiritSelectionMode = RitualSpiritSelectionMode.FixedSpirit;

    [Tooltip("Used when spiritSelectionMode = FixedSpirit.")]
    public SpiritDefinitionSO fixedSpirit;

    [Tooltip("Optional mirror of the spirit id for easier debugging / filtering in inspector.")]
    public string spiritID;

    [Tooltip("If true, this ritual can appear in known rituals automatically if you add it to the ritual database list.")]
    public bool knownByDefault = false;

    [Tooltip("If false, the building can only complete this ritual once ever.")]
    public bool repeatable = true;

    [Header("Faith Requirement")]
    [Tooltip("Minimum civilization faith (0..1) needed to perform this ritual. 0 = no requirement.")]
    [Range(0f, 1f)] public float faithRequired = 0f;

    [Header("Turn Flow")]
    [Min(1)] public int turnsRequired = 1;
    [Min(0)] public int cooldownTurns = 0;

    [Tooltip("How many workers are reserved while the ritual is running.")]
    [Min(0)] public int workerCount = 1;

    [Tooltip("If true, offering/sacrifice rituals require the chosen spirit to already be accepted by the religion manager.")]
    public bool requiresAcceptedSpirit = true;

    [Tooltip("If true, the chosen spirit must also be affiliated with the building.")]
    public bool requiresSpiritAffiliationAtBuilding = true;

    [Header("Resource Offering Payload")]
    public ResourceDefinition resourceDefinition;
    [Min(1)] public int resourceAmount = 1;

    [Header("Population Sacrifice Payload")]
    public SpiritSacrificeSexFilter sacrificeSexFilter = SpiritSacrificeSexFilter.Any;
    public SpiritSacrificeAgeFilter sacrificeAgeFilter = SpiritSacrificeAgeFilter.Any;
    [Min(1)] public int sacrificeCount = 1;

    [Header("Ritual Light Visuals")]
    public bool useCustomRitualLight = true;
    public Color ritualLightColor = Color.white;
    [Min(0f)] public float ritualLightIntensity = 3f;
    [Min(0f)] public float ritualLightRange = 8f;

    public SpiritDefinitionSO ResolveSpirit(SpiritDefinitionSO selectedSpirit)
    {
        if (spiritSelectionMode == RitualSpiritSelectionMode.FixedSpirit)
            return fixedSpirit;

        return selectedSpirit;
    }

    public string ResolveSpiritID(SpiritDefinitionSO selectedSpirit)
    {
        SpiritDefinitionSO spirit = ResolveSpirit(selectedSpirit);
        if (spirit != null && !string.IsNullOrWhiteSpace(spirit.spiritID))
            return spirit.spiritID;

        return spiritID;
    }

    public bool MatchesBeliefSystem(BeliefSystemType system)
    {
        return beliefSystem == system;
    }

    public bool IsSummoningRitual => ritualKind == ReligionRitualKind.InitialSummoning;
    public bool IsResourceOffering => ritualKind == ReligionRitualKind.ResourceOffering;
    public bool IsPopulationSacrifice => ritualKind == ReligionRitualKind.PopulationSacrifice;
    public bool IsBanishmentRitual => ritualKind == ReligionRitualKind.Banishment;
}