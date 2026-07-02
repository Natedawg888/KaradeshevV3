using System.Collections.Generic;
using UnityEngine;

public partial class UnitGroupPanelControl : MonoBehaviour
{
    public static event System.Action OnMeleeTargetConfirmed;
    public static event System.Action OnSurroundTargetConfirmed;

    private void OpenMeleeTargetsPanel(MeleeAttackActionSO meleeAction)
    {
        if (_group == null || _owner == null || meleeAction == null)
            return;

        var originTile = ResolveOwnerTileForCombat();
        if (originTile == null)
        {
            //Debug.LogWarning("[UnitGroupPanel] OpenMeleeTargetsPanel: could not resolve owner/origin tile.");
            return;
        }

        var targetTile = ResolvePreferredMeleeTargetTile(meleeAction, originTile);
        if (targetTile == null)
        {
            //Debug.LogWarning("[UnitGroupPanel] OpenMeleeTargetsPanel: could not resolve a valid melee target tile.");
            return;
        }

        OpenCombatTargetsPanelForMode(
            isSurroundMode: false,
            buildEntries: () => BuildMeleeEntriesForTile(meleeAction, targetTile),
            onChosen: (entry) => BeginMeleeAttack(meleeAction, entry, targetTile));
    }

    private void OpenSurroundTargetsPanel(SurroundActionSO surroundAction)
    {
        if (_group == null || _owner == null || surroundAction == null)
            return;

        var originTile = ResolveOwnerTileForCombat();
        if (originTile == null)
        {
            //Debug.LogWarning("[UnitGroupPanel] OpenSurroundTargetsPanel: could not resolve owner/origin tile.");
            return;
        }

        if (!surroundAction.IsValidTarget(_group, originTile, originTile))
        {
            //Debug.LogWarning("[UnitGroupPanel] OpenSurroundTargetsPanel: origin tile is not valid for surround.");
            return;
        }

        OpenCombatTargetsPanelForMode(
            isSurroundMode: true,
            buildEntries: () => BuildSurroundEntriesForTile(originTile),
            onChosen: (entry) => BeginSurroundAction(surroundAction, entry, originTile));
    }

    private void OpenCombatTargetsPanelForMode(
        bool isSurroundMode,
        System.Func<List<MeleeTargetEntry>> buildEntries,
        System.Action<MeleeTargetEntry> onChosen)
    {
        if (meleeTargetsPanelRoot == null || meleeTargetsContentRoot == null || meleeTargetItemPrefab == null)
        {
            //Debug.LogWarning("[UnitGroupPanel] Combat targets UI not wired in inspector.");
            return;
        }

        if (actionPanelRoot) actionPanelRoot.SetActive(false);
        if (trackingResultsPanelRoot) trackingResultsPanelRoot.SetActive(false);
        if (scoutResultsPanelRoot) scoutResultsPanelRoot.SetActive(false);

        foreach (Transform child in meleeTargetsContentRoot)
            Destroy(child.gameObject);

        _meleeUiBuffer.Clear();

        var entries = buildEntries != null ? buildEntries.Invoke() : null;
        if (entries != null)
            _meleeUiBuffer.AddRange(entries);

        for (int i = 0; i < _meleeUiBuffer.Count; i++)
        {
            var capturedEntry = _meleeUiBuffer[i];

            var item = Instantiate(meleeTargetItemPrefab, meleeTargetsContentRoot);
            item.Setup(
                capturedEntry,
                () => onChosen?.Invoke(capturedEntry),
                isSurroundMode);
        }

        meleeTargetsPanelRoot.SetActive(true);
    }

