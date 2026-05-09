#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// Usage: Tools → Kardeshev → Create Base Resource Spawner Definitions
// Generates 83 permanent base spawner SOs under
// Assets/ScriptableObjects/ResourceSpawners/Base/
// Re-run safe: updates existing assets rather than duplicating.
// Event-based spawners (embers, ash, carcass, dung, flood debris) are NOT created here.
public static partial class BaseSpawnerCreator
{
    private const string Root    = "Assets/ScriptableObjects/ResourceSpawners/Base";
    private const string ResRoot = "Assets/Resources/ResourceDefinition";

    private static readonly Dictionary<string, ResourceDefinition> _res = new();
    private static readonly List<string> _found   = new();
    private static readonly List<string> _missing = new();
    private static int _created, _updated;

    [MenuItem("Tools/Kardeshev/Create Base Resource Spawner Definitions")]
    public static void CreateAll()
    {
        _found.Clear(); _missing.Clear(); _res.Clear();
        _created = 0; _updated = 0;
        EnsureFolders();
        LoadResources();
        CreateLandSpawners();
        CreateWaterSpawners();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        PrintReport();
        PrintPrefabAssignments();
    }

    // ── Resource loading ──────────────────────────────────────────────────────

    private static void LoadResources()
    {
        void L(string k, string path, string label)
        {
            var r = AssetDatabase.LoadAssetAtPath<ResourceDefinition>($"{ResRoot}/{path}");
            _res[k] = r;
            if (r != null) _found.Add(label);
            else           _missing.Add(label);
        }

        // ── Present ───────────────────────────────────────────────────────────
        L("berries",     "Stage0/StartingResources/Berries.asset",                            "Berries");
        L("leaves",      "Stage0/CaveShelter00/SimpleCarrying/Leaves.asset",                  "Leaves");
        L("sticks",      "Stage0/CaveShelter00/SimpleCarrying/Sticks.asset",                  "Sticks");
        L("bark",        "Stage0/CaveShelter01/MaterialAwarenessI/Bark.asset",                "Bark");
        L("mushrooms",   "Stage0/CaveShelter00/ResourceAwarenessI/Mushrooms.asset",           "Mushrooms");
        L("ediblemush",  "Stage0/GatheringGrounds00/FungusIdentification/EdibleMushrooms.asset","EdibleMushrooms");
        L("fiber",       "Stage0/CaveShelter00/SimpleCarrying/Fiber.asset",                   "Fiber (Plant Fibre)");
        L("tubers",      "Stage0/GatheringGrounds00/RootDigging/Tubers.asset",                "Tubers");
        L("seeds",       "Stage0/CaveShelter00/ResourceAwarenessI/Seeds.asset",               "Seeds");
        L("herbs",       "Stage0/CaveShelter00/ResourceAwarenessI/Herbs.asset",               "Herbs");
        L("medherbs",    "Stage0/ShamanHut/HerbLore/MedicinalHerbs.asset",                    "MedicinalHerbs");
        L("grasshops",   "Stage0/GatheringGrounds00/InsectForaging/GrasshoppersLocusts.asset", "GrasshoppersLocusts");
        L("grubs",       "Stage0/GatheringGrounds00/InsectForaging/Grubs.asset",              "Grubs");
        L("antslarvae",  "Stage0/GatheringGrounds00/InsectForaging/AntsLarvae.asset",         "AntsLarvae");
        L("stones",      "Stage0/CaveShelter00/SimpleCarrying/Stones.asset",                  "Stones");
        L("flint",       "Stage0/CaveShelter01/MaterialAwarenessI/Flint.asset",               "Flint");
        L("obsidian",    "Folders/Materials/Obsidian.asset",                                  "Obsidian");
        L("clay",        "Folders/Materials/Clay.asset",                                      "Clay");
        L("shellfish",   "Stage0/GatheringGrounds00/CoastalForaging/Shellfish.asset",         "Shellfish");
        L("seaweed",     "Stage0/GatheringGrounds00/CoastalForaging/Seaweed.asset",           "Seaweed");
        L("waterplants", "Stage0/GatheringGrounds00/PlantIdentification/Water_Plants.asset",  "Water Plants");
        L("eggs",        "Stage0/GatheringGrounds00/NestFinding/Eggs.asset",                  "Eggs");
        L("feathers",    "Stage0/CaveShelter02/Butchery&ProcessingI/Feathers.asset",          "Feathers");
        L("fruits",      "Stage0/CaveShelter00/ResourceAwarenessI/Fruits.asset",              "Fruits");
        L("driedgrass",  "Stage0/CaveShelter00/SimpleCarrying/DriedGrass.asset",              "DriedGrass");
        L("salt",        "Stage0/StoneKnappingStation00/MaterialAwarenessII/Salt.asset",      "Salt");
        L("wood",        "Stage0/CaveShelter01/MaterialAwarenessI/Wood.asset",                "Wood");
        L("bones",       "Stage0/CaveShelter02/Butchery&ProcessingI/Bones.asset",             "Bones");
        L("vines",       "Stage0/CaveShelter00/SimpleCarrying/Vines.asset",                   "Vines");
        L("ferns",       "Stage0/GatheringGrounds00/PlantIdentification/Ferns.asset",         "Ferns");
        L("honey",       "Stage0/GatheringGrounds00/HoneyFinding/Honey.asset",                "Honey");
        L("nuts",        "Stage0/StartingResources/Nuts.asset",                               "Nuts");
        L("mud",         "Stage0/CaveShelter01/MaterialAwarenessI/Mud.asset",                 "Mud");
        L("water",       "Stage0/StartingResources/Water.asset",                               "Water");
        L("freshwater",  "Folders/FoodO/Water/FreshWater.asset",                               "FreshWater");
        L("contwater",   "Folders/FoodO/Water/Contaminated_Water.asset",                       "Contaminated Water");

        // ── Missing — outputs using these are skipped, reported below ─────────
        L("roots",       "MISSING/Roots.asset",       "Roots [MISSING]");
        L("branches",    "MISSING/Branches.asset",    "Branches [MISSING — Wood used as substitute]");
        L("sharpstone",  "MISSING/SharpStone.asset",  "Sharp Stone [MISSING — Flint used as substitute]");
        L("mineral",     "MISSING/Mineral.asset",     "Mineral [MISSING — Clay used as substitute]");
        L("driftwood",   "MISSING/Driftwood.asset",   "Driftwood [MISSING — Sticks used as fallback]");
        L("lichen",      "MISSING/Lichen.asset",       "Lichen [MISSING]");
        L("reeds",       "MISSING/Reeds.asset",        "Reeds [MISSING]");
        L("shells",      "MISSING/Shells.asset",       "Shells [MISSING]");
        L("guano",       "MISSING/Guano.asset",        "Guano [MISSING]");
        L("volcanicrock","MISSING/VolcanicRock.asset", "Volcanic Rock [MISSING]");
        L("insects",     "MISSING/Insects.asset",      "Insects (generic) [MISSING — GrasshoppersLocusts used]");
        L("larvae",      "MISSING/Larvae.asset",       "Larvae [MISSING — AntsLarvae/Grubs used]");
    }

