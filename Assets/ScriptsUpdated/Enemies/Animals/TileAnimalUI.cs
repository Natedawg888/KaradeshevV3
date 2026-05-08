using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

public class TileAnimalUI : MonoBehaviour
{
    [Header("Tile Coord (auto from TileControl)")]
    [SerializeField] private int tileX;
    [SerializeField] private int tileY;

    [SerializeField] private TileControl tileControl;

    [Header("UI")]
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform contentRoot;

    [Header("Visibility Rules")]
    [SerializeField] private bool requireDiscoveredTile = true;
    [SerializeField] private bool requireUnitGroupOnTile = true;

    [Tooltip("Also require at least one ACTIVE marker under contentRoot (markers deactivate themselves when not attacking player).")]
    [SerializeField] private bool requireAtLeastOneActiveMarker = true;

    [Tooltip("How often we re-check gating (seconds).")]
    [SerializeField] private float gatePollInterval = 0.25f;

    private static GridManager _cachedGridManager;

    public TileCoord Coord => new TileCoord(tileX, tileY);
    public RectTransform ContentRoot => contentRoot;

    private Coroutine _resolveRoutine;

    private EnvironmentStatus _envStatus;
    private TileUnitGroupControl _unitCtrl;

    private float _nextGateCheckTime = 0f;
    private bool _lastGateVisible = false;

    private void Awake()
    {
        ResolveNow(startCoroutineIfMissing: true);
        HookEnvEvents();
        RefreshGate(force: true);
    }

    private void OnEnable()
    {
        WorldCanvasMode.OnChanged += HandleWorldCanvasModeChanged;
        ResolveNow(startCoroutineIfMissing: true);
        HookEnvEvents();
        RefreshGate(force: true);
    }

    private void OnDisable()
    {
        WorldCanvasMode.OnChanged -= HandleWorldCanvasModeChanged;
        UnhookEnvEvents();
    }

    private void OnDestroy()
    {
        WorldCanvasMode.OnChanged -= HandleWorldCanvasModeChanged;
        UnhookEnvEvents();

        if (_resolveRoutine != null)
        {
            StopCoroutine(_resolveRoutine);
            _resolveRoutine = null;
        }
    }

    private void HandleWorldCanvasModeChanged(bool unitsOnly)
    {
        RefreshGate(force: true);
    }

    private void Update()
    {
        if (gatePollInterval <= 0f) return;

        if (Time.unscaledTime >= _nextGateCheckTime)
        {
            _nextGateCheckTime = Time.unscaledTime + gatePollInterval;
            RefreshGate(force: false);
        }
    }

    public void ResolveNow(bool startCoroutineIfMissing = false)
    {
        if (this == null)
            return;

        if (!gameObject)
            return;

        if (worldCanvas == null)
            worldCanvas = GetComponentInParent<Canvas>(true) ?? GetComponent<Canvas>();

        if (tileControl == null)
        {
            tileControl = GetComponent<TileControl>();

            if (tileControl == null && transform.parent != null)
                tileControl = transform.parent.GetComponentInChildren<TileControl>(true);

            if (tileControl == null)
                tileControl = GetComponentInParent<TileControl>(true);
        }

        SyncCoordFromTileControl();
        ResolveSiblingRefs();

        if (startCoroutineIfMissing && _resolveRoutine == null && tileControl == null)
            _resolveRoutine = StartCoroutine(ResolveNextFrames());
    }

    private IEnumerator ResolveNextFrames()
    {
        const int maxFrames = 10;

        for (int i = 0; i < maxFrames && tileControl == null; i++)
        {
            yield return null;
            ResolveNow(startCoroutineIfMissing: false);
        }

        _resolveRoutine = null;

        if (tileControl == null) {}
            //Debug.LogWarning($"[TileAnimalUI] Still no TileControl for {name}. Check hierarchy.");
    }

    private void SyncCoordFromTileControl()
    {
        if (tileControl == null) return;

        if (_cachedGridManager == null)
            _cachedGridManager = FindObjectOfType<GridManager>();

        if (_cachedGridManager != null)
        {
            Vector2Int gridPos = _cachedGridManager.GetGridPosition(tileControl.transform.position);
            tileX = gridPos.x;
            tileY = gridPos.y;
        }
    }

    private void ResolveSiblingRefs()
    {
        if (tileControl == null)
        {
            _envStatus = null;
            _unitCtrl = null;
            return;
        }

        Transform root = tileControl.transform.parent != null ? tileControl.transform.parent : tileControl.transform;

        if (_envStatus == null)
        {
            _envStatus =
                tileControl.GetComponent<EnvironmentStatus>() ??
                root.GetComponentInChildren<EnvironmentStatus>(true);
        }

        if (_unitCtrl == null)
        {
            _unitCtrl =
                tileControl.GetComponentInChildren<TileUnitGroupControl>(true) ??
                root.GetComponentInChildren<TileUnitGroupControl>(true);
        }
    }

