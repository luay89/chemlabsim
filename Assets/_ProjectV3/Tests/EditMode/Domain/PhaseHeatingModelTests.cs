using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using ChemLabSimV3.Engine.Chemistry;

namespace ChemLabSimV3.Tests.EditMode.Domain
{
    /// <summary>Heating lab: sensible heat, latent heat, cooling, missing data and stability.</summary>
    public class PhaseHeatingModelTests
    {
        private static ChemicalMaterial Water() => new ChemicalMaterial
        {
            formula = "H2O", state = "liquid", densityGPerMl = 0.997f,
            thermal = new MaterialThermalData
            {
                available = true, referencePressureKPa = 101.325f, meltingPointC = 0f, boilingPointC = 99.97f,
                heatCapacityLiquidJPerGK = 4.19f, enthalpyFusionJPerG = 333.6f, enthalpyVaporizationJPerG = 2256.5f,
                solidDensityGPerMl = 0.9167f
            }
        };

        private static HeatingSample Sample(double grams, double startC = 25, bool frozen = false)
        {
            var s = PhaseHeatingModel.Create(Water(), grams, startC, frozen, out string why);
            Assert.That(s, Is.Not.Null, why);
            return s;
        }

        // ── Sensible heat ─────────────────────────────────────────────

        [Test]
        public void Warming_FollowsQ_EqualsMcDeltaT()
        {
            var s = Sample(100);
            PhaseHeatingModel.Step(s, 50, 300, heatLossWPerK: 0);
            // ΔT = 300 W × 50 s / (100 g × 4.19 J/(g·K)) = 35.80 K
            Assert.That(s.TemperatureC, Is.EqualTo(25 + 15000.0 / 419.0).Within(1e-3));
            Assert.That(s.Phase, Is.EqualTo(SamplePhase.Liquid));
            Assert.That(s.VaporizedG, Is.EqualTo(0));   // no evaporation is modelled below the boiling point
            Assert.That(s.LiquidG, Is.EqualTo(100));
        }

        [Test]
        public void ReachesBoilingPoint_AtTheRightEnergy_ThenTemperatureStaysConstant()
        {
            var s = Sample(100);
            double toBoil = 100 * 4.19 * (99.97 - 25);                       // 31 412 J
            PhaseHeatingModel.Step(s, toBoil / 300 - 0.5, 300, heatLossWPerK: 0);
            Assert.That(s.Phase, Is.EqualTo(SamplePhase.Liquid));
            Assert.That(s.TemperatureC, Is.LessThan(99.97));
            PhaseHeatingModel.Step(s, 10.5, 300, heatLossWPerK: 0);
            Assert.That(s.Phase, Is.EqualTo(SamplePhase.Boiling));
            Assert.That(s.TemperatureC, Is.EqualTo(99.97).Within(1e-4));
            // 10 s past the boiling point: 3000 J / 2256.5 J/g of water vaporised.
            Assert.That(s.VaporizedG, Is.EqualTo(3000 / 2256.5).Within(2e-3));
            Assert.That(s.LiquidG + s.VaporizedG, Is.EqualTo(100).Within(1e-6));   // mass conserved
        }

        [Test]
        public void BoilsAway_ThenStops_Empty()
        {
            var s = Sample(10);
            PhaseHeatingModel.Step(s, 1e5, 500, heatLossWPerK: 0);
            Assert.That(s.Phase, Is.EqualTo(SamplePhase.Empty));
            Assert.That(s.LiquidG, Is.EqualTo(0));
            Assert.That(s.VaporizedG, Is.EqualTo(10).Within(1e-6));
            Assert.That(double.IsNaN(s.TemperatureC), Is.False);
            Assert.That(s.TemperatureC, Is.EqualTo(99.97).Within(1e-4));   // never heated past the boiling point
        }

        [Test]
        public void EnergyIsAccounted_SensiblePlusLatent()
        {
            var s = Sample(50);
            PhaseHeatingModel.Step(s, 300, 400, heatLossWPerK: 0.2);   // stays short of boiling dry
            double stored = 50 * 4.19 * (s.TemperatureC - 25) + s.VaporizedG * 2256.5;
            Assert.That(s.HeatInJ - s.HeatLostJ, Is.EqualTo(stored).Within(1.0));
        }

        // ── Cooling ───────────────────────────────────────────────────

