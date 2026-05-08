using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

public class TileMovementUI : MonoBehaviour
{
    [Header("Owning Tile + Canvas")]
    public TileControl tile;
    public Canvas worldCanvas;

    [Header("Move UI")]
    public Button moveHereButton;

    [Header("Move Cost UI")]
    public GameObject moveTurnsRoot;
    public TMP_Text moveTurnsLabel;

    [Header("Hazard UI")]
    public GameObject hazardRoot;
    public TMP_Text damageChanceLabel;
    public TMP_Text fatalChanceLabel;

    [Header("Scout UI")]
    public Button scoutButton;
    public GameObject scoutTurnsRoot;
    public TMP_Text scoutTurnsLabel;

    private Coroutine _resolveRoutine;

    private void Awake()
    {
        Hide();
    }

    private void OnDisable()
    {
        if (_resolveRoutine != null)
        {
            StopCoroutine(_resolveRoutine);
            _resolveRoutine = null;
        }
    }

    public void ResolveNow()
    {
        tile = null;
        worldCanvas = null;

        TryResolveRefs();

        if (tile == null && _resolveRoutine == null)
            _resolveRoutine = StartCoroutine(ResolveRefsNextFrames());
    }

    private IEnumerator ResolveRefsNextFrames()
    {
        const int maxFrames = 5;

        for (int i = 0; i < maxFrames && tile == null; i++)
        {
            yield return null;
            TryResolveRefs();
        }

        _resolveRoutine = null;

        if (tile == null) {}
            //Debug.LogWarning($"[TileMovementUI] Could not find TileControl for {name}. Check hierarchy/sibling placement.");
    }

    private void TryResolveRefs()
    {
        if (tile == null)
        {
            tile = GetComponentInParent<TileControl>(true);

            if (tile == null && transform.parent != null)
                tile = transform.parent.GetComponentInChildren<TileControl>(true);
        }

        if (worldCanvas == null)
            worldCanvas = GetComponentInParent<Canvas>(true);
    }

    public void ShowMoveHereButton(
        UnityAction onClick,
        float turnCost,
        float damageChance01 = 0f,
        float fatalChance01 = 0f,
        bool showHazard = false)
    {
        if (moveHereButton == null)
            return;

        if (worldCanvas == null)
            worldCanvas = GetComponentInParent<Canvas>(true);

        if (worldCanvas != null)
        {
            worldCanvas.enabled = true;
            worldCanvas.gameObject.SetActive(true);
        }

        moveHereButton.gameObject.SetActive(true);
        moveHereButton.onClick.RemoveAllListeners();
        moveHereButton.onClick.AddListener(onClick);

        if (moveTurnsLabel != null)
        {
            int displayTurns = Mathf.CeilToInt(turnCost);
            moveTurnsLabel.text = displayTurns.ToString();

            if (moveTurnsRoot != null)
                moveTurnsRoot.SetActive(true);
        }

        if (hazardRoot != null)
        {
            bool anyHazard = showHazard && (damageChance01 > 0f || fatalChance01 > 0f);
            hazardRoot.SetActive(anyHazard);

            if (anyHazard)
            {
                int dmgPct = Mathf.RoundToInt(Mathf.Clamp01(damageChance01) * 100f);
                int fatalPct = Mathf.RoundToInt(Mathf.Clamp01(fatalChance01) * 100f);

                if (damageChanceLabel != null)
                    damageChanceLabel.text = dmgPct + "%";

                if (fatalChanceLabel != null)
                    fatalChanceLabel.text = fatalPct + "%";
            }
        }
    }

    public void ShowScoutButton(
        UnityAction onClick,
        int turnCost,
        float damageChance01 = 0f,
        float fatalChance01 = 0f,
        bool showHazard = false)
    {
        if (scoutButton == null)
            return;

        if (worldCanvas == null)
            worldCanvas = GetComponentInParent<Canvas>(true);

        if (worldCanvas != null)
        {
            worldCanvas.enabled = true;
            worldCanvas.gameObject.SetActive(true);
        }

        scoutButton.gameObject.SetActive(true);
        scoutButton.onClick.RemoveAllListeners();
        scoutButton.onClick.AddListener(onClick);

        if (scoutTurnsLabel != null)
        {
            scoutTurnsLabel.text = Mathf.Max(1, turnCost).ToString();

            if (scoutTurnsRoot != null)
                scoutTurnsRoot.SetActive(true);
        }

        if (hazardRoot != null)
        {
            bool anyHazard = showHazard && (damageChance01 > 0f || fatalChance01 > 0f);
            hazardRoot.SetActive(anyHazard);

            if (anyHazard)
            {
                int dmgPct = Mathf.RoundToInt(Mathf.Clamp01(damageChance01) * 100f);
                int fatalPct = Mathf.RoundToInt(Mathf.Clamp01(fatalChance01) * 100f);

                if (damageChanceLabel != null)
                    damageChanceLabel.text = dmgPct + "%";

                if (fatalChanceLabel != null)
                    fatalChanceLabel.text = fatalPct + "%";
            }
        }
    }

    public void HideScout()
    {
        if (scoutButton != null)
        {
            scoutButton.onClick.RemoveAllListeners();
            scoutButton.gameObject.SetActive(false);
        }

        if (scoutTurnsRoot != null)
            scoutTurnsRoot.SetActive(false);

        if (hazardRoot != null)
            hazardRoot.SetActive(false);

        RefreshCanvasVisibility();
    }

    public void Hide()
    {
        if (moveHereButton != null)
        {
            moveHereButton.onClick.RemoveAllListeners();
            moveHereButton.gameObject.SetActive(false);
        }

        if (moveTurnsRoot != null)
            moveTurnsRoot.SetActive(false);

        if (hazardRoot != null)
            hazardRoot.SetActive(false);

        if (scoutButton != null)
        {
            scoutButton.onClick.RemoveAllListeners();
            scoutButton.gameObject.SetActive(false);
        }

        if (scoutTurnsRoot != null)
            scoutTurnsRoot.SetActive(false);

        RefreshCanvasVisibility();
    }

    private void RefreshCanvasVisibility()
    {
        if (worldCanvas == null)
            worldCanvas = GetComponentInParent<Canvas>(true);

        if (worldCanvas == null)
            return;

        if (!IsActionCanvas(worldCanvas))
            return;

        bool anyVisibleActionUI = false;

        if (moveHereButton != null && moveHereButton.gameObject.activeSelf)
            anyVisibleActionUI = true;

        if (!anyVisibleActionUI && scoutButton != null && scoutButton.gameObject.activeSelf)
            anyVisibleActionUI = true;

        if (!anyVisibleActionUI)
        {
            var trackingUIs = worldCanvas.GetComponentsInChildren<TileTrackingMarkerUI>(true);
            for (int i = 0; i < trackingUIs.Length; i++)
            {
                var ui = trackingUIs[i];
                if (ui == null) continue;

                if (ui.root != null)
                {
                    if (ui.root.activeSelf)
                    {
                        anyVisibleActionUI = true;
                        break;
                    }
                }
                else if (ui.gameObject.activeSelf)
                {
                    anyVisibleActionUI = true;
                    break;
                }
            }
        }

        worldCanvas.enabled = anyVisibleActionUI;
        worldCanvas.gameObject.SetActive(anyVisibleActionUI);
    }

    private bool IsActionCanvas(Canvas canvas)
    {
        if (canvas == null) return false;

        string n = canvas.gameObject.name;
        return n == "UnitTileActions" || n.StartsWith("UnitTileActions");
    }
}
