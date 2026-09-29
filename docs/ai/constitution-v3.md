# ChemLabSim V3 Constitution

## Engineering, Scientific Simulation, and Production Constitution

### 1. Final Goal
The goal of ChemLabSim V3 is not to merely display chemical equations or textual outputs.

The goal is to build an interactive educational chemistry laboratory where users feel the reaction happening in front of them in a convincing and near-real way, whether the user is:

- a child learning chemistry basics
- a student
- a teacher
- a professor or advanced learner

The final product must be:

**Scientifically grounded + Visually impressive + Interactive + Stable + Maintainable**

## 2. Scientific and Visual Vision
When a chemical reaction happens, visual change should be driven by simulation state as much as possible, not by disconnected animation.

Examples:

- temperature rise -> glow / heat distortion / appropriate steam
- boiling -> liquid transition + vapor
- evaporation -> gradual liquid decrease + vapor
- solidification -> motion and appearance change
- melting -> gradual transition from solid to liquid
- gas production -> bubbles + gas release + pressure-related change
- precipitation -> appearance and settling of solid matter
- combustion -> heat + light + flame + suitable products/gases
- exothermic reaction -> temperature rise and connected thermal effects
- endothermic reaction -> thermal drop and connected effects
- color change -> based on reaction output, not random
- foam -> linked to relevant gas-producing reactions
- crystallization -> gradual growth, not sudden pop-in

Molecular-level physics or laboratory-grade physical simulation is not required.

But the simulation must be:

**Educationally believable and scientifically grounded.**

## 3. Core Architectural Principle
The following separation is required:

Chemical Model
->
Simulation Engine
->
Simulation State
->
Visual Director
->
UI

Meaning:

```
Chemical Data
      ->
Reaction Model
      ->
Reaction / Simulation Engine
      ->
Simulation State
      ->
Visual Director
      ->
VFX / Shaders / Particles / UI
```

The UI must not calculate chemistry.

Visual effects must not become scientific truth sources.

## 4. Single Source of Truth
Each type must have one real authoritative definition only.

Forbidden:

- duplicate classes
- duplicate interfaces
- fake namespaces
- duplicate models
- duplicate states
- duplicate requests

Before deleting or moving any type:

1. Search all references.
2. Identify dependencies.
3. Identify real implementation.
4. Verify consumers.
5. Then modify.

## 5. Stubs Are Not Permanent Architecture
Stubs are allowed only as temporary, deliberate recovery steps.

But stubs:

- must not contain business logic
- must be explicitly marked
- must be replaced by real implementation
- must never become system foundation

Rule:

**Stub != Architecture**

## 6. Namespace Discipline
If code uses:

```
using A.B.C;
```

Then:

- `A.B.C` must be a real namespace
- never create a class to imitate a namespace
- never change `using` only to hide errors
- if a type exists in the correct namespace, use it directly
- if missing, search first

When seeing:

```
'X' does not exist in namespace 'A.B.C'
```

Do first:

1. Search for `X` globally.
2. Verify namespace.
3. Verify assembly visibility.
4. Verify duplicates.
5. Then choose a fix.

Never create new blind placeholder types.

## 7. Assembly Discipline
Do not:

- create random asmdefs
- delete asmdefs casually
- enable known cycle-prone assemblies
- add random references

Before any assembly change:

1. Inventory.
2. Dependency graph.
3. Cycle check.
4. Compile test.
5. Runtime verification.

## 8. No Layer Patching
Interconnected systems must be treated as a whole.

If a change touches:

- ViewModel
- State
- Data
- Request
- Engine

Then linked layers must be checked together.

Do not patch one layer while ignoring its consumers.

## 9. Models Must Preserve Contracts
Do not oversimplify models used by Engine or Controllers just to silence errors.

If `ReactionEntry` uses:

- reactants
- products
- conditions
- solubility
- reaction parameters

It must not be reduced to an empty shell.

The same rule applies to:

- MixRequest
- Material
- Reaction
- SimulationState
- ProgressState
- QuizState
- ViewModels
- any actively used model

## 10. Scientific Integrity
Any new simulation feature must define:

1. scientific inputs
2. initial state
3. state change over time
4. phenomenon conditions
5. final state
6. linked visual effect

Example:

```
Temperature
+
Boiling Point
+
Pressure
->
Phase Transition
->
Liquid -> Gas
->
Visual Steam
```

Visual effects do not decide boiling.

Simulation decides boiling.

## 11. Visual Quality Principle
Visual effects must be:

- gradual
- coherent
- controllable
- tunable
- reusable
- simulation-state linked

Allowed approaches include:

