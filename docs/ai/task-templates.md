# Task Templates

These templates are for future AI sessions. Use them as execution checklists and adapt them to the task.

## 1. Investigation Template

Use for:

- bugs not yet reproduced
- architecture questions
- repository discovery tasks

Checklist:

1. read `AGENTS.md` and `docs/ai/*`
2. inspect relevant code, scenes, data, and settings
3. identify authoritative files
4. state assumptions explicitly
5. summarize findings with evidence
6. update `project-status.md` or `engineering-decisions.md` if repository knowledge changed

## 2. Bug Fix Template

Use for:

- regressions
- compile/runtime defects
- scene wiring issues

Checklist:

1. confirm the failing path and affected subsystem
2. inspect related code and serialized assets before editing
3. create a short plan
4. apply the smallest safe fix
5. verify with the strongest safe check available
6. record any architectural consequence in `engineering-decisions.md` if needed
7. update `project-status.md` if system status changed

## 3. Feature Template

Use for:

- new behavior
- content expansion
- new tooling

Checklist:

1. locate the owning architecture and subsystem
2. confirm whether the work belongs in legacy, V3, or both
3. create a plan
4. implement minimal cohesive changes
5. verify integration points
6. update architecture or status docs if repository knowledge changed
7. update roadmap only if project direction changed

## 4. Refactor Template

Use for:

- cleanup
- decomposition
- dependency simplification

Checklist:

1. document current ownership and boundaries
2. preserve behavior first
3. avoid mixing refactor and feature changes unless required
4. verify assembly, scene, and serialized-field safety
5. record meaningful structural changes in `engineering-decisions.md`
6. update `architecture.md` if boundaries changed

## 5. Documentation Update Template

Use for:

- new architecture knowledge
- status changes
- standards changes

Checklist:

1. decide which canonical file owns the update
2. update existing docs instead of creating overlap
3. keep facts and plans separate
4. cross-check links and terminology
5. note the update in the task summary
