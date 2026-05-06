using System;
using UnityEngine;

[Serializable]
public class PlayerDiseaseSummary
{
    public string diseaseId;
    public string displayName;
    public string description;

    public Sprite diseaseIcon;

    public int totalAffected;

    public int childAffected;
    public int teenAffected;
    public int adultAffected;
    public int elderAffected;

    public DiseaseSeverity severity;
    public PathogenCauseType causeType;
    public DiseaseSpreadType spreadType;

    public bool contagious;

    public int mutationGeneration;
    public string mutationRomanNumeral;
    public string mutationCode4;

    public bool HasMutation =>
        mutationGeneration > 0 &&
        !string.IsNullOrWhiteSpace(mutationRomanNumeral) &&
        !string.IsNullOrWhiteSpace(mutationCode4);

    public string AgeGroupBreakdownText =>
        $"Children: {childAffected} | Teens: {teenAffected} | Adults: {adultAffected} | Elders: {elderAffected}";
}