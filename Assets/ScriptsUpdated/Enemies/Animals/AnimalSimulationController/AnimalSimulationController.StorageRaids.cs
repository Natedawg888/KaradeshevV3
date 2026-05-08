using System.Collections.Generic;
using UnityEngine;

public partial class AnimalSimulationController : MonoBehaviour
{
    private readonly Dictionary<TileCoord, StorageBuildingControl> _storageByTile =
        new Dictionary<TileCoord, StorageBuildingControl>();

    private readonly List<(TileCoord tile, int foodAmount)> _storageFoodBuffer =
        new List<(TileCoord, int)>();

    // ------------------------------------------------------------------
    // Called from HandleTurnEnded in TurnsAndSpawning
    // ------------------------------------------------------------------

    internal void RefreshStorageTiles()
    {
        _storageByTile.Clear();
        _storageFoodBuffer.Clear();

        var storages = FindObjectsOfType<StorageBuildingControl>(true);
        if (_grid == null) _grid = FindObjectOfType<GridManager>();

        for (int i = 0; i < storages.Length; i++)
        {
            var s = storages[i];
            if (s == null || !s.isActiveAndEnabled) continue;

            int food = CountEdibleFood(s);
            if (food <= 0) continue;

            var gp   = _grid != null ? _grid.GetGridPosition(s.transform.position) : Vector2Int.zero;
            var coord = new TileCoord { x = gp.x, y = gp.y };

            _storageByTile[coord] = s;
            _storageFoodBuffer.Add((coord, food));
        }

        _simulation.SetStorageFoodTiles(_storageFoodBuffer);
    }

    internal void RefreshRepelledTiles()
    {
        var repelled = new HashSet<TileCoord>();
        if (_grid == null) _grid = FindObjectOfType<GridManager>();

        foreach (var repeller in AnimalRepellerRegistry.Active)
        {
            if (repeller == null) continue;

            var gp     = _grid != null ? _grid.GetGridPosition(repeller.transform.position) : Vector2Int.zero;
            int radius = Mathf.Max(1, repeller.repelRadiusTiles);

            for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
                repelled.Add(new TileCoord { x = gp.x + dx, y = gp.y + dy });
        }

        _simulation.SetRepelledTiles(repelled);
    }

    // ------------------------------------------------------------------
    // Event handler — called by AnimalSimulation when a group raids storage
    // ------------------------------------------------------------------

    private void HandleGroupAttemptedStorageRaid(int animalGroupId, TileCoord tile, int requestedAmount)
    {
        if (_simulation == null) return;
        if (!_storageByTile.TryGetValue(tile, out var storage) || storage == null) return;
        if (!_simulation.TryGetGroup(animalGroupId, out var animal) || !animal.isAlive) return;

        var species = animal.species;
        if (species == null) return;

        int totalStolen = 0;
        int remaining   = requestedAmount;

        for (int i = storage.storedResources.Count - 1; i >= 0 && remaining > 0; i--)
        {
            var item = storage.storedResources[i];
            if (item == null || item.amount <= 0) continue;

            var def = item.definition;
            if (def == null) continue;
            if (def.resourceType != ResourceType.Food) continue;
            if (!IsEdibleForSpecies(def, species)) continue;

            int take = Mathf.Min(item.amount, remaining);
            item.amount -= take;
            totalStolen += take;
            remaining   -= take;

            if (item.amount <= 0)
                storage.storedResources.RemoveAt(i);
        }

        if (totalStolen <= 0) return;

        storage.RecalculateTotalStoredAmount();
        storage.UpdateStorageIcon();

        // Satisfy some hunger
        float hungerReduction = totalStolen * Mathf.Max(0.1f, species.hungerPerResourceUnit);
        var updatedAnimal     = animal;
        updatedAnimal.hunger  = Mathf.Max(0f, updatedAnimal.hunger - hungerReduction);
        _simulation.SetGroup(updatedAnimal);

        // Refresh cache so the simulation knows food levels changed
        RefreshStorageTiles();

        PostStorageRaidedNotification(animal, species, storage, totalStolen);
    }

    private static bool IsEdibleForSpecies(ResourceDefinition def, AnimalDefinition species)
    {
        if (def == null || species == null) return false;

        // No explicit list → accept any Food type
        if (species.edibleResources == null || species.edibleResources.Length == 0)
            return def.resourceType == ResourceType.Food;

        for (int i = 0; i < species.edibleResources.Length; i++)
            if (species.edibleResources[i] == def) return true;

        return false;
    }

    private static int CountEdibleFood(StorageBuildingControl storage)
    {
        int total = 0;
        if (storage == null || storage.storedResources == null) return 0;

        for (int i = 0; i < storage.storedResources.Count; i++)
        {
            var item = storage.storedResources[i];
            if (item == null || item.amount <= 0) continue;

            var def = item.definition;
            if (def == null) continue;
            if (def.resourceType == ResourceType.Food)
                total += item.amount;
        }

        return total;
    }

    private static void PostStorageRaidedNotification(
        AnimalGroupState animal,
        AnimalDefinition species,
        StorageBuildingControl storage,
        int amountStolen)
    {
        if (NotificationManager.Instance == null) return;

        string speciesName   = !string.IsNullOrWhiteSpace(species.displayName) ? species.displayName : "Animals";
        string buildingName  = storage != null && !string.IsNullOrWhiteSpace(storage.name)
            ? storage.name : "storage";
        Vector3 pos = storage != null ? storage.transform.position : default;

        string title, message;
        if (NotificationMessageCrafterManager.Instance != null)
            (title, message) = NotificationMessageCrafterManager.Instance.CraftAnimalStorageRaided(
                speciesName, buildingName, amountStolen);
        else
            (title, message) = ("Storage Raided!", $"{speciesName} stole {amountStolen} food from {buildingName}.");

        NotificationManager.Instance.AddNotification(NotificationType.AnimalStorageRaided, title, message, pos);
    }
}
