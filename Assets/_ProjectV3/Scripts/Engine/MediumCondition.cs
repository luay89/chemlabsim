// ChemLabSim v3 — Medium (pH) Condition
// Checks whether the reaction medium matches the requirement.
// A mismatch is a hard failure — no partial state for medium.

namespace ChemLabSimV3.Engine
{
    public class MediumCondition : ICondition
    {
        public ConditionResult Evaluate(ReactionEntry reaction, ConditionInput input)
        {
            if (string.IsNullOrWhiteSpace(reaction.requiredMedium))
                return ConditionResult.Pass("Medium", 1f, "No specific medium required.");

            if (ReactionEvaluator.MediumMatches(reaction.requiredMedium, input.Medium))
            {
                string label = ReactionEvaluator.MediumLabel(input.Medium);
                return ConditionResult.Pass("Medium", 1f,
                    $"Medium '{label}' matches the required environment.");
            }

            string required = reaction.requiredMedium.Trim();
            string actual   = ReactionEvaluator.MediumLabel(input.Medium);

            return ConditionResult.Fail("Medium",
                $"Reaction requires '{required}' medium but '{actual}' was provided.");
        }
    }
}
