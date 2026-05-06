using UnityEngine;

public class EnvironmentTaskRewardManager : MonoBehaviour
{
    public static EnvironmentTaskRewardManager Instance { get; private set; }

    [Header("Config (ONE place, not per tile)")]
    [SerializeField] private TaskSuccessPopulationRewardConfig populationRewardConfig;

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void TryGrantPopulationReward(EnvironmentTaskKind kind, EnvironmentControl env)
    {
        var cfg = populationRewardConfig;
        if (cfg == null || !cfg.enabled) return;
        if (env == null) return;

        if (!cfg.RollTrigger(kind, env.tileSize)) return;

        // Decide what kind of offer it is (you currently force new family; here’s both supported)
        bool isNewFamily = cfg.RollNewFamily();

        if (isNewFamily)
        {
            int adults = Mathf.Max(1, cfg.RollAdults());
            int kids = Mathf.Max(0, cfg.RollChildren());

            ImmigrantOfferManager.Instance?.EnqueueNewFamily(kind, env, cfg, adults, kids);
        }
        else
        {
            int count = Mathf.Max(1, cfg.RollIndividuals());
            ImmigrantOfferManager.Instance?.EnqueueIndividuals(kind, env, cfg, count);
        }
    }

    public void TryHouseFamilyPublic(string familyId, PlayerFamilySimulationManager famSim)
    {
        TryHouseFamily(familyId, famSim);
    }

    private void TryHouseFamily(string familyId, PlayerFamilySimulationManager famSim)
    {
        if (string.IsNullOrEmpty(familyId) || famSim == null) return;

        // 1) Prefer starter shelter (if you have one)
        var pbm = PlayerBuildingManager.Instance;
        if (pbm != null)
        {
            var all = pbm.GetAll(); // no ToList alloc, returns your backing list as IReadOnlyList
            for (int i = 0; i < all.Count; i++)
            {
                var r = all[i];
                if (r == null || r.instance == null) continue;

                if (r.isStarter && r.type == BuildingType.Shelter)
                {
                    var sc = r.instance.GetComponent<ShelterControl>();
                    if (sc != null && sc.CanAcceptFamily(familyId, famSim))
                    {
                        sc.TryAssignFamily(familyId);
                        return;
                    }
                }
            }
        }

        // 2) Fallback: any shelter that can accept them
        var best = ShelterControl.FindAnyShelterThatCanAccept(familyId, famSim);
        if (best != null)
        {
            best.TryAssignFamily(familyId);
            return;
        }

        // 3) No shelter available — family still exists, and will be picked up later by FillFamilySlots()
        Debug.Log("[Rewards] No shelter could accept the new family right now (capacity/destroyed/none built).");
    }
}