using System.Collections.Generic;
using NUnit.Framework;
using ChemLabSimV3.Data;
using ChemLabSimV3.Engine;
using ChemLabSimV3.Engine.Chemistry;

namespace ChemLabSimV3.Tests.EditMode.Domain
{
    /// <summary>Amounts in a MixRequest go through the engine to moles, limiting reactant and yield.</summary>
    public class AmountEvaluatorTests
    {
        private static ReactionChemical C(string f, string s, float n) => new ReactionChemical { formula = f, state = s, stoich = n };

        // HCl + NaOH → NaCl + H2O
        private static ReactionEntry Neutralisation() => new ReactionEntry
        {
            id = "t_neut", name_en = "Neutralisation", activationTempC = 20f, requiredMedium = "",
            reactants = new List<ReactionChemical> { C("HCl", "aq", 1), C("NaOH", "aq", 1) },
            products = new List<ReactionChemical> { C("NaCl", "aq", 1), C("H2O", "l", 1) }
        };

        private static ReagentAmountInput Solution(string f, double mL, double molar) => new ReagentAmountInput
        { Formula = f, Value = mL, Unit = "mL", State = "aqueous", StockConcentrationM = molar, StockLabel = $"{molar} M" };

        [Test]
        public void SolutionVolumes_GiveLimitingReactant()
        {
            // 20 mL 1.0 M HCl (20 mmol) + 30 mL 1.0 M NaOH (30 mmol) → HCl limits, 10 mmol NaOH left
            var e = AmountEvaluator.Evaluate(Neutralisation(), new[] { Solution("HCl", 20, 1.0), Solution("NaOH", 30, 1.0) });
            Assert.That(e.Success, Is.True, e.Stoichiometry.Message);
            Assert.That(e.Stoichiometry.LimitingFormulas, Is.EqualTo(new[] { "HCl" }));
            Assert.That(e.Stoichiometry.Reactants[1].LeftOverMol, Is.EqualTo(0.010).Within(1e-6));
            Assert.That(e.Stoichiometry.Products[0].TheoreticalGrams, Is.EqualTo(0.020 * 58.44).Within(1e-3));
        }

        [Test]
        public void MissingConcentration_StopsWithReason()
        {
            var e = AmountEvaluator.Evaluate(Neutralisation(), new[] { Solution("HCl", 20, 1.0), Solution("NaOH", 30, 0) });
            Assert.That(e.Success, Is.False);
            Assert.That(e.Stoichiometry.Status, Is.EqualTo(StoichiometryStatus.InsufficientData));
            Assert.That(e.Lines[1].Moles.Status, Is.EqualTo(ConversionStatus.MissingConcentration));
        }

        [Test]
        public void InvalidValueOrUnit_StopsWithReason()
        {
            var bad = Solution("NaOH", double.NaN, 1.0);
            Assert.That(AmountEvaluator.Evaluate(Neutralisation(), new[] { Solution("HCl", 20, 1.0), bad }).Success, Is.False);
            var wrongUnit = Solution("NaOH", 30, 1.0);
            wrongUnit.Unit = "kg";
            var e = AmountEvaluator.Evaluate(Neutralisation(), new[] { Solution("HCl", 20, 1.0), wrongUnit });
            Assert.That(e.Lines[1].Moles.Status, Is.EqualTo(ConversionStatus.UnsupportedUnit));
        }

        [Test]
        public void Engine_AttachesAmounts_OnlyWhenGiven()
        {
            var engine = new ReactionEngine(new ReactionDB { reactions = new List<ReactionEntry> { Neutralisation() } });
            var request = new MixRequest(new List<string> { "HCl", "NaOH" }, ReactionMedium.Neutral, 25f, 0.5f, 0f, false);
            Assert.That(engine.ProcessDetailed(request).Amounts, Is.Null);

            request.Amounts = new List<ReagentAmountInput> { Solution("HCl", 25, 1.0), Solution("NaOH", 25, 1.0) };
            var detailed = engine.ProcessDetailed(request);
            Assert.That(detailed.Amounts, Is.Not.Null);
            Assert.That(detailed.Amounts.Stoichiometry.IsStoichiometric, Is.True);
        }

        [Test]
        public void SolidMass_AndPureLiquidVolume()
        {
            // Mg + 2 HCl → MgCl2 + H2 ; 0.243 g Mg (10.0 mmol) + 10 mL 1.0 M HCl (10 mmol) → HCl limits (needs 20)
            var rxn = new ReactionEntry
            {
                reactants = new List<ReactionChemical> { C("Mg", "s", 1), C("HCl", "aq", 2) },
                products = new List<ReactionChemical> { C("MgCl2", "aq", 1), C("H2", "g", 1) }
            };
            var mg = new ReagentAmountInput { Formula = "Mg", Value = 0.24305, Unit = "g", State = "solid" };
            var e = AmountEvaluator.Evaluate(rxn, new[] { mg, Solution("HCl", 10, 1.0) });
            Assert.That(e.Stoichiometry.LimitingFormulas, Is.EqualTo(new[] { "HCl" }));
            Assert.That(e.Stoichiometry.Products[1].TheoreticalMol, Is.EqualTo(0.005).Within(1e-6));   // H2 = 10 mmol / 2

            var water = new ReagentAmountInput { Formula = "H2O", Value = 18.07, Unit = "mL", State = "liquid", DensityGPerMl = 0.997 };
            var spec = AmountEvaluator.ToSpec(water);
            Quantity.TryCreate(water.Value, QuantityUnit.Milliliter, out var q, out _);
            Assert.That(QuantityConverter.ToMoles(q, spec).Moles, Is.EqualTo(18.07 * 0.997 / 18.015).Within(1e-6));
        }
    }
}