    private void HookEnvEvents()
    {
        UnhookEnvEvents();

        if (_envStatus == null)
            ResolveSiblingRefs();

        if (_envStatus != null)
        {
            _envStatus.OnDiscovered += HandleEnvChanged;
            _envStatus.OnReset += HandleEnvChanged;
        }
    }

    private void UnhookEnvEvents()
    {
        if (_envStatus != null)
        {
            _envStatus.OnDiscovered -= HandleEnvChanged;
            _envStatus.OnReset -= HandleEnvChanged;
        }
    }

    private void HandleEnvChanged()
    {
        RefreshGate(force: true);
    }

    private bool HasAnyActiveMarker()
    {
        if (!requireAtLeastOneActiveMarker)
            return true;

        if (contentRoot == null)
            return false;

        for (int i = 0; i < contentRoot.childCount; i++)
        {
            var child = contentRoot.GetChild(i);
            if (child != null && child.gameObject.activeSelf)
                return true;
        }

        return false;
    }

    private bool IsTileDiscovered()
    {
        if (_envStatus == null)
            ResolveSiblingRefs();

        // Keep your current fail-open behavior if EnvironmentStatus is missing
        return _envStatus == null || _envStatus.IsDiscovered;
    }

    private bool HasUnitGroupOnThisTile()
    {
        if (_unitCtrl == null)
            ResolveSiblingRefs();

        return _unitCtrl != null &&
               _unitCtrl.Groups != null &&
               _unitCtrl.Groups.Count > 0;
    }

    public bool ShouldShowBattleCanvas()
    {
        ResolveNow();

        // Marker rule is its own AND gate
        bool markersPass = !requireAtLeastOneActiveMarker || HasAnyActiveMarker();

        // These two are OR rules, but only if enabled
        bool anyCoreRuleEnabled = requireDiscoveredTile || requireUnitGroupOnTile;

        bool discoveredPass = requireDiscoveredTile && IsTileDiscovered();
        bool unitsPass = requireUnitGroupOnTile && HasUnitGroupOnThisTile();

        // If neither rule is enabled, default to visible
        bool corePass = !anyCoreRuleEnabled || discoveredPass || unitsPass;

        return markersPass && corePass;
    }

    public void RefreshNow()
    {
        ResolveNow();
        RefreshGate(force: true);
    }

    private void RefreshGate(bool force)
    {
        if (!WorldCanvasMode.UnitsOnly)
        {
            HideForNormalMode(force);
            return;
        }

        bool shouldShow = ShouldShowBattleCanvas();

        if (!force && shouldShow == _lastGateVisible)
            return;

        _lastGateVisible = shouldShow;

        if (shouldShow)
        {
            // IMPORTANT: the battle-mode toggle may have disabled the whole canvas object
            if (worldCanvas != null && !worldCanvas.gameObject.activeSelf)
                worldCanvas.gameObject.SetActive(true);

            if (scrollRect != null)
                scrollRect.gameObject.SetActive(true);
            else if (contentRoot != null)
                contentRoot.gameObject.SetActive(true);

            if (worldCanvas != null)
                worldCanvas.enabled = true;
        }
        else
        {
            if (scrollRect != null)
                scrollRect.gameObject.SetActive(false);
            else if (contentRoot != null)
                contentRoot.gameObject.SetActive(false);

            if (worldCanvas != null)
            {
                worldCanvas.enabled = false;

                // Keep this in sync with your global toggle system
                if (worldCanvas.gameObject.activeSelf)
                    worldCanvas.gameObject.SetActive(false);
            }
        }
    }

    public void RegisterGroup()
    {
        RefreshGate(force: false);
    }

    public void UnregisterGroup()
    {
        RefreshGate(force: false);
    }

    public bool ShouldShowInCurrentMode()
    {
        // Normal mode = always hide animal canvas
        if (!WorldCanvasMode.UnitsOnly)
            return false;

        // Battle mode = follow animal UI rules
        return ShouldShowBattleCanvas();
    }

    private void HideForNormalMode(bool force)
    {
        if (!force && _lastGateVisible == false)
            return;

        _lastGateVisible = false;

        if (scrollRect != null)
            scrollRect.gameObject.SetActive(false);
        else if (contentRoot != null)
            contentRoot.gameObject.SetActive(false);

        if (worldCanvas != null)
        {
            worldCanvas.enabled = false;

            if (worldCanvas.gameObject.activeSelf)
                worldCanvas.gameObject.SetActive(false);
        }
    }
}
