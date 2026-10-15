using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using ChemLabSimV3.Controllers;
using ChemLabSimV3.Data;
using ChemLabSimV3.Engine;
using ChemLabSimV3.Engine.Chemistry;
using ChemLabSimV3.Events;
using ChemLabSimV3.Views.Bench3D;
using ChemLabSimV3.Views.Layout;

namespace ChemLabSimV3.Tests.EditMode.Domain
{
    /// <summary>
    /// Stage 5 slice F: the whole quantity path on the shipped data.
    /// materials.json → amount input (LabInputController.AmountInputFor) → MixRequest →
    /// ReactionEngine.ProcessDetailed (reactions.json) → the event ReactionController publishes →
    /// what the Result panel, the pH meter and the reagent label show.
    /// </summary>
    public class QuantityPathIntegrationTests
    {
        [Serializable] private class MaterialList { public List<ChemicalMaterial> items; }

        private static ReactionDB db;
        private static Dictionary<string, ChemicalMaterial> materials;

        [OneTimeSetUp]
        public void LoadShippedData()
        {
            string dir = Path.Combine(UnityEngine.Application.dataPath, "_Project", "DataSrc");
            db = JsonUtility.FromJson<ReactionDB>(File.ReadAllText(Path.Combine(dir, "reactions.json")));
            string mats = File.ReadAllText(Path.Combine(dir, "materials.json"));
            materials = JsonUtility.FromJson<MaterialList>("{\"items\":" + mats + "}").items
                .Where(m => !string.IsNullOrEmpty(m.formula))
                .GroupBy(m => m.formula).ToDictionary(g => g.Key, g => g.First());
            Assert.That(db.reactions.Count, Is.GreaterThan(400));
        }

        // ── The path, as the app runs it ──────────────────────────────

        private struct Outcome
        {
            public ReactionEngineResult Detailed;
            public StoichiometryEvaluatedEvent Event;
            public string Panel;
            public PhResult Ph;
            public string PhValue, PhCaption;
        }

        private static ReagentAmountInput Amount(string formula, double value, string unit)
        {
            materials.TryGetValue(formula, out var mat);
            return LabInputController.AmountInputFor(formula, mat, value, unit);
        }

        private static Outcome Mix(float temperatureC, params ReagentAmountInput[] amounts)
        {
            var request = new MixRequest(amounts.Select(a => a.Formula).ToList(), ReactionMedium.Neutral,
                                         temperatureC, 0.6f, 0.5f, false)
            { Amounts = amounts.ToList() };
            var detailed = new ReactionEngine(db).ProcessDetailed(request);
            Assert.That(detailed.Found, Is.True, "reaction not found for " + string.Join(" + ", request.ReagentNames));
            var e = ReactionController.StoichiometryEventFor(detailed, ReactionEvaluationAdapter.ToLegacyResult(detailed));
            var o = new Outcome { Detailed = detailed, Event = e, Ph = LabInstruments.PhAfterMix(e) };
            o.Panel = e.Evaluation != null ? StoichiometryPanelView.Describe(e.Evaluation, e.Proceeded) : null;
            LabInstruments.PhDisplay(o.Ph, out o.PhValue, out o.PhCaption);
            return o;
        }

        private static double Grams(string formula, double mol) => mol * MolarMass.Of(formula).GramsPerMole;

        // ── A. 20 mL 1.0 M HCl + 30 mL 1.0 M NaOH ─────────────────────

