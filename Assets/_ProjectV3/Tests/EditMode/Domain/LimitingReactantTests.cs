using System.Collections.Generic;
using NUnit.Framework;
using ChemLabSimV3.Engine.Chemistry;

namespace ChemLabSimV3.Tests.EditMode.Domain
{
    /// <summary>Limiting reactant and theoretical yield (all expected values worked by hand).</summary>
    public class LimitingReactantTests
    {
        private static ReactionChemical C(string f, string state, float n) => new ReactionChemical { formula = f, state = state, stoich = n };

        // 2 H2 + O2 → 2 H2O
        private static ReactionEntry Water() => new ReactionEntry
        {
            id = "t_water",
            reactants = new List<ReactionChemical> { C("H2", "g", 2), C("O2", "g", 1) },
            products = new List<ReactionChemical> { C("H2O", "l", 2) }
        };

        private static Dictionary<string, double> Mol(params (string f, double n)[] items)
        {
            var d = new Dictionary<string, double>();
            foreach (var (f, n) in items) d[f] = n;
            return d;
        }

        [Test]
        public void SecondReactant_IsLimiting()
        {
            // H2 4 mol allows ξ = 2; O2 1 mol allows ξ = 1 → O2 limits, ξ = 1
            var r = LimitingReactant.Calculate(Water(), Mol(("H2", 4), ("O2", 1)));
            Assert.That(r.Success, Is.True, r.Message);
            Assert.That(r.LimitingFormulas, Is.EqualTo(new[] { "O2" }));
            Assert.That(r.Extent, Is.EqualTo(1.0).Within(1e-6));
            Assert.That(r.Reactants[0].Role, Is.EqualTo(ReactantRole.Excess));
            Assert.That(r.Reactants[0].LeftOverMol, Is.EqualTo(2.0).Within(1e-6));   // 4 − 2·1
            Assert.That(r.Products[0].TheoreticalMol, Is.EqualTo(2.0).Within(1e-6));
            Assert.That(r.Products[0].TheoreticalGrams, Is.EqualTo(36.03).Within(0.001)); // 2 × 18.015
        }

        [Test]
        public void FirstReactant_IsLimiting()
        {
            // H2 1 mol allows ξ = 0.5; O2 1 mol allows ξ = 1 → H2 limits, 0.5 mol O2 left
            var r = LimitingReactant.Calculate(Water(), Mol(("H2", 1), ("O2", 1)));
            Assert.That(r.LimitingFormulas, Is.EqualTo(new[] { "H2" }));
            Assert.That(r.Extent, Is.EqualTo(0.5).Within(1e-6));
            Assert.That(r.Reactants[1].LeftOverMol, Is.EqualTo(0.5).Within(1e-6));
            Assert.That(r.Products[0].TheoreticalMol, Is.EqualTo(1.0).Within(1e-6));
        }

        [Test]
        public void ExactProportions_HaveNoExcess()
        {
            var r = LimitingReactant.Calculate(Water(), Mol(("H2", 2), ("O2", 1)));
            Assert.That(r.Success, Is.True);
            Assert.That(r.IsStoichiometric, Is.True);
            Assert.That(r.LimitingFormulas, Is.Empty);
            foreach (var s in r.Reactants)
            {
                Assert.That(s.Role, Is.EqualTo(ReactantRole.ExactlyUsed));
                Assert.That(s.LeftOverMol, Is.EqualTo(0.0));
            }
        }

        [Test]
        public void RealMasses_ThroughQuantityConverter()
        {
            // HCl + NaOH → NaCl + H2O ; 25.0 mL 0.100 M HCl (2.50 mmol) + 0.200 g NaOH (5.00 mmol)
            var rxn = new ReactionEntry
            {
                reactants = new List<ReactionChemical> { C("HCl", "aq", 1), C("NaOH", "aq", 1) },
                products = new List<ReactionChemical> { C("NaCl", "aq", 1), C("H2O", "l", 1) }
            };
            var hcl = SubstanceSpec.For("HCl", MatterState.Aqueous);
            hcl.StockConcentrationMolPerL = 0.100;
            Quantity.TryCreate(25.0, QuantityUnit.Milliliter, out var vHcl, out _);
            Quantity.TryCreate(0.200, QuantityUnit.Gram, out var mNaOH, out _);
            double nHcl = QuantityConverter.ToMoles(vHcl, hcl).Moles;
            double nNaOH = QuantityConverter.ToMoles(mNaOH, SubstanceSpec.For("NaOH", MatterState.Solid)).Moles;

            var r = LimitingReactant.Calculate(rxn, Mol(("HCl", nHcl), ("NaOH", nNaOH)));
            Assert.That(r.LimitingFormulas, Is.EqualTo(new[] { "HCl" }));
            Assert.That(r.Products[0].TheoreticalGrams, Is.EqualTo(0.0025 * 58.44).Within(1e-4));   // 0.1461 g NaCl
        }

