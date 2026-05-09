# Resource Spawner System — Full Reference

**Created:** May 9, 2026  
**Project:** Kardeshev 0.3  
**Related audit:** See `SCRIPT_AUDIT.md` §12 for the one-paragraph summary and changelog entry.

---

## 1. Why This System Exists

The old `EnvironmentResourceNode` spawned resources because a `ResourceDefinition` had that environment type in its `allowedEnvironmentTypes` list. This meant every tile type was responsible for knowing about every possible resource — a flat, hard-to-extend design.

**The new design flips responsibility:**  
A `ResourceSpawnerDefinition` ScriptableObject knows what it produces, when, and under what conditions. The tile just holds a list of spawners. Resources appear because a *source* exists on the tile, not because the environment type matches a filter inside the resource definition.

```
OLD:  ResourceDefinition.IsAvailableIn(env, tile, season) → spawn it
NEW:  ResourceSpawnerDefinition → outputs → conditions → climate → spawn it
```

`spawnedResources` (the list downstream gathering, survey, and save systems read) is **unchanged**.

---

## 2. File Map

```
ScriptsUpdated/Environment/ResourceSpawners/
  ResourceSpawnerDefinition.cs          Core SO + all serializable data classes
  ResourceSpawnerRuntime.cs             Per-tile runtime state + enums
  TileStateResourceSpawnerHandler.cs    MonoBehaviour — fire events → burnt spawners
  AnimalDeathResourceSpawnerHandler.cs  MonoBehaviour — animal death → carcass spawners
  AnimalDroppingHandler.cs              MonoBehaviour — animal presence → dung + drying

ScriptsUpdated/Environment/EnvironmentResourceNode/
  EnvironmentResourceNode.SpawnerAPI.cs  Partial class — all public spawner API methods
  EnvironmentResourceNode.Core.cs        +3 fields, +InitializeSpawners() call in Start()
  EnvironmentResourceNode.Spawning.cs    GenerateResources() + TickResourceLifecycle() routing

Assets/Editor/
  ResourceSpawnerDefinitionCreator.cs   Menu: generates 30+ base spawner SOs
  SavannaSpawnerCreator.cs              Menu: generates 9 Savanna SOs with climate curves

Assets/Resources/ResourceSpawners/
  Forest/     Grassland/     Coastal/     Terrain/
  BurntRemains/    AnimalRemains/    Weather/    AnimalDroppings/

Assets/ScriptableObjects/ResourceSpawners/
  Savanna/    (9 biome-specific SOs with climate curves)
```

---

## 3. Class Reference

### `ResourceSpawnerDefinition` (ScriptableObject)

Create via: **right-click → Resources/ResourceSpawnerDefinition**

| Field | Type | Purpose |
|-------|------|---------|
| `spawnerID` | string | Unique key used by `HasSpawner()` and `RemoveSpawner()` |
| `displayName` | string | Shown in debug logs and Inspector |
| `category` | SpawnerCategory | Semantic tag (Plant, BurntRemains, AnimalRemains, etc.) |
| `outputs` | List\<ResourceSpawnerOutput\> | Resources produced each spawn cycle |
| `baseSpawnChance` | float 0–1 | Probability the spawner fires at all each cycle |
| `spawnIntervalTurns` | int | Turns between spawn attempts |
| `isPermanent` | bool | Never expires on its own if true |
| `canExpire` | bool | Expires via `maxUses` or `lifetimeTurns` if true |
| `maxUses` | int | Max cycles before expiry (0 = unlimited) |
| `lifetimeTurns` | int | Max turns active before expiry (0 = unlimited) |
| `climate` | ResourceSpawnerClimateSettings | Optional climate multiplier curves |
| `conditions` | ResourceSpawnerConditionSettings | Env / tile / season / tile-state filters |
| `debugNotes` | string [TextArea] | Designer intent, climate notes, trigger source |

**SpawnerCategory enum:**

| Value | Intended Use |
|-------|-------------|
| `Plant` | Berry bushes, herbs, ferns, grass |
| `Bush` | Shrubs, berry shrubs |
| `Tree` | Fallen branches, tree nests |
| `Root` | Tuber patches, root beds |
| `GroundMaterial` | Dry grass, insect mounds |
| `StoneDeposit` | Rock scatters, cave minerals |
| `WaterCoastal` | Shellfish, seaweed, river source |
| `AnimalRemains` | Carcass spawners |
| `BurntRemains` | Ember, charcoal, ash |
| `WeatherCreated` | Wet soil mushrooms, flood debris |
| `EnvironmentBackground` | General tile background spawners |

