#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using ET = EnvironmentType;

// Usage: Tools → Kardeshev → Create Savanna Spawner Definitions
// Creates or updates 9 ResourceSpawnerDefinition assets in
// Assets/ScriptableObjects/ResourceSpawners/Savanna/
// Re-run safe: existing assets are updated, not duplicated.
public static class SavannaSpawnerCreator
{
    private const string SaveFolder = "Assets/ScriptableObjects/ResourceSpawners/Savanna";
    private const string ResRoot    = "Assets/Resources/ResourceDefinition";

    [MenuItem("Tools/Kardeshev/Create Savanna Spawner Definitions")]
    public static void CreateAll()
    {
        EnsureFolders();

        var found   = new List<string>();
        var missing = new List<string>();
        int created = 0, updated = 0;

        // ── Load ResourceDefinitions ─────────────────────────────────────────
        var dryGrass   = Load("Stage0/CaveShelter00/SimpleCarrying/DriedGrass.asset",   "Dry Grass (DriedGrass)",  found, missing);
        var grass      = Load("Stage0/CaveShelter00/SimpleCarrying/Grass.asset",         "Grass",                   found, missing);
        var tubers     = Load("Stage0/GatheringGrounds00/RootDigging/Tubers.asset",      "Tubers",                  found, missing);
        /*var roots =*/   Load("MISSING/Roots.asset",                                     "Roots",                   found, missing);
        var berries    = Load("Stage0/StartingResources/Berries.asset",                  "Berries",                 found, missing);
        /*var insects =*/ Load("MISSING/Insects.asset",                                   "Insects (generic)",       found, missing);
        var grubs      = Load("Stage0/GatheringGrounds00/InsectForaging/Grubs.asset",    "Grubs",                   found, missing);
        var grasshops  = Load("Stage0/GatheringGrounds00/InsectForaging/GrasshoppersLocusts.asset", "GrasshoppersLocusts", found, missing);
        var sticks     = Load("Stage0/CaveShelter00/SimpleCarrying/Sticks.asset",        "Sticks",                  found, missing);
        var wood       = Load("Stage0/CaveShelter01/MaterialAwarenessI/Wood.asset",      "Wood",                    found, missing);
        var bark       = Load("Stage0/CaveShelter01/MaterialAwarenessI/Bark.asset",      "Bark",                    found, missing);
        var leaves     = Load("Stage0/CaveShelter00/SimpleCarrying/Leaves.asset",        "Leaves",                  found, missing);
        var stones     = Load("Stage0/CaveShelter00/SimpleCarrying/Stones.asset",        "Stones",                  found, missing);
        var flint      = Load("Stage0/CaveShelter01/MaterialAwarenessI/Flint.asset",     "Flint",                   found, missing);
        var medHerbs   = Load("Stage0/ShamanHut/HerbLore/MedicinalHerbs.asset",          "MedicinalHerbs",          found, missing);
        var embers     = Load("Stage0/CaveShelter00/SimpleCarrying/Embers.asset",        "Embers",                  found, missing);
        var charcoal   = Load("Stage0/CaveShelter01/CharcoalMaking/Charcoal.asset",      "Charcoal",                found, missing);
        /*var ash =*/     Load("MISSING/Ash.asset",                                       "Ash",                     found, missing);
        /*var burntWood =*/ Load("MISSING/BurntWood.asset",                               "Burnt Wood",              found, missing);
        var freshMeat  = Load("Stage0/CaveShelter02/Butchery&ProcessingI/Fresh_Meat.asset", "Fresh_Meat (Scavenged Meat substitute)", found, missing);
        /*var scavMeat =*/ Load("MISSING/Scavenged_Meat.asset",                            "Scavenged Meat",          found, missing);
        var bones      = Load("Stage0/CaveShelter02/Butchery&ProcessingI/Bones.asset",   "Bones",                   found, missing);
        var skins      = Load("Stage0/CaveShelter02/Butchery&ProcessingI/AnimalSkins.asset", "AnimalSkins (Hide)", found, missing);
        var sinew      = Load("Stage0/CaveShelter02/Butchery&ProcessingI/Sinew.asset",   "Sinew",                   found, missing);
        var feathers   = Load("Stage0/CaveShelter02/Butchery&ProcessingI/Feathers.asset","Feathers",                found, missing);

        var savannaEnv = new ResourceSpawnerConditionSettings
            { requiredEnvironmentTypes = new List<ET> { ET.Savanna } };
        var burntConds = new ResourceSpawnerConditionSettings
            { requiredEnvironmentTypes = new List<ET> { ET.Savanna }, requiresHasBeenIgnited = true };
        var carcassConds = new ResourceSpawnerConditionSettings
            { requiredEnvironmentTypes = new List<ET> { ET.Savanna }, requiresHasCarcass = true };

        // ── Climate curves ────────────────────────────────────────────────────
        // Savanna baseline: ~25-35°C, humidity 0.3-0.65

        // Hot/dry preferring (Dry Grass): higher in heat+dryness, penalised when wet
        var dryGrassClimate = Climate(
            Curve(new Keyframe(-10f, 0.2f), new Keyframe(10f, 0.6f),
                  new Keyframe(20f, 1.0f),  new Keyframe(35f, 1.4f), new Keyframe(50f, 1.4f)),
            Curve(new Keyframe(0.0f, 1.4f), new Keyframe(0.30f, 1.2f),
                  new Keyframe(0.5f, 1.0f), new Keyframe(0.65f, 0.5f), new Keyframe(1.0f, 0.1f)));

        // Moist-preferring (Root Patch): bonus after rain, penalty in extreme dry
        var rootClimate = Climate(
            Curve(new Keyframe(-5f, 0.3f),  new Keyframe(10f, 0.7f),
                  new Keyframe(20f, 1.0f),  new Keyframe(32f, 1.0f),
                  new Keyframe(42f, 0.6f),  new Keyframe(50f, 0.4f)),
            Curve(new Keyframe(0.0f, 0.2f), new Keyframe(0.2f, 0.6f),
                  new Keyframe(0.4f, 1.0f), new Keyframe(0.65f, 1.3f), new Keyframe(1.0f, 1.1f)));

        // Warm+moderate preferring (Berry Shrub): strongly reduced in dry
        var berryClimate = Climate(
            Curve(new Keyframe(-5f, 0.1f),  new Keyframe(10f, 0.5f),
                  new Keyframe(20f, 1.0f),  new Keyframe(30f, 1.0f),
                  new Keyframe(38f, 0.6f),  new Keyframe(50f, 0.2f)),
            Curve(new Keyframe(0.0f, 0.1f), new Keyframe(0.2f, 0.3f),
                  new Keyframe(0.35f, 0.8f),new Keyframe(0.55f, 1.0f),
                  new Keyframe(0.70f, 1.2f),new Keyframe(1.0f,  0.7f)));

        // Warm/hot preferring (Insect Mound): penalised cold or flooded
        var insectClimate = Climate(
            Curve(new Keyframe(-10f, 0.1f), new Keyframe(5f, 0.4f),
                  new Keyframe(15f, 0.8f),  new Keyframe(25f, 1.0f),
                  new Keyframe(35f, 1.3f),  new Keyframe(50f, 1.3f)),
            Curve(new Keyframe(0.0f, 0.7f), new Keyframe(0.35f, 1.0f),
                  new Keyframe(0.55f, 1.1f),new Keyframe(0.75f, 0.8f),
                  new Keyframe(0.90f, 0.4f),new Keyframe(1.0f,  0.2f)));

        // Dry-tolerant (Fallen Branches): mostly neutral, slight dry bonus
        var branchClimate = Climate(
            Curve(new Keyframe(-10f, 0.6f), new Keyframe(0f, 0.8f),
                  new Keyframe(15f, 1.0f),  new Keyframe(42f, 1.1f), new Keyframe(50f, 1.0f)),
            Curve(new Keyframe(0.0f, 1.1f), new Keyframe(0.4f, 1.0f),
                  new Keyframe(0.7f, 1.0f), new Keyframe(1.0f, 0.8f)));

        // Uncommon/rare (Medicinal Herbs): rare in dry, better in moderate
        var herbClimate = Climate(
            Curve(new Keyframe(-10f, 0.3f), new Keyframe(10f, 0.6f),
                  new Keyframe(20f, 1.0f),  new Keyframe(32f, 1.0f),
                  new Keyframe(42f, 0.6f),  new Keyframe(50f, 0.3f)),
            Curve(new Keyframe(0.0f, 0.1f), new Keyframe(0.2f, 0.3f),
                  new Keyframe(0.35f, 0.7f),new Keyframe(0.5f, 1.0f),
                  new Keyframe(0.65f, 1.4f),new Keyframe(1.0f, 1.1f)));

        // ── 1. RS_SavannaDryGrassPatch ───────────────────────────────────────
        Apply("RS_SavannaDryGrassPatch",
            "savanna_dry_grass_patch", "Savanna Dry Grass Patch",
            SpawnerCategory.GroundMaterial, 0.9f, 2, true, false, 0,
            savannaEnv, dryGrassClimate,
            "Main savanna material spawner. Bonus in hot/dry; penalised in wet. " +
            "Climate: temp bonus >20°C (peaks at 35°C), humidity penalty above 0.5.",
            O(dryGrass, 2, 5), O(grass, 1, 4, w: 0.7f));
        if (WasCreated) created++; else updated++;

        // ── 2. RS_SavannaRootPatch ────────────────────────────────────────────
        Apply("RS_SavannaRootPatch",
            "savanna_root_patch", "Savanna Root Patch",
            SpawnerCategory.Root, 0.65f, 4, true, false, 0,
            savannaEnv, rootClimate,
            "Basic early food source. Better after rain/humidity. Worse in extreme dry. " +
            "Roots resource not yet in project — add output when created.",
            O(tubers, 1, 3));
        if (WasCreated) created++; else updated++;

        // ── 3. RS_SavannaBerryShrub ───────────────────────────────────────────
        Apply("RS_SavannaBerryShrub",
            "savanna_berry_shrub", "Savanna Berry Shrub",
            SpawnerCategory.Bush, 0.5f, 5, true, false, 0,
            savannaEnv, berryClimate,
            "Less reliable than forest berries. Strongly reduced in dry. " +
            "Bonus in moderate humidity. Wire season filter when season system is expanded.",
            O(berries, 1, 4, c: 0.7f));
        if (WasCreated) created++; else updated++;

        // ── 4. RS_SavannaInsectMound ──────────────────────────────────────────
        Apply("RS_SavannaInsectMound",
            "savanna_insect_mound", "Savanna Insect Mound",
            SpawnerCategory.GroundMaterial, 0.7f, 3, true, false, 0,
            savannaEnv, insectClimate,
            "Strong Paleolithic food. Bonus in warm/hot. Penalised in cold or flooded. " +
            "Generic Insects resource not in project — GrasshoppersLocusts used as insect substitute.",
            O(grubs, 1, 4), O(grasshops, 1, 3, w: 0.8f));
        if (WasCreated) created++; else updated++;

        // ── 5. RS_SavannaFallenBranches ───────────────────────────────────────
        Apply("RS_SavannaFallenBranches",
            "savanna_fallen_branches", "Savanna Fallen Branches",
            SpawnerCategory.Tree, 0.6f, 6, true, false, 0,
            savannaEnv, branchClimate,
            "Scattered shrubs, dead branches, small trees. Dry-tolerant — slight dry bonus. " +
            "Wire to storm/wind weather events when available for extra output.",
            O(sticks, 2, 5), O(wood, 1, 3, w: 0.6f), O(bark, 1, 2, w: 0.5f, c: 0.7f),
            O(leaves, 1, 3, w: 0.5f));
        if (WasCreated) created++; else updated++;

        // ── 6. RS_SavannaStoneScatter ─────────────────────────────────────────
        Apply("RS_SavannaStoneScatter",
            "savanna_stone_scatter", "Savanna Stone Scatter",
            SpawnerCategory.StoneDeposit, 0.7f, 6, true, false, 0,
            savannaEnv, null, // climate disabled — stone is unaffected
            "Non-perishable, low-regeneration material. Climate has no effect.",
            O(stones, 2, 6), O(flint, 1, 3, w: 0.7f));
        if (WasCreated) created++; else updated++;

        // ── 7. RS_SavannaMedicinalHerbs ───────────────────────────────────────
        Apply("RS_SavannaMedicinalHerbs",
            "savanna_medicinal_herbs", "Savanna Medicinal Herbs",
            SpawnerCategory.Plant, 0.35f, 7, true, false, 0,
            savannaEnv, herbClimate,
            "Uncommon/rare. Strongly reduced in dry conditions. " +
            "Bonus in moderate to high humidity.",
            O(medHerbs, 0, 2, c: 0.4f));
        if (WasCreated) created++; else updated++;

        // ── 8. RS_SavannaAshRemains (event-based, temporary) ─────────────────
        Apply("RS_SavannaAshRemains",
            "savanna_ash_remains", "Savanna Ash Remains",
            SpawnerCategory.BurntRemains, 0.8f, 2, false, true, 10,
            burntConds, null, // event-based — climate not evaluated
            "TRIGGER: OnFireExtinguished or OnTileIgnited. Do NOT add as base spawner. " +
            "Created by TileStateResourceSpawnerHandler. " +
            "Ash and Burnt Wood outputs missing — add when those ResourceDefinition assets are created.",
            O(embers, 1, 3), O(charcoal, 2, 4, w: 0.8f));
        if (WasCreated) created++; else updated++;

        // ── 9. RS_SavannaCarcassRemains (event-based, temporary) ─────────────
        Apply("RS_SavannaCarcassRemains",
            "savanna_carcass_remains", "Savanna Carcass Remains",
            SpawnerCategory.AnimalRemains, 0.9f, 1, false, true, 5,
            carcassConds, null, // event-based — climate not evaluated
            "TRIGGER: OnAnimalDeath. Do NOT add as base spawner. " +
            "Created by AnimalDeathResourceSpawnerHandler. " +
            "Fresh_Meat used as Scavenged Meat substitute. " +
            "Feathers should be added for bird species when supported.",
            O(freshMeat, 1, 3), O(bones, 1, 3, w: 0.9f), O(skins, 0, 1, w: 0.7f, c: 0.8f),
            O(sinew, 1, 2, w: 0.6f), O(feathers, 0, 2, w: 0.4f, c: 0.3f));
        if (WasCreated) created++; else updated++;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        PrintReport(found, missing, created, updated);
    }

