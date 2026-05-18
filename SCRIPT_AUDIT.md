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
│  ├─ NotificationsSaveSection (unread notifications — added May 8, 2026)
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
  ├─ NotificationManager.cs         (Singleton — stores List<NotificationData>, fires events; SaveState/LoadState)
  ├─ NotificationData.cs            (Data class + ProductionOutputEntry)
  ├─ NotificationType.cs            (Enum — 41 types as of May 8, 2026)
  ├─ NotificationMessageCrafter.cs  (ScriptableObject — randomised templates, token replacement)
  ├─ NotificationMessageCrafterManager.cs (Singleton MonoBehaviour wrapper for crafter SO)
  ├─ NotificationButtonUI.cs        (HUD button, swaps sprite on unread)
  ├─ NotificationPanelUI.cs         (Scroll panel, Open/Close/Toggle, rebuilds rows on change)
  ├─ NotificationRowUI.cs           (Single row — see architecture note below)
  └─ NotificationIconSet.cs         (ScriptableObject — type → sprite map)

GameSystems/SaveSystem/
  ├─ NotificationsSaveSection.cs    (ISaveSection — captures unread notifications into SaveSnapshot)
  └─ Data/NotificationsSaveData.cs  (Serialisable entry; producedOutputs omitted — ScriptableObject refs)

NotificationManager public API (as of May 8, 2026):
  AddNotification(type, title, message)
  AddNotification(type, title, message, bool showDeathIcon)
  AddNotification(type, title, message, Vector3 worldPosition)
  AddProductionCompletedNotification(title, message, List<ProductionOutputEntry>, Vector3 worldPosition = default)
  AddProductionPausedNotification(type, title, message, Vector3 worldPosition = default)
    └─ accepts ProductionPausedLackOfResources or ProductionPausedLackOfWorkers
  AddCraftingCompletedNotification(title, message, List<ProductionOutputEntry>, Vector3 worldPosition = default)
  AddCraftingFailedNotification(title, message, Vector3 worldPosition = default)
    └─ fires CraftingFailedWeather type
  SaveState() → NotificationsSaveData        (only unread notifications)
  LoadState(NotificationsSaveData)           (clears list, restores, fires OnNotificationsChanged)
  Passing worldPosition sets hasTileTarget = true → goToButton shows in row UI

NotificationMessageCrafterManager craft methods (as of May 8, 2026):
  Craft(type, EnvironmentControl, int populationLost)              — gathering / discovery
  CraftResearch(type, techName)
  CraftBirth(type, motherSurname, bornAlive, motherDied)
  CraftProduction(type, buildingName, planName)
  CraftBuilding(type, buildingName)
  CraftCrafting(type, recipeName, buildingName)                    — tokens: {RECIPE}, {BUILDING}
  CraftFireFight(type, targetName, casualties)                     — tokens: {NAME}, {CASUALTIES}
  CraftDiseaseOutbreak(diseaseName, causeType)                     — tokens: {DISEASE}, {CAUSE}
  CraftDiseaseKilled(diseaseName, surname)                         — tokens: {DISEASE}, {NAME}
  CraftUnitTrainingCompleted(unitName, count)                      — tokens: {UNIT}, {COUNT}
  CraftUnitSkillTrainingCompleted(groupName, unitName, skillLevel) — tokens: {GROUP}, {UNIT}, {LEVEL}
  CraftUnitTrainingFailedWeather(unitName, count, cause)           — tokens: {UNIT}, {COUNT}, {CAUSE}
  CraftUnitMovementCompleted(groupName, unitName)                  — tokens: {GROUP}, {UNIT}
  CraftUnitAttackActionCompleted(groupName, unitName, actionName)  — tokens: {GROUP}, {UNIT}, {ACTION}
  CraftUnitTargetedByAnimal(groupName, unitName, speciesName)      — tokens: {GROUP}, {UNIT}, {SPECIES}
  CraftUnitGroupDestroyed(groupName, unitName)                     — tokens: {GROUP}, {UNIT}
  CraftSpiritMoodChanged(spiritName, newMood, previousMood)        — tokens: {SPIRIT}, {MOOD}, {PREVIOUS}
  CraftSpiritSummoned(spiritName)                                  — tokens: {SPIRIT}
  CraftSpiritOfferingMade(spiritName, favorChange)                 — tokens: {SPIRIT}, {FAVOR}
  CraftAnimalRaidingBuilding(speciesName, buildingName)            — tokens: {SPECIES}, {BUILDING}
  CraftAnimalStorageRaided(speciesName, buildingName, amount)      — tokens: {SPECIES}, {BUILDING}, {AMOUNT}

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

### May 8, 2026 — RepellerZoneVisualizer (Warfare Mode Tile Highlight)

**Files changed/created:**
- `ScriptsUpdated/Enemies/Animals/RepellerZoneVisualizer.cs` *(new)*
- `ScriptsUpdated/GameSystems/GameManager/Installers/FinalSetupInstaller.cs`

**What was added:**

`RepellerZoneVisualizer` — MonoBehaviour added to the FinalSetup scene. Wired by `FinalSetupInstaller` to a button named `"RepellerZoneButton"` in the main UI canvas.

```
Behaviour:
  - Button only interactable while WorldCanvasMode.UnitsOnly == true (warfare mode)
  - Toggle ON  → finds all EnvironmentControl tiles, checks each coord against the
                  repelled set from AnimalRepellerRegistry, spawns a semi-transparent
                  light-red quad (Sprites/Default, alpha 0.35) flat on each tile
  - Toggle OFF → destroys all overlay quads
  - Leaves warfare mode → auto-hides overlays
```

**Inspector tuning:** `overlayScale` (match tile size), `yOffset`, `overlayColor`

**Editor setup required:**
1. Add `RepellerZoneVisualizer` component to a GameObject in the FinalSetup scene
2. Add a Button named `"RepellerZoneButton"` to the main UI canvas

---

### May 8, 2026 — Animal Food Storage Raiding + AnimalRepeller

**Files changed/created:**
- `ScriptsUpdated/Enemies/Animals/AnimalRepeller.cs` *(new)*
- `ScriptsUpdated/Enemies/Animals/AnimalRepellerRegistry.cs` *(new)*
- `ScriptsUpdated/Enemies/Animals/AnimalSimulation/AnimalSimulation.StorageRaids.cs` *(new)*
- `ScriptsUpdated/Enemies/Animals/AnimalSimulationController/AnimalSimulationController.StorageRaids.cs` *(new)*
- `ScriptsUpdated/Enemies/Animals/AnimalDefinition.cs`
- `ScriptsUpdated/Enemies/Animals/AnimalSimulation/AnimalSimulation.Core.cs`
- `ScriptsUpdated/Enemies/Animals/AnimalSimulation/AnimalSimulation.Decision.cs`
- `ScriptsUpdated/Enemies/Animals/AnimalSimulationController/AnimalSimulationController.Core.cs`
- `ScriptsUpdated/Enemies/Animals/AnimalSimulationController/AnimalSimulationController.TurnsAndSpawning.cs`
- Notification files (NotificationType, Crafter, CrafterManager, IconSet)

