using NUnit.Framework;
using ChemLabSimV3.Engine;

namespace ChemLabSimV3.Tests.EditMode.Domain
{
    /// <summary>
    /// Tests for the ConditionPipeline — verifies condition evaluation logic.
    /// The pipeline evaluates a ReactionEntry against a ConditionInput.
    /// </summary>
    public class ConditionPipelineTests
    {
        private static ReactionEntry CreateSampleReaction()
        {
            return new ReactionEntry
            {
                id = "rxn_test_001",
                reactantA = "HCl",
                reactantB = "NaOH",
                product = "NaCl",
                requiredMedium = "Neutral",
                activationTempC = 25f,
                catalystAllowed = true,
                catalystDeltaTempC = 10f
            };
        }

        [Test]
        public void CreateDefault_ReturnsNonNullPipeline()
        {
            var pipeline = ConditionPipeline.CreateDefault();
            Assert.IsNotNull(pipeline);
        }

        [Test]
        public void Evaluate_WithPerfectConditions_ReturnsSuccess()
        {
            var pipeline = ConditionPipeline.CreateDefault();
            var reaction = CreateSampleReaction();
            var input = new ConditionInput
            {
                TemperatureC = 50f,
                Stirring = 1f,
                Grinding = 1f,
                Medium = ReactionMedium.Neutral,
                HasCatalyst = true,
                EffectiveActivationC = 15f, // catalyst lowers from 25 to 15
                ContactFactor = 1.6f,
                PressureAtm = 1f
            };

            var result = pipeline.Evaluate(reaction, input);
            Assert.IsNotNull(result);
            Assert.That(result.OverallStatus, Is.EqualTo(ReactionStatus.Success));
            Assert.That(result.AnyFailed, Is.False);
        }

        [Test]
        public void Evaluate_WithWrongMedium_ReturnsFailure()
        {
            var pipeline = ConditionPipeline.CreateDefault();
            var reaction = CreateSampleReaction();
            var input = new ConditionInput
            {
                TemperatureC = 50f,
                Stirring = 1f,
                Grinding = 1f,
                Medium = ReactionMedium.Acidic, // reaction requires Neutral
                HasCatalyst = true,
                EffectiveActivationC = 20f,
                ContactFactor = 1.6f,
                PressureAtm = 1f
            };

            var result = pipeline.Evaluate(reaction, input);
            Assert.IsNotNull(result);
            Assert.That(result.OverallStatus, Is.EqualTo(ReactionStatus.Fail));
        }

        [Test]
        public void Evaluate_WithLowTemperature_BelowActivation()
        {
            var pipeline = ConditionPipeline.CreateDefault();
            var reaction = CreateSampleReaction();
            var input = new ConditionInput
            {
                TemperatureC = 10f,
                Stirring = 0f,
                Grinding = 0f,
                Medium = ReactionMedium.Neutral,
                HasCatalyst = false,
                EffectiveActivationC = 25f,
                ContactFactor = 0.6f,
                PressureAtm = 1f
            };

            var result = pipeline.Evaluate(reaction, input);
            Assert.IsNotNull(result);
            Assert.That(result.OverallStatus, Is.EqualTo(ReactionStatus.Fail));
        }

        [Test]
        public void PipelineResult_ContainsConditionsList()
        {
            var pipeline = ConditionPipeline.CreateDefault();
            var reaction = CreateSampleReaction();
            var input = new ConditionInput
            {
                TemperatureC = 30f,
                Stirring = 0.5f,
                Grinding = 0.5f,
                Medium = ReactionMedium.Neutral,
                HasCatalyst = false,
                EffectiveActivationC = 25f,
                ContactFactor = 1.0f,
                PressureAtm = 1f
            };

            var result = pipeline.Evaluate(reaction, input);
            Assert.IsNotNull(result.Conditions);
            Assert.That(result.Conditions.Count, Is.GreaterThanOrEqualTo(4));
            Assert.That(result.OverallRate, Is.GreaterThanOrEqualTo(0f));
        }
    }
}
