// ChemLabSim v3 — Quiz Controller
// Owns contextual question generation + answer validation after reactions.
// Publishes QuizUpdatedEvent with QuizState → UIController renders quiz UI.
// No TMP, no scene refs — uses V3Labels keys for localization.
//
// Flow: ReactionEvaluated → generate question + 3 shuffled answers →
//       QuizUpdatedEvent → view shows buttons → QuizOptionSelectedEvent →
//       validate answer → QuizAnsweredEvent + feedback → QuizUpdatedEvent.

using System.Collections.Generic;
using UnityEngine;
using ChemLabSimV3.Data;
using ChemLabSimV3.Events;

namespace ChemLabSimV3.Controllers
{
    public class QuizController : V3ControllerBase
    {
        // -- Statistics ----------------------------------------
        private int totalQuestions;
        private int correctAnswers;

        /// <summary>Correct index for the current question (private — view never sees this).</summary>
        private int currentCorrectIndex;

        public int TotalQuestions => totalQuestions;
        public int CorrectAnswers => correctAnswers;
        public QuizState CurrentState { get; private set; }

        // -- Lifecycle -----------------------------------------

        protected override void OnInitialize()
        {
            EventBus.Subscribe<ReactionEvaluatedEvent>(OnReactionEvaluated);
            EventBus.Subscribe<ReactionNotFoundEvent>(OnReactionNotFound);
            EventBus.Subscribe<QuizOptionSelectedEvent>(OnOptionSelected);
            EventBus.Subscribe<QuizAnsweredEvent>(OnQuizAnswered);
            EventBus.Subscribe<LanguageChangedEvent>(OnLanguageChanged);
            Debug.Log("[QuizController] Initialized.");
        }

        protected override void OnTeardown()
        {
            EventBus.Unsubscribe<ReactionEvaluatedEvent>(OnReactionEvaluated);
            EventBus.Unsubscribe<ReactionNotFoundEvent>(OnReactionNotFound);
            EventBus.Unsubscribe<QuizOptionSelectedEvent>(OnOptionSelected);
            EventBus.Unsubscribe<QuizAnsweredEvent>(OnQuizAnswered);
            EventBus.Unsubscribe<LanguageChangedEvent>(OnLanguageChanged);
        }

        // -- Event Handlers ------------------------------------

        private void OnReactionEvaluated(ReactionEvaluatedEvent evt)
        {
            if (!evt.Result.IsValid)
            {
                PublishHidden();
                return;
            }

            string key = SelectQuestionKey(evt.Result);
            totalQuestions++;

            string reactionId = evt.Input.reaction != null
                ? evt.Input.reaction.id ?? string.Empty
                : string.Empty;

            // Also publish the legacy QuizRequestedEvent for any other listeners
            EventBus.Publish(new QuizRequestedEvent { ReactionId = reactionId });

            var (options, correctIdx) = GenerateAnswers(key);
            currentCorrectIndex = correctIdx;

            PublishState(new QuizState
            {
                QuestionKey = key,
                QuestionText = V3Labels.Get(key),
                IsVisible = true,
                TotalAsked = totalQuestions,
                TotalCorrect = correctAnswers,
                AnswerOptions = options,
                AnsweredIndex = -1,
                IsCorrect = false,
                FeedbackText = string.Empty
            });

            Debug.Log($"[QuizController] Quiz '{key}' for '{reactionId}' (total={totalQuestions}, correct@{currentCorrectIndex}).");
        }

        private void OnReactionNotFound(ReactionNotFoundEvent evt)
        {
            // Show a reflective question instead of hiding the quiz entirely.
            // No answer options → QuizPanelView hides buttons, QuizHintView shows text.
            currentCorrectIndex = -1;

            PublishState(new QuizState
            {
                QuestionKey = "quizNotFoundQuestion",
                QuestionText = V3Labels.Get("quizNotFoundQuestion"),
                IsVisible = true,
                TotalAsked = totalQuestions,
                TotalCorrect = correctAnswers,
                AnswerOptions = null,
                AnsweredIndex = -1,
                IsCorrect = false,
                FeedbackText = string.Empty
            });
        }