**AnimalDefinition — new "Food Storage Raiding" fields:**
```
raidsStorageForFood          — master toggle per species
storageRaidHungerThreshold   — hunger fraction to trigger raiding (default 0.5)
storageRaidRangeTiles        — tile scan radius (default 8)
foodStolenPerRaidAction      — units stolen × group size per raid tick
```

**AnimalSimulation.StorageRaids (new partial):**
```
HandleStorageRaiding(ref group, hungerPct)
  — checks adjacent tiles for edible food storage; fires OnGroupAttemptedStorageRaid
    when adjacent
  — scans within range for nearest food storage (skips repelled tiles); steps toward it
  — wired in Decision.cs after HandleHumanRaiding in the hunger branch
```

**AnimalSimulation.Core additions:**
```
OnGroupAttemptedStorageRaid event (animalId, storageTile, requestedAmount)
SetStorageFoodTiles(entries)   — pushed from controller each turn
SetRepelledTiles(tiles)        — pushed from controller each turn
IsTileRepelled(coord)          — checked in storage raid pathfinding
```

**AnimalRepeller / AnimalRepellerRegistry (new):**
```
AnimalRepeller        — MonoBehaviour, attach to any building
  repelRadiusTiles    — radius (default 2)
  OnEnable/Disable    — registers with AnimalRepellerRegistry

AnimalRepellerRegistry — static registry of active AnimalRepeller instances
```

**AnimalSimulationController.StorageRaids (new partial):**
```
RefreshStorageTiles()            — finds all StorageBuildingControl with Food items,
                                   builds tile→food map, pushes to simulation
RefreshRepelledTiles()           — iterates AnimalRepellerRegistry, builds repelled
                                   tile set, pushes to simulation
HandleGroupAttemptedStorageRaid  — removes edible food from storage, reduces animal
                                   hunger, refreshes storage cache, fires notification
IsEdibleForSpecies()             — checks species.edibleResources or accepts any Food
CountEdibleFood()                — counts food-type items in a StorageBuildingControl
```
Both refresh methods called from `HandleTurnEnded` each turn.

**New notification type:** `AnimalStorageRaided` — tokens `{SPECIES}`, `{BUILDING}`, `{AMOUNT}`

---

### May 8, 2026 — Animal Building Raid Notification

**Files changed:**
- `ScriptsUpdated/Enemies/Animals/AnimalSimulationController/AnimalSimulationController.BuildingAttacks.cs`
- Notification files

**New notification type:** `AnimalRaidingBuilding` — fires once when an animal first begins raiding a given building (or switches targets); tokens `{SPECIES}`, `{BUILDING}`

---

### May 8, 2026 — Notification Save/Load

**Files changed/created:**
- `ScriptsUpdated/GameSystems/SaveSystem/Data/NotificationsSaveData.cs` *(new)*
- `ScriptsUpdated/GameSystems/SaveSystem/NotificationsSaveSection.cs` *(new)*
- `ScriptsUpdated/GameSystems/SaveSystem/SaveSectionKeys.cs`
- `ScriptsUpdated/GameSystems/SaveSystem/EnvironmentSaveSections.cs`
- `ScriptsUpdated/GameSystems/SaveSystem/SaveSnapshot.cs`
- `ScriptsUpdated/GameSystems/SaveSystem/SaveSystem.cs`
- `ScriptsUpdated/Notifications/NotificationManager.cs`

**What was added:**

Unread notifications now persist across save/load. Only unread notifications are saved — read ones are discarded on save.

```
NotificationsSaveSection (ISaveSection)
  └─ CaptureInto() → NotificationManager.Instance.SaveState()
       └─ serialises each unread NotificationData into NotificationSaveEntry
            fields: type (int), title, message, isRead, turnNumber,
                    hasTileTarget, worldPositionX/Y/Z, showDeathIcon
            NOTE: producedOutputs NOT saved (ScriptableObject refs can't be JSON-serialised)
                  notification title/message body already contains the key info

SaveSystem changes:
  RegisterSections() → adds NotificationsSaveSection
  Write path → writes {stem}.notifications.json when snapshot.notifications != null
  Load path → reads .notifications.json, calls NotificationManager.Instance?.LoadState()
  BuildMetaForSnapshot → sets hasNotifications
  CloneMeta / SeedSnapshotCacheFromLoadedData → threads notifications through

NotificationManager changes:
  SaveState()  — iterates _notifications, skips isRead, returns NotificationsSaveData
  LoadState()  — clears list, deserialises entries, fires OnNotificationsChanged
  AddNotificationInternal — calls SaveSystem.MarkSectionDirty(Notifications) so incremental
                             saves capture new notifications without a full re-save
```

**Save file produced:** `<savename>.notifications.json`

---

### May 8, 2026 — Combat, Unit Death, and Animism Spirit Notifications

**Files changed:**
- `ScriptsUpdated/Buildings/BuildingsTypes/Warfare/UnitGroupActionManager.cs`
- `ScriptsUpdated/Enemies/Animals/AnimalSimulationController/AnimalSimulationController.UnitAttacks.cs`
- `ScriptsUpdated/Warfare/Militia/TileUnitGroupControl.cs`
- `ScriptsUpdated/Religion/PlayerReligionManager.cs`
- `ScriptsUpdated/Notifications/NotificationType.cs`
- `ScriptsUpdated/Notifications/NotificationMessageCrafter.cs`
- `ScriptsUpdated/Notifications/NotificationMessageCrafterManager.cs`
- `ScriptsUpdated/Notifications/NotificationIconSet.cs`

**New notification types:**

| Type | Fired by | Notes |
|------|----------|-------|
| `UnitAttackActionCompleted` | `UnitGroupActionManager.ProcessActionForTurn` | Only for `MeleeAttackActionSO` / `RangedAttackActionSO`; uses target tile position |
| `UnitTargetedByAnimal` | `AnimalSimulationController.HandleGroupAttackedPlayerUnitGroup` | Fires only on first hit per attacker per turn (not every tick); uses unit tile position |
| `UnitGroupDestroyed` | `TileUnitGroupControl.RemoveGroupDueToFatalities` | Single funnel for all death sources; neutral phrasing works for 1 unit or many |
| `SpiritMoodChanged` | `PlayerReligionManager.AddFavor` + `ApplyEndTurnDecay` | Fires only when favor crosses a mood threshold (Angry/Sad/Neutral/Pleased/Exalted) |
| `SpiritSummoned` | `PlayerReligionManager.TryAcceptSpirit` | Fires on successful spirit acceptance |
| `SpiritOfferingMade` | `PlayerReligionManager.TryOfferResource` + `TryOfferPopulationSacrifice` | Includes favor change amount; covers resource and population sacrifice offerings |

