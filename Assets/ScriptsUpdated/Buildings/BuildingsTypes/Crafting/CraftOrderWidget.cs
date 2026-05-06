using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CraftOrderWidget : MonoBehaviour
{
    [Header("Refs")]
    public TimerUI timer;
    public Image icon;                // NEW: recipe icon target

    public string BoundOrderId { get; private set; }
    private int maxTurns;

    // Back-compat: original signature forwards to the new one without an icon
    public void Bind(string orderId, int maxTurns)
        => Bind(orderId, maxTurns, null);

    public void Bind(string orderId, int maxTurns, Sprite iconSprite)
    {
        BoundOrderId = orderId;
        this.maxTurns = Mathf.Max(1, maxTurns);

        if (icon)      icon.sprite = iconSprite; // can be null; just leaves it blank
        if (timer)     timer.Initialize(this.maxTurns);

        UpdateTurns(this.maxTurns);
    }

    public void UpdateTurns(int turnsRemaining)
    {
        turnsRemaining = Mathf.Max(0, turnsRemaining);
        if (timer)   timer.UpdateTimer(turnsRemaining);
    }
}
