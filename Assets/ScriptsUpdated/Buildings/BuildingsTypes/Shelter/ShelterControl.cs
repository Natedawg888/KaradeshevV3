using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;

public class ShelterControl : MonoBehaviour
{
    [Header("Cap & Capacity")]
    public int populationCapBonus = 30;
    public int familyCapacity     = 10;  // max families
    public int individualCapacity = 40;  // max individuals

    [Header("Housing Rules")]
    public bool requireHousedForPairing = true;

    [Header("Orders")]
    public int maxConcurrentOrders = 8;
    public int ordersPerCycle = 2;

    [Header("UI: Capacity Icon Only")]
    public Image capacityImage;
    public Image individualCapacityImage;
    public Sprite halfCapacityIcon;
    public Sprite fullCapacityIcon;

    [Header("Icon Rules")]
    public bool hideWhenBelowHalf = true;
    public bool hideWhenDestroyed = true;

    [Header("Birth Orders UI (optional)")]
    public Transform        ordersUIRoot;
    public BirthOrderWidget orderWidgetPrefab;

    [Header("Shelter Tier / Level")]
    public int shelterLevel = 0;

    [Header("Shelter Controls")]
    public bool pauseBirthing = false;

    [Header("World Move UI")]
    public Button moveHereButton;
    public GameObject moveHereRoot;

    [Header("Tornado Casualties")]
    public bool tornadoCanKillHousedPopulation = true;

    [Range(0f, 1f)] public float tornadoChildDeathChance = 0.12f;
    [Range(0f, 1f)] public float tornadoTeenDeathChance = 0.08f;
    [Range(0f, 1f)] public float tornadoAdultDeathChance = 0.06f;
    [Range(0f, 1f)] public float tornadoElderDeathChance = 0.16f;

    [Tooltip("Extra shelter-wide multiplier for tornado death chance.")]
    [Min(0f)] public float tornadoCasualtyChanceMultiplier = 1f;

    [Tooltip("Optional safety: don't roll tornado death on mothers with active birth orders.")]
    public bool tornadoSkipProtectedMothers = true;

    [Header("Fire Casualties")]
    public bool fireCanKillHousedPopulation = true;

    [Range(0f, 1f)] public float fireChildDeathChance = 0.05f;
    [Range(0f, 1f)] public float fireTeenDeathChance = 0.03f;
    [Range(0f, 1f)] public float fireAdultDeathChance = 0.02f;
    [Range(0f, 1f)] public float fireElderDeathChance = 0.07f;

    [Tooltip("Extra shelter-wide multiplier for fire death chance.")]
    [Min(0f)] public float fireCasualtyChanceMultiplier = 1f;

    [Tooltip("Optional safety: don't roll fire death on mothers with active birth orders.")]
    public bool fireSkipProtectedMothers = true;

    [Header("Volcanic Exposure")]
    public bool volcanicCanHarmHousedPopulation = true;

    public bool ashCanAffectHousedPopulation = true;
    public bool acidRainCanAffectHousedPopulation = true;

    [Range(0f, 1f)] public float ashExposureChance = 0.18f;
    [Range(0f, 1f)] public float acidRainExposureChance = 0.34f;

    [Range(0f, 1f)] public float ashHealthLossMin = 0.02f;
    [Range(0f, 1f)] public float ashHealthLossMax = 0.06f;

    [Range(0f, 1f)] public float acidRainHealthLossMin = 0.05f;
    [Range(0f, 1f)] public float acidRainHealthLossMax = 0.12f;

    [Tooltip("Do not let volcanic exposure reduce health below this floor. This system injures, it does not kill.")]
    [Range(0f, 1f)] public float minimumHealthAfterVolcanicExposure = 0.05f;

    [Tooltip("Mothers with active pregnancy orders/current pregnancy are more vulnerable to volcanic exposure.")]
    [Min(0f)] public float activePregnancyExposureMultiplier = 1.35f;

    [Header("Environmental Disease Exposure")]
    public bool shelterWeatherDiseaseExposure = true;

    [Tooltip("Shelters protect population, so this should usually stay low.")]
    [Range(0f, 1f)]
    public float shelterWeatherDiseaseChanceMultiplier = 0.45f;

    [Tooltip("Shelters reduce exposure strength compared with outdoor tasks.")]
    [Range(0f, 1f)]
    public float shelterWeatherDiseaseExposureMultiplier = 0.65f;

    [Tooltip("0 means let each EnvironmentalDiseaseRisk decide.")]
    [Min(0)]
    public int maxShelterWeatherDiseaseTargetsPerTurn = 0;

    public bool debugShelterWeatherDiseaseExposure = false;

    private readonly List<string> _tmpShelterWeatherDiseaseIds = new();

    [Header("Virus Shelter Spread")]
    public bool enableShelterVirusSpread = true;

    [Tooltip("Shelter-specific spread multiplier. Higher = crowded shelter spread is stronger.")]
    [Min(0f)] public float shelterVirusSpreadMultiplier = 1f;

    public bool debugShelterVirusSpread = false;

    private BuildingDiseaseExposureSource _buildingDiseaseExposure;

    [Header("Shelter Crowding Building Disease")]
    public bool requireOverHalfHousedForBuildingDiseaseExposure = true;

    [Tooltip("0.5 means building disease exposure only runs when the shelter is over 50% full.")]
    [Range(0f, 1f)]
    public float buildingDiseaseMinOccupancyRatio = 0.5f;

    [Tooltip("Use this for close-contact/crowding diseases like Common Cold, Adenovirus, etc.")]
    public bool applyBuildingDiseaseToAnyHousedPopulation = true;

    [Tooltip("Use this only if you also have risks meant for resting/unbusy housed people.")]
    public bool applyBuildingDiseaseToUnbusyHousedPopulation = false;

    public bool debugShelterBuildingDiseaseGate = false;

    private readonly List<string> _tmpShelterVirusSpreadIds = new();

    // --- runtime ---
    private PlayersPopulationManager      playerPop;
    private PlayerFamilySimulationManager familySim;
    private BuildingStatus _status;

    private bool _capGranted = false;

    private int turnsUntilNextPairing;
    private readonly List<PopulationBirthOrder>           activeOrders = new();
    private readonly Dictionary<string, BirthOrderWidget> widgets      = new();
    [SerializeField] private List<string> housedFamilyIds = new();
    [SerializeField] private List<string> _debugActiveOrders = new();
    public IReadOnlyList<string> HousedFamilyIds => housedFamilyIds;

    [SerializeField] private List<string> housedIndividualIds = new();
    public IReadOnlyList<string> HousedIndividualIds => housedIndividualIds;

    public int CurrentFamilyCount => housedFamilyIds?.Count ?? 0;
    public int CurrentIndividualCount => housedIndividualIds?.Count ?? 0;

    [SerializeField] private List<string> guestIndividualIds = new();
    [SerializeField] private List<string> movedOutIndividualIds = new();

    public IReadOnlyList<string> GuestIndividualIds => guestIndividualIds;
    public IReadOnlyList<string> MovedOutIndividualIds => movedOutIndividualIds;

    private static readonly List<ShelterControl> s_all = new();
    private static bool   s_moveActive;
    private static string s_pendingFamilyId;
    private static ShelterControl s_sourceShelter;
    public static System.Action<bool> OnMoveFinished;
    public static bool IsMoveModeActive => s_moveActive;

    public static event System.Action<bool> OnMoveModeChanged; // true=start, false=end
    public static bool IsMoveActive => s_moveActive;

    private PregnancyService _pregSvc;

    private bool _canCreateOrders = true;

    private void OnEnable()
    {
        s_all.Add(this);

        if (moveHereButton)
        {
            moveHereButton.onClick.RemoveAllListeners();
            moveHereButton.onClick.AddListener(PerformMoveToThisShelter);
        }

        RefreshMoveHereUI();
    }

    private void OnDisable()
    {
        if (_status != null) _status.OnStateChanged -= HandleBuildingStateChanged;
        s_all.Remove(this);
    }

    private void Start()
    {
        _status  = GetComponent<BuildingStatus>();
        _buildingDiseaseExposure = GetComponent<BuildingDiseaseExposureSource>();
        playerPop = PlayersPopulationManager.Instance;
        familySim = PlayerFamilySimulationManager.Instance;

        if (playerPop == null || familySim == null)
        {
            //Debug.LogError("[Shelter] Missing required managers.");
            enabled = false;
            return;
        }

        if (_status != null)
        {
            _status.OnStateChanged += HandleBuildingStateChanged;
            HandleBuildingStateChanged(_status.CurrentState);
        }
        else
        {
            GrantCapIfNeeded();
        }

        var field = typeof(PlayerFamilySimulationManager)
            .GetField("_pregnancySvc", BindingFlags.NonPublic | BindingFlags.Instance);
        _pregSvc = field?.GetValue(familySim) as PregnancyService;
        if (_pregSvc != null)
            _pregSvc.OnPregnancyFailed += HandlePregnancyFailed;

        FillFamilySlots();

        SyncHousedIndividualsFromFamilies();
        EnforceIndividualCapByTrimming();
        // Dedupe any accidental overlaps on startup
        PruneInvalidOrEmptyFamilies();
        SyncDebugOrders();
        UpdateCapacityIcon();
    }

