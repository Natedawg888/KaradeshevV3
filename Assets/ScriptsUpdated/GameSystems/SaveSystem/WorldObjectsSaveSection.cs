using System.Collections;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

public sealed class WorldObjectsSaveSection : SaveSectionBase
{
    public override string Key => SaveSectionKeys.WorldObjects;

    // Reused every save — no per-save heap allocation for these lists
    private static readonly List<TileSaveable>              _tileBuf         = new List<TileSaveable>(256);
    private static readonly List<BuildingSaveable>          _buildingBuf     = new List<BuildingSaveable>(64);
    private static readonly List<ConstructionTileSaveable>  _constructionBuf = new List<ConstructionTileSaveable>(16);

    private static readonly ProfilerMarker _pmTiles         = new ProfilerMarker("SaveSystem.Capture.WorldObjects.Tiles");
    private static readonly ProfilerMarker _pmBuildings     = new ProfilerMarker("SaveSystem.Capture.WorldObjects.Buildings");
    private static readonly ProfilerMarker _pmConstructions = new ProfilerMarker("SaveSystem.Capture.WorldObjects.Constructions");

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

        // --- TILES ---
        // Use the Live registry instead of FindObjectsOfType; copy into reuse buffer
        // so mid-iteration structural changes to Live don't cause exceptions.
        _tileBuf.Clear();
        foreach (TileSaveable t in TileSaveable.Live)
            if (t != null) _tileBuf.Add(t);

        for (int i = 0; i < _tileBuf.Count; i++)
        {
            TileSaveable tileSaveable = _tileBuf[i];
            TileScript tile = tileSaveable.GetComponent<TileScript>();
            if (tile == null)
                continue;

            _pmTiles.Begin();
            SaveData tileData = tileSaveable.SaveState();
            string tilePrefabName = CleanPrefabName(tile.gameObject.name);
            EnvironmentRuntimeSaveData environmentData = CaptureEnvironmentState(tile);
            snapshot.tiles.Add(new TileSaveData(tileData, tilePrefabName, environmentData));
            tileSaveable.ClearDirty();
            _pmTiles.End();

            batch++;
            if (batch >= batchSize)
            {
                batch = 0;
                yield return null;
            }
        }

        // --- BUILDINGS ---
        // BuildingSaveable.Live is the authoritative registry — no FindObjectsOfType needed
        _buildingBuf.Clear();
        foreach (BuildingSaveable b in BuildingSaveable.Live)
            if (b != null) _buildingBuf.Add(b);

        for (int i = 0; i < _buildingBuf.Count; i++)
        {
            BuildingSaveable building = _buildingBuf[i];

            _pmBuildings.Begin();
            SaveData saveData = building.SaveState();
            string prefabName = CleanPrefabName(building.gameObject.name);
            snapshot.buildings.Add(new BuildingTileSaveData(saveData, prefabName));
            building.ClearDirty();
            _pmBuildings.End();

            batch++;
            if (batch >= batchSize)
            {
                batch = 0;
                yield return null;
            }
        }

        // --- CONSTRUCTIONS ---
        _constructionBuf.Clear();
        foreach (ConstructionTileSaveable c in ConstructionTileSaveable.Live)
            if (c != null) _constructionBuf.Add(c);

        for (int i = 0; i < _constructionBuf.Count; i++)
        {
            ConstructionTileSaveable construction = _constructionBuf[i];

            _pmConstructions.Begin();
            snapshot.constructions.Add(construction.GetSaveData());
            construction.ClearDirty();
            _pmConstructions.End();

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