        [Test]
        public void A_Neutralisation_HClLimiting_EndToEnd()
        {
            var o = Mix(25f, Amount("HCl", 20, "mL"), Amount("NaOH", 30, "mL"));
            Assert.That(o.Detailed.Reaction.id, Is.EqualTo("rxn_001"));
            Assert.That(o.Event.Proceeded && o.Event.Complete, Is.True);

            var s = o.Event.Evaluation.Stoichiometry;
            Assert.That(s.Success, Is.True, s.Message);
            Assert.That(s.LimitingFormulas, Is.EqualTo(new[] { "HCl" }));
            Assert.That(s.Reactants.First(r => r.Formula == "NaOH").LeftOverMol, Is.EqualTo(0.010).Within(1e-9));
            var nacl = s.Products.First(p => p.Formula == "NaCl");
            Assert.That(nacl.TheoreticalMol, Is.EqualTo(0.020).Within(1e-4).Percent);
            // CIAAW 2021 abridged (the project's convention): M(NaCl) = 22.990 + 35.45 = 58.44 g/mol → 1.1688 g.
            // Amounts pass through float32 in Stoichiometry.Solve, hence the relative tolerance.
            Assert.That(nacl.TheoreticalGrams, Is.EqualTo(0.020 * 58.44).Within(1e-4).Percent);

            // The panel shows the engine's numbers.
            StringAssert.Contains("= 20 mmol  <color=#FF9F6B>limiting</color>", o.Panel);
            StringAssert.Contains("excess, 10 mmol left", o.Panel);
            StringAssert.Contains("Limiting reactant: <b>HCl</b>", o.Panel);
            StringAssert.Contains("= 1.169 g", o.Panel);
            StringAssert.DoesNotContain("did not proceed", o.Panel);

            // pH meter: 10 mmol OH− in 50 mL → 0.2 M → pH 13.30, flagged approximate (> 0.1 M).
            Assert.That(o.Ph.IsCalculated, Is.True, o.Ph.Basis);
            Assert.That(o.Ph.Ph, Is.EqualTo(14.0 + Math.Log10(0.2)).Within(1e-6));
            Assert.That(o.PhValue, Is.EqualTo("13.30"));
            Assert.That(o.PhCaption, Is.EqualTo("calculated (approx., concentrated)"));

            // The reagent label under the beaker shows the bottle the amount was measured from.
            Assert.That(LabBench3D.LabelCaption(materials["HCl"], true, 20, "mL"), Is.EqualTo("20 mL  ·  1.0 M bench stock"));
        }

        // ── B. Exact proportions ──────────────────────────────────────

        [Test]
        public void B_ExactProportions_NoExcess_Neutral()
        {
            var o = Mix(25f, Amount("HCl", 25, "mL"), Amount("NaOH", 25, "mL"));
            var s = o.Event.Evaluation.Stoichiometry;
            Assert.That(s.IsStoichiometric, Is.True);
            Assert.That(s.LimitingFormulas, Is.Empty);
            Assert.That(s.Reactants.All(r => r.Role == ReactantRole.ExactlyUsed && Math.Abs(r.LeftOverMol) < 1e-12), Is.True);
            StringAssert.Contains("Exact proportions", o.Panel);
            Assert.That(o.PhValue, Is.EqualTo("7.00"));
        }

        // ── C / D. Either reactant can limit, by the actual inputs ────

        [Test]
        public void C_ReagentALimits_D_ReagentBLimits()
        {
            var c = Mix(25f, Amount("HCl", 10, "mL"), Amount("NaOH", 40, "mL"));
            Assert.That(c.Event.Evaluation.Stoichiometry.LimitingFormulas, Is.EqualTo(new[] { "HCl" }));
            Assert.That(c.Event.Evaluation.Stoichiometry.Products.First(p => p.Formula == "NaCl").TheoreticalMol,
                        Is.EqualTo(0.010).Within(1e-4).Percent);

            var d = Mix(25f, Amount("HCl", 30, "mL"), Amount("NaOH", 10, "mL"));
            Assert.That(d.Event.Evaluation.Stoichiometry.LimitingFormulas, Is.EqualTo(new[] { "NaOH" }));
            StringAssert.Contains("Limiting reactant: <b>NaOH</b>", d.Panel);
            // 20 mmol H+ left in 40 mL → 0.5 M → pH 0.30
            Assert.That(d.Ph.Ph, Is.EqualTo(-Math.Log10(0.5)).Within(1e-6));
            Assert.That(d.PhValue, Is.EqualTo("0.30"));
        }

