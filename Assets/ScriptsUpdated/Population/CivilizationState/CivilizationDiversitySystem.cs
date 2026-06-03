using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CivilizationDiversitySystem : MonoBehaviour
{

    [Header("Low diversity → base health penalty")]
    [Range(0f,1f)]
    public float lowDiversityThreshold = 0.35f;

    [Range(0.01f, 1f)]
    public float diversityPenaltyStep = 0.05f;

    public int baseHealthPenaltyPerStep = 1;

    [SerializeField] private int _penaltyStepsApplied = 0;

    [Header("Genetics (what to write into civ.diversity01)")]
    [Range(0f,1f)]
    public float diversityBlendPerTurn = 0.25f;

    [Header("Genetics (performance)")]
    public int maxIndividualsOverall = 5000;
    public int rowsPerBatch = 200;
    public int framesBetweenBatches = 2;

    [Header("Turn Stagger")]
    [Tooltip("Frames to wait after onTurnEnd before processing first batch. Set to different values per system to spread frame load.")]
    [SerializeField] private int coroutineStartDelay = 0;

    // runtime state
    private Coroutine _geneticsCo;
    private bool _isBlockingTurn = false;
    private struct GeneticsState
    {
        public List<string> genes;   // selected, shuffled, capped
        public int iRow;             // next row to process (0..genes.Count-1)
        public double sum;           // accumulated sum of (1 - similarity)
        public int pairs;            // accumulated pair count
    }
    private GeneticsState _gstate;

    [SerializeField] private float _lastGeneticDiversity01;
    public float LastGeneticDiversity01 => _lastGeneticDiversity01;

    private CivilizationStateManager civ;
    private PlayerFamilySimulationManager fam;

    private void OnEnable()
    {
        TurnSystem.SubscribeToEndOfTurn(OnEndTurn);
    }

    private void OnDisable()
    {
        TurnSystem.UnsubscribeFromEndOfTurn(OnEndTurn);
        if (_isBlockingTurn) { _isBlockingTurn = false; TurnSystem.UnblockTurnAdvance(); }
    }

    private void Start()
    {
        civ = CivilizationStateManager.Instance;
        fam = PlayerFamilySimulationManager.Instance;
    }

    private void OnEndTurn()
    {
        if (civ == null || fam == null) return;

        // (Re)start genetics-only computation each turn
        if (_isBlockingTurn) { _isBlockingTurn = false; TurnSystem.UnblockTurnAdvance(); }
        if (_geneticsCo != null) StopCoroutine(_geneticsCo);
        PrepareGeneticsSnapshot();           // snapshot & shuffle genes (<= maxIndividualsOverall)
        _geneticsCo = StartCoroutine(ProcessGeneticsBatches());
    }

    private void PrepareGeneticsSnapshot()
    {
        _gstate = new GeneticsState { genes = new List<string>(), iRow = 0, sum = 0, pairs = 0 };

        var famMgr = PlayerFamilySimulationManager.Instance;
        if (famMgr == null) return;

        var people = famMgr.GetIndividuals();
        if (people == null || people.Count == 0) return;

        // collect alive with lineage
        for (int i = 0; i < people.Count; i++)
        {
            var p = people[i];
            if (p == null || !p.IsAlive) continue;
            if (string.IsNullOrEmpty(p.LineageId)) continue;
            _gstate.genes.Add(p.LineageId);
        }

        int n = _gstate.genes.Count;
        if (n <= 1) return;

        // shuffle (Fisher–Yates)
        var rng = new System.Random();
        for (int i = 0; i < n; i++)
        {
            int j = rng.Next(i, n);
            (_gstate.genes[i], _gstate.genes[j]) = (_gstate.genes[j], _gstate.genes[i]);
        }

        // cap
        if (n > maxIndividualsOverall)
            _gstate.genes.RemoveRange(maxIndividualsOverall, n - maxIndividualsOverall);
    }

    private System.Collections.IEnumerator ProcessGeneticsBatches()
    {
        for (int d = 0; d < coroutineStartDelay; d++)
            yield return null;

        _isBlockingTurn = true;
        TurnSystem.BlockTurnAdvance();

        var genes = _gstate.genes;
        if (genes == null || genes.Count <= 1)
        {
            _lastGeneticDiversity01 = 0f;
            _isBlockingTurn = false;
            TurnSystem.UnblockTurnAdvance();
            _geneticsCo = null;
            yield break;
        }

        int N = genes.Count;

        // Process row-by-row; each row i computes pairs (0..i-1)
        while (_gstate.iRow < N)
        {
            int endRow = Mathf.Min(_gstate.iRow + Mathf.Max(1, rowsPerBatch), N);
            for (int i = _gstate.iRow; i < endRow; i++)
            {
                for (int j = 0; j < i; j++)
                {
                    double sim = LineageUtils.HammingSimilarity(genes[i], genes[j]); // 0..1; match lowers diversity
                    _gstate.sum += (1.0 - sim);                                      // mismatch raises diversity
                    _gstate.pairs++;
                }
            }

            _gstate.iRow = endRow;

            if (_gstate.pairs > 0)
                _lastGeneticDiversity01 = (float)(_gstate.sum / _gstate.pairs);

            for (int f = 0; f < Mathf.Max(0, framesBetweenBatches); f++)
                yield return null;
        }

        _lastGeneticDiversity01 = (_gstate.pairs > 0) ? (float)(_gstate.sum / _gstate.pairs) : 0f;

        if (civ != null)
        {
            // apply genetics-only diversity
            float blended = Mathf.Lerp(civ.diversity01, _lastGeneticDiversity01, Mathf.Clamp01(diversityBlendPerTurn));
            civ.diversity01 = Mathf.Clamp01(blended);
        }

        // --- Reversible low-diversity → base health penalty ---
        var rule = PlayerHealthRulebook.Instance;
        if (rule != null && diversityPenaltyStep > 0f && baseHealthPenaltyPerStep > 0)
        {
            float diversity = (civ != null) ? civ.diversity01 : _lastGeneticDiversity01;
            float shortfall = Mathf.Max(0f, lowDiversityThreshold - diversity);

            // How many steps SHOULD be applied this turn?
            int desiredSteps = Mathf.FloorToInt(shortfall / diversityPenaltyStep);

            // Move from current -> desired by applying delta steps
            int deltaSteps = desiredSteps - _penaltyStepsApplied;

            if (deltaSteps != 0)
            {
                // negative deltaSteps means we are REMOVING penalty (restoring base health);
                // positive deltaSteps means adding more penalty.
                int deltaPerAge = -baseHealthPenaltyPerStep * deltaSteps;

                rule.ApplyDeltas(
                    dChildH: deltaPerAge, dTeenH: deltaPerAge, dAdultH: deltaPerAge, dElderH: deltaPerAge,
                    dC2T: 0, dT2A: 0, dA2E: 0, dLife: 0,
                    dChildRec: 0f, dTeenRec: 0f, dAdultRec: 0f, dElderRec: 0f,
                    dChildRes: 0f, dTeenRes: 0f, dAdultRes: 0f, dElderRes: 0f,
                    dLowHealthThresh: 0f, dMortAtZero: 0f, dElderStart: 0f, dElderLife: 0f
                );

                _penaltyStepsApplied = desiredSteps;
            }
        }

        _isBlockingTurn = false;
        TurnSystem.UnblockTurnAdvance();
        _geneticsCo = null;
    }
}