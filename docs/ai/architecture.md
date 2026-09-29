# Architecture

## Repository Shape

ChemLabSim is a Unity project with authored gameplay code concentrated in:

- `Assets/_Project`: legacy implementation
- `Assets/_ProjectV3`: newer architecture and systems
- `Assets/Editor`: shared editor utilities
- `Tools`: Python support scripts

Other major repository areas:

- `Packages`: Unity package manifest and lock data
- `ProjectSettings`: Unity configuration
- `Builds`: historical build outputs
- `Logs`: historical editor and build logs
- `Library`, `Temp`, `UserSettings`: generated Unity artifacts

## Active Architectures

The repository currently contains two real architectures in parallel.

### Legacy `_Project`

Characteristics:

- scene-driven MonoBehaviour architecture
- central runtime manager via `AppManager`
- secure data loading via `SecureReactionLoader`
- reaction logic via `ReactionEvaluator`
- large orchestration surface in `LabController`

Primary legacy flow:

1. `Boot.unity`
2. `AppManager`
3. `SecureReactionLoader`
4. `ReactionDB`
5. `LabController`
6. `ReactionEvaluator`
7. UI, history, progress, and feedback updates

### V3 `_ProjectV3`

Characteristics:

- layered structure
- controllers and views split
- service locator and event bus infrastructure
- reaction and chemistry engines separated from UI
- JSON-driven content for progression systems

Primary V3 flow:

1. `V3Bootstrap`
2. service initialization
3. controller discovery
4. UI/input controllers produce `MixRequest`
5. `ReactionController`
6. `ReactionEngine` or `ChemistryEngine`
7. event publication
8. view, progress, notebook, quiz, achievement, and FX updates
9. `SaveService` persistence

## Layer Organization

### Legacy layers

- bootstrap: `AppManager`, boot scenes
- data: `ReactionModels`, `ChemicalMaterialModels`, `SecureReactionLoader`
- security: `CryptoUtil`, `KeyMaterial`
- simulation: `ReactionEvaluator`
- presentation/orchestration: `LabController`, menu and UI helpers

### V3 layers

- views: `Assets/_ProjectV3/Scripts/Views`
- controllers: `Assets/_ProjectV3/Scripts/Controllers`
- engine: `Assets/_ProjectV3/Scripts/Engine`
- domain-oriented models and simulation: `Assets/_ProjectV3/Scripts/Domain`
- services: `Assets/_ProjectV3/Scripts/Services`
- core infrastructure: service locator, event bus, bootstrap, contracts
- editor/build tooling: `Assets/_ProjectV3/Scripts/Editor`

## Major Systems Map

- startup and scene boot
- secure reaction database
- reaction matching and evaluation
- chemistry simulation and live stepping
- UI input and result presentation
- progress, objectives, challenges, achievements, notebook
- save/load and local persistence
- audio and VFX
- localization and RTL support
- guided experiments and quiz content
- editor automation and build tooling

## Data Flow

### Reaction data

- authoritative authored chemistry data lives in `Assets/_Project/DataSrc/reactions.json`
- runtime secure payload lives in `Assets/_Project/DataSecure/reactions.bytes`
- `SecureReactionLoader` decrypts and validates payloads for runtime use

### V3 content data

JSON in `Assets/_ProjectV3/Resources/Data` drives:

- achievements
- challenges
- guided experiments
- lessons
- quiz questions

### Save data

- V3 save state is persisted through `SaveService`
- state is serialized to a single PlayerPrefs JSON payload
- progress-related controllers publish events that trigger save updates

## Dependency Structure

Assembly definitions:

- `ChemLabSim.Core`
- `ChemLabSimV3`
- `ChemLabSimV3.EditModeTests`
- RTLTMPro runtime, editor, and tests

Observed dependency direction:

- `ChemLabSimV3` depends on `ChemLabSim.Core`
- both architectures depend on Unity APIs
- V3 still bridges into legacy runtime data through `AppManager` and `ReactionDB`

## Scenes and Runtime Boundaries

Scenes enabled in build settings:

- `Assets/Boot.unity`
- `Assets/_Project/Scenes/Menu.unity`
- `Assets/Lab Scene.unity`
- `Assets/_ProjectV3/Scenes/LabV3.unity`

Important runtime note:

- `SceneService` references `Achievements` and `Settings` scene names, but those scene assets were not found during repository analysis.

## Architectural Constraints

- Both legacy and V3 systems coexist and are relevant.
- V3 is not fully isolated from legacy data/bootstrap paths.
- Custom gameplay prefabs are minimal in the repository; a significant amount of runtime composition is scene-bound or code-driven.
