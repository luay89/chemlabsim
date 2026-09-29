# ChemLabSim

![Unity](https://img.shields.io/badge/Unity-2022.3.62f3-000000?logo=unity&logoColor=white)
![C#](https://img.shields.io/badge/C%23-10-239120?logo=csharp&logoColor=white)
![URP](https://img.shields.io/badge/URP-14.0.12-blue)
![License](https://img.shields.io/badge/License-MIT-green)

ChemLabSim is an educational chemistry simulation built with Unity. It allows students to experiment with reactants, temperature, medium conditions, catalyst presence, and contact efficiency to observe chemical reaction outcomes.

---

## Features

- Dynamic reactant selection from encrypted reaction database
- Reaction validation and evaluation (Success / Partial / Fail)
- Temperature, stirring, grinding, and medium controls
- Catalyst support with activation energy adjustment
- Scientific explanation system with causal reasoning
- Reaction identity and balanced chemical equation display
- Influence summary and lab observation
- Safety note and quiz hint system
- Session score and recent experiment history
- Level progression with lesson objectives
- Challenge and achievement tracking
- Saved local progress across sessions
- Arabic font and RTL support via RTLTMPro
- Minimal-risk architecture with secure data loading (AES-256-CBC + HMAC-SHA256)

---

## Project Structure

The repository contains two coexisting code paths:

- `Assets/_Project` — legacy lab (assembly `ChemLabSim.Core`)
- `Assets/_ProjectV3` — V3 lab (assembly `ChemLabSimV3`, depends on `ChemLabSim.Core`; plus `ChemLabSimV3.Editor` and `ChemLabSimV3.EditModeTests`)

| Component | Description |
| --------- | ----------- |
| `Boot.unity` | Entry scene — initializes AppManager and loads encrypted data |
| `_ProjectV3/Scenes/LabV3.unity` | V3 lab scene — contains its own `AppRoot` (AppManager + SecureReactionLoader) so it can be opened directly |
| `Lab Scene.unity` | Legacy lab UI |
| `AppManager` | Singleton lifecycle manager and owner of the reaction database; persists across scenes |
| `SecureReactionLoader` | Decrypts and validates `reactions.bytes` at runtime |
| `V3Bootstrap` | V3 composition root — registers services and initializes V3 controllers |
| `LabInputController` → `ReactionController` | V3 input (`MixRequest`) and mix orchestration |
| `ReactionEngine` / `ReactionRegistry` | V3 reaction lookup and evaluation driven by the reaction database |
| `SimulationStepper` | V3 live reaction simulation playback |
| `ReactionEvaluator` / `LabController` | Legacy evaluation engine and legacy lab UI orchestration |
| `reactions.json` | Authored reaction source (`Assets/_Project/DataSrc`) |
| `reactions.bytes` | AES-256-CBC encrypted reaction database with HMAC integrity — the file actually loaded at runtime; regenerate it (`Tools/Security/Encrypt Reactions JSON -> bytes`) after editing `reactions.json` |

---

## Scientific Concepts

- **Activation energy** — each reaction requires a minimum temperature threshold to proceed
- **Effective contact** — stirring and grinding determine reagent contact quality (0.6 – 1.6)
- **Reaction medium** — reactions require a specific pH environment (Neutral, Acidic, or Basic)
- **Catalyst effect** — lowers the activation threshold without being consumed

---

## Current Status

- **Unity compilation** — previously verified clean on Unity 2022.3.62f3
- **LabV3 runtime** — reaction database wiring added to `LabV3.unity`; Play Mode verification is in progress
- **Gamified learning loop** — score, lessons, challenges, and achievements exist in the lab flow
- **Persistent local progress** — language and student progress are saved between sessions
- **Encrypted data** — reaction data is shipped as an encrypted, HMAC-checked blob

Developer and AI-agent documentation: [`docs/ai/README.md`](docs/ai/README.md) and [`docs/ai/constitution-v3.md`](docs/ai/constitution-v3.md).

---

## Future Ideas

- More reactions and reaction categories
- Guided experiment sequences with teacher-friendly presets
- Student profile export / classroom progress reports
- Animated lab effects and 3D interactions
- WebGL and Android builds for wider accessibility

---

## Project Preview

- Interactive project page: [`docs/index.html`](docs/index.html)
- Runtime screenshots/GIF capture can be added once a stable showcase build is prepared

---

## License

MIT License
