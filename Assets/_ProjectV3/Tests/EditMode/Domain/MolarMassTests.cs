using NUnit.Framework;
using ChemLabSimV3.Engine.Chemistry;

namespace ChemLabSimV3.Tests.EditMode.Domain
{
    /// <summary>Molar mass from formulas with IUPAC 2021 standard atomic weights (expected values worked by hand).</summary>
    public class MolarMassTests
    {
        [TestCase("H2O", 18.015)]              // 2(1.008) + 15.999
        [TestCase("NaCl", 58.44)]              // 22.990 + 35.45
        [TestCase("CO2", 44.009)]              // 12.011 + 2(15.999)
        [TestCase("H2SO4", 98.072)]            // 2(1.008) + 32.06 + 4(15.999)
        [TestCase("Ca(OH)2", 74.092)]          // 40.078 + 2(15.999 + 1.008)
        [TestCase("Ca3(PO4)2", 310.174)]       // 3(40.078) + 2(30.974 + 4·15.999)
        [TestCase("(NH4)2SO4", 132.134)]       // 2(14.007 + 4·1.008) + 32.06 + 4(15.999)
        [TestCase("Fe2(SO4)3", 399.858)]       // 2(55.845) + 3(32.06 + 4·15.999)
        [TestCase("CuSO4", 159.602)]           // 63.546 + 32.06 + 4(15.999)
        [TestCase("Mg", 24.305)]
        public void KnownFormulas(string formula, double expected)
        {
            var r = MolarMass.Of(formula);
            Assert.That(r.Success, Is.True, r.Error);
            Assert.That(r.GramsPerMole, Is.EqualTo(expected).Within(0.002));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        [TestCase("nacl")]          // lowercase start
        [TestCase("NaCl(aq)")]      // phase label is not part of a formula
        [TestCase("Ca(OH2")]        // unbalanced parenthesis
        [TestCase("CaOH)2")]
        [TestCase("H2O!")]
        [TestCase("()")]
        public void InvalidFormulas_Fail(string formula)
        {
            var r = MolarMass.Of(formula);
            Assert.That(r.Success, Is.False);
            Assert.That(double.IsNaN(r.GramsPerMole), Is.True);
            Assert.That(r.Error, Is.Not.Empty);
        }

        [TestCase("Xx2O")]      // not an element
        [TestCase("TcO2")]      // technetium has no standard atomic weight
        public void UnknownElements_Fail(string formula)
        {
            var r = MolarMass.Of(formula);
            Assert.That(r.Success, Is.False);
            Assert.That(r.Error, Does.Contain("standard atomic weight"));
        }

        [TestCase("FeSCN2+")]   // ion
        [TestCase("Cl-")]
        [TestCase("(C2H4)n")]   // polymer
        public void IonsAndPolymers_AreNotWeighable(string formula)
        {
            Assert.That(MolarMass.Of(formula).Success, Is.False);
        }
    }
}
