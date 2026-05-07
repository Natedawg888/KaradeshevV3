# Script Audit Report: Kardeshev 0.3 Unity Project

**Generated:** May 6, 2026  
**Focus:** Read-only analysis of C# systems for Ruflo integration  
**Scope:** Assets/Scripts (AI only) + Assets/ScriptsUpdated (organized systems)

---

## 1. Major Systems Overview

The codebase is organized into 16+ distinct systems with clear separation of concerns. The newer `ScriptsUpdated` folder contains well-organized modular systems, while `Scripts/AI` contains legacy AI code.

### Core System Layers

```
┌─────────────────────────────────────────────────────────────┐
│ Game Management & Bootstrap (Scene orchestration)            │
├─────────────────────────────────────────────────────────────┤
│ Save System (Serialization & persistence)                   │
├─────────────────────────────────────────────────────────────┤
│ World Systems (Time, Seasons, Climate, Disasters)           │
├─────────────────────────────────────────────────────────────┤
│ Grid/Environment/Weather (Map, Tiles, Environment State)    │
├─────────────────────────────────────────────────────────────┤
│ Game Content Systems                                         │
│  ├─ Population (Demographics, Age groups, Family sim)       │
│  ├─ Buildings (Construction, Health, Resistance)           │
│  ├─ Production & Crafting (Plans, Recipes)                 │
│  ├─ Technology (Tech tree, effects)                         │
│  ├─ Disease/Pathogen (Infections, transmission)            │
│  ├─ Religion (Spirits, rituals, civilization state)        │
│  ├─ Animals/AI (Complex simulation, raids, herds)          │
│  └─ Player/Warfare (Inventory, militia, combat)            │
└─────────────────────────────────────────────────────────────┘
```

---

## 2. Important Manager Scripts (Singletons)

All managers use the **Singleton pattern** and are loaded by specialty installers during bootstrap.

### A. Bootstrap & Game Lifecycle

| Script | Location | Purpose | Loads |
|--------|----------|---------|-------|
| **BootstrapLoader** | GameSystems/GameManager | Orchestrates multi-scene loading | 6 installer scenes |
| **GameSceneManager** | GameSystems/GameManager | Manages startup, autosave, turn loop | SaveSystem, Map, Tile systems |
| **GameStartContext** | GameSystems/GameManager | Holds startup mode (NewGame/LoadGame) | Used by GameSceneManager |
| **SceneStartupLifecycleScanner** | GameSystems/GameManager | Detects when scenes are ready | Scene dependencies |

### B. Core Managers (6 Installer Scenes)

| Installer | Managers Loaded | Role |
|-----------|-----------------|------|
| **WorldSetupInstaller** | GridManager, MapGenerator, TileActivator, SeasonManager | World generation & environment |
| **ManagerSetupInstaller** | BuildingManager, GeneralPopulationManager, TechnologyManager, CraftingRecipeManager, ProductionPlanManager | Game content systems |
| **UISetupInstaller** | Various UI controllers | UI initialization |
| **PlayerSetupInstaller** | PlayerInventoryManager, PlayerPopulationStatistic | Player state |
| **FinalSetupInstaller** | Final connections & validation | Startup verification |
| **TutorialSetupInstaller** | Tutorial systems | Tutorial flow |

### C. Major Singleton Managers

#### World & Time Systems
```
TurnSystem
├─ onTurnEnd (event subscribed by: SeasonManager, GameSceneManager, DiseaseManager, etc.)
├─ OnStartOfTurn
└─ Manages: Day/Dusk/Night/Dawn phases, CurrentTurn counter, Speed multiplier

SeasonManager
├─ Depends on: EnvironmentPresetManager, TurnSystem
├─ Manages: Season progression, precipitation, temperature
└─ Events: OnSeasonChanged

EnvironmentPresetManager
├─ Singleton manager for environment settings per stage
├─ Connected to: SeasonManager
└─ Emits: OnPresetApplied event

ClimateManager
├─ Planetary forcing, greenhouse gas simulation
├─ Subscribes to: TurnSystem.SubscribeToEndOfTurn()
└─ Emits: Climate state changes
```

#### Population & Demographics
```
GeneralPopulationManager
├─ Global settings: Age thresholds, health values, recovery rates
├─ Health: Child/Teen/Adult/Elder base health
├─ Lifespan: 180 turns (configurable)
├─ Mortality: Based on health & age
└─ Disease resistance: Per age group (0..1 range)

CivilizationStateManager
├─ Civilization-level metrics (0..1): happiness, health, diversity, integration, order, discovery, knowledge
├─ Depends on: SaveSystem.MarkSectionDirty()
└─ Holistic state of civilization

[Population sub-system]
├─ Individual.cs (single person state)
├─ Family.cs (family unit)
├─ PopulationGroup.cs (demographic grouping)
├─ FamilySim/ (complex family simulation subsystem)
│  ├─ Abstractions/
│  ├─ Config/
│  ├─ Core/
│  ├─ Data/
│  └─ Services/
└─ CivilizationState/ (civilization-level systems)
   ├─ CivilizationHappinessSystem
   ├─ CivilizationDiscoverySystem
   ├─ CivilizationKnowledgeSystem
   ├─ CivilizationOrderSystem
   ├─ CivilizationDiversitySystem
   ├─ CivilizationIntegrationSystem
   └─ LineageUtils
```

#### Content Systems
```
BuildingManager
├─ Registry: 11 stage-based building lists
├─ Maps: buildingID → Building def
├─ Events: OnBuildingControlRegistered, OnBuildingControlUnregistered
└─ Depends on: Stage enum, Building definitions

[Building sub-system]
├─ Building.cs (definition data)
├─ BuildingControl.cs (runtime instance)
├─ BuildingConstruction.cs
├─ BuildingHealth.cs
├─ BuildingFireResistance.cs
├─ BuildingTornadoResistance.cs
├─ BuildingVolcanicResistance.cs
└─ BuildingStatus.cs

DiseaseManager
├─ Disease definitions & pathogen causes
├─ Exposure sources: Environmental, Consumed Resource, Building
├─ Virus spread (context-based & shelter-based)
├─ Mutation mechanics
├─ Subscribes to: TurnSystem.SubscribeToEndOfTurn()
└─ Manages: Active individual diseases & outbreak tracking

TechnologyManager
├─ Technology tree definitions (per Stage)
├─ Tech unlock logic
└─ Tech.cs + TechnologyManager.cs

CraftingRecipeManager & ProductionPlanManager
├─ Recipe definitions
├─ Production plan management
└─ Tied to Building system

ReligionManager / PlayerReligionManager / PlayerKnownRitualsManager
├─ Spirit definitions (AnimismSO)
├─ Player's accepted spirits
├─ Known rituals (bootstrap list)
├─ Ritual execution
└─ Subscribes to: TurnSystem for end-of-turn ritual effects
```

