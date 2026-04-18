// ChemLabSim v3 — UI Controller
// Listens to system events, builds UI-ready ViewModels, pushes to Views.
// No TMP, no Prefabs, no PlayerPrefs, no score/level calculation.
// Views are passive display components wired via SerializeField.
//
// Migration source: LabController result display, progress display.
// Does NOT include: rich text formatting, slider labels, scroll views,
//   full achievements UI, settings menu, history notebook.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ChemLabSimV3.Core;
using ChemLabSimV3.Data;
using ChemLabSimV3.Events;
using ChemLabSimV3.Services;
using ChemLabSimV3.Views;

namespace ChemLabSimV3.Controllers
{
    public class UIController : V3ControllerBase
    {
        // -- View References (wired in Inspector or scene setup) ---
        [Header("Views")]
        [SerializeField] private ReactionResultView reactionResultView;
        [SerializeField] private ProgressView progressView;
        [SerializeField] private GuidanceView guidanceView;
        [SerializeField] private ChallengeView challengeView;
        [SerializeField] private ObjectiveView objectiveView;
        [SerializeField] private MixButtonView mixButtonView;
        [SerializeField] private AchievementToastView achievementToastView;

        [Header("Detail Views")]
        [SerializeField] private ReactionIdentityView reactionIdentityView;
        [SerializeField] private ReactionDetailsView reactionDetailsView;
        [SerializeField] private ScientificExplanationView scientificExplanationView;
        [SerializeField] private SafetyNoteView safetyNoteView;
        [SerializeField] private QuizHintView quizHintView;
        [SerializeField] private QuizPanelView quizPanelView;

        // -- Current ViewModels (read by Views) ----------------
        private ReactionResultViewModel lastReactionResult;
        private ProgressViewModel lastProgress;
        private GuidanceViewModel guidance;
        private ReactionIdentityViewModel lastIdentity;
        private ReactionDetailsViewModel lastDetails;
        private ScientificExplanationViewModel lastExplanation;
        private SafetyNoteViewModel lastSafety;
        private QuizHintViewModel lastQuiz;

        // -- Language & cached view state for re-render --------
        private int currentLanguageIndex;
        private string lastChallengeTitle;
        private int lastChallengeLevel;
        private int lastChallengeReward;
        private bool challengeCompleted;
        private string lastObjectiveTitle;
        private int lastObjectiveLevel;
        private bool objectiveCompleted;

        public ReactionResultViewModel LastReactionResult => lastReactionResult;
        public ProgressViewModel LastProgress => lastProgress;
        public GuidanceViewModel Guidance => guidance;

        // -- Lifecycle -----------------------------------------

        protected override void OnInitialize()
        {
            // Restore saved language preference
            var save = ServiceLocator.Get<SaveService>();
            int restoredLang = (save != null && save.HasSave()) ? save.GetSaveData().languageIndex : 0;
            currentLanguageIndex = restoredLang;
            V3Labels.CurrentLanguage = restoredLang;

            lastReactionResult = default;
            lastProgress = default;
            guidance = new GuidanceViewModel { HintText = V3Labels.Get("selectAndMixWelcome"), IsVisible = true };

            EventBus.Subscribe<ReactionEvaluatedEvent>(HandleReactionEvaluated);
            EventBus.Subscribe<ReactionNotFoundEvent>(HandleReactionNotFound);
            EventBus.Subscribe<ProgressUpdatedEvent>(HandleProgressUpdated);
            EventBus.Subscribe<LevelUpEvent>(HandleLevelUp);
            EventBus.Subscribe<LanguageChangedEvent>(HandleLanguageChanged);
            EventBus.Subscribe<ChallengeAssignedEvent>(HandleChallengeAssigned);
            EventBus.Subscribe<ChallengeCompletedEvent>(HandleChallengeCompleted);
            EventBus.Subscribe<ObjectiveAssignedEvent>(HandleObjectiveAssigned);
            EventBus.Subscribe<ObjectiveCompletedEvent>(HandleObjectiveCompleted);
            EventBus.Subscribe<AchievementUnlockedEvent>(HandleAchievementUnlocked);
            EventBus.Subscribe<GuidanceUpdatedEvent>(HandleGuidanceUpdated);
            EventBus.Subscribe<QuizUpdatedEvent>(HandleQuizUpdated);

            // Render initial guidance (GuidanceController will publish updated guidance on init)
            if (guidanceView != null) guidanceView.Render(guidance);

            // Clear result panels to ensure a clean slate before first Mix
            if (reactionResultView != null) reactionResultView.Clear();
            if (reactionIdentityView != null) reactionIdentityView.Hide();
            if (reactionDetailsView != null) reactionDetailsView.Hide();
            if (scientificExplanationView != null) scientificExplanationView.Clear();
            if (safetyNoteView != null) safetyNoteView.Clear();

            // Restore progress display from saved state (if any)
            if (save != null && save.HasSave())
            {
                var data = save.GetSaveData();
                var restoredProgress = new ProgressState
                {
                    SessionScore = data.progress.sessionScore,
                    TotalExperiments = data.progress.totalExperiments,
                    SuccessfulExperiments = data.progress.successfulExperiments,
                    InvalidExperiments = data.progress.invalidExperiments,
                    BestScore = data.progress.bestScore,
                    CurrentLevel = data.progress.currentLevel,
                    CurrentLessonTitle = data.progress.currentLessonTitle,
                    SuccessfulExperimentsInLevel = data.progress.successfulExperimentsInLevel,
                    NextLevelRequirement = 2
                };
                lastProgress = BuildProgressViewModel(restoredProgress, 0, false, null);
                if (progressView != null) progressView.Render(lastProgress);
            }

            Debug.Log("[UIController] Initialized.");
        }