    // ── Compact helpers ───────────────────────────────────────────────────────

    private static ResourceDefinition G(string k)
        => _res.TryGetValue(k, out var r) ? r : null;

    private static ResourceSpawnerOutput O(ResourceDefinition r, int mn, int mx,
                                            float w = 1f, float c = 1f)
        => r == null ? null
         : new ResourceSpawnerOutput
           { resource = r, minAmount = mn, maxAmount = mx, weight = w, chance = c, addToExistingStack = true };

    private static EnvironmentType[]     E(params EnvironmentType[]     e) => e;
    private static EnvironmentTileType[] T(params EnvironmentTileType[] t) => t;

    // ── Climate presets ───────────────────────────────────────────────────────

    private static AnimationCurve Crv(params Keyframe[] k) => new AnimationCurve(k);

    private static ResourceSpawnerClimateSettings Clim(AnimationCurve tc, AnimationCurve hc)
        => new ResourceSpawnerClimateSettings { enabled = true, temperatureCurve = tc, humidityCurve = hc };

    private static ResourceSpawnerClimateSettings MushClim() => Clim(
        Crv(new Keyframe(-20f,0.6f),new Keyframe(15f,1f),new Keyframe(28f,0.7f),new Keyframe(42f,0.3f)),
        Crv(new Keyframe(0f,0.4f),new Keyframe(0.35f,1f),new Keyframe(0.6f,1.4f),new Keyframe(1f,1.4f)));

    private static ResourceSpawnerClimateSettings DryGrassClim() => Clim(
        Crv(new Keyframe(-10f,0.3f),new Keyframe(15f,1f),new Keyframe(30f,1.3f),new Keyframe(50f,1.3f)),
        Crv(new Keyframe(0f,1.3f),new Keyframe(0.35f,1f),new Keyframe(0.55f,0.6f),new Keyframe(0.8f,0.2f)));