#### Save/Load & Persistence
```
SaveSystem (Singleton)
├─ Manager of all save data
├─ Events: OnSaveQueued, OnSaveStarted, OnSaveCompleted, OnSaveFailed
├─ Features:
│  ├─ Debounced saves
│  ├─ Background write thread (Task-based)
│  ├─ Chunked tile saving (50 tiles/frame)
│  ├─ Cached references (Camera, PopStat, AnimalController, TilePlacer, TileActivator)
│  ├─ Encryption support (EncryptionHelper)
│  └─ Multiple save slots (TurnAutoSave, CloseSave)
├─ RegisteredSections (Dict):
│  ├─ CoreSystemsSaveSection (Buildings, Population, etc.)
│  ├─ EnvironmentSaveSections (Tiles, environment state)
│  ├─ PopulationSaveSection
│  ├─ KnowledgeSaveSection
│  ├─ JobsSaveSection
│  ├─ WorldObjectsSaveSection
│  ├─ WorldSimSaveSection
│  └─ Custom Saveable objects
├─ Thread-safe: _backgroundSaveInFlight, _backgroundWriteTask
└─ Load phases: 8 phases with progress events

[Save subsystem]
├─ ISaveSection (interface)
├─ SaveSectionBase (base implementation)
├─ SaveSectionKeys (enum-like keys)
├─ SaveSnapshot (complete save state)
├─ SaveCaptureContext (multi-frame capture)
├─ Saveables/ (individual saveable objects)
├─ Data/ (serialized data structures)
├─ Utilities/ (serialization helpers)
└─ EncryptionHelper.cs
```

#### Grid & World
```
GridManager
├─ Grid dimensions (rows, columns)
├─ Tile access and manipulation
└─ Core to all tile-based systems

MapGenerator
├─ Depends on: GridManager
├─ Generates initial world map
└─ Stage-based generation

TileActivator
├─ Depends on: GridManager, MapGenerator
├─ Manages active/inactive tiles (performance)
└─ Tile LOD system

EnvironmentPresetManager
├─ Manages environment configurations per stage
├─ Weather & climate settings
└─ Preset-to-stage mapping
```

#### Animal Simulation (Complex)
```
AnimalSimulation (30+ partial files!)
├─ Core.cs - Main state & loop
├─ Ticking.cs - Turn-based updates
├─ TickGroup.cs - Group processing
├─ Decision.cs - Behavioral decisions
├─ Detect.cs - Threat/food detection
├─ Combat.cs - Combat resolution
├─ Groups.cs - Group management
├─ Herding.cs - Group behavior
├─ Health.cs - Individual health
├─ Mortality.cs - Death mechanics
├─ Reproduction.cs - Population growth
├─ Hunting.cs - Hunting behavior
├─ HumanRaids.cs - Raid on player
├─ Fire/Flood/Lava/Tornado/Volcano/Tsunami/Earthquake Effects
├─ Save.cs & SaveCache.cs - Serialization
├─ Debug.cs & DebugInspector.cs - Development tools
└─ UnitCombatAPI.cs - Combat interface to unit system

AnimalSimulationController
├─ Orchestrates the simulation
└─ Registered with SaveSystem

AnimalDefinition, AnimalGroupState, AnimalsCoreTypes, AnimalSizeCategory
└─ Data definitions for animal simulation
```

#### Disaster Systems
```
Earthquakes/
├─ EarthquakeSimulationSystem
├─ EarthquakeFaultLineGenerator
├─ EarthquakeEventData, EarthquakeTypes
├─ Effect resolvers: Building, Animal, Unit, Tsunami Trigger, Volcano Energy
└─ Secondary effects & camera shake

Floods/, Tsunami/, Volcano/
└─ Similar structure: Event system + Effect resolvers
```

#### Player Systems
```
PlayerInventoryManager
├─ Multiple files: Capacity, DiseaseConsumption, SaveLoad
├─ Holds player's resources
├─ Tracks capacity limits
├─ Disease risk on consumption

PlayerPopulationStatistic
├─ Population data tied to player

Player/Inventory/, Player/Population/, Player/Research/, Player/Tiles/, Player/Warfare/
└─ Player-controlled aspects of game
```

---

## 3. Script Dependencies & Initialization Order

### Bootstrap Sequence (Critical)

```
BootstrapCore Scene
  ↓
BootstrapLoader (MonoBehaviour, IEnumerator Start)
  ↓
  1. Load WorldSetupScene (additive)
     ├─ GridManager
     ├─ EnvironmentPresetManager
     ├─ MapGenerator (depends on GridManager)
     ├─ MapTilePlacer (depends on GridManager + MapGenerator)
     ├─ TileActivator (depends on GridManager + MapGenerator)
     ├─ TileUIResolveCoordinator
     ├─ SeasonManager (depends on EnvironmentPresetManager)
     └─ SavedTilePlacer, MonoEnvironmentDataSource
  ↓
  2. Load ManagerSetupScene (additive)
     ├─ LevelManager
     ├─ BuildingManager (no hard deps)
     ├─ GeneralPopulationManager (no hard deps)
     ├─ TechnologyManager (no hard deps)
     ├─ CraftingRecipeManager (no hard deps)
     └─ ProductionPlanManager (no hard deps)
  ↓
  3. Load UISetupScene
     └─ Various UI managers
  ↓
  4. Load PlayerSetupScene
     └─ Player-related systems
  ↓
  5. Load FinalSetupScene
     └─ Final connections, TurnSystem, etc.
  ↓
  6. (Optional) Load TutorialSetupScene
     └─ Tutorial systems
  ↓
GameSceneManager.RunStartupRoutine()
  ├─ Wait for frame settling
  ├─ Choose GameStartMode: NewGame or LoadGame
  ├─ NEW: Call MapGenerator.GenerateNewMap()
  ├─ LOAD: Call SaveSystem.LoadGame()
  ├─ Initialize turn systems
  └─ Start main game loop
```