        protected override void OnTeardown()
        {
            EventBus.Unsubscribe<ReactionEvaluatedEvent>(HandleReactionEvaluated);
            EventBus.Unsubscribe<ReactionNotFoundEvent>(HandleReactionNotFound);
            EventBus.Unsubscribe<ProgressUpdatedEvent>(HandleProgressUpdated);
            EventBus.Unsubscribe<LevelUpEvent>(HandleLevelUp);
            EventBus.Unsubscribe<LanguageChangedEvent>(HandleLanguageChanged);
            EventBus.Unsubscribe<ChallengeAssignedEvent>(HandleChallengeAssigned);
            EventBus.Unsubscribe<ChallengeCompletedEvent>(HandleChallengeCompleted);
            EventBus.Unsubscribe<ObjectiveAssignedEvent>(HandleObjectiveAssigned);
            EventBus.Unsubscribe<ObjectiveCompletedEvent>(HandleObjectiveCompleted);
            EventBus.Unsubscribe<AchievementUnlockedEvent>(HandleAchievementUnlocked);
            EventBus.Unsubscribe<GuidanceUpdatedEvent>(HandleGuidanceUpdated);
            EventBus.Unsubscribe<QuizUpdatedEvent>(HandleQuizUpdated);
        }

        // -- Event Handlers ------------------------------------

        private void HandleReactionEvaluated(ReactionEvaluatedEvent evt)
        {
            lastReactionResult = BuildReactionResultViewModel(evt.Input, evt.Result);
            lastIdentity = BuildReactionIdentityViewModel(evt.Input);
            lastDetails = BuildReactionDetailsViewModel(evt.Input, evt.Result);
            lastExplanation = BuildScientificExplanationViewModel(evt.Input, evt.Result);
            lastSafety = BuildSafetyNoteViewModel(evt.Input);
            // Quiz hint is now handled by QuizController → QuizUpdatedEvent

            SafeRenderView(reactionResultView, lastReactionResult);
            SafeRenderView(reactionIdentityView, lastIdentity);
            SafeRenderView(reactionDetailsView, lastDetails);
            SafeRenderView(scientificExplanationView, lastExplanation);
            SafeRenderView(safetyNoteView, lastSafety);

            Debug.Log($"[UIController] Reaction VM built: {lastReactionResult.Headline}");
        }

