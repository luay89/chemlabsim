# ChemLabSim v3 — Architecture Design Document

> **Status:** Design Phase (No Implementation)  
> **Base:** ChemLabSim v2  
> **Date:** April 2026

---

## 1. V2 Analysis — Problems Identified

### 1.1 God Class: `LabController.cs` (3,855 lines)

| Responsibility | Lines (approx) | Should Be |
|---|---|---|
| UI Setup (dropdowns, sliders, HUD, dashboard, backdrop) | ~1,200 | UIController / Prefabs |
| Canvas scaling & responsive layout | ~200 | UI Layer (CanvasManager) |
| Reaction evaluation orchestration | ~250 | ReactionController |
| Score calculation & session tracking | ~150 | ProgressController |
| Level/lesson progression | ~120 | ProgressController |
| Challenge mode | ~120 | ChallengeController |
| Objectives system | ~100 | ObjectiveController |
| Achievements | ~120 | AchievementController |
| Save/Load (PlayerPrefs) | ~100 | SaveService |
| History management | ~100 | ReactionNotebook |
| Rich text formatting helpers | ~80 | UIFormatting utility |
| Visual effects / particles | ~200 | FXController |
| Guidance messages | ~120 | GuidanceController |
| Quiz question builder | ~50 | QuizSystem |
| Localization inline calls | Scattered | LanguageService |
| Animated glow / VFX loop | ~80 | FXController |

### 1.2 What Works Well in v2 (Keep)

- **`ReactionEvaluator`** — Pure static evaluation logic, clean separation ✓
- **`ReactionModels`** — Data models are well-structured ✓
- **`AppManager`** — Singleton bootstrap pattern works ✓
- **`SecureReactionLoader`** + `CryptoUtil` — Encryption pipeline is solid ✓
- **`AppLanguageSettings`** — RTL support with RTLTMPro works ✓
- **`reactions.json`** schema — Rich, supports visual effects, safety, validation ✓

### 1.3 What Must Change

- LabController → decomposed into 6+ controllers
- All UI built programmatically → move to Prefabs + ViewControllers
- PlayerPrefs save → structured SaveService (JSON serialization)
- No audio system → dedicated AudioService
- Quiz is just a string → interactive QuizSystem
- Achievements have no screen → dedicated AchievementsScreen
- No settings scene → dedicated SettingsScreen
- Data hardcoded in code → data-driven from JSON/ScriptableObjects

---

## 2. Architecture Overview

```
┌──────────────────────────────────────────────────────────────┐
│                        SCENE LAYER                           │
│   Boot → Menu → Lab → Achievements → Settings                │
└──────────────┬───────────────────────────────────────────────┘
               │
┌──────────────▼───────────────────────────────────────────────┐
│                     UI LAYER (Views)                          │
│                                                              │
│  ┌────────────┐ ┌────────────┐ ┌──────────────┐             │
│  │  MenuView  │ │  LabView   │ │ AchievScreen │ ...         │
│  └────────────┘ └────────────┘ └──────────────┘             │
│  Prefab-based, no logic — only display & user input          │
└──────────────┬───────────────────────────────────────────────┘
               │ Events (up) / Data Binding (down)
┌──────────────▼───────────────────────────────────────────────┐
│                  GAMEPLAY LAYER (Controllers)                 │
│                                                              │
│  ┌───────────────────┐  ┌──────────────────┐                │
│  │ ReactionController│  │  UIController    │                │
│  │ (mix orchestration)│  │ (panel routing)  │                │
│  └───────────────────┘  └──────────────────┘                │
│  ┌───────────────────┐  ┌──────────────────┐                │
│  │ProgressController │  │AchievController  │                │
│  │(score,level,stats) │  │(unlock,tracking) │                │
│  └───────────────────┘  └──────────────────┘                │
│  ┌───────────────────┐  ┌──────────────────┐                │
│  │ChallengeController│  │ObjectiveController│               │
│  └───────────────────┘  └──────────────────┘                │
│  ┌───────────────────┐  ┌──────────────────┐                │
│  │  QuizController   │  │  FXController    │                │
│  └───────────────────┘  └──────────────────┘                │
│  ┌───────────────────┐                                      │
│  │GuidanceController │                                      │
│  └───────────────────┘                                      │
└──────────────┬───────────────────────────────────────────────┘
               │
┌──────────────▼───────────────────────────────────────────────┐
│                    DATA LAYER (Models)                        │
│                                                              │
│  ReactionDB, ReactionEntry, QuizQuestion, AchievementDef,   │
│  LessonDef, ChallengeDef, SafetyInfo, PlayerProgress        │
│                                                              │
│  SecureReactionLoader → ReactionRepository                   │
│  QuizDataLoader → QuizRepository                             │
│  AchievementDataLoader → AchievementRepository               │
└──────────────┬───────────────────────────────────────────────┘
               │
┌──────────────▼───────────────────────────────────────────────┐
│                  SERVICE LAYER (Cross-Cutting)                │
│                                                              │
│  ┌────────────┐ ┌────────────┐ ┌───────────────┐            │
│  │ SaveService│ │AudioService│ │LanguageService│            │
│  │(JSON file) │ │(SFX + UI) │ │  (AR / EN)    │            │
│  └────────────┘ └────────────┘ └───────────────┘            │
│  ┌────────────────┐ ┌──────────────────┐                    │
│  │ EventBus       │ │ SceneService     │                    │
│  │(SO Events)     │ │(scene transitions│                    │
│  └────────────────┘ └──────────────────┘                    │
└──────────────────────────────────────────────────────────────┘
               │
┌──────────────▼───────────────────────────────────────────────┐
│                   INFRASTRUCTURE                             │
│                                                              │
│  AppManager (Bootstrap + ServiceLocator)                     │
│  CryptoUtil + KeyMaterial (Security)                         │
│  UIFormatting (Rich Text Helpers)                            │
└──────────────────────────────────────────────────────────────┘
```

---

## 3. LabController Decomposition

### 3.1 ReactionController