### Turn System Subscription Chain

```
TurnSystem.onTurnEnd fires each turn:

Subscribers (order matters):
1. SeasonManager.HandleTurnEnd()
   └─ Updates season, precipitation, temp
   
2. GameSceneManager.HandleEndTurn()
   └─ Handles autosave, quit requests
   
3. DiseaseManager processes diseases
   └─ Spread, mutation, recovery, death
   
4. ClimateManager.HandlePlanetaryForcingTurnEnd()
   └─ Greenhouse gas buildup
   
5. Religion systems: PlayerReligionManager
   └─ Ritual effects, happiness/order impacts
   
6. TurnUI updates (SeasonDisplay, etc.)
```

### Critical Dependencies

**Hard Dependencies (Will crash if missing):**
- SaveSystem depends on: SaveSystem.Instance lookup during Awake
- BuildingManager.RebuildDefinitionIndex() needs building definitions pre-loaded
- TileActivator depends on GridManager & MapGenerator
- DiseaseManager depends on disease/pathogen definitions being loaded
- AnimalSimulationController depends on AnimalDefinitions

**Soft Dependencies (Will degrade gracefully):**
- ClimateManager can work without greenhouse gas data
- Tutorial systems are optional
- Religion systems have null-checks for optional data

---

## 4. Duplicate & Outdated Scripts

### ⚠️ CRITICAL: Two Script Folders Structure

**Old System:**
```
Assets/Scripts/AI/
├─ AIColorRegistry.cs
├─ AIManager.cs
├─ AIPlayerRegistry.cs
└─ Ai Player/ (folder)
```

**New System (Primary):**
```
Assets/ScriptsUpdated/
├─ Well-organized by game system
├─ 16+ folders with clear purpose
└─ ~200+ C# files, fully modular
```

**RISK:** Having both folders active creates confusion. The `Scripts/AI` folder appears to be legacy/outdated. Needs clarification:
- Are Scripts/AI still in use?
- Should it be deleted or archived?
- Any other legacy code hiding there?

### Potential Duplicates/Consolidation Candidates

**1. Population Managers (Multiple Hierarchies)**
```
GeneralPopulationManager
  └─ Global settings (ages, health)

CivilizationStateManager
  └─ Civ-level metrics

CivilizationState/ subsystem (5 specialized managers)
  ├─ CivilizationHappinessSystem
  ├─ CivilizationDiscoverySystem
  ├─ CivilizationKnowledgeSystem
  └─ ...

Family simulation (FamilySim/) 
  └─ Separate, complex subsystem
```
**Status:** Appears intentional (different abstraction levels), but worth reviewing for overlap.

**2. Building Resistance Systems (Multiple Files)**
```
BuildingFireResistance.cs
BuildingTornadoResistance.cs
BuildingVolcanicResistance.cs
BuildingEarthquakeResistance.cs
```
**Status:** Could be consolidated into one `BuildingDamageResistance` system with a configurable `ResistanceType` enum. Currently copy-paste prone.

**3. Disaster Effect Resolvers (Repeated Pattern)**
```
EarthquakeAnimalEffectResolver.cs
EarthquakeBuildingEffectResolver.cs
EarthquakeUnitEffectResolver.cs
EarthquakeTsunamiTriggerResolver.cs
EarthquakeVolcanoEnergyResolver.cs
```
**Status:** Similar structure for Floods, Tsunamis, Volcanoes. Could use a generic `DisasterEffectResolver<T>` base class.

**4. Disease Exposure Sources (Multiple)**
```
EnvironmentalDiseaseRisk.cs
ConsumedResourceDiseaseRisk.cs
BuildingDiseaseExposureSource.cs
```
**Status:** Could implement common `IDiseaseExposureSource` interface more consistently.

### Version Numbers & Comments

- Found comments referencing ".cs" file types in some code (e.g., `// AnimalSimulation.cs (ADD)`)
- No obvious version conflicts, but watch for TODO/FIXME comments during refactoring

---

## 5. Missing References & Risky Initialization Order Issues

### 🔴 HIGH RISK: Missing Null Checks

**SaveSystem Cached References (Awake):**
```csharp
[SerializeField] private CameraControl cameraControl;
[SerializeField] private PlayerPopulationStatistic playerPopulationStatistic;
[SerializeField] private AnimalSimulationController animalSimulationController;
[SerializeField] private SavedTilePlacer savedTilePlacer;
[SerializeField] private TileActivator tileActivator;

private void Awake()
{
    // References are optional but accessed later without checks
    RefreshCachedReferences();
}
```
**Risk:** If any reference is unassigned in Inspector, save/load could fail silently.  
**Recommendation:** Add `??` null-coalescing operators or explicit null checks.

**GameSceneManager Dependencies:**
```csharp
private void Awake()
{
    if (saveSystem == null)
        saveSystem = SaveSystem.Instance;  // Assumes Instance already exists!
}
```
**Risk:** If SaveSystem hasn't run its Awake() yet, Instance will be null.  
**Mitigation:** SaveSystem runs first (loaded earlier), but fragile.

**SeasonManager → EnvironmentPresetManager:**
```csharp
var presetMgr = EnvironmentPresetManager.Instance;
if (presetMgr == null) 
    // Debug.LogError but continues...
```
**Risk:** If EnvironmentPresetManager is not loaded, systems degrade.

### 🟡 MEDIUM RISK: Circular Event Subscriptions

**SeasonManager ↔ EnvironmentPresetManager (Events):**
```
OnEnable():
  EnvironmentPresetManager.Instance.OnPresetApplied += HandlePresetApplied;
OnDisable():
  EnvironmentPresetManager.Instance.OnPresetApplied -= HandlePresetApplied;
```
**Risk:** If unsubscribe fails, memory leak.

