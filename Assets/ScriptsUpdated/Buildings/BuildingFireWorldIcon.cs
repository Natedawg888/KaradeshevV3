using UnityEngine;

/// <summary>
/// Attach to a building's world canvas alongside its other icons.
/// Shows a fire icon while the building is burning, and switches to a
/// radial TimerUI countdown while players are actively fighting the fire.
/// Follows the same pattern as ProductionBuildingControl's world overlay.
/// </summary>
[DisallowMultipleComponent]
public class BuildingFireWorldIcon : MonoBehaviour
{
    [Header("Fire Icon (always on while burning)")]
    [Tooltip("Simple fire icon shown whenever the building is on fire.")]
    public GameObject fireIcon;

    [Header("Fight Timer (shown while actively fighting)")]
    [Tooltip("Radial TimerUI showing fight turns remaining. Uses Image.fillAmount.")]
    public TimerUI fightTimerUI;

    [Tooltip("Root object for the fight timer — hide/show this rather than the TimerUI directly.")]
    public GameObject fightTimerRoot;

    private BuildingFireState _fireState;

    private void Awake()
    {
        SetFireIcon(false);
        SetFightTimer(false);
    }

    private void OnEnable()  => Subscribe();
    private void OnDisable() => Unsubscribe();
    private void OnDestroy() => Unsubscribe();

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    /// <summary>Call once after the building is initialised (e.g. in Start or when the building spawns).</summary>
    public void Bind(BuildingFireState fireState)
    {
        Unsubscribe();
        _fireState = fireState;
        Subscribe();
        Refresh();
    }

    // ------------------------------------------------------------------
    // Internal
    // ------------------------------------------------------------------

    private void Subscribe()
    {
        if (_fireState == null) return;
        _fireState.OnIgnited      += HandleIgnited;
        _fireState.OnExtinguished += HandleExtinguished;
        _fireState.OnFightProgress += HandleFightProgress;
    }

    private void Unsubscribe()
    {
        if (_fireState == null) return;
        _fireState.OnIgnited       -= HandleIgnited;
        _fireState.OnExtinguished  -= HandleExtinguished;
        _fireState.OnFightProgress -= HandleFightProgress;
    }

    private void Refresh()
    {
        if (_fireState == null)
        {
            SetFireIcon(false);
            SetFightTimer(false);
            return;
        }

        SetFireIcon(_fireState.IsOnFire);

        if (_fireState.IsFighting)
            RefreshFightTimer();
        else
            SetFightTimer(false);
    }

    private void RefreshFightTimer()
    {
        if (_fireState == null) return;

        SetFightTimer(true);

        if (fightTimerUI != null)
            fightTimerUI.SetState(_fireState.baseFightTurns, _fireState.FightTurnsRemaining);
    }

    // ------------------------------------------------------------------
    // Event handlers
    // ------------------------------------------------------------------

    private void HandleIgnited(BuildingFireState state)
    {
        SetFireIcon(true);
        SetFightTimer(false);
    }

    private void HandleExtinguished(BuildingFireState state)
    {
        SetFireIcon(false);
        SetFightTimer(false);
    }

    private void HandleFightProgress(BuildingFireState state, int rollResult, int turnsRemaining)
    {
        RefreshFightTimer();
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private void SetFireIcon(bool on)
    {
        if (fireIcon != null) fireIcon.SetActive(on);
    }

    private void SetFightTimer(bool on)
    {
        if (fightTimerRoot != null)
            fightTimerRoot.SetActive(on);
        else if (fightTimerUI != null)
            fightTimerUI.gameObject.SetActive(on);
    }
}