**`NotificationIconSet`:** six new entries — assign sprites in Inspector

---

### May 8, 2026 — Warfare Notifications (Training, Skill Training, Weather Failure, Movement)

**Files changed:**
- `ScriptsUpdated/Player/Tiles/Building/PlayerTrainingManager.cs`
- `ScriptsUpdated/Buildings/BuildingsTypes/Warfare/KineticWarfareControl/KineticWarfareControl.GroupSkillTraining.cs`
- `ScriptsUpdated/Buildings/BuildingsTypes/Warfare/KineticWarfareControl/KineticWarfareControl.Tornado.cs`
- `ScriptsUpdated/Buildings/BuildingsTypes/Warfare/UnitGroupMovementManager.cs`
- `ScriptsUpdated/Notifications/NotificationType.cs`
- `ScriptsUpdated/Notifications/NotificationMessageCrafter.cs`
- `ScriptsUpdated/Notifications/NotificationMessageCrafterManager.cs`
- `ScriptsUpdated/Notifications/NotificationIconSet.cs`

**New notification types:**

| Type | Fired by | Notes |
|------|----------|-------|
| `UnitTrainingCompleted` | `PlayerTrainingManager.ProcessCompletions` | Fires when new units successfully spawn; world pos = tile |
| `UnitSkillTrainingCompleted` | `KineticWarfareControl.CompleteGroupSkillTraining` | Fires after skill or advancement training; world pos = building tile |
| `UnitTrainingFailedWeather` | `KineticWarfareControl.CancelTrainingOrderForTornado` | Fires on tornado or fire cancellation; cause inferred from reason string |
| `UnitMovementCompleted` | `UnitGroupMovementManager.ProcessGroupMovementForTurn` | Fires only when non-patrol path is fully exhausted; world pos = destination tile |

All four pass world position → Go-To Tile button activates in the row UI.

**Craft methods added to `NotificationMessageCrafterManager`:**
- `CraftUnitTrainingCompleted(unitName, count)` — tokens: `{UNIT}`, `{COUNT}`
- `CraftUnitSkillTrainingCompleted(groupName, unitName, skillLevel)` — tokens: `{GROUP}`, `{UNIT}`, `{LEVEL}`
- `CraftUnitTrainingFailedWeather(unitName, count, cause)` — tokens: `{UNIT}`, `{COUNT}`, `{CAUSE}`
- `CraftUnitMovementCompleted(groupName, unitName)` — tokens: `{GROUP}`, `{UNIT}`

**`NotificationIconSet`:** four new entries — assign sprites in Inspector

---

### May 8, 2026 — Disease Death Notification + DiseaseOutbreak Death Icon Fix

**Files changed:**
- `ScriptsUpdated/DiseaseSystem/DiseaseManager.cs`
- `ScriptsUpdated/Notifications/NotificationType.cs`
- `ScriptsUpdated/Notifications/NotificationMessageCrafter.cs`
- `ScriptsUpdated/Notifications/NotificationMessageCrafterManager.cs`
- `ScriptsUpdated/Notifications/NotificationIconSet.cs`

**Bug fix — `DiseaseOutbreak` was showing the death icon:**
`PostDiseaseOutbreakNotification` was hardcoding `showDeathIcon: true`. Changed to `false` — a disease being applied to the population is not a death event.

**New notification type — `DiseaseKilledPopulation`:**
Fires from `DiseaseManager.KillIndividualFromDisease()` immediately after an individual dies.

```
KillIndividualFromDisease(person, disease, state)
  └─ PostDiseaseDeathNotification(diseaseName, surname)
       ├─ surname = person.Surname (falls back to "A citizen" if blank)
       ├─ CraftDiseaseKilled(diseaseName, surname) via NotificationMessageCrafterManager
       └─ NotificationManager.AddNotification(DiseaseKilledPopulation, title, message, showDeathIcon: true)
```

**`NotificationMessageCrafter` / `NotificationMessageCrafterManager`:**
- New `CraftDiseaseKilled(diseaseName, surname)` method added to both
- Tokens: `{DISEASE}`, `{NAME}`
- 4 randomised templates in `PopulateDefaults()`

**`NotificationIconSet`:** new entry for `DiseaseKilledPopulation` — assign sprite in Inspector

---

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

### May 7, 2026 — Building Fire Overlay Panel + Fight Mechanic + World Icon

**Files changed:**
- `ScriptsUpdated/Grid_Map/Weatherv2/Fire/BuildingFireState.cs`
- `ScriptsUpdated/Panels/BuildingPanel/BuildingFireOverlayControl.cs` *(new)*
- `ScriptsUpdated/Buildings/BuildingFireWorldIcon.cs` *(new)*
- `ScriptsUpdated/Panels/BuildingPanel/BuildingPanelControl/BuildingPanelControl.Mode.cs`
- `ScriptsUpdated/Panels/BuildingPanel/BuildingPanelControl/BuildingPanelControl.Events.cs`

---

**`BuildingFireState` — full fire fighting system:**

```
Designer fields:
  extinguishCost (List<ResourceCost>)  — resources spent to start fighting
  populationRequired (int)             — workers reserved for the duration
  baseFightTurns (int)                 — starting turn estimate
  rollMin / rollMax (int)              — roll range each turn (positive = progress,
                                         negative = setback)

Fight state (runtime):
  IsFighting (bool)
  FightTurnsRemaining (int)            — decrements/increments with each roll
  LastRollResult (int)                 — result of the most recent roll

Events:
  OnIgnited(BuildingFireState)
  OnFireDamageStep(BuildingFireState, int damage)
  OnExtinguished(BuildingFireState)
  OnFightProgress(BuildingFireState, int rollResult, int turnsRemaining)

TryBeginFighting():
  1. ResourceDeduction.Deduct(extinguishCost) — refunds on failure
  2. PlayersPopulationManager.TryReservePopulation(populationRequired, out reservationId)
  3. Subscribes to TurnSystem.onTurnEnd → OnEndTurn_FightFire()

OnEndTurn_FightFire():
  roll = Random.Range(rollMin, rollMax + 1)
  FightTurnsRemaining -= roll          — can go up (negative roll) or down
  fires OnFightProgress
  calls Extinguish() if FightTurnsRemaining <= 0

CancelFighting():
  releases population reservation, unsubscribes from TurnSystem

Extinguish():
  also calls StopFighting() to clean up any active fight reservation

Awake():
  auto-binds BuildingFireWorldIcon if found in children
```

---

**`BuildingFireOverlayControl` — two-phase overlay:**

Blocks the building panel while on fire. Two distinct phases:

```
Phase 1 — Idle (not yet fighting):
  costSection (active)    — BuildingCostEntry items from extinguishCost
  populationText          — "Workers needed: X  (available: Y)"
  turnsEstimateText       — "Est. turns to extinguish: ~N"
  fightButton             — gated on CanAffordFight() + HasEnoughPopulation()
  hintText                — "Not enough resources." / "Need X available workers."

Phase 2 — Fighting (after Fight Fire clicked):
  progressSection (active)
  fightProgressSlider     — value = baseFightTurns - FightTurnsRemaining
                            fills right on progress rolls, drops on setbacks
  populationText          — "Workers fighting: X"
  cancelButton            — calls CancelFighting(), returns to Phase 1

Auto-transitions:
  OnFightProgress → RefreshFightProgress() (slider update each turn)
  OnExtinguished  → Hide()
```

---

**`BuildingFireWorldIcon` (new) — world canvas icon:**

Attach to the building's world canvas (same as `ProductionBuildingControl` icons).
Auto-bound from `BuildingFireState.Awake()` via `GetComponentInChildren`.

```
Fields:
  fireIcon (GameObject)    — fire sprite, shown while IsOnFire
  fightTimerUI (TimerUI)   — radial fill, SetState(baseFightTurns, FightTurnsRemaining)
  fightTimerRoot (GameObject) — parent of fightTimerUI, shown only while IsFighting

Events handled:
  OnIgnited      → show fireIcon, hide fightTimerRoot
  OnFightProgress → show fightTimerRoot, update TimerUI fill
  OnExtinguished → hide both
```

---

**`BuildingPanelControl` changes:**
- `fireOverlayPanel (BuildingFireOverlayControl)` in `BuildingPanelControl.Mode.cs`
- `currentFireState` cached in `Show()`, subscribed to `OnIgnited` → `HandleFireIgnited`
- If already burning when panel opens → `fireOverlayPanel.ShowFor()` immediately
- `Unsubscribe()` releases fire event and nulls `currentFireState`

**Inspector setup:**
1. Add overlay child panel → attach `BuildingFireOverlayControl`, wire all fields
2. Assign `BuildingPanelControl.fireOverlayPanel`
3. Add `BuildingFireWorldIcon` to building's world canvas child; wire `fireIcon`, `fightTimerRoot`, `fightTimerUI`
4. Set `extinguishCost`, `populationRequired`, `baseFightTurns`, `rollMin`/`rollMax` on each `BuildingFireState`

### May 7, 2026 — Complete Fire System (Buildings + Tiles)

All fire-related work is committed and pushed. Below is the consolidated reference for the full system.

---

#### Fire State Components

Both `BuildingFireState` and `EnvironmentFireState` share an identical fight mechanic:

```
Designer fields:
  extinguishCost (List<ResourceCost>)      — resources spent to start fighting
  populationRequired (int)                 — workers reserved for the duration
  baseFightTurns (int)                     — starting fight turn estimate
  rollMin / rollMax (int)                  — progress roll range per turn
  baseCasualtyChance (float 0-1)           — base worker risk at full fire strength (default 0.30)
  casualtyReductionPerSafeRoll (float 0-1) — risk drop per safe turn (default 0.05)

Runtime state:
  IsFighting, FightTurnsRemaining, LastRollResult
  CasualtiesSoFar, CurrentCasualtyChance

Events:
  OnIgnited(state)
  OnExtinguished(state)
  OnFightProgress(state, rollResult, turnsRemaining)
  OnFightCasualty(state, totalCasualties)
  OnFireDamageStep(state, damage)           — BuildingFireState only

TryBeginFighting():
  1. ResourceDeduction.Deduct(extinguishCost) — refunds on population failure
  2. PlayersPopulationManager.TryReservePopulation(populationRequired)
  3. Subscribes TurnSystem.onTurnEnd → OnEndTurn_FightFire()
  4. Resets CasualtiesSoFar=0, CurrentCasualtyChance=baseCasualtyChance

OnEndTurn_FightFire() — per-turn logic:
  1. Progress roll: Random.Range(rollMin, rollMax+1) → FightTurnsRemaining -= roll
  2. Casualty roll:
     fireStrength = BurnTurnsRemaining / BaseBurnTurns
     effectiveRisk = CurrentCasualtyChance * fireStrength
     — casualty: CasualtiesSoFar++, OnFightCasualty fired
       if CasualtiesSoFar >= populationRequired → PostFightOutcomeNotification(false) → StopFighting()
     — safe: CurrentCasualtyChance -= casualtyReductionPerSafeRoll (floor 0)
  3. OnFightProgress fired
  4. if FightTurnsRemaining <= 0 → PostFightOutcomeNotification(true) → Extinguish()

PostFightOutcomeNotification(bool succeeded):
  — succeeded: FireFightSucceeded notification with casualty count
  — failed:    FireFightFailed notification (all workers lost)
  — uses transform.position → Go-To Tile button activates in row UI

CancelFighting(): releases population reservation, unsubscribes from TurnSystem
Extinguish(): calls StopFighting() before clearing fire state

BuildingFireState extras:
  — PostFireNotification() in TryIgnite() → BuildingOnFire notification
  — Auto-binds BuildingFireWorldIcon in Awake() via GetComponentInChildren
```

---

#### Overlay Panels

**`BuildingFireOverlayControl`** — blocks building panel while burning:
```
Phase 1 (idle):
  costSection       — BuildingCostEntry items (extinguishCost)
  populationText    — workers needed + available count
  turnsEstimateText — ~baseFightTurns estimate
  fightButton       — gated on CanAffordFight() + HasEnoughPopulation()

Phase 2 (fighting):
  fightProgressSlider — value = baseFightTurns - FightTurnsRemaining (fills on progress)
  populationText      — "Workers fighting: active / total"
  casualtyText        — "Lost: N" (red when > 0)
  riskText            — "Risk: N%" (green → red gradient)
  cancelButton        — CancelFighting() → returns to Phase 1

Auto: OnFightProgress → RefreshFightProgress() | OnExtinguished → Hide()
```

**`TileFireOverlayControl`** — identical to above but typed to `EnvironmentFireState`.
Only shown on discovered tiles. Undiscovered tiles use a plain `fireBlockOverlay` (no interaction).

---

#### World Canvas Icons

**`BuildingFireWorldIcon`** — attach to building world canvas:
```
fireIcon (GameObject)    — shown while IsOnFire
fightTimerUI (TimerUI)   — radial fill, SetState(baseFightTurns, FightTurnsRemaining)
fightTimerRoot           — parent of fightTimerUI, only shown while IsFighting
Auto-bound from BuildingFireState.Awake() via GetComponentInChildren
```

**`EnvironmentControl`** — fire UI added directly alongside discovery/survey/gathering timers:
```
fireIcon (GameObject)    — shown on OnIgnited, hidden on OnExtinguished
fireTimerUI (TimerUI)    — shown + updated on OnFightProgress, hidden on OnExtinguished
Auto-find: "FireIcon" and "FireFightIconTimer" children by name in OnValidate()
```

---

#### Panel Wiring

