using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using ChemLabSimV3.Data;
using ChemLabSimV3.Domain.GuidedExperiments;
using ChemLabSimV3.Engine;

namespace ChemLabSimV3.Tests.EditMode.Domain
{
    /// <summary>
    /// Guided experiments: the runner's session and step checks, and the shipped content
    /// (guided_experiments.json) followed step by step against the real engine and reactions.json.
    /// </summary>
    public class GuidedExperimentRunnerTests
    {
        private static ExperimentStep Step(string action, StepValidation v = null)
            => new ExperimentStep { action = action, instruction = action, validation = v ?? new StepValidation() };

        private static GuidedExperimentRunner TwoExperiments()
        {
            return new GuidedExperimentRunner(new List<GuidedExperimentDef>
            {
                new GuidedExperimentDef
                {
                    id = "a", title = "First", category = "Acid-Base", difficultyLevel = 1,
                    steps = new List<ExperimentStep>
                    {
                        Step("SelectReagents", new StepValidation { expectedReagents = new List<string> { "HCl", "NaOH" } }),
                        Step("Mix", new StepValidation { expectedReactionId = "rxn_001", expectedResultStatus = "Success" }),
                    }
                },
                new GuidedExperimentDef
                {
                    id = "b", title = "Second", category = "Gas", difficultyLevel = 2, prerequisiteExperimentId = "a",
                    steps = new List<ExperimentStep> { Step("Observe") }
                },
            });
        }

        private static LabSnapshot Lab(params string[] reagents)
            => new LabSnapshot { Reagents = reagents, TemperatureC = 25f };

        // ── Runner ─────────────────────────────────────────────────────

