using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class TraderWorldIcon : MonoBehaviour
{
    [Header("Icon")]
    [Tooltip("Root GameObject to show/hide. Defaults to this GameObject if left empty.")]
    [SerializeField] private GameObject iconRoot;

    [Header("Optional")]
    [Tooltip("If assigned, displays how many turns the trader has left.")]
    [SerializeField] private TMP_Text turnsRemainingText;

    private TradeBuildingControl _tradeControl;
    private bool _turnSubscribed;

    private void Awake()
    {
        SetVisible(false);
    }

    private void Start()
    {
        var control = GetComponent<TradeBuildingControl>();
        if (control == null) control = GetComponentInParent<TradeBuildingControl>();
        if (control != null) Bind(control);
    }

    private void OnEnable()  => Subscribe();
    private void OnDisable() => Unsubscribe();
    private void OnDestroy() => Unsubscribe();

    public void Bind(TradeBuildingControl control)
    {
        Unsubscribe();
        _tradeControl = control;
        Subscribe();
        Refresh();
    }

    // ──────────────────── Subscriptions ────────────────────

    private void Subscribe()
    {
        if (_tradeControl == null) return;
        _tradeControl.OnTraderArrived += HandleTraderArrived;
        _tradeControl.OnTraderLeft    += HandleTraderLeft;
    }

    private void Unsubscribe()
    {
        if (_tradeControl != null)
        {
            _tradeControl.OnTraderArrived -= HandleTraderArrived;
            _tradeControl.OnTraderLeft    -= HandleTraderLeft;
        }

        if (_turnSubscribed)
        {
            TurnSystem.UnsubscribeFromEndOfTurn(HandleTurnEnded);
            _turnSubscribed = false;
        }
    }

    // ──────────────────── Event Handlers ────────────────────

    private void HandleTraderArrived(TravelingTraderOffer offer)
    {
        SetVisible(true);
        RefreshTurnsText();

        if (!_turnSubscribed && turnsRemainingText != null)
        {
            TurnSystem.SubscribeToEndOfTurn(HandleTurnEnded);
            _turnSubscribed = true;
        }
    }

    private void HandleTraderLeft()
    {
        SetVisible(false);
        ClearTurnsText();

        if (_turnSubscribed)
        {
            TurnSystem.UnsubscribeFromEndOfTurn(HandleTurnEnded);
            _turnSubscribed = false;
        }
    }

    private void HandleTurnEnded()
    {
        RefreshTurnsText();
    }

    // ──────────────────── Helpers ────────────────────

    private void Refresh()
    {
        if (_tradeControl == null) { SetVisible(false); ClearTurnsText(); return; }

        bool active = _tradeControl.HasActiveTrader();
        SetVisible(active);

        if (active)
        {
            RefreshTurnsText();
            if (!_turnSubscribed && turnsRemainingText != null)
            {
                TurnSystem.SubscribeToEndOfTurn(HandleTurnEnded);
                _turnSubscribed = true;
            }
        }
        else
        {
            ClearTurnsText();
        }
    }

    private void SetVisible(bool on)
    {
        GameObject target = iconRoot != null ? iconRoot : gameObject;
        if (target.activeSelf != on)
            target.SetActive(on);
    }

    private void RefreshTurnsText()
    {
        if (turnsRemainingText == null) return;
        TravelingTraderOffer offer = _tradeControl?.GetCurrentTraderOffer();
        turnsRemainingText.text = offer != null ? offer.turnsRemaining.ToString() : string.Empty;
    }

    private void ClearTurnsText()
    {
        if (turnsRemainingText != null)
            turnsRemainingText.text = string.Empty;
    }
}
