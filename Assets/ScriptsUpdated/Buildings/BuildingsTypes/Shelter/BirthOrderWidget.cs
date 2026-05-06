using TMPro;
using UnityEngine;

public class BirthOrderWidget : MonoBehaviour
{
    [Header("Refs")]
    public TimerUI timer;

    public string BoundOrderId { get; private set; }
    private int maxTurns;

    public void Bind(string orderId, int maxTurns, string tagText = null)
    {
        BoundOrderId = orderId;
        this.maxTurns = Mathf.Max(1, maxTurns);

        if (timer != null)
            timer.Initialize(this.maxTurns);

        UpdateTurns(this.maxTurns); // show full duration on bind
    }

    public void UpdateTurns(int turnsRemaining)
    {
        turnsRemaining = Mathf.Max(0, turnsRemaining);

        if (timer != null)
            timer.UpdateTimer(turnsRemaining);
    }
}