    private void OnDestroy()
    {
        if (_pregSvc != null)
            _pregSvc.OnPregnancyFailed -= HandlePregnancyFailed;

        if (_status != null) _status.OnStateChanged -= HandleBuildingStateChanged;

        if (_capGranted && playerPop != null)
        {
            playerPop.maxPopulation = Mathf.Max(0, playerPop.maxPopulation - populationCapBonus);
            _capGranted = false;
        }

        foreach (var o in activeOrders)
            familySim?.AbortPregnancy(o.MotherId);
        activeOrders.Clear();

        foreach (var w in widgets.Values)
            if (w != null) Destroy(w.gameObject);
        widgets.Clear();

        SyncDebugOrders();
    }

    public void RunEndTurnMaintenance()
    {
        if (!isActiveAndEnabled) return;

        // progress gestations / widgets
        for (int i = activeOrders.Count - 1; i >= 0; i--)
        {
            if (i < 0 || i >= activeOrders.Count) break;

            var o = activeOrders[i];
            if (o == null)
            {
                activeOrders.RemoveAt(i);
                continue;
            }

            o.TurnsRemaining = Mathf.Max(0, o.TurnsRemaining - 1);
            UpdateWidget(o);

            if (o.TurnsRemaining <= 0)
            {
                RemoveOrderById(o.OrderId, abortIfPending: false);
            }
        }

        PruneInvalidOrEmptyFamilies();
        FillFamilySlots();
        SyncHousedIndividualsFromFamilies();
        EnforceIndividualCapByTrimming();

        TrySpreadVirusesInShelter();

        TryApplyShelterWeatherDiseaseExposure(debugLogging: false);

        TryApplyShelterCrowdingBuildingDiseaseExposure();

        SyncDebugOrders();
        UpdateCapacityIcon();
    }

    public void RunEndTurnPairingStep()
    {
        if (!isActiveAndEnabled) return;

        TryCreatePairsAndStartGestations(ordersPerCycle);

        RefreshHousingTracking(false);
        SyncDebugOrders();
        UpdateCapacityIcon();
    }

    private void HandleBuildingStateChanged(BuildingState s)
    {
        switch (s)
        {
            case BuildingState.Normal:
                GrantCapIfNeeded();
                _canCreateOrders = true;
                break;

            case BuildingState.Damaged:
                GrantCapIfNeeded();
                _canCreateOrders = false;
                break;

            case BuildingState.Destroyed:
                RevokeCapIfApplied();
                _canCreateOrders = false;
                FailAllOrders("Shelter destroyed");
                EjectAllFamilies();
                break;
        }

        UpdateCapacityIcon();
    }

    private void HandlePregnancyFailed(string motherId)
    {
        var toRemove = new List<string>();
        for (int i = 0; i < activeOrders.Count; i++)
        {
            var o = activeOrders[i];
            if (o != null && o.MotherId == motherId)
                toRemove.Add(o.OrderId);
        }
        for (int i = 0; i < toRemove.Count; i++)
            RemoveOrderById(toRemove[i], abortIfPending:false);
    }

    // ---------- Housing ----------

    private void GrantCapIfNeeded()
    {
        if (playerPop == null)
            return;

        // During load, maxPopulation is restored from save.
        // Do not add the shelter bonus again.
        if (IsSaveLoadInProgress())
        {
            _capGranted = true;
            return;
        }

        if (!_capGranted)
        {
            playerPop.maxPopulation += populationCapBonus;
            _capGranted = true;
        }
    }

    private void RevokeCapIfApplied()
    {
        if (playerPop == null)
            return;

        // During load, don't modify loaded population totals.
        // Just sync internal flag state.
        if (IsSaveLoadInProgress())
        {
            _capGranted = false;
            return;
        }

        if (_capGranted)
        {
            playerPop.maxPopulation = Mathf.Max(0, playerPop.maxPopulation - populationCapBonus);
            _capGranted = false;
        }
    }

    private void SyncCapStateFromLoadedStatusWithoutChangingPopulation()
    {
        if (_status != null && _status.CurrentState == BuildingState.Destroyed)
        {
            _capGranted = false;
            _canCreateOrders = false;
        }
        else
        {
            _capGranted = true;
        }
    }

    private void EjectAllFamilies()
    {
        if (housedFamilyIds == null) housedFamilyIds = new List<string>();
        if (guestIndividualIds == null) guestIndividualIds = new List<string>();
        if (movedOutIndividualIds == null) movedOutIndividualIds = new List<string>();

        housedFamilyIds.Clear();
        guestIndividualIds.Clear();
        movedOutIndividualIds.Clear();
        housedIndividualIds.Clear();

        UpdateCapacityIcon();
    }

    private void PruneInvalidOrEmptyFamilies()
    {
        if (familySim == null || housedFamilyIds == null) return;

        housedFamilyIds.RemoveAll(fid =>
        {
            if (string.IsNullOrEmpty(fid)) return true;

            var fam = familySim.GetFamilyById(fid);
            if (fam == null) return true;

            var owner = GetShelterHousingFamily(fid);
            if (owner != null && owner != this)
                return true;

            if (!FamilyHasAnyLivingMembers(fam))
                return true;

            return false;
        });

        PruneExtraResidentLists();
        SyncHousedIndividualsFromFamilies();
        EnforceIndividualCapByTrimming();
    }

    private bool FamilyHasAnyLivingMembers(object familyObj)
    {
        if (familyObj == null) return false;
        if (!TryEnumerateFamilyIndividuals(familyObj, out var members)) return false;

        for (int i = 0; i < members.Count; i++)
        {
            var ind = members[i];
            if (ind != null && ind.IsAlive)
                return true;
        }

        return false;
    }

    private void FillFamilySlots()
    {
        if (_status != null && _status.CurrentState == BuildingState.Destroyed) return;
        if (housedFamilyIds == null) housedFamilyIds = new List<string>();
        if (familySim == null) return;

        var fams = familySim.GetFamilies();
        if (fams == null || fams.Count == 0) return;

        int currentIndividuals = GetCurrentIndividualsCount();

        for (int i = 0; i < fams.Count; i++)
        {
            if (housedFamilyIds.Count >= familyCapacity) break;

            var f = fams[i];
            if (f == null) continue;
            if (string.IsNullOrEmpty(f.FamilyId)) continue;
            if (housedFamilyIds.Contains(f.FamilyId)) continue;

            // NEW: skip if some other shelter already houses this family
            var owner = GetShelterHousingFamily(f.FamilyId);
            if (owner != null && owner != this) continue;

            int famSize = TryGetFamilySize(f, out var sz) ? sz : 0;

            if (currentIndividuals + famSize > Mathf.Max(0, individualCapacity))
                continue;

            housedFamilyIds.Add(f.FamilyId);
            currentIndividuals += famSize;

            if (housedFamilyIds.Count >= familyCapacity) break;
            if (currentIndividuals >= individualCapacity) break;
        }
    }

    public bool TryAssignFamily(string familyId)
    {
        if (_status != null && _status.CurrentState == BuildingState.Destroyed) return false;
        if (string.IsNullOrEmpty(familyId)) return false;
        if (housedFamilyIds.Contains(familyId))
        {
            RefreshHousingTracking();
            return true;
        }

        if (housedFamilyIds.Count >= familyCapacity) return false;

        var currentOwner = GetShelterHousingFamily(familyId);
        if (currentOwner != null && currentOwner != this)
        {
            if (!(s_moveActive && currentOwner == s_sourceShelter))
                return false;
        }

        var fam = familySim != null ? familySim.GetFamilyById(familyId) : null;
        if (fam == null) return false;

        int famSize = TryGetFamilySize(fam, out var sz) ? sz : 0;
        int currentIndividuals = GetCurrentIndividualsCount();

        if (currentIndividuals + famSize > Mathf.Max(0, individualCapacity))
            return false;

        housedFamilyIds.Add(familyId);

        movedOutIndividualIds.RemoveAll(id =>
        {
            var ind = GetIndividual(id);
            return ind != null && ind.FamilyId == familyId;
        });

        RefreshHousingTracking();
        return true;
    }

    public void UnassignFamily(string familyId)
    {
        if (!housedFamilyIds.Remove(familyId))
            return;

        movedOutIndividualIds.RemoveAll(id =>
        {
            var ind = GetIndividual(id);
            return ind != null && ind.FamilyId == familyId;
        });

        RefreshHousingTracking();
    }

    private static ShelterControl GetShelterHousingIndividual(string individualId)
    {
        if (string.IsNullOrEmpty(individualId)) return null;

        var shelters = GetAllSheltersSnapshot();
        for (int i = 0; i < shelters.Count; i++)
        {
            var sc = shelters[i];
            if (sc != null && sc.housedIndividualIds != null && sc.housedIndividualIds.Contains(individualId))
                return sc;
        }

        return null;
    }

