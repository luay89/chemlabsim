# Project Status

Last reviewed: `2026-10-19`

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

## Stage 5 — quantities, stoichiometry, instruments (2026-10-14, branch `android/slice-5a-quantities`)

Stage 5 acceptance gate passed on 2026-10-15 (EditMode 312/312; LinuxPreview tour desktop and 20:9 phone, 0 exceptions, 23/23 UI checks each; Linux and Android builds). Done and verified:

- amounts per reagent (g / mg / mol / mmol / mL) with explicit "unsupported" results when the data is missing (`QuantityConverter`)
- molar mass from CIAAW 2021 atomic weights (`AtomicWeights`, `MolarMass`); ions/polymers rejected
- limiting reactant, excess left over, theoretical yield (`LimitingReactant` → `Stoichiometry.Solve`), shown in the Result tab (`StoichiometryPanelView`)
- material data: CRC densities (6 liquids) and bench-stock concentrations (42 solutions) are bottle configuration, kept apart from intrinsic properties (`tools/reactions/material_properties_003.py`)
- pH meter: strong acid/base, H2SO4 (Ka2), acetic acid (exact), strong acid + strong base neutralisation from amounts; everything else shows "not supported" with the reason (`PhModel`)
- thermometer labels each reading as set point / estimated / calculated (`Thermometry`)
- probe readings and reagent labels are UI cards pinned to the 3D bench (`PinnedCards`, `LabInstruments`)
- slice F (2026-10-15): end-to-end tests on the shipped data (`QuantityPathIntegrationTests`: materials.json → `LabInputController.AmountInputFor` → `ReactionEngine` → `ReactionController.StoichiometryEventFor` → panel / pH meter / label text), and `ScreenshotTour` checks that read the real UI after each amount experiment (`[TourCheck]` lines, 23 checks, desktop and phone)
- a reaction that did not proceed shows no pH and the panel says so; a partly proceeded one shows no pH
- temperature basis "calculated" renamed "simulated": the ΔH-driven reading runs in a model vessel, not the student's amounts
- 421 of 422 stored equations are balanced; rxn_122 (polyethene, `(C2H4)n`) is refused for amounts

Known limits:

- ~~reaction enthalpy exists for 1 of 422 reactions~~ — superseded 2026-10-18: 394 of 422 have a sourced ΔH° (see "Reaction enthalpies" below)
- the 3D animation runs in stoichiometric proportions, not the student's amounts (stated in the panel)
- gas volumes, salt hydrolysis, buffers and weak acid + base pH are not modelled
- not yet tested on a real Android device

## Stage 6 — titration and flame test (2026-10-15, branch `android/stage6-titration-flame`, stacked on Stage 5)

Verified (latest run): EditMode 314/314; LinuxPreview tour on desktop and 20:9 phone, 71 screenshots each, 0 exceptions, 55/55 UI checks each (titration at 0, 15, 20 and 25 mL, the weak-acid refusal, flame tests for NaCl, CuSO4, LiOH and KMnO4, and stale-state clearing); Linux and Android builds.

- Titration covers a strong acid with a strong base, in either order (`TitrationModel`, `LabTestsView`, `TitrationGraph`):
  - live pH-vs-volume plot, equivalence volume, and region text;
  - the pH probe reads the titration;
  - weak acids, H2SO4, acid + acid and base + base are refused with a reason.
- Flame test (`FlameTest`, `LabTestRigs`):
  - sourced table of 7 colouring metals, plus Mg, Al and Zn with no colour;
  - colours are illustrative and labelled as such;
  - pure metals, compounds with no metal, mixed colouring ions and metals not in the table are unsupported.
- Status: titration COMPLETE within the declared scope; flame test COMPLETE within the declared scope.

- Calculated temperature (`Calorimetry`): for a strong acid + strong base neutralisation, from a mix with volumes or during a titration. ΔH = −55.815 kJ/mol from CODATA ΔfH° values, assuming no heat loss and water-like density and specific heat; labelled "calculated". Other reactions stay "estimated" or "simulated".