    private static ResourceSpawnerClimateSettings RootClim() => Clim(
        Crv(new Keyframe(-5f,0.4f),new Keyframe(15f,1f),new Keyframe(35f,1f),new Keyframe(45f,0.6f)),
        Crv(new Keyframe(0f,0.2f),new Keyframe(0.25f,0.7f),new Keyframe(0.5f,1f),new Keyframe(0.7f,1.3f)));

    private static ResourceSpawnerClimateSettings BerryClim() => Clim(
        Crv(new Keyframe(-5f,0.2f),new Keyframe(15f,1f),new Keyframe(30f,1f),new Keyframe(42f,0.5f)),
        Crv(new Keyframe(0f,0.1f),new Keyframe(0.25f,0.5f),new Keyframe(0.5f,1f),new Keyframe(0.75f,1.2f)));

    private static ResourceSpawnerClimateSettings InsectClim() => Clim(
        Crv(new Keyframe(-10f,0.1f),new Keyframe(10f,0.7f),new Keyframe(25f,1f),new Keyframe(40f,1.3f)),
        Crv(new Keyframe(0f,0.7f),new Keyframe(0.4f,1f),new Keyframe(0.7f,0.9f),new Keyframe(1f,0.5f)));

    private static ResourceSpawnerClimateSettings HerbClim() => Clim(
        Crv(new Keyframe(-10f,0.4f),new Keyframe(15f,1f),new Keyframe(35f,1f),new Keyframe(45f,0.6f)),
        Crv(new Keyframe(0f,0.1f),new Keyframe(0.3f,0.5f),new Keyframe(0.5f,1f),new Keyframe(0.75f,1.4f)));

    // ── Apply ─────────────────────────────────────────────────────────────────

    private static void A(string sub, string id, string dn, SpawnerCategory cat,
        float ch, int iv, EnvironmentType[] envs, EnvironmentTileType[] tiles,
        ResourceSpawnerClimateSettings cl,
        params ResourceSpawnerOutput[] outs)
    {
        string path = Root + "/" + sub + ".asset";
        var so = AssetDatabase.LoadAssetAtPath<ResourceSpawnerDefinition>(path);
        bool isNew = so == null;
        if (isNew) so = ScriptableObject.CreateInstance<ResourceSpawnerDefinition>();
        so.spawnerID = id; so.displayName = dn; so.category = cat;
        so.baseSpawnChance = ch; so.spawnIntervalTurns = iv;
        so.isPermanent = true; so.canExpire = false;
        so.debugNotes = $"Base spawner: {dn} ({cat})";
        so.conditions = new ResourceSpawnerConditionSettings
        {
            requiredEnvironmentTypes = envs  != null
                ? new List<EnvironmentType>(envs)     : new List<EnvironmentType>(),
            requiredTileTypes        = tiles != null
                ? new List<EnvironmentTileType>(tiles) : new List<EnvironmentTileType>()
        };
        so.climate = cl ?? new ResourceSpawnerClimateSettings();
        so.outputs = new List<ResourceSpawnerOutput>();
        foreach (var o in outs) if (o != null) so.outputs.Add(o);
        if (isNew) { AssetDatabase.CreateAsset(so, path); _created++; }
        else       { EditorUtility.SetDirty(so);           _updated++; }
    }

    // ── Folders ───────────────────────────────────────────────────────────────

    private static void EnsureFolders()
    {
        void Mk(string p)
        {
            if (AssetDatabase.IsValidFolder(p)) return;
            int i = p.LastIndexOf('/');
            AssetDatabase.CreateFolder(p.Substring(0, i), p.Substring(i + 1));
        }
        Mk("Assets/ScriptableObjects");
        Mk("Assets/ScriptableObjects/ResourceSpawners");
        Mk(Root);
        foreach (var s in new[]{ "BorealForest","TemperateForest","TropicalForest","SubTropical",
            "Grassland","Savanna","Desert","Tundra","Mountain","Volcano",
            "Beach","Ocean","Lake","LakeEdge","River","SaltLake","Cave" })
            Mk(Root + "/" + s);
    }

    // ── Report ────────────────────────────────────────────────────────────────

