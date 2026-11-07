// ChemLabSim v3 — Electricity Condition
// Electrolysis (visual_effects.electrical) only runs while a DC current flows through the
// vessel. Other reactions are not affected (the pipeline skips this condition for them).

namespace ChemLabSimV3.Engine
{
    public class ElectricityCondition : IApplicableCondition
    {
        public bool AppliesTo(ReactionEntry reaction) => IsElectrolysis(reaction);

        public static bool IsElectrolysis(ReactionEntry reaction)
            => reaction?.visual_effects != null && reaction.visual_effects.electrical;

        public ConditionResult Evaluate(ReactionEntry reaction, ConditionInput input)
        {
            if (!input.HasElectricCurrent)
                return ConditionResult.Fail("Electricity",
                    "This is electrolysis: it only runs while an electric current flows. Switch on the DC power.");

            return ConditionResult.Pass("Electricity", 1f,
                "Electric current is flowing: ions move to the electrodes and are discharged there.");
        }
    }
}
