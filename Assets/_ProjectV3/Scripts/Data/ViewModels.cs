// ChemLabSim v3 — UI ViewModels
// Pure data carriers for UI display. No TMP, no formatting, no logic.
// Built by UIController from incoming events, consumed by View components.

using System.Collections.Generic;

namespace ChemLabSimV3.Data
{
    // -- Reaction Result -----------------------------------

    public struct ReactionResultViewModel
    {
        /// <summary>Short outcome label: "Reaction Successful", "No Reaction", etc.</summary>
        public string Headline;

        /// <summary>Internal status key for color mapping: success/partial/fail/invalid/notFound.</summary>
        public string StatusKey;

        /// <summary>Human-readable summary from the evaluator.</summary>
        public string Explanation;

        /// <summary>Reaction name/id if matched, empty otherwise.</summary>
        public string ReactionId;

        /// <summary>English reaction name for display.</summary>
        public string ReactionName;

        /// <summary>Reaction family: Neutralization, Redox, Precipitation, etc.</summary>
        public string ReactionType;

        /// <summary>Balanced equation string: "HCl + NaOH → NaCl + H₂O".</summary>
        public string Equation;

        /// <summary>Sorted list of reactant formulas used.</summary>
        public List<string> Reactants;

        /// <summary>Product formulas if the reaction succeeded/partial.</summary>
        public List<string> Products;

        /// <summary>True if the reaction produced a result (Success or Partial).</summary>
        public bool DidReact;

        /// <summary>Observable lab evidence: "Gas bubbles evolved", "White precipitate formed".</summary>
        public string ObservationText;

        /// <summary>Condition notes: what was required and what was met/missing.</summary>
        public string ConditionNotes;

        /// <summary>Condition flags for optional UI feedback icons/hints.</summary>
        public bool MediumMismatch;
        public bool ActivationNotReached;
        public bool CatalystApplied;
        public bool LowContactQuality;

        /// <summary>Numeric details for optional condition bars.</summary>
        public float ContactFactor;
        public float TemperatureC;
        public float ActivationThresholdC;
        public float Rate01;

        /// <summary>Detailed reasons from the evaluator.</summary>
        public List<string> DetailedReasons;
    }

    // -- Progress ------------------------------------------

    public struct ProgressViewModel
    {
        public int Score;
        public int ScoreDelta;
        public int TotalExperiments;
        public int SuccessfulExperiments;
        public int InvalidExperiments;
        public int BestScore;
        public int CurrentLevel;
        public string LessonTitle;
        public int SuccessfulExperimentsInLevel;
        public int NextLevelRequirement;

        /// <summary>True when this update was triggered by a level-up.</summary>
        public bool JustLeveledUp;
        public string NewLevelTitle;
    }

    // -- Guidance ------------------------------------------

    /// <summary>Semantic guidance step — controllers produce this, views consume it.</summary>
    public enum GuidanceStep
    {
        SelectReagents,
        DuplicateReagents,
        Ready,
        Dismissed
    }

    /// <summary>Structured guidance state produced by GuidanceController.</summary>
    public struct GuidanceState
    {
        public GuidanceStep Step;
        public List<string> SelectedReagents;
        public int MediumIndex;
        public float Temperature;
        public float Stirring;
        public float Grinding;
        public bool HasCatalyst;
        public bool MayNeedExtraReactant;
        public bool IsVisible;
    }

    public struct GuidanceViewModel
    {
        /// <summary>Short context-sensitive hint for the student.</summary>
        public string HintText;

        /// <summary>True if guidance should be visible.</summary>
        public bool IsVisible;
    }

    // -- Quiz State ----------------------------------------

    /// <summary>Structured quiz state produced by QuizController.</summary>
    public struct QuizState
    {
        public string QuestionText;
        public string QuestionKey;
        public bool IsVisible;
        public int TotalAsked;
        public int TotalCorrect;

        /// <summary>Shuffled answer options (3 strings). Null when no answers available.</summary>
        public List<string> AnswerOptions;

        /// <summary>Index the student selected (-1 = not yet answered).</summary>
        public int AnsweredIndex;

        /// <summary>True if the selected answer was correct (valid only when AnsweredIndex >= 0).</summary>
        public bool IsCorrect;

        /// <summary>Localized feedback shown after answering.</summary>
        public string FeedbackText;
    }

    // -- FX State ------------------------------------------

    /// <summary>Describes which visual effects should play after a reaction.</summary>
    public struct FxState
    {
        public bool PlaySuccess;
        public bool PlayFail;
        public bool PlayGas;
        public bool PlayCatalyst;
        public bool PlayHeat;
        public bool PlayPrecipitate;
        public bool PlayColorChange;
        public bool PlayGlow;
        public bool PlaySparks;
        public bool PlaySmoke;
        public bool PlayFoam;
        public bool PlayFrost;
        public bool StopAll;

        /// <summary>Hex color string for color-change effect (e.g. "#66CCFF").</summary>
        public string ColorChangeHex;

        /// <summary>Temperature delta for heat visual intensity.</summary>
        public float TemperatureDelta;
    }

    // -- Reaction Identity ---------------------------------

    public struct ReactionIdentityViewModel
    {
        public string ReactionName;
        public string Equation;
        public string RequiredMedium;
        public float ActivationTempC;
        public bool CatalystAllowed;
        public bool ProducesGas;
        public bool IsVisible;
    }

    // -- Reaction Details (factor indicators) --------------

    public struct ReactionDetailsViewModel
    {
        public string MediumStatus;
        public string TemperatureStatus;
        public string ContactStatus;
        public string CatalystStatus;
        public float ContactFactor;
        public float Rate01;
        public float TemperatureC;
        public float ActivationThresholdC;
        public bool IsVisible;
    }

    // -- Scientific Explanation -----------------------------

    public struct ScientificExplanationViewModel
    {
        public string ExplanationText;
        public bool IsVisible;
    }

    // -- Safety Note ---------------------------------------

    public struct SafetyNoteViewModel
    {
        public string GhsCodes;
        public string WarningsText;
        public string SafetyNotes;
        public bool IsVisible;
    }

    // -- Quiz Hint -----------------------------------------

    public struct QuizHintViewModel
    {
        public string QuestionText;
        public bool IsVisible;
    }

    // -- Interactive Quiz Panel -----------------------------

    /// <summary>Full interactive quiz VM consumed by QuizPanelView.</summary>
    public struct QuizPanelViewModel
    {
        public string QuestionText;
        public List<string> AnswerOptions;
        /// <summary>-1 = unanswered; 0–2 = index selected by student.</summary>
        public int AnsweredIndex;
        public bool IsCorrect;
        public string FeedbackText;
        public bool IsVisible;
    }
}
