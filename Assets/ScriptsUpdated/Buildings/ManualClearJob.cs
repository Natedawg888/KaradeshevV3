using System.Collections.Generic;
using UnityEngine;

public class ManualClearJob : MonoBehaviour
{
    [Header("UI")]
    public TimerUI timerUI;

    private BuildingStatus status;
    private int savedAutoClearTurns = -1;

    // runtime
    private int turnsTotal;
    private int turnsLeft;
    private bool active;
    private List<ResourceAmount> rewards; // manager injects (only used at completion)

    public bool IsActive => active;
    public int TurnsLeft => turnsLeft;

    private void Awake()
    {
        status = GetComponent<BuildingStatus>();
        if (timerUI) timerUI.gameObject.SetActive(false);
    }

    public void Initialize(int manualClearTurns, List<ResourceAmount> manualClearRewards)
    {
        turnsTotal = Mathf.Max(0, manualClearTurns);
        turnsLeft  = turnsTotal;
        rewards    = manualClearRewards != null ? new List<ResourceAmount>(manualClearRewards) : new List<ResourceAmount>();
    }

    public void Begin()
    {
        if (active) return;
        active = true;

        // disable auto-clear during manual clear
        if (status)
        {
            savedAutoClearTurns = status.autoClearAfterTurns;
            status.autoClearAfterTurns = 0;
        }

        if (timerUI)
        {
            timerUI.gameObject.SetActive(true);
            timerUI.Initialize(Mathf.Max(1, turnsTotal == 0 ? 1 : turnsTotal));
            timerUI.UpdateTimer(turnsLeft);
        }
    }

    public bool AdvanceOneTurn()
    {
        if (!active) return false;

        turnsLeft = Mathf.Max(0, turnsLeft - 1);
        if (timerUI) timerUI.UpdateTimer(turnsLeft);

        return turnsLeft <= 0;
    }

    public void CompleteAndClear()
    {
        active = false;

        if (timerUI) timerUI.gameObject.SetActive(false);

        // restore auto-clear config
        if (status && savedAutoClearTurns >= 0)
            status.autoClearAfterTurns = savedAutoClearTurns;
        savedAutoClearTurns = -1;

        // clear environment
        status?.TryClearToBaseTile();
    }
}