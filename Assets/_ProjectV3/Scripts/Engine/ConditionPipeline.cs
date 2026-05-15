// ChemLabSim v3 — Condition Pipeline
// Chains multiple ICondition evaluators and aggregates their results.
// CreateDefault() assembles the standard 4-condition pipeline:
//   Medium → Temperature → Catalyst → SurfaceArea.
// Custom pipelines can be built by adding conditions manually.

using System.Collections.Generic;

namespace ChemLabSimV3.Engine
{
    /// <summary>Aggregated result from running all conditions in the pipeline.</summary>
    public struct PipelineResult
    {
        public ReactionStatus OverallStatus;
        public float OverallRate;
        public List<ConditionResult> Conditions;
        public string Summary;

        public bool AnyFailed
        {
            get
            {
                if (Conditions == null) return false;
                for (int i = 0; i < Conditions.Count; i++)
                    if (Conditions[i].Failed) return true;
                return false;
            }
        }

        public bool AnyPartial
        {
            get
            {
                if (Conditions == null) return false;
                for (int i = 0; i < Conditions.Count; i++)
                    if (Conditions[i].IsPartial) return true;
                return false;
            }
        }
    }

    /// <summary>
    /// Evaluates a reaction against an ordered list of conditions.
    /// If any condition fails → overall Fail.
    /// If any condition is partial → overall Partial.
    /// Otherwise → Success.
    /// </summary>
    public class ConditionPipeline
    {
        private readonly List<ICondition> _conditions = new List<ICondition>();

        public void Add(ICondition condition)
        {
            if (condition != null)
                _conditions.Add(condition);
        }

        /// <summary>
        /// Creates the standard pipeline: Medium, Temperature, Catalyst, SurfaceArea, Pressure.
        /// Add new conditions here to extend the engine without touching other code.
        /// </summary>
        public static ConditionPipeline CreateDefault()
        {
            var pipeline = new ConditionPipeline();
            pipeline.Add(new MediumCondition());
            pipeline.Add(new TemperatureCondition());
            pipeline.Add(new CatalystCondition());
            pipeline.Add(new SurfaceAreaCondition());
            pipeline.Add(new PressureCondition());
            return pipeline;
        }

        public PipelineResult Evaluate(ReactionEntry reaction, ConditionInput input)
        {
            var results = new List<ConditionResult>(_conditions.Count);
            bool anyFailed  = false;
            bool anyPartial = false;
            float factorSum = 0f;

            for (int i = 0; i < _conditions.Count; i++)
            {
                var cr = _conditions[i].Evaluate(reaction, input);
                results.Add(cr);

                if (cr.Failed)    anyFailed  = true;
                if (cr.IsPartial) anyPartial = true;
                factorSum += cr.Factor;
            }

            int count = _conditions.Count;
            float avgFactor = count > 0 ? factorSum / count : 0f;

            ReactionStatus status;
            string summary;

            if (anyFailed)
            {
                status  = ReactionStatus.Fail;
                summary = BuildFailSummary(results);
            }
            else if (anyPartial)
            {
                status  = ReactionStatus.Partial;
                summary = BuildPartialSummary(results);
            }
            else
            {
                status  = ReactionStatus.Success;
                summary = BuildSuccessSummary(results);
            }

            return new PipelineResult
            {
                OverallStatus = status,
                OverallRate   = status == ReactionStatus.Fail ? 0f : avgFactor,
                Conditions    = results,
                Summary       = summary
            };
        }

        private static string BuildFailSummary(List<ConditionResult> results)
        {
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].Failed)
                    return $"Reaction failed: {results[i].Reason}";
            }
            return "Reaction failed.";
        }

        private static string BuildPartialSummary(List<ConditionResult> results)
        {
            var partials = new List<string>();
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].IsPartial)
                    partials.Add(results[i].Reason);
            }

            if (partials.Count == 1)
                return $"Reaction partially occurred: {partials[0]}";

            return $"Reaction partially occurred due to {partials.Count} limiting factors.";
        }

        private static string BuildSuccessSummary(List<ConditionResult> results)
        {
            var highlights = new List<string>();
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].Factor >= 0.9f && !string.IsNullOrEmpty(results[i].Reason))
                    highlights.Add(results[i].Reason);
            }

            if (highlights.Count > 0)
                return "Reaction proceeded successfully. " + highlights[0];

            return "All conditions met. Reaction proceeds successfully.";
        }
    }
}
