// ChemLabSim v3 — Game Event Definitions
// All event structs implement IGameEvent for type-safety with EventBus.
// Each struct is a plain data carrier — no logic.

namespace ChemLabSimV3.Events
{
    /// <summary>Marker interface for all events flowing through <see cref="EventBus"/>.</summary>
    public interface IGameEvent { }

    // ----------------------------------------------
    //  Reaction Events
    // ----------------------------------------------

    /// <summary>Fired after a reaction is evaluated (success, fail, or partial).</summary>
    public struct ReactionEvaluatedEvent : IGameEvent
    {
        public ReactionEvaluationInput Input;
        public ReactionEvaluationResult Result;

        public ReactionEvaluatedEvent(ReactionEvaluationInput input, ReactionEvaluationResult result)
        {
            Input = input;
            Result = result;
        }
    }

    /// <summary>Fired when the user's mix request cannot be matched to any known reaction.</summary>
    public struct ReactionNotFoundEvent : IGameEvent
    {
        public string Message;
    }

    // ----------------------------------------------
    //  Progress Events
    // ----------------------------------------------

    /// <summary>Fired after ProgressController updates session state (score, level, counters).</summary>
    public struct ProgressUpdatedEvent : IGameEvent
    {
        public Data.ProgressState State;
        public int ScoreDelta;
    }

    /// <summary>Fired when the player advances to a new level.</summary>
    public struct LevelUpEvent : IGameEvent
    {
        public int NewLevel;
        public string LessonTitle;
    }

    // ----------------------------------------------
    //  Achievement Events
    // ----------------------------------------------

    /// <summary>Fired when an achievement is unlocked.</summary>
    public struct AchievementUnlockedEvent : IGameEvent
    {
        public string AchievementId;
        public string DisplayName;
    }

    // ----------------------------------------------
    //  Challenge Events
    // ----------------------------------------------

    /// <summary>Fired when a new challenge is assigned for the current level.</summary>
    public struct ChallengeAssignedEvent : IGameEvent
    {
        public string ChallengeId;
        public string Title;
        public int Level;
    }

    /// <summary>Fired when the current challenge is completed.</summary>
    public struct ChallengeCompletedEvent : IGameEvent
    {
        public string ChallengeId;
        public int RewardPoints;
    }

    // ----------------------------------------------
    //  Objective Events
    // ----------------------------------------------

    /// <summary>Fired when a new objective is assigned for the current level.</summary>
    public struct ObjectiveAssignedEvent : IGameEvent
    {
        public string ObjectiveId;
        public string Title;
        public int Level;
    }

    /// <summary>Fired when the current lesson objective is completed.</summary>
    public struct ObjectiveCompletedEvent : IGameEvent
    {
        public string ObjectiveId;
    }

    // ----------------------------------------------
    //  Quiz Events
    // ----------------------------------------------

    /// <summary>Fired when it is time to show a quiz question.</summary>
    public struct QuizRequestedEvent : IGameEvent
    {
        public string ReactionId;
    }

    /// <summary>Fired after the player answers a quiz question.</summary>
    public struct QuizAnsweredEvent : IGameEvent
    {
        public string QuestionId;
        public bool Correct;
    }

    /// <summary>Fired by QuizPanelView when the student clicks an answer button.
    /// QuizController determines correctness — the view has no logic.</summary>
    public struct QuizOptionSelectedEvent : IGameEvent
    {
        public int SelectedIndex;
    }

    // ----------------------------------------------
    //  Language Events
    // ----------------------------------------------

    /// <summary>Fired when the display language changes.</summary>
    public struct LanguageChangedEvent : IGameEvent
    {
        public int LanguageIndex;  // 0 = English, 1 = Arabic
    }

    // ----------------------------------------------
    //  Input Events
    // ----------------------------------------------

    /// <summary>Fired when any lab input control changes (reagent, slider, toggle).</summary>
    public struct InputChangedEvent : IGameEvent { }

    // ----------------------------------------------
    //  Guidance Events
    // ----------------------------------------------

    /// <summary>Fired by GuidanceController when guidance state changes.</summary>
    public struct GuidanceUpdatedEvent : IGameEvent
    {
        public Data.GuidanceState State;
    }

    // ----------------------------------------------
    //  Quiz Update Events
    // ----------------------------------------------

    /// <summary>Fired by QuizController with a contextual question after a reaction.</summary>
    public struct QuizUpdatedEvent : IGameEvent
    {
        public Data.QuizState State;
    }

    // ----------------------------------------------
    //  FX Events
    // ----------------------------------------------

    /// <summary>Fired by FXController describing which effects to play/stop.</summary>
    public struct FxTriggeredEvent : IGameEvent
    {
        public Data.FxState State;
    }

    // ----------------------------------------------
    //  Notebook Events
    // ----------------------------------------------

    /// <summary>Fired after NotebookController updates its entry list.</summary>
    public struct NotebookUpdatedEvent : IGameEvent
    {
        public System.Collections.Generic.IReadOnlyList<Data.NotebookEntry> Entries;
        public int EntryCounter;
    }

    // ----------------------------------------------
    //  Scene Events
    // ----------------------------------------------

    /// <summary>Fired just before a scene transition begins.</summary>
    public struct SceneTransitionEvent : IGameEvent
    {
        public string TargetScene;
    }
}