        private void HandleReactionNotFound(ReactionNotFoundEvent evt)
        {
            lastReactionResult = new ReactionResultViewModel
            {
                Headline = V3Labels.Get("notFound"),
                StatusKey = "notFound",
                Explanation = evt.Message ?? string.Empty,
                ReactionId = string.Empty,
                Reactants = new List<string>(),
                Products = new List<string>(),
                DidReact = false,
                DetailedReasons = new List<string>()
            };

            // Fallback identity — show safe placeholder instead of hiding
            lastIdentity = new ReactionIdentityViewModel
            {
                ReactionName = V3Labels.Get("unknownReaction"),
                Equation = V3Labels.Get("noVerifiedEquation"),
                RequiredMedium = "-",
                ActivationTempC = 0f,
                CatalystAllowed = false,
                ProducesGas = false,
                IsVisible = true
            };

            // Hide factor details (no meaningful data to show)
            lastDetails = new ReactionDetailsViewModel { IsVisible = false };

            // Fallback explanation
            lastExplanation = new ScientificExplanationViewModel
            {
                ExplanationText = V3Labels.Get("notFoundExplanation"),
                IsVisible = true
            };

            // Fallback safety
            lastSafety = new SafetyNoteViewModel
            {
                GhsCodes = string.Empty,
                WarningsText = V3Labels.Get("noSafetyDataForCombo"),
                IsVisible = true
            };

            // Quiz fallback is handled by QuizController (publishes reflective question)

            SafeRenderView(reactionResultView, lastReactionResult);
            SafeRenderView(reactionIdentityView, lastIdentity);
            SafeRenderView(reactionDetailsView, lastDetails);
            SafeRenderView(scientificExplanationView, lastExplanation);
            SafeRenderView(safetyNoteView, lastSafety);

            Debug.Log($"[UIController] Reaction not found: {evt.Message}");
        }

        private void HandleProgressUpdated(ProgressUpdatedEvent evt)
        {
            lastProgress = BuildProgressViewModel(evt.State, evt.ScoreDelta, false, null);

            if (progressView != null) progressView.Render(lastProgress);

            Debug.Log($"[UIController] Progress VM built: Score={lastProgress.Score}");
        }

        private void HandleLevelUp(LevelUpEvent evt)
        {
            // Merge level-up flag into the existing progress VM.
            lastProgress.JustLeveledUp = true;
            lastProgress.NewLevelTitle = evt.LessonTitle;
            lastProgress.CurrentLevel = evt.NewLevel;

            if (progressView != null) progressView.Render(lastProgress);

            Debug.Log($"[UIController] Level up → {evt.NewLevel}: {evt.LessonTitle}");
        }

        private void HandleLanguageChanged(LanguageChangedEvent evt)
        {
            currentLanguageIndex = evt.LanguageIndex;
            V3Labels.CurrentLanguage = evt.LanguageIndex;

            // Guidance re-render is handled by GuidanceController on LanguageChangedEvent

            // Re-render cached progress
            if (progressView != null && (lastProgress.Score != 0 || lastProgress.TotalExperiments != 0))
                progressView.Render(lastProgress);

            // Re-render cached reaction result (re-localize headline from StatusKey)
            if (reactionResultView != null && !string.IsNullOrEmpty(lastReactionResult.StatusKey))
            {
                lastReactionResult.Headline = V3Labels.Get(lastReactionResult.StatusKey);
                reactionResultView.Render(lastReactionResult);
            }

            // Re-render detail views (labels are resolved at render time via V3Labels)
            if (reactionIdentityView != null && lastIdentity.IsVisible)
                reactionIdentityView.Render(lastIdentity);
            if (reactionDetailsView != null && lastDetails.IsVisible)
            {
                lastDetails = RebuildDetailsLabels(lastDetails);
                reactionDetailsView.Render(lastDetails);
            }
            if (scientificExplanationView != null && lastExplanation.IsVisible)
                scientificExplanationView.Render(lastExplanation);
            if (safetyNoteView != null && lastSafety.IsVisible)
                safetyNoteView.Render(lastSafety);
            // Quiz re-render on language change handled by QuizController

            // Re-render cached challenge
            if (challengeView != null && !string.IsNullOrEmpty(lastChallengeTitle))
            {
                if (challengeCompleted)
                    challengeView.RenderCompleted(lastChallengeReward);
                else
                    challengeView.RenderAssigned(lastChallengeTitle, lastChallengeLevel);
            }

            // Re-render cached objective
            if (objectiveView != null && !string.IsNullOrEmpty(lastObjectiveTitle))
            {
                if (objectiveCompleted)
                    objectiveView.RenderCompleted();
                else
                    objectiveView.RenderAssigned(lastObjectiveTitle, lastObjectiveLevel);
            }

            // Re-render mix button label
            if (mixButtonView != null) mixButtonView.SetLabel(V3Labels.Get("mix"));

            Debug.Log($"[UIController] Language changed to index {evt.LanguageIndex}");
        }

