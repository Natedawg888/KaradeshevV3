#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// Usage: Tools → Kardeshev → Create Event Spawner Definitions
// Creates/updates 13 temporary/event-driven ResourceSpawnerDefinition SOs
// under Assets/ScriptableObjects/ResourceSpawners/Event/
//
// These are NOT base spawners — do NOT assign to prefab baseSpawners lists.
//   BurntRemains    → added at runtime by TileStateResourceSpawnerHandler
//   AnimalRemains   → added at runtime by AnimalDeathResourceSpawnerHandler
//   AnimalDroppings → added at runtime by AnimalDroppingHandler
//   Weather         → added at runtime by TileStateDisasterWirer / wet-state systems
public static class EventSpawnerCreator
{
    private const string Root    = "Assets/ScriptableObjects/ResourceSpawners/Event";
    private const string ResRoot = "Assets/Resources/ResourceDefinition";

    private static readonly Dictionary<string, ResourceDefinition> _res = new();
    private static readonly List<string> _found   = new();
    private static readonly List<string> _missing = new();
    private static int _created, _updated;

    [MenuItem("Tools/Kardeshev/Create Event Spawner Definitions")]
    public static void CreateAll()
    {
        _found.Clear(); _missing.Clear(); _res.Clear();
        _created = 0; _updated = 0;
        EnsureFolders();
        LoadResources();
        CreateBurntRemains();
        CreateWeather();
        CreateAnimalRemains();
        CreateAnimalDroppings();
        CreateWaterEvents();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        PrintReport();
    }

    // ── Resource loading ─────────────────────────────────────────────────────

    private static void LoadResources()
    {
        void L(string k, string path, string label)
        {
            var r = AssetDatabase.LoadAssetAtPath<ResourceDefinition>($"{ResRoot}/{path}");
            _res[k] = r;
            if (r != null) _found.Add(label);
            else           _missing.Add(label);
        }

        // Present
        L("embers",    "Stage0/CaveShelter00/SimpleCarrying/Embers.asset",                    "Embers");
        L("charcoal",  "Stage0/CaveShelter01/CharcoalMaking/Charcoal.asset",                  "Charcoal");
        L("sticks",    "Stage0/CaveShelter00/SimpleCarrying/Sticks.asset",                    "Sticks");
        L("fiber",     "Stage0/CaveShelter00/SimpleCarrying/Fiber.asset",                     "Fiber");
        L("mushrooms", "Stage0/CaveShelter00/ResourceAwarenessI/Mushrooms.asset",             "Mushrooms");
        L("bones",     "Stage0/CaveShelter02/Butchery&ProcessingI/Bones.asset",               "Bones");
        L("feathers",  "Stage0/CaveShelter02/Butchery&ProcessingI/Feathers.asset",            "Feathers");
        L("hide",      "Stage0/CaveShelter02/Butchery&ProcessingI/AnimalSkins.asset",         "AnimalSkins (Hide)");
        L("scavmeat",  "Stage0/StartingResources/Scavenged_Meat.asset",                       "Scavenged_Meat");
        L("dung",       "Stage0/CaveShelter00/SimpleCarrying/Dung.asset",                     "Dung");
        L("water",      "Stage0/StartingResources/Water.asset",                                "Water");
        L("freshwater", "Folders/FoodO/Water/FreshWater.asset",                               "FreshWater");
        L("contwater",  "Folders/FoodO/Water/Contaminated_Water.asset",                       "Contaminated Water");

        // Missing — outputs that use these are skipped, noted in report
        L("ash",         "MISSING/Ash.asset",            "Ash [MISSING]");
        L("burntwood",   "MISSING/BurntWood.asset",      "Burnt Wood [MISSING]");
        L("volcanicash", "MISSING/VolcanicAsh.asset",    "Volcanic Ash [MISSING]");
        L("hidescrap",   "MISSING/HideScraps.asset",     "Hide Scraps [MISSING — AnimalSkins used]");
        L("driftwood",   "MISSING/Driftwood.asset",      "Driftwood [MISSING — Sticks used]");
    }

    // ── Compact helpers ──────────────────────────────────────────────────────

    private static ResourceDefinition G(string k)
        => _res.TryGetValue(k, out var r) ? r : null;

    private static ResourceDefinition GF(string primary, string fallback)
        => G(primary) ?? G(fallback);

    private static ResourceSpawnerOutput O(ResourceDefinition r, int mn, int mx,
                                            float w = 1f, float c = 1f)
        => r == null ? null
         : new ResourceSpawnerOutput
           { resource = r, minAmount = mn, maxAmount = mx, weight = w, chance = c, addToExistingStack = true };

    private static ResourceSpawnerConditionSettings Cond(
        bool ignited = false, bool wet = false,
        bool flooded = false, bool carcass = false, bool volash = false)
        => new ResourceSpawnerConditionSettings
        {
            requiresHasBeenIgnited     = ignited,
            requiresIsCurrentlyWet     = wet,
            requiresWasRecentlyFlooded = flooded,
            requiresHasCarcass         = carcass,
            requiresHasVolcanicAsh     = volash
        };