---

### `ResourceSpawnerOutput`

One entry in a spawner's output list.

| Field | Type | Notes |
|-------|------|-------|
| `resource` | ResourceDefinition | The resource produced |
| `minAmount` | int ≥1 | Minimum units per trigger |
| `maxAmount` | int ≥1 | Maximum units per trigger |
| `weight` | float 0–1 | Relative weight among outputs (higher = chosen more often) |
| `chance` | float 0–1 | Independent per-output roll each cycle |
| `addToExistingStack` | bool | Add to an existing entry rather than creating a new `ResourceSpawnEntry` |

**Evaluation order per output:**
```
1. Roll Random.value vs output.chance           → skip if fails
2. amount = Random.Range(minAmount, maxAmount+1)
3. Clamp to remaining tile capacity             → skip if capacity = 0
4. AddResourceToNode(resource, amount, addToExistingStack)
```

---

### `ResourceSpawnerClimateSettings`

Optional climate multiplier applied to `baseSpawnChance`.

```
effectiveChance = baseSpawnChance × clamp(tempMult × humMult, 0, 3)
```

| Field | Type | Notes |
|-------|------|-------|
| `enabled` | bool | If false, returns multiplier 1.0 unconditionally |
| `temperatureCurve` | AnimationCurve | X = °C, Y = multiplier (design range 0..2) |
| `humidityCurve` | AnimationCurve | X = 0..1, Y = multiplier (design range 0..2) |

**Data source:** `ClimateManager.TryGetClimateAtWorldPos(transform.position)` — sampled **once per tile per turn tick**, not per spawner. Safe fallback to 1.0 if `ClimateManager.Instance` is null or the cell has no valid climate data yet.

**Savanna curve examples (from SavannaSpawnerCreator.cs):**

| Spawner | Hot bonus | Cold penalty | Wet penalty | Dry bonus |
|---------|-----------|-------------|------------|-----------|
| DryGrassPatch | +40% >35°C | −80% <10°C | −90% hum>0.65 | +40% hum<0.3 |
| RootPatch | none | −70% <5°C | +30% bonus | −80% <0.2 |
| BerryShrub | −40% >38°C | −90% <5°C | slight bonus | −90% <0.2 |
| InsectMound | +30% >35°C | −90% <5°C | −80% >0.9 | none |
| FallenBranches | slight +10% | −40% <0°C | −20% >0.7 | +10% |
| StoneScatter | disabled | | | |
| MedicinalHerbs | −70% >42°C | −70% <10°C | +40% >0.65 | −90% <0.2 |

---

### `ResourceSpawnerConditionSettings`

All enabled conditions must pass before a spawn cycle runs.

| Field | Type | Notes |
|-------|------|-------|
| `requiredEnvironmentTypes` | List\<EnvironmentType\> | Empty = any environment |
| `requiredTileTypes` | List\<EnvironmentTileType\> | Empty = any tile type |
| `requiredSeasonIDs` | List\<string\> | Empty = any season. Normalized (lowercased, stripped of separators). |
| `requiresHasBeenIgnited` | bool | Needs `TileStateFlags.HasBeenIgnited` |
| `requiresIsCurrentlyWet` | bool | Needs `TileStateFlags.IsCurrentlyWet` |
| `requiresWasRecentlyFlooded` | bool | Needs `TileStateFlags.WasRecentlyFlooded` |
| `requiresHasCarcass` | bool | Needs `TileStateFlags.HasCarcass` |
| `requiresHasVolcanicAsh` | bool | Needs `TileStateFlags.HasVolcanicAsh` |

---

### `ResourceSpawnerRuntime`

Stored in `EnvironmentResourceNode.activeSpawners` (private, `[SerializeField, HideInInspector]`).

| Field | Type | Notes |
|-------|------|-------|
| `definition` | ResourceSpawnerDefinition | SO this wraps |
| `isActive` | bool | If false, skipped entirely |
| `sourceReason` | SpawnerSourceReason | Used for `RemoveSpawnersByReason()` batch removal |
| `turnsSinceLastSpawn` | int | Resets to 0 after a spawn attempt |
| `remainingUses` | int | Counts down per cycle. −1 = unlimited |
| `remainingLifetimeTurns` | int | Counts down every turn. −1 = unlimited |

**`IsExpired()` logic:**
```
if !definition.canExpire       → false (never expires)
if maxUses > 0 && uses ≤ 0    → true
if lifetimeTurns > 0 && life ≤ 0 → true
```

**SpawnerSourceReason enum:**

