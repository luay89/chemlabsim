# Project Status

Last reviewed: `2026-10-13`

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
- 2026-09-30: User still could not find NaOH after PR #1. Editor.log shows data is fine (137 reactions, 90 reagents, 124 materials); the list is sorted by English name, so NaOH sits ~70 rows down as "Sodium Hydroxide  (NaOH)". `ReagentDropdownView` now builds a search box at runtime next to each reagent label (filters by name or formula, best match auto-selected) and wheel sensitivity is 60. Compiles with Unity's Roslyn; Play Mode check by the user pending.
- 2026-09-30: Added `rxn_139` NaOH + H2O dissolution (exothermic, modelled on `rxn_040`); database is now 138 reactions. `reactions.bytes` and manifest regenerated outside Unity with the same AES-CBC+HMAC scheme (old blob verified to decrypt to the old JSON first). Play Mode check pending.
- 2026-09-30: Batch 001 added 292 reactions (430 total, 114 reagents): acid-base, oxides, metals + acids, carbonates, precipitation, displacement, combustion/synthesis, high-temperature reductions, solution redox, organic, dissolution. Written with `tools/reactions/` (`reactions_tool.py add batch_001` checks atom balance, duplicates, known reagents and slider range, then re-encrypts `reactions.bytes`). New entries leave `requiredMedium` empty so no medium guessing is needed. Play Mode check pending.
- 2026-09-30: V0 3D bench slice: `Views/Bench3D/LabBench3D.cs` auto-creates (RuntimeInitializeOnLoad, LabV3 only) a dark bench, a procedural glass beaker, a `ChemLabSim/Liquid` liquid column and particle emitters (bubbles, steam, smoke, sparks) plus heat glow/frost, all driven by existing events (FxTriggered, EnvironmentChanged/FxChanged, MaterialPreviewChanged). It moves Main Camera and disables the legacy `ReactionFxView`. Materials are created at runtime, so player builds may need these shaders marked Always Included later. Play Mode check pending.
- 2026-09-29: CI Documentation Check no longer requires `AGENTS.md`; that file is local-only by design (`.gitignore`), so the check failed on every run.

- 2026-10-13: Product direction set by the user: the goal is to make chemistry simple by letting the student see the reaction happen. Platform order: Android first, then WebGL (teachers/desktop), then iOS. An AI explain/help assistant comes last and needs a backend proxy (no API key in the client).
- 2026-10-13 Android slice 1 (branch `android/slice-1-mobile-lab`):
  - `Views/Layout/LabScreenLayout.cs` (runtime, LabV3 only) keeps the beaker in the centre: Lab Controls on the left (scaled down when taller than the screen), one right column with tabs Result / Progress (scroll lists that hold the existing panels, text rows grow with their text), hint at the bottom centre, everything inside `Screen.safeArea`. Phones (<7 inch) use a 1280x720 reference and match height.
  - `Views/Shared/SymbolFontFallback.cs` registers `Resources/Fonts/DejaVuSans.ttf` as a global dynamic TMP fallback, so ✓ ✗ ⚠ ▶ ▸ ● ■ ⁺ ₂ no longer show as □. Emoji prefixes removed from guided-experiment labels.
  - `ChemLiquid.shader`: the solid band no longer draws at the bottom cap when there is no solid (it was the white noisy disk), and foam grain is scaled by a new `_FoamAmount` (it was the blocky surface).
  - Stirring/Grinding value labels show only "50%" (the row title already names them).
  - Tests: 160/160 EditMode pass. Fixed `MolecularSimulator.BuildFromFormula` (2-element formulas dropped atoms of the first element and reused an atom id; H2O now O + 2 H), `TemperatureCondition` (gap exactly at the 15 °C window edge is Fail, not a 0-factor Partial), `ServiceLocatorTests` expects the intentional error log.
  - `Editor/AndroidBuild.cs` (menu ChemLabSim V3 > Build Android APK, or batch `-buildTarget Android -executeMethod ChemLabSimV3.Editor.AndroidBuild.BuildApk`): LabV3 only, `com.chemlabsim.app`, IL2CPP ARM64, API 24-34, landscape. Writes `Resources/RuntimeShaders/*.mat` so the shaders the bench creates in code survive the player build. Output `Builds/Android/ChemLabSim.apk` (debug-signed, git-ignored).
  - Not changed (known): HCl + H2O dilution is Partial because of the generic contact factor; liquid-only mixes probably should not need stirring/grinding (engine/data decision, deferred).

## Factual Constraints For Future Tasks

- the worktree may be dirty; unrelated edits already exist
- legacy and V3 systems are both relevant
- AI sessions should update this file when project state materially changes
