using UnityEngine;

public class CivilizationIntegrationSystem : MonoBehaviour
{
    [Header("Tuning")]
    [Tooltip("Natural recovery each turn toward 1.0 (e.g., social cohesion building over time).")]
    public float naturalRecoveryPerTurn = 0.01f;

    [Tooltip("Immediate integration drop per newly added family (culture shock).")]
    public float newFamilyShockPerFamily = 0.03f;

    private CivilizationStateManager civ;
    private PlayerFamilySimulationManager fam;

    private int _prevFamilyCount = -1;

    private void OnEnable()
    {
        TurnSystem.SubscribeToEndOfTurn(OnEndTurn);
    }

    private void OnDisable()
    {
        TurnSystem.UnsubscribeFromEndOfTurn(OnEndTurn);
    }

    private void Start()
    {
        civ = CivilizationStateManager.Instance;
        fam = PlayerFamilySimulationManager.Instance;

        SnapshotFamilyCount();
    }

    private void SnapshotFamilyCount()
    {
        if (fam == null) { _prevFamilyCount = -1; return; }
        var list = fam.GetFamilies();
        _prevFamilyCount = (list == null) ? 0 : Mathf.Max(0, list.Count);
    }

    private void OnEndTurn()
    {
        if (civ == null || fam == null) return;

        // Natural recovery first
        civ.AdjustIntegration(+naturalRecoveryPerTurn);

        var list = fam.GetFamilies();
        int curCount = (list == null) ? 0 : Mathf.Max(0, list.Count);

        // First turn: just seed
        if (_prevFamilyCount < 0)
        {
            _prevFamilyCount = curCount;
            return;
        }

        int delta = curCount - _prevFamilyCount;
        if (delta > 0)
        {
            // New families joined -> integration shock
            civ.AdjustIntegration(-newFamilyShockPerFamily * delta);
        }

        _prevFamilyCount = curCount;
    }
}