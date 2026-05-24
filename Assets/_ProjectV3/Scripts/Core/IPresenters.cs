// ChemLabSim v3 — Presenter Interfaces
// Presentation Layer contracts.
// Presenters:
// 1. Receive domain events / use case results
// 2. Transform to ViewModels (pure data, no logic)
// 3. Delegate to view binders

using ChemLabSimV3.Data;
using ChemLabSimV3.Data.Models;
using ChemLabSimV3.Engine;

namespace ChemLabSimV3.Application.Presenters
{
    /// <summary>
    /// Presenter for reaction evaluation results.
    /// Consumes ReactionOutput from use case → produces ReactionResultViewModel.
    /// </summary>
    public interface IReactionPresenter
    {
        /// <summary>Handle a mix request (delegate to use case internally).</summary>
        void OnMixRequested(MixRequest request);

        /// <summary>Handle reaction evaluated event → update view.</summary>
        void OnReactionEvaluated(Engine.ReactionOutput output);
    }

    /// <summary>
    /// Presenter for progress updates.
    /// Consumes progress state → produces ProgressViewModel.
    /// </summary>
    public interface IProgressPresenter
    {
        /// <summary>Handle progress updated event → update view.</summary>
        void OnProgressUpdated(ProgressState progress);

        /// <summary>Handle level up event → show level up UI.</summary>
        void OnLevelUp(int newLevel);
    }

    /// <summary>
    /// Presenter for quiz display.
    /// Generates quiz questions and handles user answers.
    /// </summary>
    public interface IQuizPresenter
    {
        /// <summary>Handle quiz generation event → show quiz panel.</summary>
        void OnQuizGenerated(QuizQuestion question);

        /// <summary>Handle quiz answer selected → validate and update.</summary>
        void OnAnswerSelected(int answerIndex);
    }

    /// <summary>
    /// Presenter for achievements.
    /// Shows achievement unlock toasts.
    /// </summary>
    public interface IAchievementPresenter
    {
        /// <summary>Handle achievement unlocked event → show toast.</summary>
        void OnAchievementUnlocked(AchievementDef achievement);
    }

    /// <summary>
    /// Presenter for challenges.
    /// Shows challenge instructions and tracks completion.
    /// </summary>
    public interface IChallengePresenter
    {
        /// <summary>Handle challenge started event → show challenge UI.</summary>
        void OnChallengeStarted(ChallengeDef challenge);

        /// <summary>Handle challenge completed event → show completion feedback.</summary>
        void OnChallengeCompleted(ChallengeDef challenge, bool succeeded);
    }

    /// <summary>
    /// Presenter for objectives.
    /// Shows objective descriptions and progress.
    /// </summary>
    public interface IObjectivePresenter
    {
        /// <summary>Handle objective started event → show objective UI.</summary>
        void OnObjectiveStarted(ObjectiveCondition objective);

        /// <summary>Handle objective completed event → show completion feedback.</summary>
        void OnObjectiveCompleted(ObjectiveCondition objective);
    }

    /// <summary>
    /// Presenter for guidance messages.
    /// Shows contextual tips and warnings.
    /// </summary>
    public interface IGuidancePresenter
    {
        /// <summary>Handle guidance event → show guidance message.</summary>
        void OnGuidance(string message);

        /// <summary>Handle warning event → show warning message.</summary>
        void OnWarning(string message);
    }
}