**Responsibility:** Orchestrate the "Mix" action — read inputs, find reaction, evaluate, and broadcast results.

```
Fields:
  - ReactionDB db (injected)
  - ReactionEvaluationResult lastResult
  - ReactionEvaluationInput lastInput

Methods:
  + void OnMixRequested(ReactionInputData input)
  - ReactionEntry FindReaction(List<string> reagents)
  - ReactionEvaluationInput BuildInput(ReactionEntry, UIValues)
  + event Action<ReactionResultEvent> OnReactionCompleted
  + event Action<string> OnReactionFailed

Dependencies:
  - ReactionEvaluator (static, already exists)
  - EventBus (to broadcast results)
```

**Migrated from LabController:** `OnMix()`, `TryValidateSelectionBeforeMix()`, `TryBuildEvaluationInput()`, `TryFindReactionBySelectedReagents()`, `BuildSortedReagentKey()`

### 3.2 UIController

**Responsibility:** Manage Lab scene UI panels — route data to/from Views, respond to language changes.

```
Fields:
  - LabInputPanel inputPanel (Prefab reference)
  - ResultPanel resultPanel (Prefab reference)
  - DashboardPanel dashboardPanel
  - HistoryPanel historyPanel
  - HudPanel hudPanel

Methods:
  + void Initialize()
  + void ShowResult(ReactionResultEvent)
  + void UpdateDashboard(DashboardData)
  + void UpdateHud(HudData)
  + void OnLanguageChanged()

Dependencies:
  - LanguageService
  - EventBus (listens to ReactionCompleted, ProgressUpdated, etc.)
```

**Migrated from LabController:** All `Create*()` UI builder methods → **replaced by Prefabs**. `ApplyLocalizedUi()`, `ApplyHudLocalization()`, `RefreshReactionDashboard()`, `UpdateHudStatus()` → simplified to data-binding.

### 3.3 ProgressController

**Responsibility:** Track session score, streak, total experiments, level progression.

```
Fields:
  - int sessionScore
  - int sessionTotalExperiments
  - int sessionSuccessCount
  - int sessionStreak
  - int sessionBestScore
  - int currentLevel
  - string currentLessonTitle
  - int successfulExperimentsInLevel

Methods:
  + void OnReactionCompleted(ReactionResultEvent e)
  + int CalculateScoreDelta(ReactionEvaluationResult)
  + void UpdateLevelProgress(ReactionEvaluationResult)
  + ProgressSnapshot GetSnapshot()
  + event Action<ProgressSnapshot> OnProgressChanged
  + event Action<LevelUpEvent> OnLevelUp

Data Structures:
  ProgressSnapshot { score, total, successes, streak, best, level, lessonTitle }
  LevelUpEvent { newLevel, lessonTitle }

Dependencies:
  - SaveService (to persist/restore)
  - EventBus
```

**Migrated from LabController:** `sessionScore`, `sessionTotalExperiments`, `UpdateSessionProgress()`, `CalculateScoreDelta()`, `UpdateLevelProgress()`, `BuildProgressMessage()`, `GetPerformanceLevel()`, `BuildLevelMessage()`, `GetLessonTitleForLevel()`, save/load keys.

### 3.4 AchievementController

**Responsibility:** Define achievements, check unlock conditions, persist state.

```
Fields:
  - HashSet<string> unlockedAchievements
  - List<AchievementDefinition> allAchievements (data-driven)

Methods:
  + void OnReactionCompleted(ReactionResultEvent e)
  + void CheckUnlockConditions(ProgressSnapshot, ReactionResultEvent)
  + void TryUnlock(string id)
  + bool IsUnlocked(string id)
  + List<AchievementViewModel> GetAll()
  + event Action<AchievementUnlockedEvent> OnAchievementUnlocked

Data Structures:
  AchievementDefinition { id, nameEN, nameAR, descEN, descAR, iconId, condition }
  AchievementUnlockedEvent { achievement }

Dependencies:
  - SaveService
  - EventBus
  - ProgressController (reads snapshot)
```

**Migrated from LabController:** `unlockedAchievements`, `UpdateAchievements()`, `TryUnlockAchievement()`, `HasAchievement()`, `BuildAchievementMessage()`. **Achievement definitions move to JSON data.**

### 3.5 ChallengeController

**Responsibility:** Define per-level challenges, evaluate completion, award bonus points.

```
Fields:
  - ChallengeDef currentChallenge
  - bool challengeCompleted
  - List<ChallengeDef> allChallenges (data-driven)

Methods:
  + void OnLevelChanged(int level)
  + void OnReactionCompleted(ReactionResultEvent)
  + bool EvaluateChallenge(ChallengeDef, ReactionResultEvent)
  + ChallengeDef GetCurrentChallenge()
  + event Action<ChallengeCompletedEvent> OnChallengeCompleted

Data Structures:
  ChallengeDef { id, titleEN, titleAR, level, rewardPoints, condition }
  ChallengeCompletedEvent { challenge, reward }

Dependencies:
  - ProgressController (reads level)
  - EventBus
  - SaveService
```

**Migrated from LabController:** `ChallengeDefinition[]`, `UpdateChallengeProgress()`, `GetChallengeForCurrentLevel()`, `BuildChallengeMessage()`.

### 3.6 ObjectiveController

**Responsibility:** Manage per-level lesson objectives.

```
Fields:
  - ObjectiveDef currentObjective
  - bool objectiveCompleted

Methods:
  + void OnLevelChanged(int level)
  + void OnReactionCompleted(ReactionResultEvent)
  + bool EvaluateObjective(ReactionResultEvent)
  + event Action<ObjectiveCompletedEvent> OnObjectiveCompleted

Data Structures:
  ObjectiveDef { id, titleEN, titleAR, level, condition }

Dependencies:
  - ProgressController
  - EventBus
```

**Migrated from LabController:** `ObjectiveTitles[]`, `UpdateObjectiveProgress()`, `GetObjectiveForCurrentLevel()`, `BuildObjectiveMessage()`.