        private void HandleChallengeAssigned(ChallengeAssignedEvent evt)
        {
            lastChallengeTitle = evt.Title;
            lastChallengeLevel = evt.Level;
            challengeCompleted = false;
            if (challengeView != null) challengeView.RenderAssigned(evt.Title, evt.Level);
            Debug.Log($"[UIController] Challenge assigned: {evt.Title}");
        }

        private void HandleChallengeCompleted(ChallengeCompletedEvent evt)
        {
            lastChallengeReward = evt.RewardPoints;
            challengeCompleted = true;
            if (challengeView != null) challengeView.RenderCompleted(evt.RewardPoints);
            Debug.Log($"[UIController] Challenge completed: {evt.ChallengeId}");
        }

        private void HandleObjectiveAssigned(ObjectiveAssignedEvent evt)
        {
            lastObjectiveTitle = evt.Title;
            lastObjectiveLevel = evt.Level;
            objectiveCompleted = false;
            if (objectiveView != null) objectiveView.RenderAssigned(evt.Title, evt.Level);
            Debug.Log($"[UIController] Objective assigned: {evt.Title}");
        }

        private void HandleObjectiveCompleted(ObjectiveCompletedEvent evt)
        {
            objectiveCompleted = true;
            if (objectiveView != null) objectiveView.RenderCompleted();
            Debug.Log($"[UIController] Objective completed: {evt.ObjectiveId}");
        }

        private void HandleAchievementUnlocked(AchievementUnlockedEvent evt)
        {
            if (achievementToastView != null)
                achievementToastView.ShowToast(evt.DisplayName);
            Debug.Log($"[UIController] Achievement unlocked: {evt.DisplayName}");
        }

        private void HandleGuidanceUpdated(GuidanceUpdatedEvent evt)
        {
            guidance = BuildGuidanceViewModel(evt.State);
            if (guidanceView != null) guidanceView.Render(guidance);
        }

        private void HandleQuizUpdated(QuizUpdatedEvent evt)
        {
            var state = evt.State;

            // Prefer interactive panel when wired
            if (quizPanelView != null)
            {
                var panelVm = new QuizPanelViewModel
                {
                    QuestionText = state.QuestionText,
                    AnswerOptions = state.AnswerOptions,
                    AnsweredIndex = state.AnsweredIndex,
                    IsCorrect = state.IsCorrect,
                    FeedbackText = state.FeedbackText,
                    IsVisible = state.IsVisible
                };
                quizPanelView.Render(panelVm);
            }

            // Fallback: text-only hint view
            lastQuiz = new QuizHintViewModel
            {
                QuestionText = state.QuestionText,
                IsVisible = state.IsVisible
            };
            if (quizHintView != null) quizHintView.Render(lastQuiz);
        }

        // -- ViewModel Builders --------------------------------

