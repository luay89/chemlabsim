using System;
using System.Linq;
using NUnit.Framework;
using ChemLabSimV3.Engine.Chemistry;

namespace ChemLabSimV3.Tests.EditMode.Domain
{
    /// <summary>Stage 6: strong acid / strong base titration curve and the flame-test table.</summary>
    public class TitrationAndFlameTests
    {
        // Independent of PhModel: exact strong acid/base pH with water's ions, Kw = 1e-14.
        // For a net base it is solved for [OH−] (same equation, no cancellation).
        private static double Expected(double netAcidMolar)
        {
            double d = Math.Abs(netAcidMolar);
            double x = (d + Math.Sqrt(d * d + 4e-14)) / 2.0;
            return netAcidMolar >= 0 ? -Math.Log10(x) : 14.0 + Math.Log10(x);
        }

        private static TitrationModel HClWithNaOH(double acidM = 0.1, double acidMl = 25, double baseM = 0.1)
            => TitrationModel.Create(new TitrationSetup
            { Analyte = "HCl", AnalyteMolar = acidM, AnalyteMl = acidMl, Titrant = "NaOH", TitrantMolar = baseM });

        private static double PhAt(TitrationModel m, double ml)
        {
            Assert.That(m.TryPointAt(ml, out var p, out string error), Is.True, error);
            return p.Ph;
        }

        // ── Titration ────────────────────────────────────────────────

        [Test]
        public void StrongAcidStrongBase_CurvePoints()
        {
            var m = HClWithNaOH();
            Assert.That(m.Supported, Is.True, m.Reason);
            Assert.That(m.EquivalenceMl, Is.EqualTo(25.0).Within(1e-9));

            Assert.That(PhAt(m, 0), Is.EqualTo(Expected(0.1)).Within(1e-9));                    // 1.00
            Assert.That(PhAt(m, 10), Is.EqualTo(Expected(1.5e-3 / 0.035)).Within(1e-9));        // 1.37
            Assert.That(PhAt(m, 24.9), Is.EqualTo(Expected(1e-5 / 0.0499)).Within(1e-6));       // 3.70
            Assert.That(PhAt(m, 25), Is.EqualTo(7.0).Within(1e-9));
            Assert.That(PhAt(m, 25.1), Is.EqualTo(Expected(-1e-5 / 0.0501)).Within(1e-6));      // 10.30
            Assert.That(PhAt(m, 30), Is.EqualTo(Expected(-5e-4 / 0.055)).Within(1e-9));        // 11.96
            Assert.That(PhAt(m, 0), Is.EqualTo(1.0).Within(1e-6));
            Assert.That(PhAt(m, 30), Is.EqualTo(11.959).Within(1e-3));
        }

        [Test]
        public void Regions_AndApproximateFlag()
        {
            var m = HClWithNaOH();
            Assert.That(m.RegionAt(0), Is.EqualTo(TitrationRegion.Start));
            Assert.That(m.RegionAt(12), Is.EqualTo(TitrationRegion.BeforeEquivalence));
            Assert.That(m.RegionAt(25), Is.EqualTo(TitrationRegion.Equivalence));
            Assert.That(m.RegionAt(26), Is.EqualTo(TitrationRegion.AfterEquivalence));

            var concentrated = HClWithNaOH(1.0, 20, 1.0);   // bench stock: 1.0 M → beyond the dilute limit
            concentrated.TryPointAt(0, out var p0, out _);
            Assert.That(p0.Approximate, Is.True);
            concentrated.TryPointAt(20, out var eq, out _);
            Assert.That(eq.Approximate, Is.False);
            Assert.That(eq.Ph, Is.EqualTo(7.0).Within(1e-9));
        }

        [Test]
        public void CurveIsMonotonic_AndContainsEquivalence()
        {
            var curve = HClWithNaOH().Curve(50, 0.5);
            Assert.That(curve.Any(p => p.Region == TitrationRegion.Equivalence), Is.True);
            for (int i = 1; i < curve.Count; i++)
                Assert.That(curve[i].Ph, Is.GreaterThanOrEqualTo(curve[i - 1].Ph), $"at {curve[i].AddedMl} mL");
        }

        [Test]
        public void BaseInBeaker_DiproticBase()
        {
            var m = TitrationModel.Create(new TitrationSetup
            { Analyte = "NaOH", AnalyteMolar = 0.1, AnalyteMl = 20, Titrant = "HCl", TitrantMolar = 0.2 });
            Assert.That(m.AcidInBeaker, Is.False);
            Assert.That(m.EquivalenceMl, Is.EqualTo(10.0).Within(1e-9));
            Assert.That(PhAt(m, 0), Is.EqualTo(13.0).Within(1e-6));
            Assert.That(PhAt(m, 10), Is.EqualTo(7.0).Within(1e-9));
            Assert.That(PhAt(m, 15), Is.EqualTo(Expected(1e-3 / 0.035)).Within(1e-9));   // acid excess

            var ba = TitrationModel.Create(new TitrationSetup
            { Analyte = "HCl", AnalyteMolar = 0.1, AnalyteMl = 25, Titrant = "Ba(OH)2", TitrantMolar = 0.05 });
            Assert.That(ba.EquivalenceMl, Is.EqualTo(25.0).Within(1e-9));   // 2 OH− per Ba(OH)2
        }

