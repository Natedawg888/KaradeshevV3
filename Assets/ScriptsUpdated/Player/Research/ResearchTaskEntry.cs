using UnityEngine;

public class ResearchTaskEntry : MonoBehaviour
{
    public TimerUI timerUI;

    public void Bind(int totalTurns, int turnsLeft)
    {
        if (!timerUI) return;
        int total = Mathf.Max(1, totalTurns);
        int left  = Mathf.Clamp(turnsLeft, 0, total);
        timerUI.Initialize(total);
        timerUI.UpdateTimer(left);
    }

    public void UpdateTurns(int turnsLeft)
    {
        if (!timerUI) return;
        timerUI.UpdateTimer(Mathf.Max(0, turnsLeft));
    }
}
