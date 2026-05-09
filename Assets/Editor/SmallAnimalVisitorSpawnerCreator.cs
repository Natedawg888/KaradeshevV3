using System.IO;
using UnityEditor;
using UnityEngine;

// Tools → Kardeshev → Create Small Animal Visitor Spawners
// Creates ResourceSpawnerDefinition SOs for small animal visitors.
// Run once; safe to re-run (skips existing assets).
public static class SmallAnimalVisitorSpawnerCreator
{
    private const string OutRoot = "Assets/ScriptableObjects/ResourceSpawners/Visitors";

    private const string FishPath      = "Assets/Resources/ResourceDefinition/Stage0/StoneKnappingStation00/FishingToolsI/Fish.asset";
    private const string SmallGamePath = "Assets/Resources/ResourceDefinition/Stage0/GatheringGrounds00/HuntingTactics/Small_Game.asset";
    private const string ShellfishPath = "Assets/Resources/ResourceDefinition/Stage0/GatheringGrounds00/CoastalForaging/Shellfish.asset";

    [MenuItem("Tools/Kardeshev/Create Small Animal Visitor Spawners")]
    public static void CreateAll()
    {
        if (!Directory.Exists(OutRoot))
            Directory.CreateDirectory(OutRoot);

        var fish      = AssetDatabase.LoadAssetAtPath<ResourceDefinition>(FishPath);
        var smallGame = AssetDatabase.LoadAssetAtPath<ResourceDefinition>(SmallGamePath);
        var shellfish = AssetDatabase.LoadAssetAtPath<ResourceDefinition>(ShellfishPath);

        if (!fish)      Debug.LogWarning($"[VisitorCreator] Fish not found at {FishPath}");
        if (!smallGame) Debug.LogWarning($"[VisitorCreator] Small_Game not found at {SmallGamePath}");
        if (!shellfish) Debug.LogWarning($"[VisitorCreator] Shellfish not found at {ShellfishPath}");

        int created = 0;

        // ── Fish visitor — rivers, lakes, water tiles ────────────────────────
        if (A("RS_FishVisitor", "Fish Visitor",
              SpawnerCategory.SmallAnimal, 0.75f, 1, true, 4, 1,
              O(fish, 1, 4, 1f)))
            created++;

        // ── Ocean fish — ocean and coastal tiles ─────────────────────────────
        if (A("RS_OceanFishVisitor", "Ocean Fish Visitor",
              SpawnerCategory.SmallAnimal, 0.70f, 1, true, 4, 1,
              O(fish,      1, 3, 0.7f),
              O(shellfish, 1, 2, 0.5f)))
            created++;

        // ── Shellfish — beach, coastline, salt lake ──────────────────────────
        if (A("RS_ShellfishVisitor", "Shellfish Visitor",
              SpawnerCategory.SmallAnimal, 0.65f, 1, true, 3, 1,
              O(shellfish, 1, 4, 1f)))
            created++;

        // ── Small game — forest and grassland land tiles ─────────────────────
        if (A("RS_ForestSmallGameVisitor", "Forest Small Game Visitor",
              SpawnerCategory.SmallAnimal, 0.60f, 1, true, 3, 1,
              O(smallGame, 1, 3, 1f)))
            created++;

        // ── Small game — desert, savanna, mountain ───────────────────────────
        if (A("RS_AridSmallGameVisitor", "Arid Small Game Visitor",
              SpawnerCategory.SmallAnimal, 0.50f, 1, true, 3, 1,
              O(smallGame, 1, 2, 1f)))
            created++;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[VisitorCreator] Done — created {created} new visitor spawner SOs in {OutRoot}/");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ResourceSpawnerOutput O(ResourceDefinition res,
                                           int min, int max, float chance)
    {
        return new ResourceSpawnerOutput
        {
            resource          = res,
            minAmount         = min,
            maxAmount         = max,
            chance            = chance,
            weight            = 1f,
            addToExistingStack = true
        };
    }

    private static bool A(string id, string displayName,
                           SpawnerCategory cat,
                           float spawnChance, int intervalTurns,
                           bool canExpire, int lifetimeTurns, int maxUses,
                           params ResourceSpawnerOutput[] outputs)
    {
        string path = $"{OutRoot}/{id}.asset";
        if (AssetDatabase.LoadAssetAtPath<ResourceSpawnerDefinition>(path) != null)
            return false;

        var so = ScriptableObject.CreateInstance<ResourceSpawnerDefinition>();
        so.spawnerID        = id;
        so.displayName      = displayName;
        so.category         = cat;
        so.baseSpawnChance  = spawnChance;
        so.spawnIntervalTurns = intervalTurns;
        so.isPermanent      = false;
        so.canExpire        = canExpire;
        so.lifetimeTurns    = lifetimeTurns;
        so.maxUses          = maxUses;
        so.outputs.AddRange(outputs);
        so.debugNotes       = "Visitor spawner — added temporarily by SmallAnimalVisitorSystem. " +
                              "Expires via maxUses=1 (caught) or lifetime (left).";

        AssetDatabase.CreateAsset(so, path);
        return true;
    }
}