    private List<MeleeTargetEntry> BuildMeleeEntriesForTile(MeleeAttackActionSO meleeAction, TileControl targetTile)
    {
        var results = new List<MeleeTargetEntry>();
        if (meleeAction == null || targetTile == null || _group == null)
            return results;

        var playerMgr = PlayerUnitManager.Instance;

        if (meleeAction.canTargetUnitGroups)
        {
            if (TryGetUnitControlForTileCombat(targetTile, out var unitCtrl) &&
                unitCtrl != null &&
                unitCtrl.Groups != null)
            {
                for (int i = 0; i < unitCtrl.Groups.Count; i++)
                {
                    var other = unitCtrl.Groups[i];
                    if (other == null || other.unitType == null) continue;
                    if (other == _group) continue;

                    if (playerMgr != null && playerMgr.IsPlayerUnitGroupId(other.groupId))
                        continue;

                    results.Add(BuildUnitTargetEntry(other));
                }
            }
        }

        if (meleeAction.canTargetAnimals)
        {
            var sim = AnimalSimulationAccess.Current;
            if (sim != null)
            {
                Vector2Int grid = targetTile.GetGridPosition();
                TileCoord coord = new TileCoord { x = grid.x, y = grid.y };

                _animalBuf.Clear();
                sim.CollectGroupsOnTile(coord, _animalBuf);

                for (int i = 0; i < _animalBuf.Count; i++)
                {
                    var ag = _animalBuf[i];
                    if (ag == null || !ag.isAlive || ag.species == null) continue;

                    results.Add(BuildAnimalTargetEntry(ag));
                }
            }
        }

        return results;
    }

    private List<MeleeTargetEntry> BuildSurroundEntriesForTile(TileControl targetTile)
    {
        var results = new List<MeleeTargetEntry>();
        if (targetTile == null || _group == null)
            return results;

        // Units still require a friendly melee engager on the tile
        HashSet<string> targetedUnitIds = new HashSet<string>();
        CollectFriendlyActiveMeleeTargetsOnTile(targetTile, targetedUnitIds, null);

        foreach (string unitId in targetedUnitIds)
        {
            if (TryBuildUnitEntryById(targetTile, unitId, out var entry))
                results.Add(entry);
        }

        // Any alive animal on the tile can be surrounded
        var sim = AnimalSimulationAccess.Current;
        if (sim != null)
        {
            Vector2Int grid = targetTile.GetGridPosition();
            TileCoord coord = new TileCoord { x = grid.x, y = grid.y };

            _animalBuf.Clear();
            sim.CollectGroupsOnTile(coord, _animalBuf);

            for (int i = 0; i < _animalBuf.Count; i++)
            {
                var ag = _animalBuf[i];
                if (ag == null || !ag.isAlive || ag.species == null) continue;

                results.Add(BuildAnimalTargetEntry(ag));
            }
        }

        return results;
    }

    private void CollectFriendlyActiveMeleeTargetsOnTile(
        TileControl tile,
        HashSet<string> targetedUnitIds,
        HashSet<int> targetedAnimalIds)
    {
        if (tile == null || _group == null)
            return;

        targetedUnitIds?.Clear();
        targetedAnimalIds?.Clear();

        var playerMgr = PlayerUnitManager.Instance;

        if (!TryGetUnitControlForTileCombat(tile, out var unitCtrl) ||
            unitCtrl == null ||
            unitCtrl.Groups == null)
        {
            return;
        }

        for (int i = 0; i < unitCtrl.Groups.Count; i++)
        {
            var other = unitCtrl.Groups[i];
            if (other == null) continue;
            if (other == _group) continue;

            if (playerMgr != null && !playerMgr.IsPlayerUnitGroupId(other.groupId))
                continue;

            if (!(other.activeAction is MeleeAttackActionSO))
                continue;

            if (other.remainingActionTurns <= 0)
                continue;

            TileControl otherTargetTile = other.activeActionTargetTile;
            if (otherTargetTile == null || otherTargetTile.GetGridPosition() != tile.GetGridPosition())
                continue;

            if (other.activeMeleeTargetType == MeleeTargetType.Unit &&
                !string.IsNullOrEmpty(other.activeMeleeTargetUnitGroupId))
            {
                targetedUnitIds?.Add(other.activeMeleeTargetUnitGroupId);
            }
            else if (other.activeMeleeTargetType == MeleeTargetType.Animal &&
                     other.activeMeleeTargetAnimalId >= 0)
            {
                targetedAnimalIds?.Add(other.activeMeleeTargetAnimalId);
            }
        }
    }

