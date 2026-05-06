using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerConstructionManager : MonoBehaviour
{
    public static PlayerConstructionManager Instance { get; private set; }

    [Header("Dependencies")]
    [SerializeField] private PlayersPopulationManager populationManager;
    [SerializeField] private PlayerBuildingManager playerBuildingManager;
    [SerializeField] private PlayerLevel playerLevel;

    [Header("Performance")]
    [Tooltip("How many active constructions to advance per frame on end-of-turn.")]
    [Min(1)] public int maxConstructionsPerFrame = 10;

    private readonly HashSet<BuildingConstruction> active = new();
    private Coroutine processingCoroutine;
    private bool _subscribedToTurnEnd;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (populationManager == null)
            populationManager = PlayersPopulationManager.Instance;
    }

    private void OnEnable()
    {
        if (_subscribedToTurnEnd)
            return;

        TurnSystem.SubscribeToEndOfTurn(OnTurnEnded);
        _subscribedToTurnEnd = true;
    }

    private void OnDisable()
    {
        if (!_subscribedToTurnEnd)
            return;

        TurnSystem.UnsubscribeFromEndOfTurn(OnTurnEnded);
        _subscribedToTurnEnd = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (_subscribedToTurnEnd)
        {
            TurnSystem.UnsubscribeFromEndOfTurn(OnTurnEnded);
            _subscribedToTurnEnd = false;
        }
    }

    private string GetReservationOwnerId(GameObject constructionGO)
    {
        if (constructionGO == null)
            return null;

        var saveable = constructionGO.GetComponent<Saveable>();
        if (saveable != null && !string.IsNullOrWhiteSpace(saveable.uniqueID))
            return saveable.uniqueID;

        return constructionGO.GetInstanceID().ToString();
    }

    private void TagConstructionReservation(string reservationId, GameObject constructionGO)
    {
        if (populationManager == null || string.IsNullOrWhiteSpace(reservationId))
            return;

        populationManager.UpdateReservationMetadata(
            reservationId,
            PopulationReservationKind.Construction,
            GetReservationOwnerId(constructionGO),
            nameof(BuildingConstruction));
    }

    private void TagConstructionReservation(string reservationId, BuildingConstruction bc)
    {
        if (bc == null)
            return;

        TagConstructionReservation(reservationId, bc.gameObject);
    }

    public bool StartConstruction(GameObject constructionGO, Building def, string reservationIdFromPlacement, int reservedPop, int turnsRequired)
    {
        if (constructionGO == null)
        {
            Debug.LogError("[ConstructionManager] StartConstruction: constructionGO was null.");
            return false;
        }

        if (def == null)
        {
            Debug.LogError("[ConstructionManager] StartConstruction: Building def is null.");
            return false;
        }

        if (populationManager == null)
            populationManager = PlayersPopulationManager.Instance;

        if (populationManager == null)
        {
            Debug.LogError("[ConstructionManager] populationManager is not assigned.");
            return false;
        }

        BuildingConstruction bc = constructionGO.GetComponent<BuildingConstruction>();
        if (bc == null)
        {
            Debug.LogError($"[ConstructionManager] The construction prefab '{constructionGO.name}' is missing BuildingConstruction.");
            return false;
        }

        string reservationId = reservationIdFromPlacement;
        int requiredPopulation = Mathf.Max(1, reservedPop);

        if (string.IsNullOrEmpty(reservationId))
        {
            if (!populationManager.TryReservePopulation(
                    requiredPopulation,
                    PopulationReservationKind.Construction,
                    GetReservationOwnerId(constructionGO),
                    nameof(BuildingConstruction),
                    out reservationId))
            {
                Debug.LogWarning($"[ConstructionManager] Could not reserve population for construction (need {requiredPopulation}).");
                return false;
            }
            else
            {
                Debug.Log($"[ConstructionManager] Reserved population locally (id:{reservationId}).");
            }
        }
        else
        {
            Debug.Log($"[ConstructionManager] Using reservation from placement (id:{reservationId}).");

            populationManager.UpdateReservationMetadata(
                reservationId,
                PopulationReservationKind.Construction,
                GetReservationOwnerId(constructionGO),
                nameof(BuildingConstruction));
        }

        bc.Initialize(def, turnsRequired, requiredPopulation, reservationId);

        Debug.Log($"[ConstructionManager] BeginConstruction on '{constructionGO.name}' " +
                  $"(turns:{turnsRequired}, pop:{requiredPopulation}, building:'{def.buildingName}').");

        bc.BeginConstruction();

        if (bc.TurnsToComplete <= 0)
            Debug.LogWarning("[ConstructionManager] Warning: turnsToComplete <= 0 (check Building.buildTurnsRequired).");

        TagConstructionReservation(reservationId, constructionGO);

        active.Add(bc);
        return true;
    }

    private void HandleCancelRequested(BuildingConstruction bc)
    {
        if (bc == null)
            return;

        if (!string.IsNullOrEmpty(bc.ReservationId))
            populationManager?.ReleaseReservation(bc.ReservationId);

        active.Remove(bc);
        Destroy(bc.gameObject);
    }

    private void OnTurnEnded()
    {
        List<BuildingConstruction> snapshot = new List<BuildingConstruction>(active.Count);
        foreach (BuildingConstruction bc in active)
            snapshot.Add(bc);

        if (processingCoroutine != null)
            StopCoroutine(processingCoroutine);

        processingCoroutine = StartCoroutine(ProcessConstructions(snapshot));
    }

    private IEnumerator ProcessConstructions(List<BuildingConstruction> pending)
    {
        int idx = 0;
        List<BuildingConstruction> toRemove = new List<BuildingConstruction>();

        while (idx < pending.Count)
        {
            int end = Mathf.Min(idx + maxConstructionsPerFrame, pending.Count);

            for (int i = idx; i < end; i++)
            {
                BuildingConstruction bc = pending[i];
                if (bc == null || !bc.IsActive)
                {
                    toRemove.Add(bc);
                    continue;
                }

                bool completed = bc.AdvanceOneTurn();
                if (completed)
                {
                    if (!string.IsNullOrEmpty(bc.ReservationId))
                        populationManager?.ReleaseReservation(bc.ReservationId);

                    GameObject finalGO = bc.CompleteAndSpawnFinal();
                    if (finalGO != null)
                    {
                        BuildingInstance tag = finalGO.GetComponent<BuildingInstance>();
                        if (!tag)
                            tag = finalGO.AddComponent<BuildingInstance>();

                        tag.definition = bc.Definition;
                        tag.isStarter = false;

                        playerBuildingManager?.Register(tag);
                    }

                    Building bdef = bc.Definition;
                    if (bdef != null)
                        playerLevel?.AddXP(Mathf.RoundToInt(bdef.buildTurnsRequired * 2f));

                    PostBuildingNotification(bdef, finalGO);

                    active.Remove(bc);
                    Destroy(bc.gameObject);
                    toRemove.Add(bc);
                }
            }

            idx = end;
            yield return null;
        }

        for (int i = 0; i < toRemove.Count; i++)
            active.Remove(toRemove[i]);

        processingCoroutine = null;
    }

    public void ClearAllConstructionsForLoad()
    {
        if (processingCoroutine != null)
        {
            StopCoroutine(processingCoroutine);
            processingCoroutine = null;
        }

        active.Clear();
    }

    public void RegisterLoadedConstruction(BuildingConstruction bc)
    {
        if (bc == null)
            return;

        if (bc.IsActive)
        {
            active.Add(bc);

            if (populationManager == null)
                populationManager = PlayersPopulationManager.Instance;

            if (populationManager != null && !string.IsNullOrWhiteSpace(bc.ReservationId))
            {
                populationManager.UpdateReservationMetadata(
                    bc.ReservationId,
                    PopulationReservationKind.Construction,
                    GetReservationOwnerId(bc.gameObject),
                    nameof(BuildingConstruction));
            }
        }
        else
        {
            active.Remove(bc);
        }
    }

    public IEnumerator TutorialGhostCompleteConstruction(BuildingConstruction bc)
    {
        if (bc == null)
            yield break;

        while (bc != null && bc.IsActive && bc.TurnsLeft > 0)
        {
            if (TurnSystem.Instance != null)
            {
                yield return TurnSystem.Instance.StartCoroutine(
                    TurnSystem.Instance.RunGhostPhaseAdvance(() => TutorialAdvanceConstructionOneTurn(bc))
                );
            }
            else
            {
                TutorialAdvanceConstructionOneTurn(bc);
                yield return null;
            }
        }
    }

    private void TutorialAdvanceConstructionOneTurn(BuildingConstruction bc)
    {
        if (bc == null || !bc.IsActive)
            return;

        if (!active.Contains(bc))
            return;

        bool completed = bc.AdvanceOneTurn();
        if (!completed)
            return;

        if (!string.IsNullOrEmpty(bc.ReservationId))
            populationManager?.ReleaseReservation(bc.ReservationId);

        GameObject finalGO = bc.CompleteAndSpawnFinal();
        if (finalGO != null)
        {
            BuildingInstance tag = finalGO.GetComponent<BuildingInstance>();
            if (!tag)
                tag = finalGO.AddComponent<BuildingInstance>();

            tag.definition = bc.Definition;
            tag.isStarter = false;

            playerBuildingManager?.Register(tag);
        }

        PostBuildingNotification(bc.Definition, finalGO);

        active.Remove(bc);
        Destroy(bc.gameObject);
    }

    private static void PostBuildingNotification(Building bdef, GameObject finalGO)
    {
        if (NotificationManager.Instance == null) return;

        string buildingName = bdef != null ? bdef.buildingName : "Building";
        UnityEngine.Vector3 pos = finalGO != null ? finalGO.transform.position : UnityEngine.Vector3.zero;

        string title, message;
        if (NotificationMessageCrafterManager.Instance != null)
            (title, message) = NotificationMessageCrafterManager.Instance.CraftBuilding(buildingName);
        else
        {
            title   = "Construction Complete";
            message = $"{buildingName} has been constructed.";
        }

        NotificationManager.Instance.AddNotification(NotificationType.BuildingCompleted, title, message, pos);
    }
}