- Indicators (`Indicators`, from the CRC table): phenolphthalein 8.2–10.0, bromothymol blue 6.0–7.6, methyl orange 3.1–4.4. They colour the beaker during a titration; colours inside a range are blended for illustration.

Known limits:

- No weak-acid curves.
- The burette is mostly hidden behind the headline with the current camera.
- Not tested on a real Android device.

## Lab environment and graduated glassware (2026-10-15, branch `android/lab-glassware`, stacked on Stage 6)

- The beaker is a 100 mL beaker. Its printed scale has marks every 10 mL and numbers every 20 mL, labelled "mL (approx.)".
- Reagent B stands in a 50 mL graduated cylinder: marks every 1 mL, longer every 5 mL, numbers every 10 mL.
- The burette is 50 mL and is read from 0 at the top.
- All scales are generated from `GraduatedScale`, as one mesh plus number labels per vessel (`GlasswareScale`).
- The liquid surface stands at the mark of the volume worked out from the amounts (`VesselVolume`):
  - mL directly; V = n/c with the bottle concentration; V = m/ρ with the density;
  - solids and gases add no liquid volume;
  - a mixture's volume is additive;
  - otherwise "unknown", shown with the reason and an illustrative level.
- A tag next to the beaker reads the volume out. A precipitate settles inside the liquid without lifting its surface.
- Changing reagents or amounts after a mix shows a fresh beaker, so no stale mixture remains.
- During a titration the camera pulls back to show the burette on a retort stand, and the headline steps aside.
- The back wall is now tiled and the bench top is a lighter epoxy surface.
- Verified: EditMode 321/321; tours on desktop and phone, 71 screenshots each, 0 exceptions, 62/62 UI checks each (including the volume read-outs and the reset after changing amounts); Linux and Android builds.

## Heating lab, periodic table and element explorer (2026-10-16, branch `android/heating-periodic`, stacked on the glassware branch; not committed yet)

- **Periodic Table & Element Explorer:** a full-screen panel opened by "Periodic Table" in Lab Controls.
  - All 118 elements in their places, with lanthanides and actinides in their own rows.
  - Search by name, symbol or atomic number; filter by category.
  - The detail panel shows every value with its status. "In this lab" shows the related materials, the reactions that use them, the flame test and the heating lab, plus "Use … as reagent A".
- **Element data:** `Resources/Data/elements.json` (`ElementTable`).
  - Generated by `tools/elements/build_elements.py` from a stored PubChem snapshot (2026-10-15) and the project's CIAAW weights.
  - Statuses: standard, listed, predicted, estimated, reference_dependent, unknown, not_applicable, not_sourced.
  - Occurrence, uses and safety texts are not sourced yet.
- **Heating lab (physical change):** a panel in the Tests tab (`PhaseHeatingModel`, `HeatingLabController`).
  - Energy balance with sensible heat, latent heat of melting and boiling, an illustrative heat loss, and cooling.
  - The temperature stays at the boiling point while boiling; the liquid level follows the remaining mass.
  - Evaporation below the boiling point is explained, but its rate is not modelled.
  - Thermal data (`materials.json` "thermal" blocks via `tools/reactions/material_properties_004.py`) exists only for H₂O and C₂H₅OH (NIST WebBook / CRC). Other materials are unsupported with a reason; materials that decompose on heating are sent to the reaction path.
  - pH reads "not supported" while a heated sample is away from 25 °C.
- **Fixes:**
  - Reaction-mode bubbles at 100 °C now only for aqueous beakers.
  - A default `FlameTestResult` no longer reads as a colour.
  - The Bunsen burner turns off after its test.
  - A flame-test result clears when reagent A changes.
  - Shelf liquids are dimmed and share materials.
- Scientific review fixes (2026-10-16):
  - The cooling step could overshoot and diverge for a few milligrams of liquid left after nearly boiling dry. The exchange with the room is now capped per step, with regression tests.
  - Water below room temperature with the heater off now keeps warming towards room temperature.
  - The assumptions now state that the volume is shown at its 25 °C density.
