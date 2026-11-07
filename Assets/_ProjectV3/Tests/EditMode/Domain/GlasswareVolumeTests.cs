using System.Linq;
using NUnit.Framework;
using ChemLabSimV3.Controllers;
using ChemLabSimV3.Data;
using ChemLabSimV3.Engine.Chemistry;

namespace ChemLabSimV3.Tests.EditMode.Domain
{
    /// <summary>Printed volume scales of the bench glassware and the volume the liquid stands at.</summary>
    public class GlasswareVolumeTests
    {
        // ── Scales (the capacities used in the scene) ─────────────────

        [Test]
        public void Beaker100_MarksEvery10_NumbersEvery20()
        {
            var s = GraduatedScale.Beaker100(0.80);
            var marks = s.Marks();
            Assert.That(marks.Select(m => m.Ml), Is.EqualTo(new double[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 }));
            Assert.That(marks.Where(m => m.Labelled).Select(m => m.Ml), Is.EqualTo(new double[] { 20, 40, 60, 80, 100 }));
            Assert.That(s.Approximate, Is.True);
            // Equal spacing: a straight-sided vessel.
            for (int i = 1; i < marks.Count; i++)
                Assert.That(marks[i].Height - marks[i - 1].Height, Is.EqualTo(0.08).Within(1e-12));
            Assert.That(marks.Last().Height, Is.EqualTo(0.80).Within(1e-12));
        }

        [Test]
        public void Cylinder50_MarksEvery1_Major5_NumbersEvery10()
        {
            var s = GraduatedScale.Cylinder50(0.58);
            var marks = s.Marks();
            Assert.That(marks.Count, Is.EqualTo(50));
            Assert.That(marks.Count(m => m.Major), Is.EqualTo(10));
            Assert.That(marks.Where(m => m.Labelled).Select(m => m.Ml), Is.EqualTo(new double[] { 10, 20, 30, 40, 50 }));
            Assert.That(marks.First(m => m.Ml == 25).Height, Is.EqualTo(0.29).Within(1e-12));
            Assert.That(s.Approximate, Is.False);
        }

        [Test]
        public void Burette50_ReadsFromZeroAtTheTop()
        {
            var s = GraduatedScale.Burette50(0.75);
            var marks = s.Marks();
            Assert.That(marks.First().Ml, Is.EqualTo(0));
            Assert.That(marks.First().Height, Is.EqualTo(0.75).Within(1e-12));   // 0 mL at the top
            Assert.That(marks.Last().Ml, Is.EqualTo(50));
            Assert.That(marks.Last().Height, Is.EqualTo(0.0).Within(1e-12));     // 50 mL at the bottom
            Assert.That(marks.Where(m => m.Labelled).Select(m => m.Ml), Is.EqualTo(new double[] { 0, 10, 20, 30, 40, 50 }));
            // After 12 mL run out, 38 mL is left: the surface is at the 12 mL mark.
            Assert.That(s.HeightOf(38), Is.EqualTo(marks.First(m => m.Ml == 12).Height).Within(1e-12));
        }

        [Test]
        public void HeightOf_EmptyPartialFullInvalid()
        {
            var s = GraduatedScale.Beaker100(0.80);
            Assert.That(s.HeightOf(0), Is.EqualTo(0));
            Assert.That(s.HeightOf(25), Is.EqualTo(0.20).Within(1e-12));
            Assert.That(s.HeightOf(50), Is.EqualTo(2 * s.HeightOf(25)).Within(1e-12));   // linear
            Assert.That(s.HeightOf(100), Is.EqualTo(0.80).Within(1e-12));
            Assert.That(s.Exceeds(100), Is.False);
            Assert.That(s.Exceeds(101), Is.True);
            Assert.That(s.HeightOf(-1), Is.NaN);
            Assert.That(s.HeightOf(double.NaN), Is.NaN);
            Assert.That(s.HeightOf(double.PositiveInfinity), Is.NaN);
            Assert.Throws<System.ArgumentException>(() => new GraduatedScale("bad", 0, 1, 1, 1, 1));
        }