    private void SyncHousedIndividualsFromFamilies()
    {
        if (familySim == null) return;

        if (housedFamilyIds == null) housedFamilyIds = new List<string>();
        if (housedIndividualIds == null) housedIndividualIds = new List<string>();
        if (guestIndividualIds == null) guestIndividualIds = new List<string>();
        if (movedOutIndividualIds == null) movedOutIndividualIds = new List<string>();

        PruneExtraResidentLists();

        var next = new HashSet<string>(System.StringComparer.Ordinal);
        var movedOutSet = new HashSet<string>(movedOutIndividualIds, System.StringComparer.Ordinal);

        // Add members from housed families, except anyone explicitly moved out
        for (int i = 0; i < housedFamilyIds.Count; i++)
        {
            var fam = familySim.GetFamilyById(housedFamilyIds[i]);
            if (fam == null) continue;

            if (!TryEnumerateFamilyIndividuals(fam, out var members))
                continue;

            for (int m = 0; m < members.Count; m++)
            {
                var ind = members[m];
                if (ind == null || !ind.IsAlive) continue;
                if (movedOutSet.Contains(ind.Id)) continue;

                next.Add(ind.Id);
            }
        }

        // Add guests
        for (int i = 0; i < guestIndividualIds.Count; i++)
        {
            string id = guestIndividualIds[i];
            if (string.IsNullOrEmpty(id)) continue;

            var ind = GetIndividual(id);
            if (ind == null || !ind.IsAlive) continue;

            next.Add(id);
        }

        housedIndividualIds.Clear();
        housedIndividualIds.AddRange(next);
    }

    private bool TryEnumerateFamilyIndividuals(object familyObj, out List<Individual> membersOut)
    {
        var result = new List<Individual>();

        if (familyObj == null || familySim == null)
        {
            membersOut = result;
            return false;
        }

        // Strong path: actual Family model
        if (familyObj is Family fam)
        {
            var seen = new HashSet<string>(System.StringComparer.Ordinal);

            if (!string.IsNullOrEmpty(fam.PartnerAId) && seen.Add(fam.PartnerAId))
            {
                var ind = GetIndividual(fam.PartnerAId);
                if (ind != null)
                    result.Add(ind);
            }

            if (!string.IsNullOrEmpty(fam.PartnerBId) && seen.Add(fam.PartnerBId))
            {
                var ind = GetIndividual(fam.PartnerBId);
                if (ind != null)
                    result.Add(ind);
            }

            if (fam.ChildrenIds != null)
            {
                for (int i = 0; i < fam.ChildrenIds.Count; i++)
                {
                    string childId = fam.ChildrenIds[i];
                    if (string.IsNullOrEmpty(childId)) continue;
                    if (!seen.Add(childId)) continue;

                    var ind = GetIndividual(childId);
                    if (ind != null)
                        result.Add(ind);
                }
            }

            membersOut = result;
            return true;
        }

        // Fallback reflection path
        var t = familyObj.GetType();
        var pMembers = t.GetProperty("Members") ?? t.GetProperty("Individuals") ?? t.GetProperty("People");
        if (pMembers == null)
        {
            membersOut = result;
            return false;
        }

        var val = pMembers.GetValue(familyObj) as System.Collections.IEnumerable;
        if (val == null)
        {
            membersOut = result;
            return false;
        }

        var e = val.GetEnumerator();
        while (e.MoveNext())
        {
            if (e.Current is Individual ind)
                result.Add(ind);
        }

        membersOut = result;
        return true;
    }

    private void EnforceIndividualCapByTrimming()
    {
        if (individualCapacity <= 0 || familySim == null) return;

        SyncHousedIndividualsFromFamilies();

        if (GetCurrentIndividualsCount() <= individualCapacity)
        {
            UpdateCapacityIcon();
            return;
        }

        TryMoveOverflowIndividualsToOtherShelters();
        SyncHousedIndividualsFromFamilies();
        UpdateCapacityIcon();
    }

    private void RemoveEmptyFamiliesWhoseMembersNotPresent()
    {
        if (housedFamilyIds == null || familySim == null) return;

        var set = new HashSet<string>(housedIndividualIds, System.StringComparer.Ordinal);

        for (int i = housedFamilyIds.Count - 1; i >= 0; i--)
        {
            var fam = familySim.GetFamilyById(housedFamilyIds[i]);
            if (fam == null) { housedFamilyIds.RemoveAt(i); continue; }

            if (!TryEnumerateFamilyIndividuals(fam, out var members))
                continue;

            bool anyStillHoused = false;
            for (int m = 0; m < members.Count; m++)
            {
                var ind = members[m];
                if (ind != null && ind.IsAlive && set.Contains(ind.Id)) { anyStillHoused = true; break; }
            }

            if (!anyStillHoused)
                housedFamilyIds.RemoveAt(i);
        }
    }

    // ---------- Capacity Icon ----------
    private void UpdateCapacityIcon()
    {
        bool destroyed = (_status != null && _status.CurrentState == BuildingState.Destroyed);

        int famCap    = Mathf.Max(1, familyCapacity);
        int famCount  = Mathf.Clamp(housedFamilyIds?.Count ?? 0, 0, famCap);
        float famRatio = famCount / (float)famCap;

        int indCap    = Mathf.Max(1, individualCapacity);
        int indCount  = Mathf.Clamp(GetCurrentIndividualsCount(), 0, indCap);
        float indRatio = indCount / (float)indCap;

        if (hideWhenDestroyed && destroyed)
        {
            if (capacityImage)           capacityImage.gameObject.SetActive(false);
            if (individualCapacityImage) individualCapacityImage.gameObject.SetActive(false);
            return;
        }

        const float threshold = 0.5f;

        if (capacityImage)
        {
            bool showFam = famRatio >= threshold;
            capacityImage.gameObject.SetActive(showFam);
            if (showFam)
            {
                capacityImage.sprite = (famRatio >= 1f) ? fullCapacityIcon : halfCapacityIcon;
            }
        }

        if (individualCapacityImage)
        {
            bool showInd = indRatio >= threshold;
            individualCapacityImage.gameObject.SetActive(showInd);
            if (showInd)
            {
                individualCapacityImage.sprite = (indRatio >= 1f) ? fullCapacityIcon : halfCapacityIcon;
            }
        }
    }

    private static void ApplyIconState(
        Image target,
        int count,
        int cap,
        Sprite halfIcon,
        Sprite fullIcon,
        bool hideWhenBelowHalf)
    {
        if (target == null) return;

        if (count >= cap)
        {
            target.sprite = fullIcon;
            target.gameObject.SetActive(fullIcon != null || !hideWhenBelowHalf);
        }
        else if (count >= Mathf.CeilToInt(cap * 0.5f))
        {
            target.sprite = halfIcon;
            target.gameObject.SetActive(halfIcon != null || !hideWhenBelowHalf);
        }
        else
        {
            if (hideWhenBelowHalf)
            {
                target.gameObject.SetActive(false);
            }
            else
            {
                target.sprite = null;
                target.gameObject.SetActive(true);
            }
        }
    }

    // ---------- Pairing / Gestation ----------
    private void TryCreatePairsAndStartGestations(int requestedCount)
    {
        if (_status != null && _status.CurrentState == BuildingState.Destroyed) return;
        //if (pauseBirthing) { Debug.Log("[Shelter] birthing paused"); return; }
        //if (!_canCreateOrders) { Debug.Log("[Shelter] blocked: damaged"); return; }
//
        //if (requireHousedForPairing && housedFamilyIds.Count == 0)
        //{
            //Debug.Log("[Shelter] blocked: no housed families");
            return;
        }
        if (requestedCount <= 0) return;
        if (familySim == null || playerPop == null) return;

        int roomForOrders = Mathf.Max(0, maxConcurrentOrders - activeOrders.Count);
        //if (roomForOrders <= 0) { Debug.Log("[Shelter] blocked: at order capacity"); return; }
        //int toStart = Mathf.Min(requestedCount, roomForOrders);

        var cfg = familySim.GetConfig();
        float minH = cfg != null ? cfg.minHealthForBirth        : 0.6f;
        int   minA = cfg != null ? cfg.minAdultAgeForBirthTurns : 180;
        int   maxA = cfg != null ? cfg.maxAdultAgeForBirthTurns : 525;
        int gest = cfg != null ? Mathf.Max(1, cfg.gestationTurns) : 15;

        var mothersWithActiveOrders = new HashSet<string>(activeOrders.Select(o => o.MotherId));

        var candidates = new List<(Individual mother, Individual father)>();
        var got = familySim.CollectPairsForFamilies(
            housedFamilyIds,
            minH, minA, maxA,
            candidates,
            int.MaxValue
        );

        if (_pregSvc != null)
        {
            candidates.RemoveAll(p =>
                _pregSvc.IsOnParentCooldown(p.mother.Id) ||
                _pregSvc.IsMotherCurrentlyPregnant(p.mother.Id) ||
                mothersWithActiveOrders.Contains(p.mother.Id));
        }
        else
        {
            candidates.RemoveAll(p => mothersWithActiveOrders.Contains(p.mother.Id));
        }

