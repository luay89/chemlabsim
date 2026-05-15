// ChemLabSim v3 — Pressure Condition
// Evaluates whether lab pressure conditions are suitable for the reaction.
// Primarily affects gas-phase reactions via Le Chatelier's principle:
//   - Reactions producing gas: high pressure → lower yield (Partial/Fail)
//   - Reactions consuming gas: high pressure → improved yield (Pass boost)
//   - Reactions with no gas change: pressure irrelevant (Pass)
//
// For condensed-phase-only reactions, pressure has negligible effect → Pass.

using System;
using ChemLabSimV3.Engine.Chemistry;

namespace ChemLabSimV3.Engine
{
    public class PressureCondition : ICondition
    {
        // Pressure thresholds (atm)
        private const float StandardPressure = 1f;
        private const float HighPressureThreshold = 2f;
        private const float LowPressureThreshold = 0.3f;

        // Rate modifiers
        private const float PartialFactorHigh = 0.6f;
        private const float PartialFactorLow = 0.7f;
        private const float BoostFactor = 1f; // capped at 1.0 by ConditionResult

        public ConditionResult Evaluate(ReactionEntry reaction, ConditionInput input)
        {
            const string Name = "Pressure";

            float pressure = input.PressureAtm;
            if (pressure <= 0f) pressure = StandardPressure;

            // Calculate Δn_gas for this reaction
            float deltaGas = EquilibriumSolver.CalcDeltaGasMoles(reaction);

            // No gas change → pressure irrelevant
            if (Math.Abs(deltaGas) < 0.001f)
            {
                return ConditionResult.Pass(Name, 1f,
                    "Pressure has negligible effect on this reaction.");
            }

            // Gas is produced (Δn > 0):
            //   High pressure → opposes gas formation → Partial
            //   Low pressure → favors gas release → Pass
            if (deltaGas > 0f)
            {
                if (pressure > HighPressureThreshold)
                {
                    return ConditionResult.Partial(Name, PartialFactorHigh,
                        $"High pressure ({pressure:0.#} atm) opposes gas formation.");
                }

                if (pressure < LowPressureThreshold)
                {
                    return ConditionResult.Pass(Name, BoostFactor,
                        $"Low pressure ({pressure:0.#} atm) favors gas evolution.");
                }

                return ConditionResult.Pass(Name, 1f,
                    "Pressure is within normal range for this gas-producing reaction.");
            }

            // Gas is consumed (Δn < 0):
            //   High pressure → favors forward reaction → Pass
            //   Low pressure → opposes forward reaction → Partial
            if (pressure < LowPressureThreshold)
            {
                return ConditionResult.Partial(Name, PartialFactorLow,
                    $"Low pressure ({pressure:0.#} atm) hinders gas consumption.");
            }

            if (pressure > HighPressureThreshold)
            {
                return ConditionResult.Pass(Name, BoostFactor,
                    $"High pressure ({pressure:0.#} atm) favors the forward reaction.");
            }

            return ConditionResult.Pass(Name, 1f,
                "Pressure is within normal range for this reaction.");
        }
    }
}
