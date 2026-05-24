// ChemLabSim v3 — Condition Interface & Types
// Defines the contract for modular reaction condition evaluators.
// Each ICondition evaluates one independent aspect (temperature, medium, etc.).
// New conditions can be added without modifying existing ones (Open-Closed).

using UnityEngine;

namespace ChemLabSimV3.Engine
{
    public enum ConditionStatus
    {
        Passed,
        Partial,
        Failed
    }

    /// <summary>
    /// Result of a single condition evaluation.
    /// </summary>
    public struct ConditionResult
    {
        public string Name;
        public ConditionStatus Status;
        public float Factor;
        public string Reason;

        public bool Passed  => Status == ConditionStatus.Passed;
        public bool Failed  => Status == ConditionStatus.Failed;
        public bool IsPartial => Status == ConditionStatus.Partial;

        public static ConditionResult Pass(string name, float factor = 1f, string reason = "")
        {
            return new ConditionResult
            {
                Name   = name,
                Status = ConditionStatus.Passed,
                Factor = Mathf.Clamp01(factor),
                Reason = reason ?? string.Empty
            };
        }

        public static ConditionResult Partial(string name, float factor, string reason)
        {
            return new ConditionResult
            {
                Name   = name,
                Status = ConditionStatus.Partial,
                Factor = Mathf.Clamp01(factor),
                Reason = reason ?? string.Empty
            };
        }

        public static ConditionResult Fail(string name, string reason)
        {
            return new ConditionResult
            {
                Name   = name,
                Status = ConditionStatus.Failed,
                Factor = 0f,
                Reason = reason ?? string.Empty
            };
        }
    }

    /// <summary>
    /// Snapshot of lab conditions passed to each ICondition evaluator.
    /// Pre-computed derived values (ContactFactor, EffectiveActivationC) are
    /// calculated by ReactionEngine before the pipeline runs.
    /// </summary>
    public struct ConditionInput
    {
        public float TemperatureC;
        public float Stirring;
        public float Grinding;
        public ReactionMedium Medium;
        public bool HasCatalyst;

        /// <summary>Activation threshold after catalyst delta (if any).</summary>
        public float EffectiveActivationC;

        /// <summary>Contact quality factor derived from stirring × grinding (0.36–2.56).</summary>
        public float ContactFactor;

        /// <summary>Lab pressure in atmospheres (default 1.0).</summary>
        public float PressureAtm;
    }

    /// <summary>
    /// A single, pluggable reaction condition.
    /// Implement this interface to add new evaluation criteria to the pipeline.
    /// </summary>
    public interface ICondition
    {
        ConditionResult Evaluate(ReactionEntry reaction, ConditionInput input);
    }
}
