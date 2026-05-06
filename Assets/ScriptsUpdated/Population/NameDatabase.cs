using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NameDatabase", menuName = "Kardashev/Name Database", order = 10)]
public class NameDatabase : ScriptableObject
{
    [Header("Family Name Parts (optional to combine)")]
    public List<string> familyRoots = new();     // e.g., "Astra", "Nova", "Vega", ...
    public List<string> familySuffixes = new();  // e.g., "-vor", "-nari", "-ius", "-kin"

    [Tooltip("If true, family names can be selected directly from this explicit list instead of composing.")]
    public bool useExplicitFamilyListFirst = false;
    public List<string> familyExplicit = new();  // e.g., "Kepler-Prime", "Solari", "Orionis"

    [Header("Given Names")]
    public List<string> givenNeutral = new();
    public List<string> givenMale = new();
    public List<string> givenFemale = new();

    [Header("Options")]
    [Tooltip("If true, may append a random 2–3 digit numeric suffix to composed family names.")]
    public bool allowNumericSuffixOnFamily = true;

    [Min(0)] public int numericSuffixMin = 10;
    [Min(0)] public int numericSuffixMax = 999;
}