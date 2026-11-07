// ChemLabSim v3 — Guided experiment runner
// Pure C# — no Unity dependencies.
// Holds the experiments, runs a session step by step, and checks the current step against the
// live lab state. A step is done when the lab shows what it asks for; otherwise the check says
// what is still missing, in the student's terms.

using System;
using System.Collections.Generic;
using System.Linq;

namespace ChemLabSimV3.Domain.GuidedExperiments
{
    public class GuidedExperimentRunner
    {
        private readonly List<GuidedExperimentDef> _ordered = new List<GuidedExperimentDef>();
        private readonly Dictionary<string, GuidedExperimentDef> _experiments =
            new Dictionary<string, GuidedExperimentDef>(StringComparer.OrdinalIgnoreCase);

        public GuidedExperimentRunner(IEnumerable<GuidedExperimentDef> experiments)
        {
            if (experiments == null) return;
            foreach (var exp in experiments)
            {
                if (exp == null || string.IsNullOrEmpty(exp.id) || _experiments.ContainsKey(exp.id)) continue;
                _experiments[exp.id] = exp;
                _ordered.Add(exp);
            }
        }

        /// <summary>Number of registered experiments.</summary>
        public int Count => _ordered.Count;

        public GuidedExperimentDef Get(string experimentId)
        {
            if (string.IsNullOrEmpty(experimentId)) return null;
            return _experiments.TryGetValue(experimentId, out var exp) ? exp : null;
        }

        /// <summary>All experiments in the order of the data file.</summary>
        public IReadOnlyList<GuidedExperimentDef> GetAll() => _ordered;

        public List<GuidedExperimentDef> GetByCategory(string category)
            => _ordered.Where(e => string.Equals(e.category, category, StringComparison.OrdinalIgnoreCase)).ToList();

        public List<GuidedExperimentDef> GetByDifficulty(int level)
            => _ordered.Where(e => e.difficultyLevel == level).ToList();

        public ExperimentSession StartSession(string experimentId)
        {
            var def = Get(experimentId);
            if (def == null || def.steps == null || def.steps.Count == 0) return null;
            return new ExperimentSession { ExperimentId = def.id, CurrentStepIndex = 0, StartedAt = DateTime.UtcNow };
        }

        public ExperimentStep GetCurrentStep(ExperimentSession session)
        {
            var def = Get(session?.ExperimentId);
            if (def == null || session.CurrentStepIndex < 0 || session.CurrentStepIndex >= def.steps.Count) return null;
            return def.steps[session.CurrentStepIndex];
        }

        /// <summary>Moves to the next step; false (and the session completed) after the last one.</summary>
        public bool AdvanceStep(ExperimentSession session)
        {
            var def = Get(session?.ExperimentId);
            if (def == null || session.IsCompleted) return false;
            session.CurrentStepIndex++;
            if (session.CurrentStepIndex < def.steps.Count) return true;
            session.IsCompleted = true;
            session.CompletedAt = DateTime.UtcNow;
            return false;
        }

        public bool IsEligible(string experimentId, ICollection<string> completedExperimentIds)
        {
            var def = Get(experimentId);
            if (def == null) return false;
            if (string.IsNullOrEmpty(def.prerequisiteExperimentId)) return true;
            return completedExperimentIds != null && completedExperimentIds.Contains(def.prerequisiteExperimentId);
        }

        /// <summary>The status name a step compares with ("Success", "Partial", "Fail"); "NotFound" is set by the caller.</summary>
        public static string StatusOf(ReactionEvaluationResult result)
            => result.IsValid ? result.Status.ToString() : ReactionStatus.Fail.ToString();

        /// <summary>Is the step done in this lab state? If not, what is still missing.</summary>
        public static StepCheck Check(ExperimentStep step, LabSnapshot lab)
        {
            if (step == null) return StepCheck.Wait("no step");
            var v = step.validation ?? new StepValidation();
            var reagents = lab.Reagents ?? Array.Empty<string>();

            switch (step.ActionType)
            {
                case StepActionType.Observe:
                    return StepCheck.Wait("read this, then press Next");
                case StepActionType.Mix:
                    return CheckMix(v, lab, reagents);
            }

            // Setting steps: everything the validation names must hold now.
            var missing = Setup(v, lab, reagents);
            return missing == null ? StepCheck.Ok : StepCheck.Wait(missing);
        }

        private static StepCheck CheckMix(StepValidation v, LabSnapshot lab, IReadOnlyList<string> reagents)
        {
            string setup = Setup(v, lab, reagents);
            if (!lab.MixedSinceStepStart)
                return StepCheck.Wait(setup ?? "press Mix");

            bool idOk = string.IsNullOrEmpty(v.expectedReactionId) || v.expectedReactionId == lab.LastReactionId;
            bool statusOk = string.IsNullOrEmpty(v.expectedResultStatus)
                            || string.Equals(v.expectedResultStatus, lab.LastResultStatus, StringComparison.OrdinalIgnoreCase);
            if (idOk && statusOk) return StepCheck.Ok;
            return StepCheck.Wait(setup != null
                ? "that mix did not match this step: " + setup + ", then press Mix again"
                : "that mix gave a different result; check the settings and press Mix again");
        }

        /// <summary>The first setting that does not match yet, in the student's terms; null when all match.</summary>
        private static string Setup(StepValidation v, LabSnapshot lab, IReadOnlyList<string> reagents)
        {
            if (v.expectedReagents != null && v.expectedReagents.Count > 0)
            {
                var want = v.expectedReagents.Select(r => r.Trim()).ToList();
                var missing = want.Where(w => !reagents.Contains(w)).ToList();
                if (missing.Count > 0) return "choose " + string.Join(" and ", missing);
                var extra = reagents.Where(r => !want.Contains(r)).ToList();
                if (extra.Count > 0) return "remove " + string.Join(" and ", extra) + " (set that reagent to −)";
            }
            if (!float.IsNaN(v.minTemperature) && lab.TemperatureC < v.minTemperature)
                return $"raise the temperature to at least {v.minTemperature:0} °C";
            if (!float.IsNaN(v.maxTemperature) && lab.TemperatureC > v.maxTemperature)
                return $"lower the temperature to {v.maxTemperature:0} °C or less";
            if (!float.IsNaN(v.minGrinding) && lab.Grinding < v.minGrinding)
                return $"set grinding to at least {v.minGrinding * 100f:0}%";
            if (v.catalyst == 1 && !lab.Catalyst) return "switch the catalyst on";
            if (v.catalyst == 0 && lab.Catalyst) return "switch the catalyst off";
            if (v.current == 1 && !lab.Current) return "switch the electric current on";
            if (v.current == 0 && lab.Current) return "switch the electric current off";
            return null;
        }
    }
}