| Value | Set by |
|-------|--------|
| `BaseEnvironment` | `InitializeSpawners()` from `baseSpawners` list |
| `BurntTile` | `TileStateResourceSpawnerHandler` on fire events |
| `AnimalDeath` | `AnimalDeathResourceSpawnerHandler.OnAnimalDied` |
| `WeatherCreated` | Weather systems (future) |
| `PlayerAction` | Player-built structures or actions (future) |
| `AnimalPresence` | `AnimalDroppingHandler.OnAnimalEnteredTile` |

**TileStateFlags (bitfield):**

| Flag | Bit | Set by |
|------|-----|--------|
| `HasBeenIgnited` | 0 | `TileStateResourceSpawnerHandler.HandleIgnited()` |
| `IsCurrentlyWet` | 1 | Weather system (not yet wired) |
| `WasRecentlyFlooded` | 2 | Flood system (not yet wired) |
| `HasCarcass` | 3 | `AnimalDeathResourceSpawnerHandler.HandleAnimalDied()` |
| `HasVolcanicAsh` | 4 | Volcano system (not yet wired) |
| `HasFreshDung` | 5 | `AnimalDroppingHandler.HandleAnimalEntered()` |
| `HasActiveAnimal` | 6 | `AnimalDroppingHandler.HandleAnimalEntered()` |

---

## 4. EnvironmentResourceNode Integration

### New fields (`Core.cs`)

```csharp
[Header("Spawner-Based Resources")]
public List<ResourceSpawnerDefinition> baseSpawners = new();  // designer-assigned SOs

[SerializeField, HideInInspector]
private List<ResourceSpawnerRuntime> activeSpawners = new(); // runtime instances

[HideInInspector]
public TileStateFlags currentTileState = TileStateFlags.None;
```

`Start()` calls `InitializeSpawners()` after capacity and health setup.

### Routing in `Spawning.cs`

```
GenerateResources():
  if baseSpawners.Count > 0
    → RunSpawnersNow()          (initial population on new game)
    → return
  else
    → Debug.LogWarning (migration notice)
    → old resourceDefinitions path

TickResourceLifecycle() [extra-spawn block]:
  if activeSpawners.Count > 0
    → TickSpawners()            (per-turn regeneration)
  else
    → TryExtraSpawn()           (legacy path — backward compat)
```

### Public API (from `SpawnerAPI.cs` partial)

```csharp
// Add spawners
AddSpawner(def, reason)
AddTemporarySpawner(def, lifetimeTurns, reason, runImmediately = false)

// Remove spawners
RemoveSpawner(spawnerID)
RemoveSpawnersByReason(reason)

// Query
HasSpawner(spawnerID) → bool

// Resources
AddResource(def, amount)              // respects tile capacity

// Tile state
SetTileState(TileStateFlags, bool)
HasTileState(TileStateFlags) → bool

// Lifecycle (called internally)
InitializeSpawners()                  // called from Start()
RunSpawnersNow()                      // called from GenerateResources()
TickSpawners()                        // called from TickResourceLifecycle()
```

---

## 5. Three Spawner Types

### Type 1 — Permanent Base Spawners

- Configured in `EnvironmentResourceNode.baseSpawners` in the Inspector
- Initialized by `InitializeSpawners()` in `Start()`
- Condition-filtered at init and re-checked each tick
- Survive as long as the tile exists (unless `canExpire = true`)
- `sourceReason = BaseEnvironment`

**Examples:** Berry Bush, Dry Grass Patch, Shellfish Bed, Mountain Rock Outcrop

### Type 2 — Event-Based Temporary Spawners

Added at runtime by handler MonoBehaviours reacting to events. Auto-removed by `TickSpawners()` when `IsExpired()` returns true.

| Handler | Trigger | Spawners Created | Lifetime |
|---------|---------|-----------------|----------|
| `TileStateResourceSpawnerHandler` | `OnIgnited` | Ember Source | 3 turns |
| `TileStateResourceSpawnerHandler` | `OnExtinguished` | Charcoal Deposit + Ash Deposit | 8t / 15t |
| `AnimalDeathResourceSpawnerHandler` | `OnAnimalDied` static event | Carcass Remains (small/med/large/bird) | 4–7 turns |
| `AnimalDroppingHandler` | `OnAnimalEnteredTile` static event | Animal Dropping (managed externally) | — |

### Type 3 — State-Conditional Permanent Spawners

Permanent spawners that only fire when a `TileStateFlags` is set. Same as Type 1 but the conditions gate on tile state rather than just environment type.

