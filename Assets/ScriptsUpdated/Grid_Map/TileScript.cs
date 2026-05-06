using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public enum EnvironmentTileType
{
    Land,
    Coastline,
    CoastlineCorner,
    LakeEdge,
    LakeCorner,
    River,
    RiverCorner,
    RiverSplit,
    Cave,
    Ocean,
    Lake,
    Water,
    RiverMouth,
    LakeMouth,
    RiverCross,
    RiverEnd,
    Mountain,
    SaltLake,
    Beach,
    BeachEnd,
    LakeEdgeEnd,
}

public enum EnvironmentType
{
    BorealForest,
    Desert,
    Grassland,
    Lake,
    Mountain,
    Ocean,
    Savanna,
    SubTropical,
    TemperateForest,
    TropicalForest,
    Tundra,
    SaltLake,
    Beach,
    LakeEdge,
    Volcano
}

// -----------------------------------------------------------------------------
// NOTE: Legacy (old non-SO) option class kept in case other scripts still reference it.
// TileScript now uses EnvironmentTileOptionSO + EnvironmentTileVariant.
// -----------------------------------------------------------------------------
[System.Serializable]
public class EnvironmentTileOption
{
    public string environmentName;
    public EnvironmentType environmentType;
    public EnvironmentTileType tileType;
    public GameObject prefab;

    [Header("Adjacency Groups (OR)")]
    public AdjacencyGroup[] adjacencyGroups;

    [Header("Environment-Type Adjacency Filter")]
    [Tooltip("If non-empty, must see at least one neighbor with one of these types")]
    public EnvironmentType[] allowedNeighborEnvironmentTypes;

    [Tooltip("If non-empty, fails if any neighbor has one of these types")]
    public EnvironmentType[] disallowedNeighborEnvironmentTypes;
}

[System.Serializable]
public class TriggerCondition
{
    public string environmentName;

    [Tooltip("Each of these entries says “when this collider is touching an object with this tag…”")]
    public TriggerRequirement[] requirements;

    [Tooltip("…then rotate the spawned tile to this Y angle")]
    public float forcedYRotation;

    [Tooltip("If true, and this condition is met, it takes precedence over non-priority conditions")]
    public bool isPriority;
}

[System.Serializable]
public class TriggerRequirement
{
    public TileEdge triggerEdge;
    public string requiredTag;
}

[System.Serializable]
public class AdjacencyGroup
{
    public string AdjacencyName;

    [Tooltip("All of these must pass for the group to succeed")]
    public AdjacencyRequirement[] requirements;

    [Header("Optional: when *this* group passes, rotate the tile")]
    public bool applyRotation;
    public float forcedYRotation;
}

[System.Serializable]
public class AdjacencyRequirement
{
    public string AdjacencyName;

    [Tooltip("Which edge-collider to sample")]
    public TileEdge triggerEdge;

    [Tooltip("If non-empty, only these neighbor types are allowed")]
    public EnvironmentTileType[] allowedNeighborTypes;

    [Tooltip("If non-empty, these neighbor types are banned")]
    public EnvironmentTileType[] disallowedNeighborTypes;

    [Header("Also filter by GameObject.tag:")]
    public string[] allowedNeighborTags;
    public string[] disallowedNeighborTags;

    [Tooltip("If true, this requirement will *only* pass if at least one neighbour was detected here")]
    public bool requireTrigger;
}

public class TileScript : MonoBehaviour
{
    public EnvironmentTileOptionSO[] options;
    public TriggerCondition[] rotationConditions;

    [Header("Edge Triggers (set once on the tile prefab)")]
    public Collider northTrigger;
    public Collider eastTrigger;
    public Collider southTrigger;
    public Collider westTrigger;

    [Header("Corner Triggers (set once on the tile prefab)")]
    public Collider northEastTrigger;
    public Collider northWestTrigger;
    public Collider southEastTrigger;
    public Collider southWestTrigger;

    [Header("Grid Edge Rules")]
    [Tooltip("If true, adjacency requirements that point outside the GridManager bounds are ignored/passed.")]
    public bool ignoreAdjacencyRequirementsOutsideGrid = true;

    [Header("Debug")]
    public bool logSpawnDebug = false;

    private GameObject _spawnedInstance;
    private bool _hasSpawned = false;
    public bool HasSpawned => _hasSpawned;

    private EnvironmentTileType _chosenTileType;
    private EnvironmentType _chosenEnvironmentType;

    public TileSize tileSize;

    // Only THESE types are allowed to fall back when no adjacency group matches.
    // Everything “shape + rotation driven” should NOT fall back.
    private static readonly HashSet<EnvironmentTileType> kAllowGrouplessFallback = new HashSet<EnvironmentTileType>
    {
        EnvironmentTileType.Land,
        EnvironmentTileType.Ocean,
        EnvironmentTileType.Lake,
        EnvironmentTileType.Mountain,
        EnvironmentTileType.Water, // ponds/water patches usually not oriented by edge rules
    };