**`BuildingPanelControl`:**
- `fireOverlayPanel (BuildingFireOverlayControl)` in Mode.cs
- Caches `currentFireState`; subscribes `OnIgnited` → `HandleFireIgnited`
- Shows overlay immediately if burning on `Show()`; hides + unsubscribes on `Hide()`

**`UndiscoveredTilePanelControl`:**
- `fireBlockOverlay (GameObject)` — shown when `EnvironmentFireState.IsOnFire`
- Subscribes `OnIgnited` / `OnExtinguished` reactively while panel is open
- No fight interaction — tile is undiscovered

**`DiscoveredTilePanelControl`:**
- `fireOverlayPanel (TileFireOverlayControl)` — full fight UI
- Shows immediately if burning on `Show()`; `HandleFireIgnited` for reactive show
- `Hide()` closes overlay and unsubscribes

---

#### Notification Types Added (Fire System)

| Type | Fired by | Notes |
|------|----------|-------|
| `BuildingOnFire` | `BuildingFireState.TryIgnite()` | Includes world position → Go-To Tile |
| `FireFightSucceeded` | `PostFightOutcomeNotification(true)` | Includes casualty count |
| `FireFightFailed` | `PostFightOutcomeNotification(false)` | All workers lost |

**`NotificationMessageCrafterManager.CraftFireFight(type, targetName, casualties)`**
- Tokens: `{NAME}`, `{CASUALTIES}`
- Success handles zero-casualty case separately
- Used by both `BuildingFireState` and `EnvironmentFireState`

---

#### Inspector Setup Checklist

**Per building with `BuildingFireState`:**
- Set `extinguishCost`, `populationRequired`, `baseFightTurns`, `rollMin`, `rollMax`
- Set `baseCasualtyChance`, `casualtyReductionPerSafeRoll`
- Add `BuildingFireWorldIcon` child to world canvas; wire `fireIcon`, `fightTimerRoot`, `fightTimerUI`

**`BuildingPanelControl` scene object:**
- Add `BuildingFireOverlayControl` child panel; wire all fields
- Assign to `fireOverlayPanel`

**Environment tiles (`EnvironmentControl` / `EnvironmentFireState`):**
- Set fight fields on `EnvironmentFireState`
- Add `"FireIcon"` and `"FireFightIconTimer"` children to the environment canvas (auto-found by name)

**Tile panels:**
- `UndiscoveredTilePanelControl` → assign `fireBlockOverlay`
- `DiscoveredTilePanelControl` → assign `fireOverlayPanel` (`TileFireOverlayControl`)

**`NotificationIconSet` SO:** assign sprites for `BuildingOnFire`, `FireFightSucceeded`, `FireFightFailed`

---

### May 13, 2026 — Tech Encyclopedia Panel, Action Detail Panels, Camera Starting Point

#### Tech Panel — Technology Filter + Detail Panel

**Files created:**
- `ScriptsUpdated/Panels/TechPanel/TechnologyDetailPanelControl.cs` *(new)*
- `ScriptsUpdated/Panels/TechPanel/TechDetailValueRowUI.cs` *(new)*
- `ScriptsUpdated/Panels/TechPanel/TechTechnologyEntryUI.cs` *(new)*

**Files modified:**
- `ScriptsUpdated/Panels/TechPanel/TechPanelControl.cs`

**What was added:**

`TechPanelControl.PopulateTech()` was a stub — now implemented. The Tech filter tab shows all technologies the player knows AND is eligible for at their current level (`TechnologyManager.GetAll()` filtered by `IsKnown` + `IsEligibleForLevel`). Respects the existing `HandleLevelUp` event so the list refreshes on level-up.

```
TechTechnologyEntryUI
  — list row matching TechUnitEntryUI pattern
  — fields: icon (Image), nameText (TMP_Text), detailButton (Button)
  — Bind(tech, onClicked) sets display name (falls back to techID), icon
```

`TechnologyDetailPanelControl` — read-only detail panel, display only (no research actions):

```
Sections:
  Header        — icon (hidden if null), techName (falls back to techID), description
  Research Info — turns required, required knowledge, required player level,
                  required population (hidden when 0)
  Rewards       — knowledge reward + XP reward (section hidden when both 0)
  Research Costs — BuildingCostEntry rows with owned count (same as crafting/production panels)
  Researchable Buildings — TechBuildingEntryUI rows; shows "Any research building" if list is empty
  Effects       — TechDetailValueRowUI rows, one per effectSO:
                    World effect: resolves resource names, building names (BuildingManager),
                      crafting recipe names (CraftingRecipeManager), production plan names
                      (ProductionPlanManager), technology names (TechnologyManager), unit names
                    Health effect: signed deltas for health/recovery/resistance/lifespan per age group
                    Buildings effect: affected building names + health/degen deltas
                    Environment effect: filter scope (environments/tile types/sizes) + signed
                      discovery/gathering deltas and multipliers

TechDetailValueRowUI
  — generic reusable title+value row (two TMP_Text fields)
  — Setup(title, value) / Setup(title, value, Sprite icon)
  — used for Effects section; icon variant intentionally stripped down (no iconRoot)
```

Close button wired in `Awake()` → `Hide()`. `Hide()` destroys all instantiated rows (no memory buildup across tech switches).

**Inspector setup:**
- `TechPanelControl`: assign `techEntryPrefab` (TechTechnologyEntryUI) + `techDetailPanel` (TechnologyDetailPanelControl)
- `TechnologyDetailPanelControl`: `root`, `closeButton`, `techIconImage`, `techNameText`, `descriptionText`, stat texts, `rewardsSectionRoot`/`rewardsText`, `researchCostSectionRoot`/`researchCostContentRoot`/`costEntryPrefab` (BuildingCostEntry), `researchBuildingsSectionRoot`/`researchBuildingsContentRoot`/`buildingEntryPrefab` (TechBuildingEntryUI)/`anyBuildingText`, `effectsSectionRoot`/`effectsContentRoot`/`valueRowPrefab` (TechDetailValueRowUI)

---

#### Action Detail Panels — Ranged Attack + Action Router

**Files created:**
- `ScriptsUpdated/Panels/TechPanel/MeleeActionDetailPanel.cs` *(new — moved from untracked)*
- `ScriptsUpdated/Panels/TechPanel/RangedActionDetailPanel.cs` *(new)*

**Files modified:**
- `ScriptsUpdated/Panels/TechPanel/TechUnitActionDetailPanel.cs` (`ActionDetailPanelRouter`)

**What was added:**

`MeleeActionDetailPanel.durationText` — now shows the raw numeric value only (e.g. `2` not `2 turns`).

`RangedActionDetailPanel` — display-only panel for `RangedAttackActionSO`. No selected unit or target required; all values from SO only.

```
Sections: Header, Requirements (hidden if none), Targeting & Range, Timing & Damage,
          Hit Chance (hidden if useHitChance is false)
Hit chance shows: Base %, Min %, Max %, and readable modifier lines
  ("Higher Accuracy improves hit chance.", "Each tile of distance makes the shot harder.")
```