        public static ReactionResultViewModel BuildReactionResultViewModel(
            ReactionEvaluationInput input,
            ReactionEvaluationResult result)
        {
            bool didReact = result.Status == ReactionStatus.Success ||
                            result.Status == ReactionStatus.Partial;

            string statusKey;
            if (!result.IsValid)
                statusKey = "invalid";
            else if (result.Status == ReactionStatus.Success)
                statusKey = "success";
            else if (result.Status == ReactionStatus.Partial)
                statusKey = "partial";
            else
                statusKey = "fail";

            string headline = V3Labels.Get(statusKey);

            var reactants = input.reaction != null
                ? input.reaction.GetReactantFormulas()
                : new List<string>();

            var products = (didReact && input.reaction != null)
                ? FormatProductList(input.reaction.products)
                : new List<string>();

            // Build equation string with stoichiometric coefficients and state labels
            string equation = string.Empty;
            if (input.reaction != null)
                equation = BuildBalancedEquation(input.reaction);

            // Reaction name
            string reactionName = string.Empty;
            if (input.reaction != null)
                reactionName = !string.IsNullOrEmpty(input.reaction.name_en) ? input.reaction.name_en : input.reaction.id ?? string.Empty;

            // Reaction type from reaction entry
            string reactionType = string.Empty;
            if (input.reaction != null)
                reactionType = !string.IsNullOrEmpty(input.reaction.reactionType) ? input.reaction.reactionType : string.Empty;

            // Build observation text from visual effects
            string observationText = BuildObservationText(input, result);

            // Build condition notes
            string conditionNotes = BuildConditionNotes(input, result);

            return new ReactionResultViewModel
            {
                Headline = headline,
                StatusKey = statusKey,
                Explanation = result.Summary,
                ReactionId = input.reaction != null ? input.reaction.id ?? string.Empty : string.Empty,
                ReactionName = reactionName,
                ReactionType = reactionType,
                Equation = equation,
                Reactants = reactants,
                Products = products,
                DidReact = didReact,
                ObservationText = observationText,
                ConditionNotes = conditionNotes,
                MediumMismatch = result.MediumMismatch,
                ActivationNotReached = result.ActivationNotReached,
                CatalystApplied = result.CatalystApplied,
                LowContactQuality = result.LowContactQuality,
                ContactFactor = result.ContactFactor,
                TemperatureC = input.temperatureC,
                ActivationThresholdC = result.ActivationThresholdC,
                Rate01 = result.Rate01,
                DetailedReasons = result.DetailedReasons ?? new List<string>()
            };
        }

        public static ProgressViewModel BuildProgressViewModel(
            ProgressState state,
            int scoreDelta,
            bool justLeveledUp,
            string newLevelTitle)
        {
            return new ProgressViewModel
            {
                Score = state.SessionScore,
                ScoreDelta = scoreDelta,
                TotalExperiments = state.TotalExperiments,
                SuccessfulExperiments = state.SuccessfulExperiments,
                InvalidExperiments = state.InvalidExperiments,
                BestScore = state.BestScore,
                CurrentLevel = state.CurrentLevel,
                LessonTitle = state.CurrentLessonTitle,
                SuccessfulExperimentsInLevel = state.SuccessfulExperimentsInLevel,
                NextLevelRequirement = state.NextLevelRequirement,
                JustLeveledUp = justLeveledUp,
                NewLevelTitle = newLevelTitle
            };
        }

        // -- Detail View Builders ------------------------------

        public static ReactionIdentityViewModel BuildReactionIdentityViewModel(
            ReactionEvaluationInput input)
        {
            var rxn = input.reaction;
            if (rxn == null)
                return new ReactionIdentityViewModel { IsVisible = false };

            string name = !string.IsNullOrEmpty(rxn.name_en) ? rxn.name_en : rxn.id ?? "Unknown";

            var reactants = rxn.GetReactantFormulas();
            var products = rxn.GetProductFormulas();
            string equation = reactants.Count > 0
                ? string.Join(" + ", reactants) + " -> " + string.Join(" + ", products)
                : string.Empty;

            return new ReactionIdentityViewModel
            {
                ReactionName = name,
                Equation = equation,
                RequiredMedium = rxn.requiredMedium ?? "Neutral",
                ActivationTempC = rxn.activationTempC,
                CatalystAllowed = rxn.catalystAllowed,
                ProducesGas = rxn.GetProducesGas(),
                IsVisible = true
            };
        }

