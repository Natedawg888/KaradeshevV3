using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AnimalGroupMarkerView : MonoBehaviour, IScoutResultSource
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI countText;

    [Header("Hunting UI")]
    [SerializeField] private GameObject huntingIcon;   // fang / claw icon
    [SerializeField] private GameObject huntedIcon;    // danger / skull icon

    [Header("Action UI")]
    [SerializeField] private GameObject eatingIcon;    // grazing icon
    [SerializeField] private GameObject drinkingIcon;  // drinking icon

    [Header("Combat UI")]
    [SerializeField] private GameObject attackAnimalIcon; // attack / aggressor
    [SerializeField] private GameObject defendAnimalIcon;
    [SerializeField] private GameObject fleeIcon;         // running / panic

    [Header("Reproduction UI")]
    [SerializeField] private GameObject reproductionCooldownIcon;

    [Header("Player Targeting UI")]
    [SerializeField] private GameObject playerTargetedIcon;

    [Header("Health UI")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private GameObject healthRoot; // optional (so you can hide/show as a group)
    [SerializeField] private bool hideHealthWhenFull = false;

    [Header("Sacred Visuals")]
    [Tooltip("Assign ONLY marker/background/frame images here. The animal icon itself will never be tinted.")]
    [SerializeField] private List<Image> sacredTintImages = new List<Image>();

    [Tooltip("Optional extra text elements to tint for sacred animals. Count text is not tinted automatically.")]
    [SerializeField] private List<TextMeshProUGUI> sacredTintTexts = new List<TextMeshProUGUI>();

    [Tooltip("Fallback sacred color if ReligionManager has no override color.")]
    [SerializeField] private Color fallbackSacredColor = Color.cyan;

    public int GroupId { get; private set; }
    public TileCoord CurrentTile { get; private set; }

    // For scout results
    private AnimalGroupState _state;

    private bool _isPlayerTargeted;

    private bool _cachedBaseColors;
    private readonly Dictionary<Image, Color> _baseImageColors = new Dictionary<Image, Color>();
    private readonly Dictionary<TextMeshProUGUI, Color> _baseTextColors = new Dictionary<TextMeshProUGUI, Color>();

    private void Awake()
    {
        CacheBaseColorsIfNeeded();
    }

    // Init: marker is already spawned under the correct parent
    public void Init(AnimalGroupState state)
    {
        _state = state;
        GroupId = state != null ? state.id : -1;
        CurrentTile = state != null ? state.tile : default;

        CacheBaseColorsIfNeeded();
        ApplyState(state);
    }

    // Reparent when the group moves tiles
    public void UpdateFromState(AnimalGroupState state, RectTransform parentForTile)
    {
        _state = state;
        GroupId = state != null ? state.id : -1;

        if (state != null && !state.tile.Equals(CurrentTile))
        {
            CurrentTile = state.tile;

            var rect = GetComponent<RectTransform>();
            rect.SetParent(parentForTile, false);
        }

        CacheBaseColorsIfNeeded();
        ApplyState(state);
    }

    public void SetPlayerTargeted(bool targeted)
    {
        _isPlayerTargeted = targeted;

        if (playerTargetedIcon != null)
            playerTargetedIcon.SetActive(_isPlayerTargeted);
    }

    private void CacheBaseColorsIfNeeded()
    {
        if (_cachedBaseColors)
            return;

        for (int i = 0; i < sacredTintImages.Count; i++)
            RegisterImageBaseColor(sacredTintImages[i]);

        for (int i = 0; i < sacredTintTexts.Count; i++)
            RegisterTextBaseColor(sacredTintTexts[i]);

        _cachedBaseColors = true;
    }

    private void RegisterImageBaseColor(Image image)
    {
        if (image == null || _baseImageColors.ContainsKey(image))
            return;

        // Never tint the actual animal icon.
        if (image == iconImage)
            return;

        _baseImageColors.Add(image, image.color);
    }

    private void RegisterTextBaseColor(TextMeshProUGUI text)
    {
        if (text == null || _baseTextColors.ContainsKey(text))
            return;

        // Never tint the count text.
        if (text == countText)
            return;

        _baseTextColors.Add(text, text.color);
    }

    private void ApplyState(AnimalGroupState state)
    {
        // Icon
        if (iconImage != null && state != null && state.species != null && state.species.icon != null)
            iconImage.sprite = state.species.icon;

        // Count text
        if (countText != null && state != null)
            countText.text = state.size.ToString();

        bool isFleeing =
            state != null &&
            (
                state.isFleeingFromThreat ||
                state.lastAction == AnimalActionType.Flee
            );

        bool isHunted =
            state != null &&
            state.isTargetedByPredator &&
            !isFleeing;

        bool isAnyAttack =
            state != null &&
            (
                state.lastAction == AnimalActionType.AttackAnimal ||
                state.lastAction == AnimalActionType.AttackPlayer ||
                state.lastAction == AnimalActionType.AttackPlayerTile
            );

        bool isDefending =
            state != null &&
            !isFleeing &&
            (
                state.isInPredatorConflict ||
                (
                    state.lastAction == AnimalActionType.DefendAnimal &&
                    (
                        state.isTargetedByPredator ||
                        state.targetedByPredatorGroupId > 0
                    )
                )
            );

        // Hunting / hunted indicators
        if (huntingIcon != null)
            huntingIcon.SetActive(state != null && state.isHunting);

        if (huntedIcon != null)
            huntedIcon.SetActive(isHunted);

        // Eating / drinking icons
        if (eatingIcon != null)
            eatingIcon.SetActive(state != null && state.lastAction == AnimalActionType.Eat);

        if (drinkingIcon != null)
            drinkingIcon.SetActive(state != null && state.lastAction == AnimalActionType.Drink);

        // Combat icons
        if (attackAnimalIcon != null)
            attackAnimalIcon.SetActive(isAnyAttack);

        if (defendAnimalIcon != null)
            defendAnimalIcon.SetActive(isDefending);

        if (fleeIcon != null)
            fleeIcon.SetActive(isFleeing);

        // Reproduction cooldown icon
        if (reproductionCooldownIcon != null)
            reproductionCooldownIcon.SetActive(state != null && state.isOnReproductionCooldown);

        if (playerTargetedIcon != null)
            playerTargetedIcon.SetActive(_isPlayerTargeted);

        if (state != null)
            state.EnsureHealthValid();

        if (healthSlider != null && state != null)
        {
            int max = Mathf.Max(1, state.MaxHealth);
            int cur = Mathf.Clamp(state.currentHealth, 0, max);

            healthSlider.minValue = 0;
            healthSlider.maxValue = max;
            healthSlider.value = cur;

            if (healthRoot != null)
            {
                if (hideHealthWhenFull)
                    healthRoot.SetActive(cur < max);
                else
                    healthRoot.SetActive(true);
            }
        }
        else if (healthRoot != null)
        {
            healthRoot.SetActive(false);
        }

        ApplySacredVisuals(state);
    }

    private void ApplySacredVisuals(AnimalGroupState state)
    {
        bool isSacred = false;
        Color sacredColor = fallbackSacredColor;

        if (state != null && state.species != null)
        {
            PlayerReligionManager religion = PlayerReligionManager.Instance;
            if (religion != null &&
                religion.TryGetSacredAnimalMarkerColor(GroupId, state.species, out Color resolvedColor))
            {
                isSacred = true;
                sacredColor = resolvedColor;
            }
        }

        foreach (var kvp in _baseImageColors)
        {
            if (kvp.Key == null)
                continue;

            kvp.Key.color = isSacred ? sacredColor : kvp.Value;
        }

        foreach (var kvp in _baseTextColors)
        {
            if (kvp.Key == null)
                continue;

            kvp.Key.color = isSacred ? sacredColor : kvp.Value;
        }

        // Explicitly keep these at their normal colors.
        if (iconImage != null)
            iconImage.color = Color.white;
    }

    // --------------------------------------------------------------------
    //  IScoutResultSource implementation
    // --------------------------------------------------------------------

    public string GetScoutDisplayName()
    {
        if (_state != null && _state.species != null)
            return _state.species.name;

        return "Animals";
    }

    public Sprite GetScoutIcon()
    {
        if (_state != null && _state.species != null)
            return _state.species.icon;

        return null;
    }

    public int GetScoutCount()
    {
        return _state != null ? Mathf.Max(0, _state.size) : 0;
    }

    public bool GetIsMoving()
    {
        if (_state == null)
            return false;

        if (_state.isHunting ||
            _state.isTargetedByPredator ||
            _state.isInPredatorConflict ||
            _state.isFleeingFromThreat ||
            _state.lastAction == AnimalActionType.Flee)
        {
            return true;
        }

        return false;
    }

    public bool GetIsEating()
    {
        return _state != null && _state.lastAction == AnimalActionType.Eat;
    }

    public bool GetIsDrinking()
    {
        return _state != null && _state.lastAction == AnimalActionType.Drink;
    }

    public bool GetIsHunting()
    {
        return _state != null && _state.isHunting;
    }

    public bool GetIsDefending()
    {
        if (_state == null)
            return false;

        bool isFleeing =
            _state.isFleeingFromThreat ||
            _state.lastAction == AnimalActionType.Flee;

        if (isFleeing)
            return false;

        return _state.isInPredatorConflict ||
               (
                   _state.lastAction == AnimalActionType.DefendAnimal &&
                   (_state.isTargetedByPredator || _state.targetedByPredatorGroupId > 0)
               );
    }

    public bool GetIsTargeted()
    {
        return _state != null &&
               _state.isTargetedByPredator &&
               !_state.isFleeingFromThreat &&
               _state.lastAction != AnimalActionType.Flee;
    }

    public bool GetIsAttacking()
    {
        return _state != null &&
            (_state.lastAction == AnimalActionType.AttackAnimal ||
             _state.lastAction == AnimalActionType.AttackPlayer ||
             _state.lastAction == AnimalActionType.AttackPlayerTile);
    }

    public bool GetIsFleeing()
    {
        return _state != null &&
               (_state.isFleeingFromThreat || _state.lastAction == AnimalActionType.Flee);
    }
}