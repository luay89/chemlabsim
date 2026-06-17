// ChemLabSim v3 — Guided Experiment Data Models
// Pure C# — no Unity dependencies.
// Defines the structure for step-by-step guided lab experiments.

using System.Collections.Generic;

namespace ChemLabSimV3.Domain.GuidedExperiments
{
    /// <summary>
    /// Defines a complete guided experiment.
    /// </summary>
    public class GuidedExperimentDef
    {
        public string Id { get; set; }
        public string TitleKey { get; set; }          // Localization key
        public string DescriptionKey { get; set; }    // Localization key
        public string Category { get; set; }          // e.g. "Acid-Base", "Precipitation", "Gas Formation"
        public int DifficultyLevel { get; set; }      // 1-5
        public int EstimatedMinutes { get; set; }
        public string PrerequisiteExperimentId { get; set; } // null if none
        public List<string> RequiredReagents { get; set; }   // Reagent IDs needed
        public List<ExperimentStep> Steps { get; set; }
        public ExperimentEvaluation Evaluation { get; set; }
        public int RewardSkillPoints { get; set; }
        public string AchievementId { get; set; }     // Unlock on completion
    }

    /// <summary>
    /// A single step within a guided experiment.
    /// </summary>
    public class ExperimentStep
    {
        public int StepNumber { get; set; }
        public string InstructionKey { get; set; }       // Localization key for what to do
        public string HintKey { get; set; }              // Localization key for hint
        public StepActionType ActionType { get; set; }
        public StepValidation Validation { get; set; }
        public bool IsOptional { get; set; }             // Can be skipped
        public List<string> ExpectedObservations { get; set; } // What student should see
    }

    /// <summary>
    /// Type of action the student must perform.
    /// </summary>
    public enum StepActionType
    {
        SelectReagents,      // Choose specific reagents
        SetTemperature,      // Adjust temperature slider
        SetStirring,         // Adjust stirring
        SetGrinding,         // Adjust grinding
        SetMedium,           // Choose medium (Neutral/Acidic/Basic)
        ToggleCatalyst,      // Add catalyst
        Mix,                 // Press Mix button
        Observe,             // Just observe the result
        AnswerQuestion,      // Answer a quiz question
        AddReagent,          // Add more reagent
        Complete             // Final step
    }

    /// <summary>
    /// Validation rules for a step — what the system checks.
    /// </summary>
    public class StepValidation
    {
        public float? MinTemperature { get; set; }
        public float? MaxTemperature { get; set; }
        public float? MinStirring { get; set; }
        public float? MinGrinding { get; set; }
        public string RequiredMedium { get; set; }
        public bool? RequireCatalyst { get; set; }
        public List<string> ExpectedReagents { get; set; }
        public string ExpectedResultStatus { get; set; }  // "COMPLETE", "INCOMPLETE", any
        public string CustomValidator { get; set; }        // Future: scriptable validator key
    }

    /// <summary>
    /// Defines how the experiment result is evaluated.
    /// </summary>
    public class ExperimentEvaluation
    {
        public float PerfectScoreThreshold { get; set; } = 0.9f;
        public float PassingScoreThreshold { get; set; } = 0.5f;
        public List<EvaluationCriterion> Criteria { get; set; }
    }

    /// <summary>
    /// A single scoring criterion.
    /// </summary>
    public class EvaluationCriterion
    {
        public string NameKey { get; set; }       // Localization key
        public float Weight { get; set; }         // 0.0 - 1.0, sum should be 1.0
        public string DescriptionKey { get; set; } // What was evaluated
    }

    /// <summary>
    /// Runtime state of an in-progress experiment.
    /// </summary>
    public class ExperimentSession
    {
        public string ExperimentId { get; set; }
        public int CurrentStepIndex { get; set; }
        public List<StepResult> StepResults { get; set; }
        public float TotalScore { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsPerfect { get; set; }
        public System.DateTime StartedAt { get; set; }
        public System.DateTime? CompletedAt { get; set; }
        public Dictionary<string, object> StepData { get; set; }
    }

    /// <summary>
    /// Result of a single step execution.
    /// </summary>
    public class StepResult
    {
        public int StepNumber { get; set; }
        public bool Passed { get; set; }
        public float Score { get; set; }
        public string FeedbackKey { get; set; }   // Localization key for feedback
        public bool HintUsed { get; set; }
        public int AttemptsCount { get; set; }
    }
}