        [Test]
        public void HeaterOff_BoilingStops_CoolsTowardsRoomNotBelow()
        {
            var s = Sample(100);
            PhaseHeatingModel.Step(s, 200, 400, heatLossWPerK: 0);
            Assert.That(s.Phase, Is.EqualTo(SamplePhase.Boiling));
            double liquid = s.LiquidG;
            PhaseHeatingModel.Step(s, 1, 0, heatLossWPerK: 0.2);
            Assert.That(s.Phase, Is.EqualTo(SamplePhase.Liquid));
            Assert.That(s.TemperatureC, Is.LessThan(99.97));
            Assert.That(s.LiquidG, Is.EqualTo(liquid));                       // no more vapour once it stops boiling
            PhaseHeatingModel.Step(s, 1e6, 0, heatLossWPerK: 0.2);
            Assert.That(s.TemperatureC, Is.GreaterThanOrEqualTo(25 - 1e-6));
            Assert.That(s.TemperatureC, Is.EqualTo(25).Within(0.01));
        }

        // Regression: a few milligrams left after nearly boiling dry, then heater off. The explicit
        // cooling step used to overshoot and diverge (−12 million °C for 1 mg).
        [TestCase(0.001)]
        [TestCase(0.002)]
        [TestCase(1e-6)]
        public void TinyMass_CoolsToRoom_WithoutOvershoot(double grams)
        {
            var s = Sample(10, startC: 60);
            s.LiquidG = grams;
            double last = s.TemperatureC;
            for (int i = 0; i < 400; i++)
            {
                PhaseHeatingModel.Step(s, 0.05, 0, heatLossWPerK: 0.2);
                Assert.That(double.IsNaN(s.TemperatureC) || double.IsInfinity(s.TemperatureC), Is.False);
                Assert.That(s.TemperatureC, Is.LessThanOrEqualTo(last + 1e-9));     // only cools
                Assert.That(s.TemperatureC, Is.GreaterThanOrEqualTo(25 - 1e-9));   // never below the room
                last = s.TemperatureC;
            }
            Assert.That(s.TemperatureC, Is.EqualTo(25).Within(1e-6));
        }

        [Test]
        public void NearlyBoiledDry_ThenHeaterOff_StaysFiniteAndAccounted()
        {
            var s = Sample(5);
            while (s.Phase != SamplePhase.Empty && !(s.Phase == SamplePhase.Boiling && s.LiquidG < 0.004))
                PhaseHeatingModel.Step(s, 0.05, 300, heatLossWPerK: 0.2);
            Assume.That(s.Phase, Is.EqualTo(SamplePhase.Boiling));
            PhaseHeatingModel.Step(s, 60, 0, heatLossWPerK: 0.2);
            Assert.That(s.TemperatureC, Is.InRange(25.0, 99.97));
            // Everything started at 25 °C: what is left was warmed to T; what boiled off was warmed to
            // the boiling point and vaporised.
            double stored = s.LiquidG * 4.19 * (s.TemperatureC - 25)
                          + s.VaporizedG * (4.19 * (99.97 - 25) + 2256.5);
            Assert.That(s.HeatInJ - s.HeatLostJ, Is.EqualTo(stored).Within(2.0));
        }

        [Test]
        public void HeaterOff_BelowRoom_WarmsTowardsRoom_AndKeepsStepping()
        {
            var s = Sample(20, frozen: true);
            Assert.That(PhaseHeatingModel.NeedsStepping(s, heaterOn: false), Is.True);    // ice melts from the room's heat
            // 20 g of ice needs 6672 J; the room gives k·(25 − 0) = 5 W, so it has melted after ≈ 1334 s.
            PhaseHeatingModel.Step(s, 1400, 0, heatLossWPerK: 0.2);
            Assert.That(s.SolidG, Is.EqualTo(0));
            Assert.That(s.TemperatureC, Is.InRange(0.5, 10.0));                        // the melt water is still cold
            Assert.That(PhaseHeatingModel.NeedsStepping(s, heaterOn: false), Is.True);
            StringAssert.Contains("warms towards room temperature", PhaseHeatingModel.Explain(s, heaterOn: false));
            PhaseHeatingModel.Step(s, 1e5, 0, heatLossWPerK: 0.2);
            Assert.That(s.TemperatureC, Is.EqualTo(25).Within(0.01));
            Assert.That(PhaseHeatingModel.NeedsStepping(s, heaterOn: false), Is.False);  // nothing left to change
            Assert.That(PhaseHeatingModel.NeedsStepping(s, heaterOn: true), Is.True);
        }

        // ── Melting ───────────────────────────────────────────────────