        [TestCase("CH3COOH", "NaOH", "weak acid")]
        [TestCase("H2SO4", "NaOH", "Ka2")]
        [TestCase("HCl", "HNO3", "both solutions are acids")]
        [TestCase("NaOH", "KOH", "both solutions are bases")]
        [TestCase("NaCl", "NaOH", "not a strong acid or strong base")]
        [TestCase("", "NaOH", "choose the solution")]
        public void Unsupported_SaysWhy_AndGivesNoPoints(string analyte, string titrant, string reason)
        {
            var m = TitrationModel.Create(new TitrationSetup
            { Analyte = analyte, AnalyteMolar = 0.1, AnalyteMl = 25, Titrant = titrant, TitrantMolar = 0.1 });
            Assert.That(m.Supported, Is.False);
            StringAssert.Contains(reason, m.Reason);
            Assert.That(m.TryPointAt(5, out _, out string error), Is.False);
            Assert.That(error, Is.EqualTo(m.Reason));
            Assert.That(m.Curve(50, 1), Is.Empty);
            Assert.That(double.IsNaN(m.EquivalenceMl), Is.True);
        }

        [TestCase(0.0, 25.0, 0.1)]
        [TestCase(double.NaN, 25.0, 0.1)]
        [TestCase(0.1, -5.0, 0.1)]
        [TestCase(0.1, 25.0, double.PositiveInfinity)]
        public void InvalidSetupNumbers_AreRefused(double acidM, double acidMl, double baseM)
        {
            Assert.That(HClWithNaOH(acidM, acidMl, baseM).Supported, Is.False);
        }

        [TestCase(-1.0)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void InvalidAddedVolume_IsRefused(double ml)
        {
            Assert.That(HClWithNaOH().TryPointAt(ml, out _, out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void TemperatureRise_FromHeatOfNeutralisation()
        {
            // 20 mL 1.0 M HCl + 1.0 M NaOH: at 20 mL, 20 mmol water in 40 mL → 6.67 K; after it, no more heat.
            var m = HClWithNaOH(1.0, 20, 1.0);
            m.TryPointAt(0, out var p0, out _);
            m.TryPointAt(20, out var eq, out _);
            m.TryPointAt(30, out var after, out _);
            Assert.That(p0.TemperatureRiseK, Is.EqualTo(0.0).Within(1e-12));
            Assert.That(eq.TemperatureRiseK, Is.EqualTo(0.020 * 55815.0 / (40 * 4.184)).Within(1e-9));
            Assert.That(after.TemperatureRiseK, Is.EqualTo(0.020 * 55815.0 / (50 * 4.184)).Within(1e-9));   // diluted, no new heat
            Assert.That(Calorimetry.NeutralisationRiseK(0.01, 0), Is.NaN);
            Assert.That(Calorimetry.NeutralisationRiseK(double.NaN, 10), Is.NaN);
        }

        // ── Flame test ───────────────────────────────────────────────

        [TestCase("LiOH", "Li", "crimson")]
        [TestCase("NaCl", "Na", "yellow")]
        [TestCase("KCl", "K", "lilac")]
        [TestCase("CaCl2", "Ca", "orange-red")]
        [TestCase("BaCl2", "Ba", "green")]
        [TestCase("CuSO4", "Cu", "blue-green")]
        [TestCase("CuCl2", "Cu", "blue-green")]
        [TestCase("NaAlO2", "Na", "yellow")]          // Al gives no colour, Na does
        public void FlameColour_FromTheMetalIon(string formula, string element, string colour)
        {
            var r = FlameTest.Test(formula);
            Assert.That(r.Status, Is.EqualTo(FlameTestStatus.Characteristic), r.Explanation);
            Assert.That(r.Entry.Element, Is.EqualTo(element));
            StringAssert.Contains(colour, r.Entry.ColourName);
            StringAssert.Contains("nm", r.Explanation);
        }

        [TestCase("MgCl2")]
        [TestCase("ZnSO4")]
        [TestCase("Al2(SO4)3")]
        public void NoCharacteristicColour(string formula)
        {
            Assert.That(FlameTest.Test(formula).Status, Is.EqualTo(FlameTestStatus.NoColour));
        }

        [TestCase("Na", "pure metal")]
        [TestCase("H2O", "no metal ion")]
        [TestCase("NH4Cl", "no metal ion")]
        [TestCase("KMnO4", "Mn is not in this flame-test table")]
        [TestCase("FeCl3", "Fe is not in this flame-test table")]
        [TestCase("", "no sample")]
        [TestCase("FeSCN2+", "not a formula that can be tested")]
        public void Unsupported_NeverGuessesAColour(string formula, string reason)
        {
            var r = FlameTest.Test(formula);
            Assert.That(r.Status, Is.EqualTo(FlameTestStatus.Unsupported));
            Assert.That(r.HasColour, Is.False);
            StringAssert.Contains(reason, r.Explanation);
        }

        [Test]
        public void TableColours_AreIndependentOfLiquidColours()
        {
            // Every colouring entry has a documented emission and a visible colour.
            foreach (var e in FlameTest.Entries)
            {
                Assert.That(e.Emission, Is.Not.Empty, e.Element);
                Assert.That(Math.Max(e.R, Math.Max(e.G, e.B)), Is.GreaterThan(0.5f), e.Element);
            }
            // Copper sulfate solution is blue, its flame is blue-green: the flame does not follow the liquid.
            Assert.That(FlameTest.Test("CuSO4").Entry.ColourName, Is.EqualTo("blue-green"));
        }
    }
}