    private bool TryBuildUnitEntryById(TileControl tile, string unitGroupId, out MeleeTargetEntry entry)
    {
        entry = null;

        if (tile == null || string.IsNullOrEmpty(unitGroupId))
            return false;

        if (!TryGetUnitControlForTileCombat(tile, out var unitCtrl) ||
            unitCtrl == null ||
            unitCtrl.Groups == null)
        {
            return false;
        }

        for (int i = 0; i < unitCtrl.Groups.Count; i++)
        {
            var other = unitCtrl.Groups[i];
            if (other == null || other.unitType == null) continue;
            if (other.groupId != unitGroupId) continue;

            entry = BuildUnitTargetEntry(other);
            return true;
        }

        return false;
    }

    private bool TryBuildAnimalEntryById(TileControl tile, int animalGroupId, out MeleeTargetEntry entry)
    {
        entry = null;

        if (tile == null || animalGroupId < 0)
            return false;

        var sim = AnimalSimulationAccess.Current;
        if (sim == null)
            return false;

        if (!sim.TryGetGroup(animalGroupId, out var ag) ||
            ag == null ||
            !ag.isAlive ||
            ag.species == null)
        {
            return false;
        }

        Vector2Int grid = tile.GetGridPosition();
        if (ag.tile.x != grid.x || ag.tile.y != grid.y)
            return false;

        entry = BuildAnimalTargetEntry(ag);
        return true;
    }