`ActionDetailPanelRouter` updated:
- New `rangedPanel (RangedActionDetailPanel)` field under `[Header("Type Panels")]`
- `RouteToPanel()` dispatches `RangedAttackActionSO` → `rangedPanel.ShowFor(ranged)`
- `HideAllPanels()` includes `rangedPanel?.Hide()`

---

#### Camera — Zoom Over UI + Starting Point Orbit

**Files modified:**
- `ScriptsUpdated/GameSystems/Cameras/CameraControl.cs`
- `ScriptsUpdated/Grid_Map/StartingPointPicker.cs`

**`CameraControl` changes:**

`Update()` — `HandleZoom()` moved outside the `IsCameraInputBlocked()` guard. Zoom now works even when the pointer is over UI (scroll wheel / pinch-to-zoom always active as long as `IsInputLocked` is false). Drag remains gated by `IsCameraInputBlocked()`.

New orbit-target API:
```
_hasOrbitTarget (bool) / _orbitTarget (Vector3)
SetOrbitTarget(Vector3 point)  — enables orbit mode
ClearOrbitTarget()             — restores normal minimap rotation

HandleMinimapRotation():
  _hasOrbitTarget == true  → transform.RotateAround(_orbitTarget, Vector3.up, yaw)
  _hasOrbitTarget == false → mainCamera.transform.Rotate(Vector3.up, yaw, Space.World)  (unchanged)
```

**`StartingPointPicker` changes:**

`LockCameraInput()` — replaced `PushInputLock()` with `SetTutorialInputRestrictions(true, false, true, true)`: drag blocked, zoom allowed, minimap rotation allowed.

`UnlockCameraInput()` — replaced `PopInputLock()` with `ClearTutorialInputRestrictions()` + `ClearOrbitTarget()`.

`ShowPreviewFor()` — calls `cameraControl.SetOrbitTarget(envGO.transform.position)` after `FocusOnPoint()`, updating the orbit pivot each time the player cycles to a new starter tile. Minimap drag now orbits the camera around the selected tile instead of spinning in place.

---

## 12. Resource Spawner System

See **SPAWNER_AUDIT.md** for the full reference.

**Added:** May 9, 2026  
**Location:** `ScriptsUpdated/Environment/ResourceSpawners/`  
**Editor scripts:** `Assets/Editor/ResourceSpawnerDefinitionCreator.cs`, `Assets/Editor/SavannaSpawnerCreator.cs`  
**Generated assets:** `Assets/Resources/ResourceSpawners/`, `Assets/ScriptableObjects/ResourceSpawners/`  
**Summary:** Resources now spawn from `ResourceSpawnerDefinition` ScriptableObjects instead of `ResourceDefinition` lists. `EnvironmentResourceNode` keeps `spawnedResources`/`ResourceSpawnEntry` unchanged — only the spawning decision layer changed. Three spawner types: permanent base spawners, event-triggered temporary spawners (fire, animal death, dung), and climate-conditional spawners driven by `ClimateManager` temperature/humidity curves.

---

### May 9, 2026 — Resource Spawner System

**Files created:**
- `ScriptsUpdated/Environment/ResourceSpawners/ResourceSpawnerDefinition.cs` *(new)*
- `ScriptsUpdated/Environment/ResourceSpawners/ResourceSpawnerRuntime.cs` *(new)*
- `ScriptsUpdated/Environment/ResourceSpawners/TileStateResourceSpawnerHandler.cs` *(new)*
- `ScriptsUpdated/Environment/ResourceSpawners/AnimalDeathResourceSpawnerHandler.cs` *(new)*
- `ScriptsUpdated/Environment/ResourceSpawners/AnimalDroppingHandler.cs` *(new)*
- `ScriptsUpdated/Environment/EnvironmentResourceNode/EnvironmentResourceNode.SpawnerAPI.cs` *(new partial)*
- `Assets/Editor/ResourceSpawnerDefinitionCreator.cs` *(new)*
- `Assets/Editor/SavannaSpawnerCreator.cs` *(new)*

**Files modified:**
- `ScriptsUpdated/Environment/EnvironmentResourceNode/EnvironmentResourceNode.Core.cs` — added `baseSpawners`, `activeSpawners`, `currentTileState` fields; `InitializeSpawners()` call in `Start()`
- `ScriptsUpdated/Environment/EnvironmentResourceNode/EnvironmentResourceNode.Spawning.cs` — `GenerateResources()` and `TickResourceLifecycle()` route to spawner system; legacy path kept as fallback

**What changed:**
Resources now spawn from `ResourceSpawnerDefinition` ScriptableObjects instead of `ResourceDefinition` lists. Three spawner types: permanent base spawners (on tile), event-triggered temporary spawners (fire/death/weather), and climate-conditional spawners. Climate multiplier reads from `ClimateManager` — temperature and humidity curves scale `baseSpawnChance` per tick. 30 base spawner SOs and 9 Savanna SOs created by editor menu scripts. Dung → Dried Dung conversion system added as a tile-level MonoBehaviour.

---

### May 17, 2026 — Population Count Fix (Health-Zero & Disease Deaths)

**Files modified:**
- `ScriptsUpdated/Player/Population/PlayerAggregatedPopulationSimulationManager.cs`

**What was fixed:**

Two bugs caused the total population display and available/used worker counts to fall out of sync after non-disease deaths.

**Bug 1 — Health-zero deaths not propagated to FamilySim:**

`TickHealth_Player` calls `group.ApplyPopulationLoss(group.count)` when `averageHealth <= 0`, but the lost count was never added to `deathsByGroup`. This meant `fam.ApplyDeathsToIndividuals` was never called for those individuals — leaving `Individual.IsAlive = true` while the aggregate group count dropped to 0. `GetTotalTaskPool()` and `GetAvailableTaskPopulation()` both iterate live `Individual` objects, so they over-counted after health-zero group deaths.

**Fix:** After `TickHealth_Player`, the delta `beforeHealthCount - group.count` is captured and merged into `deathsByGroup`:

```csharp
int healthZeroDeaths = beforeHealthCount - group.count;
if (healthZeroDeaths > 0)
{
    if (!deathsByGroup.TryAdd(group.GroupID, healthZeroDeaths))
        deathsByGroup[group.GroupID] += healthZeroDeaths;
}
```

**Bug 2 — UI not refreshed when deaths don't empty a group:**

`PruneDeadOrEmptyGroups` only calls `MarkUIDirty` when it removes at least one group. If a mortality roll killed some members of a group but left it non-empty, the population display could stay stale for the rest of the turn.

**Fix:** `playerPop.MarkUIDirty()` is called at the end of `AdvanceTurn` whenever `deathsByGroup` is non-empty. The batch system (`_batchDepth`) defers the actual UI sync to `EndTurnBatch` at end of turn, so it fires once with the final post-death, post-birth state.

