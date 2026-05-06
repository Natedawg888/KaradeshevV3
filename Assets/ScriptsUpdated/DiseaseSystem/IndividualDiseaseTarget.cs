using UnityEngine;

public class IndividualDiseaseTarget : IDiseaseTarget
{
    public Individual Person { get; private set; }

    public string TargetId => Person != null ? Person.Id : null;

    public DiseaseTargetType TargetType => DiseaseTargetType.Individual;

    public int PopulationCount => Person != null && Person.IsAlive ? 1 : 0;

    public float HealthModifier => Person != null ? Person.Health01 : 0f;

    public IndividualDiseaseTarget(Individual person)
    {
        Person = person;
    }

    public bool CanReceiveDisease(DiseaseDefinitionSO disease)
    {
        if (Person == null)
            return false;

        if (!Person.IsAlive)
            return false;

        if (disease == null)
            return false;

        return true;
    }

    public void ApplyDiseaseEffects(DiseaseDefinitionSO disease, IndividualDiseaseState state)
    {
        if (!CanReceiveDisease(disease))
            return;

        float severity01 = state != null ? state.severity01 : 1f;

        float healthLoss = disease.GetEffectiveHealthLossPerTurn(
            Person.AggregatedAgeGroup,
            severity01);

        Person.Health01 = Mathf.Clamp01(Person.Health01 - healthLoss);
    }

    public void RecoverDisease(DiseaseDefinitionSO disease, IndividualDiseaseState state)
    {
        if (Person == null)
            return;

        if (state != null)
        {
            state.isRecovering = true;
            state.infectionStage = InfectionStage.Recovering;
        }
    }

    public void KillFromDisease(DiseaseDefinitionSO disease, IndividualDiseaseState state)
    {
        if (Person == null)
            return;

        Person.Health01 = 0f;
        Person.IsAlive = false;
    }
}