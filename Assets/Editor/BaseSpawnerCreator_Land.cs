#if UNITY_EDITOR
using UnityEngine;
using SC  = SpawnerCategory;
using ET  = EnvironmentType;
using ETT = EnvironmentTileType;

public static partial class BaseSpawnerCreator
{
    internal static void CreateLandSpawners()
    {
        // ── Boreal Forest (5) ─────────────────────────────────────────────────

        A("BorealForest/RS_BorealFallenBranchPile","boreal_fallen_branch_pile","Boreal Fallen Branch Pile",
            SC.Tree,0.6f,4,E(ET.BorealForest),T(ETT.Land),MushClim(),
            O(G("sticks"),2,4),O(G("wood"),1,2,c:0.7f),O(G("bark"),1,1,c:0.4f));

        A("BorealForest/RS_BorealBerryShrub","boreal_berry_shrub","Boreal Berry Shrub",
            SC.Bush,0.5f,5,E(ET.BorealForest),T(ETT.Land),BerryClim(),
            O(G("berries"),1,3),O(G("leaves"),1,2,c:0.5f));

        A("BorealForest/RS_BorealMushroomPatch","boreal_mushroom_patch","Boreal Mushroom Patch",
            SC.Plant,0.65f,3,E(ET.BorealForest),T(ETT.Land),MushClim(),
            O(G("mushrooms"),1,3),O(G("ediblemush"),1,2,c:0.5f));

        A("BorealForest/RS_BorealGroundCover","boreal_ground_cover","Boreal Ground Cover",
            SC.GroundMaterial,0.8f,2,E(ET.BorealForest),T(ETT.Land),null,
            O(G("leaves"),2,4),O(G("fiber"),1,3,c:0.7f),O(G("ferns"),1,2,c:0.5f));

        A("BorealForest/RS_BorealStoneScatter","boreal_stone_scatter","Boreal Stone Scatter",
            SC.StoneDeposit,0.55f,7,E(ET.BorealForest),T(ETT.Land),null,
            O(G("stones"),2,5),O(G("flint"),1,2,c:0.5f));

        // ── Temperate Forest (7) ──────────────────────────────────────────────

        A("TemperateForest/RS_TemperateBerryShrub","temperate_berry_shrub","Temperate Berry Shrub",
            SC.Bush,0.65f,4,E(ET.TemperateForest),T(ETT.Land),BerryClim(),
            O(G("berries"),1,4),O(G("leaves"),1,2,c:0.4f));

        A("TemperateForest/RS_TemperateMushroomPatch","temperate_mushroom_patch","Temperate Mushroom Patch",
            SC.Plant,0.7f,3,E(ET.TemperateForest),T(ETT.Land),MushClim(),
            O(G("mushrooms"),1,4),O(G("ediblemush"),1,3,c:0.6f));

        A("TemperateForest/RS_TemperateFallenBranches","temperate_fallen_branches","Temperate Fallen Branches",
            SC.Tree,0.7f,4,E(ET.TemperateForest),T(ETT.Land),null,
            O(G("sticks"),2,5),O(G("wood"),1,3,c:0.8f),O(G("bark"),1,2,c:0.5f),O(G("leaves"),1,3,c:0.6f));

        A("TemperateForest/RS_TemperateRootPatch","temperate_root_patch","Temperate Root Patch",
            SC.Root,0.6f,4,E(ET.TemperateForest),T(ETT.Land),RootClim(),
            O(G("tubers"),1,3));

        A("TemperateForest/RS_TemperateInsectNest","temperate_insect_nest","Temperate Insect Nest",
            SC.GroundMaterial,0.65f,3,E(ET.TemperateForest),T(ETT.Land),InsectClim(),
            O(G("grubs"),1,3),O(G("grasshops"),1,2,c:0.6f));

        A("TemperateForest/RS_TemperateHerbPatch","temperate_herb_patch","Temperate Herb Patch",
            SC.Plant,0.5f,5,E(ET.TemperateForest),T(ETT.Land),HerbClim(),
            O(G("herbs"),1,3),O(G("medherbs"),0,2,c:0.4f));

        A("TemperateForest/RS_TemperateBirdNestTree","temperate_bird_nest_tree","Temperate Bird Nest Tree",
            SC.Tree,0.4f,6,E(ET.TemperateForest),T(ETT.Land),null,
            O(G("eggs"),1,2,c:0.7f),O(G("feathers"),1,2,c:0.5f));

        // ── Tropical Forest (6) ───────────────────────────────────────────────

        A("TropicalForest/RS_TropicalFruitBush","tropical_fruit_bush","Tropical Fruit Bush",
            SC.Bush,0.75f,3,E(ET.TropicalForest),T(ETT.Land),BerryClim(),
            O(G("fruits"),1,4),O(G("berries"),1,3,c:0.6f));

        A("TropicalForest/RS_TropicalMushroomPatch","tropical_mushroom_patch","Tropical Mushroom Patch",
            SC.Plant,0.8f,2,E(ET.TropicalForest),T(ETT.Land),MushClim(),
            O(G("mushrooms"),2,4),O(G("ediblemush"),1,3,c:0.7f));

        A("TropicalForest/RS_TropicalInsectNest","tropical_insect_nest","Tropical Insect Nest",
            SC.GroundMaterial,0.8f,2,E(ET.TropicalForest),T(ETT.Land),InsectClim(),
            O(G("grubs"),1,4),O(G("antslarvae"),1,3,c:0.7f),O(G("grasshops"),1,2,c:0.5f));

        A("TropicalForest/RS_TropicalDenseGroundCover","tropical_dense_ground_cover","Tropical Dense Ground Cover",
            SC.GroundMaterial,0.9f,2,E(ET.TropicalForest),T(ETT.Land),null,
            O(G("leaves"),2,5),O(G("fiber"),1,4,c:0.8f),O(G("ferns"),1,3,c:0.7f),O(G("vines"),1,2,c:0.5f));

        A("TropicalForest/RS_TropicalFallenBranches","tropical_fallen_branches","Tropical Fallen Branches",
            SC.Tree,0.75f,3,E(ET.TropicalForest),T(ETT.Land),null,
            O(G("sticks"),2,5),O(G("wood"),1,3,c:0.8f),O(G("bark"),1,2,c:0.5f));

        A("TropicalForest/RS_TropicalMedicinalHerbs","tropical_medicinal_herbs","Tropical Medicinal Herbs",
            SC.Plant,0.55f,5,E(ET.TropicalForest),T(ETT.Land),HerbClim(),
            O(G("medherbs"),1,3),O(G("herbs"),1,2,c:0.6f));

        // ── Sub-Tropical (6) ──────────────────────────────────────────────────

        A("SubTropical/RS_SubTropicalBerryShrub","subtropical_berry_shrub","Sub-Tropical Berry Shrub",
            SC.Bush,0.6f,4,E(ET.SubTropical),T(ETT.Land),BerryClim(),
            O(G("berries"),1,3),O(G("fruits"),1,2,c:0.5f));

        A("SubTropical/RS_SubTropicalRootPatch","subtropical_root_patch","Sub-Tropical Root Patch",
            SC.Root,0.6f,4,E(ET.SubTropical),T(ETT.Land),RootClim(),
            O(G("tubers"),1,3));

        A("SubTropical/RS_SubTropicalHerbPatch","subtropical_herb_patch","Sub-Tropical Herb Patch",
            SC.Plant,0.55f,5,E(ET.SubTropical),T(ETT.Land),HerbClim(),
            O(G("herbs"),1,3),O(G("medherbs"),0,2,c:0.35f));

        A("SubTropical/RS_SubTropicalInsectNest","subtropical_insect_nest","Sub-Tropical Insect Nest",
            SC.GroundMaterial,0.7f,3,E(ET.SubTropical),T(ETT.Land),InsectClim(),
            O(G("grubs"),1,3),O(G("grasshops"),1,2,c:0.6f));

        A("SubTropical/RS_SubTropicalFallenBranches","subtropical_fallen_branches","Sub-Tropical Fallen Branches",
            SC.Tree,0.65f,4,E(ET.SubTropical),T(ETT.Land),null,
            O(G("sticks"),2,4),O(G("wood"),1,2,c:0.7f),O(G("bark"),1,2,c:0.5f));

        A("SubTropical/RS_SubTropicalSeedPatch","subtropical_seed_patch","Sub-Tropical Seed Patch",
            SC.Plant,0.65f,3,E(ET.SubTropical),T(ETT.Land),DryGrassClim(),
            O(G("seeds"),2,4));

        // ── Grassland (5) ─────────────────────────────────────────────────────

        A("Grassland/RS_GrasslandDryGrassPatch","grassland_dry_grass_patch","Grassland Dry Grass Patch",
            SC.GroundMaterial,0.9f,2,E(ET.Grassland),T(ETT.Land),DryGrassClim(),
            O(G("driedgrass"),2,5),O(G("seeds"),1,2,c:0.5f));

        A("Grassland/RS_GrasslandSeedPatch","grassland_seed_patch","Grassland Seed Patch",
            SC.Plant,0.7f,3,E(ET.Grassland),T(ETT.Land),DryGrassClim(),
            O(G("seeds"),2,5));

        A("Grassland/RS_GrasslandRootPatch","grassland_root_patch","Grassland Root Patch",
            SC.Root,0.6f,4,E(ET.Grassland),T(ETT.Land),RootClim(),
            O(G("tubers"),1,3));

        A("Grassland/RS_GrasslandInsectNest","grassland_insect_nest","Grassland Insect Nest",
            SC.GroundMaterial,0.7f,3,E(ET.Grassland),T(ETT.Land),InsectClim(),
            O(G("grubs"),1,3),O(G("grasshops"),1,3,c:0.7f));

        A("Grassland/RS_GrasslandStoneScatter","grassland_stone_scatter","Grassland Stone Scatter",
            SC.StoneDeposit,0.55f,7,E(ET.Grassland),T(ETT.Land),null,
            O(G("stones"),2,5),O(G("flint"),1,2,c:0.5f));

        // ── Savanna (7) ───────────────────────────────────────────────────────

        A("Savanna/RS_SavannaDryGrassPatch","savanna_dry_grass_patch","Savanna Dry Grass Patch",
            SC.GroundMaterial,0.9f,2,E(ET.Savanna),T(ETT.Land),DryGrassClim(),
            O(G("driedgrass"),2,5),O(G("seeds"),1,2,c:0.4f));

        A("Savanna/RS_SavannaRootPatch","savanna_root_patch","Savanna Root Patch",
            SC.Root,0.65f,4,E(ET.Savanna),T(ETT.Land),RootClim(),
            O(G("tubers"),1,3));

        A("Savanna/RS_SavannaInsectMound","savanna_insect_mound","Savanna Insect Mound",
            SC.GroundMaterial,0.7f,3,E(ET.Savanna),T(ETT.Land),InsectClim(),
            O(G("grubs"),1,4),O(G("grasshops"),1,3,c:0.8f));

        A("Savanna/RS_SavannaFallenBranches","savanna_fallen_branches","Savanna Fallen Branches",
            SC.Tree,0.6f,6,E(ET.Savanna),T(ETT.Land),DryGrassClim(),
            O(G("sticks"),2,5),O(G("wood"),1,3,c:0.6f),O(G("bark"),1,2,c:0.5f));

        A("Savanna/RS_SavannaStoneScatter","savanna_stone_scatter","Savanna Stone Scatter",
            SC.StoneDeposit,0.7f,6,E(ET.Savanna),T(ETT.Land),null,
            O(G("stones"),2,6),O(G("flint"),1,3,c:0.7f));

        A("Savanna/RS_SavannaBerryShrub","savanna_berry_shrub","Savanna Berry Shrub",
            SC.Bush,0.5f,5,E(ET.Savanna),T(ETT.Land),BerryClim(),
            O(G("berries"),1,4,c:0.7f));

        A("Savanna/RS_SavannaMedicinalHerbs","savanna_medicinal_herbs","Savanna Medicinal Herbs",
            SC.Plant,0.35f,7,E(ET.Savanna),T(ETT.Land),HerbClim(),
            O(G("medherbs"),0,2,c:0.4f));

        // ── Desert (4) ────────────────────────────────────────────────────────

        A("Desert/RS_DesertDryPlant","desert_dry_plant","Desert Dry Plant",
            SC.Plant,0.5f,6,E(ET.Desert),T(ETT.Land),DryGrassClim(),
            O(G("driedgrass"),1,3),O(G("seeds"),1,2,c:0.4f));

        A("Desert/RS_DesertStoneScatter","desert_stone_scatter","Desert Stone Scatter",
            SC.StoneDeposit,0.65f,7,E(ET.Desert),T(ETT.Land),null,
            O(G("stones"),2,6),O(G("flint"),1,3,c:0.6f));

        A("Desert/RS_DesertInsectBurrow","desert_insect_burrow","Desert Insect Burrow",
            SC.GroundMaterial,0.5f,4,E(ET.Desert),T(ETT.Land),InsectClim(),
            O(G("grubs"),1,3),O(G("grasshops"),1,2,c:0.5f));

        A("Desert/RS_DesertRootPatch","desert_root_patch","Desert Root Patch",
            SC.Root,0.4f,6,E(ET.Desert),T(ETT.Land),RootClim(),
            O(G("tubers"),1,2));

        // ── Tundra (4) ────────────────────────────────────────────────────────

        A("Tundra/RS_TundraGroundCover","tundra_ground_cover","Tundra Ground Cover",
            SC.GroundMaterial,0.7f,3,E(ET.Tundra),T(ETT.Land),null,
            O(G("driedgrass"),1,3),O(G("fiber"),1,2,c:0.6f));

        A("Tundra/RS_TundraRootPatch","tundra_root_patch","Tundra Root Patch",
            SC.Root,0.45f,5,E(ET.Tundra),T(ETT.Land),RootClim(),
            O(G("tubers"),1,2));

        A("Tundra/RS_TundraStoneScatter","tundra_stone_scatter","Tundra Stone Scatter",
            SC.StoneDeposit,0.65f,7,E(ET.Tundra),T(ETT.Land),null,
            O(G("stones"),2,5),O(G("flint"),1,2,c:0.5f));

        A("Tundra/RS_TundraLichenPatch","tundra_lichen_patch","Tundra Lichen Patch",
            SC.Plant,0.6f,4,E(ET.Tundra),T(ETT.Land),MushClim(),
            O(G("fiber"),1,3,c:0.7f));  // lichen missing — fiber as cold-region fibre fallback

        A("Tundra/RS_TundraSnowmelt","tundra_snowmelt","Tundra Snowmelt",
            SC.GroundMaterial,0.6f,4,E(ET.Tundra),T(ETT.Land),null,
            O(G("water"),1,2));  // seasonal meltwater pooling — basic water

        // ── Mountain (4) ──────────────────────────────────────────────────────

        A("Mountain/RS_MountainRockScatter","mountain_rock_scatter","Mountain Rock Scatter",
            SC.StoneDeposit,0.7f,5,E(ET.Mountain),T(ETT.Land,ETT.Mountain),null,
            O(G("stones"),2,6),O(G("flint"),1,3,c:0.6f),O(G("clay"),1,2,c:0.4f));

        A("Mountain/RS_MountainHerbPatch","mountain_herb_patch","Mountain Herb Patch",
            SC.Plant,0.45f,6,E(ET.Mountain),T(ETT.Land),HerbClim(),
            O(G("medherbs"),1,2,c:0.6f),O(G("herbs"),1,2,c:0.7f));

        A("Mountain/RS_MountainRootPatch","mountain_root_patch","Mountain Root Patch",
            SC.Root,0.45f,6,E(ET.Mountain),T(ETT.Land),RootClim(),
            O(G("tubers"),1,2));

        A("Mountain/RS_MountainMineralVein","mountain_mineral_vein","Mountain Mineral Vein",
            SC.StoneDeposit,0.5f,8,E(ET.Mountain),T(ETT.Mountain),null,
            O(G("clay"),1,3),O(G("stones"),2,4,c:0.8f));

        A("Mountain/RS_MountainSpringWater","mountain_spring_water","Mountain Spring Water",
            SC.WaterCoastal,0.65f,3,E(ET.Mountain),T(ETT.Land,ETT.Mountain),null,
            O(G("freshwater"),1,2));  // clean mountain spring — fresh water directly

        // ── Volcano (3) ───────────────────────────────────────────────────────

        A("Volcano/RS_VolcanicRockScatter","volcanic_rock_scatter","Volcanic Rock Scatter",
            SC.StoneDeposit,0.6f,6,E(ET.Volcano),T(ETT.Land),null,
            O(G("stones"),2,5),O(G("obsidian"),0,1,c:0.3f));

        A("Volcano/RS_VolcanicMineralDeposit","volcanic_mineral_deposit","Volcanic Mineral Deposit",
            SC.StoneDeposit,0.4f,8,E(ET.Volcano),T(ETT.Land),null,
            O(G("clay"),1,3),O(G("stones"),1,3,c:0.8f));

        A("Volcano/RS_ObsidianScatter","obsidian_scatter","Obsidian Scatter",
            SC.StoneDeposit,0.3f,10,E(ET.Volcano),T(ETT.Land),null,
            O(G("obsidian"),1,2),O(G("flint"),1,2,c:0.5f));
    }
}
#endif
