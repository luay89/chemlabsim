using NUnit.Framework;
using ChemLabSimV3.Engine.Chemistry;

namespace ChemLabSimV3.Tests.EditMode.Domain
{
    /// <summary>Quantity validation and conversion to moles; missing data must never be guessed.</summary>
    public class QuantityTests
    {
        private static Quantity Q(double v, QuantityUnit u)
        {
            Assert.That(Quantity.TryCreate(v, u, out var q, out var e), Is.True, e);
            return q;
        }

        [TestCase(0.0)]
        [TestCase(-5.0)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void InvalidValues_AreRejected(double value)
        {
            Assert.That(Quantity.TryCreate(value, QuantityUnit.Gram, out _, out var error), Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void DefaultQuantity_IsRejectedByConverter()
        {
            var r = QuantityConverter.ToMoles(default, SubstanceSpec.For("NaCl", MatterState.Solid));
            Assert.That(r.Status, Is.EqualTo(ConversionStatus.InvalidQuantity));
        }

        [Test]
        public void Grams_OfSolid_UseMolarMass()
        {
            // 5.844 g NaCl / 58.44 g/mol = 0.1000 mol
            var r = QuantityConverter.ToMoles(Q(5.844, QuantityUnit.Gram), SubstanceSpec.For("NaCl", MatterState.Solid));
            Assert.That(r.Success, Is.True, r.Message);
            Assert.That(r.Moles, Is.EqualTo(0.1).Within(1e-4));
        }

        [Test]
        public void Millilitres_OfSolution_UseConfiguredConcentration()
        {
            // 25.0 mL of 0.100 M HCl = 2.50 mmol
            var spec = SubstanceSpec.For("HCl", MatterState.Aqueous);
            spec.StockConcentrationMolPerL = 0.100;
            var r = QuantityConverter.ToMoles(Q(25.0, QuantityUnit.Milliliter), spec);
            Assert.That(r.Success, Is.True, r.Message);
            Assert.That(r.Moles, Is.EqualTo(0.0025).Within(1e-9));
        }

        [Test]
        public void Millilitres_OfSolution_WithoutConcentration_AreMissing()
        {
            var r = QuantityConverter.ToMoles(Q(25.0, QuantityUnit.Milliliter), SubstanceSpec.For("HCl", MatterState.Aqueous));
            Assert.That(r.Status, Is.EqualTo(ConversionStatus.MissingConcentration));
            Assert.That(double.IsNaN(r.Moles), Is.True);
        }

        [Test]
        public void Millilitres_OfPureLiquid_UseDensity()
        {
            // 18.015 mL water × 1.000 g/mL / 18.015 g/mol = 1.000 mol (density given explicitly)
            var spec = SubstanceSpec.For("H2O", MatterState.Liquid);
            spec.DensityGPerMl = 1.000;
            var r = QuantityConverter.ToMoles(Q(18.015, QuantityUnit.Milliliter), spec);
            Assert.That(r.Moles, Is.EqualTo(1.0).Within(1e-3));
        }

        [Test]
        public void Millilitres_OfPureLiquid_WithoutDensity_AreMissing()
        {
            var r = QuantityConverter.ToMoles(Q(10.0, QuantityUnit.Milliliter), SubstanceSpec.For("C2H5OH", MatterState.Liquid));
            Assert.That(r.Status, Is.EqualTo(ConversionStatus.MissingDensity));
        }

        [Test]
        public void Grams_WithoutMolarMass_AreMissing()
        {
            var r = QuantityConverter.ToMoles(Q(10.0, QuantityUnit.Gram), SubstanceSpec.For("(C2H4)n", MatterState.Solid));
            Assert.That(r.Status, Is.EqualTo(ConversionStatus.MissingMolarMass));
        }

        [Test]
        public void Grams_OfSolution_AreUnsupported()
        {
            var r = QuantityConverter.ToMoles(Q(10.0, QuantityUnit.Gram), SubstanceSpec.For("HCl", MatterState.Aqueous));
            Assert.That(r.Status, Is.EqualTo(ConversionStatus.UnsupportedUnit));
        }

        [Test]
        public void Millilitres_OfPowder_AreUnsupported()
        {
            var r = QuantityConverter.ToMoles(Q(10.0, QuantityUnit.Milliliter), SubstanceSpec.For("NaCl", MatterState.Solid));
            Assert.That(r.Status, Is.EqualTo(ConversionStatus.UnsupportedUnit));
        }

        [Test]
        public void Gas_OnlyMoles()
        {
            var o2 = SubstanceSpec.For("O2", MatterState.Gas);
            Assert.That(QuantityConverter.ToMoles(Q(0.5, QuantityUnit.Mole), o2).Moles, Is.EqualTo(0.5));
            Assert.That(QuantityConverter.ToMoles(Q(500, QuantityUnit.Milliliter), o2).Status, Is.EqualTo(ConversionStatus.UnsupportedUnit));
        }

        [Test]
        public void UnknownState_IsReported()
        {
            var r = QuantityConverter.ToMoles(Q(1.0, QuantityUnit.Mole), SubstanceSpec.For("NaCl", MatterState.Unknown));
            Assert.That(r.Status, Is.EqualTo(ConversionStatus.UnknownState));
        }

        [TestCase("aq", MatterState.Aqueous)]
        [TestCase("Aqueous", MatterState.Aqueous)]
        [TestCase("s", MatterState.Solid)]
        [TestCase("liquid", MatterState.Liquid)]
        [TestCase("g", MatterState.Gas)]
        [TestCase("plasma", MatterState.Unknown)]
        [TestCase(null, MatterState.Unknown)]
        public void ParseState(string text, MatterState expected)
        {
            Assert.That(SubstanceSpec.ParseState(text), Is.EqualTo(expected));
        }
    }
}