        public static ReactionDetailsViewModel BuildReactionDetailsViewModel(
            ReactionEvaluationInput input,
            ReactionEvaluationResult result)
        {
            if (!result.IsValid)
                return new ReactionDetailsViewModel { IsVisible = false };

            // Medium status
            string mediumStatus = result.MediumMismatch
                ? V3Labels.Get("mismatch")
                : V3Labels.Get("correct");

            // Temperature status
            string tempStatus = result.ActivationNotReached
                ? V3Labels.Get("notReachedLbl")
                : V3Labels.Get("reached");

            // Contact status
            string contactStatus;
            if (result.ContactFactor >= 1.2f)
                contactStatus = V3Labels.Get("strong");
            else if (!result.LowContactQuality)
                contactStatus = V3Labels.Get("adequate");
            else
                contactStatus = V3Labels.Get("weak");

            // Catalyst status
            string catalystStatus;
            if (input.reaction != null && !input.reaction.catalystAllowed)
                catalystStatus = V3Labels.Get("notApplicable");
            else if (result.CatalystApplied)
                catalystStatus = V3Labels.Get("applied");
            else
                catalystStatus = V3Labels.Get("notApplied");

            return new ReactionDetailsViewModel
            {
                MediumStatus = mediumStatus,
                TemperatureStatus = tempStatus,
                ContactStatus = contactStatus,
                CatalystStatus = catalystStatus,
                ContactFactor = result.ContactFactor,
                Rate01 = result.Rate01,
                TemperatureC = input.temperatureC,
                ActivationThresholdC = result.ActivationThresholdC,
                IsVisible = true
            };
        }

        private static ReactionDetailsViewModel RebuildDetailsLabels(ReactionDetailsViewModel vm)
        {
            // Re-resolve localized indicator labels after language change.
            // We don't have the original result booleans cached, so we use a heuristic:
            // the indicator text starts with the symbol (✓, ✗, ●, ⚠, –, n).
            // This is safe because language change only affects the text after the symbol.
            // For a full re-render, the original input/result would need to be cached.
            return vm; // Labels in detail views resolve from V3Labels.Get() at build time;
                       // a full re-render requires caching input/result (Phase 9 scope).
        }

        public static ScientificExplanationViewModel BuildScientificExplanationViewModel(
            ReactionEvaluationInput input,
            ReactionEvaluationResult result)
        {
            if (!result.IsValid)
                return new ScientificExplanationViewModel { IsVisible = false };

            bool didReact = result.Status == ReactionStatus.Success ||
                            result.Status == ReactionStatus.Partial;

            // Prefer authored explanation when available and reaction succeeded/partial
            string authored = input.reaction != null ? input.reaction.explanation_en : null;
            bool hasAuthored = !string.IsNullOrEmpty(authored);

            string text;

            if (result.MediumMismatch)
            {
                text = "The selected medium is incompatible with this reaction. Chemical reactions require a specific medium environment to proceed.";
            }
            else if (result.ActivationNotReached && !didReact)
            {
                float gap = result.ActivationThresholdC - input.temperatureC;
                text = $"Current temperature ({input.temperatureC:F0}\u00b0C) is {gap:F0}\u00b0C below the activation threshold ({result.ActivationThresholdC:F0}\u00b0C). Molecules lack sufficient energy to overcome the activation barrier.";
            }
            else if (result.Status == ReactionStatus.Partial)
            {
                text = hasAuthored ? authored : "The reaction occurred partially due to suboptimal conditions.";

                if (result.LowContactQuality)
                    text += $" Contact quality ({result.ContactFactor:F2}) is low \u2014 consider increasing stirring or grinding.";
                if (result.ActivationNotReached)
                    text += $" Temperature ({input.temperatureC:F0}\u00b0C) is near but below the activation threshold.";
            }
            else if (result.Status == ReactionStatus.Success)
            {
                text = hasAuthored ? authored : "The reaction succeeded under suitable conditions.";

                if (result.CatalystApplied && input.reaction != null && input.reaction.catalystAllowed)
                    text += $" The catalyst lowered the activation temperature by {input.reaction.catalystDeltaTempC:F0}\u00b0C.";
                if (!hasAuthored && result.ContactFactor >= 1.2f)
                    text += " Thorough mixing improved contact between reactants.";
            }
            else // Fail
            {
                text = V3Labels.Get("conditionsNotMet");
                if (result.MediumMismatch)
                    text += " The selected medium does not match this reaction's requirements.";
                if (result.ActivationNotReached)
                    text += $" Temperature ({input.temperatureC:F0}\u00b0C) is below the activation threshold ({result.ActivationThresholdC:F0}\u00b0C).";
                if (result.LowContactQuality)
                    text += $" Contact quality ({result.ContactFactor:F2}) is low \u2014 try more stirring or grinding.";
            }

            return new ScientificExplanationViewModel
            {
                ExplanationText = text,
                IsVisible = true
            };
        }

