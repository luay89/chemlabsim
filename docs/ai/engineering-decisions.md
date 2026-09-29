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

## Decision Update Rules

- Add a new dated entry when a meaningful architectural or process choice changes.
- Do not silently rewrite old decisions.
- If a decision becomes obsolete, mark it superseded and reference the newer entry.