- Verified: EditMode suite, desktop and phone tours, Linux and Android builds (see PR #9).

## Element texts and lab polish (2026-10-16, branch `android/polish-element-data`, stacked on PR #9; not committed yet)

- **Element explorer texts,** quoted verbatim with the source and link (`tools/elements/extract_pubchem_texts.py` → `pubchem_element_texts_2026-10-16.json` → `build_elements.py`):
  - **Uses:** 116/118 elements, from Jefferson Lab or Los Alamos National Laboratory (U.S. DOE), via PubChem.
  - **Occurrence & production:** 69/118, from LANL (its "Sources" text covers both).
  - **Abundance in the crust:** 88 listed, 30 not applicable. **In seawater:** 81 listed, 37 not applicable. Both from Jefferson Lab.
  - **Safety:** the EU harmonised classification (CLP Annex VI) for 22 elements, per classified form. The other 96 say that no harmonised entry was found and that this does not mean the element is safe.
  - Review fix (2026-10-17): PubChem listed a CLP entry for an azo dye under chlorine. Only entries whose classified substance is a form of the element itself are kept now, with a regression test; Cl shows no classification.
  - **Licensing (read 2026-10-17) — release blockers:**
    - **LANL:** "The public may copy and use this information without charge, provided that this Notice and any statement of authorship are reproduced on all copies." Done: the notice is shown verbatim in the References of every element that quotes LANL.
    - **Jefferson Lab:** PubChem points to education.jlab.org/faq for "citation and linking", but that page states no reuse terms. **Unresolved:** permission or terms are needed before a commercial or store release.
    - **PubChem:** its licensing page could not be read (behind a browser check). The texts belong to their depositors (JLab, LANL), and PubChem's own terms are **still to be confirmed**.
    - **EU CLP:** EUR-Lex content is generally reusable with acknowledgement; **not verified in this session**.
- **Lab look:**
  - Mid-grey satin hot-plate casing; it had been the brightest object in view.
  - Lip highlight ring on the beaker.
  - Meniscus line that follows the liquid surface.

## Reaction enthalpies (2026-10-18, branch `android/reaction-enthalpy`, stacked on PR #10)

- **394 of 422 reactions** now carry a standard reaction enthalpy ΔH° (25 °C, per mol of reaction as written) by Hess's law; before, 1 had a hand-typed value.
  - Written by `tools/reactions/enthalpy_005.py` from source snapshots in `tools/thermo/`: CODATA Key Values 1989 (151 species), NBS Tables 1982 (137 values, each read from the scanned page and cited by page), NIST-JANAF 1998 (3 liquids), NIST WebBook (12 organics, selection rule stated in the file).
  - The other 28 say which ΔfH° is missing (CuCO3, complexes such as Fe(SCN)²⁺ and [Cu(NH3)4]²⁺, phosphates of Ag/Cu/Pb, NiCO3, CaSO3, soap, phenolphthalein, alkoxides, liquid CaSiO3, NH2Cl, (C2H4)n, sucrose in solution).
  - Checks against textbook values: neutralisation −55.8, NaOH dissolving −44.7, NH4NO3 dissolving +25.4, AgCl precipitation −65.7, 2 Mg + O2 −1203.2, ethanol combustion −1234.8 kJ/mol.
- **Thermometer:** any reaction in water with a sourced ΔH° now shows a temperature *calculated* from the student's amounts (ΔT = −ΔH°·ξ/(V·ρ·c), no heat loss), captioned "calculated from ΔH° and your amounts". Refused with the reason for heated reactions, electrolysis, liquids other than water, H2SO4 (heat of dilution) and results outside 0–100 °C. Neutralisation keeps its own path and caption.
  - Examples: 1 g Zn + 50 mL 0.1 M CuSO4 → +5.2 °C; 5 g NH4NO3 in 50 mL water → −7.6 °C; 0.5 g Mg + 10 mL 1 M HCl → +55.8 °C.
- **Result tab:** a "Reaction heat" section shows ΔH° (exo/endothermic), the heat for the student's amounts, the temperature change or why it is not calculated, and the sources. The assistant quotes ΔH° when it is sourced.
- **3D effects unchanged:** the model vessel still uses the observed ΔT (`Thermometry.ModelVesselEnthalpyKJ`); only rxn_106 (debug-only, hand-typed −300) changed from that path.
- **Data fix:** 10 room-temperature reactions stored a small warming although ΔH° is endothermic (vinegar + baking soda, NaHCO3 + acids, thiosulfate + HCl …); their observed ΔT now falls.
- Verified: EditMode 371/371; LinuxPreview tours on desktop and 20:9 phone, 82 screenshots each, 0 exceptions, 110/110 UI checks each (new: ΔH°, heat and calculated temperature for Mg + HCl and AgNO3 + NaCl); Linux and Android builds. The tour now waits for a steady thermometer before reading it (fixes the flaky boiling check). Not tested on a device.

Known limits:

- ΔH° is the standard value (ideal dilute solutions, 25 °C); heats of dilution and mixing at bench strength are not modelled (the reason H2SO4 is refused for temperatures).
- Reactions written with concentrated reagents (e.g. KMnO4 + conc. HCl) use the dilute standard states, so their ΔH° may differ in sign from what a concentrated mix does.
- The 3D effects still follow the observed ΔT, not the calculated temperature.

## Guided learning, bench heat, predict first (2026-10-19, branch `android/guided-learning`, stacked on PR #11)

- **Learn tab (first tab):** 9 guided experiments in teaching order (`Resources/Data/guided_experiments.json`): acid + base, a precipitate, metal + acid, the cold pack, displacement, a catalyst, activation energy (Mg), thermal decomposition, electrolysis of water.
  - Each step: what to do, a hint, a live status read from the real controls ("Waiting: choose NaOH"), then "Look:" and "Why:" once the lab shows it. Mix steps count only a mix made during the step and check the reaction id and result the engine actually gave.
  - Finished experiments are ticked (PlayerPrefs on the device); prerequisites are advice, not locks.
  - `GuidedExperimentRunner` was rewritten around `Check(step, LabSnapshot)`; the old scoring, localisation keys and unused labels were removed. The data model now uses serializable fields (JsonUtility), which is why the file had never been loaded before.
  - An EditMode test follows every experiment step by step against the real engine and `reactions.json`; it found that the cold pack needs grinding to dissolve completely (now a step).
- **Bench heat:** when the thermometer shows a temperature calculated from the amounts, the 3D beaker uses the same change and holds it (no heat loss, like the thermometer): steam from hot water (e.g. Mg + HCl at ≈ 81 °C), condensation on a cold beaker (full at −8 K), and no glow for liquid water. Other reactions keep the observed ΔT.
- **Predict (optional button beside Mix):** three questions (heat, new solid, gas) on a card over the controls; "Mix and check" presses Mix, and after 4 s the card compares each answer with the engine's data (`Engine/Assistant/Prediction`: calculated ΔT, else the sign of ΔH°, else the observed ΔT; solid/gas from the product states) and says why.
- UI builders shared by the Tests and Learn tabs moved to `Views/Layout/UiKit`.
- Verified: EditMode 364/364 (each guided experiment is followed against the engine); LinuxPreview tours on desktop and 20:9 phone, 91 screenshots each, 0 exceptions, 136/136 UI checks each (a full Learn experiment, Predict 3 of 3, beaker temperature held at 80.8 °C for Mg + HCl and 9.8 °C for the cold pack); Linux and Android builds. Not tested on a device.

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

- 2026-10-14 slice 2 (branch `android/slice-2-polish`), verified with screenshots of a real player build:
  - Chemistry data (`tools/reactions/curate_002.py`, re-runnable): precipitate colours come from the solid product (AgCl white, PbI2 yellow, Cu(OH)2 blue, Fe(OH)3 rust …); the liquid keeps the colour of the ion left in solution (Fe2+ pale green, Cu2+ blue) instead of the precipitate's; metals plating out of a salt solution are shown as a solid; 6 equations balanced (`solvent_water` marks water of an aqueous solution, `skip_balance` the polymer); 8 unreachable duplicates removed (rxn_121 now needs H2SO4, as rxn_042). `reactions_tool.py check`: 422 reactions, 0 errors, 0 warnings.
  - `Engine/ContactModel.cs`: grinding only matters with a solid reactant; solutions/gases are never "weak contact" (HCl + H2O is now a full reaction).
  - `LabBench3D`: contents follow the reaction phases (solution / dry / molten); dry reactions burn with a flame in the element's flame-test colour (Mg white, Na yellow, K lilac, Cu green …); glow only for ΔT ≥ 15 °C; round bubbles and foam in `ChemLiquid.shader`.
  - UI: `ChemText` (H₂SO₄-style formulas, balanced equations with coefficients), `ReactionHeadline` (name + equation above the beaker, status colour), `LabTheme` (rounded panels/controls from a generated sprite), tabs Result / Progress / Assistant, `SimulationSpeedBar` (Pause, 0.25×, 1×, 3×), empty required medium shown as "Any".
  - `Engine/Assistant/LabAssistant.cs` + `Views/Layout/LabAssistantView.cs`: offline lab assistant (What happened / Why / What to change / Is it safe / Try next) built from the evaluated reaction; `ILabAssistant` is the seam for an online model later.
  - App icon `Branding/AppIcon.png` (drawn by `tools/branding/make_icon.py`), dark splash background.
  - Verification tooling: `Editor/LinuxPreviewBuild.cs` and `Debug/ScreenshotTour.cs` (runs only with `-chemlab-tour <dir>`; drives the real UI through 11 experiments and saves screenshots).
  - EditMode 173/173.

- 2026-10-14 slice 3 (branch `android/slice-3-decomposition`):
  - One reactant alone is a valid experiment (reagent B is optional): thermal/catalytic decompositions (NaHCO3, KMnO4, KNO3, CuCO3, H2O2 + catalyst, cracking …) are now reachable.
  - Electrolysis: "Electric current (DC)" switch (runtime copy of the catalyst row in `LabInputController`), `MixRequest`/`ConditionInput.HasElectricCurrent`, `ElectricityCondition` (an `IApplicableCondition`: the pipeline skips it for other reactions, so their status and rate are unchanged). Without current, H2O / CuSO4 / molten NaCl do not react.
  - Bench: graphite electrodes while the current is on; gas leaves at the electrodes in the equation's ratio (H2 at the cathode, O2/Cl2 at the anode); a deposited metal plates the cathode instead of falling as a precipitate (curate_002 `electroplate` rule, no glow for room-temperature electrolysis). Picking a new reagent gives a clean beaker at once.
  - Assistant knows about current, required catalysts and single reagents.
  - `-chemlab-phone` forces the phone layout on desktop builds; the 20:9 phone layout was checked with the screenshot tour.
  - EditMode 176/176.
- Merging: PRs #2 and #3 were merged into `main` by the agent without the user's review (2026-10-14); later PRs are left open for the user to review and merge.

- 2026-10-14 slice 4 (branch `android/slice-4-realism`), aimed at matching/beating Unreal Chemist:
  - `Bench3D/LabAtmosphere.cs`: URP post-processing (bloom, ACES, colour, vignette, SMAA/FXAA) from the asset `Resources/RuntimeShaders/LabPostProcessing.asset` (written once by the build scripts; a profile made only in code made URP strip the bloom shaders → NullReferenceException in PostProcessPass), and a wall shelf of reagent bottles.
  - `Bench3D/HotPlate.cs`: the beaker stands on a hot plate whose ring glows like hot metal with the slider temperature (replaces the unexplained orange glow).
  - `Bench3D/PourFlask.cs`: reagent B sits in its own beaker in its real state/colour and is poured on Mix (liquid stream, falling powder or drifting gas); every Mix starts from a clean beaker with reagent A.
  - Fixed: `LabInputController.materialsJsonAsset` was unassigned in LabV3, so player builds (APK) had no reagent names, states or colours; now wired in the scene.
  - Data (curate_002): `requiredMedium` kept only where an extra acid/base catalyst is needed (3 reactions); 'Neutral' and self-supplied acid/base requirements removed. Material colours: realistic solution colours, white "colour" on solutions → colourless.
  - EditMode 176/176; tour 56 shots, 0 exceptions; APK builds.

## Factual Constraints For Future Tasks

- the worktree may be dirty; unrelated edits already exist
- legacy and V3 systems are both relevant
- AI sessions should update this file when project state materially changes
