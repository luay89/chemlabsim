using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using ChemLabSimV3.Engine.Assistant;
using ChemLabSimV3.Engine.Chemistry;

namespace ChemLabSimV3.Tests.EditMode.Domain
{
    /// <summary>
    /// Standard reaction enthalpies in the shipped reactions.json (tools/reactions/enthalpy_005.py):
    /// every value is either sourced or says what is missing, known reactions match textbook values,
    /// and the model vessel keeps using the observed ΔT.
    /// </summary>
    public class ReactionEnthalpyTests
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
        public void EveryReaction_IsSourcedOrSaysWhatIsMissing()
        {
            foreach (var r in db.reactions)
            {
                if (Calorimetry.HasSourcedEnthalpy(r))
                {
                    Assert.That(string.IsNullOrEmpty(r.enthalpyMissing), r.id);
                    Assert.That(float.IsNaN(r.enthalpyKJPerMol) || float.IsInfinity(r.enthalpyKJPerMol), Is.False, r.id);
                    StringAssert.StartsWith("Hess's law", r.enthalpySource, r.id);
                }
                else
                {
                    Assert.That(r.enthalpyKJPerMol, Is.EqualTo(0f), r.id);
                    StringAssert.StartsWith("no sourced standard enthalpy of formation for ", r.enthalpyMissing, r.id);
                }
            }
            // 394 of 422 when this was written; a drop means a source file or species map was lost.
            Assert.That(db.reactions.Count(Calorimetry.HasSourcedEnthalpy), Is.GreaterThanOrEqualTo(390));
        }

        [TestCase("rxn_001", -55.82, 0.01)]    // HCl + NaOH: H⁺ + OH⁻ → H₂O, −55.8 (CODATA)
        [TestCase("rxn_139", -44.5, 0.5)]      // NaOH(s) dissolving, −44.5
        [TestCase("rxn_040", 25.7, 0.5)]       // NH₄NO₃(s) dissolving, +25.7
        [TestCase("rxn_006", -65.72, 0.01)]    // Ag⁺ + Cl⁻ → AgCl(s)
        [TestCase("rxn_004", -1203.2, 0.5)]    // 2 Mg + O₂ → 2 MgO
        [TestCase("rxn_308", -483.65, 0.1)]    // 2 H₂ + O₂ → 2 H₂O(g)
        [TestCase("rxn_031", -1234.8, 1.0)]    // C₂H₅OH(l) + 3 O₂ → 2 CO₂ + 3 H₂O(g)
        public void KnownReactions_MatchTextbookValues(string id, double expected, double tolerance)
        {
            var r = Get(id);
            Assert.That(Calorimetry.HasSourcedEnthalpy(r), Is.True, r.enthalpyMissing);
            Assert.That(r.enthalpyKJPerMol, Is.EqualTo(expected).Within(tolerance));
        }

        [Test]
        public void Neutralisation_AgreesWithTheCalorimetryConstant()
        {
            // Two routes to the same number: Hess's law over the stored species, and ΔH_neut.
            Assert.That(-Get("rxn_001").enthalpyKJPerMol * 1000.0, Is.EqualTo(Calorimetry.NeutralisationJPerMol).Within(10.0));
        }

        [Test]
        public void ModelVessel_UsesTheObservedTemperatureChange()
        {
            var r = Get("rxn_001");
            Assert.That(Thermometry.ModelVesselEnthalpyKJ(r), Is.EqualTo(-r.visual_effects.temperature_delta * 4.184f).Within(1e-4));
            Assert.That(Thermometry.BasisFor(r), Is.EqualTo(TemperatureBasis.Estimated));
            Assert.That(Thermometry.ModelVesselEnthalpyKJ(new ReactionEntry { enthalpyKJPerMol = -55.8f }), Is.EqualTo(-55.8f));
            Assert.That(Thermometry.ModelVesselEnthalpyKJ(null), Is.EqualTo(0f));
        }

        [Test]
        public void EndothermicRoomTemperatureReactions_NoLongerShowAWarming()
        {
            // Vinegar + baking soda takes in heat (ΔH° = +30.9 kJ/mol); its stored ΔT was +2.
            var r = Get("rxn_002");
            Assert.That(r.enthalpyKJPerMol, Is.GreaterThan(0f));
            Assert.That(r.visual_effects.temperature_delta, Is.LessThan(0f));
        }

        [Test]
        public void Refusals_SayWhy()
        {
            var heated = db.reactions.First(r => r.requiresHeating && Calorimetry.HasSourcedEnthalpy(r));
            var electrolysis = Get("rxn_123");
            Assert.That(Calorimetry.TryReaction(heated, null, out _, out string why), Is.False);
            StringAssert.StartsWith("the reaction is heated", why);
            Assert.That(Calorimetry.HasSourcedEnthalpy(electrolysis), Is.True);
            Assert.That(Calorimetry.TryReaction(electrolysis, null, out _, out why), Is.False);
            StringAssert.StartsWith("electrolysis", why);
            Assert.That(Calorimetry.TryReaction(Get("rxn_001"), null, out _, out why), Is.False);
            Assert.That(why, Is.EqualTo("the amounts are not complete"));
            Assert.That(Calorimetry.StaysLiquid(25, 80, out string boil), Is.False);
            StringAssert.Contains("boil", boil);
            Assert.That(Calorimetry.StaysLiquid(25, -30, out string freeze), Is.False);
            StringAssert.Contains("freeze", freeze);
            Assert.That(Calorimetry.StaysLiquid(25, 6.7, out _), Is.True);
        }

        [Test]
        public void HeatReleased_HasTheOppositeSignOfDeltaH()
        {
            var r = new ReactionEntry { enthalpyKJPerMol = -55.82f };
            Assert.That(Calorimetry.HeatReleasedKJ(r, 0.02), Is.EqualTo(1.1164).Within(1e-4));
            r.enthalpyKJPerMol = 25.45f;
            Assert.That(Calorimetry.HeatReleasedKJ(r, 0.0625), Is.EqualTo(-1.5906).Within(1e-3));
        }

        [Test]
        public void Assistant_QuotesTheSourcedDeltaH()
        {
            var r = Get("rxn_001");
            var ctx = new LabContext
            {
                HasExperiment = true,
                Found = true,
                Reagents = new[] { "HCl", "NaOH" },
                Input = new ReactionEvaluationInput { reaction = r },
                Result = new ReactionEvaluationResult { IsValid = true, Status = ReactionStatus.Success },
            };
            string what = new LocalLabAssistant().Answer(AssistantTopic.WhatHappened, ctx);
            StringAssert.Contains("ΔH° = −55.8 kJ per mol of reaction", what);
        }
    }
}