**Multiple TurnSystem subscribers:**
Multiple systems subscribe to `TurnSystem.onTurnEnd`. Order of execution matters but isn't documented.  
**Risk:** Changing subscription order could break game logic.

### 🟡 MEDIUM RISK: Scene Unload Order

**6-scene additive loading, 1-scene unload:**
Scenes are loaded additively but presumably unloaded together. If a system caches a reference to an object being destroyed, hard crash.

### 🟢 LOW RISK: SaveSystem Thread Safety

SaveSystem uses `Task _backgroundWriteTask` and `bool _backgroundSaveInFlight`.  
Code appears thread-safe with proper volatile handling, but could deadlock if exceptions occur in background thread.

---

## 6. Systems Recommended for Refactoring

### Priority 1: High Impact, Medium Effort

#### 1A. **AnimalSimulation Partial Class Fragmentation**
- **Issue:** 30+ partial `.cs` files (one per system)
- **Impact:** Hard to navigate, understand dependencies
- **Refactor:** Group partials into logical subdirectories or use a facade pattern
- **Timeline:** 2-3 days
```
AnimalSimulation/
├─ Behavior/ (Decision, Detect, Herding, Hunting, Combat, HumanRaids)
├─ Biology/ (Health, Mortality, Reproduction)
├─ Environment/ (Fire, Flood, Lava, Tornado, Volcano, Tsunami, Earthquake)
├─ Data/ (TickGroup, Groups, DietCounts, MergeSplit, Reproduction)
├─ Persistence/ (Save, SaveCache)
└─ AnimalSimulation.Core.cs, AnimalSimulation.Ticking.cs
```

#### 1B. **Building Resistance Systems Consolidation**
- **Issue:** 4 nearly-identical resistance files
- **Current:** BuildingFireResistance, BuildingTornadoResistance, BuildingVolcanicResistance, BuildingEarthquakeResistance
- **Refactor:** Create `BuildingDamageResistance` class with `ResistanceType` enum
- **Timeline:** 1 day
```csharp
public enum ResistanceType { Fire, Tornado, Volcanic, Earthquake }
public class BuildingDamageResistance : MonoBehaviour
{
    public Dictionary<ResistanceType, float> resistances = new();
    public float GetResistance(ResistanceType type) => resistances.TryGetValue(type, out var r) ? r : 0f;
}
```

#### 1C. **Disaster Effect Resolver Generics**
- **Issue:** Similar code in Earthquake, Flood, Tsunami, Volcano disaster folders
- **Current:** EarthquakeAnimalEffectResolver, EarthquakeBuildingEffectResolver, etc.
- **Refactor:** Generic base class `DisasterEffectResolver<T>` with specializations
- **Timeline:** 2 days
```csharp
public abstract class DisasterEffectResolver<T> where T : DisasterEventData
{
    public abstract void ResolveEffects(T disasterData, List<GameObject> targets);
}
```

### Priority 2: Medium Impact, High Effort

#### 2A. **Disease Exposure System Standardization**
- **Issue:** Multiple exposure sources (Environmental, Consumed Resource, Building) with inconsistent interfaces
- **Current:** EnvironmentalDiseaseRisk, ConsumedResourceDiseaseRisk, BuildingDiseaseExposureSource
- **Refactor:** Create `IDiseaseExposureSource` interface, consolidate logic
- **Timeline:** 2-3 days
- **Benefit:** Easier to add new exposure types (e.g., WaterSource, TravelRoute)

#### 2B. **Population State Hierarchy Unification**
- **Issue:** GeneralPopulationManager, CivilizationStateManager, Family, Individual all manage state separately
- **Current:** Multiple singleton managers + complex subsystems
- **Refactor:** Consider Service Locator or Event Bus pattern for state changes
- **Timeline:** 3-4 days
- **Risk:** Complex, could break save compatibility

#### 2C. **Save System Threading Robustness**
- **Issue:** Background thread could throw exception and leave `_backgroundSaveInFlight = true` forever
- **Current:** No exception handling in `_backgroundWriteTask`
- **Refactor:** Add try-catch, timeout mechanism, state recovery
- **Timeline:** 1 day
- **Benefit:** Prevents save deadlocks

### Priority 3: Low Impact, Nice-to-Have

#### 3A. **Installer Scene Consolidation**
- **Issue:** 6 installer scenes for bootstrap feels excessive
- **Current:** WorldSetupInstaller, ManagerSetupInstaller, UISetupInstaller, PlayerSetupInstaller, FinalSetupInstaller, TutorialSetupInstaller
- **Consider:** Merge into 2-3 scenes (World, Managers, Player) to reduce load time
- **Timeline:** 1-2 days
- **Risk:** Tight coupling

#### 3B. **TurnSystem Event Ordering Documentation**
- **Issue:** Multiple systems subscribe to `onTurnEnd`, order is implicit
- **Refactor:** Create `TurnEventPhase` enum to explicitly order subscribers
- **Timeline:** 1 day
```csharp
enum TurnEventPhase { Environment = 0, Population = 10, Content = 20, UI = 30 }
TurnSystem.Subscribe(handler, TurnEventPhase.Population);
```

---

## 7. Safe Workflow for Future Claude/Ruflo Tasks

### Before Making Changes

1. **Read Audit & Update Memory**
   ```bash
   # Check session memory for task context
   cat /memories/session/current_task.md
   
   # Review this audit for affected systems
   # Search for "RISK" in SCRIPT_AUDIT.md
   ```

2. **Identify Impact Zone**
   - System(s) affected
   - Managers that depend on this system
   - SaveSystem implications
   - Event subscribers
   - Turn system impact

3. **Check Initialization Order**
   - Is the manager in an Installer scene?
   - Which installer scene? (order matters: World → Manager → UI → Player → Final)
   - Any circular dependencies with other installers?

4. **SaveSystem Checklist**
   - Does this system have saveable data?
   - Is there a SaveSection for it?
   - Need to add to CoreSystemsSaveSection or custom section?
   - Test save/load cycle!