        // ── E. Missing concentration ──────────────────────────────────

        [Test]
        public void E_MissingConcentration_NoNumbersInvented()
        {
            // A shipped aqueous material without a bottle concentration.
            Assert.That(materials["H2CO3"].stockConcentrationM, Is.EqualTo(0f));
            var h2co3 = Amount("H2CO3", 25, "mL");
            var direct = AmountEvaluator.Evaluate(db.reactions.First(r => r.id == "rxn_001"), new[] { h2co3 });
            Assert.That(direct.Lines[0].Moles.Status, Is.EqualTo(ConversionStatus.MissingConcentration));

            // The same through the engine: a NaOH bottle without a label.
            var naoh = Amount("NaOH", 30, "mL");
            naoh.StockConcentrationM = 0;
            var o = Mix(25f, Amount("HCl", 20, "mL"), naoh);
            Assert.That(o.Event.Evaluation.Success, Is.False);
            Assert.That(o.Event.Evaluation.Lines[1].Moles.Status, Is.EqualTo(ConversionStatus.MissingConcentration));
            Assert.That(o.Event.Evaluation.Stoichiometry.Products, Is.Empty);
            StringAssert.DoesNotContain("Limiting reactant", o.Panel);
            StringAssert.DoesNotContain("Theoretical yield", o.Panel);
            Assert.That(o.Ph.IsCalculated, Is.False);
            Assert.That(o.PhValue, Is.EqualTo("--"));

            // An unknown material carries no data at all, never defaults.
            var unknown = LabInputController.AmountInputFor("XYZ", null, 10, "mL");
            Assert.That(unknown.StockConcentrationM, Is.EqualTo(0));
            Assert.That(unknown.DensityGPerMl, Is.EqualTo(0));
            Assert.That(unknown.State, Is.Empty);
        }

        // ── F. Invalid or unsupported units ───────────────────────────

        [TestCase("g", ConversionStatus.UnsupportedUnit)]    // a solution is measured by volume
        [TestCase("kg", ConversionStatus.UnsupportedUnit)]   // not a unit of the app
        [TestCase("", ConversionStatus.UnsupportedUnit)]
        public void F_UnsupportedUnit_StopsWithReason(string unit, ConversionStatus expected)
        {
            var o = Mix(25f, Amount("HCl", 20, "mL"), Amount("NaOH", 30, unit));
            Assert.That(o.Event.Evaluation.Lines[1].Moles.Status, Is.EqualTo(expected));
            Assert.That(o.Event.Evaluation.Success, Is.False);
            StringAssert.DoesNotContain("Theoretical yield", o.Panel);
            Assert.That(o.Ph.IsCalculated, Is.False);
        }

        // ── G. Negative, zero, NaN and infinite amounts ───────────────