    private static ResourceSpawnerClimateSettings WetMushroomClim()
        => new ResourceSpawnerClimateSettings
        {
            enabled          = true,
            temperatureCurve = new AnimationCurve(
                new Keyframe(-10f, 1.2f), new Keyframe(20f, 1f),
                new Keyframe(35f,  0.6f), new Keyframe(50f, 0.2f)),
            humidityCurve    = new AnimationCurve(
                new Keyframe(0f, 0.1f), new Keyframe(0.4f, 0.8f),
                new Keyframe(0.65f, 1f), new Keyframe(1f,  1.5f))
        };

    private static void A(string sub, string id, string dn, SpawnerCategory cat,
        float ch, int iv, bool canExpire, int lifetime,
        ResourceSpawnerConditionSettings cond,
        ResourceSpawnerClimateSettings cl,
        params ResourceSpawnerOutput[] outs)
    {
        string path = Root + "/" + sub + ".asset";
        var so = AssetDatabase.LoadAssetAtPath<ResourceSpawnerDefinition>(path);
        bool isNew = so == null;
        if (isNew) so = ScriptableObject.CreateInstance<ResourceSpawnerDefinition>();
        so.spawnerID          = id;
        so.displayName        = dn;
        so.category           = cat;
        so.baseSpawnChance    = ch;
        so.spawnIntervalTurns = iv;
        so.isPermanent        = false;
        so.canExpire          = canExpire;
        so.lifetimeTurns      = lifetime;
        so.debugNotes         = $"Event spawner — added at runtime by handler. NOT a base spawner.";
        so.conditions         = cond ?? new ResourceSpawnerConditionSettings();
        so.climate            = cl   ?? new ResourceSpawnerClimateSettings();
        so.outputs            = new List<ResourceSpawnerOutput>();
        foreach (var o in outs) if (o != null) so.outputs.Add(o);
        if (isNew) { AssetDatabase.CreateAsset(so, path); _created++; }
        else       { EditorUtility.SetDirty(so);           _updated++; }
    }

    // ── Burnt / Ignited ──────────────────────────────────────────────────────

    private static void CreateBurntRemains()
    {
        // Lifetime 3t — handler removes when fire is fully extinguished
        A("BurntRemains/RS_EmberSource","event_ember_source","Ember Source",
            SpawnerCategory.BurntRemains,0.9f,1,true,3,Cond(ignited:true),null,
            O(G("embers"),1,2));

        // Lifetime 8t — added on extinguish by TileStateResourceSpawnerHandler
        A("BurntRemains/RS_CharcoalDeposit","event_charcoal_deposit","Charcoal Deposit",
            SpawnerCategory.BurntRemains,0.7f,2,true,8,Cond(ignited:true),null,
            O(G("charcoal"),1,2),
            O(G("burntwood"),1,1,c:0.45f));

        // Lifetime 15t — slower-forming ash layer
        A("BurntRemains/RS_AshDeposit","event_ash_deposit","Ash Deposit",
            SpawnerCategory.BurntRemains,0.85f,2,true,15,Cond(ignited:true),null,
            O(G("ash"),1,2));
    }

    // ── Weather / state-driven ────────────────────────────────────────────────

    private static void CreateWeather()
    {
        // Lifetime 6t — added by wet-state system; only fires while tile is still wet
        A("Weather/RS_WetSoilMushroomPatch","event_wet_soil_mushroom","Wet Soil Mushroom Patch",
            SpawnerCategory.WeatherCreated,0.55f,3,true,6,Cond(wet:true),WetMushroomClim(),
            O(G("mushrooms"),1,2));

        // Lifetime 4t — short-lived flood salvage
        A("Weather/RS_FloodDebrisPile","event_flood_debris_pile","Flood Debris Pile",
            SpawnerCategory.WeatherCreated,0.7f,2,true,4,Cond(flooded:true),null,
            O(GF("driftwood","sticks"),1,2,c:0.8f),
            O(G("fiber"),1,2,c:0.6f),
            O(G("sticks"),1,1,c:0.5f));

        // Lifetime 10t — volcanic ashfall
        A("Weather/RS_VolcanicAshLayer","event_volcanic_ash_layer","Volcanic Ash Layer",
            SpawnerCategory.WeatherCreated,0.8f,2,true,10,Cond(volash:true),null,
            O(G("ash"),1,2),
            O(G("volcanicash"),1,1,c:0.5f));
    }

    // ── Animal remains ────────────────────────────────────────────────────────

