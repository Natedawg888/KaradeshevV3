public interface IDiseaseTarget
{
    string TargetId { get; }
    DiseaseTargetType TargetType { get; }

    int PopulationCount { get; }

    float HealthModifier { get; }

    bool CanReceiveDisease(DiseaseDefinitionSO disease);

    void ApplyDiseaseEffects(DiseaseDefinitionSO disease, IndividualDiseaseState state);

    void RecoverDisease(DiseaseDefinitionSO disease, IndividualDiseaseState state);

    void KillFromDisease(DiseaseDefinitionSO disease, IndividualDiseaseState state);
}

public static class DiseaseTargetKey
{
    public static string Build(DiseaseTargetType targetType, string targetId)
    {
        return $"{targetType}:{targetId}";
    }
}