---

## 4. Independent Systems Design

### 4.1 Interactive Quiz System

```
Purpose: Test understanding after reactions, scored with feedback.

Architecture:
  QuizController
    - Listens to OnReactionCompleted
    - Selects question from QuizRepository based on reaction + level
    - Triggers QuizView to display
    - Evaluates answer → broadcasts QuizAnsweredEvent

  QuizView (Prefab)
    - Question text (localized)
    - 4 answer buttons (A/B/C/D)
    - Timer bar (optional)
    - Feedback panel (correct/incorrect + explanation)

  QuizRepository
    - Loads from quiz_questions.json
    - Filters by reaction ID, difficulty level
    - Returns QuizQuestion objects

  Data Model:
    QuizQuestion {
      id, reactionId, difficulty,
      questionEN, questionAR,
      answers: [ { textEN, textAR, isCorrect } ],
      explanationEN, explanationAR
    }

  Flow:
    ReactionCompleted → QuizController picks question
    → QuizView shows → User answers → QuizController evaluates
    → ProgressController.AddQuizScore() → QuizView shows feedback
```

**What changes from v2:** Currently `BuildQuizQuestion()` just returns a string. v3 makes it an interactive system with its own data source.

### 4.2 Language System (AR / EN)

```
Purpose: Bilingual UI with proper RTL support.

Architecture:
  LanguageService (replaces AppLanguageSettings — expanded)
    + CurrentLanguage → AppLanguage
    + void SetLanguage(AppLanguage)
    + string Localize(string key) — key-based lookup
    + string Localize(string en, string ar) — inline fallback
    + event Action<AppLanguage> OnLanguageChanged
    + void ApplyToText(TMP_Text, string key)

  LocalizationTable (new)
    - Loaded from localization.json
    - Dictionary<string, LocalizedEntry>
    - LocalizedEntry { en, ar }

  LocalizedText component (new MonoBehaviour)
    - Attach to any TMP_Text in Prefabs
    - [SerializeField] string localizationKey
    - Auto-updates on language change

  Data Model:
    localization.json {
      "menu.title": { "en": "ChemLab Simulator", "ar": "محاكي المختبر الكيميائي" },
      "lab.mix_button": { "en": "Mix!", "ar": "امزج!" },
      ...
    }

  What carries over from v2:
    - AppLanguage enum ✓
    - RTLTMPro ShapeArabic() ✓
    - PlayerPrefs persistence ✓
    - LanguageChanged event ✓

  What's new in v3:
    - Key-based localization (not inline strings)
    - LocalizedText auto-component for Prefabs
    - localization.json data file
    - Inline L("en","ar") still supported as fallback
```

### 4.3 Audio System

```
Purpose: SFX for reactions, UI sounds, ambient lab atmosphere.

Architecture:
  AudioService (new)
    - Singleton, DontDestroyOnLoad
    - AudioSource pool (3-5 sources for overlapping SFX)
    + void PlaySFX(string soundId, float volume = 1f)
    + void PlayUI(UISound sound)
    + void SetMasterVolume(float)
    + void SetSFXVolume(float)
    + void SetUIVolume(float)
    + void Mute(bool)

  Sound Registry (ScriptableObject)
    - Maps soundId → AudioClip
    - Categories: Reaction, UI, Ambient
    - Loaded at boot

  UISound enum:
    ButtonClick, ButtonHover, DropdownOpen, SliderChange,
    ToggleOn, ToggleOff, TabSwitch, ScreenTransition,
    AchievementUnlock, LevelUp, QuizCorrect, QuizWrong

  Integration points:
    - ReactionController.OnReactionCompleted → PlaySFX(reaction.visual_effects.sound_id)
    - AchievementController.OnUnlocked → PlaySFX("achievement_unlock")
    - ChallengeController.OnCompleted → PlaySFX("challenge_complete")
    - QuizController.OnAnswered → PlaySFX("quiz_correct" / "quiz_wrong")
    - All UI buttons → PlayUI(ButtonClick)

  Data from v2:
    - reactions.json already has sound_id field ✓
    - Need to create AudioClip assets and registry SO
```

### 4.4 Achievement UI Screen

```
Purpose: Dedicated scene showing all achievements with unlock status.

Architecture:
  AchievementScreen (Scene: "Achievements")
    AchievementScreenController
      - Gets List<AchievementViewModel> from AchievementController
      - Populates scroll list
      - Filters: All / Unlocked / Locked

    AchievementCardView (Prefab — instantiated per achievement)
      - Icon (locked = gray, unlocked = colored)
      - Title (localized)
      - Description (localized)
      - Unlock date (if unlocked)
      - Progress bar (if applicable)

    AchievementScreenView (Prefab — scene root)
      - Header with title + back button
      - Filter tabs (All / Unlocked / Locked)
      - ScrollRect with vertical layout
      - Stats footer (X/Y unlocked)

  Navigation:
    Menu → Achievements button → Load "Achievements" scene
    Achievements → Back button → Load "Menu" scene
```

### 4.5 Progress Dashboard UI

```
Purpose: Visual overview of player progress, stats, level.

Architecture:
  ProgressDashboard (embedded in Lab scene + accessible from Menu)
    ProgressDashboardController
      - Reads ProgressSnapshot from ProgressController
      - Updates view on OnProgressChanged

    ProgressDashboardView (Prefab)
      - Score display (animated counter)
      - Level indicator (with lesson title)
      - XP bar (experiments toward next level)
      - Streak counter (flame icon)
      - Session stats (total / success / invalid)
      - Performance badge (Beginner → Expert)
      - Current objective display
      - Current challenge display

  In Lab Scene:
    - Compact mode (top bar: score + level + streak)
    - Expandable to full dashboard overlay

  In Menu Scene:
    - "Progress" button → shows full dashboard modal
```

### 4.6 Reaction Notebook (سجل منظم)