        [Test]
        public void MissingAmount_IsInsufficientData()
        {
            var r = LimitingReactant.Calculate(Water(), Mol(("H2", 2)));
            Assert.That(r.Status, Is.EqualTo(StoichiometryStatus.InsufficientData));
            Assert.That(r.Message, Does.Contain("O2"));
            Assert.That(double.IsNaN(r.Extent), Is.True);
        }

        [TestCase(0.0)]
        [TestCase(-1.0)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void InvalidAmount_IsInsufficientData(double bad)
        {
            var r = LimitingReactant.Calculate(Water(), Mol(("H2", 2), ("O2", bad)));
            Assert.That(r.Status, Is.EqualTo(StoichiometryStatus.InsufficientData));
        }

        [Test]
        public void UnbalancedEquation_IsRefused()
        {
            var rxn = Water();
            rxn.reactants[0].stoich = 1;   // H2 + O2 → 2 H2O does not balance
            var r = LimitingReactant.Calculate(rxn, Mol(("H2", 1), ("O2", 1)));
            Assert.That(r.Status, Is.EqualTo(StoichiometryStatus.Unbalanced));
        }

        [Test]
        public void SolventWater_CountsForBalance_ButNeverLimits()
        {
            // 2 NaOH + 2 Al (+ 2 H2O) → 2 NaAlO2 + 3 H2 ; Al 0.10 mol, NaOH 0.30 mol → Al limits, ξ = 0.05
            var rxn = new ReactionEntry
            {
                solvent_water = 2,
                reactants = new List<ReactionChemical> { C("NaOH", "aq", 2), C("Al", "s", 2) },
                products = new List<ReactionChemical> { C("NaAlO2", "aq", 2), C("H2", "g", 3) }
            };
            var r = LimitingReactant.Calculate(rxn, Mol(("NaOH", 0.30), ("Al", 0.10)));
            Assert.That(r.Success, Is.True, r.Message);
            Assert.That(r.LimitingFormulas, Is.EqualTo(new[] { "Al" }));
            Assert.That(r.Products[1].TheoreticalMol, Is.EqualTo(0.15).Within(1e-6));   // 3 × 0.05
        }

        [Test]
        public void ProductWithoutMolarMass_HasMolesButNoGrams()
        {
            // Tc + O2 → TcO2 : balanced, but technetium has no standard atomic weight,
            // so the yield is given in mol only and the grams are reported as unavailable.
            var rxn = new ReactionEntry
            {
                reactants = new List<ReactionChemical> { C("Tc", "s", 1), C("O2", "g", 1) },
                products = new List<ReactionChemical> { C("TcO2", "s", 1) }
            };
            var r = LimitingReactant.Calculate(rxn, Mol(("Tc", 0.01), ("O2", 0.02)));
            Assert.That(r.Success, Is.True, r.Message);
            Assert.That(r.LimitingFormulas, Is.EqualTo(new[] { "Tc" }));
            Assert.That(r.Products[0].TheoreticalMol, Is.EqualTo(0.01).Within(1e-6));
            Assert.That(double.IsNaN(r.Products[0].TheoreticalGrams), Is.True);
            Assert.That(r.Products[0].Note, Does.Contain("standard atomic weight"));
        }

        [Test]
        public void NoReaction_IsReported()
        {
            Assert.That(LimitingReactant.Calculate(null, Mol()).Status, Is.EqualTo(StoichiometryStatus.NoReaction));
        }
    }
}
