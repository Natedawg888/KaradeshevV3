using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public partial class StageThemeApplier
{
    private void ReplaceXPTracker(StageTheme theme)
    {
        if (!xpTrackerMount || !theme) return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EnsureSingleXPTrackerInEditor(theme); // now lives in this file/assembly
            WireXPTrackerRuntimeBits();
            return;
        }
#endif

        // PLAY MODE: hard replace
        for (int i = xpTrackerMount.childCount - 1; i >= 0; i--)
            Object.Destroy(xpTrackerMount.GetChild(i).gameObject);

        currentXPTracker = null;

        if (!theme.xpTrackerPrefab) return;

        currentXPTracker = Object.Instantiate(theme.xpTrackerPrefab, xpTrackerMount);
        ApplyXPTrackerLayout(theme, currentXPTracker);
        WireXPTrackerRuntimeBits();
    }

#if UNITY_EDITOR
    // Editor-only helper lives in the same assembly, but its body is behind UNITY_EDITOR,
    // so runtime builds strip it out cleanly.
    private void EnsureSingleXPTrackerInEditor(StageTheme theme)
    {
        if (!xpTrackerMount) return;

        // If the theme has no prefab, clear whatever is there.
        if (!theme.xpTrackerPrefab)
        {
            for (int i = xpTrackerMount.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(xpTrackerMount.GetChild(i).gameObject);
            currentXPTracker = null;
            return;
        }

        // Find an existing instance of the right prefab
        GameObject candidate = null;
        for (int i = 0; i < xpTrackerMount.childCount; i++)
        {
            var go  = xpTrackerMount.GetChild(i).gameObject;
            var src = PrefabUtility.GetCorrespondingObjectFromSource(go);
            if (src == theme.xpTrackerPrefab) { candidate = go; break; }
        }

        if (candidate == null)
        {
            for (int i = xpTrackerMount.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(xpTrackerMount.GetChild(i).gameObject);

            var obj = PrefabUtility.InstantiatePrefab(theme.xpTrackerPrefab, xpTrackerMount);
            candidate = obj as GameObject;

            if (candidate != null)
            {
                candidate.transform.SetParent(xpTrackerMount, false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(candidate);
            }
        }
        else
        {
            // remove extras
            for (int i = xpTrackerMount.childCount - 1; i >= 0; i--)
            {
                var child = xpTrackerMount.GetChild(i).gameObject;
                if (child != candidate) Object.DestroyImmediate(child);
            }
        }

        currentXPTracker = candidate;
        ApplyXPTrackerLayout(theme, currentXPTracker);

        if (!Application.isPlaying && candidate)
            EditorSceneManager.MarkSceneDirty(candidate.scene);
    }
#endif

    private void ApplyXPTrackerLayout(StageTheme theme, GameObject trackerGO)
    {
        if (!trackerGO) return;

        trackerGO.transform.localScale    = Vector3.one;
        trackerGO.transform.localRotation = Quaternion.identity;

        var rt = trackerGO.GetComponent<RectTransform>();
        if (rt != null)
        {
            // sensible defaults
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;

            if (theme.overrideXPTrackerPosition) rt.anchoredPosition = theme.xpTrackerAnchoredPos;
            if (theme.overrideXPTrackerSize)     rt.sizeDelta        = theme.xpTrackerSize;
        }
    }

    private void WireXPTrackerRuntimeBits()
    {
        if (!currentXPTracker) return;

        var xpUI = currentXPTracker.GetComponentInChildren<XPCircleUI>(true);
        if (xpUI != null)
        {
            if (xpUI.player == null)
                xpUI.player = playerLevel != null ? playerLevel
                              : (PlayerLevel.Instance != null ? PlayerLevel.Instance
                              : Object.FindObjectOfType<PlayerLevel>());

            if (Application.isPlaying) xpUI.Refresh();
        }
    }
}