    // -------------------------------------------------------------------------
    // Main spawn (weighted, adjacency-aware, climate-aware)
    // -------------------------------------------------------------------------
    public void SpawnEnvironmentTile()
    {
        var climateMgr = ClimateManager.Instance ?? FindObjectOfType<ClimateManager>();
        var presetMgr = EnvironmentPresetManager.Instance ?? FindObjectOfType<EnvironmentPresetManager>();

        if (options == null || options.Length == 0) return;

        // 1) TriggerCondition rotation is a FALLBACK rotation (do NOT rotate the parent before adjacency checks)
        float triggerY = 0f;
        bool forcedByTrigger = false;

        var triggerMatch =
            rotationConditions?.FirstOrDefault(c => c != null && c.isPriority && CheckTriggerCondition(c))
            ?? rotationConditions?.FirstOrDefault(c => c != null && !c.isPriority && CheckTriggerCondition(c));

        if (triggerMatch != null)
        {
            triggerY = triggerMatch.forcedYRotation;
            forcedByTrigger = true;
        }

        bool wasSpawnedBefore = _hasSpawned;

        // 2) Build candidates (adjacency groups are authoritative)
        var candidates = new List<Candidate>(64);
        BuildCandidates_AllOptions(climateMgr, candidates);

        // If somehow nothing is valid, we can optionally do a last-ditch relax to avoid holes.
        // (This only triggers in misconfigured edge cases.)
        if (candidates.Count == 0)
        {
            if (logSpawnDebug)
                Debug.LogWarning("[TileScript] No candidates found under strict adjacency rules. Relaxing as last resort.");
            BuildCandidates_AllOptions(climateMgr, candidates, relaxAdjacencyRulesAsLastResort: true);
        }

        if (candidates.Count == 0)
            return;

        // 3) Climate for this tile
        bool hasClimate = TryGetTileClimate(climateMgr, out float tileTemp, out float tileHum, out EnvironmentType climatePreferredEnv);

        // 4) Group by tile type
        var byType = new Dictionary<EnvironmentTileType, List<Candidate>>();
        for (int i = 0; i < candidates.Count; i++)
        {
            var tt = candidates[i].opt.tileType;
            if (!byType.TryGetValue(tt, out var list))
            {
                list = new List<Candidate>(16);
                byType[tt] = list;
            }
            list.Add(candidates[i]);
        }

        // 5) Apply climate filtering PER TILE TYPE (do not let a type vanish)
        var effectiveByType = new Dictionary<EnvironmentTileType, List<Candidate>>(byType.Count);

        foreach (var kv in byType)
        {
            var typeList = kv.Value;

            if (!hasClimate)
            {
                effectiveByType[kv.Key] = typeList;
                continue;
            }

            var inRange = typeList.Where(c => PassClimateRange(c.variant, tileTemp, tileHum)).ToList();
            if (inRange.Count > 0)
            {
                effectiveByType[kv.Key] = inRange;
                continue;
            }

            // Closest within this tile type
            float bestD = float.PositiveInfinity;
            var closest = new List<Candidate>();

            for (int i = 0; i < typeList.Count; i++)
            {
                float d = ClimateRangeDistance(typeList[i].variant, tileTemp, tileHum);
                if (d < bestD - 0.0001f)
                {
                    bestD = d;
                    closest.Clear();
                    closest.Add(typeList[i]);
                }
                else if (Mathf.Abs(d - bestD) < 0.0001f)
                {
                    closest.Add(typeList[i]);
                }
            }

            effectiveByType[kv.Key] = (closest.Count > 0) ? closest : typeList;
        }

        // 6) Choose tile type by preset weights (+ optional tileSize weight)
        float sizeW = 1f;
        if (presetMgr != null)
            sizeW = Mathf.Max(0f, presetMgr.GetTileSizeWeight(tileSize));

        float typeTotal = 0f;
        foreach (var kv in effectiveByType)
        {
            float w = 1f;
            if (presetMgr != null)
                w = presetMgr.GetTileTypeWeight(kv.Key);

            w = Mathf.Max(0f, w) * sizeW;
            if (w > 0f) typeTotal += w;
        }

        EnvironmentTileType chosenType = effectiveByType.Keys.First();

        if (typeTotal > 0f)
        {
            float roll = Random.value * typeTotal;
            float acc = 0f;

            foreach (var kv in effectiveByType)
            {
                float w = 1f;
                if (presetMgr != null)
                    w = presetMgr.GetTileTypeWeight(kv.Key);

                w = Mathf.Max(0f, w) * sizeW;
                if (w <= 0f) continue;

                acc += w;
                if (roll <= acc)
                {
                    chosenType = kv.Key;
                    break;
                }
            }
        }
        else
        {
            int idx = Random.Range(0, effectiveByType.Count);
            int k = 0;
            foreach (var kv in effectiveByType)
            {
                if (k++ == idx) { chosenType = kv.Key; break; }
            }
        }

        var typePool = effectiveByType[chosenType];

        // 7) Choose candidate within type (env weight * climate factor)
        Candidate chosenCand = typePool[typePool.Count - 1];

        if (typePool.Count == 1)
        {
            chosenCand = typePool[0];
        }
        else
        {
            float total = 0f;

            for (int i = 0; i < typePool.Count; i++)
            {
                float envW = 1f;
                if (presetMgr != null)
                    envW = Mathf.Max(0f, presetMgr.GetEnvironmentWeight(typePool[i].variant.environmentType));

                float climateW = 1f;
                if (hasClimate)
                    climateW = GetClimateWeightFactor(climatePreferredEnv, typePool[i].variant.environmentType);

                total += Mathf.Max(0.001f, envW * climateW);
            }

            float roll2 = Random.value * total;
            float acc2 = 0f;

            for (int i = 0; i < typePool.Count; i++)
            {
                float envW = 1f;
                if (presetMgr != null)
                    envW = Mathf.Max(0f, presetMgr.GetEnvironmentWeight(typePool[i].variant.environmentType));

                float climateW = 1f;
                if (hasClimate)
                    climateW = GetClimateWeightFactor(climatePreferredEnv, typePool[i].variant.environmentType);

                acc2 += Mathf.Max(0.001f, envW * climateW);

                if (roll2 <= acc2)
                {
                    chosenCand = typePool[i];
                    break;
                }
            }
        }

        if (logSpawnDebug)
        {
            string presetName = presetMgr != null && presetMgr.GetCurrentPreset() != null
                ? presetMgr.GetCurrentPreset().presetName
                : "NULL";
            Debug.Log($"[TileScript] Preset={presetName} hasClimate={hasClimate} chosenType={chosenCand.opt.tileType} chosenEnv={chosenCand.variant.environmentType} candidates={candidates.Count}");
        }

        // 8) Spawn
        _chosenTileType = chosenCand.opt.tileType;
        _chosenEnvironmentType = chosenCand.variant.environmentType;

        var prefabToSpawn = chosenCand.variant.prefab;

        if (EnvironmentPoolManager.Instance != null)
        {
            _spawnedInstance = EnvironmentPoolManager.Instance.Get(
                prefabToSpawn,
                transform,
                transform.position,
                Quaternion.identity
            );
        }
        else
        {
            _spawnedInstance = Instantiate(
                prefabToSpawn,
                transform.position,
                Quaternion.identity,
                transform
            );
        }

        // 9) Rotation: OptionSO adjacency group rotation wins, otherwise trigger fallback, otherwise random.
        float yRot;
        if (chosenCand.grp != null && chosenCand.grp.applyRotation)
            yRot = chosenCand.grp.forcedYRotation;
        else if (forcedByTrigger)
            yRot = triggerY;
        else
            yRot = 90f * Random.Range(0, 4);

        _spawnedInstance.transform.localRotation = Quaternion.Euler(0, yRot, 0);

        _hasSpawned = true; // MOVED UP

        InitializeSpawnedEnvironmentInstance();
        ApplySpawnedEnvironmentDiscoveryState(false);

        if (!wasSpawnedBefore)
            TileLifecycleEvents.RaiseSpawned(this);
        else
            TileLifecycleEvents.RaiseChanged(this);
    }

