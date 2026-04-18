# ChemLabSim

![Unity](https://img.shields.io/badge/Unity-2023_LTS-000000?logo=unity&logoColor=white)
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

| Component | Description |
| --------- | ----------- |
| `Boot.unity` | Entry scene — initializes AppManager and loads encrypted data |
| `Lab Scene.unity` | Main lab UI — reagent selection, controls, and result display |
| `AppManager` | Singleton lifecycle manager, persists across scenes |
| `SecureReactionLoader` | Decrypts and validates `reactions.bytes` at runtime |
| `ReactionEvaluator` | Static evaluation engine — medium, temperature, contact, catalyst |
| `LabController` | Lab UI orchestration, result formatting, and educational layers |
| `reactions.bytes` | AES-256-CBC encrypted reaction database with HMAC integrity |

---

## Scientific Concepts

- **Activation energy** — each reaction requires a minimum temperature threshold to proceed
- **Effective contact** — stirring and grinding determine reagent contact quality (0.6 – 1.6)
- **Reaction medium** — reactions require a specific pH environment (Neutral, Acidic, or Basic)
- **Catalyst effect** — lowers the activation threshold without being consumed

---

## Current Status

- **Strong Educational MVP+** — full reaction evaluation with scientific explanations
- **Gamified learning loop** — score, lessons, challenges, and achievements are active in the lab flow
- **Persistent local progress** — language and student progress are saved between sessions
- **Production-safe** — encrypted data, null guards, safe fallbacks throughout
- **Ready for further expansion** — modular design allows adding reactions and features incrementally

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
