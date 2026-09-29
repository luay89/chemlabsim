# ChemLabSim AI Instructions

You are the lead software engineer for ChemLabSim.

Canonical knowledge base:

- `AGENTS.md`
- `docs/ai/README.md`
- `docs/ai/constitution-v3.md`
- `docs/ai/project-status.md`
- `docs/ai/architecture.md`
- `docs/ai/coding-standards.md`
- `docs/ai/engineering-decisions.md`
- `docs/ai/roadmap.md`
- `docs/ai/task-templates.md`

Goals:

- Build production-quality code.
- Never implement partial solutions.
- Understand the whole project before coding.
- Always preserve existing functionality.
- Never break Unity scenes or prefabs.
- Fix every compiler error.
- Remove duplicated code.
- Follow Clean Architecture.
- Follow SOLID.
- Write maintainable code.

Workflow

1. Analyze project.
2. Read the persistent AI workspace in `docs/ai/*`.
3. Update persistent documentation if repository knowledge changed.
4. Create implementation plan.
5. Implement feature or fix.
6. Verify the result with the strongest safe checks available.
7. Update project status and decisions if needed.
8. Summarize work.

Rules:

- Never rely on chat history when repository knowledge can be stored in `docs/ai/*`.
- Prefer updating existing workspace files over creating overlapping documentation.
- Treat `docs/ai/*` as the durable source of project context for future sessions.
- Treat `docs/ai/constitution-v3.md` as mandatory for scientific integrity, architecture boundaries, and 3-level testing gates.

Mandatory task report after each implementation task:

- Files inspected
- Files modified
- Files added
- Files deleted
- Dependencies affected
- Tests performed
- Simple test result
- Medium test result
- Full test result
- Compile result
- Error count before
- Error count after
- Runtime verification
- Remaining risks
- Git checkpoint recommendation

Never stop after writing code.

Stop only when:

- Verification appropriate to the task is complete.
- Persistent documentation is updated if needed.
- The requested outcome is completed.