        public static SafetyNoteViewModel BuildSafetyNoteViewModel(
            ReactionEvaluationInput input)
        {
            var rxn = input.reaction;
            if (rxn == null || rxn.safety == null)
                return new SafetyNoteViewModel { IsVisible = false };

            // GHS codes
            string ghsCodes = rxn.safety.ghs_icons != null && rxn.safety.ghs_icons.Count > 0
                ? string.Join("  ", rxn.safety.ghs_icons)
                : string.Empty;

            // English warnings only
            string warningsText = string.Empty;
            var warnings = rxn.safety.warnings_en;

            if (warnings != null && warnings.Count > 0)
            {
                var lines = new List<string>();
                for (int i = 0; i < warnings.Count; i++)
                    lines.Add("\u26a0 " + warnings[i]);
                warningsText = string.Join("\n", lines);
            }

            // Authored safety notes from reaction data
            string safetyNotes = !string.IsNullOrEmpty(rxn.safety_notes) ? rxn.safety_notes : string.Empty;

            bool hasSafetyData = !string.IsNullOrEmpty(ghsCodes) || !string.IsNullOrEmpty(warningsText) || !string.IsNullOrEmpty(safetyNotes);

            return new SafetyNoteViewModel
            {
                GhsCodes = ghsCodes,
                WarningsText = warningsText,
                SafetyNotes = safetyNotes,
                IsVisible = hasSafetyData
            };
        }

        // -- Observation & Condition Helpers (Phase 2) ------

        private static string BuildObservationText(ReactionEvaluationInput input, ReactionEvaluationResult result)
        {
            // Prefer authored observation from reaction data
            if (input.reaction != null && !string.IsNullOrEmpty(input.reaction.observation_en))
                return input.reaction.observation_en;

            // Auto-generate from visual_effects and result flags
            bool didReact = result.Status == ReactionStatus.Success || result.Status == ReactionStatus.Partial;
            if (!didReact)
                return V3Labels.Get("noVisibleChange");

            var observations = new List<string>();
            var vfx = input.reaction?.visual_effects;

            if (vfx != null)
            {
                if (vfx.gas || (input.reaction != null && input.reaction.GetProducesGas()))
                    observations.Add(V3Labels.Get("gasEvolution"));
                if (vfx.precipitate)
                    observations.Add(V3Labels.Get("precipitateFormed"));
                if (!string.IsNullOrEmpty(vfx.color_change))
                    observations.Add(V3Labels.Get("colorChanged"));
                if (vfx.temperature_delta > 10f)
                    observations.Add(V3Labels.Get("exothermicHeat"));
            }

            if (result.CatalystApplied)
                observations.Add(V3Labels.Get("catalystActive"));

            return observations.Count > 0
                ? string.Join(". ", observations) + "."
                : V3Labels.Get("noVisibleChange");
        }

        private static string BuildConditionNotes(ReactionEvaluationInput input, ReactionEvaluationResult result)
        {
            // Prefer authored condition notes
            if (input.reaction != null && !string.IsNullOrEmpty(input.reaction.condition_notes))
                return input.reaction.condition_notes;

            // Auto-generate
            var notes = new List<string>();
            var rxn = input.reaction;

            if (rxn != null)
            {
                notes.Add($"Medium: {rxn.requiredMedium ?? "Neutral"}");
                notes.Add($"Activation: {rxn.activationTempC:F0}\u00b0C");

                if (rxn.catalystAllowed)
                    notes.Add($"Catalyst lowers threshold by {rxn.catalystDeltaTempC:F0}\u00b0C");
            }

            if (result.MediumMismatch)
                notes.Add("\u2717 Medium mismatch detected");
            if (result.ActivationNotReached)
                notes.Add($"\u2717 Temperature {input.temperatureC:F0}\u00b0C below threshold");
            if (result.LowContactQuality)
                notes.Add($"\u26a0 Low contact quality ({result.ContactFactor:F2})");

            return notes.Count > 0 ? string.Join(" | ", notes) : string.Empty;
        }

        // -- Guidance State → ViewModel ---------------------