**Affected counters:** `populationDisplayText` (total / cap), `availableText` (available / task pool), and any subscriber to `OnPopulationChanged`.

**Note:** Disease deaths via `DiseaseManager.KillIndividualFromDisease` were already handled correctly (sets `IsAlive = false`, calls `group.ApplyPopulationLoss(1)`, and calls `pop.MarkUIDirty()` immediately). No changes needed there.

---

### May 17, 2026 — Environment Tech Delta Fixes (Discovery & Gathering)

**Files modified:**
- `ScriptsUpdated/Technology/Effects/EnvironmentTechEffectSO.cs`
- `ScriptsUpdated/Player/Research/Tech/PlayerTechBuffs.cs` *(no logic change — already correct)*
- `ScriptsUpdated/Player/Tiles/PlayerDiscoveryManager.cs`
- `ScriptsUpdated/Player/Tiles/PlayerGatheringManager.cs`
- `ScriptsUpdated/Environment/EnvironmentControl.cs`
- `ScriptsUpdated/Panels/EnvironmentPanel/DiscoveryDetailsPanelControl.cs`
- `ScriptsUpdated/Panels/EnvironmentPanel/GatheringDetailsPanelControl.cs`
- `ScriptsUpdated/Panels/TechPanel/TechnologyDetailPanelControl.cs`

**Sign convention (established / enforced):**

All environment tech delta fields follow: **positive value = reduction, negative value = increase**.

```
discoveryFailureDeltaPct = 2   →  failure 22% → 20%   ✅
gatheringTurnsDelta = 3        →  turns 8 → 5          ✅
discoveryRequiredPopDelta = 2  →  pop req 5 → 3        ✅
```

The computation in `PlayerTechBuffs` was already correct (`result = base - sum(deltas)`). The fixes addressed three separate issues:

**Fix 1 — Live re-evaluation each turn (PlayerDiscoveryManager, PlayerGatheringManager):**

`effectiveFailureChance` was baked into the task at start time. If a tech was researched mid-task, the failure roll used the old (un-buffed) value. Now each turn's failure roll calls `env.GetEffectiveDiscovery()` / `env.GetEffectiveGathering()` live so newly researched techs apply immediately. `info.effectiveFailureChance` is preserved unchanged for XP calculation (reflects difficulty at task start).

**Fix 2 — Tooltip clarification (EnvironmentTechEffectSO):**

Added `[Tooltip]` attributes to `discoveryFailureDeltaPct`, `discoveryTurnsDelta`, `gatheringFailureDeltaPct`, and `gatheringTurnsDelta` so the sign convention is visible in the Unity Inspector.

**Fix 3 — Tech panel display sign (TechnologyDetailPanelControl):**

All delta fields were displayed with their raw SO value (`+2` for a `discoveryFailureDeltaPct = 2`). Since positive = reduction, the display now negates the value so a delta of 2 shows as `-2%` — correctly indicating the stat goes down.

**Fix 4 — Detail panel display and base comparison (DiscoveryDetailsPanelControl, GatheringDetailsPanelControl, EnvironmentControl):**

Added `GetPreTechDiscovery()` / `GetPreTechGathering()` to `EnvironmentControl`. These return values after seasonal + predator adjustments but before tech buffs — providing the correct base for comparison. Both detail panels now use:
- `BaseDiscoveryRequiredPop` / `BaseGatheringRequiredPop` (cached base, consistent with `GetEffective...`)
- `GetPreTechDiscovery` / `GetPreTechGathering` for turns and failure base

Display shows the effective value only (e.g. `20%`), not a delta suffix.

---

---

### May 18, 2026 — Save-on-Exit & Social Buttons (ProfilePanelControl)

**Files modified:**
- `ScriptsUpdated/Panels/Profile/ProfilePanelControl.cs`

**What was fixed/added:**

**1. Save before returning to title screen:**

`ReturnToTitleScreen()` previously called `SceneManager.LoadScene` immediately with no save. Now starts `SaveThenReturnToTitleCoroutine()`:

```
SaveThenReturnToTitleCoroutine:
  1. Hides panels, pops input lock
  2. SaveSystem.SaveCloseGameNow()
  3. Waits while SaveSystem.IsSaving == true
  4. ScoreManager.Instance?.CommitScoreToLeaderboard(...)
  5. SceneManager.LoadScene(titleSceneName, Single)
```

Guard flag `_returningToTitle` prevents double-trigger.

**2. TikTok button:**

Added alongside Patreon and Facebook under the `[Header("Support")]` block:
- `tiktokButton (Button)` — public serialized field
- `tiktokUrl (string)` — serialized, defaults to `"https://www.tiktok.com/@celtstudio"`
- `OpenTikTokPage()` — wired in `Awake()` onClick

**3. Save on social button click:**

All three social open methods (`OpenPatreonPage`, `OpenFacebookPage`, `OpenTikTokPage`) now call `SaveSystem.SaveCloseGameNow()` before `Application.OpenURL()`. Fire-and-forget (no wait needed — background write finishes while the browser is open).

**App backgrounding** (already in `GameSceneManager`, confirmed working):
- `OnApplicationPause(true)` → `SaveSystem.SaveCloseGameNow()` (all platforms)
- `OnApplicationFocus(false)` → `SaveSystem.SaveCloseGameNow()` (Android/iOS only, as backup)
- `Application.wantsToQuit` → `SaveThenQuitCoroutine()` (blocks quit until write completes)

---

### May 18, 2026 — ScoreManager & Scoreboard System

**Files created:**
- `ScriptsUpdated/GameSystems/Score/ScoreManager.cs` *(new)*
- `ScriptsUpdated/GameSystems/Score/ScoreboardData.cs` *(new)*
- `ScriptsUpdated/Panels/Profile/ScoreboardEntryUI.cs` *(new)*

**Files modified:**
- `ScriptsUpdated/GameSystems/SaveSystem/EnvironmentSaveSections.cs`
- `ScriptsUpdated/GameSystems/SaveSystem/CoreSystemsSaveSection.cs`
- `ScriptsUpdated/GameSystems/SaveSystem/SaveSystem.cs`
- `ScriptsUpdated/GameSystems/GameManager/GameSceneManager.cs`
- `ScriptsUpdated/Buildings/Repair/BuildingRepair.cs`
- `ScriptsUpdated/Player/Tiles/Building/PlayerProductionManager.cs`
- `ScriptsUpdated/Player/Tiles/Building/PlayerCraftingManager.cs`
- `ScriptsUpdated/Player/Tiles/Building/PlayerTrainingManager.cs`
- `ScriptsUpdated/Player/Population/PlayersPopulationManager.cs`
- `ScriptsUpdated/Player/Population/FamilySim/Core/PlayerFamilySimulationManager.cs`
- `ScriptsUpdated/Player/Research/PlayerResearchManager.cs`
- `ScriptsUpdated/Panels/Profile/ProfilePanelControl.cs`

