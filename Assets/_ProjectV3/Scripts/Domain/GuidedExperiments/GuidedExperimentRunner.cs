// ChemLabSim v3 — Guided Experiment Runner
// Pure C# — no Unity dependencies.
// Validates experiment steps and scores results.

using System;
using System.Collections.Generic;
using ChemLabSimV3.Data;

namespace ChemLabSimV3.Domain.GuidedExperiments
{
    /// <summary>
    /// Validates student actions against experiment step requirements.
    /// Pure business logic — no UI or Unity dependencies.
    /// </summary>
    public class GuidedExperimentRunner
    {
        private readonly Dictionary<string, GuidedExperimentDef> _experiments;

        public GuidedExperimentRunner(List<GuidedExperimentDef> experiments)
        {
            _experiments = new Dictionary<string, GuidedExperimentDef>(
                StringComparer.OrdinalIgnoreCase);

            if (experiments != null)
            {
                foreach (var exp in experiments)
                {
                    if (exp != null && !string.IsNullOrEmpty(exp.Id))
                        _experiments[exp.Id] = exp;
                }
            }
        }

        /// <summary>Number of registered experiments.</summary>
        public int Count => _experiments.Count;

        /// <summary>Get an experiment definition by ID.</summary>
        public GuidedExperimentDef Get(string experimentId)
        {
            if (string.IsNullOrEmpty(experimentId)) return null;
            return _experiments.TryGetValue(experimentId, out var exp) ? exp : null;
        }

        /// <summary>Get all available experiments.</summary>
        public IEnumerable<GuidedExperimentDef> GetAll()
        {
            return _experiments.Values;
        }

        /// <summary>Get experiments by category.</summary>
        public List<GuidedExperimentDef> GetByCategory(string category)
        {
            var result = new List<GuidedExperimentDef>();
            foreach (var exp in _experiments.Values)
            {
                if (string.Equals(exp.Category, category, StringComparison.OrdinalIgnoreCase))
                    result.Add(exp);
            }
            return result;
        }

        /// <summary>Get experiments by difficulty level.</summary>
        public List<GuidedExperimentDef> GetByDifficulty(int level)
        {
            var result = new List<GuidedExperimentDef>();
            foreach (var exp in _experiments.Values)
            {
                if (exp.DifficultyLevel == level)
                    result.Add(exp);
            }
            return result;
        }

        /// <summary>
        /// Start a new experiment session.
        /// </summary>
        public ExperimentSession StartSession(string experimentId)
        {
            var def = Get(experimentId);
            if (def == null) return null;

            return new ExperimentSession
            {
                ExperimentId = experimentId,
                CurrentStepIndex = 0,
                StepResults = new List<StepResult>(),
                TotalScore = 0f,
                IsCompleted = false,
                IsPerfect = false,
                StartedAt = DateTime.UtcNow,
                StepData = new Dictionary<string, object>()
            };
        }

        /// <summary>
        /// Get the current step definition for a session.
        /// </summary>
        public ExperimentStep GetCurrentStep(ExperimentSession session)
        {
            var def = Get(session?.ExperimentId);
            if (def == null || session == null) return null;
            if (session.CurrentStepIndex < 0 || session.CurrentStepIndex >= def.Steps.Count)
                return null;
            return def.Steps[session.CurrentStepIndex];
        }