5. **Event Subscription Checklist**
   - Does it subscribe to TurnSystem.onTurnEnd or OnStartOfTurn?
   - Order of subscription matters (see [Risk: Circular Event Subscriptions])?
   - Proper unsubscribe in OnDisable()?
   - Memory leak risk?

### Making Changes Safely

#### Small Fixes (1-2 files)
```
1. Read the file completely
2. Identify all references (search workspace)
3. Make changes
4. Verify: no new compiler errors
5. Test: affected gameplay systems (if testable)
6. Document: reason for change in git commit
```

#### System Refactors (3+ files, same system)
```
1. Create /memories/session/refactor_plan.md with:
   - Files affected
   - Classes renamed
   - Public API changes
   - Backwards compatibility plan
   
2. Use vscode_listCodeUsages to find all references
3. Use vscode_renameSymbol for safe bulk renames
4. Make changes in isolated system first
5. Update SaveSystem if persistence changed
6. Test: Full game boot + save/load cycle
7. Git commit with detailed message
```

#### Cross-System Changes (Modifying manager, dependencies, events)
```
1. STOP - Schedule with other team members!
2. Create detailed /memories/repo/architecture_change.md
3. Identify all affected systems (use audit above)
4. Plan new initialization order
5. Create test save file before changes
6. Make changes incrementally:
   a. Update one system
   b. Test in isolation
   c. Verify other systems still work
   d. Move to next system
7. Full integration test (new game + load old save)
8. Get code review!
```

### Recommended Ruflo Workflow (Multi-Agent)

#### Task: Bug Fix in Building System
```
Lead (you)
  ├─ Researcher: "Analyze BuildingManager & 1 step of callers. SendMessage findings to 'coder'."
  ├─ Coder: "Implement fix in identified files. SendMessage code to 'tester'."
  ├─ Tester: "Test in-game: Can place buildings? Can damage them? SendMessage result."
  └─ Reviewer: "Check code quality. Document change reason."
```

#### Task: Add New Disaster Type (Drought)
```
Lead (you)
  ├─ Architect: "Design Drought system using Earthquake as template. SendMessage design to 'coder'."
  ├─ Coder: "Create Drought/ folder, effect resolvers. SendMessage to 'tester'."
  ├─ Tester: "Verify triggers, effects apply to Buildings/Animals/Population. SendMessage results."
  └─ Reviewer: "Check SaveSystem integration, audit completeness."
```

#### Task: Refactor Building Resistance Systems
```
Lead (you)
  ├─ Architect: "Design unified BuildingDamageResistance. SendMessage to 'coder'."
  ├─ Coder: "Implement base class, migrate 4 resistance files. SendMessage to 'tester'."
  ├─ Tester: "Full fire/tornado/volcano/earthquake damage tests. SendMessage results."
  ├─ Reviewer: "Code quality, savings report."
  └─ You: "Merge if all green, update this audit."
```

### Key Memory Files to Maintain

```
/memories/session/
├─ current_task.md (what are we working on?)
├─ system_context.md (which systems are affected?)
├─ risks_identified.md (what could break?)
└─ test_results.md (did it work?)

/memories/repo/
├─ script_audit.md (THIS FILE - keep updated)
├─ architecture_decisions.md (why choices were made)
├─ known_issues.md (bugs, performance, refactor candidates)
└─ save_system_structure.md (serialization format)
```

### Tools to Use

| Task | Tool | Command |
|------|------|---------|
| Find all callers | vscode_listCodeUsages | `vscode_listCodeUsages(symbol="BuildingManager", lineContent="public class BuildingManager")` |
| Rename safely | vscode_renameSymbol | `vscode_renameSymbol(symbol="OldName", newName="NewName", filePath="...")` |
| Understand system | semantic_search | `"how does disease spread in this game?"` |
| Find duplicates | grep_search | Search for `public class` or `public static` patterns |
| Analyze risk | mcp_claude-flow_analyze_diff | Compare git changes before committing |

---

## 8. Game Content System Dependencies Map

### Build System

```
BuildingManager
├─ Depends: Building definitions (SO assets)
├─ Feeds into: BuildingControl instances, Save/Load
├─ Affects: Tile occupancy, production, population jobs
├─ Events: OnBuildingControlRegistered, OnBuildingControlUnregistered
└─ Disaster Risk: Fire, Tornado, Volcano, Earthquake resistances

BuildingConstruction
├─ Manages: Construction queue, progress
├─ Depends: BuildingManager, GridManager
└─ Updates: Building health during construction

BuildingHealth / BuildingFireResistance / BuildingTornadoResistance, etc.
├─ Manages: Damage types, resistance
├─ Affected by: Disasters, Time decay
└─ Triggers: Destruction, repair jobs
```

### Population System

```
GeneralPopulationManager (Config)
├─ Health: Base values per age group
├─ Lifespan: 180 turns (configurable)
├─ Recovery: Per age group recovery rates
├─ Needs: Hunger/Thirst thresholds, damage rates
└─ Mortality: Low health & elder age mortality

Individual (Runtime)
├─ State: Health, age, inventory, job status
├─ Age progression: Child → Teen → Adult → Elder
├─ Traits: Disease susceptibility, skills
└─ Events: Birth, death, disease infection

Family
├─ Relationships: Parent/child
├─ Production: Family-level buffs/debuffs
└─ Persistence: Saved with civilization state

FamilySim/
├─ Complex family unit simulation
├─ Reproduction mechanics
├─ Family tree tracking
└─ Lineage utilities

CivilizationStateManager
├─ Happiness: Affected by food, disasters, rituals
├─ Order: Affected by religion, welfare
├─ Discovery: Tech progress
├─ Knowledge: Cumulative learning
├─ Health: Population average health
├─ Diversity: Cultural variety
└─ Integration: Group cohesion

CivilizationHappinessSystem, DiscoverySystem, KnowledgeSystem, OrderSystem
├─ Sub-systems for tracking civilization metrics
└─ Updated by various game events
```

### Disease System

