# Project Status

Last reviewed: `2026-08-20`

## High-Level Summary

ChemLabSim is a Unity chemistry simulation project with:

- a legacy production path in `Assets/_Project`
- a newer V3 architecture in `Assets/_ProjectV3`
- secure reaction-data loading
- strong authored chemistry data coverage
- broad V3 gameplay/support systems with uneven implementation completeness

As of 2026-08-20, V3 execution is governed by a mandatory constitution at `docs/ai/constitution-v3.md` that enforces simulation-driven visuals, strict architecture boundaries, error-budget discipline, and 3-level testing evidence on every task.

## Repository Snapshot

- folders including root: `2166`
- files: `35105`
- authored gameplay/dev source files in primary authored areas: `182`
- C# files under `Assets`: `241`
- enabled build scenes: `4`
- custom gameplay prefabs detected: `0`
- TextMesh Pro example prefabs detected: `3`

## Current System Status

### Complete or largely present

- bootstrapping and startup flow
- secure reaction data pipeline
- legacy reaction evaluation
- V3 reaction engine
- save/load persistence
- progress, objective, challenge, achievement, and notebook systems
- editor/build automation presence

### Partial

- advanced chemistry simulation depth
- V3 UI composition maturity
- audio service implementation
- localization in current runtime behavior
- guided experiment delivery layer
- molecular and visual simulation polish

### Missing from current tree

- dedicated `Achievements` scene asset
- dedicated `Settings` scene asset

## Largest Code Areas

- legacy `LabController.cs` is the largest gameplay module at `4061` lines
- V3 has significant complexity in:
  - `UIController`
  - `ReactionVesselView`
  - `SimulationStepper`
  - molecular simulation classes
  - scene/editor setup scripts

## Data Status

Primary JSON content:

- `Assets/_Project/DataSrc/reactions.json`: `10013` lines
- `Assets/_Project/DataSrc/materials.json`: `2873` lines
- V3 content JSON total: `694` lines

V3 runtime resource packs include:

- `26` audio clips in `Assets/_ProjectV3/Resources/Audio`

## Build Confidence

Current-session build status: not revalidated by running a build in this session.

Observed evidence:

- repository contains prior Unity batch build logs showing success
- assembly definitions and package manifest are coherent
- historical logs indicate successful Linux V3 and batch builds

Assessment:

- the project appears likely to compile
- definitive compile confirmation still requires running a build or compile step

## 2026-09-27 Read-Only Review Findings

Full report: [reports/2026-09-27-v3-understanding-report.md](reports/2026-09-27-v3-understanding-report.md)

- `LabV3.unity` has an `AppRoot` (SecureReactionLoader + AppManager) wired to `reactions.bytes`; Play Mode verification still pending.
- Found: runtime loaded only 12 reactions because `reactions.bytes` (2026-04-04) predated the 147-record `reactions.json`, and `reactions.json` was a top-level array that `JsonUtility` → `ReactionDB` cannot parse.
- Fixed 2026-09-27: `reactions.json` wrapped as `{"reactions": [...]}` (records unchanged) and `reactions.bytes` + manifest regenerated with the project's own `CryptoUtil`/`KeyMaterial`; decrypt check = 147. Backups of the previous files: `Backups/reactions-data-2026-09-27/`. Play Mode confirmation pending.
- Known side effect: 20 single-reactant records make `AppManager.ValidateLoadedDatabase` log an error; the DB is still exposed because it is assigned before validation.
- CI data-validation step now requires the object format (2026-09-27).
- `AppManager.ValidateLoadedDatabase` now scans all entries; single-reactant reactions produce one warning instead of an error (2026-09-27).
- `TemperatureSliderView` max raised from 100°C to 1000°C (matches engine limits); 22 two-reactant reactions needed >100°C (2026-09-27).
- Unity batch compile (2022.3.62f3): 0 errors. EditMode: 160 tests, 156 pass, 4 fail, all unrelated to these changes: `ServiceLocatorTests.Get_UnregisteredService_ReturnsNull` (unexpected error log), `ConditionPipelineTests.Evaluate_WithLowTemperature_BelowActivation` and `ReactionEngineTests.Process_WithLowTemperature_ReturnsFail` (Partial vs Fail), `MolecularSimulatorTests.BuildFromFormula_ParsesH2O` (2 vs 3).
- Stage 2 data corrections applied: 16 equations fixed, 10 duplicate records removed (147 -> 137), `reactions.bytes` regenerated and verified; details and deferred items in [reports/2026-09-27-reaction-data-stage2.md](reports/2026-09-27-reaction-data-stage2.md). Compile 0 errors; EditMode unchanged (156/160).
- Decomposition-mode plan: (1) slider range, done; (2) data corrections + dedupe, done; (3) single-reactant thermal/catalytic mode; (4) electric-current condition for electrolysis.
- A second DB load exists in `V3Bootstrap` (for `ChemistryEngine`); `ProductionBootstrapper` has no callers.
- 2026-09-29 (PR #1): LabV3 reagent dropdowns looked truncated (NaOH unreachable). Cause: the dropdown template `ScrollRect` had scroll sensitivity 1, no scrollbar. `ReagentDropdownView` now sets vertical-only, clamped, sensitivity 30, 280px height and adds a scrollbar at runtime. `readyMix` hint no longer uses U+2713 (missing from LiberationSans SDF). Other labels still use ✓/✗/⚠ and may log the same warning. Verified in cloud: `reactions.bytes` decrypts to exactly `reactions.json` (137 reactions, 90 reactants incl. NaOH); changed C# compiles against API stubs. Play Mode check pending.
- 2026-09-29: `AGENTS.md` committed to the repo (was only local), which the CI Documentation Check requires.

## Factual Constraints For Future Tasks

- the worktree may be dirty; unrelated edits already exist
- legacy and V3 systems are both relevant
- AI sessions should update this file when project state materially changes