    private static void PrintReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Base Spawner Creation Report ===");
        sb.AppendLine("Created: " + _created + "   Updated: " + _updated);
        sb.AppendLine("\nResources FOUND (" + _found.Count + "):");
        foreach (var r in _found)   sb.AppendLine("  + " + r);
        sb.AppendLine("\nResources MISSING / SKIPPED (" + _missing.Count + "):");
        foreach (var r in _missing) sb.AppendLine("  - " + r);
        Debug.Log(sb.ToString());
    }

    private static void PrintPrefabAssignments()
    {
        Debug.Log(
            "=== Prefab baseSpawner Assignments ===\n" +
            "BorealForest Land:       RS_BorealFallenBranchPile, RS_BorealBerryShrub, RS_BorealMushroomPatch, RS_BorealGroundCover, RS_BorealStoneScatter\n" +
            "TemperateForest Land:    RS_TemperateBerryShrub, RS_TemperateMushroomPatch, RS_TemperateFallenBranches, RS_TemperateRootPatch, RS_TemperateInsectNest, RS_TemperateHerbPatch, RS_TemperateBirdNestTree\n" +
            "TropicalForest Land:     RS_TropicalFruitBush, RS_TropicalMushroomPatch, RS_TropicalInsectNest, RS_TropicalDenseGroundCover, RS_TropicalFallenBranches, RS_TropicalMedicinalHerbs\n" +
            "SubTropical Land:        RS_SubTropicalBerryShrub, RS_SubTropicalRootPatch, RS_SubTropicalHerbPatch, RS_SubTropicalInsectNest, RS_SubTropicalFallenBranches, RS_SubTropicalSeedPatch\n" +
            "Grassland Land:          RS_GrasslandDryGrassPatch, RS_GrasslandSeedPatch, RS_GrasslandRootPatch, RS_GrasslandInsectNest, RS_GrasslandStoneScatter\n" +
            "Savanna Land:            RS_SavannaDryGrassPatch, RS_SavannaRootPatch, RS_SavannaInsectMound, RS_SavannaFallenBranches, RS_SavannaStoneScatter\n" +
            "Savanna Rich:            add RS_SavannaBerryShrub + RS_SavannaMedicinalHerbs\n" +
            "Desert Land:             RS_DesertDryPlant, RS_DesertStoneScatter, RS_DesertInsectBurrow\n" +
            "Desert Oasis:            add RS_DesertRootPatch\n" +
            "Tundra Land:             RS_TundraGroundCover, RS_TundraRootPatch, RS_TundraStoneScatter, RS_TundraLichenPatch\n" +
            "Mountain:                RS_MountainRockScatter, RS_MountainHerbPatch, RS_MountainRootPatch, RS_MountainMineralVein\n" +
            "Volcano:                 RS_VolcanicRockScatter, RS_VolcanicMineralDeposit, RS_ObsidianScatter\n" +
            "Beach:                   RS_BeachShellfishBed, RS_BeachSeaweedPatch, RS_BeachDriftwoodPile, RS_BeachCoastalGrass, RS_BeachStoneScatter\n" +
            "Ocean:                   RS_OceanSeaweedPatch\n" +
            "Shallow Ocean/Coastline: RS_ShallowOceanShellfishBed, RS_ShallowOceanSeaweedPatch\n" +
            "Lake:                    RS_LakeWaterPlants, RS_LakeReedPatch, RS_LakeFreshwaterShellfish\n" +
            "LakeEdge:                RS_LakeEdgeReedPatch, RS_LakeEdgeRootPatch, RS_LakeEdgeFreshwaterShellfish, RS_LakeEdgeDriftwoodPile, RS_LakeEdgeHerbPatch\n" +
            "River:                   RS_RiverReedPatch, RS_RiverRootPatch, RS_RiverDriftwoodPile, RS_RiverFreshwaterShellfish\n" +
            "RiverMouth:              RS_RiverMouthShellfishBed, RS_RiverMouthSeaweedPatch, RS_RiverMouthDriftwoodPile, RS_RiverMouthReedPatch\n" +
            "SaltLake:                RS_SaltLakeSaltCrust, RS_SaltLakeStoneScatter, RS_SaltLakeBrineMinerals\n" +
            "Cave:                    RS_CaveStoneScatter, RS_CaveMineralVein, RS_CaveMushroomPatch\n" +
            "Damp Cave:               add RS_CaveRootPatch\n" +
            "Volcanic Cave:           RS_CaveStoneScatter + RS_CaveMineralVein + RS_VolcanicMineralDeposit + RS_ObsidianScatter\n" +
            "Cave (bats):             add RS_CaveBatNest\n" +
            "\nNOTE: Event-based spawners (embers/ash/carcass/dung/floods) are NOT in Base/ — they are added at runtime by handlers."
        );
    }
}
#endif
