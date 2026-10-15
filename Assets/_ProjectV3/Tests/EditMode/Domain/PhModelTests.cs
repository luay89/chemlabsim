using System.Collections.Generic;
using NUnit.Framework;
using ChemLabSimV3.Data;
using ChemLabSimV3.Engine.Chemistry;

namespace ChemLabSimV3.Tests.EditMode.Domain
{
    /// <summary>pH only where a model applies; textbook values at 25 °C.</summary>
    public class PhModelTests
    {
        [TestCase("HCl", 0.1, 1.00)]
        [TestCase("HNO3", 0.001, 3.00)]
        [TestCase("NaOH", 0.01, 12.00)]
        [TestCase("Ba(OH)2", 0.005, 12.00)]       // 2 OH− per formula
        [TestCase("CH3COOH", 0.1, 2.881)]         // weak acid, Ka 1.75e-5
        [TestCase("H2SO4", 0.01, 1.849)]          // Ka2 1.0e-2
        public void Solutions(string formula, double molar, double expected)
        {
            var r = PhModel.ForSolution(formula, molar);
            Assert.That(r.IsCalculated, Is.True, r.Basis);
            Assert.That(r.Ph, Is.EqualTo(expected).Within(0.002));
        }

        [Test]
        public void Water_IsNeutral() => Assert.That(PhModel.ForSolution("H2O", double.NaN).Ph, Is.EqualTo(7.0));

        [Test]
        public void VeryDiluteAcid_StaysAcidic()
        {
            // 1e-9 M HCl: water's own H+ dominates → pH 6.998, not 9
            Assert.That(PhModel.ForSolution("HCl", 1e-9).Ph, Is.EqualTo(6.998).Within(0.001));
        }

        [Test]
        public void ConcentratedSolution_IsFlaggedApproximate()
        {
            Assert.That(PhModel.ForSolution("HCl", 1.0).Approximate, Is.True);
            Assert.That(PhModel.ForSolution("HCl", 0.01).Approximate, Is.False);
        }

        [TestCase("CuSO4", 0.1)]   // hydrolysis not modelled
        [TestCase("NaCl", 0.1)]
        [TestCase("HCl", 0.0)]     // no concentration
        [TestCase("HCl", double.NaN)]
        public void Unsupported(string formula, double molar)
        {
            var r = PhModel.ForSolution(formula, molar);
            Assert.That(r.Status, Is.EqualTo(PhStatus.Unsupported));
            Assert.That(double.IsNaN(r.Ph), Is.True);
            Assert.That(r.Basis, Is.Not.Empty);
        }

        // ── After a neutralisation ────────────────────────────────

        private static ReactionChemical C(string f, string s, float n) => new ReactionChemical { formula = f, state = s, stoich = n };

        private static ReactionEntry HclNaoh() => new ReactionEntry
        {
            reactants = new List<ReactionChemical> { C("HCl", "aq", 1), C("NaOH", "aq", 1) },
            products = new List<ReactionChemical> { C("NaCl", "aq", 1), C("H2O", "l", 1) }
        };

        private static ReagentAmountInput Sol(string f, double mL, double m) => new ReagentAmountInput
        { Formula = f, Value = mL, Unit = "mL", State = "aqueous", StockConcentrationM = m };

        [Test]
        public void ExcessBase_AfterNeutralisation()
        {
            // 20 mmol HCl + 30 mmol NaOH in 50 mL → 0.2 M OH− → pH 13.30
            var amounts = AmountEvaluator.Evaluate(HclNaoh(), new[] { Sol("HCl", 20, 1.0), Sol("NaOH", 30, 1.0) });
            var r = PhModel.AfterReaction(HclNaoh(), amounts);
            Assert.That(r.IsCalculated, Is.True, r.Basis);
            Assert.That(r.Ph, Is.EqualTo(13.301).Within(0.002));
        }

        [Test]
        public void ExcessAcid_AfterNeutralisation()
        {
            // 25 mL 1.0 M HCl (25 mmol) + 25 mL 0.8 M NaOH (20 mmol) in 50 mL → 0.1 M H+ → pH 1.00
            var amounts = AmountEvaluator.Evaluate(HclNaoh(), new[] { Sol("HCl", 25, 1.0), Sol("NaOH", 25, 0.8) });
            Assert.That(PhModel.AfterReaction(HclNaoh(), amounts).Ph, Is.EqualTo(1.00).Within(0.002));
        }

        [Test]
        public void ExactNeutralisation_IsSeven()
        {
            var amounts = AmountEvaluator.Evaluate(HclNaoh(), new[] { Sol("HCl", 25, 0.1), Sol("NaOH", 25, 0.1) });
            Assert.That(PhModel.AfterReaction(HclNaoh(), amounts).Ph, Is.EqualTo(7.0).Within(1e-9));
        }

        [Test]
        public void WeakAcidWithBase_IsUnsupported()
        {
            var rxn = new ReactionEntry
            {
                reactants = new List<ReactionChemical> { C("CH3COOH", "aq", 1), C("NaOH", "aq", 1) },
                products = new List<ReactionChemical> { C("CH3COONa", "aq", 1), C("H2O", "l", 1) }
            };
            var amounts = AmountEvaluator.Evaluate(rxn, new[] { Sol("CH3COOH", 20, 1.0), Sol("NaOH", 10, 1.0) });
            Assert.That(PhModel.AfterReaction(rxn, amounts).Status, Is.EqualTo(PhStatus.Unsupported));
        }

        [Test]
        public void MissingAmounts_AreUnsupported()
        {
            Assert.That(PhModel.AfterReaction(HclNaoh(), null).Status, Is.EqualTo(PhStatus.Unsupported));
        }

        [Test]
        public void TemperatureBasis_FollowsTheData()
        {
            Assert.That(Thermometry.BasisFor(null), Is.EqualTo(TemperatureBasis.SetPoint));
            Assert.That(Thermometry.BasisFor(new ReactionEntry()), Is.EqualTo(TemperatureBasis.Estimated));
            Assert.That(Thermometry.BasisFor(new ReactionEntry { enthalpyKJPerMol = -55.8f }), Is.EqualTo(TemperatureBasis.Simulated));
        }
    }
}
