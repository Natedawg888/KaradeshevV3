using System; // for Array.Find
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProductionSelectionTileButton : MonoBehaviour
{
    [Header("UI")]
    public GameObject root;   // container for the button (can be same as button)
    public Button pickButton;

    [Header("Colors")]
    public Color normalColor   = Color.white;
    public Color selectedColor = Color.green;

    [Header("Tile")]
    public EnvironmentControl env; // the environment tile this button belongs to

    private Image _buttonImage;

    private void Awake()
    {
        if (env == null)
            env = GetComponentInParent<EnvironmentControl>();   // 👈 instead of GetComponent

        if (pickButton)
            _buttonImage = pickButton.GetComponent<Image>();
    }

#if UNITY_EDITOR
private void OnValidate()
{
    // 1) ROOT: must be the container object named "ProductionSelectionTileButton"
    if (root == null)
        root = FindRootContainer();

    // Use root as the base for all other lookups (safer)
    Transform baseT = root != null ? root.transform : transform;

    // 2) ENV: get from parents of the container
    if (env == null && baseT != null)
        env = baseT.GetComponentInParent<EnvironmentControl>(true);

    // 3) PICK BUTTON: must be child named "PickButton" under the container
    if (pickButton == null && baseT != null)
        pickButton = FindPickButtonUnder(baseT);

    // 4) Cache image for color swap
    if (pickButton != null)
    {
        _buttonImage = pickButton.GetComponent<Image>();
        if (_buttonImage == null)
            _buttonImage = pickButton.GetComponentInChildren<Image>(true);
    }
}

private GameObject FindRootContainer()
{
    // Prefer self if we're already on the correct container object
    if (string.Equals(gameObject.name, "ProductionSelectionTileButton", StringComparison.Ordinal))
        return gameObject;

    // Otherwise, find a child container with that name
    var child = transform.Find("ProductionSelectionTileButton");
    if (child != null) return child.gameObject;

    // Fallback: search deeper (inactive too)
    foreach (var t in GetComponentsInChildren<Transform>(true))
    {
        if (string.Equals(t.name, "ProductionSelectionTileButton", StringComparison.Ordinal))
            return t.gameObject;
    }

    // Last resort
    return gameObject;
}

private Button FindPickButtonUnder(Transform container)
{
    // Prefer exact child "PickButton"
    var pick = container.Find("PickButton");
    if (pick != null)
    {
        var btn = pick.GetComponent<Button>();
        if (btn != null) return btn;

        btn = pick.GetComponentInChildren<Button>(true);
        if (btn != null) return btn;
    }

    // Fallback: any button under the container
    return container.GetComponentInChildren<Button>(true);
}
#endif

    private void OnEnable()
    {
        if (pickButton)
        {
            pickButton.onClick.RemoveAllListeners();
            pickButton.onClick.AddListener(OnPickClicked);
        }

        ProductionSelectionController.OnSelectionModeChanged -= HandleModeChanged;
        ProductionSelectionController.OnSelectionModeChanged += HandleModeChanged;
        ProductionSelectionController.OnSelectionProgress -= HandleSelectionProgress;
        ProductionSelectionController.OnSelectionProgress += HandleSelectionProgress;

        HandleModeChanged(ProductionSelectionController.IsSelectionActive);
    }

    private void OnDisable()
    {
        ProductionSelectionController.OnSelectionModeChanged -= HandleModeChanged;
        ProductionSelectionController.OnSelectionProgress -= HandleSelectionProgress;

        // When this UI goes away, make sure we’re not forcing the canvas on anymore
        if (env != null)
            env.SetProductionSelectionCanvas(false);
    }

    // Fires when a tile is toggled or progress changes — refresh selection color/visibility
    private void HandleSelectionProgress(int picked, int max)
    {
        if (ProductionSelectionController.IsSelectionActive)
            RefreshVisibility();
    }

    private void HandleModeChanged(bool active)
    {
        if (!active)
        {
            if (root) root.SetActive(false);

            // turn off "force canvas for selection" when mode ends
            if (env != null)
                env.SetProductionSelectionCanvas(false);

            return;
        }

        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        if (!root) return;

        bool show = ProductionSelectionController.CanShowButtonFor(env);
        root.SetActive(show);

        if (env != null)
        {
            // While we *could* show for any in-range tile, the requirement was:
            // "only show the button on the environment canvas if the tile matches ..."
            // So we only force the canvas on when the button is actually visible.
            env.SetProductionSelectionCanvas(show);
        }

        if (show)
        {
            // keep color in sync with current selected state
            bool selected = ProductionSelectionController.IsTileSelected(env);
            ApplySelectedVisual(selected);
        }
    }

    private void OnPickClicked()
    {
        if (env == null) return;

        bool selected = ProductionSelectionController.ToggleTile(env);
        ApplySelectedVisual(selected);
    }

    private void ApplySelectedVisual(bool selected)
    {
        if (_buttonImage)
            _buttonImage.color = selected ? selectedColor : normalColor;
    }
}