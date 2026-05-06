using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public partial class BuildingPanelControl : MonoBehaviour
{
    [Header("UI Roots")]
    public GameObject root;
    public Button closeButton;

    [Header("Smooth Swap (Soft Hide)")]
    [Range(0f, 0.5f)] public float softFadeDuration = 0.1f;
    public bool useUnscaledTime = true;

    // Soft-hide infra
    private CanvasGroup _cg;

    private void Awake()
    {
        if (root != null)
        {
            _cg = root.GetComponent<CanvasGroup>();
            if (_cg == null) _cg = root.AddComponent<CanvasGroup>();
            _cg.alpha = 0f;
            _cg.interactable = false;
            _cg.blocksRaycasts = false;
            root.SetActive(false);
        }
    }

    public void Hide()
    {
        TileInteraction.SetSelectionEnabled(false);
        TileInteraction.GetInstance()?.EnableSelectionAfter(0.01f);

        cameraControl.PopInputLock();

        if (root)
        {
            root.SetActive(false);
            if (_cg != null)
            {
                _cg.alpha = 0f;
                _cg.interactable = false;
                _cg.blocksRaycasts = false;
            }
        }

        Unsubscribe();
        OnClose?.Invoke();
    }

    private void SoftHideForChild()
    {
        if (root == null) return;
        if (!root.activeSelf) root.SetActive(true);
        if (_cg == null) _cg = root.AddComponent<CanvasGroup>();

        StopAllCoroutines();
        StartCoroutine(Fade(_cg, 0f, softFadeDuration));

        _cg.interactable = false;
        _cg.blocksRaycasts = false;
    }

    public void SoftShowFromChild()
    {
        if (root == null) return;
        if (!root.activeSelf) root.SetActive(true);
        if (_cg == null) _cg = root.AddComponent<CanvasGroup>();

        StopAllCoroutines();
        StartCoroutine(Fade(_cg, 1f, softFadeDuration));

        _cg.blocksRaycasts = true;
    }
    
    private System.Collections.IEnumerator Fade(CanvasGroup cg, float target, float duration)
    {
        if (cg == null) yield break;

        float start = cg.alpha;
        float t = 0f;
        while (t < duration)
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t += dt;
            float u = duration <= 0f ? 1f : Mathf.Clamp01(t / duration);
            u = u * u * (3f - 2f * u); // smoothstep
            cg.alpha = Mathf.Lerp(start, target, u);
            if (target > start && !cg.interactable && cg.alpha >= 0.98f)
                cg.interactable = true;
            yield return null;
        }
        cg.alpha = target;
        cg.interactable = target >= 1f;
    }

    public void SoftHideForMoveMode()
    {
        if (root == null) return;
        if (!root.activeSelf) return;
        EnsureCanvasGroup();
        if (_cg.alpha <= 0.01f) return;

        SoftHideInternal();
    }

    private bool _hiddenForProductionSelection;

    public void SoftHideForProductionSelection()
    {
        if (root == null) return;
        if (!root.activeSelf) return; // hard-hidden already, don't touch it

        EnsureCanvasGroup();

        // If we're already basically invisible (e.g., hidden for child panel), don't mark.
        if (_cg.alpha <= 0.01f) return;

        _hiddenForProductionSelection = true;
        SoftHideInternal();
    }

    /// <summary>
    /// Restore the BUILDING panel after ProductionSelection mode ends,
    /// but ONLY if we hid it for that reason.
    /// </summary>
    public void SoftShowAfterProductionSelection()
    {
        if (!_hiddenForProductionSelection) return;
        _hiddenForProductionSelection = false;

        SoftShowFromChild(); // fade back to 1, re-enable raycasts
    }

    // --- helpers (keep them private) ---

    private void EnsureCanvasGroup()
    {
        if (root == null) return;
        if (_cg == null) _cg = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();
    }

    private void SoftHideInternal()
    {
        if (root == null) return;

        if (!root.activeSelf) root.SetActive(true);
        EnsureCanvasGroup();

        StopAllCoroutines();
        StartCoroutine(Fade(_cg, 0f, softFadeDuration));

        _cg.interactable = false;
        _cg.blocksRaycasts = false;
    }
}
