using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnitGroupMarker : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text nameText;
    public Image iconImage;
    public TMP_Text amountText;
    public Slider healthSlider;

    [Header("Expiry UI")]
    public Slider expirySlider;

    [Header("Upkeep UI")]
    public GameObject upkeepMissRoot;
    public Slider upkeepMissSlider;

    [Header("Movement UI")]
    public TimerUI movementTimer;

    [Header("Scout UI")]
    public TimerUI scoutTimer;

    [Header("Combat UI")]
    public TimerUI combatTimer;
    [SerializeField] private Image combatTimerTintImage;
    [SerializeField] private Color meleeTimerColor = new Color(0.90f, 0.22f, 0.22f, 1f);
    [SerializeField] private Color surroundTimerColor = new Color(1.00f, 0.76f, 0.18f, 1f);

    [Header("Ranged UI")]
    public TimerUI rangedTimer;

    // ✅ Under Attack UI (no linger)
    [Header("Under Attack UI")]
    [SerializeField] private GameObject underAttackIcon;

    [Header("Interaction")]
    public Button openGroupPanelButton;

    private TileUnitGroupData _group;
    public TileUnitGroupData BoundGroup => _group;

    private int _initialRemainingServiceTurns = -1;

    private void Awake()
    {
        if (openGroupPanelButton != null)
        {
            openGroupPanelButton.onClick.RemoveAllListeners();
            openGroupPanelButton.onClick.AddListener(OpenGroupPanelFromMarker);
        }
    }

    public void Bind(TileUnitGroupData group)
    {
        _group = group;

        if (nameText)
        {
            if (group.unitType != null)
            {
                string id =
                    !string.IsNullOrEmpty(group.unitType.unitID)
                        ? group.unitType.unitID
                        : group.unitType.unitName;

                nameText.text = id;
            }
            else
            {
                nameText.text = "Unit Group";
            }
        }

        if (iconImage && group.unitType != null && group.unitType.unitIcon != null)
            iconImage.sprite = group.unitType.unitIcon;

        _initialRemainingServiceTurns = -1;
        SetupExpirySliderInitially();
        HideUpkeepMissUI();

        if (movementTimer != null) movementTimer.gameObject.SetActive(false);
        if (scoutTimer != null) scoutTimer.gameObject.SetActive(false);
        if (combatTimer != null) combatTimer.gameObject.SetActive(false);
        if (rangedTimer != null) rangedTimer.gameObject.SetActive(false);

        if (underAttackIcon != null)
            underAttackIcon.SetActive(false);

        Refresh();
    }

    public void Refresh()
    {
        if (_group == null) return;

        if (amountText)
            amountText.text = _group.unitCount.ToString();

        if (healthSlider != null)
        {
            healthSlider.maxValue = _group.maxHealth;
            healthSlider.value = Mathf.Clamp(_group.currentHealth, 0, _group.maxHealth);
        }

        UpdateExpirySlider();
        UpdateUpkeepMissSlider();
        UpdateMovementTimer();
        UpdateScoutTimer();
        UpdateCombatTimer();     // melee
        UpdateRangedTimer();     // NEW
    }

    public void SetUnderAttack(bool underAttack)
    {
        if (underAttackIcon == null) return;

        if (underAttackIcon.activeSelf != underAttack)
            underAttackIcon.SetActive(underAttack);
    }

    private void UpdateCombatTimer()
    {
        if (combatTimer == null || _group == null)
            return;

        bool hasCombatAction =
            (_group.activeAction is MeleeAttackActionSO ||
             _group.activeAction is SurroundActionSO) &&
            _group.remainingActionTurns > 0;

        if (!hasCombatAction)
        {
            combatTimer.gameObject.SetActive(false);
            return;
        }

        int turnsLeft = Mathf.Max(0, _group.remainingActionTurns);
        int maxTurns = 1;

        if (_group.activeAction is MeleeAttackActionSO melee)
        {
            maxTurns = Mathf.Max(1, melee.durationTurns);

            if (combatTimerTintImage != null)
                combatTimerTintImage.color = meleeTimerColor;
        }
        else if (_group.activeAction is SurroundActionSO surround)
        {
            maxTurns = Mathf.Max(1, surround.durationTurns);

            if (combatTimerTintImage != null)
                combatTimerTintImage.color = surroundTimerColor;
        }

        if (!combatTimer.gameObject.activeSelf)
        {
            combatTimer.SetState(maxTurns, turnsLeft);
            combatTimer.gameObject.SetActive(true);
        }
        else
        {
            combatTimer.UpdateTimer(turnsLeft);
        }
    }

    private void UpdateRangedTimer()
    {
        if (_group == null)
            return;

        // Choose which timer object to drive
        var timer = (rangedTimer != null) ? rangedTimer : combatTimer;
        if (timer == null)
            return;

        bool hasRanged =
            _group.activeAction is RangedAttackActionSO &&
            _group.remainingActionTurns > 0;

        // If we’re reusing combatTimer, don’t hide it here while melee is active
        if (!hasRanged)
        {
            if (rangedTimer != null)
            {
                rangedTimer.gameObject.SetActive(false);
            }
            return;
        }

        int turnsLeft = Mathf.Max(0, _group.remainingActionTurns);

        int maxTurns = 1;
        if (_group.activeAction is RangedAttackActionSO ranged)
            maxTurns = Mathf.Max(1, ranged.durationTurns);

        if (!timer.gameObject.activeSelf)
        {
            timer.SetState(maxTurns, turnsLeft);
            timer.gameObject.SetActive(true);
        }
        else
        {
            timer.UpdateTimer(turnsLeft);
        }
    }

    // ---------------- Open panel from marker ----------------

    private void OpenGroupPanelFromMarker()
    {
        if (_group == null)
        {
            Debug.LogWarning("[UnitGroupMarker] Tried to open group panel, but _group is null.");
            return;
        }

        // Find the owning TileUnitGroupControl (the logical owner of this group).
        var owner = GetComponentInParent<TileUnitGroupControl>();
        if (owner == null)
        {
            Debug.LogWarning("[UnitGroupMarker] No TileUnitGroupControl found in parents.");
            return;
        }

        // Find the shared UnitGroupPanel in the scene.
        var panel = FindObjectOfType<UnitGroupPanelControl>();
        if (panel == null)
        {
            Debug.LogWarning("[UnitGroupMarker] No UnitGroupPanelControl found in scene.");
            return;
        }

        // Try to find the "right" kinetic warfare building on THIS tile
        // that can train this unit type.
        KineticWarfareControl trainerContext = null;

        var groupTile = owner.GetComponentInParent<TileControl>();
        if (groupTile != null)
        {
            // Look for any KineticWarfareControl components under this tile
            var kwOnTile = groupTile.GetComponentsInChildren<KineticWarfareControl>(true);
            if (kwOnTile != null && kwOnTile.Length > 0 && _group.unitType != null)
            {
                for (int i = 0; i < kwOnTile.Length; i++)
                {
                    var kw = kwOnTile[i];
                    if (kw == null) continue;

                    // Only consider it "right" if this building can train this unit type
                    var trainable = kw.GetAvailableTrainableUnits();
                    if (trainable == null) continue;

                    bool canTrainType = false;
                    for (int t = 0; t < trainable.Count; t++)
                    {
                        if (trainable[t] == _group.unitType)
                        {
                            canTrainType = true;
                            break;
                        }
                    }

                    if (canTrainType)
                    {
                        trainerContext = kw;
                        break;
                    }
                }
            }
        }

        // If trainerContext is null, Train button will be disabled by UpdateTrainButtonState().
        // If it's non-null, the panel will allow training on that building.
        panel.ShowFor(
            _group,
            owner,
            trainerContext: trainerContext,
            kineticPanel: null,
            buildingPanel: null);
    }

    // ---------------- Expiry slider logic ----------------

    private void SetupExpirySliderInitially()
    {
        if (expirySlider == null || _group == null)
            return;

        if (TurnSystem.Instance == null)
        {
            expirySlider.gameObject.SetActive(false);
            return;
        }

        // Only show expiry for human units that actually have an expiry.
        if (!_group.HasExpiry || _group.unitType == null || !_group.unitType.isHuman)
        {
            expirySlider.gameObject.SetActive(false);
            return;
        }

        int currentTurn = TurnSystem.GetCurrentTurn();
        int remaining   = Mathf.Max(0, _group.expiryTurn - currentTurn);

        if (remaining <= 0)
        {
            expirySlider.minValue = 0f;
            expirySlider.maxValue = 1f;
            expirySlider.value    = 0f;
            expirySlider.gameObject.SetActive(true);
            _initialRemainingServiceTurns = 1;
            return;
        }

        _initialRemainingServiceTurns = remaining;

        expirySlider.minValue = 0f;
        expirySlider.maxValue = _initialRemainingServiceTurns;
        expirySlider.value    = remaining;
        expirySlider.gameObject.SetActive(true);
    }

    private void UpdateExpirySlider()
    {
        if (expirySlider == null || _group == null || TurnSystem.Instance == null)
            return;

        // Only meaningful for human units with an expiry.
        if (!_group.HasExpiry || _group.unitType == null || !_group.unitType.isHuman)
        {
            expirySlider.gameObject.SetActive(false);
            return;
        }

        int currentTurn = TurnSystem.GetCurrentTurn();
        int remaining   = Mathf.Max(0, _group.expiryTurn - currentTurn);

        if (_initialRemainingServiceTurns <= 0)
        {
            _initialRemainingServiceTurns = Mathf.Max(1, remaining);
            expirySlider.minValue = 0f;
            expirySlider.maxValue = _initialRemainingServiceTurns;
        }

        expirySlider.gameObject.SetActive(true);
        expirySlider.value = Mathf.Clamp(remaining, 0, expirySlider.maxValue);
    }

    // ---------------- Upkeep miss slider logic ----------------

    private void HideUpkeepMissUI()
    {
        if (upkeepMissRoot != null)
            upkeepMissRoot.SetActive(false);
        else if (upkeepMissSlider != null)
            upkeepMissSlider.gameObject.SetActive(false);
    }

    private void ShowUpkeepMissUI()
    {
        if (upkeepMissRoot != null)
            upkeepMissRoot.SetActive(true);
        else if (upkeepMissSlider != null)
            upkeepMissSlider.gameObject.SetActive(true);
    }

    private void UpdateUpkeepMissSlider()
    {
        if (upkeepMissSlider == null || _group == null)
        {
            HideUpkeepMissUI();
            return;
        }

        var unit = _group.unitType;
        if (unit == null)
        {
            HideUpkeepMissUI();
            return;
        }

        int maxMisses = Mathf.Max(0, unit.maxMissedUpkeepTurns);
        int missed    = Mathf.Max(0, _group.missedUpkeepTurns);

        // Only show after first miss, and only if unit type actually has tolerance
        if (maxMisses <= 0 || missed <= 0)
        {
            HideUpkeepMissUI();
            return;
        }

        int remaining = Mathf.Max(0, maxMisses - missed);

        upkeepMissSlider.minValue = 0f;
        upkeepMissSlider.maxValue = maxMisses;
        upkeepMissSlider.value    = remaining;

        ShowUpkeepMissUI();
    }

    // ---------------- Movement timer logic ----------------

    private void UpdateMovementTimer()
    {
        if (movementTimer == null || _group == null)
            return;

        // Check if this group currently has an active movement route.
        bool hasRoute =
            _group.plannedPathGridPositions != null &&
            _group.plannedStepTurnCosts != null &&
            _group.plannedPathGridPositions.Count > 0 &&
            _group.plannedPathGridPositions.Count == _group.plannedStepTurnCosts.Count &&
            _group.currentPathIndex < _group.plannedPathGridPositions.Count;

        if (!hasRoute)
        {
            movementTimer.gameObject.SetActive(false);
            return;
        }

        // Compute total remaining movement turns for this route.
        int stepCount = _group.plannedStepTurnCosts.Count;
        int idx       = Mathf.Clamp(_group.currentPathIndex, 0, stepCount - 1);

        float remaining = 0f;

        // For the current step, use the remaining cost if > 0,
        // otherwise fall back to the full step cost.
        float currentStepRemaining =
            _group.remainingTurnCostOnCurrentStep > 0f
            ? _group.remainingTurnCostOnCurrentStep
            : _group.plannedStepTurnCosts[idx];

        remaining += Mathf.Max(0f, currentStepRemaining);

        // Future steps (after the current index).
        for (int i = idx + 1; i < stepCount; i++)
        {
            remaining += Mathf.Max(0f, _group.plannedStepTurnCosts[i]);
        }

        int remainingInt = Mathf.CeilToInt(remaining);

        if (remainingInt <= 0)
        {
            movementTimer.gameObject.SetActive(false);
            return;
        }

        // First time we show the timer for this route: initialize with full remaining.
        if (!movementTimer.gameObject.activeSelf)
        {
            movementTimer.SetState(remainingInt, remainingInt);
            movementTimer.gameObject.SetActive(true);
        }
        else
        {
            // Subsequent updates: just adjust the "turns left".
            movementTimer.UpdateTimer(remainingInt);
        }
    }

    // ---------------- Scout timer logic ----------------

    private void UpdateScoutTimer()
    {
        if (scoutTimer == null || _group == null)
            return;

        // Only show timer for an active SCOUT action
        bool hasScoutAction =
            _group.activeAction is ScoutTileActionSO &&
            _group.remainingActionTurns > 0;

        if (!hasScoutAction)
        {
            scoutTimer.gameObject.SetActive(false);
            return;
        }

        int turnsLeft = Mathf.Max(0, _group.remainingActionTurns);
        if (turnsLeft <= 0)
        {
            scoutTimer.gameObject.SetActive(false);
            return;
        }

        if (!scoutTimer.gameObject.activeSelf)
        {
            // First time: initialise max + current
            scoutTimer.SetState(turnsLeft, turnsLeft);
            scoutTimer.gameObject.SetActive(true);
        }
        else
        {
            // Subsequent updates: just tick down
            scoutTimer.UpdateTimer(turnsLeft);
        }
    }
}