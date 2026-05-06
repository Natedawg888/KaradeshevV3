using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class WorldObjectsSaveSection : SaveSectionBase
{
    public override string Key => SaveSectionKeys.WorldObjects;

    public override IEnumerator CaptureInto(
        SaveSnapshot snapshot,
        SaveCaptureContext context,
        int objectsPerFrame)
    {
        snapshot.tiles.Clear();
        snapshot.buildings.Clear();
        snapshot.constructions.Clear();

        int batchSize = Mathf.Max(1, objectsPerFrame);
        int batch = 0;

        List<TileSaveable> tiles = new List<TileSaveable>(TileSaveable.Live);
        for (int i = 0; i < tiles.Count; i++)
        {
            TileSaveable tileSaveable = tiles[i];
            if (tileSaveable == null)
                continue;

            TileScript tile = tileSaveable.GetComponent<TileScript>();
            if (tile == null)
                continue;

            SaveData tileData = tileSaveable.SaveState();
            string tilePrefabName = CleanPrefabName(tile.gameObject.name);
            EnvironmentRuntimeSaveData environmentData = CaptureEnvironmentState(tile);

            snapshot.tiles.Add(new TileSaveData(tileData, tilePrefabName, environmentData));
            tileSaveable.ClearDirty();

            batch++;
            if (batch >= batchSize)
            {
                batch = 0;
                yield return null;
            }
        }

        BuildingSaveable[] liveBuildings = Object.FindObjectsOfType<BuildingSaveable>(true);
        Debug.Log($"[WorldObjectsSaveSection] Found {liveBuildings.Length} BuildingSaveables for save capture.");

        for (int i = 0; i < liveBuildings.Length; i++)
        {
            BuildingSaveable building = liveBuildings[i];
            if (building == null)
                continue;

            SaveData saveData = building.SaveState();
            string prefabName = CleanPrefabName(building.gameObject.name);

            snapshot.buildings.Add(new BuildingTileSaveData(saveData, prefabName));
            building.ClearDirty();

            batch++;
            if (batch >= batchSize)
            {
                batch = 0;
                yield return null;
            }
        }

        Debug.Log($"[WorldObjectsSaveSection] Captured {snapshot.buildings.Count} buildings.");

        // CONSTRUCTIONS
        ConstructionTileSaveable[] liveConstructions = Object.FindObjectsOfType<ConstructionTileSaveable>(true);
        Debug.Log($"[WorldObjectsSaveSection] Found {liveConstructions.Length} ConstructionTileSaveables for save capture.");

        for (int i = 0; i < liveConstructions.Length; i++)
        {
            ConstructionTileSaveable construction = liveConstructions[i];
            if (construction == null)
                continue;

            snapshot.constructions.Add(construction.GetSaveData());
            construction.ClearDirty();

            batch++;
            if (batch >= batchSize)
            {
                batch = 0;
                yield return null;
            }
        }


        ClearDirty();
    }

    private static string CleanPrefabName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return string.Empty;

        return rawName.Replace("(Clone)", "").Trim();
    }

    private static EnvironmentRuntimeSaveData CaptureEnvironmentState(TileScript tile)
    {
        if (tile == null)
            return null;

        GameObject spawnedInstance = tile.GetSpawnedInstance();
        if (spawnedInstance == null)
            return null;

        EnvironmentControl envControl = spawnedInstance.GetComponentInChildren<EnvironmentControl>(true);
        if (envControl == null)
            return null;

        EnvironmentRuntimeSaveData data = envControl.CaptureRuntimeSaveData(
            CleanPrefabName(spawnedInstance.name),
            spawnedInstance.transform.localEulerAngles.y
        );

        if (data == null)
            return null;

        VolcanoTileState volcano = spawnedInstance.GetComponentInChildren<VolcanoTileState>(true);
        if (volcano == null)
            volcano = spawnedInstance.GetComponentInParent<VolcanoTileState>(true);

        if (volcano != null)
            data.volcanoData = volcano.CaptureRuntimeSaveData();

        return data;
    }
}