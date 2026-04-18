// ChemLabSim v3 — Progress Controller
// Gameplay/progression core only. No UI, no PlayerPrefs, no achievements/challenges.
// Listens to ReactionEvaluatedEvent, updates internal ProgressState,
// publishes ProgressUpdatedEvent + LevelUpEvent.
//
// Migration source: LabController session fields, CalculateScoreDelta(),
//   UpdateSessionProgress(), UpdateLevelProgress().
// Reuses v2: ReactionEvaluationResult, ReactionStatus (untouched).

using UnityEngine;
using ChemLabSimV3.Core;
using ChemLabSimV3.Data;
using ChemLabSimV3.Events;
using ChemLabSimV3.Services;

namespace ChemLabSimV3.Controllers
{
    public class ProgressController : V3ControllerBase
    {
        // -- Internal State ------------------------------------
        private ProgressState state;

        private static readonly string[] LessonTitles =
        {
            "Basic Reactions",
            "Medium and Temperature",
            "Catalyst and Contact",
            "Advanced Reaction Conditions"
        };

        // -- Read-only accessor --------------------------------
        public ProgressState CurrentState => state;

        // -- Lifecycle -----------------------------------------

        protected override void OnInitialize()
        {
            var save = ServiceLocator.Get<SaveService>();
            state = (save != null && save.HasSave()) ? RestoreFromSave(save.GetSaveData()) : BuildDefaultProgressState();
            EventBus.Subscribe<ReactionEvaluatedEvent>(HandleReactionEvaluated);
            Debug.Log($"[ProgressController] Initialized. Level={state.CurrentLevel}");
        }

        protected override void OnTeardown()
        {
            EventBus.Unsubscribe<ReactionEvaluatedEvent>(HandleReactionEvaluated);
        }

        // -- Event Handler -------------------------------------

        private void HandleReactionEvaluated(ReactionEvaluatedEvent evt)
        {
            int scoreDelta = UpdateScore(evt.Result);

            state.TotalExperiments++;

            if (!evt.Result.IsValid || evt.Result.MediumMismatch ||
                (evt.Result.ActivationNotReached && !DidReact(evt.Result)))
            {
                state.InvalidExperiments++;
            }

            if (DidReact(evt.Result))
                state.SuccessfulExperiments++;

            if (state.SessionScore > state.BestScore)
                state.BestScore = state.SessionScore;

            string levelUpTitle = UpdateLevelProgress(evt.Result);

            EventBus.Publish(new ProgressUpdatedEvent
            {
                State = state,
                ScoreDelta = scoreDelta
            });

            if (!string.IsNullOrEmpty(levelUpTitle))
            {
                EventBus.Publish(new LevelUpEvent
                {
                    NewLevel = state.CurrentLevel,
                    LessonTitle = levelUpTitle
                });
            }
        }

        // -- Score ---------------------------------------------

        /// <summary>
        /// Calculates score delta from the evaluation result and applies it to state.
        /// Returns the delta for event publishing.
        /// </summary>
        private int UpdateScore(ReactionEvaluationResult r)
        {
            if (!r.IsValid)
                return 0;

            int delta = 0;

            if (DidReact(r))
                delta += 10;

            if (!r.MediumMismatch)
                delta += 5;

            if (!r.ActivationNotReached)
                delta += 5;

            if (r.CatalystApplied)
                delta += 5;

            if (r.ContactFactor >= 1.0f)
                delta += 5;

            state.SessionScore += delta;
            return delta;
        }

        // -- Level Progression ---------------------------------

        /// <summary>
        /// Advances level when enough successful experiments are reached.
        /// Returns lesson title on level-up, null otherwise.
        /// </summary>
        private string UpdateLevelProgress(ReactionEvaluationResult eval)
        {
            if (!DidReact(eval))
                return null;

            state.SuccessfulExperimentsInLevel++;

            if (state.SuccessfulExperimentsInLevel >= state.NextLevelRequirement &&
                state.CurrentLevel < LessonTitles.Length)
            {
                state.CurrentLevel++;
                state.SuccessfulExperimentsInLevel = 0;
                state.CurrentLessonTitle = GetLessonTitleForLevel(state.CurrentLevel);
                return state.CurrentLessonTitle;
            }

            return null;
        }

        // -- Static Helpers ------------------------------------

        private static ProgressState RestoreFromSave(SaveData data)
        {
            var p = data.progress;
            return new ProgressState
            {
                SessionScore = p.sessionScore,
                TotalExperiments = p.totalExperiments,
                SuccessfulExperiments = p.successfulExperiments,
                InvalidExperiments = p.invalidExperiments,
                BestScore = p.bestScore,
                SuccessfulExperimentsInLevel = p.successfulExperimentsInLevel,
                CurrentLevel = Mathf.Clamp(p.currentLevel, 1, 4),
                CurrentLessonTitle = !string.IsNullOrEmpty(p.currentLessonTitle) ? p.currentLessonTitle : GetLessonTitleForLevel(p.currentLevel),
                NextLevelRequirement = 2
            };
        }

        public static ProgressState BuildDefaultProgressState()
        {
            return new ProgressState
            {
                SessionScore = 0,
                TotalExperiments = 0,
                SuccessfulExperiments = 0,
                InvalidExperiments = 0,
                BestScore = 0,
                SuccessfulExperimentsInLevel = 0,
                CurrentLevel = 1,
                CurrentLessonTitle = GetLessonTitleForLevel(1),
                NextLevelRequirement = 2
            };
        }

        public static string GetLessonTitleForLevel(int level)
        {
            int index = Mathf.Clamp(level - 1, 0, LessonTitles.Length - 1);
            return LessonTitles[index];
        }

        private static bool DidReact(ReactionEvaluationResult eval)
        {
            return eval.Status == ReactionStatus.Success || eval.Status == ReactionStatus.Partial;
        }
    }
}
