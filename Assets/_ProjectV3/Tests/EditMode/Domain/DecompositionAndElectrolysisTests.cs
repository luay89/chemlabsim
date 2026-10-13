using System.Collections.Generic;
using NUnit.Framework;
using ChemLabSimV3.Data;
using ChemLabSimV3.Engine;

namespace ChemLabSimV3.Tests.EditMode.Domain
{
    /// <summary>One-reactant reactions (heat, catalyst) and electrolysis (DC current).</summary>
    public class DecompositionAndElectrolysisTests
    {
        private static ReactionEntry Single(string id, string formula, float activationC, bool electrical)
        {
            return new ReactionEntry
            {
                id = id,
                name_en = id,
                reactionType = electrical ? "Electrochemistry" : "Thermal Decomposition",
                activationTempC = activationC,
                requiredMedium = "",
                reactants = new List<ReactionChemical> { new ReactionChemical { formula = formula, state = "aq", stoich = 2 } },
                products = new List<ReactionChemical> { new ReactionChemical { formula = "O2", state = "g", stoich = 1 } },
                visual_effects = new ReactionVisualEffects { electrical = electrical }
            };
        }

        private static ReactionEngine Engine(params ReactionEntry[] reactions)
            => new ReactionEngine(new ReactionDB { reactions = new List<ReactionEntry>(reactions) });

        private static MixRequest Request(string formula, float temperature, bool current)
        {
            var request = new MixRequest(new List<string> { formula }, ReactionMedium.Neutral, temperature, 0.5f, 0f, false);
            request.HasElectricCurrent = current;
            return request;
        }

        [Test]
        public void OneReactant_IsMatchedToItsDecomposition()
        {
            var engine = Engine(Single("rxn_heat", "NaHCO3", 270f, false));
            var hot = engine.Process(Request("NaHCO3", 300f, false));
            Assert.That(hot.Found, Is.True);
            Assert.That(hot.Status, Is.EqualTo(ReactionStatus.Success));

            var cold = engine.Process(Request("NaHCO3", 25f, false));
            Assert.That(cold.Status, Is.EqualTo(ReactionStatus.Fail));
        }

        [Test]
        public void Electrolysis_NeedsCurrent()
        {
            var engine = Engine(Single("rxn_elec", "H2O", 20f, true));
            Assert.That(engine.Process(Request("H2O", 25f, false)).Status, Is.EqualTo(ReactionStatus.Fail));
            Assert.That(engine.Process(Request("H2O", 25f, true)).Status, Is.Not.EqualTo(ReactionStatus.Fail));
        }

        [Test]
        public void Current_DoesNotChangeOrdinaryReactions()
        {
            var pipeline = ConditionPipeline.CreateDefault();
            var reaction = Single("rxn_heat", "NaHCO3", 270f, false);
            var input = new ConditionInput
            {
                TemperatureC = 300f, Medium = ReactionMedium.Neutral, EffectiveActivationC = 270f,
                ContactFactor = 1.2f, PressureAtm = 1f
            };
            var off = pipeline.Evaluate(reaction, input);
            input.HasElectricCurrent = true;
            var on = pipeline.Evaluate(reaction, input);

            Assert.That(on.OverallStatus, Is.EqualTo(off.OverallStatus));
            Assert.That(on.OverallRate, Is.EqualTo(off.OverallRate).Within(1e-5f));
            Assert.That(off.Conditions.Exists(c => c.Name == "Electricity"), Is.False);
        }
    }
}