    // -------------------------------------------------------------------------
    // Candidate building (AdjacencyGroups are authoritative)
    // -------------------------------------------------------------------------
    private void BuildCandidates_AllOptions(ClimateManager climateMgr, List<Candidate> outCandidates, bool relaxAdjacencyRulesAsLastResort = false)
    {
        outCandidates.Clear();

        for (int o = 0; o < options.Length; o++)
        {
            var opt = options[o];
            if (opt == null || opt.variants == null || opt.variants.Length == 0)
                continue;

            bool hasGroups = opt.adjacencyGroups != null && opt.adjacencyGroups.Length > 0;

            // No adjacency groups -> always valid
            if (!hasGroups)
            {
                AddCandidates_FromNeighbours(opt, grp: null, neighbours: CollectNeighboursByCollider(), climateMgr, outCandidates, allowFallbackAllVariants: true);
                continue;
            }

            // Has adjacency groups -> FIRST passing group wins (shape + rotation)
            AdjacencyGroup matchedGrp = null;
            for (int g = 0; g < opt.adjacencyGroups.Length; g++)
            {
                var grp = opt.adjacencyGroups[g];
                if (!GroupPasses(grp)) continue;
                matchedGrp = grp;
                break;
            }

            if (matchedGrp != null)
            {
                AddCandidates_FromNeighbours(opt, matchedGrp, CollectNeighboursForGroup(matchedGrp), climateMgr, outCandidates, allowFallbackAllVariants: true);
                continue;
            }

            // No group matched -> normally: DO NOT participate (preserves OptionSO rotation rules)
            // Only allow groupless fallback for “non-oriented” types, OR if we are relaxing as last resort.
            if (relaxAdjacencyRulesAsLastResort || kAllowGrouplessFallback.Contains(opt.tileType))
            {
                AddCandidates_FromNeighbours(opt, grp: null, neighbours: CollectNeighboursByCollider(), climateMgr, outCandidates, allowFallbackAllVariants: true);
            }
        }
    }

    private void AddCandidates_FromNeighbours(
        EnvironmentTileOptionSO opt,
        AdjacencyGroup grp,
        List<TileScript> neighbours,
        ClimateManager climateMgr,
        List<Candidate> outCandidates,
        bool allowFallbackAllVariants)
    {
        var neighbourEnvTypes = new List<EnvironmentType>(8);
        CollectNeighbourEnvironmentTypes(neighbours, climateMgr, neighbourEnvTypes);

        bool anyAdded = false;

        for (int v = 0; v < opt.variants.Length; v++)
        {
            var varnt = opt.variants[v];
            if (varnt == null || varnt.prefab == null) continue;

            if (PassNeighbourEnvFilter(varnt, neighbourEnvTypes))
            {
                outCandidates.Add(new Candidate { opt = opt, variant = varnt, grp = grp });
                anyAdded = true;
            }
        }

        if (!anyAdded && allowFallbackAllVariants)
        {
            for (int v = 0; v < opt.variants.Length; v++)
            {
                var varnt = opt.variants[v];
                if (varnt == null || varnt.prefab == null) continue;
                outCandidates.Add(new Candidate { opt = opt, variant = varnt, grp = grp });
            }
        }
    }

    // -------------------------------------------------------------------------
    // Colliders / neighbour sampling
    // -------------------------------------------------------------------------
    private Collider GetEdgeCollider(TileEdge edge)
    {
        return edge switch
        {
            TileEdge.North => northTrigger,
            TileEdge.East => eastTrigger,
            TileEdge.South => southTrigger,
            TileEdge.West => westTrigger,

            TileEdge.NorthEast => northEastTrigger,
            TileEdge.NorthWest => northWestTrigger,
            TileEdge.SouthEast => southEastTrigger,
            TileEdge.SouthWest => southWestTrigger,

            _ => null
        };
    }

    private void GetOverlapBoxParams(Collider col, out Vector3 center, out Vector3 halfExtents, out Quaternion rot)
    {
        if (col is BoxCollider box)
        {
            center = box.transform.TransformPoint(box.center);
            halfExtents = Vector3.Scale(box.size, box.transform.lossyScale) * 0.5f;
            rot = box.transform.rotation;
            return;
        }

        var b = col.bounds;
        center = b.center;
        halfExtents = b.extents;
        rot = col.transform.rotation;
    }

    private Collider[] OverlapBoxAll(Vector3 c, Vector3 e, Quaternion r)
        => Physics.OverlapBox(c, e, r, ~0, QueryTriggerInteraction.Collide);

    private void CollectNeighbourEnvironmentTypes(
        List<TileScript> neighbours,
        ClimateManager climateMgr,
        List<EnvironmentType> outTypes)
    {
        outTypes.Clear();
        if (neighbours == null) return;

        for (int i = 0; i < neighbours.Count; i++)
        {
            var n = neighbours[i];
            if (n == null) continue;

            if (n.HasSpawned)
            {
                outTypes.Add(n.GetChosenEnvironmentType());
                continue;
            }

            if (climateMgr != null &&
                (climateMgr.TryGetClimateAtWorldPos(n.transform.position, out float t, out float h) ||
                 climateMgr.TryComputeInstantClimateAtWorldPos(n.transform.position, out t, out h)))
            {
                outTypes.Add(climateMgr.PickBiomeFromClimate(t, h, EnvironmentType.Grassland));
            }
        }
    }

    private bool CheckTriggerCondition(TriggerCondition cond)
    {
        foreach (var req in cond.requirements ?? System.Array.Empty<TriggerRequirement>())
        {
            var col = GetEdgeCollider(req.triggerEdge);
            if (col == null) return false;

            GetOverlapBoxParams(col, out var c, out var e, out var r);

            if (!OverlapBoxAll(c, e, r).Any(h => h != null && h.CompareTag(req.requiredTag)))
                return false;
        }
        return true;
    }

