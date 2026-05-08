using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class CivilizationHappinessSystem : MonoBehaviour
{
    public static CivilizationHappinessSystem Instance { get; private set; }

    [Header("Tuning")]
    [Tooltip("Scale when needs IMPROVE (avg hunger+thirst drop). delta is positive = improvement magnitude.")]
    public float needImproveGainPerUnit = 0.25f;

    [Tooltip("Scale when needs WORSEN (avg hunger+thirst rise). delta is positive = worsening magnitude.")]
    public float needWorsenLossPerUnit  = 0.35f;

    [Tooltip("Penalty per fraction of families unhoused this turn.")]
    public float unhousedPenaltyPerFraction = 0.30f;

    [Tooltip("Bonus for a successful pairing attempt.")]
    public float pairingSuccessBonus  = 0.02f;

    [Tooltip("Penalty when a pairing attempt fails/unable to pair.")]
    public float pairingFailurePenalty = 0.02f;

    [Tooltip("Bonus when a player task completes successfully (normalized weight).")]
    public float taskSuccessBonusPerWeight = 0.01f;

    [Tooltip("Penalty when a player task fails (normalized weight).")]
    public float taskFailurePenaltyPerWeight = 0.015f;

    [Tooltip("Penalty per level of mismatch (family below shelter level).")]
    public float shelterLevelMismatchPenaltyPerLevel = 0.01f;

    // CivilizationHappinessSystem.cs  (add to fields)
    [Header("Birth outcome (happiness)")]
    [Tooltip("Happiness bonus per baby that survives a successful birth.")]
    public float birthSuccessBonusPerBaby = 0.03f;

    [Tooltip("Happiness penalty per baby that dies during/at birth when pregnancy otherwise succeeds.")]
    public float newbornDeathPenaltyPerBaby = 0.04f;

    [Tooltip("Happiness penalty when a pregnancy fails (no birth), mother survives.")]
    public float pregnancyFailurePenalty = 0.05f;

    [Tooltip("Extra penalty if the mother dies when the pregnancy fails.")]
    public float maternalDeathPenaltyOnFailure = 0.15f;

    [Tooltip("Penalty if the mother dies even though the birth succeeded.")]
    public float maternalDeathPenaltyOnSuccess = 0.10f;

    [Header("Food Variety (nutrition)")]
    public bool enableFoodVariety = true;

    [Tooltip("Max distinct foods we expect per turn, even if more foods are known.")]
    public int varietyExpectationCap = 6;

    [Tooltip("Don't apply LOW-variety penalties until at least this many edible foods are known.")]
    public int minKnownFoodsForPenalty = 4;

    [Range(0f, 1f)] public float lowVarietyThreshold = 0.25f; // below this => penalty
    [Range(0f, 1f)] public float highVarietyThreshold = 0.60f; // above this => bonus

    public float lowVarietyPenaltyMax = 0.03f; // per turn
    public float highVarietyBonusMax = 0.02f; // per turn

    [Tooltip("Ignore variety calc if less than this many nutrition points were consumed.")]
    public float minNutritionPointsToCount = 0.5f;

    [Tooltip("Excluded from known/variety.")]
    public string spoiledFoodResourceId = "spd";

    public bool debugFoodVariety = false;

    [Header("Spoiled Food (nutrition)")]
    public bool enableSpoiledFoodPenalty = true;

    [Tooltip("Penalty per spoiled unit consumed (on the nutrition-consumption phase).")]
    public float spoiledPenaltyPerUnit = 0.01f;

    [Tooltip("Extra penalty per nutrition-point taken from spoiled food (optional).")]
    public float spoiledPenaltyPerNutritionPoint = 0.0f;

    [Tooltip("Cap the total spoiled penalty per eating phase.")]
    public float spoiledPenaltyMax = 0.08f;

    public bool debugSpoiledFood = false;

    [Header("Food Quality (grade)")]
    public bool enableFoodGrade = true;

    [Tooltip("Don't penalize grade mismatch until you know at least this many edible foods.")]
    public int minKnownFoodsForGradePenalty = 4;

    [Tooltip("Max penalty per eating phase if you eat far below best-known grade.")]
    public float gradePenaltyMax = 0.04f;

    [Tooltip("Optional bonus when you eat near best-known grade.")]
    public float gradeBonusMax = 0.02f;

    [Tooltip("Ignore tiny mismatches.")]
    public float gradeMismatchIgnoreBelow = 0.25f;

    [Tooltip("Mismatch (in grade steps) that gives full penalty/bonus scaling.")]
    public float gradeMismatchFullAt = 1.25f;

    public int gradeExpectationTopK = 3;

    public bool debugFoodGrade = false;

    // refs
    private CivilizationStateManager civ;
    private PlayersPopulationManager pop;
    private PlayerFamilySimulationManager fam;

    // previous turn snapshot
    private float _prevAvgHunger = -1f; // unset sentinel
    private float _prevAvgThirst = -1f;

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()  { TurnSystem.SubscribeToEndOfTurn(OnEndTurn); }
    private void OnDisable() { TurnSystem.UnsubscribeFromEndOfTurn(OnEndTurn); }

    private void Start()
    {
        civ = CivilizationStateManager.Instance;
        pop = PlayersPopulationManager.Instance;
        fam = PlayerFamilySimulationManager.Instance;
        SnapshotNeeds();
    }

    private void OnEndTurn()
    {
        if (civ == null || pop == null || fam == null) return;

        // 1) Needs delta → happiness
        float curAvgHunger, curAvgThirst;
        ComputeWeightedNeeds(out curAvgHunger, out curAvgThirst);

        if (_prevAvgHunger >= 0f && _prevAvgThirst >= 0f)
        {
            float prevCombined = 0.5f * (_prevAvgHunger + _prevAvgThirst);
            float curCombined  = 0.5f * (curAvgHunger + curAvgThirst);
            float diff = prevCombined - curCombined; // + = improved, - = worse

            if (diff > 0f)      civ.AdjustHappiness(+needImproveGainPerUnit * diff);
            else if (diff < 0f) civ.AdjustHappiness(-needWorsenLossPerUnit  * -diff);
        }

        _prevAvgHunger = curAvgHunger;
        _prevAvgThirst = curAvgThirst;

        // 2) Housing penalty (consider BOTH family and individual shortfalls)
        int totalFamilies   = Mathf.Max(0, fam.GetFamilies()?.Count ?? 0);
        int totalIndividuals = CountTotalLivingIndividuals();

        int shelterFamSlots = CountShelterFamilyCapacity();
        int shelterIndSlots = CountShelterIndividualCapacity();

        float famShortfallFrac = 0f;
        if (totalFamilies > 0 && shelterFamSlots < totalFamilies)
            famShortfallFrac = (totalFamilies - shelterFamSlots) / (float)totalFamilies;

        float indShortfallFrac = 0f;
        if (totalIndividuals > 0 && shelterIndSlots < totalIndividuals)
            indShortfallFrac = (totalIndividuals - shelterIndSlots) / (float)totalIndividuals;

        // Use the worse shortage (families or individuals) to drive penalty
        float fractionUnhousedOrUnaccommodated = Mathf.Max(famShortfallFrac, indShortfallFrac);
        if (fractionUnhousedOrUnaccommodated > 0f)
            civ.AdjustHappiness(-unhousedPenaltyPerFraction * fractionUnhousedOrUnaccommodated);

        EvaluateShelterLevelMismatches();
        EvaluateFoodVarietyHappiness();

    }

    private void SnapshotNeeds()
    {
        if (pop == null) { _prevAvgHunger = _prevAvgThirst = -1f; return; }
        ComputeWeightedNeeds(out _prevAvgHunger, out _prevAvgThirst);
    }

    private void ComputeWeightedNeeds(out float avgHunger, out float avgThirst)
    {
        avgHunger = 0f; avgThirst = 0f;
        if (pop == null) return;

        var groups = pop.AllPopulations;
        int total = groups.Sum(g => Mathf.Max(0, g?.count ?? 0));
        if (total <= 0) return;

        float hSum = 0f, tSum = 0f;
        for (int i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            if (g == null || g.count <= 0) continue;
            hSum += g.hungerLevel * g.count;
            tSum += g.thirstLevel * g.count;
        }
        avgHunger = Mathf.Clamp01(hSum / total);
        avgThirst = Mathf.Clamp01(tSum / total);
    }

    private int CountShelterFamilyCapacity()
    {
        int slots = 0;
        var bldMgr = PlayerBuildingManager.Instance;
        if (bldMgr == null) return 0;

        var all = bldMgr.GetAll();
        for (int i = 0; i < all.Count; i++)
        {
            var rec = all[i];
            if (rec == null || rec.instance == null) continue;
            var shelter = rec.instance.GetComponent<ShelterControl>();
            if (shelter && shelter.isActiveAndEnabled)
                slots += Mathf.Max(0, shelter.familyCapacity);
        }
        return slots;
    }

    private int CountShelterIndividualCapacity()
    {
        int slots = 0;
        var bldMgr = PlayerBuildingManager.Instance;
        if (bldMgr == null) return 0;

        var all = bldMgr.GetAll();
        for (int i = 0; i < all.Count; i++)
        {
            var rec = all[i];
            if (rec == null || rec.instance == null) continue;
            var shelter = rec.instance.GetComponent<ShelterControl>();
            if (shelter && shelter.isActiveAndEnabled)
                slots += Mathf.Max(0, shelter.individualCapacity);
        }
        return slots;
    }

    private int CountTotalLivingIndividuals()
    {
        var inds = fam?.GetIndividuals();
        if (inds == null || inds.Count == 0) return 0;

        int living = 0;
        for (int i = 0; i < inds.Count; i++)
        {
            var p = inds[i];
            if (p != null && p.IsAlive) living++;
        }
        return living;
    }

    // ─────────────── External hooks for other systems ───────────────

    public void NotifyPairingSuccess()  { civ?.AdjustHappiness(+pairingSuccessBonus); }
    public void NotifyPairingFailure()  { civ?.AdjustHappiness(-pairingFailurePenalty); }

    /// Report task result (weight ~ importance: 1 = normal, 2 = big, etc.)
    public void NotifyTaskResult(bool success, float weight = 1f)
    {
        if (civ == null) return;
        weight = Mathf.Max(0f, weight);
        civ.AdjustHappiness((success ? +taskSuccessBonusPerWeight : -taskFailurePenaltyPerWeight) * weight);
    }

    public void NotifyShelterLevelMismatch(int levelDelta)
    {
        if (civ == null || levelDelta <= 0) return;
        civ.AdjustHappiness(-shelterLevelMismatchPenaltyPerLevel * levelDelta);
    }

    /// Family “level” below shelter level → penalty per level gap.
    private void EvaluateShelterLevelMismatches()
    {
        var bld = PlayerBuildingManager.Instance;
        if (civ == null || bld == null) return;

        var all = bld.GetAll();
        if (all == null || all.Count == 0) return;

        int maxShelterLevel = 0;

        // familyId -> their best (highest) shelter level (in case a family is assigned to multiple shelters)
        var familyLevel = new Dictionary<string, int>();

        for (int i = 0; i < all.Count; i++)
        {
            var rec = all[i];
            if (rec == null || rec.instance == null) continue;

            var shelter = rec.instance.GetComponent<ShelterControl>();
            if (shelter == null || !shelter.isActiveAndEnabled) continue;

            int lvl = Mathf.Max(1, shelter.shelterLevel);
            if (lvl > maxShelterLevel) maxShelterLevel = lvl;

            var housed = shelter.HousedFamilyIds;
            if (housed == null || housed.Count == 0) continue;

            for (int j = 0; j < housed.Count; j++)
            {
                var fid = housed[j];
                if (string.IsNullOrEmpty(fid)) continue;

                if (familyLevel.TryGetValue(fid, out var existing))
                    familyLevel[fid] = Mathf.Max(existing, lvl);
                else
                    familyLevel[fid] = lvl;
            }
        }

        if (maxShelterLevel <= 1 || familyLevel.Count == 0)
            return; // nothing to compare or no housed families

        foreach (var kv in familyLevel)
        {
            int delta = maxShelterLevel - Mathf.Max(1, kv.Value);
            if (delta > 0)
                NotifyShelterLevelMismatch(delta);
        }
    }

    public void NotifyPregnancyFailure(bool motherDied)
    {
        if (civ == null) return;
        civ.AdjustHappiness(-pregnancyFailurePenalty);
        if (motherDied) civ.AdjustHappiness(-maternalDeathPenaltyOnFailure);
    }

    public void NotifyBirthSuccess(int babiesAlive, int babiesDied, bool motherDied)
    {
        if (civ == null) return;

        if (babiesAlive > 0)
            civ.AdjustHappiness(+birthSuccessBonusPerBaby * Mathf.Max(0, babiesAlive));

        if (babiesDied > 0)
            civ.AdjustHappiness(-newbornDeathPenaltyPerBaby * Mathf.Max(0, babiesDied));

        if (motherDied)
            civ.AdjustHappiness(-maternalDeathPenaltyOnSuccess);
    }

    private void EvaluateFoodVarietyHappiness()
    {
        if (!enableFoodVariety || civ == null) return;

        var inv = PlayerInventoryManager.Instance;
        if (inv == null) return;

        inv.GetAndClearFoodHappinessMetrics(
            out int distinctConsumedNonSpoiled,
            out int unitsConsumedTotal,
            out float nutritionPointsTotal,
            out int spoiledUnits,
            out float spoiledNutritionPoints,
            out float avgGradeNonSpoiled,
            out int maxGradeConsumedNonSpoiled);

        // If they basically didn't eat this turn, let your existing hunger/thirst deltas handle happiness.
        if (nutritionPointsTotal < minNutritionPointsToCount) return;

        int knownEdibleFoods = CountKnownEdibleFoods();
        int expectedPool = Mathf.Clamp(knownEdibleFoods, 1, Mathf.Max(1, varietyExpectationCap));

        float varietyFrac = distinctConsumedNonSpoiled / (float)expectedPool;

        float delta = 0f;

        // Bonus for high variety
        if (varietyFrac >= highVarietyThreshold)
        {
            float t = Mathf.InverseLerp(highVarietyThreshold, 1f, varietyFrac);
            delta = +highVarietyBonusMax * t;
        }
        // Penalty for low variety (only once you know "enough" foods)
        else if (knownEdibleFoods >= minKnownFoodsForPenalty && varietyFrac <= lowVarietyThreshold)
        {
            float t = Mathf.InverseLerp(lowVarietyThreshold, 0f, varietyFrac);
            delta = -lowVarietyPenaltyMax * t;
        }

        if (enableFoodGrade && knownEdibleFoods >= minKnownFoodsForGradePenalty)
        {
            float expectedKnownGrade = GetExpectedKnownFoodGradeTopK();

            // Only matters if the civ actually knows something "better" than 0.
            if (expectedKnownGrade > 0 && avgGradeNonSpoiled > 0f)
            {
                float mismatch = expectedKnownGrade - avgGradeNonSpoiled; // + = ate lower than best-known

                if (mismatch > gradeMismatchIgnoreBelow)
                {
                    float t = Mathf.InverseLerp(gradeMismatchIgnoreBelow, gradeMismatchFullAt, mismatch);
                    float penalty = -gradePenaltyMax * Mathf.Clamp01(t);
                    civ.AdjustHappiness(penalty);

                    if (debugFoodGrade) {}
                        //Debug.Log($"[HAPPY][FoodGrade] bestKnown={expectedKnownGrade} avgEaten={avgGradeNonSpoiled:F2} mismatch={mismatch:F2} => {penalty:0.000;-0.000;0.000}");
                }
                else if (gradeBonusMax > 0f)
                {
                    // Optional “we're eating the good stuff” bonus (only when you're close to best)
                    float t = 1f - Mathf.Clamp01(mismatch / Mathf.Max(1e-6f, gradeMismatchFullAt));
                    float bonus = +gradeBonusMax * Mathf.Clamp01(t);
                    civ.AdjustHappiness(bonus);

                    if (debugFoodGrade) {}
                        //Debug.Log($"[HAPPY][FoodGrade] bestKnown={expectedKnownGrade} avgEaten={avgGradeNonSpoiled:F2} close => +{bonus:0.000}");
                }
            }
        }

        if (enableSpoiledFoodPenalty && spoiledUnits > 0)
        {
            float p =
                (spoiledUnits * spoiledPenaltyPerUnit) +
                (spoiledNutritionPoints * spoiledPenaltyPerNutritionPoint);

            float penalty = -Mathf.Clamp(p, 0f, spoiledPenaltyMax);
            civ.AdjustHappiness(penalty);

            if (debugSpoiledFood) {}
                //Debug.Log($"[HAPPY][SpoiledFood] spoiledUnits={spoiledUnits} spoiledPts={spoiledNutritionPoints:F1} => happiness {penalty:0.000;-0.000;0.000}");
        }

        if (Mathf.Abs(delta) > 1e-6f)
            civ.AdjustHappiness(delta);

        if (debugFoodVariety)
        {
            //Debug.Log($"[HAPPY][FoodVariety] distinct={distinctConsumedNonSpoiled} expectedPool={expectedPool} knownEdible={knownEdibleFoods} " +
                      //$"frac={varietyFrac:F2} points={nutritionPointsTotal:F1} => happiness {delta:+0.000;-0.000;0.000}");
        }
    }

    private int CountKnownEdibleFoods()
    {
        var knownMgr = PlayerKnownResourcesManager.Instance;
        if (knownMgr == null) return 0;

        int count = 0;
        foreach (var def in knownMgr.GetAllKnown())
        {
            if (def == null) continue;
            if (def.isGroup) continue;
            if (def.resourceType != ResourceType.Food) continue;

            if (!string.IsNullOrEmpty(spoiledFoodResourceId) &&
                string.Equals(def.resourceID, spoiledFoodResourceId, StringComparison.OrdinalIgnoreCase))
                continue;

            // only foods that actually provide nutrition
            if (def.GetNutritionPerUnit() <= 0f) continue;

            count++;
        }
        return count;
    }

    private float GetExpectedKnownFoodGradeTopK()
    {
        var knownMgr = PlayerKnownResourcesManager.Instance;
        if (knownMgr == null) return 0f;

        List<int> grades = new List<int>();

        foreach (var def in knownMgr.GetAllKnown())
        {
            if (def == null) continue;
            if (def.isGroup) continue;
            if (def.resourceType != ResourceType.Food) continue;

            if (!string.IsNullOrEmpty(spoiledFoodResourceId) &&
                string.Equals(def.resourceID, spoiledFoodResourceId, StringComparison.OrdinalIgnoreCase))
                continue;

            if (def.GetNutritionPerUnit() <= 0f) continue;

            grades.Add(def.GetFoodGradeValue());
        }

        if (grades.Count == 0) return 0f;

        grades.Sort((a, b) => b.CompareTo(a)); // desc

        int k = Mathf.Clamp(gradeExpectationTopK, 1, grades.Count);
        float sum = 0f;
        for (int i = 0; i < k; i++) sum += grades[i];
        return sum / k;
    }
}