    private MeleeTargetEntry BuildUnitTargetEntry(TileUnitGroupData other)
    {
        var u = other.unitType;

        return new MeleeTargetEntry
        {
            type = MeleeTargetType.Unit,
            displayName = !string.IsNullOrEmpty(other.groupName) ? other.groupName : u.unitName,
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
    }

    private MeleeTargetEntry BuildAnimalTargetEntry(AnimalGroupState ag)
    {
        return new MeleeTargetEntry
        {
            type = MeleeTargetType.Animal,
            displayName = ag.species.displayName,
            icon = ag.species.icon,
            count = ag.size,
            aggression = ag.species.aggression,
            flightiness = ag.species.flightiness,
            animalGroupId = ag.id
        };
    }

    private void BeginMeleeAttack(MeleeAttackActionSO meleeAction, MeleeTargetEntry entry, TileControl targetTile)
    {
        if (_group == null || _owner == null || meleeAction == null || entry == null)
            return;

        var originTile = ResolveOwnerTileForCombat();
        if (originTile == null)
        {
            //Debug.LogWarning("[UnitGroupPanel] BeginMeleeAttack: could not resolve owner/origin tile.");
            return;
        }

        if (targetTile == null)
            targetTile = originTile;

        if (!meleeAction.IsValidTarget(_group, originTile, targetTile))
        {
            //Debug.Log("[UnitGroupPanel] BeginMeleeAttack: target tile is not valid.");
            return;
        }

        UnitGroupActionManager.Instance?.ClearTrackedMeleeTargetMarker(_group);
        UnitGroupActionManager.Instance?.ClearTrackedSurroundTargetMarker(_group);

        _group.activeAction = meleeAction;
        _group.remainingActionTurns = Mathf.Max(1, meleeAction.durationTurns);
        _group.activeActionTargetTile = targetTile;
        _group.activeActionTargetGrid = targetTile.GetGridPosition();

        _group.ClearSurroundState();
        _group.ClearMeleeState();

        _group.activeMeleeTargetType = entry.type;

        if (entry.type == MeleeTargetType.Animal)
        {
            _group.activeMeleeTargetAnimalId = entry.animalGroupId;
            _group.activeMeleeTargetUnitGroupId = null;
        }
        else
        {
            _group.activeMeleeTargetAnimalId = -1;
            _group.activeMeleeTargetUnitGroupId = entry.unitGroupId;
        }

        UnitGroupActionManager.Instance?.SetTrackedMeleeTargetMarker(_group);
        _owner.RefreshMarker(_group);
        UnitGroupActionManager.RaiseGroupActionStateChanged(_group);

        OnMeleeTargetConfirmed?.Invoke();

        if (meleeTargetsPanelRoot) meleeTargetsPanelRoot.SetActive(false);
        CloseAllPanelsStayHere();
    }

    private void BeginSurroundAction(SurroundActionSO surroundAction, MeleeTargetEntry entry, TileControl targetTile)
    {
        if (_group == null || _owner == null || surroundAction == null || entry == null)
            return;

        var originTile = ResolveOwnerTileForCombat();
        if (originTile == null)
        {
            //Debug.LogWarning("[UnitGroupPanel] BeginSurroundAction: could not resolve owner/origin tile.");
            return;
        }

        if (targetTile == null)
            targetTile = originTile;

        if (!surroundAction.IsValidTarget(_group, originTile, targetTile))
        {
            //Debug.Log("[UnitGroupPanel] BeginSurroundAction: target tile is not valid.");
            return;
        }

        UnitGroupActionManager.Instance?.ClearTrackedMeleeTargetMarker(_group);
        UnitGroupActionManager.Instance?.ClearTrackedSurroundTargetMarker(_group);

        _group.activeAction = surroundAction;
        _group.remainingActionTurns = Mathf.Max(1, surroundAction.durationTurns);
        _group.activeActionTargetTile = targetTile;
        _group.activeActionTargetGrid = targetTile.GetGridPosition();

        _group.ClearMeleeState();
        _group.ClearSurroundState();

        _group.activeSurroundTargetType = entry.type;

        if (entry.type == MeleeTargetType.Animal)
        {
            _group.activeSurroundTargetAnimalId = entry.animalGroupId;
            _group.activeSurroundTargetUnitGroupId = null;
        }
        else
        {
            _group.activeSurroundTargetAnimalId = -1;
            _group.activeSurroundTargetUnitGroupId = entry.unitGroupId;
        }

        UnitGroupActionManager.Instance?.SetTrackedSurroundTargetMarker(_group);
        _owner.RefreshMarker(_group);
        UnitGroupActionManager.RaiseGroupActionStateChanged(_group);

        OnSurroundTargetConfirmed?.Invoke();

        if (meleeTargetsPanelRoot) meleeTargetsPanelRoot.SetActive(false);
        CloseAllPanelsStayHere();
    }

    private void SetupInCombatUI()
    {
        if (inCombatCloseButton != null)
        {
            inCombatCloseButton.onClick.RemoveAllListeners();
            inCombatCloseButton.onClick.AddListener(() =>
            {
                if (inCombatPanelRoot) inCombatPanelRoot.SetActive(false);
            });
        }

        if (stopAttackButton != null)
        {
            stopAttackButton.onClick.RemoveAllListeners();
            stopAttackButton.onClick.AddListener(StopCombatEarly);
        }

        if (retreatButton != null)
        {
            retreatButton.onClick.RemoveAllListeners();
            retreatButton.onClick.AddListener(RetreatFromCombat);
        }

        if (inCombatPanelRoot) inCombatPanelRoot.SetActive(false);
    }

    private bool GroupIsInCombat()
    {
        return _group != null &&
               (_group.activeAction is MeleeAttackActionSO ||
                _group.activeAction is RangedAttackActionSO ||
                _group.activeAction is SurroundActionSO) &&
               _group.remainingActionTurns > 0;
    }

    private void ToggleInCombatPanel()
    {
        if (inCombatPanelRoot == null)
            return;

        bool next = !inCombatPanelRoot.activeSelf;
        inCombatPanelRoot.SetActive(next);

        if (next)
            RefreshInCombatPanel();
    }

    private void RefreshInCombatPanel()
    {
        if (!GroupIsInCombat() || inCombatPanelRoot == null)
            return;

        EnsureInCombatDisplays();

        var attackerEntry = BuildAttackerEntry();
        if (_attackerDisplay != null)
            _attackerDisplay.Setup(attackerEntry);

        var targetEntry = BuildActiveCombatTargetEntry();
        if (_targetDisplay != null)
            _targetDisplay.Setup(targetEntry);

        bool canDisengage =
            _group != null &&
            (_group.activeAction is SurroundActionSO || _group.meleeRetaliatedLastTick);

        if (stopAttackButton) stopAttackButton.interactable = canDisengage;
        if (retreatButton) retreatButton.interactable = canDisengage;
    }

    private TileControl ResolveActiveTargetTile()
    {
        if (_group == null)
            return null;

        if (_group.activeActionTargetTile != null)
            return _group.activeActionTargetTile;

        var grid = _group.activeActionTargetGrid;
        if (grid == Vector2Int.zero)
            return null;

        if (UnitGroupActionManager.Instance != null)
            return UnitGroupActionManager.Instance.FindTileByGridPosition_SLOW(grid);

        var allTiles = GameObject.FindObjectsOfType<TileControl>();
        for (int i = 0; i < allTiles.Length; i++)
        {
            var t = allTiles[i];
            if (t != null && t.GetGridPosition() == grid)
                return t;
        }

        return null;
    }

    private MeleeTargetEntry BuildActiveCombatTargetEntry()
    {
        if (_group == null)
            return null;

        var targetTile = ResolveActiveTargetTile();
        if (targetTile == null)
            return null;

        bool isSurround = _group.activeAction is SurroundActionSO;

        MeleeTargetType targetType = isSurround
            ? _group.activeSurroundTargetType
            : _group.activeMeleeTargetType;

        string unitGroupId = isSurround
            ? _group.activeSurroundTargetUnitGroupId
            : _group.activeMeleeTargetUnitGroupId;

        int animalGroupId = isSurround
            ? _group.activeSurroundTargetAnimalId
            : _group.activeMeleeTargetAnimalId;

        if (targetType == MeleeTargetType.Unit && !string.IsNullOrEmpty(unitGroupId))
        {
            if (TryBuildUnitEntryById(targetTile, unitGroupId, out var unitEntry))
                return unitEntry;
        }

        if (targetType == MeleeTargetType.Animal && animalGroupId >= 0)
        {
            if (TryBuildAnimalEntryById(targetTile, animalGroupId, out var animalEntry))
                return animalEntry;
        }

        return null;
    }

    private void StopCombatEarly()
    {
        if (!GroupIsInCombat())
            return;

        if (_group.activeAction is SurroundActionSO)
            UnitGroupActionManager.Instance?.ClearTrackedSurroundTargetMarker(_group);
        else if (_group.activeAction is MeleeAttackActionSO)
            UnitGroupActionManager.Instance?.ClearTrackedMeleeTargetMarker(_group);

        _group.remainingActionTurns = 0;
        _group.activeAction = null;
        _group.activeActionTargetTile = null;
        _group.activeActionTargetGrid = Vector2Int.zero;

        _group.ClearMeleeState();
        _group.ClearSurroundState();

        _owner?.RefreshMarker(_group);
        UnitGroupActionManager.RaiseGroupActionStateChanged(_group);

        if (inCombatPanelRoot) inCombatPanelRoot.SetActive(false);

        Refresh();
    }

    private void RetreatFromCombat()
    {
        if (!GroupIsInCombat())
            return;

        var g = _group;
        var o = _owner;

        bool wasSurround = g != null && g.activeAction is SurroundActionSO;
        bool againstUnit = g != null &&
                           ((wasSurround && g.activeSurroundTargetType == MeleeTargetType.Unit) ||
                            (!wasSurround && g.activeMeleeTargetType == MeleeTargetType.Unit));

        bool againstAnimal = g != null &&
                             ((wasSurround && g.activeSurroundTargetType == MeleeTargetType.Animal) ||
                              (!wasSurround && g.activeMeleeTargetType == MeleeTargetType.Animal));

        bool afterRetaliation = g != null && g.meleeRetaliatedLastTick;

        PlayerReligionManager.Instance?.NotifyUnitRetreatedFromCombat(
            g,
            againstUnit,
            againstAnimal,
            afterRetaliation,
            wasSurround);

        StopCombatEarly();

        if (UnitGroupMovementManager.Instance != null && g != null && o != null)
        {
            UnitGroupMovementManager.Instance.BeginMovementForGroup(
                g,
                o,
                turnCostMultiplier: 0.2f,
                minTurnCost: 1f);
        }
    }

    private void EnsureInCombatDisplays()
    {
        if (_attackerDisplay == null && inCombatTargetDisplayPrefab != null && inCombatAttackerRoot != null)
            _attackerDisplay = Instantiate(inCombatTargetDisplayPrefab, inCombatAttackerRoot);

        if (_targetDisplay == null && inCombatTargetDisplayPrefab != null && inCombatTargetRoot != null)
            _targetDisplay = Instantiate(inCombatTargetDisplayPrefab, inCombatTargetRoot);
    }

    private MeleeTargetEntry BuildAttackerEntry()
    {
        if (_group == null || _group.unitType == null)
            return null;

        var u = _group.unitType;

        return new MeleeTargetEntry
        {
            type = MeleeTargetType.Unit,
            displayName = !string.IsNullOrEmpty(_group.groupName) ? _group.groupName : u.unitName,
            icon = u.unitIcon,
            count = _group.unitCount,

            unitGroupId = _group.groupId,

            movementSpeed = u.movementSpeed + _group.bonusMovementSpeed,
            power = u.power + _group.bonusPower,
            defense = u.defense + _group.bonusDefense,
            agility = u.agility + _group.bonusAgility,
            accuracy = u.accuracy + _group.bonusAccuracy,
            range = u.range + _group.bonusRange,
            stealth = u.stealth + _group.bonusStealth,

            currentHealth = _group.currentHealth,
            maxHealth = _group.maxHealth
        };
    }

    private TileControl ResolveOwnerTileForCombat()
    {
        if (_owner == null)
            return null;

        if (UnitGroupActionManager.Instance != null)
        {
            var resolved = UnitGroupActionManager.Instance.ResolveTileForUnitControl(_owner);
            if (resolved != null)
                return resolved;
        }

        var tile = _owner.GetComponent<TileControl>();
        if (tile != null)
            return tile;

        tile = _owner.GetComponentInParent<TileControl>(true);
        if (tile != null)
            return tile;

        tile = _owner.GetComponentInChildren<TileControl>(true);
        if (tile != null)
            return tile;

        if (_owner.transform.parent != null)
        {
            tile = _owner.transform.parent.GetComponentInChildren<TileControl>(true);
            if (tile != null)
                return tile;
        }

        return null;
    }

    private TileControl ResolvePreferredMeleeTargetTile(MeleeAttackActionSO meleeAction, TileControl originTile)
    {
        if (meleeAction == null || originTile == null || _group == null)
            return null;

        var selectedTile = TileInteraction.SelectedTile;
        if (selectedTile != null && meleeAction.IsValidTarget(_group, originTile, selectedTile))
            return selectedTile;

        if (meleeAction.IsValidTarget(_group, originTile, originTile))
            return originTile;

        return null;
    }

    private bool TryGetUnitControlForTileCombat(TileControl tile, out TileUnitGroupControl unitCtrl)
    {
        unitCtrl = null;

        if (tile == null)
            return false;

        if (UnitGroupActionManager.Instance != null &&
            UnitGroupActionManager.Instance.TryGetUnitControlForTile(tile, out unitCtrl) &&
            unitCtrl != null)
        {
            return true;
        }

        unitCtrl = tile.GetComponent<TileUnitGroupControl>();
        if (unitCtrl != null)
            return true;

        unitCtrl = tile.GetComponentInChildren<TileUnitGroupControl>(true);
        if (unitCtrl != null)
            return true;

        if (tile.transform.parent != null)
        {
            unitCtrl = tile.transform.parent.GetComponentInChildren<TileUnitGroupControl>(true);
            if (unitCtrl != null)
                return true;
        }

        return false;
    }
}