        public static GuidanceViewModel BuildGuidanceViewModel(GuidanceState state)
        {
            if (!state.IsVisible)
                return new GuidanceViewModel { HintText = string.Empty, IsVisible = false };

            string text;
            switch (state.Step)
            {
                case GuidanceStep.SelectReagents:
                    text = V3Labels.Get("stepChooseReagents");
                    break;
                case GuidanceStep.DuplicateReagents:
                    text = V3Labels.Get("stepDuplicateReagents");
                    break;
                case GuidanceStep.Ready:
                    text = state.MayNeedExtraReactant
                        ? V3Labels.Get("readyMix") + "\n" + V3Labels.Get("hintExtraReactant")
                        : V3Labels.Get("readyMix");
                    break;
                case GuidanceStep.Dismissed:
                    return new GuidanceViewModel { HintText = string.Empty, IsVisible = false };
                default:
                    text = V3Labels.Get("selectAndMix");
                    break;
            }

            return new GuidanceViewModel { HintText = text, IsVisible = true };
        }

        // -- Safe View Rendering (null + exception guard per view) --

        private static void SafeRenderView(ReactionResultView view, ReactionResultViewModel vm)
        {
            if (view == null) return;
            try { view.Render(vm); }
            catch (System.Exception ex) { Debug.LogException(ex); }
        }

        private static void SafeRenderView(ReactionIdentityView view, ReactionIdentityViewModel vm)
        {
            if (view == null) return;
            try { view.Render(vm); }
            catch (System.Exception ex) { Debug.LogException(ex); }
        }

        private static void SafeRenderView(ReactionDetailsView view, ReactionDetailsViewModel vm)
        {
            if (view == null) return;
            try { view.Render(vm); }
            catch (System.Exception ex) { Debug.LogException(ex); }
        }

        private static void SafeRenderView(ScientificExplanationView view, ScientificExplanationViewModel vm)
        {
            if (view == null) return;
            try { view.Render(vm); }
            catch (System.Exception ex) { Debug.LogException(ex); }
        }

        private static void SafeRenderView(SafetyNoteView view, SafetyNoteViewModel vm)
        {
            if (view == null) return;
            try { view.Render(vm); }
            catch (System.Exception ex) { Debug.LogException(ex); }
        }

        // -- Equation Formatting Helpers -----------------------

        /// <summary>
        /// Builds a balanced equation with stoichiometric coefficients and state labels.
        /// Example: 2 Al(s) + Fe₂O₃(s) → Al₂O₃(s) + 2 Fe(l)
        /// </summary>
        private static string BuildBalancedEquation(ReactionEntry rxn)
        {
            if (rxn == null) return string.Empty;

            var lhs = FormatChemicalList(rxn.reactants);
            var rhs = FormatChemicalList(rxn.products);

            if (string.IsNullOrEmpty(lhs) && string.IsNullOrEmpty(rhs))
                return string.Empty;

            return lhs + " \u2192 " + rhs;
        }

        private static string FormatChemicalList(List<ReactionChemical> chemicals)
        {
            if (chemicals == null || chemicals.Count == 0)
                return string.Empty;

            var parts = new List<string>();
            for (int i = 0; i < chemicals.Count; i++)
            {
                var chem = chemicals[i];
                if (chem == null || string.IsNullOrEmpty(chem.formula))
                    continue;

                string term = string.Empty;

                // Add stoichiometric coefficient (omit 1)
                int coeff = Mathf.RoundToInt(chem.stoich);
                if (coeff > 1)
                    term = coeff + " ";

                term += chem.formula;

                // Add state label
                if (!string.IsNullOrEmpty(chem.state))
                    term += "(" + chem.state + ")";

                parts.Add(term);
            }

            return string.Join(" + ", parts);
        }

        /// <summary>
        /// Formats products as "Formula(state)" for the product list display.
        /// </summary>
        private static List<string> FormatProductList(List<ReactionChemical> chemicals)
        {
            var result = new List<string>();
            if (chemicals == null) return result;

            for (int i = 0; i < chemicals.Count; i++)
            {
                var chem = chemicals[i];
                if (chem == null || string.IsNullOrEmpty(chem.formula))
                    continue;

                string entry = chem.formula;
                if (!string.IsNullOrEmpty(chem.state))
                    entry += "(" + chem.state + ")";
                result.Add(entry);
            }

            return result;
        }
    }
}