        [Test]
        public void Ice_MeltsAtZero_WithLatentHeat()
        {
            var s = Sample(50, frozen: true);
            Assert.That(s.Phase, Is.EqualTo(SamplePhase.Solid));
            Assert.That(s.SolidMl, Is.EqualTo(50 / 0.9167).Within(1e-3));      // ice takes more room than water
            PhaseHeatingModel.Step(s, 100, 100, heatLossWPerK: 0);
            Assert.That(s.Phase, Is.EqualTo(SamplePhase.Melting));
            Assert.That(s.TemperatureC, Is.EqualTo(0));
            Assert.That(s.SolidG, Is.EqualTo(50 - 10000 / 333.6).Within(1e-3));
            PhaseHeatingModel.Step(s, 100, 100, heatLossWPerK: 0);
            Assert.That(s.SolidG, Is.EqualTo(0));
            Assert.That(s.TemperatureC, Is.GreaterThan(0));                  // only after all the ice has melted
        }

        // ── Missing data and invalid input ────────────────────────────

        [Test]
        public void NoThermalData_IsUnsupported_WithReason()
        {
            var methanol = new ChemicalMaterial { formula = "CH3OH", state = "liquid", densityGPerMl = 0.791f };
            Assert.That(PhaseHeatingModel.IsSupported(methanol, out string why), Is.False);
            StringAssert.Contains("no sourced", why);
            Assert.That(PhaseHeatingModel.Create(methanol, 10, 25, false, out _), Is.Null);
            Assert.That(PhaseHeatingModel.IsSupported(null, out _), Is.False);

            var noFusion = Water();
            noFusion.thermal.enthalpyFusionJPerG = 0;
            Assert.That(PhaseHeatingModel.CanStartFrozen(noFusion), Is.False);
            Assert.That(PhaseHeatingModel.Create(noFusion, 10, 25, true, out _), Is.Null);
        }

        [TestCase(0.0, 25.0)]
        [TestCase(-5.0, 25.0)]
        [TestCase(double.NaN, 25.0)]
        [TestCase(10.0, double.NaN)]
        [TestCase(10.0, 120.0)]    // would be steam at 101.325 kPa
        public void InvalidStart_IsRefused(double grams, double startC)
        {
            Assert.That(PhaseHeatingModel.Create(Water(), grams, startC, false, out string why), Is.Null);
            Assert.That(why, Is.Not.Empty);
        }

        [Test]
        public void Step_IgnoresInvalidTime_AndNegativePower()
        {
            var s = Sample(10);
            PhaseHeatingModel.Step(s, double.NaN, 300);
            PhaseHeatingModel.Step(s, -1, 300);
            PhaseHeatingModel.Step(s, double.PositiveInfinity, 300);
            Assert.That(s.TemperatureC, Is.EqualTo(25));
            PhaseHeatingModel.Step(s, 10, -50, heatLossWPerK: 0);              // treated as heater off
            Assert.That(s.TemperatureC, Is.EqualTo(25));
        }

        [Test]
        public void ExplanationNamesPhysicalChange_AndTheReferencePressure()
        {
            var s = Sample(100);
            PhaseHeatingModel.Step(s, 200, 400, heatLossWPerK: 0);
            string text = PhaseHeatingModel.Explain(s, heaterOn: true);
            StringAssert.Contains("101.325 kPa", text);
            StringAssert.Contains("Physical change", text);
            StringAssert.Contains("latent heat", text);
            StringAssert.Contains("evaporation", PhaseHeatingModel.Explain(Sample(100), heaterOn: true));
        }

        // ── The shipped data ──────────────────────────────────────────

        [System.Serializable]
        private class MaterialList { public System.Collections.Generic.List<ChemicalMaterial> items; }

        [Test]
        public void ShippedThermalData_OnlyForCheckedSubstances()
        {
            string json = File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "_Project", "DataSrc", "materials.json"));
            var mats = JsonUtility.FromJson<MaterialList>("{\"items\":" + json + "}").items;
            var withData = mats.Where(m => m.thermal != null && m.thermal.available).Select(m => m.formula).OrderBy(f => f).ToArray();
            Assert.That(withData, Is.EqualTo(new[] { "C2H5OH", "H2O" }));
            var water = mats.First(m => m.formula == "H2O");
            Assert.That(water.thermal.boilingPointC, Is.EqualTo(99.97f).Within(1e-4));
            Assert.That(water.thermal.enthalpyVaporizationJPerG, Is.EqualTo(2256.5f).Within(1e-3));
            Assert.That(water.thermal.referencePressureKPa, Is.EqualTo(101.325f).Within(1e-4));
            Assert.That(water.thermal.source, Does.Contain("NIST"));
            Assert.That(PhaseHeatingModel.IsSupported(mats.First(m => m.formula == "CH3OH"), out _), Is.False);
        }
    }
}
