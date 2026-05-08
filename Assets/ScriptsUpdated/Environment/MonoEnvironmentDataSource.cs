using System;
using System.Collections.Generic;
using UnityEngine;
using System.Text;

public class MonoEnvironmentDataSource : MonoBehaviour, IEnvironmentDataSource
{
    public static MonoEnvironmentDataSource Instance { get; private set; }

    public event Action<TileCoord, EnvironmentControl> OnEnvironmentRegisteredOrUpdated;
    public event Action<TileCoord, EnvironmentControl> OnEnvironmentUnregistered;

    private readonly Dictionary<TileCoord, EnvironmentControl> _tilesByCoord =
        new Dictionary<TileCoord, EnvironmentControl>();

    private readonly Dictionary<EnvironmentControl, TileCoord> _primaryCoordByEnv =
        new Dictionary<EnvironmentControl, TileCoord>();

    private readonly Dictionary<EnvironmentControl, List<TileCoord>> _footprintCoordsByEnv =
        new Dictionary<EnvironmentControl, List<TileCoord>>();

    [Header("Debug")]
    public bool debugTileRegistration = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _tilesByCoord.Clear();

        if (debugTileRegistration)
            //Debug.Log("[MonoEnvironmentDataSource] Awake");
    }

    public void RebuildFromLiveTiles()
    {
        _tilesByCoord.Clear();
        _primaryCoordByEnv.Clear();
        _footprintCoordsByEnv.Clear();

        TileControl[] liveTiles = FindObjectsOfType<TileControl>(true);
        int registered = 0;

        for (int i = 0; i < liveTiles.Length; i++)
        {
            TileControl tile = liveTiles[i];
            if (tile == null)
                continue;

            EnvironmentControl env = tile.EnvironmentControl;
            if (env == null)
                continue;

            Vector2Int pos = tile.GetGridPosition();
            RegisterOrUpdate(new TileCoord(pos.x, pos.y), env);
            registered++;
        }

        if (debugTileRegistration)
            //Debug.Log($"[MonoEnvironmentDataSource] RebuildFromLiveTiles complete. Registered={registered}");
    }

    public void RegisterOrUpdate(TileCoord coord, EnvironmentControl env)
    {
        if (env == null) return;

        UnregisterAllCoordsForEnv(env, raiseEvents: false);

        _primaryCoordByEnv[env] = coord;

        List<TileCoord> footprint = BuildFootprintCoords(coord, env);
        if (footprint.Count == 0)
            footprint.Add(coord);

        _footprintCoordsByEnv[env] = footprint;

        for (int i = 0; i < footprint.Count; i++)
            _tilesByCoord[footprint[i]] = env;

        OnEnvironmentRegisteredOrUpdated?.Invoke(coord, env);

        if (debugTileRegistration)
        {
            //Debug.Log(
                //$"[ENV REGISTER] env={env.name} primary={coord} footprintCount={footprint.Count} " +
                //$"envType={env.environmentType} tileType={env.environmentTileType}");
        }
    }

    public void Unregister(TileCoord coord, EnvironmentControl env)
    {
        UnregisterAllCoordsForEnv(env, raiseEvents: true);
    }

    private void UnregisterAllCoordsForEnv(EnvironmentControl env, bool raiseEvents)
    {
        if (env == null)
            return;

        TileCoord primaryCoord = default;
        bool hadPrimary = _primaryCoordByEnv.TryGetValue(env, out primaryCoord);

        if (_footprintCoordsByEnv.TryGetValue(env, out var coords))
        {
            for (int i = 0; i < coords.Count; i++)
            {
                TileCoord coord = coords[i];

                if (_tilesByCoord.TryGetValue(coord, out var existing) && existing == env)
                    _tilesByCoord.Remove(coord);
            }

            _footprintCoordsByEnv.Remove(env);
        }

        _primaryCoordByEnv.Remove(env);

        if (raiseEvents)
            OnEnvironmentUnregistered?.Invoke(hadPrimary ? primaryCoord : default, env);
    }

    private List<TileCoord> BuildFootprintCoords(TileCoord primaryCoord, EnvironmentControl env)
    {
        var results = new List<TileCoord>(4);

        GridManager grid = GridManager.Instance;
        if (grid == null || env == null)
        {
            if (debugTileRegistration)
                //Debug.LogWarning($"[ENV FOOTPRINT FALLBACK] env={(env != null ? env.name : "null")} used primary only because grid/env was missing. primary={primaryCoord}");

            results.Add(primaryCoord);
            return results;
        }

        Transform root = env.transform;

        if (!TryGetWorldBounds(root, out Bounds bounds))
        {
            if (debugTileRegistration)
                //Debug.LogWarning($"[ENV FOOTPRINT FALLBACK] env={env.name} used primary only because no collider/renderer bounds were found. primary={primaryCoord}");

            results.Add(primaryCoord);
            return results;
        }

        float epsilon = grid.cellSize * 0.10f;

        Vector2Int min = grid.GetGridPosition(new Vector3(bounds.min.x + epsilon, 0f, bounds.min.z + epsilon));
        Vector2Int max = grid.GetGridPosition(new Vector3(bounds.max.x - epsilon, 0f, bounds.max.z - epsilon));

        if (debugTileRegistration)
        {
            //Debug.Log(
                //$"[ENV FOOTPRINT RESULT] env={env.name} primary={primaryCoord} " +
                //$"boundsSize={bounds.size} min={min} max={max} " +
                //$"cellWidth={(max.x - min.x + 1)} cellHeight={(max.y - min.y + 1)}");
        }

        for (int x = min.x; x <= max.x; x++)
        {
            for (int y = min.y; y <= max.y; y++)
            {
                if (x < 0 || x >= grid.columns || y < 0 || y >= grid.rows)
                    continue;

                var coord = new TileCoord(x, y);

                if (!results.Contains(coord))
                    results.Add(coord);
            }
        }

        if (results.Count == 0)
            results.Add(primaryCoord);

        return results;
    }

    private bool TryGetWorldBounds(Transform root, out Bounds bounds)
    {
        if (root == null)
        {
            bounds = default;
            return false;
        }

        var ownCollider = root.GetComponent<Collider>();
        if (ownCollider != null)
        {
            bounds = ownCollider.bounds;
            return true;
        }

        var colliders = root.GetComponentsInChildren<Collider>(true);
        if (colliders != null && colliders.Length > 0)
        {
            bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
                bounds.Encapsulate(colliders[i].bounds);
            return true;
        }

        var ownRenderer = root.GetComponent<Renderer>();
        if (ownRenderer != null)
        {
            bounds = ownRenderer.bounds;
            return true;
        }

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers != null && renderers.Length > 0)
        {
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        bounds = default;
        return false;
    }

    public void RegisterTile(TileControl tile)
    {
        if (tile == null) return;

        var env = tile.EnvironmentControl;
        if (env == null) return;

        Vector2Int gridPos = tile.GetGridPosition();
        var coord = new TileCoord(gridPos.x, gridPos.y);
        RegisterOrUpdate(coord, env);
    }

    public void UnregisterTile(TileControl tile)
    {
        if (tile == null) return;

        var env = tile.EnvironmentControl;
        if (env == null) return;

        Vector2Int gridPos = tile.GetGridPosition();
        var coord = new TileCoord(gridPos.x, gridPos.y);
        Unregister(coord, env);
    }

    public IEnumerable<KeyValuePair<TileCoord, EnvironmentControl>> AllTiles
    {
        get
        {
            foreach (var kvp in _primaryCoordByEnv)
            {
                if (kvp.Key != null)
                    yield return new KeyValuePair<TileCoord, EnvironmentControl>(kvp.Value, kvp.Key);
            }
        }
    }

    public void GetNeighbourTilesNonAlloc(TileCoord center, int maxDistance, List<TileCoord> results, bool includeCenter = false)
    {
        if (results == null)
            return;

        results.Clear();

        if (maxDistance < 0)
            return;

        EnvironmentControl centerEnv = null;
        _tilesByCoord.TryGetValue(center, out centerEnv);

        if (includeCenter)
        {
            if (centerEnv != null && _primaryCoordByEnv.TryGetValue(centerEnv, out var centerPrimary))
            {
                if (!results.Contains(centerPrimary))
                    results.Add(centerPrimary);
            }
            else
            {
                results.Add(center);
            }
        }

        if (centerEnv != null &&
            _footprintCoordsByEnv.TryGetValue(centerEnv, out var footprint) &&
            footprint != null &&
            footprint.Count > 0)
        {
            for (int i = 0; i < footprint.Count; i++)
            {
                AddNeighbourPrimariesAroundSample(
                    footprint[i],
                    center,
                    centerEnv,
                    maxDistance,
                    results,
                    includeCenter);
            }

            return;
        }

        AddNeighbourPrimariesAroundSample(
            center,
            center,
            centerEnv,
            maxDistance,
            results,
            includeCenter);
    }

    private void AddNeighbourPrimariesAroundSample(
        TileCoord scanOrigin,
        TileCoord requestedCenter,
        EnvironmentControl centerEnv,
        int maxDistance,
        List<TileCoord> results,
        bool includeCenter)
    {
        for (int dx = -maxDistance; dx <= maxDistance; dx++)
        {
            for (int dy = -maxDistance; dy <= maxDistance; dy++)
            {
                int manhattan = Mathf.Abs(dx) + Mathf.Abs(dy);

                if (manhattan == 0 && !includeCenter)
                    continue;

                if (manhattan > maxDistance)
                    continue;

                var sampleCoord = new TileCoord(scanOrigin.x + dx, scanOrigin.y + dy);

                if (!_tilesByCoord.TryGetValue(sampleCoord, out var sampleEnv) || sampleEnv == null)
                    continue;

                if (!includeCenter && centerEnv != null && sampleEnv == centerEnv)
                    continue;

                if (!includeCenter && centerEnv == null && sampleCoord.Equals(requestedCenter))
                    continue;

                TileCoord resultCoord = sampleCoord;

                if (_primaryCoordByEnv.TryGetValue(sampleEnv, out var primaryCoord))
                    resultCoord = primaryCoord;

                if (!results.Contains(resultCoord))
                    results.Add(resultCoord);
            }
        }
    }

    public IEnumerable<TileCoord> GetNeighbourTiles(TileCoord center, int maxDistance)
    {
        if (maxDistance <= 0)
            yield break;

        var yielded = new HashSet<TileCoord>();

        EnvironmentControl centerEnv = null;
        _tilesByCoord.TryGetValue(center, out centerEnv);

        if (centerEnv != null &&
            _footprintCoordsByEnv.TryGetValue(centerEnv, out var footprint) &&
            footprint != null &&
            footprint.Count > 0)
        {
            for (int i = 0; i < footprint.Count; i++)
            {
                foreach (var coord in EnumerateNeighbourPrimariesAroundSample(footprint[i], center, centerEnv, maxDistance))
                {
                    if (yielded.Add(coord))
                        yield return coord;
                }
            }

            yield break;
        }

        foreach (var coord in EnumerateNeighbourPrimariesAroundSample(center, center, centerEnv, maxDistance))
        {
            if (yielded.Add(coord))
                yield return coord;
        }
    }

    private IEnumerable<TileCoord> EnumerateNeighbourPrimariesAroundSample(
        TileCoord scanOrigin,
        TileCoord requestedCenter,
        EnvironmentControl centerEnv,
        int maxDistance)
    {
        for (int dx = -maxDistance; dx <= maxDistance; dx++)
        {
            for (int dy = -maxDistance; dy <= maxDistance; dy++)
            {
                int manhattan = Mathf.Abs(dx) + Mathf.Abs(dy);

                if (manhattan == 0 || manhattan > maxDistance)
                    continue;

                var sampleCoord = new TileCoord(scanOrigin.x + dx, scanOrigin.y + dy);

                if (!_tilesByCoord.TryGetValue(sampleCoord, out var sampleEnv) || sampleEnv == null)
                    continue;

                if (centerEnv != null && sampleEnv == centerEnv)
                    continue;

                if (centerEnv == null && sampleCoord.Equals(requestedCenter))
                    continue;

                if (_primaryCoordByEnv.TryGetValue(sampleEnv, out var primaryCoord))
                    yield return primaryCoord;
                else
                    yield return sampleCoord;
            }
        }
    }

    public ResourceConsumptionResult ConsumeResourcesForAnimalGroup(
    TileCoord coord,
    AnimalDefinition species,
    int groupSize,
    float maxHungerToSatisfy,
    float maxThirstToSatisfy)
    {
        var result = new ResourceConsumptionResult();
        if (species == null) return result;

        var edibleArray = species.edibleResources;
        var hydrationArray = species.hydrationResources;

        bool wantsFood = maxHungerToSatisfy > 0f && edibleArray != null && edibleArray.Length > 0;
        bool wantsWater = maxThirstToSatisfy > 0f && hydrationArray != null && hydrationArray.Length > 0;

        if (!wantsFood && !wantsWater)
            return result;

        if (!_tilesByCoord.TryGetValue(coord, out var envControl) || envControl == null)
            return result;

        var nodes = envControl.GetComponentsInChildren<EnvironmentResourceNode>();
        if (nodes == null || nodes.Length == 0)
            return result;

        float hungerRemaining = maxHungerToSatisfy;
        float thirstRemaining = maxThirstToSatisfy;

        foreach (var node in nodes)
        {
            var nodeResources = node.MutableSpawnedResources;
            if (nodeResources == null || nodeResources.Count == 0)
                continue;

            for (int i = 0; i < nodeResources.Count; i++)
            {
                if (hungerRemaining <= 0f && thirstRemaining <= 0f)
                    break;

                var entry = nodeResources[i];
                if (entry.amount <= 0)
                    continue;

                var def = entry.definition;
                if (def == null)
                    continue;

                bool canEat = wantsFood && ResourceInArray(edibleArray, def);
                bool canDrink = wantsWater && ResourceInArray(hydrationArray, def);

                if (!canEat && !canDrink)
                    continue;

                float nutritionPerUnit = canEat ? species.hungerPerResourceUnit : 0f;
                float hydrationPerUnit = canDrink ? species.thirstPerResourceUnit : 0f;

                int unitsForHunger = 0;
                int unitsForThirst = 0;

                if (nutritionPerUnit > 0f && hungerRemaining > 0f)
                {
                    unitsForHunger = Mathf.Min(
                        entry.amount,
                        Mathf.CeilToInt(hungerRemaining / nutritionPerUnit)
                    );
                }

                if (hydrationPerUnit > 0f && thirstRemaining > 0f)
                {
                    int maxUnitsByThirst = Mathf.CeilToInt(thirstRemaining / hydrationPerUnit);

                    int remainingUnits = entry.amount - unitsForHunger;
                    if (remainingUnits > 0)
                        unitsForThirst = Mathf.Min(remainingUnits, maxUnitsByThirst);
                }

                int totalUnits = unitsForHunger + unitsForThirst;
                if (totalUnits <= 0)
                    continue;

                entry.amount -= totalUnits;
                nodeResources[i] = entry;

                float hungerSatisfied = unitsForHunger * nutritionPerUnit;
                float thirstSatisfied = unitsForThirst * hydrationPerUnit;

                hungerRemaining -= hungerSatisfied;
                thirstRemaining -= thirstSatisfied;

                result.hungerSatisfied += hungerSatisfied;
                result.thirstSatisfied += thirstSatisfied;
            }

            if (hungerRemaining <= 0f && thirstRemaining <= 0f)
                break;
        }

        return result;
    }

    public bool HasHydrationResourcesForSpecies(TileCoord coord, AnimalDefinition species)
    {
        if (species == null || species.hydrationResources == null || species.hydrationResources.Length == 0)
            return false;

        if (!_tilesByCoord.TryGetValue(coord, out var envControl) || envControl == null)
            return false;

        var nodes = envControl.GetComponentsInChildren<EnvironmentResourceNode>();
        if (nodes == null || nodes.Length == 0)
            return false;

        foreach (var node in nodes)
        {
            var nodeResources = node.MutableSpawnedResources;
            if (nodeResources == null || nodeResources.Count == 0)
                continue;

            for (int i = 0; i < nodeResources.Count; i++)
            {
                var entry = nodeResources[i];
                if (entry.amount <= 0 || entry.definition == null)
                    continue;

                if (ResourceInArray(species.hydrationResources, entry.definition))
                    return true;
            }
        }

        return false;
    }

    private static bool ResourceInArray(ResourceDefinition[] array, ResourceDefinition def)
    {
        if (array == null) return false;

        for (int i = 0; i < array.Length; i++)
        {
            if (array[i] == def)
                return true;
        }

        return false;
    }

    public bool TryGetPrimaryCoord(TileCoord anyCoordOnEnv, out TileCoord primaryCoord)
    {
        primaryCoord = default;

        if (!_tilesByCoord.TryGetValue(anyCoordOnEnv, out var env) || env == null)
            return false;

        return _primaryCoordByEnv.TryGetValue(env, out primaryCoord);
    }

    public bool TryGetFootprintCoords(TileCoord anyCoordOnEnv, List<TileCoord> results)
    {
        if (results == null)
            return false;

        results.Clear();

        if (!_tilesByCoord.TryGetValue(anyCoordOnEnv, out var env) || env == null)
            return false;

        if (_footprintCoordsByEnv.TryGetValue(env, out var coords) && coords != null && coords.Count > 0)
        {
            for (int i = 0; i < coords.Count; i++)
                results.Add(coords[i]);

            return true;
        }

        if (_primaryCoordByEnv.TryGetValue(env, out var primaryCoord))
        {
            results.Add(primaryCoord);
            return true;
        }

        return false;
    }

    public bool HasLiveEnvironmentTile(TileCoord coord)
        => _tilesByCoord.TryGetValue(coord, out var env) && env != null;

    public TileEnvironmentData GetTileData(TileCoord coord)
    {
        TileEnvironmentData data = default;

        if (_tilesByCoord.TryGetValue(coord, out var env) && env != null)
        {
            var envType = env.environmentType;
            var tileType = env.environmentTileType;

            bool isWater = IsWaterTile(envType, tileType);

            data.hasWater = isWater;
            data.plantFood = isWater ? 0f : 10f;
            data.dangerLevel = 0f;
            data.climateScore = 1f;
            data.environmentType = envType;
            data.tileType = tileType;
        }
        else
        {
            data.hasWater = false;
            data.plantFood = 0f;
            data.dangerLevel = 0f;
            data.climateScore = 1f;
            data.environmentType = EnvironmentType.Grassland;
            data.tileType = EnvironmentTileType.Land;
        }

        return data;
    }

    private bool IsWaterTile(EnvironmentType envType, EnvironmentTileType tileType)
    {
        if (envType == EnvironmentType.Ocean || envType == EnvironmentType.Lake)
            return true;

        switch (tileType)
        {
            case EnvironmentTileType.Lake:
            case EnvironmentTileType.Water:
            case EnvironmentTileType.River:
            case EnvironmentTileType.RiverCorner:
            case EnvironmentTileType.RiverSplit:
            case EnvironmentTileType.RiverCross:
            case EnvironmentTileType.RiverEnd:
            case EnvironmentTileType.RiverMouth:
            case EnvironmentTileType.LakeMouth:
            case EnvironmentTileType.LakeEdge:
            case EnvironmentTileType.LakeCorner:
                return true;

            default:
                return false;
        }
    }

    public void DebugDumpCoord(TileCoord coord, int radius = 2)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"[ENV DEBUG] Dump around {coord} radius={radius}");
        sb.AppendLine($"[ENV DEBUG] registeredCoords={_tilesByCoord.Count} primaryEnvs={_primaryCoordByEnv.Count} footprintEnvs={_footprintCoordsByEnv.Count}");

        AppendCoordDebug(sb, coord, "CENTER");

        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                TileCoord sample = new TileCoord(coord.x + dx, coord.y + dy);
                AppendCoordDebug(sb, sample, "NEIGHBOUR");
            }
        }

        //Debug.Log(sb.ToString());
    }

    private void AppendCoordDebug(StringBuilder sb, TileCoord coord, string label)
    {
        bool live = _tilesByCoord.TryGetValue(coord, out var env) && env != null;

        if (!live)
        {
            sb.AppendLine($"[ENV DEBUG] {label} coord={coord} registered=NO");
            return;
        }

        bool hasPrimary = _primaryCoordByEnv.TryGetValue(env, out var primaryCoord);
        bool hasFootprint = _footprintCoordsByEnv.TryGetValue(env, out var footprint) && footprint != null;

        TileEnvironmentData data = GetTileData(coord);

        sb.AppendLine(
            $"[ENV DEBUG] {label} coord={coord} registered=YES " +
            $"env={(env != null ? env.name : "null")} " +
            $"envType={data.environmentType} tileType={data.tileType} hasWater={data.hasWater} " +
            $"primary={(hasPrimary ? primaryCoord.ToString() : "MISSING")} " +
            $"footprintCount={(hasFootprint ? footprint.Count : 0)}");
    }

    public void DebugValidateRegistry()
    {
        int missingPrimaryForCoord = 0;
        int missingFootprintForPrimary = 0;
        int coordsNotInOwnFootprint = 0;
        int nullEnvCoords = 0;

        foreach (var kvp in _tilesByCoord)
        {
            TileCoord coord = kvp.Key;
            EnvironmentControl env = kvp.Value;

            if (env == null)
            {
                nullEnvCoords++;
                continue;
            }

            if (!_primaryCoordByEnv.ContainsKey(env))
                missingPrimaryForCoord++;

            if (!_footprintCoordsByEnv.TryGetValue(env, out var footprint) || footprint == null || footprint.Count == 0)
            {
                missingFootprintForPrimary++;
            }
            else
            {
                bool contains = false;
                for (int i = 0; i < footprint.Count; i++)
                {
                    if (footprint[i].Equals(coord))
                    {
                        contains = true;
                        break;
                    }
                }

                if (!contains)
                    coordsNotInOwnFootprint++;
            }
        }

        //Debug.Log(
            //$"[ENV REGISTRY VALIDATE] registeredCoords={_tilesByCoord.Count} " +
            //$"primaryEnvs={_primaryCoordByEnv.Count} footprintEnvs={_footprintCoordsByEnv.Count} " +
            //$"nullEnvCoords={nullEnvCoords} missingPrimaryForCoord={missingPrimaryForCoord} " +
            //$"missingFootprintForPrimary={missingFootprintForPrimary} coordsNotInOwnFootprint={coordsNotInOwnFootprint}");
    }
}
