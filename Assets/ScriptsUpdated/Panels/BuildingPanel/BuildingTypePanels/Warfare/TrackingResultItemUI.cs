using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TrackingResultItemUI : MonoBehaviour
{
    public Image iconImage;
    public TMP_Text nameLabel;
    public TMP_Text countLabel;
    public Button trackButton;

    // ✅ NEW: onTracked callback (close panel, clear results, etc.)
    public void Setup(
        TrackingResultEntry entry,
        int markerTurns,
        TileUnitGroupData owningGroup,
        Action onTracked)
    {
        if (entry == null)
        {
            gameObject.SetActive(false);
            return;
        }

        if (nameLabel != null)
            nameLabel.text = entry.entityName;

        if (iconImage != null)
        {
            iconImage.sprite = entry.icon;
            iconImage.gameObject.SetActive(entry.icon != null);
        }

        if (countLabel != null)
            countLabel.text = entry.count.ToString();

        if (trackButton == null)
            return;

        trackButton.onClick.RemoveAllListeners();
        trackButton.onClick.AddListener(() =>
        {
            var tile = entry.sourceTile;

            if (tile == null && UnitGroupActionManager.Instance != null)
                tile = UnitGroupActionManager.Instance.FindTileByGridPosition_SLOW(entry.sourceGrid);

            if (tile == null)
            {
                Debug.LogWarning("[TrackingResultItemUI] Could not find source tile to show marker.");
                return;
            }

            // ✅ Move camera to that tile
            var camCtrl = FindObjectOfType<CameraControl>();
            if (camCtrl != null)
            {
                Transform viewT = (camCtrl.mainCamera != null)
                    ? camCtrl.mainCamera.transform
                    : camCtrl.transform;

                Vector3 target = tile.transform.position;

                // Find the point on the ground plane (same Y as the tile) the camera is currently looking at.
                Vector3 origin = viewT.position;
                Vector3 dir    = viewT.forward;

                if (Mathf.Abs(dir.y) > 0.0001f)
                {
                    float enter = (target.y - origin.y) / dir.y;
                    Vector3 currentLookPoint = origin + dir * enter;

                    // Pan so the look point becomes the tile (keep same height + rotation).
                    Vector3 delta = target - currentLookPoint;
                    delta.y = 0f;

                    camCtrl.transform.position += delta;
                }
                else
                {
                    // Fallback: just match XZ, keep height
                    Vector3 p = camCtrl.transform.position;
                    camCtrl.transform.position = new Vector3(target.x, p.y, target.z);
                }

                camCtrl.minimapNeedsUpdate = true;
            }

            // ✅ Show marker + timer + register target on owning group
            var mgr = TrackingMarkerManager.Instance;
            if (mgr == null)
            {
                Debug.LogWarning("[TrackingResultItemUI] No TrackingMarkerManager in scene.");
                return;
            }

            mgr.ShowMarker(tile, entry.icon, markerTurns, owningGroup);

            // ✅ Close tracking panel (and whatever else caller wants)
            onTracked?.Invoke();
        });
    }
}