        [Test]
        public void Runner_KeepsFileOrder_AndFindsById()
        {
            var r = TwoExperiments();
            Assert.That(r.Count, Is.EqualTo(2));
            Assert.That(r.GetAll().Select(e => e.id), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(r.Get("B").title, Is.EqualTo("Second"));
            Assert.That(r.Get("missing"), Is.Null);
            Assert.That(r.GetByCategory("gas").Single().id, Is.EqualTo("b"));
            Assert.That(r.GetByDifficulty(1).Single().id, Is.EqualTo("a"));
            Assert.That(new GuidedExperimentRunner(null).Count, Is.EqualTo(0));
        }

        [Test]
        public void Session_AdvancesThroughEveryStep_ThenCompletes()
        {
            var r = TwoExperiments();
            var s = r.StartSession("a");
            Assert.That(r.GetCurrentStep(s).ActionType, Is.EqualTo(StepActionType.SelectReagents));
            Assert.That(r.AdvanceStep(s), Is.True);
            Assert.That(r.GetCurrentStep(s).ActionType, Is.EqualTo(StepActionType.Mix));
            Assert.That(r.AdvanceStep(s), Is.False);
            Assert.That(s.IsCompleted, Is.True);
            Assert.That(r.GetCurrentStep(s), Is.Null);
            Assert.That(r.StartSession("missing"), Is.Null);
        }

        [Test]
        public void Prerequisites_UnlockTheNextExperiment()
        {
            var r = TwoExperiments();
            Assert.That(r.IsEligible("a", new HashSet<string>()), Is.True);
            Assert.That(r.IsEligible("b", new HashSet<string>()), Is.False);
            Assert.That(r.IsEligible("b", new HashSet<string> { "a" }), Is.True);
        }

        [Test]
        public void SelectReagents_SaysWhatIsMissingOrExtra_InAnyOrder()
        {
            var step = TwoExperiments().Get("a").steps[0];
            Assert.That(GuidedExperimentRunner.Check(step, Lab("NaOH", "HCl")).Done, Is.True);
            StringAssert.Contains("choose NaOH", GuidedExperimentRunner.Check(step, Lab("HCl")).Waiting);
            StringAssert.Contains("remove Zn", GuidedExperimentRunner.Check(step, Lab("HCl", "NaOH", "Zn")).Waiting);
        }

        [Test]
        public void Mix_NeedsAMixSinceTheStepStarted_WithTheExpectedResult()
        {
            var step = TwoExperiments().Get("a").steps[1];
            var lab = Lab("HCl", "NaOH");
            Assert.That(GuidedExperimentRunner.Check(step, lab).Waiting, Is.EqualTo("press Mix"));
            lab.MixedSinceStepStart = true;
            lab.LastReactionId = "rxn_001";
            lab.LastResultStatus = "Partial";
            Assert.That(GuidedExperimentRunner.Check(step, lab).Done, Is.False);
            lab.LastResultStatus = "Success";
            Assert.That(GuidedExperimentRunner.Check(step, lab).Done, Is.True);
        }

        [Test]
        public void Settings_AreCheckedWithReasons()
        {
            var hot = Step("SetTemperature", new StepValidation { minTemperature = 500 });
            StringAssert.Contains("at least 500 °C", GuidedExperimentRunner.Check(hot, Lab()).Waiting);
            var cat = Step("ToggleCatalyst", new StepValidation { catalyst = 1 });
            StringAssert.Contains("catalyst on", GuidedExperimentRunner.Check(cat, Lab()).Waiting);
            var cur = Step("ToggleCurrent", new StepValidation { current = 1 });
            var lab = Lab();
            lab.Current = true;
            Assert.That(GuidedExperimentRunner.Check(cur, lab).Done, Is.True);
            Assert.That(GuidedExperimentRunner.Check(Step("Observe"), lab).Done, Is.False);   // Next button
            Assert.That(Step("NoSuchAction").ActionType, Is.EqualTo(StepActionType.Observe));
        }

        // ── Shipped content against the real engine ────────────────────

        private static GuidedExperimentCatalog LoadCatalog()
        {
            var asset = Resources.Load<TextAsset>("Data/guided_experiments");
            Assert.That(asset, Is.Not.Null);
            return JsonUtility.FromJson<GuidedExperimentCatalog>(asset.text);
        }

        [Test]
        public void ShippedContent_LoadsWithUnsetValuesLeftUnset()
        {
            var cat = LoadCatalog();
            Assert.That(cat.experiments.Count, Is.GreaterThanOrEqualTo(8));
            var first = cat.experiments[0].steps[0];
            Assert.That(first.ActionType, Is.EqualTo(StepActionType.SelectReagents));
            Assert.That(float.IsNaN(first.validation.minTemperature), Is.True, "a missing number must stay unset");
            Assert.That(first.validation.catalyst, Is.EqualTo(-1), "a missing switch must stay 'any'");
            foreach (var e in cat.experiments)
            {
                Assert.That(string.IsNullOrEmpty(e.title) || string.IsNullOrEmpty(e.conclusion), Is.False, e.id);
                foreach (var s in e.steps)
                {
                    Assert.That(System.Enum.IsDefined(typeof(StepActionType), s.action ?? ""), Is.True, $"{e.id}: '{s.action}'");
                    Assert.That(string.IsNullOrEmpty(s.instruction) || string.IsNullOrEmpty(s.explanation), Is.False, e.id);
                }
                if (!string.IsNullOrEmpty(e.prerequisiteExperimentId))
                    Assert.That(cat.experiments.Any(x => x.id == e.prerequisiteExperimentId), Is.True, e.id);
            }
        }

        [Test]
        public void ShippedContent_EveryExperimentCanBeCompleted_OnTheShippedData()
        {
            string path = Path.Combine(UnityEngine.Application.dataPath, "_Project", "DataSrc", "reactions.json");
            var engine = new ReactionEngine(JsonUtility.FromJson<ReactionDB>(File.ReadAllText(path)));

            foreach (var e in LoadCatalog().experiments)
            {
                // A student who does exactly what each step asks, starting from the lab's defaults.
                var reagents = new List<string>();
                float temperature = 25f, grinding = 0f;
                bool catalyst = false, current = false;

                for (int i = 0; i < e.steps.Count; i++)
                {
                    var step = e.steps[i];
                    var v = step.validation;
                    if (v.expectedReagents.Count > 0) { reagents.Clear(); reagents.AddRange(v.expectedReagents); }
                    if (!float.IsNaN(v.minTemperature)) temperature = Mathf.Max(temperature, v.minTemperature);
                    if (!float.IsNaN(v.maxTemperature)) temperature = Mathf.Min(temperature, v.maxTemperature);
                    if (!float.IsNaN(v.minGrinding)) grinding = Mathf.Max(grinding, v.minGrinding);
                    if (v.catalyst >= 0) catalyst = v.catalyst == 1;
                    if (v.current >= 0) current = v.current == 1;

                    var lab = new LabSnapshot
                    {
                        Reagents = reagents.ToArray(), TemperatureC = temperature, Grinding = grinding,
                        Catalyst = catalyst, Current = current
                    };
                    if (step.ActionType == StepActionType.Mix)
                    {
                        var request = new MixRequest(new List<string>(reagents), ReactionMedium.Neutral, temperature, 0.5f, grinding, catalyst)
                        { HasElectricCurrent = current };
                        var detailed = engine.ProcessDetailed(request);
                        lab.MixedSinceStepStart = true;
                        lab.LastReactionId = detailed.Found ? detailed.Reaction.id : null;
                        lab.LastResultStatus = detailed.Found
                            ? GuidedExperimentRunner.StatusOf(ReactionEvaluationAdapter.ToLegacyResult(detailed))
                            : "NotFound";
                    }
                    if (step.ActionType == StepActionType.Observe) continue;

                    var check = GuidedExperimentRunner.Check(step, lab);
                    Assert.That(check.Done, Is.True,
                        $"{e.id} step {i + 1} ({step.action}): {check.Waiting} — got {lab.LastReactionId ?? "-"} {lab.LastResultStatus}");
                }
            }
        }
    }
}
