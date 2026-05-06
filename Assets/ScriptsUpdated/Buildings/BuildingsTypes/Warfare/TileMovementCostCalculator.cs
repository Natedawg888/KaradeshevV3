using UnityEngine;

public static class TileMovementCostCalculator
{
    private const float DISCOVERED_ENV_MULT = 0.5f;
    private const float UNDISCOVERED_ENV_MULT = 1.33f;

    public static float GetTurnCostForStep(TileControl tile, TileUnitGroupData group)
    {
        if (tile == null || group == null || group.unitType == null)
            return 0f;

        var unit = group.unitType;

        float movementPerTurn = Mathf.Max(0.1f, unit.movementSpeed + group.bonusMovementSpeed);

        EnvironmentType envType = EnvironmentType.Grassland;
        EnvironmentTileType tileType = EnvironmentTileType.Land;
        TileSize tileSize = TileSize.Medium;

        // Resolve tile/environment/building data more robustly.
        TileScript tileScript = ResolveTileScript(tile);
        EnvironmentControl envCtrl = ResolveEnvironmentControl(tile);
        EnvironmentStatus envStatus = ResolveEnvironmentStatus(tile, envCtrl);
        BuildingControl buildingCtrl = ResolveBuildingControl(tile);

        if (tileScript != null)
        {
            envType = tileScript.GetChosenEnvironmentType();
            tileType = tileScript.GetChosenTileType();
            tileSize = tileScript.tileSize;
        }

        // If this is a building tile, override size using building definition.
        if (tile.tileContentType == TileContentType.Building && buildingCtrl != null)
        {
            var def = BuildingManager.Instance?.GetBuildingByID(buildingCtrl.buildingID);
            if (def != null)
                tileSize = def.requiredTileSize;
        }

        // Hard restriction: can the unit enter this terrain at all?
        if (!CanUnitEnterTile(unit, envType, tileType))
            return float.PositiveInfinity;

        float basePoints = GetBasePointsForEnvironment(envType);

        basePoints *= GetTileTypeMultiplier(tileType);
        basePoints *= GetTileSizeMultiplier(tileSize);

        if (tile.tileContentType == TileContentType.Building)
            basePoints *= 1.1f;

        basePoints *= GetDiscoveryMultiplier(tile, envStatus);

        basePoints = ApplyUnitTerrainModifiers(unit, envType, tileType, basePoints);

        float turnCost = basePoints / movementPerTurn;

        float minTurnCost = 1f / movementPerTurn;
        return Mathf.Max(minTurnCost, turnCost);
    }

    private static TileScript ResolveTileScript(TileControl tile)
    {
        if (tile == null)
            return null;

        var tileScript = tile.GetComponent<TileScript>();
        if (tileScript != null)
            return tileScript;

        tileScript = tile.GetComponentInChildren<TileScript>(true);
        if (tileScript != null)
            return tileScript;

        return null;
    }

    private static EnvironmentControl ResolveEnvironmentControl(TileControl tile)
    {
        if (tile == null)
            return null;

        if (tile.EnvironmentControl != null)
            return tile.EnvironmentControl;

        var envCtrl = tile.GetComponentInChildren<EnvironmentControl>(true);
        if (envCtrl != null)
            return envCtrl;

        return null;
    }

    private static EnvironmentStatus ResolveEnvironmentStatus(TileControl tile, EnvironmentControl envCtrl)
    {
        if (envCtrl != null)
        {
            var status = envCtrl.GetComponentInChildren<EnvironmentStatus>(true);
            if (status != null)
                return status;
        }

        if (tile != null)
        {
            var status = tile.GetComponentInChildren<EnvironmentStatus>(true);
            if (status != null)
                return status;
        }

        return null;
    }

    private static BuildingControl ResolveBuildingControl(TileControl tile)
    {
        if (tile == null)
            return null;

        var buildingCtrl = tile.GetComponent<BuildingControl>();
        if (buildingCtrl != null)
            return buildingCtrl;

        buildingCtrl = tile.GetComponentInChildren<BuildingControl>(true);
        if (buildingCtrl != null)
            return buildingCtrl;

        buildingCtrl = tile.GetComponentInParent<BuildingControl>(true);
        return buildingCtrl;
    }

