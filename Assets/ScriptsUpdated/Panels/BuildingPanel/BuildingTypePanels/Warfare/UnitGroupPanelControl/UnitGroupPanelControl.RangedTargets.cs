using System.Collections.Generic;
using UnityEngine;

public partial class UnitGroupPanelControl : MonoBehaviour
{
    private struct RangedCandidate
    {
        public MeleeTargetEntry entry;
        public TileControl tile;
        public Vector2Int grid;
    }

    private readonly List<TileControl> _rangedTilesBuf = new(256);
    private readonly List<RangedCandidate> _rangedCandidates = new(256);

    private void OpenRangedTargetsPanel(RangedAttackActionSO rangedAction)
    {
        if (_group == null || _owner == null || rangedAction == null)
            return;

        if (meleeTargetsPanelRoot == null || meleeTargetsContentRoot == null || meleeTargetItemPrefab == null)
        {
            //Debug.LogWarning("[UnitGroupPanel] Ranged targets UI not wired (reuses melee target UI).");
            return;
        }

        // close other panels
        if (actionPanelRoot) actionPanelRoot.SetActive(false);
        if (trackingResultsPanelRoot) trackingResultsPanelRoot.SetActive(false);
        if (scoutResultsPanelRoot) scoutResultsPanelRoot.SetActive(false);

        foreach (Transform child in meleeTargetsContentRoot)
            Destroy(child.gameObject);

        _rangedTilesBuf.Clear();
        _rangedCandidates.Clear();

        var originTile = _owner.GetComponentInParent<TileControl>();
        if (originTile == null || _group.unitType == null)
            return;

        int maxRange = rangedAction.GetEffectiveTileRange(_group);

        if (UnitGroupActionManager.Instance == null)
        {
            //Debug.LogWarning("[UnitGroupPanel] No UnitGroupActionManager in scene.");
            return;
        }

        UnitGroupActionManager.Instance.CollectTilesInRangeBFS(originTile, maxRange, _rangedTilesBuf, includeOrigin: true);

        var sim = AnimalSimulationAccess.Current;

        for (int ti = 0; ti < _rangedTilesBuf.Count; ti++)
        {
            var tile = _rangedTilesBuf[ti];
            if (tile == null) continue;

            var grid = tile.GetGridPosition();

            // ---- Units on this tile ----
            if (rangedAction.canTargetUnitGroups)
            {
                var unitCtrl = tile.GetComponentInChildren<TileUnitGroupControl>();
                if (unitCtrl != null && unitCtrl.Groups != null)
                {
                    for (int i = 0; i < unitCtrl.Groups.Count; i++)
                    {
                        var other = unitCtrl.Groups[i];
                        if (other == null || other.unitType == null) continue;
                        if (other == _group) continue;

                        // ✅ NEW: don't show player-owned unit groups as targets
                        if (IsPlayerOwnedUnitGroup(other))
                            continue;

                        var u = other.unitType;

                        var entry = new MeleeTargetEntry
                        {
                            type = MeleeTargetType.Unit,
                            displayName = $"{(!string.IsNullOrEmpty(other.groupName) ? other.groupName : u.unitName)} ({grid.x},{grid.y})",
                            icon = u.unitIcon,
                            count = other.unitCount,

                            unitGroupId = other.groupId,

                            movementSpeed = u.movementSpeed + other.bonusMovementSpeed,
                            power = u.power + other.bonusPower,
                            defense = u.defense + other.bonusDefense,
                            agility = u.agility + other.bonusAgility,
                            accuracy = u.accuracy + other.bonusAccuracy,
                            range = u.range + other.bonusRange,
                            stealth = u.stealth + other.bonusStealth,

                            currentHealth = other.currentHealth,
                            maxHealth = other.maxHealth,
                        };

                        _rangedCandidates.Add(new RangedCandidate { entry = entry, tile = tile, grid = grid });
                    }
                }
            }

            // ---- Animals on this tile ----
            if (rangedAction.canTargetAnimals && sim != null)
            {
                // ✅ NEW: don’t show animals if the tile is undiscovered
                if (!IsTileDiscovered(tile))
                    continue;

                TileCoord coord = new TileCoord { x = grid.x, y = grid.y };

                _animalBuf.Clear();
                sim.CollectGroupsOnTile(coord, _animalBuf);

                for (int i = 0; i < _animalBuf.Count; i++)
                {
                    var ag = _animalBuf[i];
                    if (ag == null || !ag.isAlive || ag.species == null) continue;

                    var entry = new MeleeTargetEntry
                    {
                        type = MeleeTargetType.Animal,
                        displayName = $"{ag.species.displayName} ({grid.x},{grid.y})",
                        icon = ag.species.icon,
                        count = ag.size,
                        aggression = ag.species.aggression,
                        flightiness = ag.species.flightiness,
                        animalGroupId = ag.id
                    };

                    _rangedCandidates.Add(new RangedCandidate { entry = entry, tile = tile, grid = grid });
                }
            }
        }

        if (_rangedCandidates.Count == 0)
        {
            //Debug.Log("[UnitGroupPanel] No ranged targets found in range.");
            return;
        }

        // spawn UI
        for (int i = 0; i < _rangedCandidates.Count; i++)
        {
            var cand = _rangedCandidates[i];
            var item = Instantiate(meleeTargetItemPrefab, meleeTargetsContentRoot);

            item.Setup(cand.entry, () =>
            {
                BeginRangedAttack(rangedAction, cand);
            });
        }

        meleeTargetsPanelRoot.SetActive(true);
    }

    private void BeginRangedAttack(RangedAttackActionSO rangedAction, RangedCandidate cand)
    {
        if (_group == null || _owner == null || rangedAction == null)
            return;

        if (cand.tile == null)
            return;

        _group.activeAction = rangedAction;
        _group.remainingActionTurns = Mathf.Max(1, rangedAction.durationTurns);
        _group.activeActionTargetTile = cand.tile;
        _group.activeActionTargetGrid = cand.grid;

        // Reuse your existing "activeMeleeTarget*" fields as the combat target payload
        _group.activeMeleeTargetType = cand.entry.type;

        if (cand.entry.type == MeleeTargetType.Animal)
        {
            _group.activeMeleeTargetAnimalId = cand.entry.animalGroupId;
            _group.activeMeleeTargetUnitGroupId = null;
        }
        else
        {
            _group.activeMeleeTargetAnimalId = -1;
            _group.activeMeleeTargetUnitGroupId = cand.entry.unitGroupId;
        }

        if (meleeTargetsPanelRoot) meleeTargetsPanelRoot.SetActive(false);
        CloseAllPanelsStayHere();
    }

    private bool IsTileDiscovered(TileControl tile)
    {
        if (tile == null) return false;

        // Most common: EnvironmentStatus lives on the tile root or under it
        var status = tile.GetComponentInChildren<EnvironmentStatus>(true) 
                    ?? tile.GetComponentInParent<EnvironmentStatus>();

        // If there is no status component, assume discovered so you don’t hard-break maps.
        if (status == null) return true;

        return status.IsDiscovered;
    }
}