        /// <summary>
        /// Validate a step against the current step's validation rules.
        /// Returns a StepResult with pass/fail and score.
        /// </summary>
        public StepResult ValidateStep(ExperimentSession session, MixRequest mixRequest, ReactionEvaluationResult evaluation)
        {
            var result = new StepResult
            {
                StepNumber = session.CurrentStepIndex + 1,
                Passed = false,
                Score = 0f,
                AttemptsCount = 1,
                HintUsed = false
            };

            var step = GetCurrentStep(session);
            if (step == null)
            {
                result.FeedbackKey = "guidedExp_error_stepNotFound";
                return result;
            }

            var validation = step.Validation;
            if (validation == null)
            {
                // No validation rules = auto-pass
                result.Passed = true;
                result.Score = 1f;
                result.FeedbackKey = "guidedExp_stepPassed";
                return result;
            }

            float maxScore = 1f;
            float earnedScore = 1f;

            // Validate temperature
            if (validation.MinTemperature.HasValue && mixRequest.Temperature < validation.MinTemperature.Value)
            {
                earnedScore -= 0.25f;
            }
            if (validation.MaxTemperature.HasValue && mixRequest.Temperature > validation.MaxTemperature.Value)
            {
                earnedScore -= 0.25f;
            }

            // Validate stirring
            if (validation.MinStirring.HasValue && mixRequest.Stirring < validation.MinStirring.Value)
            {
                earnedScore -= 0.2f;
            }

            // Validate grinding
            if (validation.MinGrinding.HasValue && mixRequest.Grinding < validation.MinGrinding.Value)
            {
                earnedScore -= 0.2f;
            }

            // Validate medium
            if (!string.IsNullOrEmpty(validation.RequiredMedium))
            {
                var reqMedium = validation.RequiredMedium;
                var actualMedium = mixRequest.Medium;
                if (!string.Equals(reqMedium, actualMedium, StringComparison.OrdinalIgnoreCase))
                {
                    // Check against ReactionMedium enum values
                    string actualStr = actualMedium;
                    if (!string.Equals(reqMedium, actualStr, StringComparison.OrdinalIgnoreCase))
                    {
                        earnedScore -= 0.3f;
                    }
                }
            }

            // Validate catalyst
            if (validation.RequireCatalyst.HasValue && validation.RequireCatalyst.Value)
            {
                if (!mixRequest.HasCatalyst)
                    earnedScore -= 0.2f;
            }

            // Validate reagents
            if (validation.ExpectedReagents != null && validation.ExpectedReagents.Count > 0)
            {
                foreach (var expected in validation.ExpectedReagents)
                {
                    if (!mixRequest.ReagentNames.Contains(expected))
                    {
                        earnedScore -= 0.3f;
                    }
                }
            }

            // Validate result status
            if (!string.IsNullOrEmpty(validation.ExpectedResultStatus))
            {
                if (evaluation != null)
                {
                    string actualStatus = evaluation.Status;
                    if (!string.Equals(validation.ExpectedResultStatus, actualStatus, StringComparison.OrdinalIgnoreCase)
                        && validation.ExpectedResultStatus != "Any")
                    {
                        earnedScore -= 0.3f;
                    }
                }
            }

            // Clamp score
            earnedScore = Math.Max(0f, Math.Min(1f, earnedScore));

            result.Score = earnedScore;
            result.Passed = earnedScore >= 0.5f;

            // Set feedback
            if (result.Passed)
            {
                result.FeedbackKey = earnedScore >= 0.9f
                    ? "guidedExp_stepPerfect"
                    : "guidedExp_stepPassed";
            }
            else
            {
                result.FeedbackKey = "guidedExp_stepFailed";
            }

            return result;
        }

        /// <summary>
        /// Advance to the next step in the experiment.
        /// </summary>
        public bool AdvanceStep(ExperimentSession session, StepResult stepResult)
        {
            if (session == null) return false;

            session.StepResults.Add(stepResult);
            session.TotalScore += stepResult.Score;

            var def = Get(session.ExperimentId);
            if (def == null) return false;

            session.CurrentStepIndex++;

            // Check if experiment is complete
            if (session.CurrentStepIndex >= def.Steps.Count)
            {
                session.IsCompleted = true;
                session.CompletedAt = DateTime.UtcNow;
                session.TotalScore /= def.Steps.Count; // Normalize to 0-1
                session.IsPerfect = session.TotalScore >= def.Evaluation?.PerfectScoreThreshold;
                return false; // No more steps
            }

            return true; // More steps remain
        }

        /// <summary>
        /// Get eligibility: whether a student can attempt an experiment based on prerequisites.
        /// </summary>
        public bool IsEligible(string experimentId, HashSet<string> completedExperimentIds)
        {
            var def = Get(experimentId);
            if (def == null) return false;
            if (string.IsNullOrEmpty(def.PrerequisiteExperimentId)) return true;
            return completedExperimentIds?.Contains(def.PrerequisiteExperimentId) == true;
        }
    }
}