    private static float GetBasePointsForEnvironment(EnvironmentType env)
    {
        switch (env)
        {
            case EnvironmentType.Grassland: return 8f;
            case EnvironmentType.Savanna: return 9f;

            case EnvironmentType.TemperateForest: return 11f;
            case EnvironmentType.SubTropical: return 11f;
            case EnvironmentType.TropicalForest: return 12f;
            case EnvironmentType.BorealForest: return 12f;

            case EnvironmentType.Tundra: return 12f;

            case EnvironmentType.Lake: return 16f;
            case EnvironmentType.Ocean: return 20f;

            case EnvironmentType.Mountain: return 16f;

            case EnvironmentType.Desert: return 14f;

            default: return 10f;
        }
    }

    private static float GetTileTypeMultiplier(EnvironmentTileType tileType)
    {
        switch (tileType)
        {
            case EnvironmentTileType.River:
            case EnvironmentTileType.RiverCorner:
            case EnvironmentTileType.RiverSplit:
            case EnvironmentTileType.RiverCross:
            case EnvironmentTileType.RiverEnd:
            case EnvironmentTileType.RiverMouth:
            case EnvironmentTileType.LakeMouth:
                return 1.25f;

            case EnvironmentTileType.Coastline:
            case EnvironmentTileType.CoastlineCorner:
            case EnvironmentTileType.LakeEdge:
            case EnvironmentTileType.LakeCorner:
                return 1.1f;

            case EnvironmentTileType.Mountain:
                return 1.5f;

            case EnvironmentTileType.Cave:
                return 1.3f;

            case EnvironmentTileType.Ocean:
            case EnvironmentTileType.Lake:
            case EnvironmentTileType.Water:
                return 2.0f;

            default:
                return 1.0f;
        }
    }

    private static float GetTileSizeMultiplier(TileSize tileSize)
    {
        switch (tileSize)
        {
            case TileSize.Tiny: return 0.5f;
            case TileSize.Small: return 0.75f;
            case TileSize.Medium: return 1.0f;
            case TileSize.Large: return 1.5f;
            case TileSize.Giant: return 2.0f;
            case TileSize.Massive: return 3.0f;
            default: return 1.0f;
        }
    }

    private static float GetDiscoveryMultiplier(TileControl tile, EnvironmentStatus envStatus)
    {
        if (tile == null)
            return 1f;

        if (tile.tileContentType != TileContentType.Environment)
            return 1f;

        if (envStatus == null)
            return 1f;

        return envStatus.IsDiscovered
            ? DISCOVERED_ENV_MULT
            : UNDISCOVERED_ENV_MULT;
    }

    public static bool CanUnitEnterTile(
        MilitiaUnit unit,
        EnvironmentType envType,
        EnvironmentTileType tileType)
    {
        if (unit == null)
            return false;

        bool envOk = true;
        bool tileOk = true;

        if (unit.restrictByEnvironmentType &&
            unit.allowedEnvironmentTypes != null &&
            unit.allowedEnvironmentTypes.Count > 0)
        {
            envOk = unit.allowedEnvironmentTypes.Contains(envType);
        }

        if (unit.restrictByTileType &&
            unit.allowedTileTypes != null &&
            unit.allowedTileTypes.Count > 0)
        {
            tileOk = unit.allowedTileTypes.Contains(tileType);
        }

        return envOk && tileOk;
    }

    private static float ApplyUnitTerrainModifiers(
        MilitiaUnit unit,
        EnvironmentType envType,
        EnvironmentTileType tileType,
        float basePoints)
    {
        if (unit == null)
            return basePoints;

        if (unit.environmentMoveModifiers != null)
        {
            for (int i = 0; i < unit.environmentMoveModifiers.Count; i++)
            {
                var mod = unit.environmentMoveModifiers[i];
                if (mod == null) continue;

                if (mod.environmentType == envType && mod.costMultiplier > 0f)
                    basePoints *= mod.costMultiplier;
            }
        }

        if (unit.tileTypeMoveModifiers != null)
        {
            for (int i = 0; i < unit.tileTypeMoveModifiers.Count; i++)
            {
                var mod = unit.tileTypeMoveModifiers[i];
                if (mod == null) continue;

                if (mod.tileType == tileType && mod.costMultiplier > 0f)
                    basePoints *= mod.costMultiplier;
            }
        }

        return basePoints;
    }
}