---

#### ScoreManager (new singleton)

```
ScoreManager
├─ Singleton MonoBehaviour — place on any manager GameObject in the manager scene
├─ Configurable point values (Inspector):
│  ├─ discoveryPoints      = 10
│  ├─ gatheringPoints      = 5
│  ├─ craftingPoints       = 15
│  ├─ productionCyclePoints= 8
│  ├─ birthPoints          = 20
│  ├─ buildingCompletePoints = 25
│  ├─ buildingRepairPoints = 10
│  ├─ trainingPoints       = 20
│  ├─ combatVictoryPoints  = 15   (hook: ScoreManager.NotifyCombatVictory())
│  ├─ researchPoints       = 30
│  └─ populationAgedPoints = 5
├─ CurrentScore (int, read-only property)
├─ OnScoreChanged (event Action<int>)
├─ _gameStarted flag — score events are ignored until GameSceneManager calls OnGameStarted()
│  └─ prevents points firing during save-load phase when managers register/fire events
├─ Save/Load: SaveState() → int, LoadState(int) — integrated into CoreSystems section
└─ Leaderboard: persists to Application.persistentDataPath/scoreboard.json (plain JSON, not encrypted)
```

**Event-based hooks (subscribed in Start, unsubscribed in OnDestroy):**

| Event | Manager | Points |
|-------|---------|--------|
| `PlayerDiscoveryManager.OnDiscoveryCompleted` | PlayerDiscoveryManager | discoveryPoints |
| `PlayerGatheringManager.OnGatheringCompleted` | PlayerGatheringManager | gatheringPoints |
| `PlayerBuildingManager.OnBuildingPlaced` | PlayerBuildingManager | buildingCompletePoints |

**Direct-call hooks (called from within manager code):**

| Static method | Called from | Points |
|--------------|-------------|--------|
| `ScoreManager.NotifyCraftCompleted()` | `PlayerCraftingManager.ProcessCompletions()` after `OnOrderFinalizedExternally` | craftingPoints |
| `ScoreManager.NotifyProductionCycle()` | `PlayerProductionManager.OnProductionCycleCompleted()` before `return true` | productionCyclePoints |
| `ScoreManager.NotifyBirth()` | `PlayersPopulationManager.AddBirthAndReturnGroup()` before `return g` | birthPoints |
| `ScoreManager.NotifyBuildingRepaired()` | `BuildingRepair.FinishJob()` after `OnRepairCompleted?.Invoke()` | buildingRepairPoints |
| `ScoreManager.NotifyTrainingCompleted()` | `PlayerTrainingManager.ProcessCompletions()` inside `if (spawned)` | trainingPoints |
| `ScoreManager.NotifyResearchCompleted()` | `PlayerResearchManager.Complete()` after `PostResearchNotification` | researchPoints |
| `ScoreManager.NotifyPopulationAged()` | `PlayerFamilySimulationManager` inside `if (newGroup != oldGroup)` | populationAgedPoints |
| `ScoreManager.NotifyCombatVictory()` | *Not yet wired — call from player-unit combat resolution* | combatVictoryPoints |

**Leaderboard API:**
```
CommitScoreToLeaderboard(playerName, civName, avatarName)
  — appends entry, sorts descending by score, trims to 5
  — only commits if CurrentScore > 0
  — called from ProfilePanelControl.SaveThenReturnToTitleCoroutine() before scene load

GetLeaderboard() → ScoreboardData
  — reads scoreboard.json; returns empty ScoreboardData if file missing or corrupt
```

---

#### ScoreboardData / ScoreboardEntry (new)

```csharp
[Serializable] class ScoreboardData  { List<ScoreboardEntry> entries; }
[Serializable] class ScoreboardEntry { int score; string playerName; string civilizationName; string avatarName; }
```

Stored at `Application.persistentDataPath/scoreboard.json` — plain JSON, survives across game sessions, independent of the main save files.

---

#### ScoreboardEntryUI (new)

MonoBehaviour component for each scoreboard row prefab:

```
Fields (wire in Inspector):
  rankText       (TMP_Text)   — "1" through "5"
  playerNameText (TMP_Text)
  civNameText    (TMP_Text)
  scoreText      (TMP_Text)
  profileImage   (Image)      — avatar sprite resolved by name from ProfilePanelControl.availableAvatars

Methods:
  SetEntry(int rank, ScoreboardEntry entry, Sprite avatar)
  SetEmpty(int rank)
```

---

#### ProfilePanelControl — score display additions

New Inspector fields:
```
[Header("Score")]
  currentScoreText        (TMP_Text)  — displays live CurrentScore, updated via OnScoreChanged event
  scoreboardContentParent (Transform) — Content transform of the scoreboard ScrollView
  scoreboardEntryPrefab   (GameObject)— prefab with ScoreboardEntryUI component
```

`RefreshScoreDisplay()` — called on `ShowProfilePanel()` and via `OnScoreChanged`:
- Updates `currentScoreText` (formatted with "N0" — thousands separator)
- Calls `RefreshScoreboardEntries()`

`RefreshScoreboardEntries()`:
- Destroys all existing children of `scoreboardContentParent`
- Reads leaderboard from `ScoreManager.Instance.GetLeaderboard()`
- If empty — does nothing (no placeholder rows shown)
- Instantiates 1–5 entries (only as many as there are actual scores)
- Resolves avatar sprite by name from `availableAvatars`

`OnEnable` / `OnDisable` — subscribes / unsubscribes `ScoreManager.Instance.OnScoreChanged`

**Save integration:**
- `int currentScore` added to `CoreSystemsSectionSaveData`
- `CoreSystemsSaveSection.CaptureInto()` → `ScoreManager.Instance.SaveState()`
- `SaveSystem.LoadWorldStateCoroutine()` → `ScoreManager.Instance.LoadState(core.currentScore)`

**Startup guard:**
- `GameSceneManager.RunStartupRoutine()` calls `ScoreManager.Instance?.OnGameStarted()` just before `_startupComplete = true`
- Score events fired before this point (e.g. during load) are silently ignored

**Inspector setup required:**
1. Add `ScoreManager` component to a manager scene GameObject
2. Wire `currentScoreText` (TMP_Text) on `ProfilePanelControl`
3. Set up a ScrollView; assign its `Content` to `scoreboardContentParent`
4. Build a `ScoreboardEntryUI` prefab; assign to `scoreboardEntryPrefab`
5. Give the Content a `Vertical Layout Group` + `Content Size Fitter (Preferred Size)` so it grows with entries

---

**End of Report**

*Status: Ready for Ruflo Integration*  
*Last Updated: May 18, 2026 (ScoreManager system, scoreboard UI, save-on-exit improvements, TikTok button)*  
*Audit Confidence: High (comprehensive read-only scan)*