```
Purpose: Persistent log of all performed experiments with details.

Architecture:
  ReactionNotebook (replaces in-memory experimentHistory)
    NotebookController
      - Listens to OnReactionCompleted
      - Creates NotebookEntry
      - Persists via SaveService
      + List<NotebookEntry> GetEntries(filter?)
      + NotebookEntry GetEntry(int index)

    NotebookView (Prefab — overlay panel in Lab)
      - ScrollRect with entry cards
      - Each card shows: reagents, conditions, outcome, score, timestamp
      - Tap to expand → full details + chemical equation + safety notes
      - Filter by: Success / Fail / Partial
      - Search by reagent name

    NotebookEntry {
      id, timestamp,
      reagents: List<string>,
      medium, temperature, stirring, grinding, catalyst,
      outcome: ReactionStatus,
      scoreDelta,
      reactionId,
      chemicalEquation,
      safetyNotes: List<string>,
      quizResult (if applicable)
    }

  What changes from v2:
    - v2 had ExperimentHistoryEntry (3 fields, in-memory only)
    - v3: full structured entry, persisted, searchable, expandable
```

---

## 5. Scene Structure

```
Boot (Scene 0)
  ├─ AppManager (DontDestroyOnLoad)
  │   ├─ SecureReactionLoader
  │   ├─ SaveService
  │   ├─ AudioService
  │   ├─ LanguageService
  │   └─ EventBus
  ├─ BootController
  │   ├─ Load all data
  │   ├─ Validate
  │   └─ Transition → Menu
  └─ Splash screen (optional)

Menu (Scene 1)
  ├─ MenuController
  ├─ MenuView (Prefab)
  │   ├─ Title + subtitle
  │   ├─ Start Lab button
  │   ├─ Achievements button
  │   ├─ Settings button
  │   ├─ Progress summary widget
  │   └─ Language toggle
  └─ MenuUIEnhancer (keep, refactored)

Lab (Scene 2)
  ├─ ReactionController
  ├─ UIController
  ├─ ProgressController
  ├─ AchievementController
  ├─ ChallengeController
  ├─ ObjectiveController
  ├─ QuizController
  ├─ GuidanceController
  ├─ FXController
  ├─ NotebookController
  └─ Lab UI (Prefabs)
      ├─ LabInputPanel
      ├─ ResultPanel
      ├─ DashboardPanel
      ├─ HudPanel
      ├─ NotebookPanel (overlay)
      ├─ QuizPanel (overlay)
      └─ BackButton

Achievements (Scene 3)
  ├─ AchievementScreenController
  └─ AchievementScreenView (Prefab)

Settings (Scene 4)
  ├─ SettingsController
  └─ SettingsView (Prefab)
      ├─ Language selection (AR / EN)
      ├─ Audio volume (Master, SFX, UI)
      ├─ Reset progress (with confirmation)
      └─ About / Credits
```

---

## 6. Data Model (Expandable)

### 6.1 File Structure

```
Assets/_Project/
  DataSrc/
    reactions.json          ← exists (keep, expand)
    quiz_questions.json     ← NEW
    achievements.json       ← NEW
    lessons.json            ← NEW
    challenges.json         ← NEW
    localization.json       ← NEW
    audio_registry.json     ← NEW (or ScriptableObject)
  DataSecure/
    reactions.bytes         ← exists (keep)
    quiz_questions.bytes    ← NEW (if encrypted)
```

### 6.2 reactions.json (v3 — expanded)

Existing schema is kept. Additions:

```json
{
  "reactions": [
    {
      "id": "rxn_001",
      "name_ar": "...",
      "name_en": "...",
      "reactants": [...],
      "products": [...],
      "activationTempC": 25,
      "requiredMedium": "Neutral",
      "catalystAllowed": false,
      "catalystDeltaTempC": 0,
      "visual_effects": {...},
      "safety": {...},
      "validation": {...},

      "// v3 additions below": "",
      "difficulty": 1,
      "lesson_id": "lesson_001",
      "tags": ["acid-base", "neutralization"],
      "educational_note_en": "This is a classic neutralization...",
      "educational_note_ar": "هذا تفاعل تعادل كلاسيكي..."
    }
  ]
}
```

### 6.3 quiz_questions.json (NEW)

```json
{
  "questions": [
    {
      "id": "q_001",
      "reaction_id": "rxn_001",
      "difficulty": 1,
      "question_en": "What is the product of HCl + NaOH?",
      "question_ar": "ما ناتج تفاعل HCl مع NaOH؟",
      "answers": [
        { "text_en": "NaCl + H₂O", "text_ar": "NaCl + H₂O", "is_correct": true },
        { "text_en": "NaOH + HCl", "text_ar": "NaOH + HCl", "is_correct": false },
        { "text_en": "Na₂O + Cl₂", "text_ar": "Na₂O + Cl₂", "is_correct": false },
        { "text_en": "H₂ + NaCl",  "text_ar": "H₂ + NaCl",  "is_correct": false }
      ],
      "explanation_en": "Acid-base neutralization produces salt and water.",
      "explanation_ar": "تفاعل التعادل ينتج ملحاً وماءً."
    }
  ]
}
```

### 6.4 achievements.json (NEW)