```
DiseaseManager
├─ Definitions: Disease types (SO)
├─ Causes: Pathogen definitions (SO)
├─ Exposure Sources:
│  ├─ EnvironmentalDiseaseRisk (terrain/weather)
│  ├─ ConsumedResourceDiseaseRisk (eating bad food)
│  └─ BuildingDiseaseExposureSource (shelter/gathering place)
├─ Mechanics:
│  ├─ Virus spread (shelter & task groups)
│  ├─ Mutation on spread
│  ├─ Recovery with immunity
│  └─ Mortality: Infected die, disease fades
├─ Integration:
│  ├─ Affects: Population health, work efficiency, task failure rate
│  ├─ Triggered by: Food consumption, task failure, crowding
│  └─ Mitigated by: Vaccination, quarantine, age resistance
└─ Events: onTurnEnd subscription for spread/recovery cycle
```

### Production & Crafting

```
ProductionPlanManager
├─ Plans: Queue of production orders
├─ Execution: Building-based production
├─ Dependency: Buildings must exist
└─ Output: Resources into player inventory

CraftingRecipeManager
├─ Recipes: Definition data (SO)
├─ Crafting: Building-based recipes
├─ Requirements: Input resources, tech unlocks
└─ Output: New resources, buildings
```

### Technology System

```
TechnologyManager
├─ Tree: Per-stage tech definitions
├─ Unlock: Prerequisite chain
├─ Effects: Tech.cs defines impact (damage/speed/cost buffs)
└─ Persistence: Saved with civilization state
```

### Religion System

```
ReligionManager
├─ Spirits: AnimismSO definitions
├─ Acceptance: Player's accepted spirits
└─ Rituals: Ritual effects, happiness/order impacts

PlayerReligionManager
├─ Current accepted spirits
├─ Ritual queue
└─ Subscribes: TurnSystem.onTurnEnd for ritual effects

PlayerKnownRitualsManager
├─ Known rituals (bootstrap list from SO)
├─ Ritual execution
└─ Effects: Happiness, order, population impacts
```

### Time & Environment

```
TurnSystem
├─ Timer: Day/Dusk/Night/Dawn cycle
├─ Turn counter: CurrentTurn += 1 per cycle
├─ Speed: Normal vs. Fast multiplier
├─ Events: onTurnEnd fires subscribers
└─ Subscribers: SeasonManager, GameSceneManager, DiseaseManager, ClimateManager, Religion, TurnUI

SeasonManager
├─ Seasons: Per-stage seasonal definitions
├─ Progression: Turns per season
├─ Effects: Precipitation, temperature, disaster likelihood
├─ Depends: EnvironmentPresetManager, TurnSystem
└─ Events: OnSeasonChanged

EnvironmentPresetManager
├─ Presets: Weather/climate per stage
├─ Application: Applies to world on season change
└─ Events: OnPresetApplied

ClimateManager
├─ Forcing: Greenhouse gas buildup
├─ Feedback: Affects temperature extremes
├─ Subscribes: TurnSystem.onTurnEnd
└─ Affects: Disaster frequency/severity
```

### Disaster Systems (Earthquake, Flood, Volcano, Tsunami)

Each follows similar pattern:
```
Earthquake/
├─ EarthquakeSimulationSystem: Main logic
├─ EarthquakeFaultLineGenerator: Map generation
├─ EarthquakeEventData: Event payload
├─ EarthquakeTypes: Enums (magnitude, type)
└─ Effect Resolvers:
   ├─ EarthquakeBuildingEffectResolver
   ├─ EarthquakeAnimalEffectResolver
   ├─ EarthquakeUnitEffectResolver
   ├─ EarthquakeTsunamiTriggerResolver (cross-system!)
   └─ EarthquakeVolcanoEnergyResolver (cross-system!)
```

---

## 9. Script Organization Summary

### By Responsibility

