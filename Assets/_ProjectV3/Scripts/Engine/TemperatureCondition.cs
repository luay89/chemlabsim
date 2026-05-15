// ChemLabSim v3 — Temperature Condition
// Evaluates whether the reaction temperature meets the activation threshold.
// Returns Passed/Partial/Failed with a normalized factor.

using UnityEngine;

namespace ChemLabSimV3.Engine
{
    public class TemperatureCondition : ICondition
    {
        private const float PartialWindowC = 15f;
        private const float FullStrengthRangeC = 30f;

        public ConditionResult Evaluate(ReactionEntry reaction, ConditionInput input)
        {
            float threshold = input.EffectiveActivationC;

            if (input.TemperatureC >= threshold)
            {
                float excess = input.TemperatureC - threshold;
                float factor = 0.5f + 0.5f * Mathf.Clamp01(excess / FullStrengthRangeC);
                return ConditionResult.Pass("Temperature", factor,
                    $"Temperature {input.TemperatureC:0.#}\u00B0C meets activation threshold ({threshold:0.#}\u00B0C).");
            }

            float gap = threshold - input.TemperatureC;

            if (gap <= PartialWindowC)
            {
                float factor = Mathf.Clamp01(1f - gap / PartialWindowC) * 0.5f;
                return ConditionResult.Partial("Temperature", factor,
                    $"Temperature {input.TemperatureC:0.#}\u00B0C is slightly below activation threshold ({threshold:0.#}\u00B0C).");
            }

            return ConditionResult.Fail("Temperature",
                $"Temperature {input.TemperatureC:0.#}\u00B0C is too low. Activation requires {threshold:0.#}\u00B0C.");
        }
    }
}