```json
{
  "achievements": [
    {
      "id": "ach_first_reaction",
      "name_en": "First Successful Reaction",
      "name_ar": "أول تفاعل ناجح",
      "desc_en": "Complete your first successful reaction.",
      "desc_ar": "أكمل أول تفاعل ناجح.",
      "icon_id": "icon_flask",
      "condition": {
        "type": "success_count",
        "value": 1
      }
    },
    {
      "id": "ach_level_2",
      "name_en": "Reach Level 2",
      "name_ar": "الوصول للمستوى 2",
      "desc_en": "Progress to Level 2.",
      "desc_ar": "تقدم إلى المستوى 2.",
      "icon_id": "icon_star",
      "condition": {
        "type": "level_reached",
        "value": 2
      }
    },
    {
      "id": "ach_5_experiments",
      "name_en": "Complete 5 Experiments",
      "name_ar": "أكمل 5 تجارب",
      "desc_en": "Perform 5 experiments in a session.",
      "desc_ar": "نفّذ 5 تجارب في جلسة واحدة.",
      "icon_id": "icon_beaker",
      "condition": {
        "type": "experiment_count",
        "value": 5
      }
    },
    {
      "id": "ach_score_100",
      "name_en": "Score 100",
      "name_ar": "احصل على 100 نقطة",
      "desc_en": "Reach a score of 100.",
      "desc_ar": "حقق 100 نقطة.",
      "icon_id": "icon_trophy",
      "condition": {
        "type": "score_reached",
        "value": 100
      }
    },
    {
      "id": "ach_catalyst_master",
      "name_en": "Use Catalyst Correctly",
      "name_ar": "استخدم المحفز بشكل صحيح",
      "desc_en": "Successfully use a catalyst in a reaction.",
      "desc_ar": "استخدم محفزاً بنجاح في تفاعل.",
      "icon_id": "icon_catalyst",
      "condition": {
        "type": "catalyst_success",
        "value": 1
      }
    },
    {
      "id": "ach_first_challenge",
      "name_en": "Complete First Challenge",
      "name_ar": "أكمل أول تحدي",
      "desc_en": "Complete your first challenge.",
      "desc_ar": "أكمل أول تحدٍّ لك.",
      "icon_id": "icon_medal",
      "condition": {
        "type": "challenge_count",
        "value": 1
      }
    }
  ]
}
```

### 6.5 lessons.json (NEW)

```json
{
  "lessons": [
    {
      "id": "lesson_001",
      "level": 1,
      "title_en": "Basic Reactions",
      "title_ar": "التفاعلات الأساسية",
      "description_en": "Learn about simple acid-base and displacement reactions.",
      "description_ar": "تعرف على تفاعلات الحموض والقواعد والإزاحة البسيطة.",
      "objective": {
        "title_en": "Perform one valid successful reaction.",
        "title_ar": "نفّذ تفاعلاً واحداً ناجحاً.",
        "condition": { "type": "success_in_level", "value": 1 }
      },
      "unlock_requirement": null,
      "reaction_ids": ["rxn_001", "rxn_002"]
    },
    {
      "id": "lesson_002",
      "level": 2,
      "title_en": "Medium and Temperature",
      "title_ar": "الوسط ودرجة الحرارة",
      "description_en": "Explore how medium pH and temperature affect reactions.",
      "description_ar": "اكتشف تأثير الوسط ودرجة الحرارة على التفاعلات.",
      "objective": {
        "title_en": "Complete a reaction using the correct medium.",
        "title_ar": "أكمل تفاعلاً باستخدام الوسط الصحيح.",
        "condition": { "type": "medium_match_in_level", "value": 1 }
      },
      "unlock_requirement": { "type": "level_reached", "value": 2 },
      "reaction_ids": ["rxn_003", "rxn_004"]
    },
    {
      "id": "lesson_003",
      "level": 3,
      "title_en": "Catalyst and Contact",
      "title_ar": "المحفز والتلامس",
      "description_en": "Understand catalysts and surface contact factors.",
      "description_ar": "افهم دور المحفز وعوامل التلامس السطحي.",
      "objective": {
        "title_en": "Complete a reaction with strong contact or proper catalyst use.",
        "title_ar": "أكمل تفاعلاً بتلامس قوي أو استخدام محفز صحيح.",
        "condition": { "type": "contact_or_catalyst", "value": 1 }
      },
      "unlock_requirement": { "type": "level_reached", "value": 3 },
      "reaction_ids": ["rxn_005"]
    },
    {
      "id": "lesson_004",
      "level": 4,
      "title_en": "Advanced Reaction Conditions",
      "title_ar": "ظروف التفاعل المتقدمة",
      "description_en": "Master all reaction parameters for complex reactions.",
      "description_ar": "أتقن جميع معاملات التفاعل للتفاعلات المعقدة.",
      "objective": {
        "title_en": "Complete an advanced successful reaction under correct conditions.",
        "title_ar": "أكمل تفاعلاً متقدماً ناجحاً في الظروف الصحيحة.",
        "condition": { "type": "advanced_success", "value": 1 }
      },
      "unlock_requirement": { "type": "level_reached", "value": 4 },
      "reaction_ids": ["rxn_006", "rxn_007"]
    }
  ]
}
```

### 6.6 challenges.json (NEW)

```json
{
  "challenges": [
    {
      "id": "ch_001",
      "level": 1,
      "title_en": "Complete a successful reaction without catalyst",
      "title_ar": "أكمل تفاعلاً ناجحاً بدون محفز",
      "reward_points": 10,
      "condition": { "type": "success_no_catalyst", "value": 1 }
    },
    {
      "id": "ch_002",
      "level": 2,
      "title_en": "Use the correct medium in one attempt",
      "title_ar": "استخدم الوسط الصحيح من أول محاولة",
      "reward_points": 10,
      "condition": { "type": "medium_match_first_try", "value": 1 }
    },
    {
      "id": "ch_003",
      "level": 3,
      "title_en": "Reach a strong contact factor (≥1.2)",
      "title_ar": "حقق عامل تلامس قوي (≥1.2)",
      "reward_points": 10,
      "condition": { "type": "contact_factor_gte", "value": 1.2 }
    },
    {
      "id": "ch_004",
      "level": 4,
      "title_en": "Complete two successful reactions in a row",
      "title_ar": "أكمل تفاعلين ناجحين متتاليين",
      "reward_points": 10,
      "condition": { "type": "streak_gte", "value": 2 }
    }
  ]
}
```

### 6.7 Player Save Data (JSON — SaveService)

