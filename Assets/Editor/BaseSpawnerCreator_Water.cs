#if UNITY_EDITOR
using UnityEngine;
using SC  = SpawnerCategory;
using ET  = EnvironmentType;
using ETT = EnvironmentTileType;

public static partial class BaseSpawnerCreator
{
    internal static void CreateWaterSpawners()
    {
        // ── Beach (5) ─────────────────────────────────────────────────────────

        A("Beach/RS_BeachShellfishBed","beach_shellfish_bed","Beach Shellfish Bed",
            SC.WaterCoastal,0.8f,3,E(ET.Beach),T(ETT.Beach,ETT.BeachEnd,ETT.Coastline),null,
            O(G("shellfish"),1,4));

        A("Beach/RS_BeachSeaweedPatch","beach_seaweed_patch","Beach Seaweed Patch",
            SC.WaterCoastal,0.75f,3,E(ET.Beach),T(ETT.Beach,ETT.BeachEnd,ETT.Coastline),null,
            O(G("seaweed"),1,3));

        A("Beach/RS_BeachDriftwoodPile","beach_driftwood_pile","Beach Driftwood Pile",
            SC.Tree,0.6f,5,E(ET.Beach),T(ETT.Beach,ETT.BeachEnd),null,
            O(G("sticks"),2,4),O(G("wood"),1,2,c:0.6f));

        A("Beach/RS_BeachCoastalGrass","beach_coastal_grass","Beach Coastal Grass",
            SC.GroundMaterial,0.7f,3,E(ET.Beach),T(ETT.Beach,ETT.BeachEnd),null,
            O(G("driedgrass"),1,3),O(G("fiber"),1,2,c:0.6f));

        A("Beach/RS_BeachStoneScatter","beach_stone_scatter","Beach Stone Scatter",
            SC.StoneDeposit,0.55f,7,E(ET.Beach),T(ETT.Beach,ETT.BeachEnd),null,
            O(G("stones"),2,5),O(G("flint"),1,2,c:0.4f));

        A("Beach/RS_BeachTidepoolWater","beach_tidepool_water","Beach Tidepool Water",
            SC.WaterCoastal,0.7f,3,E(ET.Beach),T(ETT.Beach,ETT.BeachEnd,ETT.Coastline),null,
            O(G("contwater"),1,2));  // salt / brackish — needs purification

        // ── Ocean (3) ─────────────────────────────────────────────────────────

        A("Ocean/RS_OceanSeaweedPatch","ocean_seaweed_patch","Ocean Seaweed Patch",
            SC.WaterCoastal,0.7f,3,E(ET.Ocean),T(ETT.Ocean,ETT.Water),null,
            O(G("seaweed"),1,4));

        A("Ocean/RS_ShallowOceanShellfishBed","shallow_ocean_shellfish_bed","Shallow Ocean Shellfish Bed",
            SC.WaterCoastal,0.75f,3,E(ET.Ocean),T(ETT.Coastline,ETT.CoastlineCorner),null,
            O(G("shellfish"),1,4));

        A("Ocean/RS_ShallowOceanSeaweedPatch","shallow_ocean_seaweed_patch","Shallow Ocean Seaweed Patch",
            SC.WaterCoastal,0.7f,3,E(ET.Ocean),T(ETT.Coastline,ETT.CoastlineCorner),null,
            O(G("seaweed"),1,3),O(G("waterplants"),1,2,c:0.5f));

        A("Ocean/RS_OceanSaltWater","ocean_salt_water","Ocean Salt Water",
            SC.WaterCoastal,0.9f,2,E(ET.Ocean),T(ETT.Ocean,ETT.Water,ETT.Coastline,ETT.CoastlineCorner),null,
            O(G("contwater"),1,3));  // undrinkable salt water

        // ── Lake (3) ──────────────────────────────────────────────────────────

        A("Lake/RS_LakeWaterPlants","lake_water_plants","Lake Water Plants",
            SC.WaterCoastal,0.75f,3,E(ET.Lake),T(ETT.Lake,ETT.Water),null,
            O(G("waterplants"),1,4),O(G("seaweed"),1,2,c:0.5f));

        A("Lake/RS_LakeReedPatch","lake_reed_patch","Lake Reed Patch",
            SC.WaterCoastal,0.7f,3,E(ET.Lake),T(ETT.Lake,ETT.LakeMouth),null,
            O(G("fiber"),1,3));  // reeds missing — fiber fallback

        A("Lake/RS_LakeFreshwaterShellfish","lake_freshwater_shellfish","Lake Freshwater Shellfish",
            SC.WaterCoastal,0.65f,4,E(ET.Lake),T(ETT.Lake),null,
            O(G("shellfish"),1,3));

        A("Lake/RS_LakeFreshWaterSource","lake_fresh_water_source","Lake Fresh Water Source",
            SC.WaterCoastal,0.95f,2,E(ET.Lake),T(ETT.Lake,ETT.Water,ETT.LakeMouth),null,
            O(G("freshwater"),1,3));

        // ── Lake Edge (5) ─────────────────────────────────────────────────────

        A("LakeEdge/RS_LakeEdgeReedPatch","lake_edge_reed_patch","Lake Edge Reed Patch",
            SC.WaterCoastal,0.75f,3,E(ET.LakeEdge),T(ETT.LakeEdge,ETT.LakeCorner,ETT.LakeEdgeEnd),null,
            O(G("fiber"),1,4));

        A("LakeEdge/RS_LakeEdgeRootPatch","lake_edge_root_patch","Lake Edge Root Patch",
            SC.Root,0.6f,4,E(ET.LakeEdge),T(ETT.LakeEdge,ETT.LakeCorner),RootClim(),
            O(G("tubers"),1,3));

        A("LakeEdge/RS_LakeEdgeFreshwaterShellfish","lake_edge_freshwater_shellfish","Lake Edge Freshwater Shellfish",
            SC.WaterCoastal,0.65f,4,E(ET.LakeEdge),T(ETT.LakeEdge),null,
            O(G("shellfish"),1,3));

        A("LakeEdge/RS_LakeEdgeDriftwoodPile","lake_edge_driftwood_pile","Lake Edge Driftwood Pile",
            SC.Tree,0.55f,5,E(ET.LakeEdge),T(ETT.LakeEdge,ETT.LakeEdgeEnd),null,
            O(G("sticks"),2,4),O(G("wood"),1,2,c:0.6f));

        A("LakeEdge/RS_LakeEdgeHerbPatch","lake_edge_herb_patch","Lake Edge Herb Patch",
            SC.Plant,0.5f,5,E(ET.LakeEdge),T(ETT.LakeEdge),HerbClim(),
            O(G("herbs"),1,2),O(G("waterplants"),1,2,c:0.6f));

        A("LakeEdge/RS_LakeEdgeWaterGather","lake_edge_water_gather","Lake Edge Water Gather",
            SC.WaterCoastal,0.85f,2,E(ET.LakeEdge),T(ETT.LakeEdge,ETT.LakeCorner,ETT.LakeEdgeEnd),null,
            O(G("water"),1,2));  // shoreline collection — may need purification

        // ── River (4) ─────────────────────────────────────────────────────────
        // Rivers span many EnvironmentTypes — tile-type filter only (null envs = any).

        A("River/RS_RiverReedPatch","river_reed_patch","River Reed Patch",
            SC.WaterCoastal,0.7f,3,null,T(ETT.River,ETT.RiverCorner,ETT.RiverSplit,ETT.RiverCross,ETT.RiverEnd),null,
            O(G("fiber"),1,4));

        A("River/RS_RiverRootPatch","river_root_patch","River Root Patch",
            SC.Root,0.6f,4,null,T(ETT.River,ETT.RiverCorner),RootClim(),
            O(G("tubers"),1,3),O(G("mud"),1,2,c:0.5f));

        A("River/RS_RiverDriftwoodPile","river_driftwood_pile","River Driftwood Pile",
            SC.Tree,0.6f,5,null,T(ETT.River,ETT.RiverMouth,ETT.RiverSplit),null,
            O(G("sticks"),2,5),O(G("wood"),1,3,c:0.7f));

        A("River/RS_RiverFreshwaterShellfish","river_freshwater_shellfish","River Freshwater Shellfish",
            SC.WaterCoastal,0.65f,4,null,T(ETT.River),null,
            O(G("shellfish"),1,3));

        A("River/RS_RiverFreshWaterSource","river_fresh_water_source","River Fresh Water Source",
            SC.WaterCoastal,0.95f,2,null,T(ETT.River,ETT.RiverCorner,ETT.RiverSplit,ETT.RiverCross,ETT.RiverEnd),null,
            O(G("freshwater"),1,3));  // flowing river — clean fresh water

        // ── River Mouth (4) — stored in River/ folder ─────────────────────────

        A("River/RS_RiverMouthShellfishBed","river_mouth_shellfish_bed","River Mouth Shellfish Bed",
            SC.WaterCoastal,0.8f,3,null,T(ETT.RiverMouth,ETT.LakeMouth),null,
            O(G("shellfish"),1,4));

        A("River/RS_RiverMouthSeaweedPatch","river_mouth_seaweed_patch","River Mouth Seaweed Patch",
            SC.WaterCoastal,0.7f,3,null,T(ETT.RiverMouth),null,
            O(G("seaweed"),1,3),O(G("waterplants"),1,2,c:0.5f));

        A("River/RS_RiverMouthDriftwoodPile","river_mouth_driftwood_pile","River Mouth Driftwood Pile",
            SC.Tree,0.7f,4,null,T(ETT.RiverMouth),null,
            O(G("sticks"),2,4),O(G("wood"),1,3,c:0.7f));

        A("River/RS_RiverMouthReedPatch","river_mouth_reed_patch","River Mouth Reed Patch",
            SC.WaterCoastal,0.7f,3,null,T(ETT.RiverMouth),null,
            O(G("fiber"),1,4));

        // ── Salt Lake (3) ─────────────────────────────────────────────────────

        A("SaltLake/RS_SaltLakeSaltCrust","salt_lake_salt_crust","Salt Lake Salt Crust",
            SC.GroundMaterial,0.8f,5,E(ET.SaltLake),T(ETT.SaltLake),null,
            O(G("salt"),1,4));

        A("SaltLake/RS_SaltLakeStoneScatter","salt_lake_stone_scatter","Salt Lake Stone Scatter",
            SC.StoneDeposit,0.55f,7,E(ET.SaltLake),T(ETT.SaltLake),null,
            O(G("stones"),2,4),O(G("flint"),0,2,c:0.4f));

        A("SaltLake/RS_SaltLakeBrineMinerals","salt_lake_brine_minerals","Salt Lake Brine Minerals",
            SC.GroundMaterial,0.55f,7,E(ET.SaltLake),T(ETT.SaltLake),null,
            O(G("salt"),1,3),O(G("clay"),1,2,c:0.5f));

        A("SaltLake/RS_SaltLakeBrineWater","salt_lake_brine_water","Salt Lake Brine Water",
            SC.WaterCoastal,0.9f,3,E(ET.SaltLake),T(ETT.SaltLake),null,
            O(G("contwater"),1,3));  // highly saline — undrinkable

        // ── Cave (5) ──────────────────────────────────────────────────────────
        // Caves occur across environment types — tile-type filter only.

        A("Cave/RS_CaveStoneScatter","cave_stone_scatter","Cave Stone Scatter",
            SC.StoneDeposit,0.7f,5,null,T(ETT.Cave),null,
            O(G("stones"),2,6),O(G("flint"),1,3,c:0.6f));

        A("Cave/RS_CaveMineralVein","cave_mineral_vein","Cave Mineral Vein",
            SC.StoneDeposit,0.5f,8,null,T(ETT.Cave),null,
            O(G("clay"),1,4),O(G("stones"),1,3,c:0.7f));

        A("Cave/RS_CaveMushroomPatch","cave_mushroom_patch","Cave Mushroom Patch",
            SC.Plant,0.6f,4,null,T(ETT.Cave),MushClim(),
            O(G("mushrooms"),1,4),O(G("ediblemush"),1,2,c:0.5f));

        A("Cave/RS_CaveRootPatch","cave_root_patch","Cave Root Patch",
            SC.Root,0.5f,5,null,T(ETT.Cave),RootClim(),
            O(G("tubers"),1,2),O(G("fiber"),1,2,c:0.5f));

        A("Cave/RS_CaveBatNest","cave_bat_nest","Cave Bat Nest",
            SC.AnimalRemains,0.4f,6,null,T(ETT.Cave),null,
            O(G("bones"),1,2,c:0.6f),O(G("feathers"),1,3,c:0.5f));

        A("Cave/RS_CaveDripWater","cave_drip_water","Cave Drip Water",
            SC.WaterCoastal,0.75f,3,null,T(ETT.Cave),null,
            O(G("water"),1,2));  // stalactite drips / cave springs — unboiled
    }
}
#endif