        // NEW: only let this shelter act on mothers physically living here
        candidates.RemoveAll(p =>
            p.mother == null ||
            !IsIndividualCurrentlyHousedHere(p.mother.Id));

        int started = 0;
        for (int i = 0; i < candidates.Count && started < toStart; i++)
        {
            var (mom, dad) = candidates[i];

            if (mom == null || dad == null)
                continue;

            if (!IsIndividualCurrentlyHousedHere(mom.Id))
                continue;

            if (mothersWithActiveOrders.Contains(mom.Id))
                continue;

            if (_pregSvc != null && _pregSvc.IsOnParentCooldown(mom.Id))
                continue;

            if (_pregSvc != null && _pregSvc.IsMotherCurrentlyPregnant(mom.Id))
                continue;

            if (!TryResolvePairShelterForPairing(mom, dad, out var targetShelter))
                continue;

            if (targetShelter == null)
                continue;

            if (!targetShelter.TryStartGestationOrderHere(mom, dad, gest))
                continue;

            mothersWithActiveOrders.Add(mom.Id);
            started++;
        }

        if (started == 0)
        {
            //Debug.Log("[Shelter] no pregnancies could be started this cycle (cooldowns/needs/reservations).");
        }

        SyncDebugOrders();
    }

    private void ResolveBirth(PopulationBirthOrder order)
    {
        if (order == null) return;

        var mother = GetIndividual(order.MotherId);
        var father = GetIndividual(order.FatherId);

        int born = familySim.ResolveBirthAndReturnChildrenCount(mother, father);
        if (born > 1) {}
            //Debug.Log($"[Shelter] Multiples born: {born} babies.");

        SyncHousedIndividualsFromFamilies();
        EnforceIndividualCapByTrimming();
    }

    private Individual GetIndividual(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        if (familySim == null)
            familySim = PlayerFamilySimulationManager.Instance;

        if (familySim == null)
            return null;

        var individuals = familySim.GetIndividuals();
        if (individuals == null)
            return null;

        for (int i = 0; i < individuals.Count; i++)
        {
            var ind = individuals[i];
            if (ind == null)
                continue;

            if (ind.Id == id)
                return ind;
        }

        return null;
    }

    private void RemoveOrderById(string orderId, bool abortIfPending)
    {
        if (string.IsNullOrEmpty(orderId)) return;

        int idx = -1;
        for (int i = 0; i < activeOrders.Count; i++)
        {
            if (activeOrders[i] != null && activeOrders[i].OrderId == orderId)
            {
                idx = i; break;
            }
        }
        if (idx < 0 || idx >= activeOrders.Count) return;

        var o = activeOrders[idx];

        if (abortIfPending && o != null && familySim != null)
            familySim.AbortPregnancy(o.MotherId);

        activeOrders.RemoveAt(idx);

        if (widgets.TryGetValue(orderId, out var w) && w != null)
            Destroy(w.gameObject);
        widgets.Remove(orderId);

        SyncDebugOrders();
    }

    private void SpawnWidget(PopulationBirthOrder order)
    {
        if (ordersUIRoot == null || orderWidgetPrefab == null) return;
        var cfg = familySim?.GetConfig();
        int gest = cfg != null ? Mathf.Max(1, cfg.gestationTurns) : 10;

        var w = Instantiate(orderWidgetPrefab, ordersUIRoot);
        w.Bind(order.OrderId, maxTurns: gest);
        w.UpdateTurns(order.TurnsRemaining);
        widgets[order.OrderId] = w;
    }

    private void UpdateWidget(PopulationBirthOrder order)
    {
        if (order == null) return;
        if (widgets.TryGetValue(order.OrderId, out var w) && w != null)
            w.UpdateTurns(order.TurnsRemaining);
    }

    private void SyncDebugOrders()
    {
        _debugActiveOrders.Clear();
        for (int i = 0; i < activeOrders.Count; i++)
        {
            var o = activeOrders[i];
            _debugActiveOrders.Add(
                $"{i + 1}. Fam:{o.FamilyId} Mom:{o.MotherId} Dad:{o.FatherId} T:{o.TurnsRemaining} Res:{o.ReservationId}"
            );
        }
    }

    private void FailAllOrders(string reason = null)
    {
        var ids = new List<string>(activeOrders.Count);
        for (int i = 0; i < activeOrders.Count; i++)
            if (activeOrders[i] != null) ids.Add(activeOrders[i].OrderId);

        for (int i = 0; i < ids.Count; i++)
            RemoveOrderById(ids[i], abortIfPending:true);

        if (!string.IsNullOrEmpty(reason)) {}
            //Debug.Log($"[Shelter] All orders failed: {reason}");
    }

    // ---------- Helpers ----------
    private int GetCurrentIndividualsCount()
    {
        return Mathf.Clamp(housedIndividualIds?.Count ?? 0, 0, int.MaxValue);
    }

    private bool TryGetFamilySize(object familyObj, out int size)
    {
        size = 0;
        if (familyObj == null) return false;

        if (TryEnumerateFamilyIndividuals(familyObj, out var members))
        {
            int living = 0;
            for (int i = 0; i < members.Count; i++)
            {
                var ind = members[i];
                if (ind != null && ind.IsAlive)
                    living++;
            }

            size = living;
            return true;
        }

        var t = familyObj.GetType();

        var pSize = t.GetProperty("LivingCount")
                 ?? t.GetProperty("Size")
                 ?? t.GetProperty("MemberCount")
                 ?? t.GetProperty("Count");

        if (pSize != null && pSize.PropertyType == typeof(int))
        {
            size = (int)pSize.GetValue(familyObj);
            return true;
        }

        return false;
    }

    // --- GLOBAL HOUSING LOOKUPS (NEW) ---
    private static ShelterControl GetShelterHousingFamily(string familyId)
    {
        if (string.IsNullOrEmpty(familyId)) return null;

        var shelters = GetAllSheltersSnapshot();
        for (int i = 0; i < shelters.Count; i++)
        {
            var sc = shelters[i];
            if (sc != null && sc.housedFamilyIds != null && sc.housedFamilyIds.Contains(familyId))
                return sc;
        }

        return null;
    }

    public static void BeginMoveMode(string familyId, ShelterControl source)
    {
        s_pendingFamilyId = familyId;
        s_sourceShelter   = source;
        s_moveActive      = true;

        OnMoveModeChanged?.Invoke(true);

        for (int i = 0; i < s_all.Count; i++)
            s_all[i].RefreshMoveHereUI();
    }

    public static void CancelMoveMode(bool moved = false)
    {
        s_moveActive = false;
        s_pendingFamilyId = null;
        s_sourceShelter = null;

        for (int i = 0; i < s_all.Count; i++)
            s_all[i].RefreshMoveHereUI();

        OnMoveModeChanged?.Invoke(false);
        OnMoveFinished?.Invoke(moved);
    }

    private void RefreshMoveHereUI()
    {
        bool show = false;

        if (s_moveActive && this != s_sourceShelter && familySim != null && !string.IsNullOrEmpty(s_pendingFamilyId))
        {
            show = CanAcceptFamily(s_pendingFamilyId, familySim);
        }

        if (moveHereRoot) moveHereRoot.SetActive(show);
        if (moveHereButton) moveHereButton.gameObject.SetActive(show);
    }

    public bool CanAcceptFamily(string familyId, PlayerFamilySimulationManager famMgr)
    {
        if (_status != null && _status.CurrentState == BuildingState.Destroyed) return false;
        if (string.IsNullOrEmpty(familyId) || famMgr == null) return false;

        if (housedFamilyIds.Contains(familyId)) return true; // already here
        if (housedFamilyIds.Count >= familyCapacity) return false;

        // NEW: refuse if another shelter owns it, unless this is the active move target from that source
        var owner = GetShelterHousingFamily(familyId);
        if (owner != null && owner != this)
        {
            if (!(s_moveActive && owner == s_sourceShelter))
                return false;
        }

        var fam = famMgr.GetFamilyById(familyId);
        if (fam == null) return false;

        int famSize = TryGetFamilySize(fam, out var sz) ? sz : 0;
        int currentIndividuals = GetCurrentIndividualsCount();

        if (currentIndividuals + famSize > Mathf.Max(0, individualCapacity))
            return false;

        return true;
    }

    private void PerformMoveToThisShelter()
    {
        if (!s_moveActive || string.IsNullOrEmpty(s_pendingFamilyId) || s_sourceShelter == null)
        {
            CancelMoveMode(false);
            return;
        }

        if (!CanAcceptFamily(s_pendingFamilyId, familySim))
        {
            //Debug.Log("[Shelter] Target no longer available.");
            CancelMoveMode(false);
            return;
        }

        if (TryAssignFamily(s_pendingFamilyId))
        {
            s_sourceShelter.TransferBirthOrdersForFamilyTo(this, s_pendingFamilyId);
            s_sourceShelter.UnassignFamily(s_pendingFamilyId);

            // Force both shelters to fully rebuild counts immediately.
            RefreshHousingTracking();
            s_sourceShelter.RefreshHousingTracking();

            //Debug.Log($"[Shelter] Moved family {s_pendingFamilyId} to {name}");
            CancelMoveMode(true);
        }
        else
        {
            //Debug.Log("[Shelter] Move failed at assignment step.");
            CancelMoveMode(false);
        }
    }

    public static ShelterControl FindAnyShelterThatCanAccept(string familyId, PlayerFamilySimulationManager famMgr)
    {
        if (string.IsNullOrEmpty(familyId) || famMgr == null) return null;

        var pbm = PlayerBuildingManager.Instance;
        if (pbm == null) return null;

        var all = pbm.GetAll();
        if (all == null) return null;

        ShelterControl best = null;
        int bestFreeIndividuals = int.MinValue;

        for (int i = 0; i < all.Count; i++)
        {
            var r = all[i];
            if (r == null || r.instance == null) continue;
            if (r.type != BuildingType.Shelter) continue;

            var sc = r.instance.GetComponent<ShelterControl>();
            if (sc == null) continue;

            if (!sc.CanAcceptFamily(familyId, famMgr))
                continue;

            int freeInd = Mathf.Max(0, sc.individualCapacity - sc.GetCurrentIndividualsCount());

            if (freeInd > bestFreeIndividuals)
            {
                bestFreeIndividuals = freeInd;
                best = sc;
            }
        }

        return best;
    }

    private void PruneExtraResidentLists()
    {
        if (guestIndividualIds == null) guestIndividualIds = new List<string>();
        if (movedOutIndividualIds == null) movedOutIndividualIds = new List<string>();

        guestIndividualIds.RemoveAll(id =>
        {
            var ind = GetIndividual(id);
            return ind == null || !ind.IsAlive;
        });

        movedOutIndividualIds.RemoveAll(id =>
        {
            var ind = GetIndividual(id);
            return ind == null || !ind.IsAlive || string.IsNullOrEmpty(ind.FamilyId) || !housedFamilyIds.Contains(ind.FamilyId);
        });
    }

    private bool IsIndividualCurrentlyHousedHere(string individualId)
    {
        return !string.IsNullOrEmpty(individualId) &&
               housedIndividualIds != null &&
               housedIndividualIds.Contains(individualId);
    }

    public bool CanAcceptIndividual(string individualId, ShelterControl source = null)
    {
        if (_status != null && _status.CurrentState == BuildingState.Destroyed) return false;
        if (string.IsNullOrEmpty(individualId)) return false;

        var ind = GetIndividual(individualId);
        if (ind == null || !ind.IsAlive) return false;

        if (IsIndividualCurrentlyHousedHere(individualId))
            return true;

        if (GetCurrentIndividualsCount() >= Mathf.Max(0, individualCapacity))
            return false;

        var owner = GetShelterHousingIndividual(individualId);
        if (owner != null && owner != this && owner != source)
            return false;

        return true;
    }

    private void RemoveIndividualFromThisShelter(string individualId)
    {
        if (string.IsNullOrEmpty(individualId)) return;

        if (guestIndividualIds.Remove(individualId))
        {
            RefreshHousingTracking(false);
            return;
        }

        var ind = GetIndividual(individualId);
        if (ind != null &&
            !string.IsNullOrEmpty(ind.FamilyId) &&
            housedFamilyIds.Contains(ind.FamilyId) &&
            !movedOutIndividualIds.Contains(individualId))
        {
            movedOutIndividualIds.Add(individualId);
        }

        RefreshHousingTracking(false);
    }

    private void AddIndividualToThisShelter(string individualId)
    {
        if (string.IsNullOrEmpty(individualId)) return;

        var ind = GetIndividual(individualId);
        if (ind == null || !ind.IsAlive) return;

        if (!string.IsNullOrEmpty(ind.FamilyId) && housedFamilyIds.Contains(ind.FamilyId))
        {
            movedOutIndividualIds.Remove(individualId);
            guestIndividualIds.Remove(individualId);
        }
        else
        {
            if (!guestIndividualIds.Contains(individualId))
                guestIndividualIds.Add(individualId);
        }

        RefreshHousingTracking(false);
    }

    private static bool TryMoveIndividualBetweenShelters(string individualId, ShelterControl source, ShelterControl target)
    {
        if (target == null) return false;
        if (source == null || source == target) return false;
        if (string.IsNullOrEmpty(individualId)) return false;

        if (!target.CanAcceptIndividual(individualId, source))
            return false;

        source.RemoveIndividualFromThisShelter(individualId);
        target.AddIndividualToThisShelter(individualId);

        //Debug.Log($"[Shelter] Moved individual {individualId} from {source.name} to {target.name}");
        return true;
    }

    public static ShelterControl FindAnyShelterThatCanAcceptIndividual(string individualId, ShelterControl exclude = null)
    {
        if (string.IsNullOrEmpty(individualId)) return null;

        ShelterControl best = null;
        int bestFreeIndividuals = int.MinValue;

        for (int i = 0; i < s_all.Count; i++)
        {
            var sc = s_all[i];
            if (sc == null || sc == exclude) continue;
            if (!sc.CanAcceptIndividual(individualId, exclude)) continue;

            int freeInd = Mathf.Max(0, sc.individualCapacity - sc.GetCurrentIndividualsCount());
            if (freeInd > bestFreeIndividuals)
            {
                bestFreeIndividuals = freeInd;
                best = sc;
            }
        }

        return best;
    }

    private bool TryResolvePairShelterForPairing(
    Individual mother,
    Individual father,
    out ShelterControl targetShelter)
    {
        targetShelter = null;

        if (mother == null || father == null)
            return false;

        // This shelter only starts from mothers currently living here.
        if (!IsIndividualCurrentlyHousedHere(mother.Id))
            return false;

        // If father is already here, keep the pregnancy here.
        if (IsIndividualCurrentlyHousedHere(father.Id))
        {
            if (!CanOwnPregnancyForMother(mother.Id))
                return false;

            targetShelter = this;
            return true;
        }

        var fatherShelter = GetShelterHousingIndividual(father.Id);

        // Case 1: father is in another shelter.
        if (fatherShelter != null && fatherShelter != this)
        {
            // Preferred: move father into mother's shelter, keep order here.
            bool canPullFatherHere =
                CanOwnPregnancyForMother(mother.Id) &&
                CanAcceptIndividual(father.Id, fatherShelter);

            if (canPullFatherHere)
            {
                if (TryMoveIndividualBetweenShelters(father.Id, fatherShelter, this))
                {
                    targetShelter = this;
                    return true;
                }
            }

            // Inverse behavior: if this shelter can't host father,
            // try moving mother to father's shelter and let that shelter own the order.
            bool canMoveMotherThere =
                fatherShelter.CanOwnPregnancyForMother(mother.Id) &&
                fatherShelter.CanAcceptIndividual(mother.Id, this);

            if (canMoveMotherThere)
            {
                if (TryMoveIndividualBetweenShelters(mother.Id, this, fatherShelter))
                {
                    targetShelter = fatherShelter;
                    return true;
                }
            }

            return false;
        }

        // Case 2: father is not housed anywhere.
        // Try to host him here first.
        bool canHostUnhousedFatherHere =
            CanOwnPregnancyForMother(mother.Id) &&
            CanAcceptIndividual(father.Id, null);

        if (canHostUnhousedFatherHere)
        {
            AddIndividualToThisShelter(father.Id);
            //Debug.Log($"[Shelter] Pulled unhoused partner {father.Id} into {name} for pairing.");
            targetShelter = this;
            return true;
        }

        // No inverse move for unhoused fathers right now.
        return false;
    }

    private void TransferBirthOrdersForFamilyTo(ShelterControl target, string familyId)
    {
        if (target == null || target == this) return;
        if (string.IsNullOrEmpty(familyId)) return;
        if (activeOrders == null || activeOrders.Count == 0) return;

        var toMove = new List<PopulationBirthOrder>();

        for (int i = 0; i < activeOrders.Count; i++)
        {
            var order = activeOrders[i];
            if (order != null && order.FamilyId == familyId)
                toMove.Add(order);
        }

        if (toMove.Count == 0)
            return;

        for (int i = 0; i < toMove.Count; i++)
        {
            var order = toMove[i];
            if (order == null) continue;

            // Remove from old shelter tracking
            activeOrders.Remove(order);

            if (widgets.TryGetValue(order.OrderId, out var oldWidget) && oldWidget != null)
                Destroy(oldWidget.gameObject);
            widgets.Remove(order.OrderId);

            // Avoid duplicate order in target
            bool alreadyInTarget = false;
            for (int j = 0; j < target.activeOrders.Count; j++)
            {
                var existing = target.activeOrders[j];
                if (existing != null && existing.OrderId == order.OrderId)
                {
                    alreadyInTarget = true;
                    break;
                }
            }

            if (!alreadyInTarget)
                target.activeOrders.Add(order);

            // Delete any stale target widget and recreate it in the new shelter UI
            if (target.widgets.TryGetValue(order.OrderId, out var targetWidget) && targetWidget != null)
                Destroy(targetWidget.gameObject);
            target.widgets.Remove(order.OrderId);

            target.SpawnWidget(order);
            target.UpdateWidget(order);
        }

        SyncDebugOrders();
        target.SyncDebugOrders();

        //Debug.Log($"[Shelter] Moved {toMove.Count} birth order(s) for family {familyId} from {name} to {target.name}");
    }

    private void TryMoveOverflowIndividualsToOtherShelters()
    {
        if (familySim == null) return;
        if (individualCapacity <= 0) return;
        if (housedIndividualIds == null) return;

        SyncHousedIndividualsFromFamilies();

        int overflow = GetCurrentIndividualsCount() - individualCapacity;
        if (overflow <= 0) return;

        var protectedMothers = new HashSet<string>(
            activeOrders
                .Where(o => o != null && !string.IsNullOrEmpty(o.MotherId))
                .Select(o => o.MotherId),
            System.StringComparer.Ordinal);

        var candidates = housedIndividualIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => GetIndividual(id))
            .Where(ind =>
                ind != null &&
                ind.IsAlive &&
                (ind.AggregatedAgeGroup == AgeGroup.Teen ||
                 ind.AggregatedAgeGroup == AgeGroup.Adult ||
                 ind.AggregatedAgeGroup == AgeGroup.Elder) &&
                !ind.IsBusy &&
                !protectedMothers.Contains(ind.Id))
            .OrderBy(ind => guestIndividualIds != null && guestIndividualIds.Contains(ind.Id) ? 0 : 1)
            .ThenBy(ind => ind.AggregatedAgeGroup == AgeGroup.Elder ? 0 : 1)
            .ToList();

        int moved = 0;

        for (int i = 0; i < candidates.Count && overflow > 0; i++)
        {
            var ind = candidates[i];
            if (ind == null || string.IsNullOrEmpty(ind.Id))
                continue;

            var target = FindAnyShelterThatCanAcceptIndividual(ind.Id, this);
            if (target == null) continue;

            if (TryMoveIndividualBetweenShelters(ind.Id, this, target))
            {
                overflow--;
                moved++;
            }
        }

        if (moved > 0)
        {
            SyncHousedIndividualsFromFamilies();
            UpdateCapacityIcon();
        }

        if (overflow > 0)
        {
            //Debug.LogWarning($"[Shelter] {name} is still over individual capacity by {overflow}. No valid shelters found for overflow residents.");
        }
    }

    private bool CanCreateAnotherPregnancyOrder()
    {
        if (_status != null && _status.CurrentState == BuildingState.Destroyed) return false;
        if (pauseBirthing) return false;
        if (!_canCreateOrders) return false;

        return activeOrders.Count < maxConcurrentOrders;
    }

    private bool CanOwnPregnancyForMother(string motherId)
    {
        if (string.IsNullOrEmpty(motherId)) return false;
        if (!CanCreateAnotherPregnancyOrder()) return false;

        for (int i = 0; i < activeOrders.Count; i++)
        {
            var o = activeOrders[i];
            if (o != null && o.MotherId == motherId)
                return false;
        }

        if (_pregSvc != null)
        {
            if (_pregSvc.IsOnParentCooldown(motherId)) return false;
            if (_pregSvc.IsMotherCurrentlyPregnant(motherId)) return false;
        }

        return true;
    }

    private bool TryStartGestationOrderHere(Individual mom, Individual dad, int gestationTurns)
    {
        if (mom == null || dad == null) return false;
        if (!IsIndividualCurrentlyHousedHere(mom.Id)) return false;
        if (!CanOwnPregnancyForMother(mom.Id)) return false;
        if (familySim == null) return false;

        if (!familySim.TryStartPregnancyWithReservation(mom, dad, gestationTurns, out var reservationId))
            return false;

        var order = new PopulationBirthOrder(mom.Id, dad.Id, mom.FamilyId, gestationTurns)
        {
            ReservationId = reservationId
        };

        activeOrders.Add(order);
        _pregSvc?.SetPreferredBirthFamily(mom.Id, order.FamilyId);

        SpawnWidget(order);
        SyncDebugOrders();

        CivilizationHappinessSystem.Instance?.NotifyPairingSuccess();
        return true;
    }

    public static List<ShelterControl> GetAllSheltersSnapshot()
    {
        var result = new List<ShelterControl>();

        var pbm = PlayerBuildingManager.Instance;
        if (pbm == null)
            return result;

        var all = pbm.GetAll();
        if (all == null)
            return result;

        var seen = new HashSet<ShelterControl>();

        for (int i = 0; i < all.Count; i++)
        {
            var rec = all[i];
            if (rec == null || rec.instance == null)
                continue;

            if (rec.type != BuildingType.Shelter)
                continue;

            var sc = rec.instance.GetComponent<ShelterControl>();
            if (sc == null)
                sc = rec.instance.GetComponentInChildren<ShelterControl>();

            if (sc == null)
                continue;

            if (seen.Add(sc))
                result.Add(sc);
        }

        return result;
    }

    public ShelterRuntimeSaveData CaptureRuntimeSaveData(string buildingSaveableID)
    {
        ShelterRuntimeSaveData data = new ShelterRuntimeSaveData
        {
            buildingSaveableID = buildingSaveableID,
            pauseBirthing = pauseBirthing,
            turnsUntilNextPairing = turnsUntilNextPairing
        };

        if (housedFamilyIds != null)
            data.housedFamilyIds = new List<string>(housedFamilyIds);

        if (guestIndividualIds != null)
            data.guestIndividualIds = new List<string>(guestIndividualIds);

        if (movedOutIndividualIds != null)
            data.movedOutIndividualIds = new List<string>(movedOutIndividualIds);

        for (int i = 0; i < activeOrders.Count; i++)
        {
            var order = activeOrders[i];
            if (order == null)
                continue;

            data.activeOrders.Add(new ShelterBirthOrderSaveData
            {
                orderId = order.OrderId,
                motherId = order.MotherId,
                fatherId = order.FatherId,
                familyId = order.FamilyId,
                turnsRemaining = order.TurnsRemaining,
                reservationId = order.ReservationId
            });
        }

        return data;
    }

    public void ApplyRuntimeSaveData(ShelterRuntimeSaveData data)
    {
        if (data == null)
            return;

        pauseBirthing = data.pauseBirthing;
        turnsUntilNextPairing = Mathf.Max(0, data.turnsUntilNextPairing);

        if (housedFamilyIds == null) housedFamilyIds = new List<string>();
        if (guestIndividualIds == null) guestIndividualIds = new List<string>();
        if (movedOutIndividualIds == null) movedOutIndividualIds = new List<string>();

        housedFamilyIds.Clear();
        guestIndividualIds.Clear();
        movedOutIndividualIds.Clear();

        if (data.housedFamilyIds != null)
            housedFamilyIds.AddRange(data.housedFamilyIds.Where(id => !string.IsNullOrWhiteSpace(id)));

        if (data.guestIndividualIds != null)
            guestIndividualIds.AddRange(data.guestIndividualIds.Where(id => !string.IsNullOrWhiteSpace(id)));

        if (data.movedOutIndividualIds != null)
            movedOutIndividualIds.AddRange(data.movedOutIndividualIds.Where(id => !string.IsNullOrWhiteSpace(id)));

        ClearOrdersForLoad();

        if (data.activeOrders != null)
        {
            for (int i = 0; i < data.activeOrders.Count; i++)
            {
                ShelterBirthOrderSaveData saved = data.activeOrders[i];
                if (saved == null || string.IsNullOrWhiteSpace(saved.motherId))
                    continue;

                int gest = Mathf.Max(1, saved.turnsRemaining);
                var order = new PopulationBirthOrder(saved.motherId, saved.fatherId, saved.familyId, gest);

                SaveReflectionUtil.Set(order, "OrderId", saved.orderId);
                SaveReflectionUtil.Set(order, "TurnsRemaining", Mathf.Max(0, saved.turnsRemaining));
                SaveReflectionUtil.Set(order, "ReservationId", saved.reservationId);

                activeOrders.Add(order);
                SpawnWidget(order);
                UpdateWidget(order);
            }
        }

        PruneInvalidOrEmptyFamilies();
        SyncHousedIndividualsFromFamilies();
        EnforceIndividualCapByTrimming();
        SyncDebugOrders();
        UpdateCapacityIcon();

        if (IsSaveLoadInProgress())
            SyncCapStateFromLoadedStatusWithoutChangingPopulation();

        RefreshMoveHereUI();
    }

    private void RefreshHousingTracking(bool enforceCap = true)
    {
        if (housedFamilyIds == null) housedFamilyIds = new List<string>();
        if (housedIndividualIds == null) housedIndividualIds = new List<string>();
        if (guestIndividualIds == null) guestIndividualIds = new List<string>();
        if (movedOutIndividualIds == null) movedOutIndividualIds = new List<string>();

        PruneExtraResidentLists();
        SyncHousedIndividualsFromFamilies();
        RemoveEmptyFamiliesWhoseMembersNotPresent();
        SyncHousedIndividualsFromFamilies();

        if (enforceCap)
        {
            EnforceIndividualCapByTrimming();

            // Overflow moves can leave a family with no residents here anymore.
            RemoveEmptyFamiliesWhoseMembersNotPresent();
            SyncHousedIndividualsFromFamilies();
        }

        UpdateCapacityIcon();

        SaveSystem.MarkSectionDirty(SaveSectionKeys.Jobs);
        SaveSystem.MarkSectionDirty(SaveSectionKeys.WorldObjects);
    }

    private bool IsSaveLoadInProgress()
    {
        return SaveSystem.Instance != null && SaveSystem.Instance.IsLoading;
    }

    private void ClearOrdersForLoad()
    {
        activeOrders.Clear();

        foreach (var w in widgets.Values)
            if (w != null)
                Destroy(w.gameObject);

        widgets.Clear();
    }

    private float GetTornadoDeathChanceForAgeGroup(AgeGroup ageGroup)
    {
        return ageGroup switch
        {
            AgeGroup.Child => tornadoChildDeathChance,
            AgeGroup.Teen => tornadoTeenDeathChance,
            AgeGroup.Adult => tornadoAdultDeathChance,
            AgeGroup.Elder => tornadoElderDeathChance,
            _ => 0f
        };
    }

    public int TryApplyTornadoCasualties(float externalChanceMultiplier = 1f, bool debugLogging = false)
    {
        if (!tornadoCanKillHousedPopulation)
            return 0;

        if (familySim == null)
            familySim = PlayerFamilySimulationManager.Instance;

        if (playerPop == null)
            playerPop = PlayersPopulationManager.Instance;

        if (familySim == null || housedIndividualIds == null || housedIndividualIds.Count == 0)
            return 0;

        SyncHousedIndividualsFromFamilies();

        HashSet<string> protectedMotherIds = null;
        if (tornadoSkipProtectedMothers && activeOrders != null && activeOrders.Count > 0)
        {
            protectedMotherIds = new HashSet<string>(System.StringComparer.Ordinal);

            for (int i = 0; i < activeOrders.Count; i++)
            {
                var order = activeOrders[i];
                if (order == null || string.IsNullOrWhiteSpace(order.MotherId))
                    continue;

                protectedMotherIds.Add(order.MotherId);
            }
        }

        List<string> killIds = new List<string>();

        for (int i = 0; i < housedIndividualIds.Count; i++)
        {
            string individualId = housedIndividualIds[i];
            if (string.IsNullOrWhiteSpace(individualId))
                continue;

            Individual ind = GetIndividual(individualId);
            if (ind == null || !ind.IsAlive)
                continue;

            // User requested: only people not marked busy can be rolled
            if (ind.IsBusy)
                continue;

            if (protectedMotherIds != null && protectedMotherIds.Contains(ind.Id))
                continue;

            float chance = GetTornadoDeathChanceForAgeGroup(ind.AggregatedAgeGroup);
            if (chance <= 0f)
                continue;

            chance *= tornadoCasualtyChanceMultiplier;
            chance *= Mathf.Max(0f, externalChanceMultiplier);
            chance = Mathf.Clamp01(chance);

            if (UnityEngine.Random.value <= chance)
            {
                // If they are in any non-busy reservation, try to detach cleanly first
                if (playerPop != null)
                    playerPop.TryDetachIndividualFromExistingReservations(ind.Id, out _);

                killIds.Add(ind.Id);
            }
        }

        if (killIds.Count == 0)
            return 0;

        if (!familySim.TryKillIndividualsById(killIds, out int killedCount) || killedCount <= 0)
            return 0;

        RefreshHousingTracking(false);

        if (debugLogging)
        {
            //Debug.Log(
                //$"[Shelter] Tornado casualties at '{name}' | " +
                //$"Rolled={killIds.Count} | Killed={killedCount} | " +
                //$"ResidentsAfter={CurrentIndividualCount}"
            //);
        }

        return killedCount;
    }

    private float GetFireDeathChanceForAgeGroup(AgeGroup ageGroup)
    {
        return ageGroup switch
        {
            AgeGroup.Child => fireChildDeathChance,
            AgeGroup.Teen => fireTeenDeathChance,
            AgeGroup.Adult => fireAdultDeathChance,
            AgeGroup.Elder => fireElderDeathChance,
            _ => 0f
        };
    }

    public int TryApplyFireCasualties(float externalChanceMultiplier = 1f, bool debugLogging = false)
    {
        if (!fireCanKillHousedPopulation)
            return 0;

        if (familySim == null)
            familySim = PlayerFamilySimulationManager.Instance;

        if (playerPop == null)
            playerPop = PlayersPopulationManager.Instance;

        if (familySim == null || housedIndividualIds == null || housedIndividualIds.Count == 0)
            return 0;

        SyncHousedIndividualsFromFamilies();

        HashSet<string> protectedMotherIds = null;
        if (fireSkipProtectedMothers && activeOrders != null && activeOrders.Count > 0)
        {
            protectedMotherIds = new HashSet<string>(System.StringComparer.Ordinal);

            for (int i = 0; i < activeOrders.Count; i++)
            {
                var order = activeOrders[i];
                if (order == null || string.IsNullOrWhiteSpace(order.MotherId))
                    continue;

                protectedMotherIds.Add(order.MotherId);
            }
        }

        List<string> killIds = new List<string>();

        for (int i = 0; i < housedIndividualIds.Count; i++)
        {
            string individualId = housedIndividualIds[i];
            if (string.IsNullOrWhiteSpace(individualId))
                continue;

            Individual ind = GetIndividual(individualId);
            if (ind == null || !ind.IsAlive)
                continue;

            if (ind.IsBusy)
                continue;

            if (protectedMotherIds != null && protectedMotherIds.Contains(ind.Id))
                continue;

            float chance = GetFireDeathChanceForAgeGroup(ind.AggregatedAgeGroup);
            if (chance <= 0f)
                continue;

            chance *= fireCasualtyChanceMultiplier;
            chance *= Mathf.Max(0f, externalChanceMultiplier);
            chance = Mathf.Clamp01(chance);

            if (UnityEngine.Random.value <= chance)
            {
                if (playerPop != null)
                    playerPop.TryDetachIndividualFromExistingReservations(ind.Id, out _);

                killIds.Add(ind.Id);
            }
        }

        if (killIds.Count == 0)
            return 0;

        if (!familySim.TryKillIndividualsById(killIds, out int killedCount) || killedCount <= 0)
            return 0;

        RefreshHousingTracking(false);

        if (debugLogging)
        {
            //Debug.Log(
                //$"[Shelter] Fire casualties at '{name}' | " +
                //$"Rolled={killIds.Count} | Killed={killedCount} | " +
                //$"ResidentsAfter={CurrentIndividualCount}"
            //);
        }

        return killedCount;
    }

    private BuildingVolcanicResistance GetVolcanicResistance()
    {
        BuildingVolcanicResistance resistance = GetComponent<BuildingVolcanicResistance>();
        if (resistance == null)
            resistance = GetComponentInParent<BuildingVolcanicResistance>(true);
        if (resistance == null)
            resistance = GetComponentInChildren<BuildingVolcanicResistance>(true);

        return resistance;
    }

    private bool HasActivePregnancyOrderForMother(string individualId)
    {
        if (string.IsNullOrWhiteSpace(individualId) || activeOrders == null)
            return false;

        for (int i = 0; i < activeOrders.Count; i++)
        {
            PopulationBirthOrder order = activeOrders[i];
            if (order == null || string.IsNullOrWhiteSpace(order.MotherId))
                continue;

            if (order.MotherId == individualId)
                return true;
        }

        return false;
    }

    public int TryApplyVolcanicShelterExposure(bool acidRain, float externalMultiplier = 1f, bool debugLogging = false)
    {
        if (!volcanicCanHarmHousedPopulation)
            return 0;

        if (acidRain && !acidRainCanAffectHousedPopulation)
            return 0;

        if (!acidRain && !ashCanAffectHousedPopulation)
            return 0;

        if (familySim == null)
            familySim = PlayerFamilySimulationManager.Instance;

        if (playerPop == null)
            playerPop = PlayersPopulationManager.Instance;

        if (familySim == null || housedIndividualIds == null || housedIndividualIds.Count == 0)
            return 0;

        SyncHousedIndividualsFromFamilies();

        BuildingVolcanicResistance resistance = GetVolcanicResistance();
        if (resistance != null && resistance.IsImmune(acidRain))
        {
            if (debugLogging) {}
                //Debug.Log($"[Shelter] {name} is immune to {(acidRain ? "acid rain" : "ash")} exposure.");
            return 0;
        }

        float baseChance = acidRain ? acidRainExposureChance : ashExposureChance;
        float minLoss = acidRain ? acidRainHealthLossMin : ashHealthLossMin;
        float maxLoss = acidRain ? acidRainHealthLossMax : ashHealthLossMax;

        float resistanceChanceMultiplier = resistance != null
            ? resistance.GetExposureChanceMultiplier(acidRain)
            : 1f;

        float resistanceHealthMultiplier = resistance != null
            ? resistance.GetHealthLossMultiplier(acidRain)
            : 1f;

        int affectedCount = 0;
        float totalHealthLoss = 0f;

        for (int i = 0; i < housedIndividualIds.Count; i++)
        {
            string individualId = housedIndividualIds[i];
            if (string.IsNullOrWhiteSpace(individualId))
                continue;

            Individual ind = GetIndividual(individualId);
            if (ind == null || !ind.IsAlive)
                continue;

            // User requested: affect sheltered population that is not being used.
            if (ind.IsBusy)
                continue;

            bool pregnancySensitive =
                HasActivePregnancyOrderForMother(ind.Id) ||
                (familySim != null && familySim.IsIndividualCurrentlyPregnant(ind.Id));

            float chance = baseChance;
            chance *= Mathf.Max(0f, externalMultiplier);
            chance *= Mathf.Max(0f, resistanceChanceMultiplier);

            if (pregnancySensitive)
                chance *= Mathf.Max(0f, activePregnancyExposureMultiplier);

            chance = Mathf.Clamp01(chance);

            if (UnityEngine.Random.value > chance)
                continue;

            float loss = UnityEngine.Random.Range(minLoss, maxLoss);
            loss *= Mathf.Max(0f, externalMultiplier);
            loss *= Mathf.Max(0f, resistanceHealthMultiplier);

            if (pregnancySensitive)
                loss *= Mathf.Max(0f, activePregnancyExposureMultiplier);

            // Apply age-group resistance to the actual health damage.
            float ageResistance01 = GetIndividualAgeResistance01(ind);
            loss *= (1f - ageResistance01);

            loss = Mathf.Max(0f, loss);
            if (loss <= 0f)
                continue;

            float oldHealth = ind.Health01;
            float newHealth = Mathf.Clamp(ind.Health01 - loss, minimumHealthAfterVolcanicExposure, 1f);

            if (newHealth >= oldHealth - 0.0001f)
                continue;

            ind.Health01 = newHealth;
            affectedCount++;
            totalHealthLoss += (oldHealth - newHealth);
        }

        if (affectedCount > 0)
        {
            playerPop?.MarkUIDirty();
            SaveSystem.MarkSectionDirty(SaveSectionKeys.Population);
            SaveSystem.MarkSectionDirty(SaveSectionKeys.WorldObjects);
        }

        if (debugLogging)
        {
            //Debug.Log(
                //$"[Shelter] Volcanic {(acidRain ? "acid rain" : "ash")} exposure at '{name}' | " +
                //$"Affected={affectedCount} | " +
                //$"TotalHealthLoss={totalHealthLoss:F3} | " +
                //$"Residents={CurrentIndividualCount}");
        }

        return affectedCount;
    }

    private float GetIndividualAgeResistance01(Individual ind)
    {
        if (ind == null)
            return 0f;

        if (PlayerHealthRulebook.Instance != null)
            return Mathf.Clamp01(PlayerHealthRulebook.Instance.GetResistance(ind.AggregatedAgeGroup));

        if (GeneralPopulationManager.Instance != null)
            return Mathf.Clamp01(GeneralPopulationManager.Instance.GetResistance(ind.AggregatedAgeGroup));

        return 0f;
    }

    private int TryApplyShelterWeatherDiseaseExposure(bool debugLogging = false)
    {
        if (!shelterWeatherDiseaseExposure)
            return 0;

        if (_status != null && _status.CurrentState == BuildingState.Destroyed)
            return 0;

        if (DiseaseManager.Instance == null)
            return 0;

        if (familySim == null)
            familySim = PlayerFamilySimulationManager.Instance;

        if (playerPop == null)
            playerPop = PlayersPopulationManager.Instance;

        if (familySim == null || playerPop == null)
            return 0;

        SyncHousedIndividualsFromFamilies();

        if (housedIndividualIds == null || housedIndividualIds.Count == 0)
            return 0;

        _tmpShelterWeatherDiseaseIds.Clear();

        for (int i = 0; i < housedIndividualIds.Count; i++)
        {
            string individualId = housedIndividualIds[i];

            if (string.IsNullOrWhiteSpace(individualId))
                continue;

            Individual person = GetIndividual(individualId);

            if (person == null || !person.IsAlive)
                continue;

            // The user wanted shelter exposure for unbusy population only.
            if (person.IsBusy)
                continue;

            if (playerPop.IsIndividualReservedAnywhere(person.Id))
                continue;

            if (_tmpShelterWeatherDiseaseIds.Contains(person.Id))
                continue;

            _tmpShelterWeatherDiseaseIds.Add(person.Id);
        }

        if (_tmpShelterWeatherDiseaseIds.Count == 0)
            return 0;

        int infections = DiseaseManager.Instance.TryApplyEnvironmentalDiseaseRiskForBuildingComponent(
            this,
            _tmpShelterWeatherDiseaseIds,
            DiseaseTaskResultType.ShelterBuildingWeatherExposure,
            shelterWeatherDiseaseChanceMultiplier,
            shelterWeatherDiseaseExposureMultiplier,
            maxShelterWeatherDiseaseTargetsPerTurn);

        if (debugLogging || debugShelterWeatherDiseaseExposure)
        {
            if (infections > 0)
            {
                //Debug.Log(
                    //$"[Shelter] Weather disease exposure at '{name}'. " +
                    //$"UnbusyHousedTargets={_tmpShelterWeatherDiseaseIds.Count}, Infections={infections}");
            }
        }

        return infections;
    }

    private int TrySpreadVirusesInShelter()
    {
        if (!enableShelterVirusSpread)
            return 0;

        if (DiseaseManager.Instance == null)
            return 0;

        if (_status != null && _status.CurrentState == BuildingState.Destroyed)
            return 0;

        if (familySim == null)
            familySim = PlayerFamilySimulationManager.Instance;

        if (playerPop == null)
            playerPop = PlayersPopulationManager.Instance;

        if (familySim == null || playerPop == null)
            return 0;

        SyncHousedIndividualsFromFamilies();

        if (housedIndividualIds == null || housedIndividualIds.Count <= 1)
            return 0;

        _tmpShelterVirusSpreadIds.Clear();

        for (int i = 0; i < housedIndividualIds.Count; i++)
        {
            string id = housedIndividualIds[i];

            if (string.IsNullOrWhiteSpace(id))
                continue;

            Individual person = GetIndividual(id);

            if (person == null || !person.IsAlive)
                continue;

            // Shelter spread only applies to unbusy people resting/living there.
            if (person.IsBusy)
                continue;

            if (playerPop.IsIndividualReservedAnywhere(person.Id))
                continue;

            _tmpShelterVirusSpreadIds.Add(person.Id);
        }

        if (_tmpShelterVirusSpreadIds.Count <= 1)
            return 0;

        int spreadCount = DiseaseManager.Instance.TrySpreadContagiousVirusesWithinGroup(
            _tmpShelterVirusSpreadIds,
            "Shelter",
            name,
            shelterVirusSpreadMultiplier);

        if (debugShelterVirusSpread && spreadCount > 0)
        {
            //Debug.Log(
                //$"[Shelter] Virus spread in shelter '{name}'. " +
                //$"Contacts={_tmpShelterVirusSpreadIds.Count}, NewInfections={spreadCount}");
        }

        return spreadCount;
    }

    private int TryApplyShelterCrowdingBuildingDiseaseExposure()
    {
        if (_buildingDiseaseExposure == null)
            return 0;

        if (_status != null && _status.CurrentState == BuildingState.Destroyed)
            return 0;

        SyncHousedIndividualsFromFamilies();

        int maxHoused = Mathf.Max(1, individualCapacity);
        int housedCount = GetCurrentIndividualsCount();
        float occupancyRatio = housedCount / (float)maxHoused;

        if (requireOverHalfHousedForBuildingDiseaseExposure &&
            occupancyRatio <= buildingDiseaseMinOccupancyRatio)
        {
            if (debugShelterBuildingDiseaseGate)
            {
                //Debug.Log(
                    //$"[Shelter] Building disease skipped at '{name}'. " +
                    //$"Housed={housedCount}/{maxHoused}, " +
                    //$"Ratio={occupancyRatio:F2}, " +
                    //$"RequiredOver={buildingDiseaseMinOccupancyRatio:F2}");
            }

            return 0;
        }

        int infections = 0;

        if (applyBuildingDiseaseToUnbusyHousedPopulation)
        {
            infections += _buildingDiseaseExposure.TryApplyToShelterUnbusyPopulation(
                this,
                BuildingDiseaseTriggerTiming.EveryTurn);
        }

        if (applyBuildingDiseaseToAnyHousedPopulation)
        {
            infections += _buildingDiseaseExposure.TryApplyToShelterAnyHousedPopulation(
                this,
                BuildingDiseaseTriggerTiming.EveryTurn);
        }

        if (debugShelterBuildingDiseaseGate && infections > 0)
        {
            //Debug.Log(
                //$"[Shelter] Building crowding disease ran at '{name}'. " +
                //$"Housed={housedCount}/{maxHoused}, " +
                //$"Ratio={occupancyRatio:F2}, " +
                //$"Infections={infections}");
        }

        return infections;
    }
}
