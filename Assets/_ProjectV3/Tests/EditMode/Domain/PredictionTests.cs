using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using ChemLabSimV3.Engine.Assistant;

namespace ChemLabSimV3.Tests.EditMode.Domain
{
    /// <summary>"Predict first": the outcome each answer is compared with, on the shipped reactions.</summary>
    public class PredictionTests
    {
        private static ReactionDB db;

        [OneTimeSetUp]
        public void LoadShippedData()
        {
            string path = Path.Combine(UnityEngine.Application.dataPath, "_Project", "DataSrc", "reactions.json");
            db = JsonUtility.FromJson<ReactionDB>(File.ReadAllText(path));
        }

        private static ReactionEntry Get(string id) => db.reactions.First(r => r.id == id);

        [Test]
        public void Neutralisation_WarmsUp_NoSolidNoGas()
        {
            var o = Prediction.Outcome(Get("rxn_001"), true, 6.67);
            Assert.That(o.Heat, Is.EqualTo(HeatAnswer.GivesOut));
            StringAssert.Contains("+6.7 °C", o.HeatWhy);
            Assert.That(o.Solid, Is.False);
            Assert.That(o.Gas, Is.False);
            var right = new PredictionAnswers { Heat = HeatAnswer.GivesOut, Solid = false, Gas = false };
            Assert.That(Prediction.Score(right, o), Is.EqualTo(3));
        }

        [Test]
        public void ColdPack_CoolsDown_FromTheCalculatedChange()
        {
            Assert.That(Prediction.Outcome(Get("rxn_040"), true, -15.2).Heat, Is.EqualTo(HeatAnswer.TakesIn));
            Assert.That(Prediction.Outcome(Get("rxn_040"), true, -0.4).Heat, Is.EqualTo(HeatAnswer.HardlyAny));
        }

        [Test]
        public void Precipitate_IsASolid_ZincAndAcid_GiveAGas()
        {
            var agcl = Prediction.Outcome(Get("rxn_006"), true, double.NaN);
            Assert.That(agcl.Solid, Is.True);
            StringAssert.Contains("AgCl", agcl.SolidWhy);
            var zinc = Prediction.Outcome(Get("rxn_005"), true, double.NaN);
            Assert.That(zinc.Gas, Is.True);
            StringAssert.Contains("H2", zinc.GasWhy);
        }

        [Test]
        public void WithoutACalculation_TheSignOfDeltaHDecides()
        {
            var burn = Prediction.Outcome(Get("rxn_004"), true, double.NaN);   // 2 Mg + O2, −1203 kJ
            Assert.That(burn.Heat, Is.EqualTo(HeatAnswer.GivesOut));
            StringAssert.Contains("ΔH° = −1203", burn.HeatWhy);
            Assert.That(burn.Solid, Is.True);
            var soda = Prediction.Outcome(Get("rxn_028"), true, double.NaN);   // NaHCO3 decomposition, +135.6
            Assert.That(soda.Heat, Is.EqualTo(HeatAnswer.TakesIn));
            Assert.That(soda.Gas && soda.Solid, Is.True);
        }

        [Test]
        public void NoReaction_HardlyAnyHeat_NoSolidNoGas()
        {
            var none = Prediction.Outcome(null, false, double.NaN);
            Assert.That(none.Reacted, Is.False);
            Assert.That(none.Heat, Is.EqualTo(HeatAnswer.HardlyAny));
            Assert.That(none.Solid || none.Gas, Is.False);
            StringAssert.Contains("do not react", none.HeatWhy);
            var cold = Prediction.Outcome(Get("rxn_004"), false, double.NaN);   // Mg + O2 at 25 °C
            StringAssert.Contains("did not start", cold.GasWhy);
            var guess = new PredictionAnswers { Heat = HeatAnswer.GivesOut, Solid = true, Gas = false };
            Assert.That(Prediction.Score(guess, cold), Is.EqualTo(1));
            Assert.That(guess.Complete, Is.True);
            Assert.That(new PredictionAnswers { Heat = HeatAnswer.None }.Complete, Is.False);
        }
    }
}
