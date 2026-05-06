using UnityEngine;
using System;

public class NameGenerator : MonoBehaviour
{
    public static NameGenerator Instance { get; private set; }

    [Header("Data")]
    public NameDatabase database;

    [Header("Random")]
    [Tooltip("Leave blank to use time-based seed; set for deterministic runs.")]
    public int seed = 0;

    private System.Random rng;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        rng = (seed == 0) ? new System.Random() : new System.Random(seed);

        if (database == null)
            Debug.LogWarning("[NameGenerator] No NameDatabase assigned. Names may be empty.");
    }

    // ------------------ Family Names ------------------

    public string NextFamilyName()
    {
        if (database == null) return "Family";

        // Prefer explicit list if provided & enabled
        if (database.useExplicitFamilyListFirst && database.familyExplicit.Count > 0)
            return Pick(database.familyExplicit);

        // Compose from parts if available
        bool hasRoot = database.familyRoots.Count > 0;
        bool hasSuffix = database.familySuffixes.Count > 0;

        if (hasRoot && hasSuffix)
        {
            string root = Pick(database.familyRoots);
            string suf  = Pick(database.familySuffixes);
            string name = root + suf;

            if (database.allowNumericSuffixOnFamily && Flip())
                name += "-" + rng.Next(Mathf.Max(1, database.numericSuffixMin), Mathf.Max(database.numericSuffixMin + 1, database.numericSuffixMax + 1)).ToString();

            return name;
        }

        // Fallbacks
        if (hasRoot)   return Pick(database.familyRoots);
        if (hasSuffix) return "Clan" + Pick(database.familySuffixes);

        if (database.familyExplicit.Count > 0) return Pick(database.familyExplicit);

        return "Family-" + rng.Next(100, 999);
    }

    // ------------------ Given Names ------------------

    public string NextGivenName(Gender gender)
    {
        if (database == null) return "Nameless";

        switch (gender)
        {
            case Gender.Male:
                if (database.givenMale.Count > 0)   return Pick(database.givenMale);
                break;
            case Gender.Female:
                if (database.givenFemale.Count > 0) return Pick(database.givenFemale);
                break;
        }

        if (database.givenNeutral.Count > 0) return Pick(database.givenNeutral);

        // ultimate fallback
        return "Nova-" + rng.Next(100, 999);
    }

    // ------------------ Utilities ------------------

    private T Pick<T>(System.Collections.Generic.IReadOnlyList<T> list)
    {
        if (list == null || list.Count == 0) return default;
        return list[rng.Next(list.Count)];
    }

    private bool Flip() => rng.NextDouble() < 0.5;
}