        // ── Volume from the amounts ───────────────────────────────────

        private static ReagentAmountInput A(string f, double v, string unit, string state, double stock = 0, double density = 0)
            => new ReagentAmountInput { Formula = f, Value = v, Unit = unit, State = state, StockConcentrationM = stock, DensityGPerMl = density };

        [Test]
        public void Volume_OnlyWhereTheDataGivesIt()
        {
            Assert.That(VesselVolume.Of(A("HCl", 25, "mL", "aqueous", 1.0)).Ml, Is.EqualTo(25));
            Assert.That(VesselVolume.Of(A("HCl", 0.02, "mol", "aqueous", 1.0)).Ml, Is.EqualTo(20).Within(1e-9));   // V = n / c
            Assert.That(VesselVolume.Of(A("H2O", 9.97, "g", "liquid", 0, 0.997)).Ml, Is.EqualTo(10).Within(1e-9));  // V = m / ρ
            Assert.That(VesselVolume.Of(A("Mg", 0.5, "g", "solid")).Ml, Is.EqualTo(0));       // solids add no liquid
            Assert.That(VesselVolume.Of(A("O2", 0.05, "mol", "gas")).Ml, Is.EqualTo(0));

            // No concentration / no density / grams of a solution / bad numbers → unknown, never a guess.
            Assert.That(VesselVolume.Of(A("H2CO3", 0.02, "mol", "aqueous")).Known, Is.False);
            Assert.That(VesselVolume.Of(A("C2H5OH", 5, "g", "liquid")).Known, Is.False);
            Assert.That(VesselVolume.Of(A("HCl", 5, "g", "aqueous", 1.0)).Known, Is.False);
            Assert.That(VesselVolume.Of(A("HCl", double.NaN, "mL", "aqueous", 1.0)).Known, Is.False);
            Assert.That(VesselVolume.Of(A("HCl", -3, "mL", "aqueous", 1.0)).Known, Is.False);
            Assert.That(VesselVolume.Of(A("X", 5, "mL", "")).Known, Is.False);
        }

        [Test]
        public void MixtureVolume_IsAdditive_AndUnknownIfAnyPartIs()
        {
            var t = VesselVolume.Total(new[] { A("HCl", 20, "mL", "aqueous", 1.0), A("NaOH", 30, "mL", "aqueous", 1.0) });
            Assert.That(t.Known, Is.True);
            Assert.That(t.Ml, Is.EqualTo(50));
            StringAssert.Contains("additive", t.Basis);

            var withSolid = VesselVolume.Total(new[] { A("Mg", 0.5, "g", "solid"), A("HCl", 10, "mL", "aqueous", 1.0) });
            Assert.That(withSolid.Ml, Is.EqualTo(10));
            StringAssert.Contains("not counted", withSolid.Basis);   // the solid's own volume is not modelled

            var unknown = VesselVolume.Total(new[] { A("HCl", 20, "mL", "aqueous", 1.0), A("H2CO3", 0.01, "mol", "aqueous") });
            Assert.That(unknown.Known, Is.False);
            Assert.That(VesselVolume.Total(null).Known, Is.False);
        }

        [Test]
        public void Volume_FromTheSameAmountInputTheEngineGets()
        {
            // The bench builds its volume from LabInputController.AmountInputFor, like the mix request.
            var mat = new ChemicalMaterial { formula = "HCl", state = "aqueous", stockConcentrationM = 1.0f, stockLabel = "1.0 M bench stock" };
            var input = LabInputController.AmountInputFor("HCl", mat, 25, "mL");
            Assert.That(VesselVolume.Of(input).Ml, Is.EqualTo(25));
            var moles = LabInputController.AmountInputFor("HCl", mat, 0.025, "mol");
            Assert.That(VesselVolume.Of(moles).Ml, Is.EqualTo(25).Within(1e-9));
        }
    }
}