    private (bool pass, string reason) CheckAdjacencyRequirement(AdjacencyRequirement req)
    {
        var col = GetEdgeCollider(req.triggerEdge);
        if (col == null)
            return (false, $"Missing edge collider for {req.triggerEdge}");

        GetOverlapBoxParams(col, out var c, out var e, out var r);

        if (ignoreAdjacencyRequirementsOutsideGrid && IsAdjacencySampleOutsideGrid(c))
            return (true, $"Ignored {req.triggerEdge}: outside grid");

        var hits = OverlapBoxAll(c, e, r);

        var neighbours = new List<TileScript>(8);
        var seen = new HashSet<TileScript>();
        var tagBag = new HashSet<string>();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider h = hits[i];

            if (h == null)
                continue;

            TileScript ts = h.GetComponentInParent<TileScript>();

            if (ts == null || ts == this)
                continue;

            if (!ts.isActiveAndEnabled)
                continue;

            if (seen.Add(ts))
                neighbours.Add(ts);

            if (!string.IsNullOrEmpty(h.tag))
                tagBag.Add(h.tag);

            if (!string.IsNullOrEmpty(ts.gameObject.tag))
                tagBag.Add(ts.gameObject.tag);

            GameObject spawned = ts.GetSpawnedInstanceSafe();

            if (spawned != null)
            {
                if (!string.IsNullOrEmpty(spawned.tag))
                    tagBag.Add(spawned.tag);

                Transform[] trs = spawned.GetComponentsInChildren<Transform>(true);

                for (int t = 0; t < trs.Length; t++)
                {
                    if (trs[t] != null && !string.IsNullOrEmpty(trs[t].tag))
                        tagBag.Add(trs[t].tag);
                }
            }
        }

        if (req.requireTrigger && neighbours.Count == 0)
            return (false, "requireTrigger saw none");

        List<TileScript> spawnedNeighbours = neighbours
            .Where(n => n != null && n.HasSpawned)
            .ToList();

        bool hasAllowedTypes =
            req.allowedNeighborTypes != null &&
            req.allowedNeighborTypes.Length > 0;

        bool hasDisallowedTypes =
            req.disallowedNeighborTypes != null &&
            req.disallowedNeighborTypes.Length > 0;

        bool hasAllowedTags =
            req.allowedNeighborTags != null &&
            req.allowedNeighborTags.Length > 0;

        bool hasDisallowedTags =
            req.disallowedNeighborTags != null &&
            req.disallowedNeighborTags.Length > 0;

        // IMPORTANT:
        // If allowed tags are configured, they must match on THIS edge.
        // This is what prevents RiverSplit / T-junction from passing when there is
        // only some random unspawned replacement root or land tile on that side.
        if (hasAllowedTags)
        {
            bool matchedAllowedTag = false;

            for (int i = 0; i < req.allowedNeighborTags.Length; i++)
            {
                string allowed = req.allowedNeighborTags[i];

                if (string.IsNullOrEmpty(allowed))
                    continue;

                if (tagBag.Contains(allowed))
                {
                    matchedAllowedTag = true;
                    break;
                }
            }

            if (!matchedAllowedTag)
                return (false, "allowedNeighborTags mismatch");
        }

        if (hasDisallowedTags)
        {
            for (int i = 0; i < req.disallowedNeighborTags.Length; i++)
            {
                string banned = req.disallowedNeighborTags[i];

                if (string.IsNullOrEmpty(banned))
                    continue;

                if (tagBag.Contains(banned))
                    return (false, "disallowedNeighborTags saw forbidden");
            }
        }

        // IMPORTANT:
        // The old logic only checked allowedNeighborTypes if spawned.Count > 0.
        // During earthquake staging, replacement roots can be unspawned, which meant
        // a shape group could pass even though no valid river type existed on that edge.
        if (hasAllowedTypes)
        {
            bool matchedAllowedType = false;

            for (int i = 0; i < spawnedNeighbours.Count; i++)
            {
                TileScript n = spawnedNeighbours[i];

                if (n == null)
                    continue;

                EnvironmentTileType nType = n.GetChosenTileType();

                if (req.allowedNeighborTypes.Contains(nType))
                {
                    matchedAllowedType = true;
                    break;
                }
            }

            if (!matchedAllowedType)
                return (false, "allowedNeighborTypes mismatch");
        }

        if (hasDisallowedTypes)
        {
            for (int i = 0; i < spawnedNeighbours.Count; i++)
            {
                TileScript n = spawnedNeighbours[i];

                if (n == null)
                    continue;

                EnvironmentTileType nType = n.GetChosenTileType();

                if (req.disallowedNeighborTypes.Contains(nType))
                    return (false, "disallowedNeighborTypes saw forbidden");
            }
        }

