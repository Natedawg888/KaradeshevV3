using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FamilyCardUI : MonoBehaviour
{
    [Header("Refs")]
    public TMP_Text titleText;
    public TMP_Text maleAgeText;
    public TMP_Text femaleAgeText;
    public TMP_Text childrenCountText;

    public Button moveButton;
    public Button renameButton;

    [Header("Inline Rename")]
    public TMP_InputField renameInput;
    public GameObject renameContainer;

    [Header("Pregnancy Cooldown")]
    public GameObject femaleCooldownContainer; // <-- NEW (assign a parent/panel for the cooldown)
    public TMP_Text   femaleCooldownText;      // existing label inside the container

    private System.Action _onRequestMove;

    private Family _boundFamily;
    private PlayerFamilySimulationManager _famMgr;

    // PartnerB is female in your setup
    private string _motherId;

    private void OnEnable()  => TurnSystem.SubscribeToEndOfTurn(OnEndTurn);
    private void OnDisable() => TurnSystem.UnsubscribeFromEndOfTurn(OnEndTurn);

    private void OnEndTurn() => UpdateFemaleCooldownUI();

    public void Bind(Family fam,
                     PlayerFamilySimulationManager famMgr,
                     System.Action onRequestMove,
                     System.Action onRequestRename /*unused*/)
    {
        _boundFamily   = fam;
        _famMgr        = famMgr;
        _onRequestMove = onRequestMove;

        string name = string.IsNullOrWhiteSpace(fam.FamilyName) ? "Family" : fam.FamilyName;
        if (titleText) titleText.text = name;

        int maleAge   = GetAgeTurns(famMgr, fam.PartnerAId);
        int femaleAge = GetAgeTurns(famMgr, fam.PartnerBId);

        if (maleAgeText)   maleAgeText.text   = $"{Mathf.Max(0, maleAge)}";
        if (femaleAgeText) femaleAgeText.text = $"{Mathf.Max(0, femaleAge)}";

        int kids = 0;
        if (fam.ChildrenIds != null && fam.ChildrenIds.Count > 0)
        {
            for (int i = 0; i < fam.ChildrenIds.Count; i++)
            {
                var ch = famMgr.GetIndividuals().FirstOrDefault(p => p.Id == fam.ChildrenIds[i]);
                if (ch != null && ch.IsAlive) kids++;
            }
        }
        if (childrenCountText) childrenCountText.text = $"{kids}";

        if (moveButton)
        {
            moveButton.onClick.RemoveAllListeners();
            moveButton.onClick.AddListener(() => _onRequestMove?.Invoke());
        }

        if (renameButton)
        {
            renameButton.onClick.RemoveAllListeners();
            renameButton.onClick.AddListener(BeginInlineRename);
        }

        if (renameInput)
        {
            SetRenameVisible(false);
            renameInput.onEndEdit.RemoveAllListeners();
            renameInput.onEndEdit.AddListener(CommitInlineRename);
            renameInput.onSubmit.RemoveAllListeners();
            renameInput.onSubmit.AddListener(CommitInlineRename);
        }

        _motherId = fam.PartnerBId;
        UpdateFemaleCooldownUI(); // initial paint
    }

    private void UpdateFemaleCooldownUI()
    {
        // Determine whether to show
        bool canShow = !string.IsNullOrEmpty(_motherId) && _famMgr != null;
        int turns = canShow ? _famMgr.GetParentCooldownTurnsLeft(_motherId) : 0;
        bool show = canShow && turns > 0;

        // Toggle container if assigned, otherwise toggle the text directly
        if (femaleCooldownContainer != null)
            femaleCooldownContainer.SetActive(show);

        if (femaleCooldownText != null)
        {
            femaleCooldownText.gameObject.SetActive(show || femaleCooldownContainer == null);
            if (show) femaleCooldownText.text = $"{turns}";
        }
    }

    private void BeginInlineRename()
    {
        if (renameInput == null || _boundFamily == null) return;

        string current = string.IsNullOrWhiteSpace(_boundFamily.FamilyName) ? "Family" : _boundFamily.FamilyName;
        renameInput.text = current;

        SetRenameVisible(true);
        renameInput.ActivateInputField();
        renameInput.Select();
        renameInput.caretPosition = renameInput.text.Length;
    }

    private void CommitInlineRename(string newName)
    {
        if (_boundFamily == null || renameInput == null) return;

        string trimmed = (newName ?? "").Trim();
        if (!string.IsNullOrEmpty(trimmed))
        {
            _boundFamily.FamilyName = trimmed;
            if (titleText) titleText.text = trimmed;
        }
        SetRenameVisible(false);
    }

    private void SetRenameVisible(bool editing)
    {
        if (titleText) titleText.gameObject.SetActive(!editing);

        if (renameContainer != null)
            renameContainer.SetActive(editing);
        else if (renameInput != null)
            renameInput.gameObject.SetActive(editing);

        if (renameButton) renameButton.interactable = !editing;
    }

    private int GetAgeTurns(PlayerFamilySimulationManager mgr, string id)
    {
        if (mgr == null || string.IsNullOrEmpty(id)) return 0;
        var p = mgr.GetIndividuals().FirstOrDefault(i => i.Id == id);
        return p != null && p.IsAlive ? p.AgeInTurns : 0;
    }
}