```json
{
  "version": 3,
  "sessionScore": 0,
  "sessionBestScore": 0,
  "sessionTotalExperiments": 0,
  "sessionSuccessCount": 0,
  "currentLevel": 1,
  "successfulExperimentsInLevel": 0,
  "unlockedAchievements": ["ach_first_reaction"],
  "completedChallenges": ["ch_001"],
  "completedObjectives": ["lesson_001"],
  "notebook": [
    {
      "id": "entry_001",
      "timestamp": "2026-04-07T10:30:00Z",
      "reactionId": "rxn_001",
      "reagents": ["HCl", "NaOH"],
      "medium": "Neutral",
      "temperature": 25,
      "stirring": 0.5,
      "grinding": 0.5,
      "catalyst": false,
      "outcome": "Success",
      "scoreDelta": 15
    }
  ],
  "quizScores": {
    "q_001": true,
    "q_003": false
  },
  "settings": {
    "language": "en",
    "masterVolume": 1.0,
    "sfxVolume": 1.0,
    "uiVolume": 1.0
  }
}
```

---

## 7. Communication Between Systems

### 7.1 Event Bus (ScriptableObject Events)

The primary communication mechanism. Controllers never reference each other directly.

```
Event Flow Example — "Mix" Button Pressed:

  User clicks Mix
    → LabInputPanel.OnMixClicked()
    → UIController reads input values
    → ReactionController.OnMixRequested(inputData)
        → Evaluates reaction
        → Fires: EventBus.ReactionCompleted(resultEvent)
            ├→ UIController         → ShowResult()
            ├→ ProgressController   → UpdateScore(), UpdateLevel()
            │   └→ Fires: EventBus.ProgressChanged(snapshot)
            │       ├→ UIController → UpdateDashboard()
            │       └→ EventBus.LevelUp (if applicable)
            │           ├→ ChallengeController  → LoadNewChallenge()
            │           └→ ObjectiveController  → LoadNewObjective()
            ├→ AchievementController → CheckUnlocks()
            │   └→ Fires: EventBus.AchievementUnlocked
            │       ├→ UIController → ShowUnlockToast()
            │       └→ AudioService → PlaySFX("achievement")
            ├→ ChallengeController  → CheckCompletion()
            ├→ ObjectiveController  → CheckCompletion()
            ├→ QuizController       → MaybeShowQuiz()
            ├→ FXController         → PlayReactionFX()
            ├→ AudioService         → PlaySFX(sound_id)
            ├→ NotebookController   → AddEntry()
            └→ SaveService          → AutoSave()
```

### 7.2 Event Types

```csharp
// ScriptableObject-based events (or C# events on a central EventBus)

ReactionCompletedEvent {
    ReactionEvaluationResult result;
    ReactionEvaluationInput input;
    int scoreDelta;
}

ProgressChangedEvent {
    ProgressSnapshot snapshot;
}

LevelUpEvent {
    int newLevel;
    string lessonTitle;
}

AchievementUnlockedEvent {
    AchievementDefinition achievement;
}

ChallengeCompletedEvent {
    ChallengeDef challenge;
    int rewardPoints;
}

ObjectiveCompletedEvent {
    ObjectiveDef objective;
}

QuizAnsweredEvent {
    QuizQuestion question;
    bool correct;
}

LanguageChangedEvent {
    AppLanguage language;
}
```

### 7.3 Dependency Graph

```
                    ┌──────────────┐
                    │  EventBus    │ ← Central message broker
                    └──────┬───────┘
                           │
        ┌──────────────────┼──────────────────────┐
        │                  │                      │
   ┌────▼─────┐    ┌──────▼──────┐    ┌──────────▼────────┐
   │Reaction  │    │  Progress   │    │  Achievement      │
   │Controller│    │  Controller │    │  Controller       │
   └────┬─────┘    └──────┬──────┘    └───────────────────┘
        │                 │
   ┌────▼─────┐    ┌──────▼──────┐
   │ Reaction │    │  Challenge  │
   │ Evaluator│    │  Controller │
   │ (static) │    └──────┬──────┘
   └──────────┘           │
                   ┌──────▼──────┐
                   │  Objective  │
                   │  Controller │
                   └─────────────┘

Services (no direct dependencies between them):
  SaveService ← used by: Progress, Achievement, Challenge, Objective, Notebook
  AudioService ← used by: FX, UI, Quiz, Achievement
  LanguageService ← used by: all Views
```

---

## 8. Folder Structure (v3)