**Examples:**
- `WetSoilMushroomSpawner` — `requiresIsCurrentlyWet = true`
- `FloodDebrisSpawner` — `requiresWasRecentlyFlooded = true`

---

## 6. Dung → Dried Dung System

`AnimalDroppingHandler` (attach to same GameObject as `EnvironmentResourceNode`).

```
Animal arrives:
  OnAnimalEnteredTile fires → activeAnimalCount++
  If spawner not already active → AddSpawner(dungDropSpawner, AnimalPresence)
  SetTileState(HasFreshDung, true) + SetTileState(HasActiveAnimal, true)
  Reset drying timer to 0

Animal leaves, count hits 0:
  RemoveSpawner(dungDropSpawner.spawnerID)
  SetTileState(HasActiveAnimal, false)
  If Dung exists on tile → start drying timer

Each turn (TurnSystem.onTurnEnd subscription):
  If animals present → reset timer to 0 (fresh dung keeps coming)
  Else increment turnsSinceLastDeposit
  When ≥ dungDryingTurns:
    node.Consume(dungDefinition, all)
    node.AddResource(driedDungDefinition, same amount)
    SetTileState(HasFreshDung, false)
    Reset timer to -1
```

**Inspector fields:**

| Field | Type | Default | Notes |
|-------|------|---------|-------|
| `dungDropSpawner` | ResourceSpawnerDefinition SO | — | Choose species weight: Animal/HeavyGrazer/SmallAnimal |
| `dungDryingTurns` | int | 4 | Turns after last animal before conversion |
| `dungDefinition` | ResourceDefinition | — | Dung (resourceID: DNG) |
| `driedDungDefinition` | ResourceDefinition | — | Dried Dung (resourceID: DDNG) |

**Call sites in animal simulation (not yet wired):**
```csharp
AnimalDroppingHandler.OnAnimalEnteredTile?.Invoke(
    new AnimalTileRequest { targetNode = node, speciesID = group.species?.speciesID });

AnimalDroppingHandler.OnAnimalLeftTile?.Invoke(
    new AnimalTileRequest { targetNode = node, speciesID = group.species?.speciesID });
```

---

## 7. Event Handler Wiring

### `AnimalDeathResourceSpawnerHandler`

Attach to same GameObject as `EnvironmentResourceNode`.

```csharp
// Fire from AnimalSimulation when a group dies:
AnimalDeathResourceSpawnerHandler.OnAnimalDied?.Invoke(new AnimalDeathSpawnRequest
{
    targetNode = node,        // EnvironmentResourceNode on the tile
    speciesID  = group.species?.speciesID ?? "",
    groupSize  = deaths
});
```

Handler matches `targetNode` → picks species-specific spawner or `defaultRemainsSpawner` → calls `AddTemporarySpawner(..., runImmediately: true)` → sets `TileStateFlags.HasCarcass`.

**Inspector:**
- `defaultRemainsSpawner` — fallback SO (e.g. `MediumAnimalRemainsSpawner`)
- `speciesSpawners` — list of `(speciesID string, spawner SO)` for per-species overrides
- `carcassLifetimeTurns` — default 5

### `TileStateResourceSpawnerHandler`

Attach to same GameObject as `EnvironmentFireState` and `EnvironmentResourceNode`. Subscribes to `EnvironmentFireState.OnIgnited` / `OnExtinguished` automatically in `OnEnable/OnDisable`.

**Inspector:**
- `emberSpawner` — assign `EmberSpawner` SO
- `charcoalSpawner` — assign `CharcoalDepositSpawner` SO
- `ashSpawner` — assign `AshDepositSpawner` SO
- `emberLifetimeTurns` — default 3
- `charcoalLifetimeTurns` — default 8
- `ashLifetimeTurns` — default 15

---

## 8. Editor Generator Scripts

### `ResourceSpawnerDefinitionCreator.cs`

**Menus:**
- **Tools → Kardeshev → Create Resource Spawner Definitions** — creates 30 general biome SOs
- **Tools → Kardeshev → Create Dung Spawner Definitions** — creates 3 dung SOs

**Output:** `Assets/Resources/ResourceSpawners/`