    // ── Tracking ──────────────────────────────────────────────────────────────

    private static bool WasCreated;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void EnsureFolders()
    {
        var steps = new[]
        {
            ("Assets",                             "ScriptableObjects"),
            ("Assets/ScriptableObjects",           "ResourceSpawners"),
            ("Assets/ScriptableObjects/ResourceSpawners", "Savanna")
        };
        foreach (var (parent, child) in steps)
        {
            string full = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(full))
                AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static ResourceDefinition Load(string sub, string label,
                                            List<string> found, List<string> missing)
    {
        var def = AssetDatabase.LoadAssetAtPath<ResourceDefinition>($"{ResRoot}/{sub}");
        if (def != null) found.Add(label);
        else             missing.Add(label);
        return def;
    }

    private static ResourceSpawnerOutput O(ResourceDefinition res, int min, int max,
                                            float w = 1f, float c = 1f)
    {
        if (res == null) return null;
        return new ResourceSpawnerOutput
        { resource = res, minAmount = min, maxAmount = max, weight = w, chance = c, addToExistingStack = true };
    }

    private static AnimationCurve Curve(params Keyframe[] keys) => new AnimationCurve(keys);

    private static ResourceSpawnerClimateSettings Climate(AnimationCurve tempCurve,
                                                           AnimationCurve humCurve)
    {
        return new ResourceSpawnerClimateSettings
        {
            enabled          = true,
            temperatureCurve = tempCurve,
            humidityCurve    = humCurve
        };
    }

    private static void Apply(string assetName, string id, string displayName,
                               SpawnerCategory cat, float chance, int interval,
                               bool permanent, bool canExpire, int lifetime,
                               ResourceSpawnerConditionSettings conds,
                               ResourceSpawnerClimateSettings climate,
                               string notes,
                               params ResourceSpawnerOutput[] outputs)
    {
        string path = SaveFolder + "/" + assetName + ".asset";
        var    so   = AssetDatabase.LoadAssetAtPath<ResourceSpawnerDefinition>(path);
        WasCreated  = so == null;
        if (WasCreated) so = ScriptableObject.CreateInstance<ResourceSpawnerDefinition>();

        so.spawnerID          = id;
        so.displayName        = displayName;
        so.category           = cat;
        so.baseSpawnChance    = chance;
        so.spawnIntervalTurns = interval;
        so.isPermanent        = permanent;
        so.canExpire          = canExpire;
        so.lifetimeTurns      = lifetime;
        so.debugNotes         = notes;
        so.conditions         = conds ?? new ResourceSpawnerConditionSettings();
        so.climate            = climate ?? new ResourceSpawnerClimateSettings();

        so.outputs = new System.Collections.Generic.List<ResourceSpawnerOutput>();
        foreach (var o in outputs)
            if (o != null) so.outputs.Add(o);

        if (WasCreated) AssetDatabase.CreateAsset(so, path);
        else            EditorUtility.SetDirty(so);
    }

    private static void PrintReport(List<string> found, List<string> missing, int created, int updated)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Savanna Spawner Creation Report ===");
        sb.AppendLine("Spawners created : " + created);
        sb.AppendLine("Spawners updated : " + updated);
        sb.AppendLine("\nResources FOUND (" + found.Count + "):");
        foreach (var r in found) sb.AppendLine("  + " + r);
        sb.AppendLine("\nResources MISSING / SKIPPED (" + missing.Count + "):");
        foreach (var r in missing) sb.AppendLine("  - " + r + "  [no asset — output skipped]");
        sb.AppendLine("\nClimate curves wired for: DryGrass, Root, Berry, Insects, Branches, Herbs.");
        sb.AppendLine("Climate DISABLED for: StoneScatter, AshRemains, CarcassRemains (event-based).");
        Debug.Log(sb.ToString());
    }
}
#endif
