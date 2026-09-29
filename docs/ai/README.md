# ChemLabSim AI Workspace

This directory is the canonical knowledge base for AI agents working in this repository.

Future sessions should treat these files as the source of truth instead of relying on chat history.

## Required Read Order

1. [AGENTS.md](/home/luay/Projects/projects/ChemLabSim/AGENTS.md)
2. [docs/ai/README.md](/home/luay/Projects/projects/ChemLabSim/docs/ai/README.md)
3. [docs/ai/constitution-v3.md](/home/luay/Projects/projects/ChemLabSim/docs/ai/constitution-v3.md)
4. [docs/ai/project-status.md](/home/luay/Projects/projects/ChemLabSim/docs/ai/project-status.md)
5. [docs/ai/architecture.md](/home/luay/Projects/projects/ChemLabSim/docs/ai/architecture.md)
6. [docs/ai/coding-standards.md](/home/luay/Projects/projects/ChemLabSim/docs/ai/coding-standards.md)
7. [docs/ai/engineering-decisions.md](/home/luay/Projects/projects/ChemLabSim/docs/ai/engineering-decisions.md)
8. [docs/ai/roadmap.md](/home/luay/Projects/projects/ChemLabSim/docs/ai/roadmap.md)
9. [docs/ai/task-templates.md](/home/luay/Projects/projects/ChemLabSim/docs/ai/task-templates.md)

## Workspace Purpose

This workspace preserves:

- repository architecture
- coding standards
- engineering decisions
- constitution rules
- current status
- roadmap context
- reusable execution templates

## Required AI Session Loop

Every new task should follow this loop:

1. Read the required files above.
2. Re-scan the repository areas relevant to the task.
3. Create a short execution plan.
4. Implement only the smallest safe change that solves the task.
5. Verify the result with the strongest non-destructive checks available.
6. Update this workspace if the task changes project knowledge, status, or decisions.
7. Report what changed and what remains true.

## Canonical Rules

- Do not rely on previous chat context when project knowledge can be stored here.
- Prefer updating existing documents over creating duplicate guidance.
- Keep documentation factual and durable.
- Separate current facts from future intent.
- Record architectural or process changes in [engineering-decisions.md](/home/luay/Projects/projects/ChemLabSim/docs/ai/engineering-decisions.md).
- Record project-progress changes in [project-status.md](/home/luay/Projects/projects/ChemLabSim/docs/ai/project-status.md).
- Record roadmap changes in [roadmap.md](/home/luay/Projects/projects/ChemLabSim/docs/ai/roadmap.md).

## Scope Notes

- The repository currently contains both legacy `_Project` code and newer `_ProjectV3` code.
- Generated Unity folders such as `Library`, `Temp`, and historical `Builds` are present in the repository and should not be treated as primary authored source.
- Agent-facing instructions exist in both [AGENTS.md](/home/luay/Projects/projects/ChemLabSim/AGENTS.md) and [.github/workflows/.github/copilot-instructions.md](/home/luay/Projects/projects/ChemLabSim/.github/workflows/.github/copilot-instructions.md); this directory is the shared backing knowledge for both.
- [docs/ai/constitution-v3.md](/home/luay/Projects/projects/ChemLabSim/docs/ai/constitution-v3.md) is the mandatory scientific simulation and testing constitution for all implementation tasks.