        return (true, "PASS");
    }

    public GameObject GetSpawnedInstanceSafe()
    {
        return _spawnedInstance;
    }

    private bool IsAdjacencySampleOutsideGrid(Vector3 sampleWorldPosition)
    {
        GridManager gm = GridManager.Instance;

        if (gm == null)
            gm = FindObjectOfType<GridManager>();

        if (gm == null)
            return false;

        Vector2Int gridPos = gm.GetGridPosition(sampleWorldPosition);

        return gridPos.x < 0 ||
               gridPos.y < 0 ||
               gridPos.x >= gm.columns ||
               gridPos.y >= gm.rows;
    }

    private bool GroupPasses(AdjacencyGroup grp)
    {
        if (grp == null || grp.requirements == null || grp.requirements.Length == 0)
            return true;

        for (int i = 0; i < grp.requirements.Length; i++)
        {
            var (pass, _) = CheckAdjacencyRequirement(grp.requirements[i]);
            if (!pass) return false;
        }
        return true;
    }

    private List<TileScript> CollectNeighboursForGroup(AdjacencyGroup grp)
    {
        var set = new HashSet<TileScript>();
        if (grp == null || grp.requirements == null) return set.ToList();

        foreach (var req in grp.requirements)
        {
            var col = GetEdgeCollider(req.triggerEdge);
            if (col == null) continue;

            GetOverlapBoxParams(col, out var c, out var e, out var r);
            var hits = OverlapBoxAll(c, e, r);

            foreach (var h in hits)
            {
                if (h == null) continue;

                var ts = h.GetComponentInParent<TileScript>();
                if (ts != null && ts != this)
                    set.Add(ts);
            }
        }

        return set.ToList();
    }

    private List<TileScript> CollectNeighboursByCollider()
    {
        var set = new HashSet<TileScript>();

        var col = GetComponent<Collider>();
        if (col == null) return set.ToList();

        var ext = col.bounds.extents * 1.2f;
        var hits = OverlapBoxAll(col.bounds.center, ext, transform.rotation);

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h == null) continue;

            var ts = h.GetComponentInParent<TileScript>();
            if (ts != null && ts != this)
                set.Add(ts);
        }

        return set.ToList();
    }

    // -------------------------------------------------------------------------
    // Climate
    // -------------------------------------------------------------------------
    private bool TryGetTileClimate(ClimateManager climateMgr, out float temp, out float hum, out EnvironmentType preferredEnv)
    {
        temp = 0f;
        hum = 0f;
        preferredEnv = EnvironmentType.Grassland;

        if (climateMgr == null) return false;

        bool ok =
            climateMgr.TryGetClimateAtWorldPos(transform.position, out temp, out hum) ||
            climateMgr.TryComputeInstantClimateAtWorldPos(transform.position, out temp, out hum);

        if (!ok) return false;

        preferredEnv = climateMgr.PickBiomeFromClimate(temp, hum, EnvironmentType.Grassland);
        return true;
    }

    private bool PassClimateRange(EnvironmentTileVariant v, float temp, float hum)
    {
        if (v == null) return false;

        var f = v.neighborEnvFilter;
        if (f == null) return true;

        if (f.useTemperatureRange)
        {
            float minT = Mathf.Min(f.minTempC, f.maxTempC);
            float maxT = Mathf.Max(f.minTempC, f.maxTempC);
            if (temp < minT || temp > maxT) return false;
        }

        if (f.useHumidityRange)
        {
            float minH = Mathf.Min(f.minHumidity, f.maxHumidity);
            float maxH = Mathf.Max(f.minHumidity, f.maxHumidity);
            if (hum < minH || hum > maxH) return false;
        }

        return true;
    }

    private float ClimateRangeDistance(EnvironmentTileVariant v, float temp, float hum)
    {
        if (v == null) return float.PositiveInfinity;

        var f = v.neighborEnvFilter;
        if (f == null) return 0f;

        float d = 0f;

        if (f.useTemperatureRange)
        {
            float minT = Mathf.Min(f.minTempC, f.maxTempC);
            float maxT = Mathf.Max(f.minTempC, f.maxTempC);
            if (temp < minT) d += (minT - temp);
            else if (temp > maxT) d += (temp - maxT);
        }

        if (f.useHumidityRange)
        {
            float minH = Mathf.Min(f.minHumidity, f.maxHumidity);
            float maxH = Mathf.Max(f.minHumidity, f.maxHumidity);
            if (hum < minH) d += (minH - hum);
            else if (hum > maxH) d += (hum - maxH);
        }

        return d;
    }

    // -------------------------------------------------------------------------
    // Variant neighbour ENV filtering (spawn-order safe)
    // -------------------------------------------------------------------------
    private bool PassNeighbourEnvFilter(EnvironmentTileVariant v, IReadOnlyList<EnvironmentType> neighbourEnvs)
    {
        if (v == null) return false;
        if (neighbourEnvs == null) neighbourEnvs = System.Array.Empty<EnvironmentType>();

        var allowed = v.neighborEnvFilter != null
            ? v.neighborEnvFilter.allowedNeighborEnvironmentTypes
            : v.allowedNeighborEnvironmentTypes;

        var disallowed = v.neighborEnvFilter != null
            ? v.neighborEnvFilter.disallowedNeighborEnvironmentTypes
            : v.disallowedNeighborEnvironmentTypes;

        if (disallowed != null && disallowed.Length > 0)
        {
            for (int i = 0; i < neighbourEnvs.Count; i++)
                if (disallowed.Contains(neighbourEnvs[i]))
                    return false;
        }

        if (allowed != null && allowed.Length > 0)
        {
            if (neighbourEnvs.Count > 0)
            {
                bool any = false;
                for (int i = 0; i < neighbourEnvs.Count; i++)
                {
                    if (allowed.Contains(neighbourEnvs[i]))
                    {
                        any = true;
                        break;
                    }
                }
                if (!any) return false;
            }
        }

        return true;
    }

    private int GetSpecificityScore(EnvironmentTileVariant v)
    {
        if (v == null) return 0;

        if (v.neighborEnvFilter != null)
            return v.neighborEnvFilter.GetSpecificityScore();

        int a = v.allowedNeighborEnvironmentTypes != null ? v.allowedNeighborEnvironmentTypes.Length : 0;
        int d = v.disallowedNeighborEnvironmentTypes != null ? v.disallowedNeighborEnvironmentTypes.Length : 0;
        return a + d;
    }

    // -------------------------------------------------------------------------
    // Force spawn methods
    // (These are unchanged from your last version, except they still compile with BuildingStatus.)
    // -------------------------------------------------------------------------

    private void ApplySpawnedEnvironmentDiscoveryState(bool discovered)
    {
        if (_spawnedInstance == null) return;

        var env = _spawnedInstance.GetComponentInChildren<EnvironmentControl>(true);
        if (env == null) return;

        if (env.TryGetComponent<EnvironmentStatus>(out var status))
        {
            status.SetDiscovered(discovered);
        }
        else
        {
            Debug.LogWarning("[TileScript] Spawned environment has no EnvironmentStatus; cannot apply discovery state.");
        }
    }

    public bool ForceSpawnSpecific(
    EnvironmentType desiredEnv,
    EnvironmentTileType desiredType,
    bool markDiscovered,
    IReadOnlyList<EnvironmentType> neighbourEnvironmentTypes,
    bool allowFallbackToAnyEnvVariant = true)
    {
        if (options == null || options.Length == 0) return false;
        if (neighbourEnvironmentTypes == null) neighbourEnvironmentTypes = System.Array.Empty<EnvironmentType>();

        var optMatches = new List<EnvironmentTileOptionSO>();
        for (int i = 0; i < options.Length; i++)
        {
            var o = options[i];
            if (o == null) continue;
            if (o.tileType == desiredType)
                optMatches.Add(o);
        }
        if (optMatches.Count == 0) return false;

        var variants = new List<(EnvironmentTileOptionSO opt, EnvironmentTileVariant v)>();
        for (int i = 0; i < optMatches.Count; i++)
        {
            var opt = optMatches[i];
            if (opt.variants == null) continue;

            for (int j = 0; j < opt.variants.Length; j++)
            {
                var v = opt.variants[j];
                if (v == null || v.prefab == null) continue;
                if (v.environmentType == desiredEnv)
                    variants.Add((opt, v));
            }
        }

        if (variants.Count == 0)
        {
            if (!allowFallbackToAnyEnvVariant)
                return false;

            for (int i = 0; i < optMatches.Count; i++)
            {
                var opt = optMatches[i];
                if (opt.variants == null) continue;

                for (int j = 0; j < opt.variants.Length; j++)
                {
                    var v = opt.variants[j];
                    if (v == null || v.prefab == null) continue;
                    variants.Add((opt, v));
                }
            }
        }

        if (variants.Count == 0) return false;

        var passing = variants.Where(x => PassNeighbourEnvFilter(x.v, neighbourEnvironmentTypes)).ToList();
        var pool = (passing.Count > 0) ? passing : variants;

        int bestScore = int.MinValue;
        var best = new List<(EnvironmentTileOptionSO opt, EnvironmentTileVariant v)>();
        for (int i = 0; i < pool.Count; i++)
        {
            int score = GetSpecificityScore(pool[i].v);
            if (score > bestScore)
            {
                bestScore = score;
                best.Clear();
                best.Add(pool[i]);
            }
            else if (score == bestScore)
            {
                best.Add(pool[i]);
            }
        }

        var pick = best[Random.Range(0, best.Count)];

        bool wasSpawnedBefore = _hasSpawned;

        Quaternion localRot = Quaternion.identity;
        bool hadInstance = _spawnedInstance != null;
        if (hadInstance)
        {
            localRot = _spawnedInstance.transform.localRotation;

            if (EnvironmentPoolManager.Instance != null)
                EnvironmentPoolManager.Instance.Release(_spawnedInstance);
            else
                Destroy(_spawnedInstance);

            _spawnedInstance = null;
        }

        _chosenTileType = desiredType;
        _chosenEnvironmentType = pick.v.environmentType;

        if (EnvironmentPoolManager.Instance != null)
        {
            _spawnedInstance = EnvironmentPoolManager.Instance.Get(
                pick.v.prefab,
                transform,
                transform.position,
                Quaternion.identity
            );
        }
        else
        {
            _spawnedInstance = Instantiate(
                pick.v.prefab,
                transform.position,
                Quaternion.identity,
                transform
            );
        }

        if (hadInstance)
            _spawnedInstance.transform.localRotation = localRot;

        _hasSpawned = true; // MOVED UP

        InitializeSpawnedEnvironmentInstance();
        ApplySpawnedEnvironmentDiscoveryState(markDiscovered);

        if (!wasSpawnedBefore)
            TileLifecycleEvents.RaiseSpawned(this);
        else
            TileLifecycleEvents.RaiseChanged(this);

        return true;
    }

    public bool ForceSpawnSpecific(EnvironmentType desiredEnv, EnvironmentTileType desiredType)
        => ForceSpawnSpecific(desiredEnv, desiredType, true, System.Array.Empty<EnvironmentType>());

    public bool ForceSpawnSpecific(EnvironmentType desiredEnv, EnvironmentTileType desiredType, bool markDiscovered)
        => ForceSpawnSpecific(desiredEnv, desiredType, markDiscovered, System.Array.Empty<EnvironmentType>());

    // -------------------------------------------------------------------------
    // Getters / lifecycle
    // -------------------------------------------------------------------------
    public EnvironmentTileType GetChosenTileType() => _chosenTileType;
    public EnvironmentType GetChosenEnvironmentType() => _chosenEnvironmentType;

    public void DeactivateSpawnedInstance()
    {
        if (_spawnedInstance != null)
            _spawnedInstance.SetActive(false);
    }

    public void ActivateSpawnedInstance()
    {
        if (_spawnedInstance != null)
            _spawnedInstance.SetActive(true);
    }

    public GameObject GetSpawnedInstance() => _spawnedInstance;

    // -------------------------------------------------------------------------
    // Climate weighting helpers
    // -------------------------------------------------------------------------
    private int GetBiomeGroup(EnvironmentType env)
    {
        switch (env)
        {
            case EnvironmentType.Tundra:
            case EnvironmentType.BorealForest:
                return 0;

            case EnvironmentType.TemperateForest:
                return 1;

            case EnvironmentType.TropicalForest:
            case EnvironmentType.SubTropical:
                return 2;

            case EnvironmentType.Grassland:
                return 3;

            case EnvironmentType.Savanna:
            case EnvironmentType.Desert:
                return 4;

            case EnvironmentType.Ocean:
            case EnvironmentType.Lake:
                return 5;

            case EnvironmentType.Mountain:
            case EnvironmentType.Volcano:
                return 6;

            default:
                return 7;
        }
    }

    private float GetClimateWeightFactor(EnvironmentType climatePreferred, EnvironmentType candidate)
    {
        if (candidate == climatePreferred)
            return 2.5f;

        if (GetBiomeGroup(candidate) == GetBiomeGroup(climatePreferred))
            return 1.5f;

        return 1.0f;
    }

    private struct Candidate
    {
        public EnvironmentTileOptionSO opt;
        public EnvironmentTileVariant variant;
        public AdjacencyGroup grp; // THIS is the winning rotation group from the OptionSO
    }

    public bool ForceSpawnSpecificTileType(EnvironmentTileType desiredType, float? yRotation = null)
    {
        var climateMgr = ClimateManager.Instance ?? FindObjectOfType<ClimateManager>();
        if (options == null || options.Length == 0) return false;

        var candidates = new List<Candidate>(32);

        // Strict: adjacencyGroups must match if present
        BuildCandidates_ForTileType(climateMgr, desiredType, candidates, relaxAdjacencyRulesAsLastResort: false);

        // Last resort relax (prevents holes if misconfigured)
        if (candidates.Count == 0)
            BuildCandidates_ForTileType(climateMgr, desiredType, candidates, relaxAdjacencyRulesAsLastResort: true);

        if (candidates.Count == 0) return false;

        // Prefer climate env if we can compute it (nice selection)
        EnvironmentType preferredEnv = EnvironmentType.Grassland;
        if (climateMgr != null &&
            (climateMgr.TryGetClimateAtWorldPos(transform.position, out float t, out float h) ||
             climateMgr.TryComputeInstantClimateAtWorldPos(transform.position, out t, out h)))
        {
            preferredEnv = climateMgr.PickBiomeFromClimate(t, h, EnvironmentType.Grassland);
        }

        // Prefer preferred env variants if possible
        var pool = candidates.Where(c => c.variant != null && c.variant.environmentType == preferredEnv).ToList();
        if (pool.Count == 0) pool = candidates;

        // Prefer most specific neighbour-env filter
        int bestScore = int.MinValue;
        var best = new List<Candidate>();
        for (int i = 0; i < pool.Count; i++)
        {
            int score = GetSpecificityScore(pool[i].variant);
            if (score > bestScore)
            {
                bestScore = score;
                best.Clear();
                best.Add(pool[i]);
            }
            else if (score == bestScore)
            {
                best.Add(pool[i]);
            }
        }

        var chosen = best[Random.Range(0, best.Count)];

        bool wasSpawnedBefore = _hasSpawned;

        // Replace old instance
        if (_spawnedInstance != null)
        {
            if (EnvironmentPoolManager.Instance != null)
                EnvironmentPoolManager.Instance.Release(_spawnedInstance);
            else
                Destroy(_spawnedInstance);

            _spawnedInstance = null;
        }

        _chosenTileType = desiredType;
        _chosenEnvironmentType = chosen.variant.environmentType;

        if (EnvironmentPoolManager.Instance != null)
        {
            _spawnedInstance = EnvironmentPoolManager.Instance.Get(
                chosen.variant.prefab,
                transform,
                transform.position,
                Quaternion.identity
            );
        }
        else
        {
            _spawnedInstance = Instantiate(
                chosen.variant.prefab,
                transform.position,
                Quaternion.identity,
                transform
            );
        }

        // Rotation priority:
        // 1) explicit override passed in
        // 2) matched adjacency group rotation (OptionSO rule)
        // 3) random
        float y =
            yRotation.HasValue ? yRotation.Value :
            (chosen.grp != null && chosen.grp.applyRotation) ? chosen.grp.forcedYRotation :
            (90f * Random.Range(0, 4));

        _spawnedInstance.transform.localRotation = Quaternion.Euler(0, y, 0);

        _hasSpawned = true; // before init/events

        InitializeSpawnedEnvironmentInstance();

        if (!wasSpawnedBefore)
            TileLifecycleEvents.RaiseSpawned(this);
        else
            TileLifecycleEvents.RaiseChanged(this);

        return true;
    }

    // Backwards-compatible overload (if any scripts call it with 1 arg)
    public bool ForceSpawnSpecificTileType(EnvironmentTileType desiredType)
        => ForceSpawnSpecificTileType(desiredType, null);

    // -------------------------------------------------------------------------
    // Candidate builder for a specific tile type (supports strict + relaxed mode)
    // -------------------------------------------------------------------------
    private void BuildCandidates_ForTileType(
        ClimateManager climateMgr,
        EnvironmentTileType desiredType,
        List<Candidate> outCandidates,
        bool relaxAdjacencyRulesAsLastResort)
    {
        outCandidates.Clear();

        for (int o = 0; o < options.Length; o++)
        {
            var opt = options[o];
            if (opt == null) continue;
            if (opt.tileType != desiredType) continue;
            if (opt.variants == null || opt.variants.Length == 0) continue;

            bool hasGroups = opt.adjacencyGroups != null && opt.adjacencyGroups.Length > 0;

            // No adjacency groups -> always valid
            if (!hasGroups)
            {
                AddCandidates_FromNeighbours(opt, grp: null, neighbours: CollectNeighboursByCollider(), climateMgr, outCandidates, allowFallbackAllVariants: true);
                continue;
            }

            // Has adjacency groups -> FIRST passing group wins (shape + rotation)
            AdjacencyGroup matchedGrp = null;
            for (int g = 0; g < opt.adjacencyGroups.Length; g++)
            {
                var grp = opt.adjacencyGroups[g];
                if (!GroupPasses(grp)) continue;
                matchedGrp = grp;
                break;
            }

            if (matchedGrp != null)
            {
                AddCandidates_FromNeighbours(opt, matchedGrp, CollectNeighboursForGroup(matchedGrp), climateMgr, outCandidates, allowFallbackAllVariants: true);
                continue;
            }

            // No group matched:
            // - Normally: DO NOT participate (preserves OptionSO "groups drive shape+rotation")
            // - Allow only for non-oriented types OR last-resort relax
            if (relaxAdjacencyRulesAsLastResort || kAllowGrouplessFallback.Contains(opt.tileType))
            {
                AddCandidates_FromNeighbours(opt, grp: null, neighbours: CollectNeighboursByCollider(), climateMgr, outCandidates, allowFallbackAllVariants: true);
            }
        }
    }

    public bool ForceSpawnSpecificTileTypeFiltered(EnvironmentTileType desiredType, bool markDiscovered = true)
    {
        var climateMgr = ClimateManager.Instance ?? FindObjectOfType<ClimateManager>();
        if (options == null || options.Length == 0) return false;

        // Determine preferred env (for nicer picks)
        EnvironmentType preferredEnv = EnvironmentType.Grassland;
        if (climateMgr != null &&
            (climateMgr.TryGetClimateAtWorldPos(transform.position, out float t, out float h) ||
             climateMgr.TryComputeInstantClimateAtWorldPos(transform.position, out t, out h)))
        {
            preferredEnv = climateMgr.PickBiomeFromClimate(t, h, EnvironmentType.Grassland);
        }

        var candidates = new List<Candidate>(32);

        // Build candidates STRICTLY:
        // - if opt has adjacencyGroups, must match a group
        // - only variants that pass PassNeighbourEnvFilter are added
        BuildCandidates_ForTileType_Filtered(climateMgr, desiredType, candidates);

        if (candidates.Count == 0)
            return false;

        // Prefer preferred-env variants if possible
        var pool = candidates.Where(c => c.variant != null && c.variant.environmentType == preferredEnv).ToList();
        if (pool.Count == 0) pool = candidates;

        // Prefer most specific variant
        int bestScore = int.MinValue;
        var best = new List<Candidate>();
        for (int i = 0; i < pool.Count; i++)
        {
            int score = GetSpecificityScore(pool[i].variant);
            if (score > bestScore)
            {
                bestScore = score;
                best.Clear();
                best.Add(pool[i]);
            }
            else if (score == bestScore)
            {
                best.Add(pool[i]);
            }
        }

        var chosen = best[Random.Range(0, best.Count)];

        bool wasSpawnedBefore = _hasSpawned;

        // Replace old instance
        if (_spawnedInstance != null)
        {
            if (EnvironmentPoolManager.Instance != null)
                EnvironmentPoolManager.Instance.Release(_spawnedInstance);
            else
                Destroy(_spawnedInstance);

            _spawnedInstance = null;
        }

        _chosenTileType = desiredType;
        _chosenEnvironmentType = chosen.variant.environmentType;

        if (EnvironmentPoolManager.Instance != null)
        {
            _spawnedInstance = EnvironmentPoolManager.Instance.Get(
                chosen.variant.prefab,
                transform,
                transform.position,
                Quaternion.identity
            );
        }
        else
        {
            _spawnedInstance = Instantiate(
                chosen.variant.prefab,
                transform.position,
                Quaternion.identity,
                transform
            );
        }

        // Rotation: adjacency-forced if applicable, otherwise random
        float y = (chosen.grp != null && chosen.grp.applyRotation)
            ? chosen.grp.forcedYRotation
            : (90f * Random.Range(0, 4));

        _spawnedInstance.transform.localRotation = Quaternion.Euler(0, y, 0);

        _hasSpawned = true; // before init/events

        InitializeSpawnedEnvironmentInstance();
        ApplySpawnedEnvironmentDiscoveryState(markDiscovered);

        if (!wasSpawnedBefore)
            TileLifecycleEvents.RaiseSpawned(this);
        else
            TileLifecycleEvents.RaiseChanged(this);

        return true;
    }

    // -------------------------------------------------------------------------
    // Candidate builder (FILTERED) for a tile type
    // - only adds variants that pass neighbour-env filters
    // - strict adjacency: if groups exist, must match one
    // -------------------------------------------------------------------------
    private void BuildCandidates_ForTileType_Filtered(
        ClimateManager climateMgr,
        EnvironmentTileType desiredType,
        List<Candidate> outCandidates)
    {
        outCandidates.Clear();

        for (int o = 0; o < options.Length; o++)
        {
            var opt = options[o];
            if (opt == null) continue;
            if (opt.tileType != desiredType) continue;
            if (opt.variants == null || opt.variants.Length == 0) continue;

            bool hasGroups = opt.adjacencyGroups != null && opt.adjacencyGroups.Length > 0;

            // Collect neighbour envs for filtering
            List<TileScript> neighbours = null;
            AdjacencyGroup matchedGrp = null;

            if (!hasGroups)
            {
                neighbours = CollectNeighboursByCollider();
            }
            else
            {
                // First passing group wins
                for (int g = 0; g < opt.adjacencyGroups.Length; g++)
                {
                    var grp = opt.adjacencyGroups[g];
                    if (!GroupPasses(grp)) continue;
                    matchedGrp = grp;
                    break;
                }

                // If groups exist but none matched, this option does NOT participate in filtered mode
                if (matchedGrp == null)
                    continue;

                neighbours = CollectNeighboursForGroup(matchedGrp);
            }

            var neighbourEnvTypes = new List<EnvironmentType>(8);
            CollectNeighbourEnvironmentTypes(neighbours, climateMgr, neighbourEnvTypes);

            for (int v = 0; v < opt.variants.Length; v++)
            {
                var varnt = opt.variants[v];
                if (varnt == null || varnt.prefab == null) continue;

                if (PassNeighbourEnvFilter(varnt, neighbourEnvTypes))
                    outCandidates.Add(new Candidate { opt = opt, variant = varnt, grp = matchedGrp });
            }
        }
    }

    public bool HasEnvironmentVariant(EnvironmentTileType tileType, EnvironmentType env)
    {
        if (options == null) return false;

        for (int i = 0; i < options.Length; i++)
        {
            var opt = options[i];
            if (opt == null) continue;
            if (opt.tileType != tileType) continue;

            var vars = opt.variants;
            if (vars == null) continue;

            for (int v = 0; v < vars.Length; v++)
            {
                var varnt = vars[v];
                if (varnt == null || varnt.prefab == null) continue;

                if (varnt.environmentType == env)
                    return true;
            }
        }

        return false;
    }

    public bool TryForceSpawnSavedPrefab(
    string savedPrefabName,
    EnvironmentType desiredEnv,
    EnvironmentTileType desiredType,
    bool markDiscovered,
    float localYRotation)
    {
        if (string.IsNullOrWhiteSpace(savedPrefabName) || options == null || options.Length == 0)
            return false;

        string wantedName = savedPrefabName.Replace("(Clone)", "").Trim();

        EnvironmentTileVariant matchedVariant = null;

        for (int i = 0; i < options.Length; i++)
        {
            var opt = options[i];
            if (opt == null || opt.tileType != desiredType || opt.variants == null)
                continue;

            for (int j = 0; j < opt.variants.Length; j++)
            {
                var variant = opt.variants[j];
                if (variant == null || variant.prefab == null)
                    continue;

                string variantName = variant.prefab.name.Replace("(Clone)", "").Trim();

                if (variant.environmentType == desiredEnv && variantName == wantedName)
                {
                    matchedVariant = variant;
                    break;
                }
            }

            if (matchedVariant != null)
                break;
        }

        // Fallback: allow prefab-name-only match if exact env match failed.
        if (matchedVariant == null)
        {
            for (int i = 0; i < options.Length; i++)
            {
                var opt = options[i];
                if (opt == null || opt.tileType != desiredType || opt.variants == null)
                    continue;

                for (int j = 0; j < opt.variants.Length; j++)
                {
                    var variant = opt.variants[j];
                    if (variant == null || variant.prefab == null)
                        continue;

                    string variantName = variant.prefab.name.Replace("(Clone)", "").Trim();

                    if (variantName == wantedName)
                    {
                        matchedVariant = variant;
                        break;
                    }
                }

                if (matchedVariant != null)
                    break;
            }
        }

        if (matchedVariant == null)
            return false;

        bool wasSpawnedBefore = _hasSpawned;

        if (_spawnedInstance != null)
        {
            if (EnvironmentPoolManager.Instance != null)
                EnvironmentPoolManager.Instance.Release(_spawnedInstance);
            else
                Destroy(_spawnedInstance);

            _spawnedInstance = null;
        }

        _chosenTileType = desiredType;
        _chosenEnvironmentType = matchedVariant.environmentType;

        if (EnvironmentPoolManager.Instance != null)
        {
            _spawnedInstance = EnvironmentPoolManager.Instance.Get(
                matchedVariant.prefab,
                transform,
                transform.position,
                Quaternion.identity
            );
        }
        else
        {
            _spawnedInstance = Instantiate(
                matchedVariant.prefab,
                transform.position,
                Quaternion.identity,
                transform
            );
        }

        _spawnedInstance.transform.localRotation = Quaternion.Euler(0f, localYRotation, 0f);

        _hasSpawned = true; // before init/events

        InitializeSpawnedEnvironmentInstance();
        ApplySpawnedEnvironmentDiscoveryState(markDiscovered);

        if (!wasSpawnedBefore)
            TileLifecycleEvents.RaiseSpawned(this);
        else
            TileLifecycleEvents.RaiseChanged(this);

        return true;
    }

    private void InitializeSpawnedEnvironmentInstance()
    {
        if (_spawnedInstance == null)
            return;

        var env = _spawnedInstance.GetComponentInChildren<EnvironmentControl>(true);
        if (env == null)
            return;

        env.InitializeForTile(this);
        env.RebuildRuntimeUIState();
    }
}