    private static void CreateAnimalRemains()
    {
        var hide = GF("hidescrap","hide");  // HideScraps missing → AnimalSkins

        A("AnimalRemains/RS_SmallAnimalRemains","event_small_animal_remains","Small Animal Remains",
            SpawnerCategory.AnimalRemains,0.95f,1,true,4,Cond(carcass:true),null,
            O(G("scavmeat"),1,2),
            O(G("bones"),1,1,c:0.6f),
            O(hide,1,1,c:0.35f));

        A("AnimalRemains/RS_MediumAnimalRemains","event_medium_animal_remains","Medium Animal Remains",
            SpawnerCategory.AnimalRemains,0.95f,1,true,5,Cond(carcass:true),null,
            O(G("scavmeat"),1,3),
            O(G("bones"),1,2,c:0.75f),
            O(hide,1,1,c:0.45f));

        A("AnimalRemains/RS_LargeAnimalRemains","event_large_animal_remains","Large Animal Remains",
            SpawnerCategory.AnimalRemains,0.95f,1,true,7,Cond(carcass:true),null,
            O(G("scavmeat"),2,4),
            O(G("bones"),1,3,c:0.8f),
            O(hide,1,2,c:0.55f));

        A("AnimalRemains/RS_BirdRemains","event_bird_remains","Bird Remains",
            SpawnerCategory.AnimalRemains,0.95f,1,true,4,Cond(carcass:true),null,
            O(G("scavmeat"),1,1),
            O(G("feathers"),1,2,c:0.75f),
            O(G("bones"),1,1,c:0.35f));
    }

    // ── Water events ─────────────────────────────────────────────────────────

    private static void CreateWaterEvents()
    {
        // Flood leaves standing contaminated water — short-lived, drains quickly
        A("Water/RS_FloodStagnantWater","event_flood_stagnant_water","Flood Stagnant Water",
            SpawnerCategory.WeatherCreated,0.8f,2,true,5,Cond(flooded:true),null,
            O(G("contwater"),1,2));

        // Rain on any tile — short window for rain collection (basic water, needs boiling)
        A("Water/RS_RainCollectedWater","event_rain_collected_water","Rain Collected Water",
            SpawnerCategory.WeatherCreated,0.65f,2,true,4,Cond(wet:true),null,
            O(G("water"),1,2));

        // Volcanic ashfall contaminates nearby water sources
        A("Water/RS_VolcanicContaminatedWater","event_volcanic_contaminated_water","Volcanic Contaminated Water",
            SpawnerCategory.WeatherCreated,0.75f,3,true,8,Cond(volash:true),null,
            O(G("contwater"),1,2));
    }

    // ── Animal droppings ──────────────────────────────────────────────────────
    // canExpire = false — AnimalDroppingHandler controls add/remove, not lifetime

    private static void CreateAnimalDroppings()
    {
        A("AnimalDroppings/RS_AnimalDropping","event_animal_dropping","Animal Dropping",
            SpawnerCategory.EnvironmentBackground,0.85f,2,false,0,null,null,
            O(G("dung"),1,1));

        A("AnimalDroppings/RS_HeavyGrazerDropping","event_heavy_grazer_dropping","Heavy Grazer Dropping",
            SpawnerCategory.EnvironmentBackground,1.0f,2,false,0,null,null,
            O(G("dung"),1,2));

        A("AnimalDroppings/RS_SmallAnimalDropping","event_small_animal_dropping","Small Animal Dropping",
            SpawnerCategory.EnvironmentBackground,0.65f,3,false,0,null,null,
            O(G("dung"),1,1));
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
        foreach (var s in new[]{ "BurntRemains","Weather","AnimalRemains","AnimalDroppings","Water" })
            Mk(Root + "/" + s);
    }

    // ── Report ────────────────────────────────────────────────────────────────

    private static void PrintReport()
    {
        var sb = new StringBuilder("=== Event Spawner Creation Report ===\n");
        sb.AppendLine($"Created: {_created}   Updated: {_updated}");
        sb.AppendLine("\nResources FOUND:");
        foreach (var r in _found)   sb.AppendLine("  + " + r);
        sb.AppendLine("\nResources MISSING / outputs skipped:");
        foreach (var r in _missing) sb.AppendLine("  - " + r);
        sb.AppendLine(
            "\n[Runtime wiring]" +
            "\n  BurntRemains/RS_EmberSource, RS_CharcoalDeposit, RS_AshDeposit" +
            "\n    → assign to TileStateResourceSpawnerHandler.emberSpawner / charcoalSpawner / ashSpawner" +
            "\n  AnimalRemains/RS_*AnimalRemains, RS_BirdRemains" +
            "\n    → assign to AnimalDeathResourceSpawnerHandler.speciesSpawners or defaultRemainsSpawner" +
            "\n  AnimalDroppings/RS_*Dropping" +
            "\n    → assign to AnimalDroppingHandler.dungDropSpawner" +
            "\n  Weather/RS_FloodDebrisPile" +
            "\n    → assign to TileStateDisasterWirer.floodSpawner" +
            "\n  Weather/RS_VolcanicAshLayer" +
            "\n    → assign to TileStateDisasterWirer.ashSpawner" +
            "\n  Weather/RS_WetSoilMushroomPatch" +
            "\n    → needs a wet-state handler (not yet implemented)" +
            "\n  Water/RS_FloodStagnantWater" +
            "\n    → added by TileStateDisasterWirer on flood (alongside FloodDebrisPile)" +
            "\n  Water/RS_RainCollectedWater" +
            "\n    → added by rain/wet-state handler (not yet implemented)" +
            "\n  Water/RS_VolcanicContaminatedWater" +
            "\n    → added by TileStateDisasterWirer on lava/ash activation");
        Debug.Log(sb.ToString());
    }
}
#endif
