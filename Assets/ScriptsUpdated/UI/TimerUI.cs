using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


[Serializable]
public class TimerUIState
{
    public int maxTurns;
    public int currentTurns;
    public bool isActive;
}

public class TimerUI : MonoBehaviour
{
    public Image timerImage; // Reference to the Image component
    private int maxTurns;
    private int currentTurns;

    public void Initialize(int maxTurns)
    {
        this.maxTurns    = Mathf.Max(1, maxTurns);
        this.currentTurns = this.maxTurns;
        UpdateTimer();
    }

    public void UpdateTimer(int turnsLeft)
    {
        currentTurns = Mathf.Max(0, turnsLeft);
        if (maxTurns <= 0) maxTurns = 1;

        float fillAmount = (float)currentTurns / maxTurns;
        if (timerImage != null)
            timerImage.fillAmount = fillAmount;
    }

    public int GetRemainingTime()
    {
        return currentTurns;
    }

    public void SetRemainingTime(int turns)
    {
        currentTurns = Mathf.Max(0, turns);
        UpdateTimer();
    }

    private void UpdateTimer()
    {
        if (maxTurns <= 0) maxTurns = 1;

        float fillAmount = (float)currentTurns / maxTurns;
        if (timerImage != null)
            timerImage.fillAmount = fillAmount;
    }

    public void SetState(int maxTurns, int turnsLeft)
    {
        this.maxTurns    = Mathf.Max(1, maxTurns);
        this.currentTurns = Mathf.Clamp(turnsLeft, 0, this.maxTurns);
        UpdateTimer();
    }
}