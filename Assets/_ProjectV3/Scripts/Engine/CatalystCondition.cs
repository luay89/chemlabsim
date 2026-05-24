// ChemLabSim v3 — Catalyst Condition
// Evaluates catalyst usage: required/applied/unused.
// Hard-fails only when the reaction explicitly requires a catalyst and none is provided.

namespace ChemLabSimV3.Engine
{
    public class CatalystCondition : ICondition
    {
        public ConditionResult Evaluate(ReactionEntry reaction, ConditionInput input)
        {
            if (reaction.requiresCatalyst && !input.HasCatalyst)
            {
                return ConditionResult.Fail("Catalyst",
                    "This reaction requires a catalyst to proceed.");
            }

            if (input.HasCatalyst && reaction.catalystAllowed)
            {
                float delta = reaction.catalystDeltaTempC;
                return ConditionResult.Pass("Catalyst", 1f,
                    $"Catalyst applied \u2014 activation threshold lowered by {delta:0.#}\u00B0C.");
            }

            if (input.HasCatalyst && !reaction.catalystAllowed)
            {
                return ConditionResult.Pass("Catalyst", 0.5f,
                    "Catalyst added but this reaction does not benefit from it.");
            }

            // No catalyst used — neutral outcome
            return ConditionResult.Pass("Catalyst", 0.5f, "No catalyst used.");
        }
    }
}