| Category | Location | Files | Criticality |
|----------|----------|-------|------------|
| **Bootstrap** | GameSystems/GameManager | 4 | 🔴 Critical |
| **Save/Load** | GameSystems/SaveSystem | 15+ | 🔴 Critical |
| **Turn/Time** | WorldSystems/Time | 5 | 🔴 Critical |
| **Grid/World** | Grid_Map | 8 | 🔴 Critical |
| **Population** | Population | 20+ | 🟡 Important |
| **Buildings** | Buildings | 14 | 🟡 Important |
| **Disasters** | Disaster/* | 40+ | 🟡 Important |
| **Disease** | DiseaseSystem | 10 | 🟡 Important |
| **Animals** | Enemies/Animals | 30+ | 🟡 Important |
| **Production/Craft** | Production, Crafting | 4 | 🟢 Standard |
| **Technology** | Technology | 3 | 🟢 Standard |
| **Religion** | Religion | 8 | 🟢 Standard |
| **Player** | Player/* | 8 | 🟢 Standard |
| **Notifications** | Notifications | 9 | 🟢 Standard |
| **UI** | Panels | 20+ | 🟢 Standard |

### By Dependency Level

**Tier 1 (Foundation - everything depends on these):**
- SaveSystem
- GridManager
- TurnSystem

**Tier 2 (Systems - used by multiple features):**
- GeneralPopulationManager
- BuildingManager
- DiseaseManager
- SeasonManager
- TechnologyManager

**Tier 3 (Features - used by specific systems):**
- ProductionPlanManager
- CraftingRecipeManager
- ReligionManager
- AnimalSimulationController

**Tier 4 (UI/Polish - independent):**
- PlayerInventoryManager
- SeasonDisplay
- NotificationManager
- Various UI panels

---

## 10. Recommendations Summary

### ✅ What's Working Well
- Clear system separation in ScriptsUpdated/
- Installer-based bootstrap is robust
- SaveSystem is well-designed and thread-safe
- Singleton pattern used consistently
- Turn-based event system is clean

### ⚠️ Areas to Watch
- Partial class fragmentation (AnimalSimulation)
- Duplicate resistance systems
- Multiple population managers at different levels
- Circular event subscriptions possible
- Missing null checks in SaveSystem refs
- Script/AI folder needs clarification

### 🚀 Next Steps for Ruflo Integration

1. **Small Tasks First**
   - Single system bugfixes
   - UI improvements
   - Config adjustments

2. **Medium Tasks with Validation**
   - Add new building types
   - New disaster events
   - Tech tree extensions

3. **Major Refactors Later**
   - AnimalSimulation reorganization
   - Population system consolidation
   - Resistance system unification

---

## Appendix: Key File Locations Quick Reference

```
GameSystems/GameManager/
  ├─ BootstrapLoader.cs
  ├─ GameSceneManager.cs
  ├─ GameStartContext.cs
  ├─ Installers/ (6 setup scenes)
  └─ ...

GameSystems/SaveSystem/
  ├─ SaveSystem.cs
  ├─ ISaveSection.cs
  ├─ SaveSectionBase.cs
  ├─ SaveSectionKeys.cs
  ├─ SaveSnapshot.cs
  └─ *SaveSection.cs (multiple)

Population/
  ├─ GeneralPopulationManager.cs
  ├─ Individual.cs, Family.cs, PopulationGroup.cs
  ├─ CivilizationState/ (5 systems)
  └─ FamilySim/ (complex subsystem)

Buildings/
  ├─ BuildingManager.cs
  ├─ Building.cs, BuildingControl.cs
  ├─ BuildingHealth.cs
  ├─ Building*Resistance.cs (4 files)
  └─ Repair/, BuildingsTypes/

DiseaseSystem/
  ├─ DiseaseManager.cs
  ├─ IDiseaseTarget.cs
  ├─ *DiseaseRisk.cs (3 files)
  └─ Panel/

Enemies/Animals/
  ├─ AnimalDefinition.cs
  ├─ AnimalSimulation/ (30+ partial files)
  ├─ AnimalSimulationController/
  └─ ...

WorldSystems/
  ├─ Time/
  │  ├─ TurnSystem.cs
  │  ├─ SeasonManager.cs
  │  ├─ ClimateManager.cs
  │  └─ SeasonDisplay.cs
  └─ Themes/, Levels/, Resources/

Disaster/
  ├─ Earthquakes/ (12 files)
  ├─ Floods/
  ├─ Volcanoes/
  └─ Tsunamis/

Grid_Map/
  ├─ GridManager.cs
  ├─ MapGenerator.cs
  ├─ EnvironmentPresetManager.cs
  └─ TileActivator.cs, TileScript.cs

Notifications/
  ├─ NotificationManager.cs         (Singleton — stores List<NotificationData>, fires events)
  ├─ NotificationData.cs            (Data class + ProductionOutputEntry)
  ├─ NotificationType.cs            (Enum — 16 types)
  ├─ NotificationMessageCrafter.cs  (ScriptableObject — randomised templates, token replacement)
  ├─ NotificationMessageCrafterManager.cs (Singleton MonoBehaviour wrapper for crafter SO)
  ├─ NotificationButtonUI.cs        (HUD button, swaps sprite on unread)
  ├─ NotificationPanelUI.cs         (Scroll panel, Open/Close/Toggle, rebuilds rows on change)
  ├─ NotificationRowUI.cs           (Single row — see architecture note below)
  └─ NotificationIconSet.cs         (ScriptableObject — type → sprite map)

NotificationManager public API (as of May 7, 2026):
  AddNotification(type, title, message)
  AddNotification(type, title, message, bool showDeathIcon)
  AddNotification(type, title, message, Vector3 worldPosition)
  AddProductionCompletedNotification(title, message, List<ProductionOutputEntry>, Vector3 worldPosition = default)
  AddProductionPausedNotification(type, title, message, Vector3 worldPosition = default)
    └─ accepts ProductionPausedLackOfResources or ProductionPausedLackOfWorkers
  AddCraftingCompletedNotification(title, message, List<ProductionOutputEntry>, Vector3 worldPosition = default)
  AddCraftingFailedNotification(title, message, Vector3 worldPosition = default)
    └─ fires CraftingFailedWeather type
  Passing worldPosition sets hasTileTarget = true → goToButton shows in row UI

NotificationMessageCrafterManager craft methods:
  Craft(type, EnvironmentControl, int populationLost)     — gathering / discovery
  CraftResearch(type, techName)
  CraftBirth(type, motherSurname, bornAlive, motherDied)
  CraftProduction(type, buildingName, planName)
  CraftBuilding(type, buildingName)
  CraftCrafting(type, recipeName, buildingName)           — tokens: {RECIPE}, {BUILDING}

NotificationRowUI architecture (as of May 7, 2026):
  Fields:
    titleText, messageText, turnText   — TMP labels
    deleteButton                       — removes notification from manager
    typeIcon, deathIcon                — sprites from NotificationIconSet
    iconSet                            — NotificationIconSet SO reference
    goToButton                         — camera jump; shown when data.hasTileTarget is true
    viewOutputButton (Button)          — shows/hides outputPanel; active for ProductionCompleted + CraftingCompleted
    outputPanel (GameObject)           — ScrollView; toggled by viewOutputButton click
    resourceItemPrefab (GameObject)    — ResourceEntryPrefab; spawned into outputContainer
    outputContainer (Transform)        — Content transform inside outputPanel; spawn target

  Prefab: Resources/UI_Assets/(New) Prefabs/Notifications/NotificationItemPrefab.prefab
    NotificationItemPrefab (root, NotificationRowUI)
    ├─ NotificationItemTitleCard
    ├─ NotificationItemTurnTitleCard / NotificationItemTurnText
    ├─ NotificationItemClear            (deleteButton)
    ├─ NotificationItemDeath            (deathIcon)
    ├─ NotificationItemGoToTile         (goToButton)
    ├─ NotificationItemMessageScrollView
    ├─ NotificationItemIconImage        (typeIcon)
    └─ NotificationItemViewOutput       (viewOutputButton — Button; shown for ProductionCompleted + CraftingCompleted)
        └─ ScrollView                   (outputPanel)
            └─ Viewport → Content       (outputContainer)
```

---

## 11. Changelog

### May 7, 2026 — Notification System Refactor

**Files changed:**
- `ScriptsUpdated/Notifications/NotificationRowUI.cs`
- `Resources/UI_Assets/(New) Prefabs/Notifications/NotificationItemPrefab.prefab`

**What changed:**

`NotificationRowUI` previously held a `SurveyPanelControl outputPanel` reference and delegated production output display to `SurveyPanelControl.ShowTutorialEntries()`. This created a hard dependency on the environment survey panel for unrelated notification UI.

**Replaced with direct inline rendering:**
- Removed `SurveyPanelControl outputPanel`
- Added `GameObject resourceItemPrefab` — references `ResourceEntryPrefab` directly
- Added `Transform outputContainer` — the ScrollView `Content` transform; items are `Instantiate`d here
- Added `GameObject outputPanel` — the `ScrollView` GameObject; this is what the button shows/hides

**Behaviour:**
- `Populate()` hides `outputPanel` on init (collapsed by default)
- `viewOutputButton` is only shown for `ProductionCompleted` notifications that have output entries
- Clicking `viewOutputButton` toggles `outputPanel` (show/hide the whole ScrollView)
- Items are spawned fresh into `outputContainer` on each expand; destroyed on collapse
- `ResourceEntryUI.Initialize(ResourceSpawnEntry)` is called per entry — spoilage slider will show 0 remaining (no spoilage context in a notification)

**Prefab additions:**
- `NotificationItemViewOutput` child added to root — Image + Button (the viewOutputButton)
- `ScrollView` child inside it — the outputPanel
- `outputPanel`, `resourceItemPrefab`, `outputContainer` wired in the `NotificationRowUI` component
- `resourceItemPrefab` → `ResourceEntryPrefab` (guid `cdbbc2fb23ca7f04a8cecfc1de1b47c7`)

### May 7, 2026 — Crafting Notifications + Go-To Tile for Production & Crafting

**Files changed:**
- `ScriptsUpdated/Notifications/NotificationType.cs`
- `ScriptsUpdated/Notifications/NotificationManager.cs`
- `ScriptsUpdated/Notifications/NotificationMessageCrafter.cs`
- `ScriptsUpdated/Notifications/NotificationMessageCrafterManager.cs`
- `ScriptsUpdated/Notifications/NotificationIconSet.cs`
- `ScriptsUpdated/Notifications/NotificationRowUI.cs`

**New notification types added:**
- `CraftingCompleted` — fires when a crafting recipe finishes; shows viewOutputButton with crafted items
- `CraftingFailedWeather` — fires when bad weather interrupts a craft; no output list

**New `NotificationManager` API:**
- `AddCraftingCompletedNotification(title, message, List<ProductionOutputEntry>, Vector3 worldPosition = default)`
- `AddCraftingFailedNotification(title, message, Vector3 worldPosition = default)`
- `AddProductionPausedNotification(type, title, message, Vector3 worldPosition = default)` — dedicated helper for the two paused types
- `AddProductionCompletedNotification` updated with optional `worldPosition` parameter

**Go-To Tile button extended:**  
All four production/crafting event types now support the go-to tile button. The button activates automatically when `worldPosition` is passed to the notification (sets `hasTileTarget = true`). Pass the building's `transform.position` at the call site. No row UI change was needed — `goToButton` already gates on `hasTileTarget`.

**`NotificationMessageCrafter` / `NotificationMessageCrafterManager`:**
- New `CraftCrafting(type, recipeName, buildingName)` method added to both
- Tokens: `{RECIPE}`, `{BUILDING}`
- Template sets added for both `CraftingCompleted` and `CraftingFailedWeather` in `PopulateDefaults()`

**`NotificationRowUI`:**
- `viewOutputButton` visibility condition extended: now also active for `CraftingCompleted` (with non-empty `producedOutputs`)

**`NotificationIconSet`:**
- Two new entries added to `Reset()`: `CraftingCompleted`, `CraftingFailedWeather` — sprites unassigned, assign in Inspector

**Wiring crafting notifications in `PlayerCraftingManager`:**
```csharp
// On completion (inside ProcessCompletions, after inventory add):
var crafter = NotificationMessageCrafterManager.Instance;
var (title, msg) = crafter.CraftCrafting(NotificationType.CraftingCompleted, recipeName, buildingName);
var outputs = /* convert cc.payout (List<ResourceAmount>) to List<ProductionOutputEntry> */;
NotificationManager.Instance?.AddCraftingCompletedNotification(title, msg, outputs, buildingPosition);

