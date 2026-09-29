# Roadmap

This roadmap records persistent direction inferred from the current repository structure and existing project documents. It is not a promise of implementation order.

## Current Direction

The repository points toward a gradual evolution from the legacy lab flow to the V3 architecture rather than a single full rewrite.

## Roadmap Themes

### 1. Stabilize the dual-architecture repository

Evidence:

- active legacy and V3 codepaths coexist
- V3 still depends on legacy runtime data/bootstrap paths

Likely work areas:

- reducing ambiguity between legacy and V3 ownership
- keeping shared data contracts stable
- preserving build and scene integrity while new systems evolve

### 2. Mature the V3 lab experience

Evidence:

- extensive V3 controllers, views, chemistry, and simulation code already exists
- `LabV3` is in build settings

Likely work areas:

- completing missing user-facing connections
- tightening view/controller/service integration
- validating runtime data and scene wiring

### 3. Expand educational content systems

Evidence:

- lessons, challenges, quiz, achievements, and guided experiment data are already present as JSON

Likely work areas:

- content integration
- progression balancing
- guided experience delivery

### 4. Improve fidelity of chemistry and visualization

Evidence:

- V3 contains chemistry, thermodynamics, equilibrium, molecular simulation, vessel rendering, and FX systems

Likely work areas:

- consistency between simulation layers
- stronger live visual feedback
- deeper alignment between chemistry output and presentation

### 5. Keep tooling and agent knowledge durable

Evidence:

- the repository now includes a persistent AI workspace
- editor automation and batch build infrastructure already exist

Likely work areas:

- keeping docs current
- documenting major system shifts
- preserving reusable execution patterns for future agents

## Roadmap Update Rules

- Update themes when repository direction materially changes.
- Keep roadmap items broad and durable.
- Move facts about current implementation state to [project-status.md](/home/luay/Projects/projects/ChemLabSim/docs/ai/project-status.md), not here.
