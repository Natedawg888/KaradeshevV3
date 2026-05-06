using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class UnitGroupPanelControl : MonoBehaviour
{
    [Header("Tracking Results")]
    public GameObject trackingResultsPanelRoot;

    public Transform trackingAnimalsContentRoot;
    public Transform trackingUnitsContentRoot;

    public TrackingResultItemUI trackingResultItemPrefab;
    public Button trackingResultsCloseButton;

    // reuse buffers to reduce allocations
    private static readonly List<TrackingResultEntry> _animalUiBuffer = new(64);
    private static readonly List<TrackingResultEntry> _unitUiBuffer = new(64);

    private void SetupTrackingResultsUI()
    {
        if (trackingResultsCloseButton != null)
        {
            trackingResultsCloseButton.onClick.RemoveAllListeners();
            trackingResultsCloseButton.onClick.AddListener(OnTrackingResultsCloseClicked);
        }

        if (trackingResultsPanelRoot != null)
            trackingResultsPanelRoot.SetActive(false);
    }

    private void OnTrackingResultsCloseClicked()
    {
        if (trackingResultsPanelRoot != null)
            trackingResultsPanelRoot.SetActive(false);

        if (_group != null)
        {
            _group.lastTrackingAnimalResults?.Clear();
            _group.lastTrackingUnitResults?.Clear();
            _group.hasPendingTrackingResults = false;
            _group.lastTrackingMarkerTurns = 1;
        }

        UpdateActionButtonState();
    }

    private void OpenTrackingResultsPanel()
    {
        if (trackingResultsPanelRoot == null || trackingResultItemPrefab == null)
        {
            Debug.LogWarning("[UnitGroupPanel] Tracking results UI not wired in inspector.");
            return;
        }

        if (actionPanelRoot != null)
            actionPanelRoot.SetActive(false);

        if (trackingAnimalsContentRoot != null)
            foreach (Transform child in trackingAnimalsContentRoot) Destroy(child.gameObject);

        if (trackingUnitsContentRoot != null)
            foreach (Transform child in trackingUnitsContentRoot) Destroy(child.gameObject);

        int markerTurns = (_group != null && _group.lastTrackingMarkerTurns > 0)
            ? _group.lastTrackingMarkerTurns
            : 1;

        var markerMgr = TrackingMarkerManager.Instance;

        // ✅ origin = the tile this unit group is currently on (used for distance sorting)
        Vector2Int originGrid = Vector2Int.zero;
        if (_owner != null)
        {
            var originTile = _owner.GetComponentInParent<TileControl>();
            if (originTile != null)
                originGrid = originTile.GetGridPosition();
        }

        // ---------------- Animals (skip already tracked) + SORT BY DISTANCE ----------------
        _animalUiBuffer.Clear();

        if (_group != null && _group.lastTrackingAnimalResults != null)
        {
            for (int i = 0; i < _group.lastTrackingAnimalResults.Count; i++)
            {
                var entry = _group.lastTrackingAnimalResults[i];
                if (entry == null) continue;

                if (markerMgr != null)
                {
                    TileControl tile = entry.sourceTile;

                    if (tile == null && UnitGroupActionManager.Instance != null)
                        tile = UnitGroupActionManager.Instance.FindTileByGridPosition_SLOW(entry.sourceGrid);

                    // ✅ Skip animals already being tracked (same tile + same icon)
                    if (tile != null && markerMgr.IsMarkerActive(tile, entry.icon))
                        continue;
                }

                _animalUiBuffer.Add(entry);
            }
        }

        _animalUiBuffer.Sort((a, b) =>
        {
            int da = (a.sourceGrid - originGrid).sqrMagnitude;
            int db = (b.sourceGrid - originGrid).sqrMagnitude;
            int c = da.CompareTo(db);
            if (c != 0) return c;
            return string.Compare(a.entityName, b.entityName, System.StringComparison.Ordinal);
        });

        if (trackingAnimalsContentRoot != null)
        {
            for (int i = 0; i < _animalUiBuffer.Count; i++)
            {
                var entry = _animalUiBuffer[i];
                var item = Instantiate(trackingResultItemPrefab, trackingAnimalsContentRoot);
                item.Setup(entry, markerTurns, _group, () =>
                {
                    OnTrackingResultsCloseClicked();   // closes tracking results + clears results flags
                    CloseAllPanelsStayHere();
                });
            }
        }

        // ---------------- Units + SORT BY DISTANCE ----------------
        _unitUiBuffer.Clear();

        if (_group != null && _group.lastTrackingUnitResults != null)
        {
            for (int i = 0; i < _group.lastTrackingUnitResults.Count; i++)
            {
                var entry = _group.lastTrackingUnitResults[i];
                if (entry == null) continue;
                _unitUiBuffer.Add(entry);
            }
        }

        _unitUiBuffer.Sort((a, b) =>
        {
            int da = (a.sourceGrid - originGrid).sqrMagnitude;
            int db = (b.sourceGrid - originGrid).sqrMagnitude;
            int c = da.CompareTo(db);
            if (c != 0) return c;
            return string.Compare(a.entityName, b.entityName, System.StringComparison.Ordinal);
        });

        if (trackingUnitsContentRoot != null)
        {
            for (int i = 0; i < _unitUiBuffer.Count; i++)
            {
                var entry = _unitUiBuffer[i];
                var item = Instantiate(trackingResultItemPrefab, trackingUnitsContentRoot);
                item.Setup(entry, markerTurns, _group, () =>
                {
                    OnTrackingResultsCloseClicked();   // closes tracking results + clears results flags
                    CloseAllPanelsStayHere();
                });
            }
        }

        trackingResultsPanelRoot.SetActive(true);
    }
}