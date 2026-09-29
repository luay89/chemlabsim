# Coding Standards

## General Standards

- Preserve existing behavior unless the task explicitly changes behavior.
- Prefer minimal, reversible changes.
- Do not rewrite large systems when a local change is sufficient.
- Keep naming and style aligned with the existing local subsystem.
- Avoid duplicate logic and duplicate documentation.

## Constitution Enforcement

- `docs/ai/constitution-v3.md` is mandatory for all implementation tasks.
- Scientific behavior must be simulation-driven first, then visualized.
- UI must consume simulation state and must not become a chemistry engine.
- Every task must include 3-level verification evidence: simple, medium, full-system.
- Reject changes that increase baseline error count unless explicitly justified.

## Repository-Specific Standards

- Analyze the repository or the affected subsystem before editing.
- Treat `Assets/_Project` and `Assets/_ProjectV3` as separate but connected architectures.
- Avoid introducing new architectural patterns unless the task requires them.
- Do not modify generated Unity directories as a way to fix source issues.
- Prefer changes in authored source under `Assets/_Project`, `Assets/_ProjectV3`, `Assets/Editor`, `Tools`, `Packages`, `ProjectSettings`, and documentation.

## Documentation Standards

- Update the AI workspace when the task changes architecture, standards, decisions, status, or roadmap.
- Keep status documents factual.
- Keep decisions durable and date-stamped.
- Prefer one canonical document per concern.

## Unity Standards

- Do not rename scenes, assets, or serialized fields casually.
- Avoid breaking scene references or asmdef boundaries.
- Respect current build scenes unless the task explicitly changes them.
- Treat `.meta` files as serialization-critical.

## Safety Standards

- Do not revert unrelated user changes.
- Avoid destructive commands unless explicitly requested.
- Prefer read-first investigation before code changes.
- When verification is needed, prefer the least destructive check that still gives meaningful confidence.

## Change Workflow Standards

Every task should:

1. inspect the relevant code and data
2. create a plan
3. implement the smallest safe change
4. verify results
5. update persistent documentation if project knowledge changed

## Current Style Observations

- Legacy code uses larger MonoBehaviour controllers and direct Unity wiring.
- V3 code uses more explicit controllers, views, services, event buses, and engine classes.
- JSON is a primary content authoring format for chemistry data and progression content.
- Security-sensitive data loading already exists and should be treated carefully.