        private void OnOptionSelected(QuizOptionSelectedEvent evt)
        {
            // Ignore if already answered, no question active, or reflective-only (no answers)
            if (!CurrentState.IsVisible || CurrentState.AnsweredIndex >= 0) return;
            if (currentCorrectIndex < 0 || CurrentState.AnswerOptions == null) return;

            bool correct = evt.SelectedIndex == currentCorrectIndex;
            if (correct) correctAnswers++;

            // Publish the canonical QuizAnsweredEvent (consumed by ProgressController, etc.)
            EventBus.Publish(new QuizAnsweredEvent
            {
                QuestionId = CurrentState.QuestionKey,
                Correct = correct
            });

            string feedback = correct
                ? V3Labels.Get("quizFeedbackCorrect")
                : V3Labels.Get("quizFeedbackWrong");

            PublishState(new QuizState
            {
                QuestionKey = CurrentState.QuestionKey,
                QuestionText = CurrentState.QuestionText,
                IsVisible = true,
                TotalAsked = totalQuestions,
                TotalCorrect = correctAnswers,
                AnswerOptions = CurrentState.AnswerOptions,
                AnsweredIndex = evt.SelectedIndex,
                IsCorrect = correct,
                FeedbackText = feedback
            });

            Debug.Log($"[QuizController] Answer index={evt.SelectedIndex} correct={correct} ({correctAnswers}/{totalQuestions}).");
        }

        private void OnQuizAnswered(QuizAnsweredEvent evt)
        {
            // Stats already updated in OnOptionSelected.
            // This handler remains for external publishers (future).
        }

        private void OnLanguageChanged(LanguageChangedEvent evt)
        {
            if (!CurrentState.IsVisible) return;

            var refreshed = CurrentState;
            refreshed.QuestionText = V3Labels.Get(refreshed.QuestionKey);

            // Re-resolve answer options in new language
            if (refreshed.AnswerOptions != null && refreshed.AnswerOptions.Count > 0)
            {
                var (options, _) = GenerateAnswersPreserveOrder(refreshed.QuestionKey, currentCorrectIndex);
                refreshed.AnswerOptions = options;
            }

            // Re-resolve feedback if answered
            if (refreshed.AnsweredIndex >= 0)
            {
                refreshed.FeedbackText = refreshed.IsCorrect
                    ? V3Labels.Get("quizFeedbackCorrect")
                    : V3Labels.Get("quizFeedbackWrong");
            }

            PublishState(refreshed);
        }

        // -- Question Logic (from v2 BuildQuizQuestion decision tree) --

        private static string SelectQuestionKey(ReactionEvaluationResult result)
        {
            bool didReact = result.Status == ReactionStatus.Success ||
                            result.Status == ReactionStatus.Partial;

            if (result.MediumMismatch) return "quizMediumMismatch";
            if (result.ActivationNotReached && !didReact) return "quizActivationNotReached";
            if (result.CatalystApplied && didReact) return "quizCatalystRole";
            if (result.LowContactQuality) return "quizLowContact";
            if (result.Status == ReactionStatus.Partial) return "quizPartialReaction";
            if (didReact) return "quizSuccessFactors";
            return "quizHelpConditions";
        }

        // -- Answer Generation ---------------------------------

        /// <summary>Build 3 shuffled answer options. Returns (options, correctIndex).</summary>
        private static (List<string> options, int correctIndex) GenerateAnswers(string questionKey)
        {
            string correct = V3Labels.Get(questionKey + "_correct");
            string d1 = V3Labels.Get(questionKey + "_d1");
            string d2 = V3Labels.Get(questionKey + "_d2");

            var list = new List<string> { correct, d1, d2 };

            // Fisher-Yates shuffle
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }

            int idx = list.IndexOf(correct);
            return (list, idx);
        }

        /// <summary>Re-resolve answer texts for a language change, preserving existing order.</summary>
        private static (List<string> options, int correctIndex) GenerateAnswersPreserveOrder(
            string questionKey, int preservedCorrectIndex)
        {
            if (preservedCorrectIndex < 0 || preservedCorrectIndex > 2)
                return GenerateAnswers(questionKey);

            string correct = V3Labels.Get(questionKey + "_correct");
            string d1 = V3Labels.Get(questionKey + "_d1");
            string d2 = V3Labels.Get(questionKey + "_d2");

            var distractors = new List<string> { d1, d2 };
            var list = new List<string>(3);
            int dIdx = 0;
            for (int i = 0; i < 3; i++)
            {
                if (i == preservedCorrectIndex)
                    list.Add(correct);
                else
                    list.Add(distractors[dIdx++]);
            }

            return (list, preservedCorrectIndex);
        }

        // -- Publish Helpers -----------------------------------

        private void PublishHidden()
        {
            PublishState(new QuizState
            {
                IsVisible = false,
                TotalAsked = totalQuestions,
                TotalCorrect = correctAnswers,
                AnsweredIndex = -1
            });
        }

        private void PublishState(QuizState state)
        {
            CurrentState = state;
            EventBus.Publish(new QuizUpdatedEvent { State = state });
        }
    }
}