```
Assets/_Project/
├── _Core.asmdef
├── Scripts/
│   ├── Core/
│   │   ├── AppManager.cs                 ← KEEP (expand as bootstrapper)
│   │   ├── EventBus.cs                   ← NEW
│   │   └── GameEvents.cs                 ← NEW (event type definitions)
│   │
│   ├── Controllers/
│   │   ├── ReactionController.cs         ← NEW (from LabController)
│   │   ├── UIController.cs               ← NEW (from LabController)
│   │   ├── ProgressController.cs         ← NEW (from LabController)
│   │   ├── AchievementController.cs      ← NEW (from LabController)
│   │   ├── ChallengeController.cs        ← NEW (from LabController)
│   │   ├── ObjectiveController.cs        ← NEW (from LabController)
│   │   ├── QuizController.cs             ← NEW
│   │   ├── GuidanceController.cs         ← NEW (from LabController)
│   │   ├── FXController.cs               ← NEW (from LabController)
│   │   ├── NotebookController.cs         ← NEW
│   │   ├── MenuController.cs             ← KEEP (simplify)
│   │   ├── SettingsController.cs         ← NEW
│   │   └── AchievementScreenController.cs ← NEW
│   │
│   ├── Views/
│   │   ├── Lab/
│   │   │   ├── LabInputPanel.cs          ← NEW (Prefab code-behind)
│   │   │   ├── ResultPanel.cs            ← NEW
│   │   │   ├── DashboardPanel.cs         ← NEW
│   │   │   ├── HudPanel.cs              ← NEW
│   │   │   ├── NotebookPanel.cs          ← NEW
│   │   │   └── QuizPanel.cs             ← NEW
│   │   ├── Menu/
│   │   │   └── MenuView.cs              ← REFACTOR (from MenuUIEnhancer)
│   │   ├── Achievements/
│   │   │   ├── AchievementScreenView.cs  ← NEW
│   │   │   └── AchievementCardView.cs    ← NEW
│   │   ├── Settings/
│   │   │   └── SettingsView.cs           ← NEW
│   │   └── Shared/
│   │       ├── LocalizedText.cs          ← NEW (auto-localize component)
│   │       └── UIFormatting.cs           ← NEW (from LabController C class)
│   │
│   ├── Data/
│   │   ├── Models/
│   │   │   ├── ReactionModels.cs         ← KEEP
│   │   │   ├── QuizModels.cs             ← NEW
│   │   │   ├── AchievementModels.cs      ← NEW
│   │   │   ├── LessonModels.cs           ← NEW
│   │   │   ├── ChallengeModels.cs        ← NEW
│   │   │   └── PlayerProgress.cs         ← NEW (save data model)
│   │   ├── Repositories/
│   │   │   ├── ReactionRepository.cs     ← NEW (wraps SecureReactionLoader)
│   │   │   ├── QuizRepository.cs         ← NEW
│   │   │   ├── AchievementRepository.cs  ← NEW
│   │   │   ├── LessonRepository.cs       ← NEW
│   │   │   └── ChallengeRepository.cs    ← NEW
│   │   └── Loaders/
│   │       ├── SecureReactionLoader.cs   ← KEEP
│   │       └── JsonDataLoader.cs         ← NEW (generic JSON loader)
│   │
│   ├── Services/
│   │   ├── SaveService.cs                ← NEW (JSON file-based)
│   │   ├── AudioService.cs               ← NEW
│   │   ├── LanguageService.cs            ← REFACTOR (from AppLanguageSettings)
│   │   └── SceneService.cs               ← NEW (scene transition helper)
│   │
│   ├── Logic/
│   │   └── ReactionEvaluator.cs          ← KEEP (as-is)
│   │
│   ├── Security/
│   │   ├── CryptoUtil.cs                 ← KEEP
│   │   └── KeyMaterial.cs                ← KEEP
│   │
│   └── UI/
│       └── CanvasScalerFixer.cs          ← KEEP
│
├── Prefabs/                              ← NEW (all UI as prefabs)
│   ├── Lab/
│   │   ├── LabInputPanel.prefab
│   │   ├── ResultPanel.prefab
│   │   ├── DashboardPanel.prefab
│   │   ├── HudPanel.prefab
│   │   ├── NotebookPanel.prefab
│   │   └── QuizPanel.prefab
│   ├── Menu/
│   │   └── MenuView.prefab
│   ├── Achievements/
│   │   ├── AchievementScreenView.prefab
│   │   └── AchievementCard.prefab
│   ├── Settings/
│   │   └── SettingsView.prefab
│   └── Shared/
│       ├── ToastNotification.prefab
│       └── ConfirmDialog.prefab
│
├── DataSrc/
│   ├── reactions.json                    ← KEEP (expand)
│   ├── quiz_questions.json               ← NEW
│   ├── achievements.json                 ← NEW
│   ├── lessons.json                      ← NEW
│   ├── challenges.json                   ← NEW
│   └── localization.json                 ← NEW
│
├── DataSecure/
│   ├── reactions.bytes                   ← KEEP
│   └── reactions.manifest.json           ← KEEP
│
├── Audio/                                ← NEW
│   ├── SFX/
│   │   ├── sizzle.wav
│   │   ├── fizz.wav
│   │   ├── pop.wav
│   │   └── ...
│   ├── UI/
│   │   ├── click.wav
│   │   ├── hover.wav
│   │   └── ...
│   └── SoundRegistry.asset              ← ScriptableObject
│
├── Scenes/
│   ├── Boot.unity                        ← KEEP
│   ├── Menu.unity                        ← KEEP (refactor)
│   ├── Lab.unity                         ← REFACTOR (was "Lab Scene")
│   ├── Achievements.unity                ← NEW
│   └── Settings.unity                    ← NEW
│
└── Fronts/
    └── NotoSansArabic-Regular SDF.asset  ← KEEP
```

---

## 9. Migration Plan: v2 → v3

### Phase 0: Preparation

| Step | Action | Risk |
|---|---|---|
| 0.1 | Create `v3-dev` branch from `main` | None |
| 0.2 | Tag v2 release as `v2.0.0` | None |
| 0.3 | Create new folder structure (empty) | None |
| 0.4 | Create JSON data files from hardcoded v2 data | Low |

### Phase 1: Infrastructure (Foundation)

| Step | Action | Source |
|---|---|---|
| 1.1 | Create `EventBus` + `GameEvents` | New |
| 1.2 | Create `SaveService` (JSON-based) | New (replaces PlayerPrefs scattered in LabController) |
| 1.3 | Refactor `AppLanguageSettings` → `LanguageService` | Existing → expand |
| 1.4 | Create `AudioService` (stub — no clips yet) | New |
| 1.5 | Create `SceneService` | New |
| 1.6 | Update `AppManager` as bootstrapper | Existing → expand |

### Phase 2: Data Layer

| Step | Action | Source |
|---|---|---|
| 2.1 | Keep `ReactionModels.cs` + `SecureReactionLoader` as-is | Existing ✓ |
| 2.2 | Create `QuizModels`, `AchievementModels`, `LessonModels`, `ChallengeModels` | New |
| 2.3 | Create `PlayerProgress` model | New |
| 2.4 | Create `JsonDataLoader` (generic) | New |
| 2.5 | Create repositories for each data type | New |
| 2.6 | Write `quiz_questions.json`, `achievements.json`, `lessons.json`, `challenges.json` | Extracted from LabController + new |

### Phase 3: Controller Extraction (Critical)

