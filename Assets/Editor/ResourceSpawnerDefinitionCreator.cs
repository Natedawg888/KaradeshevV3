#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using ET  = EnvironmentType;
using ETT = EnvironmentTileType;

// Usage: Tools → Kardeshev → Create Resource Spawner Definitions
// Creates all ResourceSpawnerDefinition .asset files under Assets/Resources/ResourceSpawners/
// Safe to re-run — will overwrite existing assets.
public static class ResourceSpawnerDefinitionCreator
{
    private const string OutRoot = "Assets/Resources/ResourceSpawners";
    private const string ResRoot = "Assets/Resources/ResourceDefinition";

    [MenuItem("Tools/Kardeshev/Create Resource Spawner Definitions")]
    public static void CreateAll()
    {
        CreateFolders();

        // ── Load ResourceDefinitions ────────────────────────────────────────────
        var berries      = R("Stage0/StartingResources/Berries.asset");
        var fruits       = R("Stage0/CaveShelter00/ResourceAwarenessI/Fruits.asset");
        var nuts         = R("Stage0/StartingResources/Nuts.asset");
        var mushrooms    = R("Stage0/CaveShelter00/ResourceAwarenessI/Mushrooms.asset");
        var edMush       = R("Stage0/GatheringGrounds00/FungusIdentification/EdibleMushrooms.asset");
        var hoofFungus   = R("Stage0/GatheringGrounds00/FungusIdentification/Hoof_fungus.asset");
        var sticks       = R("Stage0/CaveShelter00/SimpleCarrying/Sticks.asset");
        var wood         = R("Stage0/CaveShelter01/MaterialAwarenessI/Wood.asset");
        var bark         = R("Stage0/CaveShelter01/MaterialAwarenessI/Bark.asset");
        var leaves       = R("Stage0/CaveShelter00/SimpleCarrying/Leaves.asset");
        var ferns        = R("Stage0/GatheringGrounds00/PlantIdentification/Ferns.asset");
        var vines        = R("Stage0/CaveShelter00/SimpleCarrying/Vines.asset");
        var eggs         = R("Stage0/GatheringGrounds00/NestFinding/Eggs.asset");
        var honey        = R("Stage0/GatheringGrounds00/HoneyFinding/Honey.asset");
        var grass        = R("Stage0/CaveShelter00/SimpleCarrying/Grass.asset");
        var driedGrass   = R("Stage0/CaveShelter00/SimpleCarrying/DriedGrass.asset");
        var fiber        = R("Stage0/CaveShelter00/SimpleCarrying/Fiber.asset");
        var seeds        = R("Stage0/CaveShelter00/ResourceAwarenessI/Seeds.asset");
        var herbs        = R("Stage0/CaveShelter00/ResourceAwarenessI/Herbs.asset");
        var medHerbs     = R("Stage0/ShamanHut/HerbLore/MedicinalHerbs.asset");
        var edFlowers    = R("Stage0/GatheringGrounds00/PlantIdentification/Edible_Flowers.asset");
        var tubers       = R("Stage0/GatheringGrounds00/RootDigging/Tubers.asset");
        var leafyGreens  = R("Stage0/StartingResources/LeafyGreens.asset");
        var grasshops    = R("Stage0/GatheringGrounds00/InsectForaging/GrasshoppersLocusts.asset");
        var caterpillars = R("Stage0/GatheringGrounds00/InsectForaging/Caterpillars.asset");
        var grubs        = R("Stage0/GatheringGrounds00/InsectForaging/Grubs.asset");
        var antsLarvae   = R("Stage0/GatheringGrounds00/InsectForaging/AntsLarvae.asset");
        var termites     = R("Stage0/GatheringGrounds00/InsectForaging/Termites.asset");
        var shellfish    = R("Stage0/GatheringGrounds00/CoastalForaging/Shellfish.asset");
        var seaweed      = R("Stage0/GatheringGrounds00/CoastalForaging/Seaweed.asset");
        var waterPlants  = R("Stage0/GatheringGrounds00/PlantIdentification/Water_Plants.asset");
        var stones       = R("Stage0/CaveShelter00/SimpleCarrying/Stones.asset");
        var flint        = R("Stage0/CaveShelter01/MaterialAwarenessI/Flint.asset");
        var obsidian     = R("Folders/Materials/Obsidian.asset");
        var clay         = R("Folders/Materials/Clay.asset");
        var mud          = R("Stage0/CaveShelter01/MaterialAwarenessI/Mud.asset");
        var treeResin    = R("Stage0/CaveShelter01/Resin&Adhesives/Tree_resin.asset");
        var embers       = R("Stage0/CaveShelter00/SimpleCarrying/Embers.asset");
        var charcoal     = R("Stage0/CaveShelter01/CharcoalMaking/Charcoal.asset");
        var water        = R("Stage0/StartingResources/Water.asset");
        var freshWater   = R("Folders/FoodO/Water/FreshWater.asset");
        var freshMeat    = R("Stage0/CaveShelter02/Butchery&ProcessingI/Fresh_Meat.asset");
        var bones        = R("Stage0/CaveShelter02/Butchery&ProcessingI/Bones.asset");
        var skins        = R("Stage0/CaveShelter02/Butchery&ProcessingI/AnimalSkins.asset");
        var feathers     = R("Stage0/CaveShelter02/Butchery&ProcessingI/Feathers.asset");
        var sinew        = R("Stage0/CaveShelter02/Butchery&ProcessingI/Sinew.asset");
        var fat          = R("Stage0/CaveShelter02/Butchery&ProcessingI/Fat.asset");

        // ── FOREST ─────────────────────────────────────────────────────────────
        C("Forest/BerryBushSpawner", "berry_bush", "Berry Bush", SpawnerCategory.Plant,
            0.75f, 3, EnvC(ET.BorealForest, ET.TemperateForest, ET.TropicalForest, ET.SubTropical),
            O(berries, 2, 5), O(fruits, 1, 3, w: 0.7f, c: 0.6f), O(nuts, 1, 2, w: 0.5f, c: 0.4f));

        C("Forest/MushroomPatchSpawner", "mushroom_patch", "Mushroom Patch", SpawnerCategory.Plant,
            0.6f, 4, EnvC(ET.BorealForest, ET.TemperateForest, ET.TropicalForest, ET.SubTropical),
            O(mushrooms, 1, 3), O(edMush, 1, 3, w: 0.8f), O(hoofFungus, 0, 2, w: 0.4f, c: 0.3f));

        C("Forest/FallenBranchSpawner", "fallen_branch", "Fallen Branch", SpawnerCategory.Plant,
            0.8f, 2, EnvC(ET.BorealForest, ET.TemperateForest, ET.TropicalForest, ET.SubTropical),
            O(sticks, 2, 6), O(wood, 1, 3, w: 0.6f), O(bark, 1, 2, w: 0.5f, c: 0.7f));

        C("Forest/ForestGroundCoverSpawner", "forest_ground_cover", "Forest Ground Cover", SpawnerCategory.Plant,
            0.7f, 3, EnvC(ET.BorealForest, ET.TemperateForest, ET.TropicalForest, ET.SubTropical),
            O(leaves, 2, 5), O(ferns, 1, 3, w: 0.8f), O(vines, 1, 2, w: 0.5f, c: 0.6f));

        C("Forest/TreeNestSpawner", "tree_nest", "Tree Nest", SpawnerCategory.Plant,
            0.4f, 6, EnvC(ET.BorealForest, ET.TemperateForest, ET.TropicalForest, ET.SubTropical),
            O(eggs, 1, 3), O(honey, 1, 2, w: 0.5f, c: 0.4f));

        C("Forest/ForestInsectSpawner", "forest_insects", "Forest Insects", SpawnerCategory.Plant,
            0.65f, 3, EnvC(ET.BorealForest, ET.TemperateForest, ET.TropicalForest, ET.SubTropical),
            O(grubs, 1, 4), O(caterpillars, 1, 3, w: 0.8f), O(termites, 1, 3, w: 0.6f));

        // ── GRASSLAND ──────────────────────────────────────────────────────────
        C("Grassland/DryGrassSpawner", "dry_grass", "Dry Grass Field", SpawnerCategory.GroundMaterial,
            0.9f, 2, EnvC(ET.Grassland, ET.Savanna),
            O(grass, 3, 8), O(driedGrass, 2, 5, w: 0.8f), O(fiber, 1, 3, w: 0.6f));

        C("Grassland/SeedPlantSpawner", "seed_plant", "Seed Plant", SpawnerCategory.Plant,
            0.65f, 3, EnvC(ET.Grassland, ET.Savanna, ET.SubTropical),
            O(seeds, 1, 4), O(herbs, 1, 3, w: 0.7f), O(edFlowers, 1, 2, w: 0.4f, c: 0.5f));

        C("Grassland/GrassInsectSpawner", "grass_insects", "Grassland Insects", SpawnerCategory.Plant,
            0.5f, 4, EnvC(ET.Grassland, ET.Savanna),
            O(grasshops, 1, 4), O(caterpillars, 1, 3, w: 0.8f), O(antsLarvae, 1, 3, w: 0.6f));

        C("Grassland/TuberRootSpawner", "tuber_root", "Root and Tuber Patch", SpawnerCategory.GroundMaterial,
            0.55f, 4, EnvC(ET.Grassland, ET.Savanna, ET.SubTropical),
            O(tubers, 1, 3), O(leafyGreens, 1, 3, w: 0.7f));

        // ── COASTAL / WATER ────────────────────────────────────────────────────
        C("Coastal/ShellfishBedSpawner", "shellfish_bed", "Shellfish Bed", SpawnerCategory.WaterCoastal,
            0.8f, 3, TileC(ETT.Coastline, ETT.CoastlineCorner, ETT.Beach, ETT.BeachEnd),
            O(shellfish, 2, 6));

        C("Coastal/SeaweedPatchSpawner", "seaweed_patch", "Seaweed Patch", SpawnerCategory.WaterCoastal,
            0.75f, 3, TileC(ETT.Coastline, ETT.CoastlineCorner, ETT.Beach),
            O(seaweed, 2, 5), O(waterPlants, 1, 3, w: 0.7f));

        C("Coastal/DriftwoodSpawner", "driftwood", "Driftwood", SpawnerCategory.WaterCoastal,
            0.5f, 5, TileC(ETT.Beach, ETT.BeachEnd, ETT.Coastline),
            O(sticks, 2, 4), O(wood, 1, 2, w: 0.6f));

        C("Coastal/RiverSourceSpawner", "river_source", "River Source", SpawnerCategory.WaterCoastal,
            0.9f, 1, TileC(ETT.River, ETT.RiverCorner, ETT.RiverSplit, ETT.RiverMouth, ETT.RiverCross, ETT.RiverEnd),
            O(water, 2, 5), O(freshWater, 1, 3, w: 0.8f), O(waterPlants, 1, 2, w: 0.5f, c: 0.6f));

        // ── TERRAIN ────────────────────────────────────────────────────────────
        C("Terrain/MountainRockSpawner", "mountain_rock", "Mountain Rock Outcrop", SpawnerCategory.GroundMaterial,
            0.8f, 2, EnvC(ET.Mountain),
            O(stones, 3, 8), O(flint, 1, 3, w: 0.7f), O(obsidian, 0, 2, w: 0.3f, c: 0.25f));

        C("Terrain/MountainHerbSpawner", "mountain_herb", "Mountain Herb Patch", SpawnerCategory.Plant,
            0.5f, 4, EnvC(ET.Mountain),
            O(herbs, 1, 3), O(medHerbs, 0, 2, w: 0.5f, c: 0.4f));

        C("Terrain/DesertPlantSpawner", "desert_plant", "Desert Plant", SpawnerCategory.Plant,
            0.4f, 5, EnvC(ET.Desert),
            O(herbs, 1, 2), O(seeds, 1, 2, w: 0.7f), O(fiber, 0, 2, w: 0.5f, c: 0.5f));

        C("Terrain/TundraGroundSpawner", "tundra_ground", "Tundra Ground Cover", SpawnerCategory.GroundMaterial,
            0.45f, 5, EnvC(ET.Tundra),
            O(mushrooms, 1, 2), O(ferns, 0, 2, w: 0.6f, c: 0.5f), O(fiber, 1, 2, w: 0.5f));

        C("Terrain/CaveMineralSpawner", "cave_mineral", "Cave Mineral Deposit", SpawnerCategory.GroundMaterial,
            0.7f, 3, TileC(ETT.Cave),
            O(stones, 2, 5), O(flint, 1, 3, w: 0.7f), O(clay, 1, 2, w: 0.5f));

        C("Terrain/CaveMushroomSpawner", "cave_mushroom", "Cave Mushroom Patch", SpawnerCategory.Plant,
            0.55f, 4, TileC(ETT.Cave),
            O(mushrooms, 1, 3), O(hoofFungus, 0, 2, w: 0.5f, c: 0.4f));

        C("Terrain/SavannaTreeSpawner", "savanna_tree", "Savanna Tree", SpawnerCategory.Plant,
            0.6f, 4, EnvC(ET.Savanna),
            O(bark, 1, 3), O(treeResin, 0, 2, w: 0.5f, c: 0.4f), O(nuts, 1, 3, w: 0.7f));

        C("Terrain/VolcanicMineralSpawner", "volcanic_mineral", "Volcanic Mineral Deposit", SpawnerCategory.GroundMaterial,
            0.7f, 3, EnvC(ET.Volcano),
            O(stones, 2, 5), O(obsidian, 1, 3, w: 0.8f), O(clay, 0, 2, w: 0.4f, c: 0.5f));

        // ── BURNT REMAINS (temporary, requiresHasBeenIgnited) ──────────────────
        var burntConds = new ResourceSpawnerConditionSettings { requiresHasBeenIgnited = true };

        X("BurntRemains/EmberSpawner", "ember_source", "Ember Source", SpawnerCategory.BurntRemains,
            0.9f, 1, lifetime: 3, burntConds,
            O(embers, 2, 5));

        X("BurntRemains/CharcoalDepositSpawner", "charcoal_deposit", "Charcoal Deposit", SpawnerCategory.BurntRemains,
            0.7f, 2, lifetime: 8, burntConds,
            O(charcoal, 2, 4));

        // Ash resource not yet in project — assign manually when Ash.asset is created
        X("BurntRemains/AshDepositSpawner", "ash_deposit", "Ash Deposit", SpawnerCategory.BurntRemains,
            0.75f, 2, lifetime: 15, burntConds);

        // ── ANIMAL REMAINS (temporary, event-driven via AnimalDeathResourceSpawnerHandler) ──
        X("AnimalRemains/SmallAnimalRemainsSpawner", "small_animal_remains", "Small Animal Remains", SpawnerCategory.AnimalRemains,
            0.85f, 1, lifetime: 4, conds: null,
            O(freshMeat, 1, 2), O(bones, 1, 2, w: 0.8f), O(feathers, 0, 3, w: 0.6f, c: 0.6f));

        X("AnimalRemains/MediumAnimalRemainsSpawner", "medium_animal_remains", "Medium Animal Remains", SpawnerCategory.AnimalRemains,
            0.9f, 1, lifetime: 5, conds: null,
            O(freshMeat, 2, 4), O(bones, 1, 3, w: 0.8f), O(skins, 1, 1, w: 0.7f, c: 0.8f),
            O(sinew, 1, 2, w: 0.6f), O(fat, 1, 2, w: 0.5f));

        X("AnimalRemains/LargeAnimalRemainsSpawner", "large_animal_remains", "Large Animal Remains", SpawnerCategory.AnimalRemains,
            0.95f, 1, lifetime: 7, conds: null,
            O(freshMeat, 3, 8), O(bones, 2, 4, w: 0.9f), O(skins, 1, 2, w: 0.8f, c: 0.9f),
            O(sinew, 2, 4, w: 0.7f), O(fat, 2, 3, w: 0.6f));

        X("AnimalRemains/BirdRemainsSpawner", "bird_remains", "Bird Remains", SpawnerCategory.AnimalRemains,
            0.8f, 1, lifetime: 4, conds: null,
            O(freshMeat, 1, 2), O(bones, 1, 2, w: 0.7f), O(feathers, 2, 5, w: 0.9f));

        // ── WEATHER-CREATED (temporary) ────────────────────────────────────────
        X("Weather/WetSoilMushroomSpawner", "wet_soil_mushrooms", "Wet Soil Mushrooms", SpawnerCategory.WeatherCreated,
            0.7f, 2, lifetime: 6,
            conds: new ResourceSpawnerConditionSettings { requiresIsCurrentlyWet = true },
            O(mushrooms, 1, 4), O(edMush, 1, 3, w: 0.8f), O(waterPlants, 1, 2, w: 0.5f));

        X("Weather/FloodDebrisSpawner", "flood_debris", "Flood Debris", SpawnerCategory.WeatherCreated,
            0.75f, 2, lifetime: 4,
            conds: new ResourceSpawnerConditionSettings { requiresWasRecentlyFlooded = true },
            O(mud, 2, 5), O(sticks, 1, 3, w: 0.6f), O(vines, 1, 2, w: 0.4f, c: 0.5f));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SpawnerCreator] Done — 30 ResourceSpawnerDefinition assets created in " + OutRoot);
    }

    // ── DUNG SPAWNERS ─────────────────────────────────────────────────────────

    [MenuItem("Tools/Kardeshev/Create Dung Spawner Definitions")]
    public static void CreateDungSpawners()
    {
        CreateFolders();

        // Ensure AnimalDroppings sub-folder exists
        string dungFolder = OutRoot + "/AnimalDroppings";
        if (!AssetDatabase.IsValidFolder(dungFolder))
            AssetDatabase.CreateFolder(OutRoot, "AnimalDroppings");

        var dung     = R("Stage0/CaveShelter00/SimpleCarrying/Dung.asset");
        var driedDng = R("Stage0/CaveShelter00/SimpleCarrying/DriedDung.asset");

        // Active dropping spawner — permanently active while an animal is present.
        // AnimalDroppingHandler adds/removes this via OnAnimalEnteredTile / OnAnimalLeftTile.
        // isPermanent=true, canExpire=false — lifecycle managed externally.
        C("AnimalDroppings/AnimalDroppingSpawner",
            "animal_dropping", "Animal Dropping", SpawnerCategory.EnvironmentBackground,
            chance: 0.8f, interval: 2,
            conds: new ResourceSpawnerConditionSettings { requiresHasBeenIgnited = false },
            O(dung, 1, 3));

        // Heavy grazer — larger herds, more dung, shorter interval
        C("AnimalDroppings/HeavyGrazerDroppingSpawner",
            "heavy_grazer_dropping", "Heavy Grazer Dropping", SpawnerCategory.EnvironmentBackground,
            chance: 0.9f, interval: 1,
            conds: null,
            O(dung, 2, 5));

        // Small animal — birds, rodents — light deposits
        C("AnimalDroppings/SmallAnimalDroppingSpawner",
            "small_animal_dropping", "Small Animal Dropping", SpawnerCategory.EnvironmentBackground,
            chance: 0.6f, interval: 3,
            conds: null,
            O(dung, 1, 2));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SpawnerCreator] Done — dung spawner assets created in " + dungFolder);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void CreateFolders()
    {
        var folders = new[]
        {
            OutRoot,
            $"{OutRoot}/Forest",    $"{OutRoot}/Grassland",   $"{OutRoot}/Coastal",
            $"{OutRoot}/Terrain",   $"{OutRoot}/BurntRemains", $"{OutRoot}/AnimalRemains",
            $"{OutRoot}/Weather",   $"{OutRoot}/AnimalDroppings"
        };
        foreach (var folder in folders)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/') ?? "Assets";
                string child  = Path.GetFileName(folder);
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }

    private static ResourceDefinition R(string sub)
        => AssetDatabase.LoadAssetAtPath<ResourceDefinition>($"{ResRoot}/{sub}");

    private static ResourceSpawnerOutput O(ResourceDefinition res, int min, int max,
                                            float w = 1f, float c = 1f)
        => new ResourceSpawnerOutput
           { resource = res, minAmount = min, maxAmount = max, weight = w, chance = c, addToExistingStack = true };

    private static ResourceSpawnerConditionSettings EnvC(params ET[] envTypes)
        => new ResourceSpawnerConditionSettings { requiredEnvironmentTypes = new List<ET>(envTypes) };

    private static ResourceSpawnerConditionSettings TileC(params ETT[] tileTypes)
        => new ResourceSpawnerConditionSettings { requiredTileTypes = new List<ETT>(tileTypes) };

    // Permanent spawner
    private static void C(string sub, string id, string display, SpawnerCategory cat,
                           float chance, int interval,
                           ResourceSpawnerConditionSettings conds,
                           params ResourceSpawnerOutput[] outputs)
    {
        var so = ScriptableObject.CreateInstance<ResourceSpawnerDefinition>();
        so.spawnerID = id; so.displayName = display; so.category = cat;
        so.baseSpawnChance = chance; so.spawnIntervalTurns = interval;
        so.isPermanent = true; so.canExpire = false;
        so.conditions = conds ?? new ResourceSpawnerConditionSettings();
        so.outputs = new List<ResourceSpawnerOutput>(outputs);
        AssetDatabase.CreateAsset(so, $"{OutRoot}/{sub}.asset");
    }

    // Expiring/temporary spawner
    private static void X(string sub, string id, string display, SpawnerCategory cat,
                           float chance, int interval, int lifetime,
                           ResourceSpawnerConditionSettings conds,
                           params ResourceSpawnerOutput[] outputs)
    {
        var so = ScriptableObject.CreateInstance<ResourceSpawnerDefinition>();
        so.spawnerID = id; so.displayName = display; so.category = cat;
        so.baseSpawnChance = chance; so.spawnIntervalTurns = interval;
        so.isPermanent = false; so.canExpire = true;
        so.maxUses = 0; so.lifetimeTurns = lifetime;
        so.conditions = conds ?? new ResourceSpawnerConditionSettings();
        so.outputs = new List<ResourceSpawnerOutput>(outputs);
        AssetDatabase.CreateAsset(so, $"{OutRoot}/{sub}.asset");
    }
}
#endif
