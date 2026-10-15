# Engineering Decisions

This file records durable architectural and process decisions that future AI sessions should preserve unless a later decision supersedes them.

## Decision Log

### 2026-08-20: Adopt ChemLabSim V3 scientific simulation constitution as a mandatory gate

Status: accepted

Reason:

- A stricter quality bar is needed to keep simulation scientifically grounded while scaling V3 visual and interaction depth.
- Prior guidance covered process quality but did not explicitly enforce simulation-truth boundaries and 3-level test evidence on every task.

Implications:

- `docs/ai/constitution-v3.md` is now mandatory reading for implementation tasks.
- Every task report must include compile/test/error-budget/runtime evidence, including simple, medium, and full-system checks.
- Architecture changes must preserve Single Source of Truth and avoid layer patching and blind type duplication.

### 2026-07-12: Use `docs/ai` as the canonical AI workspace

Status: accepted

Reason:

- The repository already had agent-facing guidance in `AGENTS.md` and Copilot instructions, but project knowledge was not centralized.
- A documentation workspace inside the repository allows future AI sessions to recover context without chat history.

Implications:

- `docs/ai` is the canonical persistent AI knowledge base.
- Agent entry points should direct future sessions to this workspace.

### 2026-07-12: Preserve both legacy `_Project` and V3 `_ProjectV3` as active architectural context

Status: accepted

Reason:

- Both codepaths are present, referenced, and materially relevant to future work.
- V3 still bridges into legacy runtime data/bootstrap paths.

Implications:

- Analysis and planning must consider both architectures.
- Future changes should not assume the legacy path is dead code without repository evidence.

### 2026-07-12: Treat generated Unity folders as non-canonical source

Status: accepted

Reason:

- `Library`, `Temp`, and build-output directories exist in the repository, but they are generated artifacts rather than authoritative authored source.

Implications:

- Source-of-truth analysis should prioritize authored assets, scripts, settings, and manifests.
- Generated artifacts can still provide evidence, such as prior build logs, but should not define architecture.

### 2026-07-12: Keep AI status reporting factual and separate from roadmap intent

Status: accepted

Reason:

- Future AI sessions need to distinguish what exists now from what is planned or inferred.

Implications:

- `project-status.md` records current observed state.
- `roadmap.md` records planned or likely next areas, not guaranteed implementation state.

### 2026-10-13: Android first; LabV3 is the shipped scene

Status: accepted

Reason:

- Most target students use Android phones; the 3D bench and particles run better as a native app than in mobile browsers.
- `LabV3.unity` carries its own `AppRoot`, so it can be the only scene in the mobile build.

Implications:

- Android build: `ChemLabSimV3.Editor.AndroidBuild` (IL2CPP, ARM64, landscape, application id `com.chemlabsim.app`). WebGL stays in CI as the secondary target; iOS later.
- UI must work on a ~6 inch landscape phone: the beaker stays in the centre, info goes in the right tab column (`LabScreenLayout`), and the UI respects the safe area.
- Shaders that are only reached through `Shader.Find` need a material under `Resources/RuntimeShaders`.
- The AI assistant (planned) talks to a backend proxy; no API keys in the app.

### 2026-10-14: Visual verification with a player build, and chemistry-derived colours

Status: accepted

Reason:

- Visual bugs (white disk, false heat glow, liquid in dry reactions, blocky foam) were invisible to EditMode tests.
- Hand-picked colours per reaction were often chemically wrong (precipitate colour painted on the whole liquid).

Implications:

- UI/visual changes are checked by building `LinuxPreviewBuild` and running `ScreenshotTour` (`-chemlab-tour <dir>`), then reviewing the screenshots. Build and run in a separate git worktree when the user has the project open in Unity.
- Colours in `reactions.json` follow the rules in `tools/reactions/curate_002.py`; new batches should go through it.
- Contact factor has one definition: `ContactModel`.

### 2026-10-14: Scientific data policy for quantities, pH and temperature (Stage 5)

Status: accepted

Reason:

- The app teaches chemistry; a plausible-looking invented number is worse than no number.

Implications:

- Missing data gives an explicit unavailable/unsupported result with the reason; values are never filled in.
- Bottle configuration (stock concentration, label) is stored apart from intrinsic material properties (density, molar mass) and cited.
- pH is only calculated for the models listed in `PhModel`; concentrated solutions (> 0.1 M) are marked approximate.
- Temperatures carry their basis (set point / estimated / calculated).
- 3D readings are screen-space cards pinned to world anchors (`PinnedCards`), not world-space text, so they stay readable on phones.

### 2026-10-15: Stage 6 scope — titration and flame test

Status: accepted (taken during an unattended session; open to review)

Reason:

- Both modes must use real models, not animations, and say clearly what they do not cover.

Implications:

- Titration covers only strong acid + strong base (either in the beaker), with the PhModel strong acids and bases. The concentrations come from the configured bench-stock bottles, and the beaker volume from reagent A's amount in mL. Weak acids, buffers and H2SO4 are refused with a reason.
- The flame test uses a fixed table (Li, Na, K, Ca, Sr, Ba, Cu; no colour for Mg, Al, Zn), with sources cited in `FlameTest`. Colours are illustrative sRGB and are never derived from the liquid colour. Pure metals, compounds with no metal, compounds with mixed colouring ions, and metals not in the table are unsupported.
- Both live in a fourth "Tests" tab (`LabTestsView`). The 3D burette and burner (`LabTestRigs`) only display events. The chemistry stays in `Engine/Chemistry`.
- Strong acid/base pH uses `PhModel.PhFromNetStrongAcid`, which solves for [OH⁻] on the basic side to avoid floating-point cancellation.
- A temperature is labelled "calculated" only when the energy balance is known from the student's amounts: strong acid + strong base, via `Calorimetry`, with the assumptions stated in the caption.

## Decision Update Rules

- Add a new dated entry when a meaningful architectural or process choice changes.
- Do not silently rewrite old decisions.
- If a decision becomes obsolete, mark it superseded and reference the newer entry.
