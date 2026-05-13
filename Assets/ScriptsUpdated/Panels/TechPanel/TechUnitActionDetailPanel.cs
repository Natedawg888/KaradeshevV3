using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Routes a UnitActionDefinitionSO to the correct type-specific detail panel.
/// Place this as the sub-panel child of TechUnitDetailPanel.
///
/// Inspector setup:
///   root          — the panel's root GameObject (shown/hidden by ShowFor/Hide)
///   closeButton   — closes the router (and hides all child panels)
///   meleePanel    — assign the MeleeActionDetailPanel child GameObject
/// </summary>
public class ActionDetailPanelRouter : MonoBehaviour
{
    [Header("Root")]
    public GameObject root;
    public Button closeButton;

    [Header("Type Panels")]
    public MeleeActionDetailPanel    meleePanel;
    public SurroundActionDetailPanel surroundPanel;
    public TrackingActionDetailPanel  trackingPanel;
    public ScoutTileActionDetailPanel scoutTilePanel;
    public RangedActionDetailPanel    rangedPanel;

    private void Awake()
    {
        if (closeButton) closeButton.onClick.AddListener(Hide);
        if (root) root.SetActive(false);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void ShowFor(UnitActionDefinitionSO action, TileUnitGroupData group = null)
    {
        if (action == null) { Hide(); return; }

        if (root) root.SetActive(true);
        gameObject.SetActive(true);

        HideAllPanels();
        RouteToPanel(action, group);
    }

    public void Hide()
    {
        HideAllPanels();
        if (root) root.SetActive(false);
    }

    // ── Routing ───────────────────────────────────────────────────────────────

    private void RouteToPanel(UnitActionDefinitionSO action, TileUnitGroupData group)
    {
        if (action is MeleeAttackActionSO melee && meleePanel != null)
        {
            meleePanel.ShowFor(melee, group);
            return;
        }

        if (action is SurroundActionSO surround && surroundPanel != null)
        {
            surroundPanel.ShowFor(surround);
            return;
        }

        if (action is TrackAreaActionSO tracking && trackingPanel != null)
        {
            trackingPanel.ShowFor(tracking);
            return;
        }

        if (action is ScoutTileActionSO scout && scoutTilePanel != null)
        {
            scoutTilePanel.ShowFor(scout);
            return;
        }

        if (action is RangedAttackActionSO ranged && rangedPanel != null)
        {
            rangedPanel.ShowFor(ranged);
            return;
        }

        // Unknown / not-yet-implemented action type — panel stays closed gracefully.
    }

    private void HideAllPanels()
    {
        if (meleePanel    != null) meleePanel.Hide();
        if (surroundPanel != null) surroundPanel.Hide();
        if (trackingPanel  != null) trackingPanel.Hide();
        if (scoutTilePanel != null) scoutTilePanel.Hide();
        if (rangedPanel    != null) rangedPanel.Hide();
    }
}