// On weather failure:
var (title, msg) = crafter.CraftCrafting(NotificationType.CraftingFailedWeather, recipeName, buildingName);
NotificationManager.Instance?.AddCraftingFailedNotification(title, msg, buildingPosition);
```

### May 7, 2026 — Building Fire Notification

**Files changed:**
- `ScriptsUpdated/Notifications/NotificationType.cs`
- `ScriptsUpdated/Notifications/NotificationMessageCrafter.cs`
- `ScriptsUpdated/Notifications/NotificationIconSet.cs`
- `ScriptsUpdated/Grid_Map/Weatherv2/Fire/BuildingFireState.cs`

**New notification type:** `BuildingOnFire`

**How it fires:**  
`BuildingFireState.TryIgnite()` calls `PostFireNotification()` immediately after `OnIgnited` fires. No external subscription or manager needed — the notification is self-contained on the building.

```
BuildingFireState.TryIgnite()
  └─ PostFireNotification()
       ├─ Gets buildingName from BuildingControl.buildingName (falls back to gameObject.name)
       ├─ CraftBuilding(BuildingOnFire, buildingName) via NotificationMessageCrafterManager
       └─ NotificationManager.AddNotification(BuildingOnFire, title, message, transform.position)
```

Passing `transform.position` sets `hasTileTarget = true` — the Go-To Tile button activates automatically in the row UI.

**`NotificationMessageCrafter`:**
- `BuildingOnFire` fallback added to `CraftBuilding()` switch: `"Building on Fire!" / "{buildingName} is on fire!"`
- Template set added in `PopulateDefaults()` — token: `{BUILDING}`

**`NotificationIconSet`:** new entry for `BuildingOnFire` — assign fire sprite in Inspector

**Pattern note:** Follows the identical pattern to `BuildingStatus.PostBuildingStateNotification()` (used for `BuildingDamaged` / `BuildingDestroyed`). Both use `CraftBuilding()` + `AddNotification(..., transform.position)`.

---

**End of Report**

*Status: Ready for Ruflo Integration*  
*Last Updated: May 7, 2026*  
*Audit Confidence: High (comprehensive read-only scan)*