| Step | Action | Lines from LabController |
|---|---|---|
| 3.1 | Extract `ProgressController` | ~400 lines (score, level, stats, save/load) |
| 3.2 | Extract `AchievementController` | ~150 lines (unlock logic, persistence) |
| 3.3 | Extract `ChallengeController` | ~120 lines (challenge eval, progression) |
| 3.4 | Extract `ObjectiveController` | ~100 lines (objective eval) |
| 3.5 | Extract `ReactionController` | ~300 lines (mix, find, evaluate, validate) |
| 3.6 | Extract `FXController` | ~200 lines (particles, glow, VFX) |
| 3.7 | Extract `GuidanceController` | ~120 lines (hint messages) |
| 3.8 | Extract `NotebookController` | ~100 lines (history → notebook) |
| 3.9 | Create `QuizController` | New (~200 lines) |
| 3.10 | Create `UIController` (orchestrator) | New (~200 lines) |
| 3.11 | **Delete `LabController.cs`** | 3,855 lines → 0 |

### Phase 4: UI Layer (Prefabs)

| Step | Action | Source |
|---|---|---|
| 4.1 | Design `LabInputPanel` prefab (dropdowns, sliders, toggle, mix button) | Replace programmatic creation |
| 4.2 | Design `ResultPanel` prefab | Replace programmatic creation |
| 4.3 | Design `DashboardPanel` prefab | Replace programmatic creation |
| 4.4 | Design `HudPanel` prefab | Replace programmatic creation |
| 4.5 | Design `NotebookPanel` prefab | New |
| 4.6 | Design `QuizPanel` prefab | New |
| 4.7 | Refactor `MenuView` prefab | From MenuUIEnhancer |
| 4.8 | Create `AchievementScreenView` + `AchievementCard` prefabs | New |
| 4.9 | Create `SettingsView` prefab | New |
| 4.10 | Create `LocalizedText` component | New |
| 4.11 | Move `UIFormatting` (rich text helpers) to shared utility | From LabController.C class |

### Phase 5: Scenes

| Step | Action |
|---|---|
| 5.1 | Refactor `Boot.unity` — add all persistent services |
| 5.2 | Refactor `Menu.unity` — use MenuView prefab, add Achievement/Settings buttons |
| 5.3 | Refactor `Lab Scene.unity` → `Lab.unity` — wire all controllers + prefabs |
| 5.4 | Create `Achievements.unity` |
| 5.5 | Create `Settings.unity` |

### Phase 6: New Features

| Step | Action |
|---|---|
| 6.1 | Implement interactive Quiz flow |
| 6.2 | Add audio clips + wire AudioService |
| 6.3 | Implement Reaction Notebook persistence + search |
| 6.4 | Wire localization.json + LocalizedText components |
| 6.5 | Implement Settings screen (volume, language, reset) |

### Phase 7: Validation

| Step | Action |
|---|---|
| 7.1 | Compile check — zero errors |
| 7.2 | Test Boot → Menu → Lab flow |
| 7.3 | Test reaction evaluation (same results as v2) |
| 7.4 | Test save/load migration |
| 7.5 | Test AR/EN language switching |
| 7.6 | Test all new scenes |
| 7.7 | Performance profiling |

---

## 10. What We Keep vs. Rebuild

### KEEP (as-is or minimal changes)

| File | Reason |
|---|---|
| `ReactionEvaluator.cs` | Pure logic, well-separated, correct |
| `ReactionModels.cs` | Data models are solid |
| `SecureReactionLoader.cs` | Encryption pipeline works |
| `CryptoUtil.cs` + `KeyMaterial.cs` | Security layer is clean |
| `CanvasScalerFixer.cs` | Utility, still needed |
| `reactions.json` schema | Expand, don't replace |
| `_Core.asmdef` | Assembly definition |
| RTLTMPro integration | Arabic shaping works |

### REFACTOR (keep concept, rewrite structure)

| Component | From → To |
|---|---|
| `AppLanguageSettings` | Static class → `LanguageService` + `LocalizedText` + `localization.json` |
| `AppManager` | Singleton → Bootstrapper with service registration |
| `MenuController` + `MenuUIEnhancer` | Merged → `MenuController` + `MenuView` prefab |
| Experiment History | In-memory list → `NotebookController` + `SaveService` |

### REBUILD (new architecture)

| Component | Reason |
|---|---|
| `LabController` (3,855 lines) | God class → 10 focused controllers |
| All programmatic UI creation | Code → Prefab-based views |
| Save/Load (scattered PlayerPrefs) | → Centralized `SaveService` (JSON) |
| Achievement system | Hardcoded strings → data-driven JSON |
| Challenge system | Hardcoded array → data-driven JSON |
| Objective system | Hardcoded array → data-driven JSON |
| Level/Lesson definitions | Hardcoded → `lessons.json` |

### NEW (didn't exist in v2)

| Component |
|---|
| Interactive Quiz System (with UI) |
| Audio System |
| Achievement Screen (scene) |
| Settings Screen (scene) |
| Reaction Notebook (persistent, searchable) |
| EventBus (system communication) |
| Localization table (localization.json) |
| SceneService (transitions) |
| All Prefabs (UI building blocks) |

---

## 11. Risks & Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Breaking reaction evaluation | High | `ReactionEvaluator` stays untouched; same input/output contract |
| Save data incompatibility | Medium | `SaveService` includes v2 → v3 migration on first load |
| Encryption pipeline disruption | High | `SecureReactionLoader` stays as-is; new data files use plain JSON initially |
| UI regression (layout/styling) | Medium | Build prefabs to match v2 visual output first, then improve |
| Scope creep | High | Phase 1-3 = foundation; Phase 4-6 = features; strict phase gates |

---

## 12. Summary

| Metric | v2 | v3 |
|---|---|---|
| LabController lines | 3,855 | 0 (decomposed) |
| Controllers | 1 god class | 10+ focused controllers |
| UI creation | 100% programmatic | 100% prefab-based |
| Data files | 1 (reactions.json) | 6+ JSON files |
| Scenes | 3 (Boot, Menu, Lab) | 5 (+ Achievements, Settings) |
| Save system | Scattered PlayerPrefs | Centralized JSON SaveService |
| Audio | None | Full AudioService |
| Quiz | String builder | Interactive system |
| Localization | Inline L() calls | Key-based + LocalizedText component |
| System communication | Direct method calls | EventBus (decoupled) |