        [TestCase(-5.0)]
        [TestCase(0.0)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void G_InvalidAmount_StopsWithReason(double value)
        {
            var o = Mix(25f, Amount("HCl", value, "mL"), Amount("NaOH", 30, "mL"));
            Assert.That(o.Event.Evaluation.Lines[0].Moles.Status, Is.EqualTo(ConversionStatus.InvalidQuantity));
            Assert.That(o.Event.Evaluation.Success, Is.False);
            Assert.That(o.Event.Evaluation.Stoichiometry.LimitingFormulas, Is.Empty);
            StringAssert.DoesNotContain("Theoretical yield", o.Panel);
            Assert.That(o.PhValue, Is.EqualTo("--"));
        }

        // ── H. Yield or pH that cannot be justified ───────────────────

        [Test]
        public void H_ReactionThatDidNotProceed_ShowsNoPhAndSaysSo()
        {
            // Mg + O2 needs ignition; at 25 °C the engine does not run it.
            var o = Mix(25f, Amount("Mg", 0.5, "g"), Amount("O2", 0.05, "mol"));
            Assert.That(o.Event.Proceeded, Is.False);
            StringAssert.StartsWith("<color=#FFBD47>The reaction did not proceed", o.Panel);
            Assert.That(o.Ph.IsCalculated, Is.False);
            StringAssert.Contains("did not proceed", o.Ph.Basis);
        }

        [Test]
        public void H_PrecipitationPh_IsUnsupportedWithReason()
        {
            var o = Mix(25f, Amount("AgNO3", 25, "mL"), Amount("NaCl", 25, "mL"));
            Assert.That(o.Ph.IsCalculated, Is.False);
            Assert.That(o.PhValue, Is.EqualTo("--"));
            StringAssert.StartsWith("not supported: AgNO3 is not a strong acid or base", o.PhCaption);
        }

        [Test]
        public void H_EveryShippedReaction_GivesNumbersOnlyWhenBalanced()
        {
            int balanced = 0, refused = 0;
            foreach (var r in db.reactions)
            {
                var moles = r.reactants.GroupBy(c => c.formula).ToDictionary(g => g.Key, g => 1.0);
                var s = LimitingReactant.Calculate(r, moles);
                if (s.Success)
                {
                    balanced++;
                    foreach (var p in s.Products)
                    {
                        Assert.That(double.IsNaN(p.TheoreticalMol) || double.IsInfinity(p.TheoreticalMol), Is.False, r.id);
                        Assert.That(p.TheoreticalMol, Is.GreaterThan(0), r.id + " " + p.Formula);
                        if (!double.IsNaN(p.TheoreticalGrams))
                            Assert.That(p.TheoreticalGrams, Is.EqualTo(Grams(p.Formula, p.TheoreticalMol)).Within(1e-4).Percent, r.id);
                    }
                }
                else
                {
                    refused++;
                    Assert.That(s.Products, Is.Empty, r.id);
                    Assert.That(s.Message, Is.Not.Empty, r.id);
                }
            }
            TestContext.WriteLine($"balanced {balanced}, refused {refused}");
            Assert.That(balanced, Is.GreaterThan(0));
        }

        // ── I. Precipitation with valid data ──────────────────────────

        [Test]
        public void I_SilverChloride_Precipitation_EndToEnd()
        {
            var o = Mix(25f, Amount("AgNO3", 25, "mL"), Amount("NaCl", 25, "mL"));
            Assert.That(o.Detailed.Reaction.id, Is.EqualTo("rxn_006"));
            Assert.That(o.Event.Complete, Is.True);
            var s = o.Event.Evaluation.Stoichiometry;
            Assert.That(s.IsStoichiometric, Is.True);
            var agcl = s.Products.First(p => p.Formula == "AgCl");
            Assert.That(agcl.TheoreticalMol, Is.EqualTo(0.0025).Within(1e-4).Percent);
            // CIAAW 2021 abridged: M(AgCl) = 107.87 + 35.45 = 143.32 g/mol → 0.3583 g
            Assert.That(agcl.TheoreticalGrams, Is.EqualTo(0.0025 * 143.32).Within(1e-4).Percent);
            StringAssert.Contains("AgCl(s)  2.5 mmol  = 0.358 g", o.Panel);

            // A smaller NaCl portion makes NaCl the limiting reactant.
            var limited = Mix(25f, Amount("AgNO3", 25, "mL"), Amount("NaCl", 10, "mL"));
            Assert.That(limited.Event.Evaluation.Stoichiometry.LimitingFormulas, Is.EqualTo(new[] { "NaCl" }));
            Assert.That(limited.Event.Evaluation.Stoichiometry.Products.First(p => p.Formula == "AgCl").TheoreticalMol,
                        Is.EqualTo(0.001).Within(1e-4).Percent);
        }
    }
}
