// ChemLabSim v3 — Surface Area Condition
// Evaluates effective contact quality from stirring + grinding.
// Contact factor = Lerp(0.6, 1.6, stirring) * Lerp(0.6, 1.6, grinding).

using UnityEngine;

namespace ChemLabSimV3.Engine
{
    public class SurfaceAreaCondition : ICondition
    {
        private const float PartialThreshold  = 0.85f;
        private const float StrongThreshold   = 1.20f;
        private const float MaxContactFactor  = 1.6f;

        public ConditionResult Evaluate(ReactionEntry reaction, ConditionInput input)
        {
            float cf = input.ContactFactor;

            if (cf >= StrongThreshold)
            {
                float factor = Mathf.Clamp01(cf / MaxContactFactor);
                return ConditionResult.Pass("SurfaceArea", factor,
                    $"Excellent reactant contact (factor {cf:0.00}). Stirring and grinding are strong.");
            }

            if (cf >= PartialThreshold)
            {
                float factor = Mathf.InverseLerp(PartialThreshold, StrongThreshold, cf) * 0.5f + 0.5f;
                return ConditionResult.Pass("SurfaceArea", factor,
                    $"Adequate reactant contact (factor {cf:0.00}).");
            }

            float partialFactor = Mathf.Clamp01(cf / PartialThreshold) * 0.5f;
            return ConditionResult.Partial("SurfaceArea", partialFactor,
                $"Weak reactant contact (factor {cf:0.00}). Increase stirring or grinding.");
        }
    }
}
