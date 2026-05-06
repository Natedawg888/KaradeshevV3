using System;
using UnityEngine;

[Serializable]
public class Individual
{
    public string Id;
    public Gender Gender;
    public int AgeInTurns;
    public float Health01;
    public string FamilyId;
    public bool IsAlive = true;

    public AgeGroup AggregatedAgeGroup;
    public Guid AggregatedGroupGuid;

    public int Generation;

    // Optional surname for UI
    public string Surname;

    // Task-state
    public bool IsBusy;  // default false

    public string LineageId;

    public Individual(
        Gender gender,
        int ageInTurns,
        float health01,
        AgeGroup aggAge,
        Guid aggGroupGuid,
        int generation = 0,
        string surname = null)
    {
        Id = Guid.NewGuid().ToString();
        Gender = gender;
        AgeInTurns = ageInTurns;
        Health01 = Mathf.Clamp01(health01);
        AggregatedAgeGroup = aggAge;
        AggregatedGroupGuid = aggGroupGuid;
        Generation = generation;
        Surname = surname;
        IsBusy = false;
    }

    public void AgeOneTurn() => AgeInTurns++;
}