| Sub-folder | Contents |
|-----------|---------|
| `Forest/` | BerryBush, MushroomPatch, FallenBranch, ForestGroundCover, TreeNest, ForestInsects |
| `Grassland/` | DryGrass, SeedPlant, GrassInsects, TuberRoot |
| `Coastal/` | ShellfishBed, SeaweedPatch, Driftwood, RiverSource |
| `Terrain/` | MountainRock, MountainHerb, DesertPlant, TundraGround, CaveMineral, CaveMushroom, SavannaTree, VolcanicMineral |
| `BurntRemains/` | EmberSpawner (3t), CharcoalDepositSpawner (8t), AshDepositSpawner (15t) |
| `AnimalRemains/` | Small (4t), Medium (5t), Large (7t), Bird (4t) |
| `Weather/` | WetSoilMushrooms (6t), FloodDebris (4t) |
| `AnimalDroppings/` | AnimalDropping, HeavyGrazerDropping, SmallAnimalDropping |

All SOs use `AssetDatabase.CreateAsset` — **safe to re-run** (overwrites existing).

### `SavannaSpawnerCreator.cs`

**Menu:** **Tools → Kardeshev → Create Savanna Spawner Definitions**

**Output:** `Assets/ScriptableObjects/ResourceSpawners/Savanna/`

| Asset | Category | Permanent | Climate | Interval |
|-------|----------|-----------|---------|----------|
| `RS_SavannaDryGrassPatch` | GroundMaterial | ✓ | Hot+dry bonus | 2t |
| `RS_SavannaRootPatch` | Root | ✓ | Moisture bonus | 4t |
| `RS_SavannaBerryShrub` | Bush | ✓ | Strongly penalised when dry | 5t |
| `RS_SavannaInsectMound` | GroundMaterial | ✓ | Hot bonus, cold/flood penalty | 3t |
| `RS_SavannaFallenBranches` | Tree | ✓ | Dry-tolerant | 6t |
| `RS_SavannaStoneScatter` | StoneDeposit | ✓ | Disabled | 6t |
| `RS_SavannaMedicinalHerbs` | Plant | ✓ | Humidity bonus | 7t |
| `RS_SavannaAshRemains` | BurntRemains | ✗ (10t) | Disabled | 2t |
| `RS_SavannaCarcassRemains` | AnimalRemains | ✗ (5t) | Disabled | 1t |

**Re-run safe:** uses `LoadAssetAtPath` + `EditorUtility.SetDirty` for updates.  
**Prints a report** after running: resources found/missing, spawners created/updated.

---

## 9. How to Add a New Biome's Spawners

1. **Create the Editor script** — copy `SavannaSpawnerCreator.cs`, change the `SaveFolder` constant and `[MenuItem]` string
2. **Load ResourceDefinitions** — use the `Load(path, label, found, missing)` helper for each resource
3. **Define climate curves** (optional) — use `Climate(tempCurve, humCurve)` + `Curve(new Keyframe(...), ...)` helpers
4. **Call `Apply()`** for each spawner — pass id, category, chance, interval, conditions, climate, outputs
5. **Run the menu item** — assets are created or updated in the output folder
6. **Wire to tiles** — drag SOs into `EnvironmentResourceNode.baseSpawners` on the relevant tile prefab or via an installer

---

## 10. Migration from Old resourceDefinitions

Any tile with an empty `baseSpawners` list and a populated `resourceDefinitions` list still works.  
`GenerateResources()` detects this and logs:

```
[TileName] EnvironmentResourceNode is using legacy resourceDefinitions.
Assign baseSpawners instead to use the new spawner system.
```

**Per-tile migration steps:**
1. Identify the resources that should spawn on this tile type
2. Find or create the matching `ResourceSpawnerDefinition` SOs
3. Add them to `EnvironmentResourceNode.baseSpawners` in the Inspector
4. Leave `resourceDefinitions` in place (won't be used once `baseSpawners` has entries)

---

## 11. Known Limitations

| Issue | Impact | Fix |
|-------|--------|-----|
| `activeSpawners` not saved | Temporary spawners (fire, death) lost on save/load | Add `SpawnerStateSaveSection` |
| `IsCurrentlyWet` / `WasRecentlyFlooded` / `HasVolcanicAsh` flags not set | Weather/state-conditional spawners never activate | Wire flags from weather/flood/volcano systems |
| Animal simulation events not wired | Dung + carcass spawners never fire at runtime | Add event calls in `AnimalSimulationController` |
| Missing ResourceDefinitions | Ash, Burnt Wood, Scavenged Meat, Roots, generic Insects — spawner outputs left empty | Create the SOs, then drag into spawner `outputs` in Inspector |
| No save section for `ResourceSpawnerRuntime` state | Turn counters reset on load | Extend `WorldSimSaveSection` or add dedicated section |

---

*Last Updated: May 9, 2026*  
*Audit Confidence: High — all classes read directly from source*