- Unity VFX Graph
- Particle Systems
- Shaders
- URP effects
- Lighting
- Post-processing
- fluid-like visual techniques
- heat distortion
- smoke
- steam
- foam
- bubbles
- sparks
- fire
- precipitation
- crystal growth
- custom meshes

External systems/assets/tools are allowed if they provide better outcomes under:

**Isolation + Adapter + Stable Interface**

No external dependency should break core system stability.

## 12. Architecture Must Survive Expansion
Any new feature must be addable/removable without collapsing other systems.

Preferred pattern when needed:

```
Interface
->
Implementation
->
Adapter
->
Consumer
```

Avoid circular dependencies:

```
A -> B -> C -> D -> A
```

## 13. Mandatory Testing Constitution
This is mandatory.

After every addition or change, run tests at three levels:

### Level 1: Simple Test
Smallest changed unit, such as:

- class
- method
- model
- parser
- calculation
- condition

Goal:

**Does the change itself work?**

### Level 2: Medium Test
Direct integration of changed unit with immediate consumers, such as:

```
ReactionEntry
->
ReactionEngine
```

or:

```
MixRequest
->
SimulationStepper
```

Goal:

**Does it work with directly linked systems?**

### Level 3: Full System Test
Full project verification:

- compile
- integration tests
- acceptance tests
- bootstrap
- scene loading
- UI wiring
- simulation flow
- runtime errors
- critical logs

Goal:

**Did this change break anything outside its local area?**

## 14. Error Budget Rule
Before change:

`Baseline Errors = X`

After change:

`New Errors = Y`

Reject change when:

`Y > X`

unless intentional and explicitly justified.

If error count spikes, stop.

Do not continue broad patching after a single unstable change.

Return to last healthy state and fix root cause.

## 15. Golden Rollback Rule
Before major architecture changes:

- create a git checkpoint
- record compile status
- record test status
- record changed files

If widespread breakage occurs:

**STOP -> DIAGNOSE -> ROLLBACK**

Never stack patching on top of broken state.

## 16. Compile Is a Gate
No step should proceed on unstable prior step.

Required flow:

```
Change
->
Simple Test
->
Medium Test
->
Full Compile
->
Full Test
->
PASS
->
Next Step
```

If failed:

```
STOP
->
Diagnose
->
Fix
->
Retest
```

## 17. No Blind Deletion
Never delete the following just because they seem unused:

- class
- interface
- model
- state
- property
- field
- file
- assembly

Before deletion:

```
Global Search
->
Reference Analysis
->
Dependency Analysis
->
Runtime Relevance
->
Delete
->
Tests
```

## 18. Development Workflow
Every future step must follow:

### STEP A: BUILD
Define the required change precisely.

### STEP B: INSPECT
Inspect files and relationships before editing.

### STEP C: IMPLEMENT
Implement the smallest safe change.

### STEP D: SIMPLE TEST
Test the changed part itself.

### STEP E: MEDIUM TEST
Test direct integration.

### STEP F: FULL TEST
Test full project behavior.

### STEP G: HARDEN
Add protections and required tests.

### STEP H: CHECKPOINT
Record status in git.

Then and only then move forward.

## 19. Production Quality
Target is not only:

`Compilation = CLEAN`

But:

`Compilation = CLEAN + Tests = PASS + Runtime = STABLE + Simulation = CORRECT + Visuals = HIGH QUALITY + Architecture = MAINTAINABLE`

## 20. Final Product Vision
The user should be able to:

1. choose materials
2. set quantities
3. add catalyst or adjust conditions
4. adjust temperature
5. control stirring
6. change medium
7. observe gradual reaction start
8. observe physical and chemical changes
9. observe temperature, gas, steam, foam, precipitate, color, and related effects
10. pause experiment and inspect current state
11. understand why reaction occurred
12. receive scientific results in a clear and enjoyable way

Goal:

**The user should feel that the chemistry is happening, not that an animation is playing.**

## 21. Final Engineering Rule
Before any change, ask:

Does this change move the system closer to realistic chemical simulation without breaking architecture?

If yes:

**Proceed.**

If no:

**Stop and redesign.**

## 22. Mandatory Copilot Task Report
After every task, Copilot must provide a report including:

- files inspected
- files modified
- files added
- files deleted
- dependencies affected
- tests performed
- simple test result
- medium test result
- full test result
- compile result
- error count before
- error count after
- runtime verification
- remaining risks
- git checkpoint recommendation

Copilot must not say "No errors found" without explaining how verification was performed.

## Final Principle
**Do not optimize for fixing today's error.**

Optimize for:

**A scientifically grounded, visually impressive, extensible, testable, and stable ChemLabSim that can continue growing without architectural collapse.**

Every new feature must leave the project:

**Better than it found it.**
