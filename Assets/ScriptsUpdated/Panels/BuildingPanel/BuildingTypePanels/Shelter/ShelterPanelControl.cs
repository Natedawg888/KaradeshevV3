using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShelterPanelControl : MonoBehaviour
{
    [Header("Roots")]
    public GameObject root;
    public Button closeButton;

    [Header("Header")]
    public TMP_Text titleText;

    [Header("Capacity Text")]
    public TMP_Text populationCapacityText;
    public TMP_Text familyCapacityText;

    [Header("Controls")]
    public Button pauseBirthingButton;
    public Image pauseBirthingIcon;

    [Header("Families List")]
    public Transform contentRoot;
    public FamilyCardUI familyCardPrefab;

    [Header("Optional Fallbacks")]
    [Tooltip("Optional fallback BuildingPanel if OpenFor() is called with a null parent (e.g. test scenes).")]
    [SerializeField] private BuildingPanelControl defaultParentPanel;

    // --- runtime ---
    private BuildingPanelControl _parentPanel;
    private BuildingControl _building;
    private TileControl _tile;
    private ShelterControl _shelter;
    private PlayerFamilySimulationManager _fam;

    private CanvasGroup _cg;

    // If true, don't show the Building panel on Hide() (used by Move flow)
    private bool _suppressReopenOnHide = false;

    public bool IsShowing => root != null && root.activeInHierarchy;

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

        if (closeButton)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() =>
            {
                _suppressReopenOnHide = false;
                Hide();
            });
        }

        if (pauseBirthingButton)
        {
            pauseBirthingButton.onClick.RemoveAllListeners();
            pauseBirthingButton.onClick.AddListener(TogglePauseBirthing);
        }
    }

    public void OpenFor(BuildingControl building, BuildingPanelControl parent, TileControl tile)
    {
        _parentPanel = parent != null ? parent : defaultParentPanel;
        _building = building;
        _tile = tile;
        _shelter = building ? building.GetComponent<ShelterControl>() : null;
        _fam = PlayerFamilySimulationManager.Instance;

        if (_shelter == null)
        {
            //Debug.LogError("[ShelterPanel] Building has no ShelterControl.");
            return;
        }

        if (titleText != null)
        {
            var name = !string.IsNullOrWhiteSpace(building.buildingName)
                ? building.buildingName
                : (BuildingManager.Instance?.GetBuildingByID(building.buildingID)?.buildingName ?? building.buildingID);

            if (_tile != null)
            {
                var gp = _tile.GetGridPosition();
                titleText.text = $"{name}";
            }
            else
            {
                titleText.text = name;
            }
        }

        UpdatePauseBirthingVisual();
        RefreshCapacityTexts();
        RefreshFamilies();

        if (root) root.SetActive(true);
        if (_cg != null)
        {
            _cg.alpha = 1f;
            _cg.interactable = true;
            _cg.blocksRaycasts = true;
        }
    }

    // We DO NOT reopen via Show(); we simply soft-show the existing Building panel instantly.
    public void Hide()
    {
        if (_cg != null)
        {
            _cg.alpha = 0f;
            _cg.interactable = false;
            _cg.blocksRaycasts = false;
        }

        if (root) root.SetActive(false);

        // Clear list (optional tidy; comment out to keep for quicker reopen)
        if (contentRoot)
        {
            for (int i = contentRoot.childCount - 1; i >= 0; i--)
                Destroy(contentRoot.GetChild(i).gameObject);
        }

        if (!_suppressReopenOnHide && _parentPanel != null)
        {
            _parentPanel.SoftShowFromChild(); // instant
        }

        _suppressReopenOnHide = false;
    }

    // --- Pause birthing button logic ---
    private void TogglePauseBirthing()
    {
        if (_shelter == null) return;

        _shelter.pauseBirthing = !_shelter.pauseBirthing;
        UpdatePauseBirthingVisual();
        RefreshCapacityTexts();
    }

    private void UpdatePauseBirthingVisual()
    {
        bool paused = _shelter != null && _shelter.pauseBirthing;
        if (pauseBirthingIcon != null)
            pauseBirthingIcon.color = paused ? Color.red : Color.white;

        if (pauseBirthingButton != null)
        {
            var cg = pauseBirthingButton.GetComponent<CanvasGroup>();
            if (cg == null) cg = pauseBirthingButton.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = paused ? 0.9f : 1f;
        }
    }

    private void RefreshCapacityTexts()
    {
        if (_shelter == null)
            return;

        if (populationCapacityText != null)
        {
            populationCapacityText.text =
                $"{_shelter.CurrentIndividualCount} \n {_shelter.individualCapacity}";
        }

        if (familyCapacityText != null)
        {
            familyCapacityText.text =
                $"{_shelter.CurrentFamilyCount} \n {_shelter.familyCapacity}";
        }
    }

    // --- Families list ---
    private void RefreshFamilies()
    {
        if (_shelter == null || contentRoot == null || familyCardPrefab == null)
        {
            RefreshCapacityTexts();
            return;
        }

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        var famMgr = _fam;
        if (famMgr == null)
        {
            RefreshCapacityTexts();
            return;
        }

        var ids = _shelter.HousedFamilyIds;
        if (ids == null)
        {
            RefreshCapacityTexts();
            return;
        }

        // Build a quick lookup so we don’t LINQ-scan the whole population for every family
        var people = famMgr.GetIndividuals();
        var byId = people
            .Where(p => p != null && !string.IsNullOrEmpty(p.Id))
            .ToDictionary(p => p.Id, p => p, StringComparer.Ordinal);

        foreach (var fid in ids)
        {
            var fam = famMgr.GetFamilyById(fid);
            if (fam == null) continue;

            bool maleAlive =
                !string.IsNullOrEmpty(fam.PartnerAId) &&
                byId.TryGetValue(fam.PartnerAId, out var male) &&
                male.IsAlive;

            bool femaleAlive =
                !string.IsNullOrEmpty(fam.PartnerBId) &&
                byId.TryGetValue(fam.PartnerBId, out var female) &&
                female.IsAlive;

            // Skip “empty” families (0 male + 0 female)
            if (!maleAlive && !femaleAlive)
                continue;

            var card = Instantiate(familyCardPrefab, contentRoot);
            card.Bind(
                fam,
                famMgr,
                onRequestMove: () => StartMoveFlow(fid),
                onRequestRename: null
            );
        }

        RefreshCapacityTexts();
    }

    // --- Move flow across shelters ---
    private void StartMoveFlow(string familyId)
    {
        // Hide the building panel too (without "closing" it)
        var parent = _parentPanel != null ? _parentPanel : defaultParentPanel;
        parent?.SoftHideForMoveMode();

        _suppressReopenOnHide = true; // don’t expose the building panel underneath
        Hide();

        ShelterControl.OnMoveFinished -= HandleMoveFinished;
        ShelterControl.OnMoveFinished += HandleMoveFinished;

        ShelterControl.BeginMoveMode(familyId, _shelter);
    }

    private void HandleMoveFinished(bool moved)
    {
        ShelterControl.OnMoveFinished -= HandleMoveFinished;

        // Restore building panel first
        var parent = _parentPanel != null ? _parentPanel : defaultParentPanel;
        parent?.SoftShowFromChild();

        if (_building != null && parent != null)
        {
            _suppressReopenOnHide = false;
            OpenFor(_building, parent, _tile); // reopen shelter panel
        }
    }
}
