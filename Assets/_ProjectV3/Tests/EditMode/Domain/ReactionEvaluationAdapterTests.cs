using NUnit.Framework;
using ChemLabSimV3.Data;
using ChemLabSimV3.Engine;

namespace ChemLabSimV3.Tests.EditMode.Domain
{
    public class ReactionEvaluationAdapterTests
    {
        private ReactionDB CreateSingleReactionDb()
        {
            return new ReactionDB
            {
                reactions = new System.Collections.Generic.List<ReactionEntry>
                {
                    new ReactionEntry
                    {
                        id = "rxn_test_001",
                        reactantA = "HCl",
                        reactantB = "NaOH",
                        product = "NaCl",
                        requiredMedium = "Neutral",
                        activationTempC = 25f,
                        catalystAllowed = true,
                        catalystDeltaTempC = 10f
                    }
                }
            };
        }

        [Test]
        public void ToLegacyResult_WithSuccess_IsValidAndMatchesStatus()
        {
            var engine = new ReactionEngine(CreateSingleReactionDb());
            var request = new MixRequest(
                new System.Collections.Generic.List<string> { "HCl", "NaOH" },
                ReactionMedium.Neutral,
                30f,
                0.8f,
                0.8f,
                false);

            var detailed = engine.ProcessDetailed(request);
            var legacy = ReactionEvaluationAdapter.ToLegacyResult(detailed);

            Assert.IsTrue(legacy.IsValid);
            Assert.That(legacy.Status, Is.EqualTo(ReactionStatus.Success));
            Assert.That(legacy.Rate01, Is.GreaterThan(0f));
        }

        [Test]
        public void ToLegacyResult_WithNotFound_IsInvalid()
        {
            var engine = new ReactionEngine(CreateSingleReactionDb());
            var request = new MixRequest(
                new System.Collections.Generic.List<string> { "X", "Y" },
                ReactionMedium.Neutral,
                25f,
                0.5f,
                0.5f,
                false);

            var detailed = engine.ProcessDetailed(request);
            var legacy = ReactionEvaluationAdapter.ToLegacyResult(detailed);

            Assert.IsFalse(legacy.IsValid);
            Assert.That(legacy.Status, Is.EqualTo(ReactionStatus.Fail));
        }

        [Test]
        public void ToLegacyInput_PreservesMixRequestValues()
        {
            var reaction = CreateSingleReactionDb().reactions[0];
            var request = new MixRequest(
                new System.Collections.Generic.List<string> { "HCl", "NaOH" },
                ReactionMedium.Acidic,
                42f,
                0.3f,
                0.7f,
                true);

            var input = ReactionEvaluationAdapter.ToLegacyInput(request, reaction);

            Assert.That(input.reaction.id, Is.EqualTo("rxn_test_001"));
            Assert.That(input.temperatureC, Is.EqualTo(42f));
            Assert.That(input.medium, Is.EqualTo(ReactionMedium.Acidic));
            Assert.That(input.hasCatalyst, Is.True);
        }
    }
}
