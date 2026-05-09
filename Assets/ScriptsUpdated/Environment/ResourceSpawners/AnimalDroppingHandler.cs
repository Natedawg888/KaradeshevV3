using System;
using UnityEngine;

// Attach alongside EnvironmentResourceNode.
// When an animal arrives: adds the dung spawner and begins depositing Dung.
// When the last animal leaves: removes the spawner and starts a drying timer.
// After dungDryingTurns with no animals present: converts all Dung → DriedDung.
//
// Call sites in animal simulation:
//   AnimalDroppingHandler.OnAnimalEnteredTile?.Invoke(
//       new AnimalTileRequest { targetNode = node, speciesID = group.species?.speciesID });
//   AnimalDroppingHandler.OnAnimalLeftTile?.Invoke(
//       new AnimalTileRequest { targetNode = node, speciesID = group.species?.speciesID });
public class AnimalDroppingHandler : MonoBehaviour
{
    [Header("Spawner Definition")]
    [Tooltip("ResourceSpawnerDefinition SO that produces Dung while animals are present.")]
    public ResourceSpawnerDefinition dungDropSpawner;

    [Header("Dung Drying")]
    [Tooltip("Turns after the last animal leaves before Dung converts to DriedDung.")]
    [Min(1)] public int dungDryingTurns = 4;

    [Tooltip("ResourceDefinition for Dung (resourceID: DNG).")]
    public ResourceDefinition dungDefinition;

    [Tooltip("ResourceDefinition for DriedDung (resourceID: DDNG).")]
    public ResourceDefinition driedDungDefinition;

    [Header("Debug")]
    [SerializeField] private bool debugLogging;

    // ── Static events ────────────────────────────────────────────────────────
    public static Action<AnimalTileRequest> OnAnimalEnteredTile;
    public static Action<AnimalTileRequest> OnAnimalLeftTile;

    private EnvironmentResourceNode node;
    private int activeAnimalCount      = 0;
    private int turnsSinceLastDeposit  = -1; // -1 = no drying pending

    // ── Setup ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        node = GetComponent<EnvironmentResourceNode>();
        if (node == null)
            Debug.LogWarning($"[DungHandler] [{name}] No EnvironmentResourceNode found.");
    }

    private void OnEnable()
    {
        OnAnimalEnteredTile += HandleAnimalEntered;
        OnAnimalLeftTile    += HandleAnimalLeft;
        TurnSystem.SubscribeToEndOfTurn(HandleTurnEnd);
    }

    private void OnDisable()
    {
        OnAnimalEnteredTile -= HandleAnimalEntered;
        OnAnimalLeftTile    -= HandleAnimalLeft;
        TurnSystem.UnsubscribeFromEndOfTurn(HandleTurnEnd);
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void HandleAnimalEntered(AnimalTileRequest req)
    {
        if (node == null || req?.targetNode != node) return;

        activeAnimalCount++;
        node.SetTileState(TileStateFlags.HasActiveAnimal, true);
        node.SetTileState(TileStateFlags.HasFreshDung, true);

        // Reset any in-progress drying — fresh dung is being deposited
        turnsSinceLastDeposit = 0;

        if (dungDropSpawner == null) return;

        if (!node.HasSpawner(dungDropSpawner.spawnerID))
        {
            node.AddSpawner(dungDropSpawner, SpawnerSourceReason.AnimalPresence);
            if (debugLogging)
                Debug.Log($"[DungHandler] [{name}] Animal arrived (count={activeAnimalCount}) " +
                          $"— dung spawner '{dungDropSpawner.displayName}' added");
        }
    }

    private void HandleAnimalLeft(AnimalTileRequest req)
    {
        if (node == null || req?.targetNode != node) return;

        activeAnimalCount = Mathf.Max(0, activeAnimalCount - 1);

        if (activeAnimalCount > 0) return; // other animals still on tile

        node.SetTileState(TileStateFlags.HasActiveAnimal, false);

        if (dungDropSpawner != null)
        {
            node.RemoveSpawner(dungDropSpawner.spawnerID);
            if (debugLogging)
                Debug.Log($"[DungHandler] [{name}] All animals left — dung spawner removed");
        }

        // Start drying only if there is actually dung to dry
        int dungNow = dungDefinition != null ? node.GetAmount(dungDefinition) : 0;
        if (dungNow > 0)
        {
            turnsSinceLastDeposit = 0;
            if (debugLogging)
                Debug.Log($"[DungHandler] [{name}] Drying timer started — {dungNow}x Dung present, " +
                          $"will convert in {dungDryingTurns} turns");
        }
        else
        {
            turnsSinceLastDeposit = -1;
        }
    }

    // ── Turn tick ─────────────────────────────────────────────────────────────

    private void HandleTurnEnd()
    {
        if (node == null || turnsSinceLastDeposit < 0) return;

        // While animals are still present, keep resetting the timer
        if (activeAnimalCount > 0)
        {
            turnsSinceLastDeposit = 0;
            return;
        }

        turnsSinceLastDeposit++;
        if (debugLogging)
            Debug.Log($"[DungHandler] [{name}] Drying: {turnsSinceLastDeposit}/{dungDryingTurns} turns");

        if (turnsSinceLastDeposit >= dungDryingTurns)
            ConvertDungToDried();
    }

    // ── Conversion ────────────────────────────────────────────────────────────

    private void ConvertDungToDried()
    {
        if (dungDefinition == null || driedDungDefinition == null)
        {
            Debug.LogWarning($"[DungHandler] [{name}] dungDefinition or driedDungDefinition not assigned.");
            turnsSinceLastDeposit = -1;
            return;
        }

        int amount = node.GetAmount(dungDefinition);
        if (amount <= 0)
        {
            turnsSinceLastDeposit = -1;
            node.SetTileState(TileStateFlags.HasFreshDung, false);
            return;
        }

        node.Consume(dungDefinition, amount);
        node.AddResource(driedDungDefinition, amount);

        turnsSinceLastDeposit = -1;
        node.SetTileState(TileStateFlags.HasFreshDung, false);

        if (debugLogging)
            Debug.Log($"[DungHandler] [{name}] Converted {amount}x Dung → DriedDung " +
                      $"(dried after {dungDryingTurns} turns)");
    }
}

// ── Request object passed via static events ─────────────────────────────────
public class AnimalTileRequest
{
    public EnvironmentResourceNode targetNode;
    public string speciesID;
}
